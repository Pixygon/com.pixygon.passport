using System;

namespace Pixygon.Passport {
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
}