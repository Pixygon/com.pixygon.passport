using System;

namespace Pixygon.Passport {
    [Serializable]
    public class PfpData {
        public string id;
        public string chain;
        public string hash;
        public PfpData(string id, string chain, string hash) {
            this.id = id;
            this.chain = chain;
            this.hash = hash;
        }
    }
}