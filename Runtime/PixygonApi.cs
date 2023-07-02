using System;
using System.Threading.Tasks;
using Pixygon.Saving;
using UnityEngine;
using UnityEngine.Networking;

namespace Pixygon.Passport {
    public class PixygonApi : MonoBehaviour {
        private const string PixygonServerURL = "https://pixygon-server.onrender.com/";
        public bool IsLoggedIn { get; private set; }
        public bool IsLoggingIn { get; private set; }
        public LoginToken AccountData { get; private set; }
        private async void Awake() {
            if (PlayerPrefs.GetInt("RememberMe") != 1) return;
            IsLoggingIn = true;
            AccountData = await LogIn(PlayerPrefs.GetString("Username"), PlayerPrefs.GetString("Password"));
            if (AccountData == null) return;
            SaveManager.SettingsSave._user = AccountData.user;
            SaveManager.SettingsSave._isLoggedIn = true;
        }
        public async void StartLogin(string user, string pass, bool rememberMe = false, Action onLogin = null, Action<string> onFail = null) {
            if (rememberMe) {
                PlayerPrefs.SetInt("RememberMe", 1);
                PlayerPrefs.SetString("Username", user);
                PlayerPrefs.SetString("Password", pass);
                PlayerPrefs.Save();
            }
            AccountData = await LogIn(user, pass, onFail);
            if (AccountData != null) {
                SaveManager.SettingsSave._user = AccountData.user;
                SaveManager.SettingsSave._isLoggedIn = true;
            }
            onLogin?.Invoke();
        }
        public async void StartSignup(string user, string email, string pass, bool rememberMe = false, Action onSignup = null, Action<string> onFail = null) {
            if (rememberMe) {
                PlayerPrefs.SetInt("RememberMe", 1);
                PlayerPrefs.SetString("Username", user);
                PlayerPrefs.SetString("Password", pass);
                PlayerPrefs.Save();
            }
            AccountData = await Signup(user, email, pass, onFail);
            if (AccountData != null) {
                SaveManager.SettingsSave._user = AccountData.user;
                SaveManager.SettingsSave._isLoggedIn = true;
            }
            onSignup?.Invoke();
        }
        public async void PatchWaxWallet(string wallet) {
            var www = await PostWWW($"users/{AccountData.user._id}/wax/{wallet}", "", true, AccountData.token);
            AccountData.user = JsonUtility.FromJson<AccountData>(www.downloadHandler.text);
            SaveManager.SettingsSave._user = AccountData.user;
        }
        public async void PatchEthWallet(string wallet) {
            Debug.Log("Patching eth-wallet");
            var www = await PostWWW($"users/{AccountData.user._id}/eth/{wallet}", "", true, AccountData.token);
            Debug.Log("EthWallet Patch: " + www.downloadHandler.text);
        }
        public async void PatchTezWallet(string wallet) {
            Debug.Log("Patching tez-wallet");
            var www = await PostWWW($"users/{AccountData.user._id}/tez/{wallet}", "", true, AccountData.token);
            Debug.Log("TezWallet Patch: " + www.downloadHandler.text);
        }
        public async void PatchMatWallet(string wallet) {
            Debug.Log("Patching matic-wallet");
            var www = await PostWWW($"users/{AccountData.user._id}/mat/{wallet}", "", true, AccountData.token);
            Debug.Log("MaticWallet Patch: " + www.downloadHandler.text);
        }
        public async void PatchImxWallet(string wallet)
        {
            Debug.Log("Patching matic-wallet");
            var www = await PostWWW($"users/{AccountData.user._id}/imx/{wallet}", "", true, AccountData.token);
            Debug.Log("ImxWallet Patch: " + www.downloadHandler.text);
        }
        public async void PatchBio(string bio) {
            Debug.Log("Patching bio");
            var www = await PostWWW($"users/{AccountData.user._id}/editbio/{bio}", "", true, AccountData.token);
            Debug.Log("Bio Patch: " + www.downloadHandler.text);
        }
        public async void PatchPicturePath(string picturePath)
        {
            Debug.Log("Patching bio");
            var www = await PostWWW($"users/{AccountData.user._id}/editpicturepath/{picturePath}", "", true, AccountData.token);
            Debug.Log("PicturePath Patch: " + www.downloadHandler.text);
        }


        public async Task<AccountData> GetUser(string userId) {
            var www = await GetWWW($"users/view/{userId}");
            if (!string.IsNullOrWhiteSpace(www.error))
            {
                Debug.Log("GET USER ERROR!! " + www.error + " and this " + www.downloadHandler.text);
                return null;
            }
            if (string.IsNullOrWhiteSpace(www.downloadHandler.text) || www.downloadHandler.text == "null") return null;
            return JsonUtility.FromJson<AccountData>(www.downloadHandler.text);
        }
        public async Task<string> FollowUser(string followId) {
            var www = await PostWWW($"users/{AccountData.user._id}/{followId}", "", true, AccountData.token);
            if (!string.IsNullOrWhiteSpace(www.error)) {
                Debug.Log("FOLLOW USER ERROR!! " + www.error + " and this " + www.downloadHandler.text);
                return null;
            }
            Debug.Log(www.downloadHandler.text);
            return "{\"_results\":" + www.downloadHandler.text + "}";
        }
        public async Task<string> GetFollowing(string userId)
        {
            var www = await GetWWW("users/" + userId + "/following");
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
            var www = await GetWWW("users/" + userId + "/followers");
            if (!string.IsNullOrWhiteSpace(www.error))
            {
                Debug.Log("GET FOLLOWERS ERROR!! " + www.error + " and this " + www.downloadHandler.text);
                return null;
            }
            Debug.Log(www.downloadHandler.text);
            return "{\"_results\":" + www.downloadHandler.text + "}";
        }


        //SEARCH
        public async Task<string> UserSearch(string searchString) {
            var www = await GetWWW("users/s/" + searchString);
            if (!string.IsNullOrWhiteSpace(www.error)) {
                Debug.Log("GET USER SEARCH ERROR!! " + www.error + " and this " + www.downloadHandler.text);
                return null;
            }
            Debug.Log(www.downloadHandler.text);
            return "{\"_results\":" + www.downloadHandler.text + "}";
        }
        public async Task<string> CollectionSearch(string searchString)
        {
            var www = await GetWWW("users/s/" + searchString);
            if (!string.IsNullOrWhiteSpace(www.error))
            {
                Debug.Log("GET COLLECTION SEARCH ERROR!! " + www.error + " and this " + www.downloadHandler.text);
                return null;
            }
            return "{\"_results\":" + www.downloadHandler.text + "}";
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




        public void StartLogout() {
            PlayerPrefs.DeleteKey("RememberMe");
            PlayerPrefs.DeleteKey("Username");
            PlayerPrefs.DeleteKey("Password");
            PlayerPrefs.Save();
            SaveManager.SettingsSave._user = null;
            SaveManager.SettingsSave._isLoggedIn = false;
            AccountData = null;
            IsLoggedIn = false;
        }
        public async Task<Savedata> GetSave(string gameId, int slot) {
            var www = await GetWWW($"savedata/{gameId}/{AccountData.user._id}/{slot}", AccountData.token);
            if (!string.IsNullOrWhiteSpace(www.error)) {
                Debug.Log("GET SAVE ERROR!! " + www.error + " and this " + www.downloadHandler.text);
                return null;
            }
            if (string.IsNullOrWhiteSpace(www.downloadHandler.text) || www.downloadHandler.text == "null") return null;
            return JsonUtility.FromJson<Savedata>(www.downloadHandler.text);
        }
        public async Task<Savedata> PostSave(string gameId, int slot, string savedata) {
            var www = await PostWWW($"savedata/{gameId}/{AccountData.user._id}/{slot}", JsonUtility.ToJson(new Savejson(savedata)), false, AccountData.token);
            if (string.IsNullOrWhiteSpace(www.error)) return JsonUtility.FromJson<Savedata>(www.downloadHandler.text);
            Debug.Log("POST SAVE ERROR!! " + www.error + " and this " + www.downloadHandler.text);
            return null;
        }
        public async void PatchSave(Savedata savedata) {
            await PostWWW($"savedata/{savedata._id}", JsonUtility.ToJson(new Savejson(savedata.savejson)), true, AccountData.token);
        }
        private async Task<LoginToken> LogIn(string user, string pass, Action<string> onFail = null) {
            Debug.Log("Starting login!!");
            IsLoggingIn = true;
            var www = await PostWWW("auth/login", JsonUtility.ToJson(new LoginData(user, pass)));
            if (!string.IsNullOrWhiteSpace(www.error)) {
                Debug.Log("ERROR!! " + www.error + " and this " + www.downloadHandler.text);
                onFail?.Invoke($"{www.error}\n{www.downloadHandler.text}");
                IsLoggingIn = false;
                return null;
            }
            IsLoggedIn = true;
            IsLoggingIn = false;
            Debug.Log("Logged in: " + www.downloadHandler.text);
            return JsonUtility.FromJson<LoginToken>(www.downloadHandler.text);
        }
        private async Task<LoginToken> Signup(string user, string email, string pass, Action<string> onFail = null) {
            var www = await PostWWW("auth/register", JsonUtility.ToJson(new SignupData(user, email, pass)));
            if (!string.IsNullOrWhiteSpace(www.error)) {
                Debug.Log("ERROR!! " + www.error + " and this " + www.downloadHandler.text);
                onFail?.Invoke($"{www.error}\n{www.downloadHandler.text}");
                return null;
            }
            IsLoggedIn = true;
            Debug.Log("Signed in: " + www.downloadHandler.text);
            return JsonUtility.FromJson<LoginToken>(www.downloadHandler.text);
        }

        public async void GetUsers() {
            await GetWWW("Users");
        }
        public async void GetFeedback() {
            await GetWWW("client/feedbacks");
        }
        public async void PostHighScore(string game, string user, int score, string detail) {
            var www = await PostWWW("client/highscores", JsonUtility.ToJson(new HighScore(game, user, score, detail)));
            Debug.Log($"highScore: {www.downloadHandler.text}");
        }
        public async void PostFeedback(Feedback feedback) {
            var www = await PostWWW("client/feedbacks", JsonUtility.ToJson(feedback));
            Debug.Log($"Feedback: {www.downloadHandler.text}");
        }

        private async static Task<UnityWebRequest> GetWWW(string path, string token = "") {
            var www = UnityWebRequest.Get(PixygonServerURL + path);
            www.timeout = 60;
            if (token != string.Empty)
                www.SetRequestHeader("Authorization", $"Bearer {token}");
            www.SendWebRequest();
            while (!www.isDone)
                await Task.Yield();
            return www;
        }
        private async static Task<UnityWebRequest> PostWWW(string path, string body, bool patch = false, string token = "") {
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
    }

    [Serializable]
    public class Savejson {
        public string savejson;
        public Savejson(string s) {
            savejson = s;
        }
    }

    [Serializable]
    public class LoginData {
        public string userName;
        public string password;

        public LoginData(string user, string pass) {
            userName = user;
            password = pass;
        }
    }

    [Serializable]
    public class SignupData {
        public string userName;
        public string email;
        public string password;

        public SignupData(string user, string email, string pass) {
            userName = user;
            this.email = email;
            password = pass;
        }
    }

    [Serializable]
    public class LoginToken {
        public AccountData user;
        public string token;
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
    public class HighScore {
        public string gameId;
        public string userId;
        public int score;
        public string detail;
        public HighScore(string gameId, string userId, int score, string detail) {
            this.gameId = gameId;
            this.userId = userId;
            this.score = score;
            this.detail = detail;
        }
    }
}