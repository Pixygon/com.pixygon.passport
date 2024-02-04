using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pixygon.Passport
{
    public class XpTab : MonoBehaviour {
        [SerializeField] private Image _xpImage;
        [SerializeField] private TextMeshProUGUI _levelText;

        public void SetTab(int level, float percentage) {
            _levelText.text = level.ToString();
            _xpImage.fillAmount = percentage;
        }
    }
}