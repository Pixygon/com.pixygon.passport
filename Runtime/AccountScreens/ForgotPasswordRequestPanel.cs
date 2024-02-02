using TMPro;
using UnityEngine;

namespace Pixygon.Passport {
    public class ForgotPasswordRequestPanel : MonoBehaviour {
        [SerializeField] private AccountUI _accountUi;
        [SerializeField] private TMP_InputField _emailInput;
        
        private void ClearInputs() {
            _emailInput.text = "";
        }

        public void ActivateScreen(bool active) {
            gameObject.SetActive(active);
            if(active) ClearInputs();
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