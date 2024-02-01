using TMPro;
using UnityEngine;

namespace Pixygon.Passport {
    public class VerificationPanel : MonoBehaviour {
        [SerializeField] private AccountUI _accountUi;
        [SerializeField] private GameObject _verificationPanel;
        [SerializeField] private TMP_InputField _verificationCode;
        
        private void ClearInputs() {
            _verificationCode.text = "";
        }

        public void ActivateScreen(bool active) {
            gameObject.SetActive(active);
            if(active) ClearInputs();
        }
        public void OnVerify() {
            _accountUi.OnVerify(_verificationCode.text);
        }
    }
}