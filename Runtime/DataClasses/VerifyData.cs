using System;

namespace Pixygon.Passport {
    [Serializable]
    public class VerifyData {
        public string userName;
        public int verificationCode;
        public VerifyData(string user, int code) {
            userName = user;
            verificationCode = code;
        }
    }
}