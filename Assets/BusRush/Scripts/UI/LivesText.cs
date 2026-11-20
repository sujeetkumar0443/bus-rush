using TMPro;
using UnityEngine;

namespace BusRush.UI
{
    /// <summary>Shows the lives count and a refill-timer placeholder ("FULL" when at max).</summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LivesText : MonoBehaviour
    {
        [SerializeField] private TMP_Text timerText;

        private TMP_Text text;

        private void Awake()
        {
            text = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            Render();
            SaveSystem.Changed += Render;
        }

        private void OnDisable()
        {
            SaveSystem.Changed -= Render;
        }

        private void Render()
        {
            int lives = SaveSystem.Data.lives;
            int max = GameConfig.Instance != null ? GameConfig.Instance.maxLives : 5;
            text.text = lives.ToString();
            if (timerText != null)
            {
                // Life refill timer arrives with the lives system; placeholder for now.
                timerText.text = lives >= max ? "FULL" : "--:--";
            }
        }
    }
}
