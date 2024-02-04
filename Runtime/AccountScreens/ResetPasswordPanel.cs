using TMPro;
using UnityEngine;

namespace Pixygon.Passport {
    public class ResetPasswordPanel : AccountPanel {
        [SerializeField] private TMP_InputField _resetHashInput;
        [SerializeField] private TMP_InputField _resetPassInput;
        protected override void ClearInputs() {
            _resetHashInput.text = "";
            _resetPassInput.text = "";
        }
        public void OnSendResetPassword() {
            _accountUi.OnSendResetPassword(_resetHashInput.text, _resetPassInput.text);
            ActivateScreen(false);
        }
    }
}
