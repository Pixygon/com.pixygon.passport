using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Pixygon.Passport {
    public class PassportStatus : MonoBehaviour {
        [SerializeField] private TextMeshProUGUI _activityText;
        [SerializeField] private TextMeshProUGUI _subactivityText;
        [SerializeField] private Image _gameIcon;
        [SerializeField] private GameObject _gameIconObject;
        
        public void Set(string activity, string gameId) {
            if (!string.IsNullOrEmpty(activity)) {
                var s = activity.Split('|');
                _activityText.text = s[0];
                _subactivityText.text = s[1];
            }
            _gameIconObject.SetActive(false);
            if (!string.IsNullOrEmpty(gameId));
                GetGame(gameId);
        }

        private async void GetGame(string id) {
            var game = await PixygonApi.GetGame(id);
            Debug.Log("Game: " + game);
            var icon = game;
            _gameIconObject.SetActive(false);
        }

        public void Clear() {
            _activityText.text = "";
            _subactivityText.text = "";
            _gameIconObject.SetActive(false);
        }
    }
}
