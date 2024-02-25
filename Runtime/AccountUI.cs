using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Pixygon.Passport {
    public class AccountUI : MonoBehaviour {
        [SerializeField] private bool _showSignInAtLaunch;
        [SerializeField] private bool _forceLogin;
        
        [SerializeField] private GameObject _loginLoadingScreen;
        [SerializeField] private GameObject _logoutScreen;
        [SerializeField] private GameObject _deleteAccountPanel;
        
        [SerializeField] private LoginPanel _loginPanel;
        [SerializeField] private RegisterPanel _registerPanel;
        [SerializeField] private VerificationPanel _verificationPanel;
        [SerializeField] private ForgotPasswordRequestPanel _forgotPasswordRequestPanel;
        [SerializeField] private ResetPasswordPanel _resetPasswordPanel;
        [SerializeField] private ErrorPanel _accountErrors;

        public LoginState LoginState = LoginState.Startup;
        public Action OnLoginAction;
        public Action OnLogoutAction;
        public bool ForceLogin => _forceLogin;
        private string _currentUser;
        private string _currentEmail;

        
        private void Start() {
            DoStartUpLogin();
        }
        
        private async void DoStartUpLogin() {
            while (PixygonApi.Instance.IsLoggingIn || LoginState == LoginState.Signup || LoginState == LoginState.Validate) await Task.Yield();
            if (PixygonApi.Instance.IsLoggedIn)
                CloseAccountScreen();
            else {
                if(_showSignInAtLaunch)
                    StartLogin();
                else
                    CloseAccountScreen();
            }
        }
        public void StartLogin() {
            LoginState = LoginState.Login;
            if (PixygonApi.Instance.IsLoggedIn) {
                if (LoginState == LoginState.Login)
                    CloseAccountScreen();
            }
            else {
                Debug.Log("Time to log in!");
                gameObject.SetActive(true);
                _loginPanel.ActivateScreen(!PixygonApi.Instance.IsLoggingIn);
                _loginLoadingScreen.SetActive(PixygonApi.Instance.IsLoggingIn);
            }
        }
        public void StartRegister() {
            _loginPanel.ActivateScreen(false);
            _registerPanel.ActivateScreen(true);
            LoginState = LoginState.Signup;
        }
        public void CancelSignup() {
            _registerPanel.ActivateScreen(false);
            StartLogin();
        }
        public void CancelLogout() {
            _logoutScreen.SetActive(false);
            gameObject.SetActive(false);
        }
        public void OpenAccountScreen() {
            LoginState = LoginState.None;
            PopulateAccountScreen();
        }
        public void CloseAccountScreen() {
            _loginPanel.ActivateScreen(false);
            gameObject.SetActive(false);
            LoginState = LoginState.None;
        }
        public void StartLogout() {
            Debug.Log("Start Logout!");
            _logoutScreen.SetActive(true);
            gameObject.SetActive(true);
        }
        public void OnLogout() {
            PixygonApi.Instance.StartLogout();
            //_loginPanel.ActivateScreen(true);
            _logoutScreen.SetActive(false);
            StartLogin();
            OnLogoutAction?.Invoke();
        }
        private void PopulateAccountScreen() {
            _loginLoadingScreen.SetActive(false);
            CloseAccountScreen();
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
            OnLoginAction?.Invoke();
            PopulateAccountScreen();
        }
        private void SetError() {
            LoginState = LoginState.Error;
            _loginLoadingScreen.SetActive(false);
        }
        public void Signup(string user, string email, string pass, bool rememberMe) {
            PixygonApi.Instance.StartSignup(user, email, pass,
                rememberMe, SignupComplete, s => {
                    _accountErrors.SetErrorMessage("Signup Failed", s, StartRegister);
                    SetError();
                });
            _currentUser = user;
            _loginLoadingScreen.SetActive(true);
            _registerPanel.ActivateScreen(false);
        }
        private void SignupComplete() {
            LoginState = LoginState.Validate;
            _loginLoadingScreen.SetActive(false);
            _verificationPanel.ActivateScreen(true);
        }
        public void OnVerify(string code) {
            PixygonApi.VerifyUser(_currentUser, int.Parse(code),
                VerificationComplete, s => {
                    _accountErrors.SetErrorMessage("Verification Failed", s, SignupComplete);
                    SetError();
                });
            _loginLoadingScreen.SetActive(true);
            _verificationPanel.ActivateScreen(false);
        }
        private void VerificationComplete() {
            _loginLoadingScreen.SetActive(false);
            Debug.Log("Verified...!");
            StartLogin();
            //PopulateAccountScreen();
            //CloseAccountScreen();
        }
        public void OpenPasswordReset() {
            _forgotPasswordRequestPanel.ActivateScreen(true);
            _loginPanel.ActivateScreen(false);
            LoginState = LoginState.PasswordRecovery;
        }
        public void CancelPasswordReset() {
            _loginLoadingScreen.SetActive(false);
            _forgotPasswordRequestPanel.ActivateScreen(false);
            _loginPanel.ActivateScreen(true);
            LoginState = LoginState.None;
        }
        public void OnResetPassword(string email) {
            _currentEmail = email;
            PixygonApi.ForgotPassword(email, ResetPasswordComplete, s => {
                _accountErrors.SetErrorMessage("Recovery Failed", s, () => { _forgotPasswordRequestPanel.ActivateScreen(true); });
                SetError();
            });
            _loginLoadingScreen.SetActive(true);
            _loginPanel.ActivateScreen(false);
        }
        private void ResetPasswordComplete() {
            _loginLoadingScreen.SetActive(false);
            _forgotPasswordRequestPanel.ActivateScreen(false);
            _resetPasswordPanel.ActivateScreen(true);
        }
        public void OnSendResetPassword(string hash, string newPass) {
            PixygonApi.ForgotPasswordRecovery(_currentEmail, hash, newPass, NewPasswordSet,
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
            LoginState = LoginState.None;
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
    }
    public enum LoginState {
        Startup,
        None,
        Login,
        Signup,
        Validate,
        PasswordRecovery,
        Error
    }
}