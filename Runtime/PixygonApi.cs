using System;
using System.Threading.Tasks;
using Pixygon.Saving;
using UnityEngine;
using UnityEngine.Networking;

namespace Pixygon.Passport {
    /// <summary>
    /// Persistent singleton that talks to the Pixygon backend at
    /// <c>https://api.pixygon.com/v1/</c>. Survives scene transitions via
    /// <c>DontDestroyOnLoad</c>; the in-memory <see cref="AccountData"/> +
    /// <see cref="OwnedGameIds"/> persist across the whole session.
    ///
    /// <para>See <c>Documentation/README.md</c> in the package for the full
    /// integration guide, auth flow diagrams, and endpoint reference.</para>
    /// </summary>
    public class PixygonApi : MonoBehaviour {
        // Production API. Single line to swap for staging.
        private const string PixygonServerURL = "https://api.pixygon.com/v1/";

        // Default request timeouts. Conservative — we'd rather a slow request
        // surface as a friendly error than spin forever.
        private const int DefaultTimeoutSeconds = 30;
        // One automatic retry on 5xx for refresh + idempotent GETs. Catches
        // most transient backend hiccups (gateway timeouts, cold starts).
        private const int RetryCount5xx = 1;

        public bool IsLoggedIn { get; private set; }
        public bool IsLoggingIn { get; private set; }
        public LoginToken AccountData { get; private set; }
        public static PixygonApi Instance { get; private set; }

        /// <summary>
        /// Fired whenever the login state, account data, or owned games list
        /// changes. Consumers downstream of Passport (e.g. PaidEdition gates)
        /// listen so they can react to a mid-session login without reloading.
        /// </summary>
        public event Action OnLoginStateChanged;

        // Remember-me prefs keys. Passwords are never stored; we persist the
        // access + refresh tokens and rehydrate via /auth/refresh. The
        // access token expires in ~7 days, the refresh token in ~30 days.
        private const string PrefRememberMe   = "Pixygon.RememberMe";
        private const string PrefUserId       = "Pixygon.UserId";
        private const string PrefToken        = "Pixygon.Token";
        private const string PrefRefreshToken = "Pixygon.RefreshToken";
        // Pre-0.5 keys, cleared once at boot. The old client stored the
        // password in PlayerPrefs (yikes); the migration wipes it on first
        // run with the new package version.
        private const string LegacyPrefRemember  = "RememberMe";
        private const string LegacyPrefUsername  = "Username";
        private const string LegacyPrefPassword  = "Password";

        /// <summary>
        /// Slugs of games the logged-in account owns. Populated in the
        /// background after login by <see cref="FetchOwnedGames"/>. Empty
        /// when not signed in or when the request hasn't completed yet —
        /// callers should treat absence as "we don't know yet", not "no".
        /// </summary>
        public string[] OwnedGameIds { get; private set; } = Array.Empty<string>();

        /// <summary>True if the logged-in account is known to own the given game (by slug).</summary>
        public bool OwnsGame(string slug) {
            if (string.IsNullOrEmpty(slug) || OwnedGameIds == null) return false;
            for (var i = 0; i < OwnedGameIds.Length; i++) {
                if (OwnedGameIds[i] == slug) return true;
            }
            return false;
        }

        // ======== Lifecycle ===============================================

        private async void Awake() {
            // Correct singleton: when a fresh scene instantiates its own
            // PixygonApi MonoBehaviour but the persistent Instance already
            // exists, kill the *new* one and bail. Previously we destroyed
            // the persistent Instance on every scene load, throwing away
            // AccountData and forcing a re-refresh.
            if (Instance != null && Instance != this) {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            MigrateLegacyRememberMe();

            if (PlayerPrefs.GetInt(PrefRememberMe) != 1) return;
            IsLoggingIn = true;

            var cachedUserId = PlayerPrefs.GetString(PrefUserId);
            if (Debug.isDebugBuild)
                Debug.Log($"[PixygonApi] Attempting remember-me refresh for userId={cachedUserId}");

            AccountData = await RefreshSession(cachedUserId,
                                               PlayerPrefs.GetString(PrefRefreshToken));

            // Some Awake-cycles can race a teardown (scene-init Destroy on a
            // duplicate instance). If we're gone by the time the await
            // resumes, don't touch shared state.
            if (this == null) return;

            // Only an access token is strictly required to keep the session
            // alive. Missing user fields are patched from the cached id;
            // a missing user.userName triggers a follow-up GetUser fetch.
            if (AccountData == null || string.IsNullOrEmpty(AccountData.token)) {
                Debug.LogWarning("[PixygonApi] Refresh returned no access token — clearing remember-me and showing login.");
                ClearRememberMe();
                SaveManager.SettingsSave._user = null;
                SaveManager.SettingsSave._isLoggedIn = false;
                AccountData = null;
                IsLoggingIn = false;
                return;
            }
            if (AccountData.user == null) {
                Debug.LogWarning("[PixygonApi] Refresh response missing user record — synthesising minimal user from cached id.");
                AccountData.user = new Pixygon.Saving.AccountData { _id = cachedUserId };
            } else if (string.IsNullOrEmpty(AccountData.user._id) && !string.IsNullOrEmpty(cachedUserId)) {
                Debug.LogWarning("[PixygonApi] Refresh response user._id empty — patching from cached id.");
                AccountData.user._id = cachedUserId;
            }

            // Server's /auth/refresh response can omit user fields entirely.
            // If we got a stub back, fetch the real account before the rest
            // of the app gates on IsLoggingIn = false.
            if (string.IsNullOrEmpty(AccountData.user.userName)
                && !string.IsNullOrEmpty(AccountData.user._id)) {
                if (Debug.isDebugBuild)
                    Debug.Log("[PixygonApi] Refresh user record is a stub — fetching full profile via GetUser.");
                var freshUser = await GetUser(AccountData.user._id);
                if (this == null) return;
                if (freshUser != null) {
                    if (string.IsNullOrEmpty(freshUser._id)) freshUser._id = AccountData.user._id;
                    AccountData.user = freshUser;
                    if (Debug.isDebugBuild)
                        Debug.Log($"[PixygonApi] Full profile fetched: userName='{freshUser.userName}' displayName='{freshUser.displayName}'");
                } else {
                    Debug.LogWarning("[PixygonApi] GetUser fetch after refresh failed — UI will show the stub fields until next refresh.");
                }
            }

            IsLoggedIn = true;
            IsLoggingIn = false;
            PersistRememberMe(AccountData);
            SaveManager.SettingsSave._user = AccountData.user;
            SaveManager.SettingsSave._isLoggedIn = true;
            SetLatestActivity("Online", string.Empty, string.Empty);
            OnLoginStateChanged?.Invoke();
            _ = FetchOwnedGames();
        }

        private void OnApplicationQuit() {
            SetLatestActivity("Offline", string.Empty, string.Empty);
        }

        private void MigrateLegacyRememberMe() {
            if (PlayerPrefs.GetInt(LegacyPrefRemember) != 1) return;
            PlayerPrefs.DeleteKey(LegacyPrefRemember);
            PlayerPrefs.DeleteKey(LegacyPrefUsername);
            PlayerPrefs.DeleteKey(LegacyPrefPassword);
            PlayerPrefs.Save();
        }

        // ======== Authentication ==========================================

        public async void StartLogin(string user, string pass, bool rememberMe = false,
                                     Action onLogin = null, Action<ErrorResponse> onFail = null) {
            AccountData = await LogIn(user, pass, onFail);
            if (this == null) return;
            if (AccountData != null) {
                SaveManager.SettingsSave._user = AccountData.user;
                SaveManager.SettingsSave._isLoggedIn = true;
                if (rememberMe) PersistRememberMe(AccountData);
                OnLoginStateChanged?.Invoke();
                _ = FetchOwnedGames();
            }
            onLogin?.Invoke();
        }

        public async void StartSignup(string user, string email, string pass, bool rememberMe = false,
                                      Action onSignup = null, Action<ErrorResponse> onFail = null) {
            // Signup doesn't auto-login (email verification first), so there's
            // nothing to remember yet — the rememberMe flag is propagated
            // through the subsequent StartLogin call.
            await Signup(user, email, pass, onFail);
            if (this == null) return;
            onSignup?.Invoke();
        }

        public static async void VerifyUser(string user, int code,
                                            Action onVerify = null, Action<ErrorResponse> onFail = null) {
            var res = await Request(UnityWebRequest.kHttpVerbPOST, "auth/verify",
                                    JsonUtility.ToJson(new VerifyData(user, code)));
            if (!res.Success) {
                onFail?.Invoke(new ErrorResponse(res.Error, res.Body));
                return;
            }
            onVerify?.Invoke();
        }

        public static async void ForgotPassword(string email,
                                                Action onVerify = null, Action<ErrorResponse> onFail = null) {
            var res = await Request(UnityWebRequest.kHttpVerbPOST, "auth/forgotPassword",
                                    JsonUtility.ToJson(new RecoveryData(email)));
            if (!res.Success) {
                onFail?.Invoke(new ErrorResponse(res.Error, res.Body));
                return;
            }
            onVerify?.Invoke();
        }

        public static async void ForgotPasswordRecovery(string email, string hash, string newPass,
                                                        Action onVerify = null, Action<ErrorResponse> onFail = null) {
            var res = await Request(UnityWebRequest.kHttpVerbPOST, "auth/forgotPasswordRecovery",
                                    JsonUtility.ToJson(new RecoverySubmitData(email, hash, newPass)));
            if (!res.Success) {
                onFail?.Invoke(new ErrorResponse(res.Error, res.Body));
                return;
            }
            onVerify?.Invoke();
        }

        public void StartLogout() {
            ClearRememberMe();
            SaveManager.SettingsSave._user = null;
            SaveManager.SettingsSave._isLoggedIn = false;
            AccountData = null;
            IsLoggedIn = false;
            OwnedGameIds = Array.Empty<string>();
            OnLoginStateChanged?.Invoke();
        }

        public async void DeleteUser(Action onVerify = null, Action<ErrorResponse> onFail = null) {
            // Snapshot the token BEFORE clearing local state — the previous
            // version cleared first and NRE'd on AccountData.token.
            var token = AccountData?.token ?? string.Empty;
            ClearRememberMe();
            SaveManager.SettingsSave._user = null;
            SaveManager.SettingsSave._isLoggedIn = false;
            AccountData = null;
            IsLoggedIn = false;
            OwnedGameIds = Array.Empty<string>();
            var res = await Request(UnityWebRequest.kHttpVerbGET, "users/delete", token: token);
            if (this == null) return;
            if (!res.Success) {
                onFail?.Invoke(new ErrorResponse(res.Error, res.Body));
                return;
            }
            onVerify?.Invoke();
        }

        private async Task<LoginToken> LogIn(string user, string pass, Action<ErrorResponse> onFail = null) {
            IsLoggingIn = true;
            var res = await Request(UnityWebRequest.kHttpVerbPOST, "auth/login",
                                    JsonUtility.ToJson(new LoginData(user, pass)));
            if (this == null) return null;
            if (!res.Success) {
                onFail?.Invoke(new ErrorResponse(res.Error, res.Body));
                IsLoggingIn = false;
                return null;
            }
            IsLoggedIn = true;
            IsLoggingIn = false;
            try { return JsonUtility.FromJson<LoginToken>(res.Body); }
            catch (Exception e) {
                Debug.LogError($"[PixygonApi] LogIn parse failed: {e.Message}; body={res.Body}");
                IsLoggedIn = false;
                return null;
            }
        }

        private async Task<LoginToken> Signup(string user, string email, string pass, Action<ErrorResponse> onFail = null) {
            var res = await Request(UnityWebRequest.kHttpVerbPOST, "auth/register",
                                    JsonUtility.ToJson(new SignupData(user, email, pass)));
            if (this == null) return null;
            if (!res.Success) {
                onFail?.Invoke(new ErrorResponse(res.Error, res.Body));
                return null;
            }
            IsLoggedIn = false;
            IsLoggingIn = false;
            try { return JsonUtility.FromJson<LoginToken>(res.Body); }
            catch (Exception e) {
                Debug.LogError($"[PixygonApi] Signup parse failed: {e.Message}; body={res.Body}");
                return null;
            }
        }

        /// <summary>
        /// Trade a cached refresh token for a fresh access + refresh pair.
        /// Server expects the refresh token in the Authorization header
        /// (not the access token). Returns null on missing/expired/revoked.
        /// One automatic retry on 5xx so a single transient backend blip
        /// doesn't sign the user out.
        /// </summary>
        private async Task<LoginToken> RefreshSession(string userId, string refreshToken) {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(refreshToken)) {
                if (Debug.isDebugBuild)
                    Debug.Log("[PixygonApi] RefreshSession skipped: no userId or refreshToken cached.");
                return null;
            }
            var res = await Request(UnityWebRequest.kHttpVerbGET,
                                    $"auth/refresh/{userId}",
                                    token: refreshToken,
                                    retries: RetryCount5xx);
            if (this == null) return null;
            if (!res.Success) {
                Debug.LogWarning($"[PixygonApi] RefreshSession HTTP error: {res.Error}");
                return null;
            }
            if (string.IsNullOrWhiteSpace(res.Body) || res.Body == "null") {
                Debug.LogWarning("[PixygonApi] RefreshSession returned an empty body.");
                return null;
            }
            try {
                var parsed = JsonUtility.FromJson<LoginToken>(res.Body);
                if (Debug.isDebugBuild)
                    Debug.Log($"[PixygonApi] RefreshSession parsed: token={(string.IsNullOrEmpty(parsed?.token) ? "EMPTY" : "set")} refreshToken={(string.IsNullOrEmpty(parsed?.refreshToken) ? "EMPTY" : "set")} user._id={(parsed?.user != null ? parsed.user._id : "(null user)")}");
                return parsed;
            } catch (Exception e) {
                Debug.LogError($"[PixygonApi] RefreshSession parse failed: {e.Message}; body={res.Body}");
                return null;
            }
        }

        private static void PersistRememberMe(LoginToken token) {
            if (token == null) return;
            // Don't overwrite stored values with empty strings — the server
            // may omit a field on a refresh response and overwriting a
            // perfectly-good cached refresh token with "" would sign the
            // user out next boot.
            PlayerPrefs.SetInt(PrefRememberMe, 1);
            if (token.user != null && !string.IsNullOrEmpty(token.user._id))
                PlayerPrefs.SetString(PrefUserId, token.user._id);
            if (!string.IsNullOrEmpty(token.token))
                PlayerPrefs.SetString(PrefToken, token.token);
            if (!string.IsNullOrEmpty(token.refreshToken))
                PlayerPrefs.SetString(PrefRefreshToken, token.refreshToken);
            PlayerPrefs.Save();
            if (Debug.isDebugBuild)
                Debug.Log($"[PixygonApi] PersistRememberMe: userId='{PlayerPrefs.GetString(PrefUserId)}' token={(string.IsNullOrEmpty(PlayerPrefs.GetString(PrefToken)) ? "EMPTY" : "set")} refreshToken={(string.IsNullOrEmpty(PlayerPrefs.GetString(PrefRefreshToken)) ? "EMPTY" : "set")}");
        }

        private static void ClearRememberMe() {
            PlayerPrefs.DeleteKey(PrefRememberMe);
            PlayerPrefs.DeleteKey(PrefUserId);
            PlayerPrefs.DeleteKey(PrefToken);
            PlayerPrefs.DeleteKey(PrefRefreshToken);
            PlayerPrefs.Save();
        }

        // ======== User profile + activity =================================

        public static async Task<AccountData> GetUser(string userId) {
            if (string.IsNullOrEmpty(userId)) return null;
            var res = await Request(UnityWebRequest.kHttpVerbGET, $"users/view/{userId}");
            if (!res.Success) {
                Debug.LogWarning($"[PixygonApi] GetUser failed: {res.Error}");
                return null;
            }
            if (string.IsNullOrWhiteSpace(res.Body) || res.Body == "null") return null;
            try { return JsonUtility.FromJson<AccountData>(res.Body); }
            catch (Exception e) {
                Debug.LogError($"[PixygonApi] GetUser parse failed: {e.Message}");
                return null;
            }
        }

        public async Task<AccountData> GetUserFromTwitch(string twitchId) {
            if (string.IsNullOrEmpty(twitchId)) return null;
            var res = await Request(UnityWebRequest.kHttpVerbGET, $"users/twitch/{twitchId}");
            if (this == null) return null;
            if (!res.Success) {
                Debug.LogWarning($"[PixygonApi] GetUserFromTwitch failed: {res.Error}");
                return null;
            }
            if (string.IsNullOrWhiteSpace(res.Body) || res.Body == "null") return null;
            try { return JsonUtility.FromJson<AccountData>(res.Body); }
            catch (Exception e) {
                Debug.LogError($"[PixygonApi] GetUserFromTwitch parse failed: {e.Message}");
                return null;
            }
        }

        public async void SetProfile(string bio, string displayName, string[] links,
                                     Action onSuccess = null, Action<ErrorResponse> onFail = null) {
            var token = AccountData?.token ?? string.Empty;
            var res = await Request(UnityWebRequest.kHttpVerbPOST, "users/setProfile",
                                    JsonUtility.ToJson(new ProfileData(bio, displayName, links)),
                                    token: token);
            if (this == null) return;
            if (!res.Success) {
                onFail?.Invoke(new ErrorResponse(res.Error, res.Body));
                return;
            }
            await RefreshUser();
            if (this == null) return;
            onSuccess?.Invoke();
        }

        public async void SetLatestActivity(string activity, string subactivity, string gameId) {
            var token = AccountData?.token ?? string.Empty;
            // Don't fire if there's no token — the endpoint requires auth
            // and the call would just 401 every boot.
            if (string.IsNullOrEmpty(token)) return;
            await Request(UnityWebRequest.kHttpVerbPOST, "users/activity",
                          JsonUtility.ToJson(new Activity(activity, subactivity, gameId)),
                          token: token);
        }

        public async void PatchTwitchAccount(string twitchAccount) {
            var token = AccountData?.token ?? string.Empty;
            await Request("PATCH", $"users/twitch/{twitchAccount}", string.Empty, token);
        }

        public async void PatchDreadwagerSkin(int i) {
            var token = AccountData?.token ?? string.Empty;
            await Request("PATCH", $"users/addDreadwagerSkin/{i}", string.Empty, token);
        }

        public async void PatchGameXp(int i) {
            var token = AccountData?.token ?? string.Empty;
            await Request("PATCH", $"users/gameXp/{i}", string.Empty, token);
        }

        public async void PatchStreamerXp(int i) {
            var token = AccountData?.token ?? string.Empty;
            await Request("PATCH", $"users/streamerXp/{i}", string.Empty, token);
        }

        public async void PatchViewerXp(int i, string id) {
            await Request("PATCH", $"users/viewerXp/{i}/{id}", string.Empty);
        }

        private async Task RefreshUser() {
            if (AccountData?.user == null || string.IsNullOrEmpty(AccountData.user._id)) return;
            AccountData.user = await GetUser(AccountData.user._id);
            if (this == null) return;
            SaveManager.SettingsSave._user = AccountData.user;
        }

        // ======== Social graph ============================================

        public async Task<string> FollowUser(string followId) {
            var token = AccountData?.token ?? string.Empty;
            var res = await Request("PATCH", $"users/follow/{followId}", string.Empty, token);
            if (this == null) return null;
            if (!res.Success) {
                Debug.LogWarning($"[PixygonApi] FollowUser failed: {res.Error}");
                return null;
            }
            await RefreshUser();
            return "{\"_results\":" + res.Body + "}";
        }

        public async Task<string> UnfollowUser(string followId) {
            var token = AccountData?.token ?? string.Empty;
            var res = await Request("PATCH", $"users/unfollow/{followId}", string.Empty, token);
            if (this == null) return null;
            if (!res.Success) {
                Debug.LogWarning($"[PixygonApi] UnfollowUser failed: {res.Error}");
                return null;
            }
            await RefreshUser();
            return "{\"_results\":" + res.Body + "}";
        }

        public async Task<string> GetFollowing(string userId) {
            var res = await Request(UnityWebRequest.kHttpVerbGET, $"users/following/{userId}");
            if (this == null) return null;
            if (!res.Success) {
                Debug.LogWarning($"[PixygonApi] GetFollowing failed: {res.Error}");
                return null;
            }
            return "{\"_results\":" + res.Body + "}";
        }

        public async Task<string> GetFollowers(string userId) {
            var res = await Request(UnityWebRequest.kHttpVerbGET, $"users/followers/{userId}");
            if (this == null) return null;
            if (!res.Success) {
                Debug.LogWarning($"[PixygonApi] GetFollowers failed: {res.Error}");
                return null;
            }
            return "{\"_results\":" + res.Body + "}";
        }

        public async Task<string> UserSearch(string searchString) {
            var res = await Request(UnityWebRequest.kHttpVerbGET, "users/s/" + searchString);
            if (this == null) return null;
            if (!res.Success) {
                Debug.LogWarning($"[PixygonApi] UserSearch failed: {res.Error}");
                return null;
            }
            return "{\"_results\":" + res.Body + "}";
        }

        // ======== Owned games + entitlement ===============================

        /// <summary>
        /// Refresh the list of game slugs this account owns. Called
        /// automatically after a successful login or refresh; exposed
        /// publicly so a store flow can invalidate the cache after a
        /// purchase completes.
        /// </summary>
        public async Task FetchOwnedGames() {
            // Skip the fetch entirely if we don't have a usable user id —
            // would hit /v1/users//ownedGames and 404, polluting logs.
            if (AccountData == null
                || AccountData.user == null
                || string.IsNullOrEmpty(AccountData.user._id)) {
                OwnedGameIds = Array.Empty<string>();
                return;
            }
            var res = await Request(UnityWebRequest.kHttpVerbGET,
                                    $"users/{AccountData.user._id}/ownedGames",
                                    token: AccountData.token);
            if (this == null) return;
            if (!res.Success) {
                Debug.LogWarning($"[PixygonApi] FetchOwnedGames failed: {res.Error}");
                return;
            }
            if (string.IsNullOrWhiteSpace(res.Body) || res.Body == "null") {
                OwnedGameIds = Array.Empty<string>();
                OnLoginStateChanged?.Invoke();
                return;
            }
            try {
                // Server returns a raw JSON array — JsonUtility can't read
                // top-level arrays, so wrap then unwrap.
                var holder = JsonUtility.FromJson<OwnedGamesResponse>("{\"ids\":" + res.Body + "}");
                OwnedGameIds = holder != null && holder.ids != null
                    ? holder.ids : Array.Empty<string>();
            } catch (Exception e) {
                Debug.LogWarning($"[PixygonApi] FetchOwnedGames parse failed: {e.Message}; body={res.Body}");
                OwnedGameIds = Array.Empty<string>();
            }
            OnLoginStateChanged?.Invoke();
        }

        // ======== Player-authored content =================================

        /// <summary>
        /// POST an authored death message. Server attributes via bearer
        /// token; anonymous fallback if not signed in (server still
        /// accepts but won't attribute).
        /// </summary>
        public async void PostDeathMessage(string gameId, string text,
                                           Action onPosted = null, Action<ErrorResponse> onFail = null) {
            var token = AccountData?.token ?? string.Empty;
            var res = await Request(UnityWebRequest.kHttpVerbPOST,
                                    $"{gameId}/deathMessage",
                                    JsonUtility.ToJson(new SocialMessage(text)),
                                    token);
            if (this == null) return;
            if (!res.Success) {
                onFail?.Invoke(new ErrorResponse(res.Error, res.Body));
                return;
            }
            onPosted?.Invoke();
        }

        /// <summary>
        /// POST a "small win" — short blurb for the in-game message board.
        /// Same attribution + fallback semantics as <see cref="PostDeathMessage"/>.
        /// </summary>
        public async void PostSmallWin(string gameId, string text,
                                       Action onPosted = null, Action<ErrorResponse> onFail = null) {
            var token = AccountData?.token ?? string.Empty;
            var res = await Request(UnityWebRequest.kHttpVerbPOST,
                                    $"{gameId}/smallWin",
                                    JsonUtility.ToJson(new SocialMessage(text)),
                                    token);
            if (this == null) return;
            if (!res.Success) {
                onFail?.Invoke(new ErrorResponse(res.Error, res.Body));
                return;
            }
            onPosted?.Invoke();
        }

        // ======== Save data ===============================================

        public async Task<Savedata> GetSave(string gameId, int slot) {
            if (AccountData?.user == null || string.IsNullOrEmpty(AccountData.user._id)) return null;
            var res = await Request(UnityWebRequest.kHttpVerbGET,
                                    $"savedata/{gameId}/{AccountData.user._id}/{slot}",
                                    token: AccountData.token);
            if (this == null) return null;
            if (!res.Success) {
                Debug.LogWarning($"[PixygonApi] GetSave failed: {res.Error}");
                return null;
            }
            if (string.IsNullOrWhiteSpace(res.Body) || res.Body == "null") return null;
            try { return JsonUtility.FromJson<Savedata>(res.Body); }
            catch (Exception e) {
                Debug.LogError($"[PixygonApi] GetSave parse failed: {e.Message}");
                return null;
            }
        }

        public async Task<Savedata> PostSave(string gameId, int slot, string savedata) {
            if (AccountData?.user == null || string.IsNullOrEmpty(AccountData.user._id)) return null;
            var res = await Request(UnityWebRequest.kHttpVerbPOST,
                                    $"savedata/{gameId}/{AccountData.user._id}/{slot}",
                                    JsonUtility.ToJson(new Savejson(savedata)),
                                    AccountData.token);
            if (this == null) return null;
            if (!res.Success) {
                Debug.LogWarning($"[PixygonApi] PostSave failed: {res.Error}");
                return null;
            }
            try { return JsonUtility.FromJson<Savedata>(res.Body); }
            catch (Exception e) {
                Debug.LogError($"[PixygonApi] PostSave parse failed: {e.Message}");
                return null;
            }
        }

        public async void PatchSave(Savedata savedata) {
            var token = AccountData?.token ?? string.Empty;
            await Request("PATCH", $"savedata/{savedata._id}",
                          JsonUtility.ToJson(new Savejson(savedata.savejson)),
                          token);
        }

        // ======== Highscores + feedback ===================================

        public static async Task<string> GetHighScores(string gameId, string scoretype, string version,
                                                      string fromDate, string toDate) {
            var res = await Request(UnityWebRequest.kHttpVerbGET,
                                    $"highscores/{gameId}/{scoretype}/{version}/{fromDate}/{toDate}");
            return res.Success ? res.Body : null;
        }

        public static async void PostHighScore(string score) {
            await Request(UnityWebRequest.kHttpVerbPOST, "highscores", score);
        }

        public async void PostFeedback(Feedback feedback) {
            await Request(UnityWebRequest.kHttpVerbPOST, "client/feedbacks", JsonUtility.ToJson(feedback));
        }

        public static async Task<string> GetGame(string gameId) {
            if (string.IsNullOrEmpty(gameId)) return null;
            var res = await Request(UnityWebRequest.kHttpVerbGET, $"client/game/{gameId}");
            if (!res.Success) {
                Debug.LogWarning($"[PixygonApi] GetGame failed: {res.Error}");
                return null;
            }
            return "{\"_results\":" + res.Body + "}";
        }

        // ======== Deprecated NFT surface (no-op stubs) ====================
        //
        // The Pixygon NFT integration (WAX/Eth/Tez/Matic/IMX wallets, item-
        // box mint/deposit/withdraw, NFT/Wax collection search/love, PFP
        // assignment from on-chain assets) is no longer part of the
        // first-party API. The stubs below remain so existing consumer
        // projects that wired the legacy methods don't fail to compile;
        // they log a one-shot warning and return empty / null.
        //
        // Remove these methods (and the consuming UI) when the project is
        // confirmed NFT-free, or replace with a fresh implementation if a
        // first-party Pixygon NFT system is built.

        [Obsolete("NFT integration removed from Pixygon.Passport. This method is a no-op stub kept for back-compat — delete the call site.")]
        public Task<string> MintAsset(ItemBoxSlots items) { WarnDeprecated(nameof(MintAsset)); return Task.FromResult<string>(null); }

        [Obsolete("NFT integration removed from Pixygon.Passport. This method is a no-op stub kept for back-compat — delete the call site.")]
        public Task<string> DepositItems(ItemBoxSlots items) { WarnDeprecated(nameof(DepositItems)); return Task.FromResult<string>(null); }

        [Obsolete("NFT integration removed from Pixygon.Passport. This method is a no-op stub kept for back-compat — delete the call site.")]
        public Task<string> WithdrawItems(ItemBoxSlots items) { WarnDeprecated(nameof(WithdrawItems)); return Task.FromResult<string>(null); }

        [Obsolete("NFT integration removed from Pixygon.Passport. This method is a no-op stub kept for back-compat — delete the call site.")]
        public Task<ItemBoxSlot[]> GetItems() { WarnDeprecated(nameof(GetItems)); return Task.FromResult(Array.Empty<ItemBoxSlot>()); }

        [Obsolete("Wallet patches removed from Pixygon.Passport.")] public void PatchWaxWallet(string wallet) { WarnDeprecated(nameof(PatchWaxWallet)); }
        [Obsolete("Wallet patches removed from Pixygon.Passport.")] public void PatchEthWallet(string wallet) { WarnDeprecated(nameof(PatchEthWallet)); }
        [Obsolete("Wallet patches removed from Pixygon.Passport.")] public void PatchTezWallet(string wallet) { WarnDeprecated(nameof(PatchTezWallet)); }
        [Obsolete("Wallet patches removed from Pixygon.Passport.")] public void PatchMatWallet(string wallet) { WarnDeprecated(nameof(PatchMatWallet)); }
        [Obsolete("Wallet patches removed from Pixygon.Passport.")] public void PatchImxWallet(string wallet) { WarnDeprecated(nameof(PatchImxWallet)); }

        [Obsolete("NFT PFP removed from Pixygon.Passport.")]
        public void SetAsPfp(string chain, string hash, Action<ErrorResponse> onFail = null) { WarnDeprecated(nameof(SetAsPfp)); }

        [Obsolete("WAX collection endpoints removed from Pixygon.Passport.")]
        public Task<string> LoveCollection(string collectionName) { WarnDeprecated(nameof(LoveCollection)); return Task.FromResult<string>(null); }

        [Obsolete("WAX collection endpoints removed from Pixygon.Passport.")]
        public Task<string> GetLovedCollections(string userId) { WarnDeprecated(nameof(GetLovedCollections)); return Task.FromResult<string>(null); }

        [Obsolete("Collection search endpoint removed from Pixygon.Passport.")]
        public Task<string> CollectionSearch(string searchString) { WarnDeprecated(nameof(CollectionSearch)); return Task.FromResult<string>(null); }

        [Obsolete("NFT search endpoint removed from Pixygon.Passport.")]
        public Task<string> NftSearch(string searchString) { WarnDeprecated(nameof(NftSearch)); return Task.FromResult<string>(null); }

        // One warning per process, not per call — saves a swamp of logs if
        // a call site fires every frame.
        private static readonly System.Collections.Generic.HashSet<string> s_warnedOnce
            = new System.Collections.Generic.HashSet<string>();
        private static void WarnDeprecated(string name) {
            if (s_warnedOnce.Add(name))
                Debug.LogWarning($"[PixygonApi] {name} is a no-op stub — NFT surface was removed. Drop the call site.");
        }

        // ======== HTTP helper =============================================

        /// <summary>
        /// One request, automatically disposed via <c>using</c>, with a
        /// proper async-completion await (not a busy yield loop), unified
        /// error handling, and optional retry on 5xx.
        ///
        /// <para>POST and PATCH bodies go in <paramref name="body"/>; GET
        /// passes <see cref="string.Empty"/> there. Bearer token (if any)
        /// goes in <paramref name="token"/>. The async-extension awaiter
        /// is used so WebGL builds don't busy-spin a frame per request.</para>
        /// </summary>
        private static async Task<ApiResult> Request(string method, string path,
                                                     string body = "",
                                                     string token = "",
                                                     int retries = 0) {
            var attempt = 0;
            ApiResult result = default;
            while (true) {
                using var www = BuildRequest(method, path, body, token);
                var op = www.SendWebRequest();
                while (!op.isDone) await Task.Yield();
                result = ToResult(www);
                // Retry once on transient backend hiccups. 4xx are client
                // errors and shouldn't be retried; only 5xx + protocol/timeouts.
                var shouldRetry = !result.Success
                    && attempt < retries
                    && (result.StatusCode >= 500 || www.result == UnityWebRequest.Result.ConnectionError);
                if (!shouldRetry) break;
                attempt++;
            }
            return result;
        }

        private static UnityWebRequest BuildRequest(string method, string path, string body, string token) {
            UnityWebRequest www;
            // Different ctors for GET vs body-bearing verbs. Put() takes a
            // body buffer; for POST/PATCH we override method afterwards.
            if (method == UnityWebRequest.kHttpVerbGET) {
                www = UnityWebRequest.Get(PixygonServerURL + path);
            } else {
                www = UnityWebRequest.Put(PixygonServerURL + path, body ?? string.Empty);
                www.method = method;
                www.SetRequestHeader("Content-Type", "application/json");
            }
            www.timeout = DefaultTimeoutSeconds;
            if (!string.IsNullOrEmpty(token))
                www.SetRequestHeader("Authorization", $"Bearer {token}");
            return www;
        }

        private static ApiResult ToResult(UnityWebRequest www) {
            // UWR.result is the authoritative success/failure signal; www.error
            // is empty string on protocol errors with bodies (4xx/5xx with
            // JSON), so we infer status code from responseCode directly.
            var ok = www.result == UnityWebRequest.Result.Success;
            var body = www.downloadHandler != null ? www.downloadHandler.text : string.Empty;
            var error = ok ? string.Empty : (string.IsNullOrEmpty(www.error) ? $"HTTP {www.responseCode}" : www.error);
            return new ApiResult(ok, www.responseCode, body, error);
        }

        /// <summary>
        /// Capture-by-value result of one request. The raw UWR is disposed
        /// inside <see cref="Request"/> — callers never see it.
        /// </summary>
        private readonly struct ApiResult {
            public readonly bool Success;
            public readonly long StatusCode;
            public readonly string Body;
            public readonly string Error;

            public ApiResult(bool success, long status, string body, string error) {
                Success = success;
                StatusCode = status;
                Body = body;
                Error = error;
            }
        }
    }

    // ======== Data classes ================================================

    [Serializable]
    public class Activity {
        public string activity;
        public string subactivity;
        public string gameId;

        public Activity(string a, string b, string id) {
            activity = a;
            subactivity = b;
            gameId = id;
        }
    }

    [Serializable]
    public class Savejson {
        public string savejson;
        public Savejson(string s) {
            savejson = s;
        }
    }

    [Serializable]
    public class LoginToken {
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
    public class Feedback {
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
    public class Savedata {
        public string _id;
        public string gameId;
        public string userId;
        public int slot;
        public string savejson;
    }

    [Serializable]
    public class ProfileData {
        public string bio;
        public string displayName;
        public string[] links;

        public ProfileData(string bio, string displayName, string[] links) {
            this.bio = bio;
            this.displayName = displayName;
            this.links = links;
        }
    }

    /// <summary>
    /// Wrapper struct used to deserialise a top-level JSON array of owned
    /// game slugs — JsonUtility can't read a top-level array natively, so the
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

    // ======== Deprecated NFT data classes (kept for back-compat) ==========
    //
    // Existing consumer code (e.g. Dreadwager's ItemBoxUI) still references
    // these as parameter / return types. The API methods that consume them
    // are no-op stubs (see PixygonApi.cs deprecated section); the classes
    // remain so downstream call sites compile. Delete when the consumer
    // UI is removed.

    [Serializable]
    [Obsolete("ItemBox NFT data class — kept only so deprecated PixygonApi stubs compile.")]
    public class ItemBoxSlots {
        public ItemBoxSlot[] slots;
    }

    [Serializable]
    [Obsolete("ItemBox NFT data class — kept only so deprecated PixygonApi stubs compile.")]
    public class ItemBoxSlot {
        public string itemId;
        public string title;
        public int template;
        public int quantity;

        public ItemBoxSlot(string i, string t, int temp, int q) {
            itemId = i;
            title = t;
            template = temp;
            quantity = q;
        }
    }
}
