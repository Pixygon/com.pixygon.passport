using System.Linq;
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

        [SerializeField] private GameObject _followBtn;
        [SerializeField] private GameObject _unfollowBtn;
        [SerializeField] private GameObject _followLoading;

        [SerializeField] private XpTab _gameTab;
        [SerializeField] private XpTab _streamerTab;
        [SerializeField] private XpTab _communityTab;
        [SerializeField] private XpTab _viewerTab;
        
        private AccountData _user;

        private void Clear() {
            _usernameText.text = "Loading";
            _activityText.text = "";
            _subactivityText.text = "";
            _bioText.text = "";
            _followersText.text = "";
            _levelText.text = "-";
            _pfpIcon.ClearIcon();
            _gameTab.SetTab(0);
            _streamerTab.SetTab(0);
            _communityTab.SetTab(0);
            _viewerTab.SetTab(0);
        }
        public async void GetUser(string id) {
            Clear();
            gameObject.SetActive(true);
            _user = null;
            var user = await PixygonApi.Instance.GetUser(id);
            Set(user);
        }
        private void Set(AccountData user) {
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
            _gameTab.SetTab(user.gameXp);
            _streamerTab.SetTab(user.streamerXp);
            _communityTab.SetTab(user.communityXp);
            _viewerTab.SetTab(user.viewerXp);
            _user = user;
            CheckIfFollowing();
        }

        private void CheckIfFollowing() {
            _followLoading.SetActive(true);
            if (PixygonApi.Instance.AccountData.user.following != null) {
                if (PixygonApi.Instance.AccountData.user.following.Any(s => s == _user._id)) {
                    _unfollowBtn.SetActive(true);
                    _followBtn.SetActive(false);
                    _followLoading.SetActive(false);
                    return;
                }
            }
            _unfollowBtn.SetActive(false);
            _followBtn.SetActive(true);
            _followLoading.SetActive(false);
        }

        public void Close() {
            Debug.Log("Hello??");
            gameObject.SetActive(false);
        }

        public async void OnFollow() {
            _followBtn.SetActive(false);
            _followLoading.SetActive(true);
            await PixygonApi.Instance.FollowUser(_user._id);
            GetUser(_user._id);
        }
        public async void OnUnfollow() {
            _unfollowBtn.SetActive(false);
            _followLoading.SetActive(true);
            await PixygonApi.Instance.FollowUser(_user._id);
            GetUser(_user._id);
        }
    }
}