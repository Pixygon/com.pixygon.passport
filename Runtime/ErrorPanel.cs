using System;
using TMPro;
using UnityEngine;

namespace Pixygon.Passport {
    public class ErrorPanel : MonoBehaviour {
        [SerializeField] private TextMeshProUGUI _title;
        [SerializeField] private TextMeshProUGUI _error;

        private Action _onFail;

        public void SetErrorMessage(string title, ErrorResponse error, Action onFail) {
            gameObject.SetActive(true);
            _title.text = title;
            _error.text = error._msg;
            _onFail = onFail;
        }

        public void SetErrorMessage(string title, string error, Action onFail) {
            gameObject.SetActive(true);
            _title.text = title;
            _error.text = error;
            _onFail = onFail;
        }

        public void Close() {
            gameObject.SetActive(false);
            _title.text = "";
            _error.text = "";
            _onFail.Invoke();
        }
    }
}