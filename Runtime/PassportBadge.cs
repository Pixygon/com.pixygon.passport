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
            if (!PixygonApi.Instance.IsLoggedIn) {
                _usernameText.text = "Not logged in!";
                _profilePic.ClearIcon();
                _passportStatus.Clear();
            } else {
                _usernameText.text = string.IsNullOrEmpty(PixygonApi.Instance.AccountData.user.displayName)
                    ? PixygonApi.Instance.AccountData.user.userName
                    : PixygonApi.Instance.AccountData.user.displayName;
                _profilePic.GetIcon(PixygonApi.Instance.AccountData.user.picturePath);
                _passportStatus.Set(PixygonApi.Instance.AccountData.user.latestActivity, PixygonApi.Instance.AccountData.user.latestGame);
                //GetGame(PixygonApi.Instance.AccountData.user.latestActivity, PixygonApi.Instance.AccountData.user.latestGame);
            }
            _openTimer = 5f;
            _open = true;
            GetComponent<Animator>().SetBool("Open", true);
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
                    GetComponent<Animator>().SetBool("Open", false);
                }
            }            
        }
        public void OnPointerEnter(PointerEventData eventData) {
            _isOver = true;
            _openTimer = 5f;
            _open = true;
            GetComponent<Animator>().SetBool("Open", true);
        }
        public void OnPointerExit(PointerEventData eventData) {
            _isOver = false;
        }

        public void OnClick() {
            Debug.Log("Clicked on Passport Badge!");
            if (!PixygonApi.Instance.IsLoggedIn)
                _accountUi?.StartLogin();
            else
                _passportCard?.GetUser(PixygonApi.Instance.AccountData.user._id);
        }
    }
}