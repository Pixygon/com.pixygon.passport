using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pixygon.Passport {
    public class AccountUI : MonoBehaviour {

        [SerializeField] private GameObject _loginLoadingScreen;
        
        [Header("Logout")] [SerializeField] private GameObject _logoutScreen;

        [Header("Verification")]
        [SerializeField] private GameObject _verificationPanel;

        [SerializeField] private InputField _verificationCode;

        [Header("Reset Password")]
        [SerializeField] private GameObject _forgotPasswordPanel;

        [SerializeField] private TMP_InputField _emailInput;

        [SerializeField] private AccountErrors _accountErrors;

        [SerializeField] private GameObject _deleteAccountPanel;
        [SerializeField] private LoginPanel _loginPanel;
        [SerializeField] private RegisterPanel _registerPanel;

        private LoginState _loginState = LoginState.Startup;


        private void ClearInputs() {
            _verificationCode.text = "";
            _emailInput.text = "";
        }


        private void Update() {
            switch (_loginState) {
                case LoginState.Startup:
                    if (PixygonApi.Instance.IsLoggingIn) return;
                    if (PixygonApi.Instance.IsLoggedIn)
                        CloseAccountScreen();
                    else {
                        StartLogin();
                    }

                    break;
                case LoginState.Login:
                    if (PixygonApi.Instance.IsLoggingIn) return;
                    if (PixygonApi.Instance.IsLoggedIn)
                        CloseAccountScreen();
                    else {
                        StartLogin();
                    }

                    break;
            }
        }

        public void StartLogin() {
            _loginState = LoginState.Login;
            if (PixygonApi.Instance.IsLoggedIn) {
                if (_loginState == LoginState.Login)
                    CloseAccountScreen();
            }
            else {
                gameObject.SetActive(true);
                _loginPanel.ActivateScreen(!PixygonApi.Instance.IsLoggingIn);
                _loginLoadingScreen.SetActive(PixygonApi.Instance.IsLoggingIn);
            }
        }

        public void StartRegister() {
            _registerPanel.ActivateScreen(true);
            _loginState = LoginState.Signup;
        }

        public void OpenAccountScreen() {
            _loginState = LoginState.None;
            PopulateAccountScreen();
        }

        public void CloseAccountScreen() {
            _loginPanel.ActivateScreen(false);
            gameObject.SetActive(false);
            _loginState = LoginState.None;
        }

        public void StartLogout() {
            _logoutScreen.SetActive(true);
        }

        public void OnLogout() {
            PixygonApi.Instance.StartLogout();
            _loginPanel.ActivateScreen(true);
            _logoutScreen.SetActive(false);
            ClearInputs();
        }

        private void PopulateAccountScreen() {
            _loginLoadingScreen.SetActive(false);
        }

        public void Login(string user, string pass, bool rememberMe) {
            PixygonApi.Instance.StartLogin(user, pass, rememberMe, LoginComplete,
                s => {
                    _accountErrors.SetErrorMessage("Login Failed", s, StartLogin);
                    SetError();
                });
            _loginLoadingScreen.SetActive(true);
        }

        private void LoginComplete() {
            if (!PixygonApi.Instance.IsLoggedIn) return;
            _loginLoadingScreen.SetActive(false);
            PopulateAccountScreen();
        }

        private void SetError() {
            _loginState = LoginState.Error;
            _loginLoadingScreen.SetActive(false);
        }

        

        public void Signup(string user, string email, string pass, bool rememberMe) {
            PixygonApi.Instance.StartSignup(user, email, pass,
                rememberMe, SignupComplete, s => {
                    _accountErrors.SetErrorMessage("Signup Failed", s, StartRegister);
                    SetError();
                });
            currentUser = user;
            _loginLoadingScreen.SetActive(true);
            _registerPanel.ActivateScreen(false);
        }

        private string currentUser;

        private void SignupComplete() {
            Debug.Log("This is wrong!");
            _loginLoadingScreen.SetActive(false);
            _verificationPanel.SetActive(true);
        }

        public void OnVerify() {
            PixygonApi.Instance.VerifyUser(currentUser, int.Parse(_verificationCode.text),
                VerificationComplete, s => {
                    _accountErrors.SetErrorMessage("Verification Failed", s, SignupComplete);
                    SetError();
                });
            _loginLoadingScreen.SetActive(true);
            _registerPanel.ActivateScreen(false);
        }

        private void VerificationComplete() {
            if (!PixygonApi.Instance.IsLoggedIn) return;
            _verificationPanel.SetActive(false);
            PopulateAccountScreen();
            CloseAccountScreen();
        }

        public void OpenPasswordReset() {
            _forgotPasswordPanel.SetActive(true);
            _loginPanel.ActivateScreen(false);
            _loginState = LoginState.PasswordRecovery;
        }

        public void CancelPasswordReset() {
            _loginLoadingScreen.SetActive(false);
            _forgotPasswordPanel.SetActive(false);
            _loginPanel.ActivateScreen(true);
            _loginState = LoginState.None;
        }

        public void OnResetPassword() {
            PixygonApi.Instance.ForgotPassword(_emailInput.text, ResetPasswordComplete, s => {
                _accountErrors.SetErrorMessage("Recovery Failed", s, () => { _forgotPasswordPanel.SetActive(true); });
                SetError();
            });
            _loginLoadingScreen.SetActive(true);
            _forgotPasswordPanel.SetActive(false);
            _loginPanel.ActivateScreen(false);
        }

        [SerializeField] private ResetPasswordPanel _resetPasswordPanel;

        private void ResetPasswordComplete() {
            _loginLoadingScreen.SetActive(false);
            _forgotPasswordPanel.SetActive(false);
            _resetPasswordPanel.ActivateScreen(true);
        }

        public void OnSendResetPassword(string hash, string newPass) {
            PixygonApi.Instance.ForgotPasswordRecovery(_emailInput.text, hash, newPass, NewPasswordSet,
                s => {
                    _accountErrors.SetErrorMessage("Recovery Failed", s,
                        () => { _resetPasswordPanel.ActivateScreen(true); });
                    SetError();
                });
            _loginLoadingScreen.SetActive(true);
        }

        private void NewPasswordSet() {
            _loginLoadingScreen.SetActive(false);
            _loginPanel.ActivateScreen(true);
            _loginState = LoginState.None;
        }

        public void StartDelete() {
            _deleteAccountPanel.SetActive(true);
        }

        public void OnDeleteAccount() {
            PixygonApi.Instance.DeleteUser(DeletionComplete,
                s => {
                    _accountErrors.SetErrorMessage("Recovery Failed", s,
                        () => { });
                    SetError();
                });
        }

        private void DeletionComplete() {
            Debug.Log("Account deleted!");
        }

        private enum LoginState {
            Startup,
            None,
            Login,
            Signup,
            Validate,
            PasswordRecovery,
            Error
        }
    }
}