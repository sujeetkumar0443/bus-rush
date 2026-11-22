using BusRush.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>
    /// Hidden test panel in the Game HUD (on a <see cref="DebugOnly"/> object: hidden by default, three-finger tap or a
    /// 1.2 s long-press top-left). Go to level N, +1000 coins, +5 of each booster, Win level, Fail level, Reset save.
    /// </summary>
    public sealed class DebugPopupPanel : MonoBehaviour
    {
        [SerializeField] private Button minusButton;
        [SerializeField] private Button plusButton;
        [SerializeField] private Button plusTenButton;
        [SerializeField] private Button goButton;
        [SerializeField] private TMP_Text goLabel;
        [SerializeField] private Button coinsButton;
        [SerializeField] private Button boostersButton;
        [SerializeField] private Button winButton;
        [SerializeField] private Button failButton;
        [SerializeField] private Button resetButton;

        private int target = 1;

        private void Awake()
        {
            minusButton.onClick.AddListener(() => SetTarget(target - 1));
            plusButton.onClick.AddListener(() => SetTarget(target + 1));
            plusTenButton.onClick.AddListener(() => SetTarget(target + 10));
            goButton.onClick.AddListener(() =>
            {
                SaveSystem.Data.level = target;
                SaveSystem.Commit();
                SceneLoader.LoadGame();
            });
            coinsButton.onClick.AddListener(() =>
            {
                CoinWallet.Add(1000);
                Toast.Show("+1000 coins");
            });
            boostersButton.onClick.AddListener(() =>
            {
                foreach (BoosterType type in new[] { BoosterType.Vip, BoosterType.Arrange, BoosterType.Shuffle }) BoosterInventory.Add(type, 5);
                Toast.Show("+5 of each booster");
            });
            winButton.onClick.AddListener(() =>
            {
                if (GameSession.Current != null) GameSession.Current.DebugClearBoard();
            });
            failButton.onClick.AddListener(() =>
            {
                if (GameSession.Current != null) GameSession.Current.DebugFail();
            });
            resetButton.onClick.AddListener(() =>
            {
                SaveSystem.ResetToDefaults();
                SceneLoader.LoadHome();
            });
        }

        private void Start()
        {
            GameSession session = GameSession.Current;
            SetTarget(session != null && session.Level > 0 ? session.Level : SaveSystem.Data.level);
        }

        private void SetTarget(int level)
        {
            target = Mathf.Clamp(level, 1, LevelPlan.PlannedLevels);
            goLabel.text = "Go to level " + target;
        }
    }
}
