using System;

namespace Pixygon.Passport {
    [Serializable]
    public class RecoverySubmitData {
        public string email;
        public string hash;
        public string newPass;
        public RecoverySubmitData(string email, string hash, string newPass) {
            this.email = email;
            this.hash = hash;
            this.newPass = newPass;
        }
    }
}