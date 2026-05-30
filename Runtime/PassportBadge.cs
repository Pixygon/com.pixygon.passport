using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Pixygon.Passport
{
    public class PassportBadge : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
        [SerializeField] private PassportCard _passportCard;
        [SerializeField] private AccountUI _accountUi;
        [SerializeField] private TextMeshProUGUI _usernameText;
        [SerializeField] private IconGetter _profilePic;
        [SerializeField] private PassportStatus _passportStatus;

        private float _openTimer;
        private bool _open = true;
        private bool _isOver;
        public void Set() {
            // PixygonApi.Instance can be null briefly during scene boot order
            // — render as logged-out in that case rather than NRE.
            var api = PixygonApi.Instance;
            var loggedIn = api != null && api.IsLoggedIn
                           && api.AccountData != null && api.AccountData.user != null;
            if (!loggedIn) {
                _usernameText.text = "Not logged in!";
                _profilePic.ClearIcon();
                _passportStatus.Clear();
            } else {
                var u = api.AccountData.user;
                _usernameText.text = string.IsNullOrEmpty(u.displayName) ? u.userName : u.displayName;
                _profilePic.GetIcon(u.picturePath);
                _passportStatus.Set(u.latestActivity, u.latestGame);
            }
            _openTimer = 5f;
            _open = true;
            // Animator is optional — designer may have removed it. Don't
            // NRE if it isn't present.
            if (TryGetComponent<Animator>(out var anim)) anim.SetBool("Open", true);
        }

        /*
        private async void GetGame(string activity, string id) {
            _gameIcon.sprite = null;
            _gameIcon.gameObject.SetActive(false);
            if (!string.IsNullOrEmpty(activity)) {
                var s = activity.Split('|');
                _activityText.text = s[0];
                _subActivityText.text = s[1];
            }
            var game = PixygonApi.GetGame(id);
        }
        */

        private void Update() {
            if (_open && !_isOver) {
                if (_openTimer > 0f)
                    _openTimer -= Time.deltaTime;
                else {
                    _open = false;
                    if (TryGetComponent<Animator>(out var anim)) anim.SetBool("Open", false);
                }
            }
        }
        public void OnPointerEnter(PointerEventData eventData) {
            _isOver = true;
            _openTimer = 5f;
            _open = true;
            if (TryGetComponent<Animator>(out var anim)) anim.SetBool("Open", true);
        }
        public void OnPointerExit(PointerEventData eventData) {
            _isOver = false;
        }

        public void OnClick() {
            // PixygonApi.Instance can be null if the badge fires before the
            // singleton's Awake on a fresh boot — treat as logged-out.
            var api = PixygonApi.Instance;
            if (api == null || !api.IsLoggedIn)
                _accountUi?.StartLogin();
            else
                _passportCard?.GetUser(api.AccountData?.user?._id);
        }
    }
}