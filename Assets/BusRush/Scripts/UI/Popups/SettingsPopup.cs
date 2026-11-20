using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>
    /// Settings (from Home) / Pause (from Game). Toggles are saved immediately.
    /// In the Game scene it pauses the game and shows Home / Restart / Resume.
    /// </summary>
    public sealed class SettingsPopup : Popup
    {
        [SerializeField] private TMP_Text title;
        [SerializeField] private SwitchToggle sound;
        [SerializeField] private SwitchToggle music;
        [SerializeField] private SwitchToggle vibration;
        [SerializeField] private GameObject gameButtons;
        [SerializeField] private Button homeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button resumeButton;
        [SerializeField] private RectTransform panelForResize;
        [SerializeField] private float homePanelHeight = 400f;
        [SerializeField] private float gamePanelHeight = 520f;

        private bool pauseMode;

        protected override void Awake()
        {
            base.Awake();
            sound.Toggle.onValueChanged.AddListener(v => Set(d => d.soundOn = v));
            music.Toggle.onValueChanged.AddListener(v => Set(d => d.musicOn = v));
            vibration.Toggle.onValueChanged.AddListener(v =>
            {
                Set(d => d.vibrationOn = v);
                if (v) Haptics.Medium();
            });
            homeButton.onClick.AddListener(() =>
            {
                Close();
                SceneLoader.LoadHome();
            });
            restartButton.onClick.AddListener(() =>
            {
                Close();
                SceneLoader.LoadGame();
            });
            resumeButton.onClick.AddListener(Close);
        }

        protected override void OnOpening()
        {
            pauseMode = SceneManager.GetActiveScene().name == SceneLoader.GameScene;
            title.text = pauseMode ? "PAUSED" : "SETTINGS";
            gameButtons.SetActive(pauseMode);
            if (panelForResize != null)
            {
                panelForResize.sizeDelta = new Vector2(panelForResize.sizeDelta.x, pauseMode ? gamePanelHeight : homePanelHeight);
            }

            SaveData data = SaveSystem.Data;
            sound.SetSilently(data.soundOn);
            music.SetSilently(data.musicOn);
            vibration.SetSilently(data.vibrationOn);

            if (pauseMode && GameManager.Instance != null && GameManager.Instance.State == GameState.Playing)
            {
                GameManager.Instance.SetState(GameState.Paused);
            }
        }

        protected override void OnClosed()
        {
            if (pauseMode && GameManager.Instance != null && GameManager.Instance.State == GameState.Paused)
            {
                GameManager.Instance.SetState(GameState.Playing);
            }
        }

        private static void Set(System.Action<SaveData> change)
        {
            change(SaveSystem.Data);
            SaveSystem.Commit();
        }
    }
}
