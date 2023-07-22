using System;

namespace Pixygon.Passport {
    [Serializable]
    public class LoginData {
        public string userName;
        public string password;

        public LoginData(string user, string pass) {
            userName = user;
            password = pass;
        }
    }
}