using System;

namespace Pixygon.Passport {
    [Serializable]
    public class RecoveryData {
        public string email;
        public RecoveryData(string email) {
            this.email = email;
        }
    }
}