using System;
using System.Threading.Tasks;
using Pixygon.Saving;
using UnityEngine;
using UnityEngine.Networking;

namespace Pixygon.Passport
{
    public class PixygonApi : MonoBehaviour
    {
        // Production API. If we ever need a staging swap, this is the single
        // line to touch.
        private const string PixygonServerURL = "https://api.pixygon.com/v1/";
        public bool IsLoggedIn { get; private set; }
        public bool IsLoggingIn { get; private set; }
        public LoginToken AccountData { get; private set; }
        public static PixygonApi Instance;

        /// <summary>
        /// Fired whenever the login state, account data, or owned games list
        /// changes. Consumers downstream of Passport (e.g. Dreadwager's
        /// PaidEdition gate) listen so they can react to a mid-session login
        /// without re-loading the scene.
        /// </summary>
        public event Action OnLoginStateChanged;

        // Remember-me prefs keys. We previously stored the user's password
        // in PlayerPrefs — that's both a security smell and a fragile coupling
        // to "the password the user typed once". We now persist the JWT-style
        // access token + refresh token + userId and rehydrate the session by
        // refreshing the token, not by re-logging in with the password.
        // The access token expires in ~7 days and is sent on every API call;
        // the refresh token expires in ~30 days and is only sent to
        // /auth/refresh to mint a new access token + refresh token pair.
        private const string PrefRememberMe = "Pixygon.RememberMe";
        private const string PrefUserId = "Pixygon.UserId";
        private const string PrefToken = "Pixygon.Token";
        private const string PrefRefreshToken = "Pixygon.RefreshToken";
        // Legacy keys we still read once at boot for users upgrading from an
        // older client. Cleared after successful migration.
        private const string LegacyPrefRemember = "RememberMe";
        private const string LegacyPrefUsername = "Username";
        private const string LegacyPrefPassword = "Password";

        /// <summary>
        /// Asset ids of games the logged-in account owns. Populated in the
        /// background after login by <see cref="FetchOwnedGames"/>. Empty
        /// when not logged in or when the request hasn't completed yet —
        /// callers should treat absence as "we don't know yet", not "no".
        /// </summary>
        public string[] OwnedGameIds { get; private set; } = System.Array.Empty<string>();

        /// <summary>
        /// True if the logged-in account is known to own the given game.
        /// False when not logged in, when the owned-games fetch hasn't
        /// completed, or when the game id genuinely isn't owned.
        /// </summary>
        public bool OwnsGame(string gameId)
        {
            if (string.IsNullOrEmpty(gameId) || OwnedGameIds == null) return false;
            for (var i = 0; i < OwnedGameIds.Length; i++)
            {
                if (OwnedGameIds[i] == gameId) return true;
            }
            return false;
        }

        private async void Awake()
        {
            // Correct singleton: when a fresh scene instantiates its own
            // PixygonApi MonoBehaviour but the persistent Instance already
            // exists, kill the *new* one and bail. The previous code did
            // `Destroy(Instance)` which threw away the authenticated, in-
            // memory AccountData and re-ran the whole token-refresh on
            // every scene load — so a user without "Remember me" checked
            // appeared logged out the moment they returned to the Hub.
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // Survive scene transitions so AccountData / OwnedGameIds / the
            // OnLoginStateChanged subscribers all persist Menu → Hub → Game → Hub.
            DontDestroyOnLoad(gameObject);
            // Migrate older saves: lift any legacy RememberMe entry into the
            // new token-based scheme. The legacy entry stored the user's
            // password — clear it whether or not we end up logged in so it
            // doesn't sit around on disk.
            MigrateLegacyRememberMe();
            if (PlayerPrefs.GetInt(PrefRememberMe) != 1) return;
            IsLoggingIn = true;
            // Try to refresh the cached session. We pass the REFRESH token
            // (not the access token) — the spec for /auth/refresh is to send
            // the longer-lived refresh token in the bearer header so the
            // server can mint a fresh access+refresh pair. If the refresh
            // token is missing, expired, or revoked we silently clear the
            // cache and surface the login UI as if no session was saved.
            AccountData = await RefreshSession(
                PlayerPrefs.GetString(PrefUserId),
                PlayerPrefs.GetString(PrefRefreshToken));
            IsLoggingIn = false;
            // Validate the refreshed session. We reject and bail to the
            // interactive login on any of:
            //   - null token (server didn't mint one)
            //   - null user (server returned just a token)
            //   - empty user._id (response shape mismatch — happens when the
            //     server serialises as "id" but we expect "_id"; every later
            //     call would 404 on users//, FetchOwnedGames especially).
            if (AccountData == null
                || string.IsNullOrEmpty(AccountData.token)
                || AccountData.user == null
                || string.IsNullOrEmpty(AccountData.user._id))
            {
                Debug.LogWarning("[PixygonApi] Refresh returned an incomplete session — clearing remember-me and surfacing the login screen.");
                ClearRememberMe();
                SaveManager.SettingsSave._user = null;
                SaveManager.SettingsSave._isLoggedIn = false;
                AccountData = null;
                return;
            }
            IsLoggedIn = true;
            PersistRememberMe(AccountData);
            SaveManager.SettingsSave._user = AccountData.user;
            SaveManager.SettingsSave._isLoggedIn = true;
            SetLatestActivity("Online", "", "");
            OnLoginStateChanged?.Invoke();
            // Owned-games is a background fetch — login UI doesn't block on it.
            // The fetch itself fires OnLoginStateChanged again when complete.
            _ = FetchOwnedGames();
        }

        private void MigrateLegacyRememberMe() {
            if (PlayerPrefs.GetInt(LegacyPrefRemember) != 1) return;
            // We can't refresh with a legacy save (we don't have a token), so
            // just clear it. Next login will use the new scheme.
            PlayerPrefs.DeleteKey(LegacyPrefRemember);
            PlayerPrefs.DeleteKey(LegacyPrefUsername);
            PlayerPrefs.DeleteKey(LegacyPrefPassword);
            PlayerPrefs.Save();
        }

        private static void PersistRememberMe(LoginToken token) {
            if (token == null || token.user == null) return;
            PlayerPrefs.SetInt(PrefRememberMe, 1);
            PlayerPrefs.SetString(PrefUserId, token.user._id ?? string.Empty);
            PlayerPrefs.SetString(PrefToken, token.token ?? string.Empty);
            PlayerPrefs.SetString(PrefRefreshToken, token.refreshToken ?? string.Empty);
            PlayerPrefs.Save();
        }

        private static void ClearRememberMe() {
            PlayerPrefs.DeleteKey(PrefRememberMe);
            PlayerPrefs.DeleteKey(PrefUserId);
            PlayerPrefs.DeleteKey(PrefToken);
            PlayerPrefs.DeleteKey(PrefRefreshToken);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Trade a cached refresh token for a fresh access + refresh pair.
        /// The server expects the refresh token in the Authorization header
        /// (not the access token). Returns null if the refresh token is
        /// missing, expired, or revoked — caller should fall back to
        /// interactive login.
        /// </summary>
        private async Task<LoginToken> RefreshSession(string userId, string refreshToken) {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(refreshToken)) return null;
            var www = await GetWWW($"auth/refresh/{userId}", refreshToken);
            if (!string.IsNullOrWhiteSpace(www.error)) return null;
            var body = www.downloadHandler.text;
            if (string.IsNullOrWhiteSpace(body) || body == "null") return null;
            try {
                return JsonUtility.FromJson<LoginToken>(body);
            } catch {
                return null;
            }
        }
        private void OnApplicationQuit()
        {
            SetLatestActivity("Offline", "", "");
        }
        public async void StartLogin(string user, string pass, bool rememberMe = false, Action onLogin = null, Action<ErrorResponse> onFail = null)
        {
            AccountData = await LogIn(user, pass, onFail);
            if (AccountData != null)
            {
                SaveManager.SettingsSave._user = AccountData.user;
                SaveManager.SettingsSave._isLoggedIn = true;
                // Persist only the token after a successful login. We don't
                // touch the prefs on a failed login so a typo'd password
                // doesn't blow away the previously-remembered session.
                if (rememberMe) PersistRememberMe(AccountData);
                OnLoginStateChanged?.Invoke();
                // Owned-games fires in the background — the login UI doesn't
                // block on it, and a slow API response can't stall the boot.
                _ = FetchOwnedGames();
            }
            onLogin?.Invoke();
        }
        public async void StartSignup(string user, string email, string pass, bool rememberMe = false, Action onSignup = null, Action<ErrorResponse> onFail = null)
        {
            // Signup doesn't auto-login (the user still has to verify their
            // email), so there's nothing to remember yet. The rememberMe
            // intent is propagated through the subsequent StartLogin call.
            await Signup(user, email, pass, onFail);
            onSignup?.Invoke();
        }
        public static async void VerifyUser(string user, int code, Action onVerify = null, Action<ErrorResponse> onFail = null)
        {
            var www = await PostWWW("auth/verify", JsonUtility.ToJson(new VerifyData(user, code)));
            if (!string.IsNullOrWhiteSpace(www.error))
            {
                onFail?.Invoke(new ErrorResponse(www.error, www.downloadHandler.text));
                return;
            }
            onVerify?.Invoke();
        }
        public static async void ForgotPassword(string email, Action onVerify = null, Action<ErrorResponse> onFail = null)
        {
            var www = await PostWWW("auth/forgotPassword", JsonUtility.ToJson(new RecoveryData(email)));
            if (!string.IsNullOrWhiteSpace(www.error))
            {
                onFail?.Invoke(new ErrorResponse(www.error, www.downloadHandler.text));
                return;
            }
            onVerify?.Invoke();
        }
        public static async void ForgotPasswordRecovery(string email, string hash, string newPass, Action onVerify = null, Action<ErrorResponse> onFail = null)
        {
            var www = await PostWWW("auth/forgotPasswordRecovery", JsonUtility.ToJson(new RecoverySubmitData(email, hash, newPass)));
            if (!string.IsNullOrWhiteSpace(www.error))
            {
                onFail?.Invoke(new ErrorResponse(www.error, www.downloadHandler.text));
                return;
            }
            onVerify?.Invoke();
        }
        public async void PatchWaxWallet(string wallet)
        {
            var www = await PostWWW($"users/wax/{wallet}", "", true, AccountData.token);
            AccountData.user = JsonUtility.FromJson<AccountData>(www.downloadHandler.text);
            SaveManager.SettingsSave._user = AccountData.user;
        }
        public async void PatchEthWallet(string wallet)
        {
            Debug.Log("Patching eth-wallet");
            var www = await PostWWW($"users/eth/{wallet}", "", true, AccountData.token);
            Debug.Log("EthWallet Patch: " + www.downloadHandler.text);
        }
        public async void PatchTezWallet(string wallet)
        {
            Debug.Log("Patching tez-wallet");
            var www = await PostWWW($"users/tez/{wallet}", "", true, AccountData.token);
            Debug.Log("TezWallet Patch: " + www.downloadHandler.text);
        }
        public async void PatchMatWallet(string wallet)
        {
            Debug.Log("Patching matic-wallet");
            var www = await PostWWW($"users/mat/{wallet}", "", true, AccountData.token);
            Debug.Log("MaticWallet Patch: " + www.downloadHandler.text);
        }
        public async void PatchImxWallet(string wallet)
        {
            Debug.Log("Patching imx-wallet");
            var www = await PostWWW($"users/imx/{wallet}", "", true, AccountData.token);
            Debug.Log("ImxWallet Patch: " + www.downloadHandler.text);
        }
        public async void PatchTwitchAccount(string twitchAccount)
        {
            Debug.Log("Patching twitch-account");
            var www = await PostWWW($"users/twitch/{twitchAccount}", "", true, AccountData.token);
            Debug.Log("Twitch-account Patch: " + www.downloadHandler.text);
        }
        public async void PatchDreadwagerSkin(int i)
        {
            Debug.Log("Patching Dreadwager Skin");
            var www = await PostWWW($"users/addDreadwagerSkin/{i}", "", true, AccountData.token);
            Debug.Log("Dreadwager Skin Patch: " + www.downloadHandler.text);
        }

        // ---- Owned games & purchases --------------------------------------

        /// <summary>
        /// Refresh the list of games this account owns. Called automatically
        /// after a successful login / refresh, but exposed publicly so a
        /// store flow can invalidate the cache after a purchase completes.
        /// Safe to invoke when not logged in — no-ops and clears the cache.
        /// </summary>
        public async Task FetchOwnedGames()
        {
            // Skip the fetch entirely if we don't have a usable user id —
            // would hit /v1/users//ownedGames and 404, polluting logs.
            if (AccountData == null
                || AccountData.user == null
                || string.IsNullOrEmpty(AccountData.user._id))
            {
                OwnedGameIds = System.Array.Empty<string>();
                return;
            }
            var www = await GetWWW($"users/{AccountData.user._id}/ownedGames", AccountData.token);
            if (!string.IsNullOrWhiteSpace(www.error))
            {
                Debug.Log($"[PixygonApi] FetchOwnedGames failed: {www.error}");
                return;
            }
            var body = www.downloadHandler.text;
            if (string.IsNullOrWhiteSpace(body) || body == "null")
            {
                OwnedGameIds = System.Array.Empty<string>();
                return;
            }
            try
            {
                // Server returns a raw JSON array of game ids. JsonUtility
                // can't deserialize a top-level array, so we wrap and unwrap.
                var wrapped = "{\"ids\":" + body + "}";
                var holder = JsonUtility.FromJson<OwnedGamesResponse>(wrapped);
                OwnedGameIds = holder != null && holder.ids != null
                    ? holder.ids : System.Array.Empty<string>();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PixygonApi] FetchOwnedGames parse failed: {e.Message}; body={body}");
                OwnedGameIds = System.Array.Empty<string>();
            }
            // PaidEdition / other entitlement-aware UI listens here so it
            // can re-evaluate without polling.
            OnLoginStateChanged?.Invoke();
        }

        // ---- Player-authored content (death messages, small wins) ---------

        /// <summary>
        /// POST an authored death message attributed to the current logged-in
        /// account. Server ties it to the user's id so the message board can
        /// show "RIP from {username}". Falls back to anonymous if not
        /// logged in; the server still accepts the post but won't attribute it.
        /// </summary>
        public async void PostDeathMessage(string gameId, string text, Action onPosted = null, Action<ErrorResponse> onFail = null)
        {
            var token = AccountData?.token ?? string.Empty;
            var www = await PostWWW($"{gameId}/deathMessage", JsonUtility.ToJson(new SocialMessage(text)), false, token);
            if (!string.IsNullOrWhiteSpace(www.error))
            {
                onFail?.Invoke(new ErrorResponse(www.error, www.downloadHandler.text));
                return;
            }
            onPosted?.Invoke();
        }

        /// <summary>
        /// POST a "small win" — a short blurb the player wants to share on the
        /// in-game message board ("Beat the third boss with only one helper!").
        /// Server attributes to the logged-in user; anonymous fallback like
        /// <see cref="PostDeathMessage"/>.
        /// </summary>
        public async void PostSmallWin(string gameId, string text, Action onPosted = null, Action<ErrorResponse> onFail = null)
        {
            var token = AccountData?.token ?? string.Empty;
            var www = await PostWWW($"{gameId}/smallWin", JsonUtility.ToJson(new SocialMessage(text)), false, token);
            if (!string.IsNullOrWhiteSpace(www.error))
            {
                onFail?.Invoke(new ErrorResponse(www.error, www.downloadHandler.text));
                return;
            }
            onPosted?.Invoke();
        }
        public async void PatchGameXp(int i)
        {
            Debug.Log("Patching Game XP");
            var www = await PostWWW($"users/gameXp/{i}", "", true, AccountData.token);
            Debug.Log("Patching Game XP: " + www.downloadHandler.text);
        }
        public async void PatchStreamerXp(int i)
        {
            Debug.Log("Patching Streamer XP");
            var www = await PostWWW($"users/streamerXp/{i}", "", true, AccountData.token);
            Debug.Log("Patching Streamer XP: " + www.downloadHandler.text);
        }
        public async void PatchViewerXp(int i, string id)
        {
            Debug.Log("Patching Viewer XP");
            var www = await PostWWW($"users/viewerXp/{i}/{id}", "", true);
            Debug.Log("Patching Viewer XP: " + www.downloadHandler.text);
        }
        public async void DeleteUser(Action onVerify = null, Action<ErrorResponse> onFail = null)
        {
            // Capture the token before we clear local state — the previous
            // version cleared AccountData first and then immediately NRE'd on
            // AccountData.token. We now snapshot, clear, then fire the
            // server-side delete with the snapshotted token.
            var token = AccountData?.token ?? string.Empty;
            ClearRememberMe();
            SaveManager.SettingsSave._user = null;
            SaveManager.SettingsSave._isLoggedIn = false;
            AccountData = null;
            IsLoggedIn = false;
            OwnedGameIds = System.Array.Empty<string>();
            var www = await GetWWW($"users/delete", token);
            if (!string.IsNullOrWhiteSpace(www.error))
            {
                onFail?.Invoke(new ErrorResponse(www.error, www.downloadHandler.text));
                return;
            }
            onVerify?.Invoke();
        }
        public static async Task<AccountData> GetUser(string userId)
        {
            var www = await GetWWW($"users/view/{userId}");
            if (!string.IsNullOrWhiteSpace(www.error))
            {
                Debug.Log("GET USER ERROR!! " + www.error + " and this " + www.downloadHandler.text);
                return null;
            }
            if (string.IsNullOrWhiteSpace(www.downloadHandler.text) || www.downloadHandler.text == "null") return null;
            return JsonUtility.FromJson<AccountData>(www.downloadHandler.text);
        }
        public async Task<AccountData> GetUserFromTwitch(string twitchId)
        {
            var www = await GetWWW($"users/twitch/{twitchId}");
            if (!string.IsNullOrWhiteSpace(www.error))
            {
                Debug.Log("GET USER ERROR!! " + www.error + " and this " + www.downloadHandler.text);
                return null;
            }
            Debug.Log(www.downloadHandler.text);
            if (string.IsNullOrWhiteSpace(www.downloadHandler.text) || www.downloadHandler.text == "null") return null;
            return JsonUtility.FromJson<AccountData>(www.downloadHandler.text);
        }
        public async Task<string> FollowUser(string followId)
        {
            var www = await PostWWW($"users/follow/{followId}", "", true, AccountData.token);
            if (!string.IsNullOrWhiteSpace(www.error))
            {
                Debug.Log("FOLLOW USER ERROR!! " + www.error + " and this " + www.downloadHandler.text);
                return null;
            }
            await RefreshUser();
            return "{\"_results\":" + www.downloadHandler.text + "}";
        }
        public async Task<string> GetFollowing(string userId)
        {
            var www = await GetWWW($"users/following/{userId}");
            if (!string.IsNullOrWhiteSpace(www.error))
            {
                Debug.Log("GET FOLLOWING ERROR!! " + www.error + " and this " + www.downloadHandler.text);
                return null;
            }
            Debug.Log(www.downloadHandler.text);
            return "{\"_results\":" + www.downloadHandler.text + "}";
        }
        public async Task<string> GetFollowers(string userId)
        {
            var www = await GetWWW($"users/followers/{userId}");
            if (!string.IsNullOrWhiteSpace(www.error))
            {
                Debug.Log("GET FOLLOWERS ERROR!! " + www.error + " and this " + www.downloadHandler.text);
                return null;
            }
            Debug.Log(www.downloadHandler.text);
            return "{\"_results\":" + www.downloadHandler.text + "}";
        }


        //SEARCH
        public async Task<string> UserSearch(string searchString)
        {
            var www = await GetWWW("users/s/" + searchString);
            if (string.IsNullOrWhiteSpace(www.error)) return "{\"_results\":" + www.downloadHandler.text + "}";
            Debug.Log("GET USER SEARCH ERROR!! " + www.error + " and this " + www.downloadHandler.text);
            return null;
        }
        public async Task<string> CollectionSearch(string searchString)
        {
            var www = await GetWWW("users/s/" + searchString);
            if (string.IsNullOrWhiteSpace(www.error)) return "{\"_results\":" + www.downloadHandler.text + "}";
            Debug.Log("GET COLLECTION SEARCH ERROR!! " + www.error + " and this " + www.downloadHandler.text);
            return null;
        }
        public async Task<string> NftSearch(string searchString)
        {
            var www = await GetWWW("users/s/" + searchString);
            if (!string.IsNullOrWhiteSpace(www.error))
            {
                Debug.Log("GET NFT SEARCH ERROR!! " + www.error + " and this " + www.downloadHandler.text);
                return null;
            }
            return "{\"_results\":" + www.downloadHandler.text + "}";
        }
        public void StartLogout()
        {
            ClearRememberMe();
            SaveManager.SettingsSave._user = null;
            SaveManager.SettingsSave._isLoggedIn = false;
            AccountData = null;
            IsLoggedIn = false;
            OwnedGameIds = System.Array.Empty<string>();
            OnLoginStateChanged?.Invoke();
        }
        public async Task<Savedata> GetSave(string gameId, int slot)
        {
            var www = await GetWWW($"savedata/{gameId}/{AccountData.user._id}/{slot}", AccountData.token);
            if (!string.IsNullOrWhiteSpace(www.error))
            {
                Debug.Log("GET SAVE ERROR!! " + www.error + " and this " + www.downloadHandler.text);
                return null;
            }
            if (string.IsNullOrWhiteSpace(www.downloadHandler.text) || www.downloadHandler.text == "null") return null;
            return JsonUtility.FromJson<Savedata>(www.downloadHandler.text);
        }
        public async Task<Savedata> PostSave(string gameId, int slot, string savedata)
        {
            var www = await PostWWW($"savedata/{gameId}/{AccountData.user._id}/{slot}", JsonUtility.ToJson(new Savejson(savedata)), false, AccountData.token);
            if (string.IsNullOrWhiteSpace(www.error)) return JsonUtility.FromJson<Savedata>(www.downloadHandler.text);
            Debug.Log("POST SAVE ERROR!! " + www.error + " and this " + www.downloadHandler.text);
            return null;
        }
        public async void PatchSave(Savedata savedata)
        {
            await PostWWW($"savedata/{savedata._id}", JsonUtility.ToJson(new Savejson(savedata.savejson)), true, AccountData.token);
        }
        private async Task<LoginToken> LogIn(string user, string pass, Action<ErrorResponse> onFail = null)
        {
            IsLoggingIn = true;
            var www = await PostWWW("auth/login", JsonUtility.ToJson(new LoginData(user, pass)));
            if (!string.IsNullOrWhiteSpace(www.error))
            {
                onFail?.Invoke(new ErrorResponse(www.error, www.downloadHandler.text));
                IsLoggingIn = false;
                return null;
            }
            IsLoggedIn = true;
            IsLoggingIn = false;
            return JsonUtility.FromJson<LoginToken>(www.downloadHandler.text);
        }
        private async Task<LoginToken> Signup(string user, string email, string pass, Action<ErrorResponse> onFail = null)
        {
            var www = await PostWWW("auth/register", JsonUtility.ToJson(new SignupData(user, email, pass)));
            if (!string.IsNullOrWhiteSpace(www.error))
            {
                onFail?.Invoke(new ErrorResponse(www.error, www.downloadHandler.text));
                return null;
            }
            IsLoggedIn = false;
            IsLoggingIn = false;
            return JsonUtility.FromJson<LoginToken>(www.downloadHandler.text);
        }
        public async void GetUsers()
        {
            await GetWWW("Users");
        }
        public async void GetFeedback()
        {
            await GetWWW("client/feedbacks");
        }
        public static async Task<string> GetHighScores(string gameId, string scoretype, string version, string fromDate, string toDate)
        {
            var www = await GetWWW($"highscores/{gameId}/{scoretype}/{version}/{fromDate}/{toDate}");
            Debug.Log(www.url + www.error + www.downloadHandler.text);
            return www.downloadHandler.text;
        }
        public static async void PostHighScore(string score)
        {
            var www = await PostWWW("highscores", score);
        }
        public async void PostFeedback(Feedback feedback)
        {
            var www = await PostWWW("client/feedbacks", JsonUtility.ToJson(feedback));
            Debug.Log($"Feedback: {www.downloadHandler.text}");
        }
        public async void SetAsPfp(string chain, string hash, Action<ErrorResponse> onFail = null)
        {
            Debug.Log("On set pfp!");
            var www = await PostWWW($"users/setPfp", JsonUtility.ToJson(new PfpData(chain, hash)), false, AccountData.token);
            if (string.IsNullOrWhiteSpace(www.error)) return;
            onFail?.Invoke(new ErrorResponse(www.error, www.downloadHandler.text));
        }
        public async Task<string> LoveCollection(string collectionName)
        {
            var www = await PostWWW($"users/loveWaxCollection/{collectionName}", "", true, AccountData.token);
            if (!string.IsNullOrWhiteSpace(www.error))
            {
                Debug.Log("FOLLOW COLLECTION ERROR!! " + www.error + " and this " + www.downloadHandler.text);
                return null;
            }
            await RefreshUser();
            return "{\"_results\":" + www.downloadHandler.text + "}";
        }
        public async Task<string> GetLovedCollections(string userId)
        {
            var www = await GetWWW($"users/{userId}/lovedWaxCollections");
            if (!string.IsNullOrWhiteSpace(www.error))
            {
                Debug.Log("GET FOLLOWING ERROR!! " + www.error + " and this " + www.downloadHandler.text);
                return null;
            }
            Debug.Log(www.downloadHandler.text);
            return "{\"_results\":" + www.downloadHandler.text + "}";
        }
        public static async Task<string> GetGame(string gameId)
        {
            var www = await GetWWW($"client/game/{gameId}");
            if (!string.IsNullOrWhiteSpace(www.error))
            {
                Debug.Log("GET FOLLOWING ERROR!! " + www.error + " and this " + www.downloadHandler.text);
                return null;
            }
            Debug.Log(www.downloadHandler.text);
            return "{\"_results\":" + www.downloadHandler.text + "}";
        }
        public async void SetProfile(string bio, string displayName, string[] links, Action<ErrorResponse> onFail = null)
        {
            Debug.Log("Patching profile");
            var www = await PostWWW($"users/setProfile", JsonUtility.ToJson(new ProfileData(bio, displayName, links)), false, AccountData.token);
            await RefreshUser();
            onFail?.Invoke(new ErrorResponse(www.error, www.downloadHandler.text));
        }
        public async void SetLatestActivity(string activity, string subactivity, string gameId)
        {
            Debug.Log("Set latest activity");
            var www = await PostWWW($"users/activity", JsonUtility.ToJson(new Activity(activity, subactivity, gameId)), false, AccountData.token);
            //await RefreshUser();
            if (www.error != null)
            {
                Debug.Log(www.error + "\n" + www.downloadHandler.text);
            }
            //onFail?.Invoke( new ErrorResponse(www.error, www.downloadHandler.text));
        }

        private static async Task<UnityWebRequest> GetWWW(string path, string token = "")
        {
            var www = UnityWebRequest.Get(PixygonServerURL + path);
            www.timeout = 60;
            if (token != string.Empty)
                www.SetRequestHeader("Authorization", $"Bearer {token}");
            www.SendWebRequest();
            while (!www.isDone)
                await Task.Yield();
            return www;
        }
        private static async Task<UnityWebRequest> PostWWW(string path, string body, bool patch = false, string token = "")
        {
            var www = UnityWebRequest.Put(PixygonServerURL + path, body);
            www.timeout = 30;
            www.method = patch ? "PATCH" : "POST";
            if (token != string.Empty)
                www.SetRequestHeader("Authorization", $"Bearer {token}");
            www.SetRequestHeader("Content-Type", "application/json");
            www.SendWebRequest();
            while (!www.isDone)
                await Task.Yield();
            return www;
        }
        private async Task RefreshUser()
        {
            AccountData.user = await GetUser(AccountData.user._id);
            SaveManager.SettingsSave._user = AccountData.user;
            Debug.Log("Refreshed user!!");
            //SaveManager.SettingsSave._user.waxWallet = AccountData.user.waxWallet;
        }

        public async Task<string> MintAsset(ItemBoxSlots items)
        {
            var www = await PostWWW($"users/mintAssets", JsonUtility.ToJson(items), false, AccountData.token);
            if (www.error != null)
            {
                Debug.Log(www.error + "\n" + www.downloadHandler.text);
            }
            await RefreshUser();
            return www.downloadHandler.text;
        }
        public async Task<string> DepositItems(ItemBoxSlots items)
        {
            var itemString = JsonUtility.ToJson(items);
            Debug.Log("Deposit Items: " + itemString);
            var www = await PostWWW($"users/depositItems", itemString, false, AccountData.token);
            if (www.error != null)
            {
                Debug.Log(www.error + "\n" + www.downloadHandler.text);
            }
            await RefreshUser();
            return www.downloadHandler.text;
        }

        public async Task<string> WithdrawItems(ItemBoxSlots items)
        {
            var itemString = JsonUtility.ToJson(items);
            Debug.Log("Withdraw Items");
            var www = await PostWWW($"users/withdrawItems", itemString, false, AccountData.token);
            if (www.error != null)
            {
                Debug.Log(www.error + "\n" + www.downloadHandler.text);
            }
            await RefreshUser();
            return www.downloadHandler.text;
        }
        public async Task<ItemBoxSlot[]> GetItems()
        {
            Debug.Log("Get Items");
            var www = await GetWWW($"users/getItemBox", AccountData.token);
            if (www.error != null)
            {
                Debug.Log(www.error + "\n" + www.downloadHandler.text);
            }
            return JsonUtility.FromJson<ItemBoxSlots>("{\"slots\":" + www.downloadHandler.text + "}").slots;
        }
    }

    [Serializable]
    public class ItemBoxSlots
    {
        public ItemBoxSlot[] slots;
    }
    [Serializable]
    public class ItemBoxSlot
    {
        public string itemId;
        public string title;
        public int template;
        public int quantity;

        public ItemBoxSlot(string i, string t, int temp, int q)
        {
            itemId = i;
            title = t;
            template = temp;
            quantity = q;
        }
    }

    [Serializable]
    public class Activity
    {
        public string activity;
        public string subactivity;
        public string gameId;

        public Activity(string a, string b, string id)
        {
            activity = a;
            subactivity = b;
            gameId = id;
        }
    }
    [Serializable]
    public class Savejson
    {
        public string savejson;
        public Savejson(string s)
        {
            savejson = s;
        }
    }
    [Serializable]
    public class LoginToken
    {
        /// <summary>Authenticated user record, returned by /auth/login and /auth/refresh.</summary>
        public AccountData user;
        /// <summary>7-day JWT access token. Sent on every authenticated API call as the bearer token.</summary>
        public string token;
        /// <summary>30-day JWT refresh token. Sent ONLY to /auth/refresh to mint a new access+refresh pair.</summary>
        public string refreshToken;
        /// <summary>Lifetime of <see cref="token"/> in seconds (~604800 for 7 days). Used for proactive refresh scheduling if needed.</summary>
        public int expiresIn;
    }
    [Serializable]
    public class Feedback
    {
        public string gameId;
        public string title;
        public string feedbackType;
        public string description;
        public int rating;
        public float coordinateX;
        public float coordinateY;
        public float coordinateZ;
        public string area;
    }
    [Serializable]
    public class Savedata
    {
        public string _id;
        public string gameId;
        public string userId;
        public int slot;
        public string savejson;
    }

    [Serializable]
    public class ProfileData
    {
        public string bio;
        public string displayName;
        public string[] links;

        public ProfileData(string bio, string displayName, string[] links)
        {
            this.bio = bio;
            this.displayName = displayName;
            this.links = links;
        }
    }

    /// <summary>
    /// Wrapper struct used to deserialise a top-level JSON array of owned
    /// game ids — JsonUtility can't read a top-level array natively, so the
    /// API response is wrapped client-side as <c>{"ids":[...]}</c>.
    /// </summary>
    [Serializable]
    public class OwnedGamesResponse {
        public string[] ids;
    }

    /// <summary>
    /// Body for player-authored content posts (death messages, small wins).
    /// Server attributes via the bearer token; only the text comes from the
    /// payload. Kept generic so future "fun stuff" endpoints can reuse it.
    /// </summary>
    [Serializable]
    public class SocialMessage {
        public string text;

        public SocialMessage(string text) {
            this.text = text;
        }
    }
}