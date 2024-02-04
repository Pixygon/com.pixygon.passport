using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pixygon.Passport {
    public class RegisterPanel : AccountPanel {
        [SerializeField] private TMP_InputField _signUpUserInput;
        [SerializeField] private TMP_InputField _signUpEmailInput;
        [SerializeField] private TMP_InputField _signUpPassInput;
        [SerializeField] private Toggle _signUpRememberMe;
        [SerializeField] private Toggle _toggleEmails;
        [SerializeField] private Toggle _toggleTerms;

        [SerializeField] private GameObject _usernameVerified;
        [SerializeField] private GameObject _emailVerified;
        [SerializeField] private GameObject _passwordVerified;
        //[SerializeField] private TextMeshProUGUI _passwordErrorText;
        [SerializeField] private GameObject _passwordError;
        
        private bool _isInputVerified;
        
        protected override void ClearInputs() {
            _signUpUserInput.text = "";
            _signUpEmailInput.text = "";
            _signUpPassInput.text = "";
        }
        public void VerifyInput() {
            _isInputVerified = true;
            //_passwordErrorText.text =
            //    "Password must be 8 characters long, contain a number and both upper and lowercase letters.";
            _usernameVerified.SetActive(false);
            _emailVerified.SetActive(false);
            _passwordVerified.SetActive(false);
            _passwordError.SetActive(false);

            if (_signUpUserInput.text.Length < 1) _isInputVerified = false;
            else _usernameVerified.SetActive(true);
            
            if (_signUpEmailInput.text.Length < 5) _isInputVerified = false;
            else _emailVerified.SetActive(true);

            if (_signUpPassInput.text == "") {
                //_passwordErrorText.text =
                //    "Password must be 8 characters long, contain a number and both upper and lowercase letters.";
                _isInputVerified = false;
            } else {
                if (_signUpPassInput.text.Length < 8) {
                    //_passwordErrorText.text = "<color=#FF958B>Password must be longer than 8 digits!";
                    _isInputVerified = false;
                }
                if (!_signUpPassInput.text.Any(c => char.IsDigit(c))) {
                    _passwordError.SetActive(true);
                    //_passwordErrorText.text = "<color=#FF958B>Password must contain at least one number!";
                    _isInputVerified = false;
                } else {
                    _passwordVerified.SetActive(true);
                }
            }
            if(!_toggleTerms.isOn)
                _isInputVerified = false;

            Debug.Log("Input verified: " + _isInputVerified);
            if (!_isInputVerified) return;
            //_passwordErrorText.text =
            //    "Password must be 8 characters long, contain a number and both upper and lowercase letters.";
            _isInputVerified = true;
        }
        public void Signup() {
            if (!_isInputVerified) return;
            _accountUi.Signup(_signUpUserInput.text, _signUpEmailInput.text,
                _signUpPassInput.text,
                _signUpRememberMe.isOn);
            gameObject.SetActive(false);
        }
        public void Back() {
            _accountUi.CancelSignup();
        }
    }
}