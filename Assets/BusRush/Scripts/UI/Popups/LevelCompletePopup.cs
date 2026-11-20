using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>Win screen: rotating rays, stars popping in, coin reward, Claim / Next Level, and an empty MREC slot.</summary>
    public sealed class LevelCompletePopup : Popup
    {
        [SerializeField] private RectTransform[] stars = new RectTransform[3];
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private Transform rewardRoot;
        [SerializeField] private UIButton claimButton;
        [SerializeField] private TMP_Text claimLabel;
        [SerializeField] private UIButton nextButton;

        private int completedLevel = 1;
        private int starCount = 3;
        private int reward;
        private bool claimed;
        private bool hasSetup;

        protected override void Awake()
        {
            base.Awake();
            claimButton.Button.onClick.AddListener(Claim);
            nextButton.Button.onClick.AddListener(NextLevel);
        }

        /// <summary>
        /// Call before opening (Get&lt;LevelCompletePopup&gt;().Setup(...) then Open). Without it the popup shows
        /// the current level, 3 stars and the configured reward.
        /// </summary>
        public void Setup(int level, int starsEarned, int coins)
        {
            completedLevel = Mathf.Max(1, level);
            starCount = Mathf.Clamp(starsEarned, 0, stars.Length);
            reward = Mathf.Max(0, coins);
            hasSetup = true;
            if (IsOpen) Play();
        }

        protected override void OnOpening()
        {
            if (!hasSetup)
            {
                completedLevel = SaveSystem.Data.level;
                starCount = stars.Length;
                reward = GameConfig.Instance != null ? GameConfig.Instance.levelCompleteCoins : 50;
            }
            if (GameManager.Instance != null) GameManager.Instance.SetState(GameState.Won);
            claimed = false;
            Play();
        }

        protected override void OnClosed()
        {
            hasSetup = false;
        }

        private void Play()
        {
            levelText.text = "LEVEL " + completedLevel;
            rewardText.text = "+" + reward;
            claimButton.SetInteractable(true);
            claimLabel.text = "CLAIM";

            for (int i = 0; i < stars.Length; i++)
            {
                RectTransform star = stars[i];
                star.localScale = Vector3.zero;
                if (i >= starCount) continue;

                Tween.Scale(this, star, Vector3.zero, Vector3.one, 0.35f, Ease.OutBack, 0.3f + i * 0.28f, Haptics.Short);
            }

            rewardRoot.localScale = Vector3.zero;
            Tween.Scale(this, rewardRoot, Vector3.zero, Vector3.one, 0.3f, Ease.OutBack, 0.3f + starCount * 0.28f);
        }

        private void Claim()
        {
            if (claimed) return;
            claimed = true;
            SaveSystem.AddCoins(reward);
            claimButton.SetInteractable(false);
            claimLabel.text = "CLAIMED";
            Tween.Scale(this, rewardRoot, Vector3.one * 1.3f, Vector3.one, 0.3f, Ease.OutBack);
        }

        private void NextLevel()
        {
            Claim();
            SaveSystem.Data.level = Mathf.Max(SaveSystem.Data.level, completedLevel + 1);
            SaveSystem.Commit();
            Close();
            SceneLoader.LoadGame();
        }
    }
}
