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

        private void Start()
        {
            if (UIManager.Instance != null) UIManager.Instance.BackPressedWithNoPopup += OpenPause;
        }

        private void OnDisable()
        {
            SaveSystem.Changed -= Refresh;
        }

        private void OnDestroy()
        {
            if (UIManager.Instance != null) UIManager.Instance.BackPressedWithNoPopup -= OpenPause;
        }

        private void Refresh()
        {
            levelText.text = "Level " + SaveSystem.Data.level;
        }

        private static void OpenPause()
        {
            if (UIManager.Instance != null) UIManager.Instance.Open<SettingsPopup>();
        }
    }
}
