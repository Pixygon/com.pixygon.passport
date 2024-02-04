using TMPro;
using UnityEngine;

namespace Pixygon.Passport {
    public class VerificationPanel : AccountPanel {
        [SerializeField] private TMP_InputField _verificationCode;
        
        protected override void ClearInputs() {
            _verificationCode.text = "";
        }
        public void OnVerify() {
            _accountUi.OnVerify(_verificationCode.text);
        }
    }
}