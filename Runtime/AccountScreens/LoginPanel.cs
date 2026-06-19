using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Pixygon.Passport {
    public class LoginPanel : AccountPanel {
        [SerializeField] private GameObject _backBtn;
        [SerializeField] private TMP_InputField _userInput;
        [SerializeField] private TMP_InputField _passInput;
        [SerializeField] private Toggle _rememberMe;
        [Tooltip("The 'Log in' button. Disabled the moment the user submits so a frustrated double-click doesn't fire two auth/login POSTs against the server. Re-enabled when the panel re-opens (e.g. after a failed attempt). Optional — flow still works if left unwired, the protection just won't apply.")]
        [SerializeField] private Button _loginButton;

        // Remember what was typed so a failed login lets the user fix the
        // password without re-entering their username. Cleared only when
        // the panel is opened fresh (not when the error dialog re-opens it).
        private string _lastTypedUser = string.Empty;
        private bool _reopenAfterFailure;

        protected override void ClearInputs() {
            // If we're re-opening after a login failure (the AccountUI's
            // error onClose callback re-activates this panel), preserve
            // the username so the user only retypes their password. The
            // password field still clears either way — never want stale
            // wrong passwords sitting in the field.
            if (_reopenAfterFailure) {
                _reopenAfterFailure = false;
                _userInput.text = _lastTypedUser;
                _passInput.text = string.Empty;
                FocusField(_passInput);
                return;
            }
            _userInput.text = string.Empty;
            _passInput.text = string.Empty;
            FocusField(_userInput);
        }

        public override void ActivateScreen(bool active) {
            _backBtn.SetActive(!_accountUi.ForceLogin);
            // Reopening the panel after a failed login (or just opening it
            // fresh) means the user can try again — restore the button.
            if (active && _loginButton != null) _loginButton.interactable = true;
            base.ActivateScreen(active);
        }

        public void Login() {
            // Snapshot the username BEFORE we hide the panel so the
            // post-failure reopen can restore it.
            _lastTypedUser = _userInput.text;
            _reopenAfterFailure = true;
            // Visual "your click registered" feedback. The panel hides on
            // the next line anyway, but disabling the button is what
            // prevents two queued clicks from firing two POSTs.
            if (_loginButton != null) _loginButton.interactable = false;
            _accountUi.Login(_userInput.text, _passInput.text, _rememberMe.isOn);
            gameObject.SetActive(false);
        }

        public void Back() {
            // Cancel any pending "this was a failure attempt" state so
            // the next time the panel opens it starts fresh.
            _reopenAfterFailure = false;
            _lastTypedUser = string.Empty;
            if (_loginButton != null) _loginButton.interactable = true;
            _accountUi.CloseAccountScreen();
        }

        private void Update() {
            if (!gameObject.activeInHierarchy) return;
            // Enter/Return submits the form — same as clicking "Log in". Guard against
            // re-firing while a submit is already in flight (the button goes
            // non-interactable in Login()), so a held Enter can't queue two POSTs.
            if (SubmitPressedThisFrame()) {
                if (_loginButton == null || _loginButton.interactable) Login();
                return;
            }
            if (!TabPressedThisFrame()) return;
            // With only two text fields, Tab and Shift+Tab do the same
            // thing — swap focus. (Adding more fields would warrant a
            // proper ordered cycle.) We skip when focus is on the toggle
            // or the Log-in button so Tab doesn't yank a user out of a
            // mid-toggle interaction.
            var es = EventSystem.current;
            if (es == null) return;
            var current = es.currentSelectedGameObject;
            if (current == _userInput.gameObject) {
                FocusField(_passInput);
            } else if (current == _passInput.gameObject) {
                FocusField(_userInput);
            } else {
                // Nothing relevant focused — Tab starts at the first field.
                FocusField(_userInput);
            }
        }

        /// <summary>
        /// Move keyboard focus into <paramref name="field"/> so controller +
        /// keyboard users can start typing immediately. Safe to call when
        /// no EventSystem exists (early scene boot) — guards against null.
        /// </summary>
        private static void FocusField(TMP_InputField field) {
            if (field == null) return;
            var es = EventSystem.current;
            if (es == null) return;
            es.SetSelectedGameObject(field.gameObject);
            field.ActivateInputField();
        }

        /// <summary>
        /// Returns true if Tab was pressed this frame. Uses the new Input
        /// System Package — the asmdef references <c>Unity.InputSystem</c>
        /// and the package.json declares it as a dependency, so consumers
        /// can rely on it being available. <see cref="Keyboard.current"/>
        /// is null on devices with no keyboard at all (mobile builds with
        /// no Bluetooth keyboard attached); the null guard keeps the call
        /// safe in those cases.
        /// </summary>
        private static bool TabPressedThisFrame() {
            var kb = Keyboard.current;
            return kb != null && kb.tabKey.wasPressedThisFrame;
        }

        /// <summary>True if Enter/Return (main or numpad) was pressed this frame.</summary>
        private static bool SubmitPressedThisFrame() {
            var kb = Keyboard.current;
            return kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame);
        }

    }
}