using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>Gameplay HUD shell: restart, level label, pause, booster row. No gameplay logic yet.</summary>
    public sealed class GameHUD : MonoBehaviour
    {
        [SerializeField] private Button restartButton;
        [SerializeField] private Button pauseButton;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text passengersText;
        [SerializeField] private LevelBanner banner;

        private int shownPassengers = -1;

        private void Awake()
        {
            restartButton.onClick.AddListener(SceneLoader.LoadGame);
            pauseButton.onClick.AddListener(OpenPause);
        }

        private void OnEnable()
        {
            SaveSystem.Changed += Refresh;
            Refresh();
        }

        private System.Collections.IEnumerator Start()
        {
            if (UIManager.Instance != null) UIManager.Instance.BackPressedWithNoPopup += OpenPause;

            // Level intro banner, once the session has picked its level.
            yield return null;
            Gameplay.GameSession session = Gameplay.GameSession.Current;
            if (banner != null && session != null && session.Level > 0)
            {
                Refresh();
                banner.Play(session.Level, LevelPlan.Get(session.Level).tier);
            }
        }

        private void OnDisable()
        {
            SaveSystem.Changed -= Refresh;
        }

        private void Update()
        {
            if (passengersText == null) return;
            Gameplay.GameSession session = Gameplay.GameSession.Current;
            int left = session != null ? session.PassengersLeft : 0;
            if (left == shownPassengers) return;
            shownPassengers = left;
            passengersText.text = $"Passengers left {left}";
        }

        private void OnDestroy()
        {
            if (UIManager.Instance != null) UIManager.Instance.BackPressedWithNoPopup -= OpenPause;
        }

        private void Refresh()
        {
            // The session's level, not the save: the save already points at the next level once the board is cleared.
            Gameplay.GameSession session = Gameplay.GameSession.Current;
            int level = session != null && session.Level > 0 ? session.Level : SaveSystem.Data.level;
            levelText.text = "Level " + level;
        }

        private static void OpenPause()
        {
            if (UIManager.Instance != null) UIManager.Instance.Open<SettingsPopup>();
        }
    }
}
