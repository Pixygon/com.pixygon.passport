using System;

namespace Pixygon.Passport {
    [Serializable]
    public class ErrorResponse {
        public string _code;
        public string _msg;

        public ErrorResponse(string code, string msg) {
            _code = code;
            _msg = msg;
        }
    }
}