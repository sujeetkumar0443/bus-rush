using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>TEMPORARY: opens popups in the Game scene for previewing. Sits on a <see cref="DebugOnly"/> object.</summary>
    public sealed class DebugPopupPanel : MonoBehaviour
    {
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button levelCompleteButton;
        [SerializeField] private Button outOfSpaceButton;

        private void Awake()
        {
            settingsButton.onClick.AddListener(Open<SettingsPopup>);
            levelCompleteButton.onClick.AddListener(Open<LevelCompletePopup>);
            outOfSpaceButton.onClick.AddListener(Open<OutOfSpacePopup>);
        }

        private static void Open<T>() where T : Popup
        {
            if (UIManager.Instance != null) UIManager.Instance.Open<T>();
        }
    }
}
