using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>
    /// Pause (UI v2, Pause.dc.html), Game scene: "PAUSED" ribbon, "Level N", Sound / Music / Vibration rows and
    /// Resume / Restart / Home. Game time is paused while it is open (GameState.Paused); close X = Resume.
    /// </summary>
    public sealed class PausePopup : V2Popup
    {
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button homeButton;

        protected override void Awake()
        {
            base.Awake();
            resumeButton.onClick.AddListener(Close);
            restartButton.onClick.AddListener(() =>
            {
                Close();
                SceneLoader.LoadGame();
            });
            homeButton.onClick.AddListener(() =>
            {
                Close();
                SceneLoader.LoadHome();
            });
        }

        protected override void OnOpening()
        {
            base.OnOpening();
            Gameplay.GameSession session = Gameplay.GameSession.Current;
            int level = session != null && session.Level > 0 ? session.Level : SaveSystem.Data.level;
            levelText.text = "Level " + level;
            if (GameManager.Instance != null && GameManager.Instance.State == GameState.Playing)
            {
                GameManager.Instance.SetState(GameState.Paused);
            }
        }

        protected override void OnClosed()
        {
            if (GameManager.Instance != null && GameManager.Instance.State == GameState.Paused)
            {
                GameManager.Instance.SetState(GameState.Playing);
            }
        }
    }
}
