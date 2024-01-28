using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pixygon.Passport
{
    public class XpTab : MonoBehaviour {
        [SerializeField] private Image _xpImage;
        [SerializeField] private TextMeshProUGUI _levelText;

        public void SetTab(int xp) {
            var (level, remainingXP, percentage) = CalculateLevelAndXP(xp);
            _levelText.text = level.ToString();
            _xpImage.fillAmount = percentage;
        }
        
        private static (int level, int remainingXP, float percentage) CalculateLevelAndXP(int experiencePoints)
        {
            // Constants for the leveling algorithm
            const int baseXp = 100; // The base XP required for level 1
            const double xpIncreaseFactor = 1.2; // Factor to increase XP requirement between levels

            // Calculate the total XP required for all levels up to the current level
            int TotalXpRequired(int currentLevel) =>
                (int)Math.Floor(baseXp * Math.Pow(xpIncreaseFactor, currentLevel - 1));

            // Find the highest level reached based on the total accumulated XP
            var level = 1;
            while (experiencePoints >= TotalXpRequired(level + 1))
            {
                level++;
            }

            // Calculate the remaining XP after reaching the current level
            var remainingXp = experiencePoints - TotalXpRequired(level);
            
            var xPToNextLevel = TotalXpRequired(level + 1) - TotalXpRequired(level);

            var percentageToNextLevel = (float)remainingXp / xPToNextLevel;

            return (level, remainingXp >= 0 ? remainingXp : 0, percentageToNextLevel);

        }
    }
}