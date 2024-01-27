using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pixygon.Passport {
    public class LoginPanel : MonoBehaviour {
        [SerializeField] private AccountUI _accountUi;
        [SerializeField] private TMP_InputField _userInput;
        [SerializeField] private TMP_InputField _passInput;
        [SerializeField] private Toggle _rememberMe;

        private void ClearInputs() {
            _userInput.text = string.Empty;
            _passInput.text = string.Empty;
        }

        public void ActivateScreen(bool active) {
            gameObject.SetActive(active);
            if(active) ClearInputs();
        }
        
        public void Login() {
            _accountUi.Login(_userInput.text, _passInput.text, _rememberMe.isOn);
            gameObject.SetActive(false);
        }
    }
}