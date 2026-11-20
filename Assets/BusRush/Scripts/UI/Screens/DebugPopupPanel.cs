using BusRush.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>TEMPORARY: popup previews and level shortcuts in the Game scene. Sits on a <see cref="DebugOnly"/> object.</summary>
    public sealed class DebugPopupPanel : MonoBehaviour
    {
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button levelCompleteButton;
        [SerializeField] private Button outOfSpaceButton;
        [SerializeField] private Button nextLevelButton;
        [SerializeField] private Button clearBoardButton;
        [SerializeField] private Button autoDepartButton;
        [SerializeField] private TMPro.TMP_Text autoDepartLabel;

        private void Start()
        {
            RefreshAutoDepart();
        }

        private void RefreshAutoDepart()
        {
            bool on = GameSession.Current != null && GameSession.Current.DebugAutoDepart;
            autoDepartLabel.text = on ? "Auto depart: ON" : "Auto depart: OFF";
        }

        private void Awake()
        {
            autoDepartButton.onClick.AddListener(() =>
            {
                if (GameSession.Current == null) return;
                GameSession.Current.DebugAutoDepart = !GameSession.Current.DebugAutoDepart;
                RefreshAutoDepart();
            });
            settingsButton.onClick.AddListener(Open<SettingsPopup>);
            levelCompleteButton.onClick.AddListener(Open<LevelCompletePopup>);
            outOfSpaceButton.onClick.AddListener(Open<OutOfSpacePopup>);
            nextLevelButton.onClick.AddListener(() =>
            {
                if (GameSession.Current != null) GameSession.Current.DebugNextLevel();
            });
            clearBoardButton.onClick.AddListener(() =>
            {
                if (GameSession.Current != null) GameSession.Current.DebugClearBoard();
            });
        }

        private static void Open<T>() where T : Popup
        {
            if (UIManager.Instance != null) UIManager.Instance.Open<T>();
        }
    }
}
