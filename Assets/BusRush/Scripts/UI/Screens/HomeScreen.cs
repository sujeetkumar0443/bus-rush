using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>Home: top bar, side buttons, level path, PLAY, next-unlock progress and the banner slot.</summary>
    public sealed class HomeScreen : MonoBehaviour
    {
        [SerializeField] private Button playButton;
        [SerializeField] private Transform playPulseTarget;
        [SerializeField] private TMP_Text playLevelText;
        [SerializeField] private Button addCoinsButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button shopButton;
        [SerializeField] private Button dailyButton;
        [SerializeField] private GameObject dailyBadge;
        [SerializeField] private Button noAdsButton;
        [SerializeField] private Button boostsButton;
        [SerializeField] private RectTransform unlockFill;
        [SerializeField] private TMP_Text unlockText;
        [SerializeField] private TMP_Text unlockProgressText;

        private Coroutine playPulse;
        private Coroutine badgePulse;

        private void Awake()
        {
            playButton.onClick.AddListener(SceneLoader.LoadGame);
            addCoinsButton.onClick.AddListener(() => Open<ShopPopup>());
            shopButton.onClick.AddListener(() => Open<ShopPopup>());
            noAdsButton.onClick.AddListener(() => Open<ShopPopup>());
            dailyButton.onClick.AddListener(() => Open<DailyRewardPopup>());
            boostsButton.onClick.AddListener(() =>
            {
                BoosterPopup popup = Open<BoosterPopup>();
                if (popup != null) popup.Select(BoosterType.Vip);
            });
            settingsButton.onClick.AddListener(() => Open<SettingsPopup>());
        }

        private void OnEnable()
        {
            SaveSystem.Changed += Refresh;
            dailyBadge.transform.localScale = Vector3.one;
            Refresh();
            playPulseTarget.localScale = Vector3.one;
            playPulse = Tween.Pulse(this, playPulseTarget, 0.04f, 1.4f);
        }

        private void OnDisable()
        {
            SaveSystem.Changed -= Refresh;
            playPulse = null;
            badgePulse = null;
        }

        private static T Open<T>() where T : Popup
        {
            return UIManager.Instance != null ? UIManager.Instance.Open<T>() : null;
        }

        private void Refresh()
        {
            SaveData data = SaveSystem.Data;
            playLevelText.text = "Level " + data.level;
            noAdsButton.gameObject.SetActive(!data.adsRemoved);

            bool canClaim = DailyRewards.CanClaim;
            if (dailyBadge.activeSelf != canClaim || (canClaim && badgePulse == null))
            {
                dailyBadge.SetActive(canClaim);
                Tween.Stop(this, ref badgePulse);
                if (canClaim) badgePulse = Tween.Pulse(this, dailyBadge.transform, 0.15f, 0.8f);
            }

            GameConfig config = GameConfig.Instance;
            if (config != null && config.TryGetNextUnlock(data.level, out GameConfig.Unlock next, out int previous))
            {
                float span = Mathf.Max(1, next.level - previous);
                float k = Mathf.Clamp01((data.level - previous) / span);
                unlockFill.anchorMax = new Vector2(Mathf.Lerp(0.08f, 1f, k), unlockFill.anchorMax.y);
                unlockText.text = "Next: " + next.name;
                unlockProgressText.text = $"{data.level}/{next.level}";
            }
            else
            {
                unlockFill.anchorMax = new Vector2(1f, unlockFill.anchorMax.y);
                unlockText.text = "Everything unlocked!";
                unlockProgressText.text = string.Empty;
            }
        }
    }
}
