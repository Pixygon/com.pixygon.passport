using System;
using System.Linq;
using Pixygon.Saving;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pixygon.Passport {
    public class PassportCard : MonoBehaviour {
        [SerializeField] private TextMeshProUGUI _usernameText;
        [SerializeField] private TextMeshProUGUI _bioText;
        [SerializeField] private TextMeshProUGUI _followersText;
        [SerializeField] private TextMeshProUGUI _levelText;

        [SerializeField] private IconGetter _pfpIcon;
        //[SerializeField] private Image _pfpIcon;
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
        [SerializeField] private PassportStatus _passportStatus;
        
        private AccountData _user;

        private void Clear() {
            _usernameText.text = "Loading";
            _bioText.text = "";
            _followersText.text = "";
            _levelText.text = "-";
            _pfpIcon.ClearIcon();
            _passportStatus.Clear();
            _gameTab.SetTab(0, 0);
            _streamerTab.SetTab(0, 0);
            _communityTab.SetTab(0, 0);
            _viewerTab.SetTab(0, 0);
        }
        public async void GetUser(string id) {
            Clear();
            gameObject.SetActive(true);
            _user = null;
            var user = await PixygonApi.GetUser(id);
            Set(user);
        }
        private void Set(AccountData user) {
            gameObject.SetActive(true);
            
            _usernameText.text = string.IsNullOrEmpty(user.displayName) ? user.userName : user.displayName;
            _passportStatus.Set(user.latestActivity, user.latestGame);
            _bioText.text = user.bio;
            _followersText.text = $"{user.followers.Length} Followers";
            _pfpIcon.GetIcon(user.picturePath, !user.usePfp);
            _user = user;
            CalculateLevels();
            CheckIfFollowing();
        }

        private void CalculateLevels() {
            var (gameLevel, gamePercentage) = CalculateLevelAndXP(_user.gameXp);
            _gameTab.SetTab(gameLevel, gamePercentage);
            var (streamerLevel, streamerPercentage) = CalculateLevelAndXP(_user.streamerXp);
            _streamerTab.SetTab(streamerLevel, streamerPercentage);
            var (communityLevel, communityPercentage) = CalculateLevelAndXP(_user.communityXp);
            _communityTab.SetTab(communityLevel, communityPercentage);
            var (viewerLevel, viewerPercentage) = CalculateLevelAndXP(_user.viewerXp);
            _viewerTab.SetTab(viewerLevel, viewerPercentage);
            var totalLevel = Mathf.FloorToInt(((gameLevel * 4) + (streamerLevel * 2) + (communityLevel * 2) + viewerLevel)/9f);
            _levelText.text = totalLevel.ToString();
        }
        
        private static (int level, float percentage) CalculateLevelAndXP(int experiencePoints) {
            const int baseXp = 30;
            const double xpIncreaseFactor = 2.3;
            int TotalXpRequired(int currentLevel) =>
                (int)Math.Floor(baseXp * Math.Pow(xpIncreaseFactor, currentLevel-1));
            var level = 0;
            while (experiencePoints >= TotalXpRequired(level + 1))
                level++;
            var remainingXp = experiencePoints- (level == 0 ? 0 : TotalXpRequired(level));
            var xPToNextLevel = TotalXpRequired(level + 1) - (level == 0 ? 0 : TotalXpRequired(level));
            var percentageToNextLevel = (float)remainingXp / (float)xPToNextLevel;
            return (level+1, percentageToNextLevel);
        }

        private void CheckIfFollowing() {
            if (_user._id == PixygonApi.Instance.AccountData.user._id) {
                _unfollowBtn.SetActive(false);
                _followBtn.SetActive(false);
                _followLoading.SetActive(false);
                return;
            }
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