using TMPro;
using UnityEngine;

namespace Pixygon.Passport {
    public class ForgotPasswordRequestPanel : AccountPanel {
        [SerializeField] private TMP_InputField _emailInput;
        protected override void ClearInputs() {
            _emailInput.text = "";
        }
        public void OnResetPassword() {
            _accountUi.OnResetPassword(_emailInput.text);
            ActivateScreen(false);
        }
        public void OnCloseScreen() {
            _accountUi.CancelPasswordReset();
        }
    }
}