using Pixygon.Saving;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pixygon.Passport {
    public class PassportCard : MonoBehaviour {
        [SerializeField] private TextMeshProUGUI _usernameText;
        [SerializeField] private TextMeshProUGUI _activityText;
        [SerializeField] private TextMeshProUGUI _subactivityText;
        [SerializeField] private TextMeshProUGUI _bioText;
        [SerializeField] private TextMeshProUGUI _followersText;
        [SerializeField] private TextMeshProUGUI _levelText;

        [SerializeField] private IconGetter _pfpIcon;
        //[SerializeField] private Image _pfpIcon;
        [SerializeField] private Image _gameIcon;
        [SerializeField] private Image[] _linkIcons;

        [SerializeField] private Sprite _noPfpSprite;
        [SerializeField] private Sprite _noGameSprite;

        public async void GetUser(string id) {
            gameObject.SetActive(true);
            var user = await PixygonApi.Instance.GetUser(id);
            Set(user);
        }
        public void Set(AccountData user) {
            gameObject.SetActive(true);
            
            _usernameText.text = string.IsNullOrEmpty(user.displayName) ? user.userName : user.displayName;

            if (!string.IsNullOrEmpty(user.latestActivity)) {
                var s = user.latestActivity.Split('|');
                _activityText.text = s[0];
                _subactivityText.text = s[1];
            }

            _bioText.text = user.bio;
            _followersText.text = $"{user.followers.Length} Followers";
            _levelText.text = "0";
            _pfpIcon.GetIcon(user.picturePath, !user.usePfp);
            //_gameIcon.sprite = _noGameSprite;
        }

        public void Close() {
            gameObject.SetActive(false);
        }
    }
}