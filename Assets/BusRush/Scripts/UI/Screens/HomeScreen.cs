using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>
    /// Home (UI v2, Docs/UI_v2_Design/Home.dc.html): coin pill with "+" (free coins), settings, Daily / Road Map /
    /// Lucky Spin side buttons and the big "Level N" button. Animations live in <see cref="HomeAmbience"/>.
    /// </summary>
    public sealed class HomeScreen : MonoBehaviour
    {
        /// <summary>Coin icon of the pill on screen (target of the coin fly), or null.</summary>
        public static RectTransform CoinTarget { get; private set; }

        [SerializeField] private Button levelButton;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private Button addCoinsButton;
        [SerializeField] private RectTransform coinIcon;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button dailyButton;
        [SerializeField] private GameObject dailyDot;
        [SerializeField] private Button roadMapButton;
        [SerializeField] private Button luckySpinButton;

        private void Awake()
        {
            levelButton.onClick.AddListener(SceneLoader.LoadGame);
            addCoinsButton.onClick.AddListener(() => Open<FreeCoinsPopup>());
            settingsButton.onClick.AddListener(() => Open<SettingsPopup>());
            dailyButton.onClick.AddListener(() => Open<DailyRewardPopup>());
            roadMapButton.onClick.AddListener(() => Toast.Show("Coming soon"));
            luckySpinButton.onClick.AddListener(() => Toast.Show("Coming soon"));
        }

        private void OnEnable()
        {
            CoinTarget = coinIcon;
            SaveSystem.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            SaveSystem.Changed -= Refresh;
            if (CoinTarget == coinIcon) CoinTarget = null;
        }

        private static T Open<T>() where T : Popup => UIManager.Instance != null ? UIManager.Instance.Open<T>() : null;

        private void Refresh()
        {
            levelText.text = "Level " + SaveSystem.Data.level;
            dailyDot.SetActive(DailyRewards.CanClaim);
        }
    }
}
