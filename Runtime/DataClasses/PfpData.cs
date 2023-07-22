using System;

namespace Pixygon.Passport {
    [Serializable]
    public class PfpData {
        public string chain;
        public string hash;
        public PfpData(string chain, string hash) {
            this.chain = chain;
            this.hash = hash;
        }
    }
}