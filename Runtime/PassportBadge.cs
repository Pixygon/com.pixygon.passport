using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Pixygon.Passport
{
    public class PassportBadge : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
        [SerializeField] private PassportCard _passportCard;
        [SerializeField] private TextMeshProUGUI _usernameText;
        [SerializeField] private TextMeshProUGUI _activityText;
        [SerializeField] private TextMeshProUGUI _subActivityText;
        [SerializeField] private IconGetter _profilePic;
        [SerializeField] private Image _gameIcon;

        private float _openTimer;
        private bool _open = true;
        private bool _isOver;
        public void Set() {
            if (!PixygonApi.Instance.IsLoggedIn) {
                _usernameText.text = "Not logged in!";
                _activityText.text = string.Empty;
                _subActivityText.text = string.Empty;
                _profilePic.ClearIcon();
                _gameIcon.sprite = null;
                _gameIcon.gameObject.SetActive(false);
            } else {
                _usernameText.text = string.IsNullOrEmpty(PixygonApi.Instance.AccountData.user.displayName)
                    ? PixygonApi.Instance.AccountData.user.userName
                    : PixygonApi.Instance.AccountData.user.displayName;

                Debug.Log("Time to get the pfp!");
                _profilePic.GetIcon(PixygonApi.Instance.AccountData.user.picturePath);
                GetGame(PixygonApi.Instance.AccountData.user.latestActivity, PixygonApi.Instance.AccountData.user.latestGame);
            }
            _openTimer = 5f;
            _open = true;
            GetComponent<Animator>().SetBool("Open", true);
        }

        private async void GetGame(string activity, string id) {
            _gameIcon.sprite = null;
            _gameIcon.gameObject.SetActive(false);
            if (!string.IsNullOrEmpty(activity)) {
                var s = activity.Split('|');
                _activityText.text = s[0];
                _subActivityText.text = s[1];
            }
            var game = PixygonApi.Instance.GetGame(id);
        }

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
                FindFirstObjectByType<AccountUI>()?.StartLogin();
            else
                _passportCard?.GetUser(PixygonApi.Instance.AccountData.user._id);
        }
    }
}
