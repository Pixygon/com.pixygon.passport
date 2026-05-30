using System;
using TMPro;
using UnityEngine;

namespace Pixygon.Passport {
    /// <summary>
    /// Tiny modal that surfaces login / signup / verification / recovery
    /// failures to the user. Title + body are always plain prose — the
    /// raw server JSON is humanized by <see cref="LoginErrorHumanizer"/>
    /// before reaching here, so users never see <c>{ "msg": "..." }</c>.
    /// </summary>
    public class ErrorPanel : MonoBehaviour {
        [SerializeField] private TextMeshProUGUI _title;
        [SerializeField] private TextMeshProUGUI _error;

        private Action _onFail;

        /// <summary>
        /// Preferred entry point — pass a context, the raw server response,
        /// and a continuation callback for when the user closes the dialog.
        /// The error is humanized internally so callers don't have to
        /// remember to do it themselves.
        /// </summary>
        public void ShowError(LoginErrorContext context, ErrorResponse error, Action onClose) {
            var humanized = LoginErrorHumanizer.Humanize(context, error);
            SetTextAndShow(humanized.Title, humanized.Body, onClose);
        }

        /// <summary>
        /// Legacy entry point — humanizes the response with the supplied
        /// title overriding the humanizer's default. Kept so existing
        /// callers that already pass a hand-written title keep compiling.
        /// </summary>
        public void SetErrorMessage(string title, ErrorResponse error, Action onFail) {
            // Even with a legacy title, run the body through the humanizer
            // so raw JSON / "Invalid Credentials" never reaches the UI.
            var humanized = LoginErrorHumanizer.Humanize(LoginErrorContext.Login, error);
            SetTextAndShow(title, humanized.Body, onFail);
        }

        /// <summary>
        /// Direct title + body string — used by hand-authored client errors
        /// that don't come from a server (e.g. local validation).
        /// </summary>
        public void SetErrorMessage(string title, string error, Action onFail) {
            SetTextAndShow(title, error, onFail);
        }

        private void SetTextAndShow(string title, string body, Action onFail) {
            gameObject.SetActive(true);
            if (_title != null) _title.text = title;
            if (_error != null) _error.text = body;
            _onFail = onFail;
        }

        public void Close() {
            gameObject.SetActive(false);
            if (_title != null) _title.text = "";
            if (_error != null) _error.text = "";
            _onFail?.Invoke();
        }
    }
}