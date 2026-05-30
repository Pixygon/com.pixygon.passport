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
                // Server joins activity + subactivity with '|'. Guard the
                // index so a short string like "Online" (no pipe) doesn't
                // crash s[1]. Missing parts read as empty.
                var s = activity.Split('|');
                _activityText.text = s.Length > 0 ? s[0] : string.Empty;
                _subactivityText.text = s.Length > 1 ? s[1] : string.Empty;
            } else {
                _activityText.text = string.Empty;
                _subactivityText.text = string.Empty;
            }
            _gameIconObject.SetActive(false);
            // FIX: the previous version had a stray ';' after this if, which
            // ended the conditional. GetGame ran unconditionally and 404'd
            // on '/v1/client/game/' whenever the user had no current game.
            if (!string.IsNullOrEmpty(gameId)) {
                GetGame(gameId);
            }
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
