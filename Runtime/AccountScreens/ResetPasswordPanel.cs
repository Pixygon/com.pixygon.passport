using TMPro;
using UnityEngine;

namespace Pixygon.Passport
{
    public class ResetPasswordPanel : MonoBehaviour {
        [SerializeField] private AccountUI _accountUi;
        [SerializeField] private TMP_InputField _resetHashInput;
        [SerializeField] private TMP_InputField _resetPassInput;
        private void ClearInputs() {
            _resetHashInput.text = "";
            _resetPassInput.text = "";
        }

        public void ActivateScreen(bool active) {
            gameObject.SetActive(active);
            ClearInputs();
        }
        public void OnSendResetPassword() {
            _accountUi.OnSendResetPassword(_resetHashInput.text, _resetPassInput.text);
            gameObject.SetActive(false);
        }
    }
}
