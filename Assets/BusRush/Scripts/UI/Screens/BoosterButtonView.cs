using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>
    /// HUD booster button: count badge, "+" badge when empty (opens the booster popup) and a lock before its unlock level.
    /// Using a booster is gameplay and comes in a later step.
    /// </summary>
    public sealed class BoosterButtonView : MonoBehaviour
    {
        [SerializeField] private BoosterType type;
        [SerializeField] private Button button;
        [SerializeField] private GameObject countBadge;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private GameObject plusBadge;
        [SerializeField] private GameObject lockOverlay;
        [SerializeField] private TMP_Text lockText;
        [Tooltip("Booster name; hidden while locked (the lock overlay shows the unlock level in its place).")]
        [SerializeField] private GameObject nameLabel;

        public BoosterType Type => type;

        private void Awake()
        {
            button.onClick.AddListener(OnClick);
        }

        private void OnEnable()
        {
            SaveSystem.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            SaveSystem.Changed -= Refresh;
        }

        private bool IsLocked
        {
            get
            {
                int unlock = GameConfig.Instance != null ? GameConfig.Instance.GetBoosterUnlockLevel(type) : 1;
                return SaveSystem.Data.level < unlock;
            }
        }

        private void Refresh()
        {
            int count = SaveSystem.Data.GetBooster(type);
            bool locked = IsLocked;
            int unlock = GameConfig.Instance != null ? GameConfig.Instance.GetBoosterUnlockLevel(type) : 1;

            lockOverlay.SetActive(locked);
            lockText.text = "LV " + unlock;
            if (nameLabel != null) nameLabel.SetActive(!locked);
            countBadge.SetActive(!locked && count > 0);
            plusBadge.SetActive(!locked && count == 0);
            countText.text = count.ToString();
        }

        private void OnClick()
        {
            if (IsLocked)
            {
                Tween.Shake(this, (RectTransform)transform, 8f);
                return;
            }

            if (SaveSystem.Data.GetBooster(type) <= 0)
            {
                BoosterPopup popup = UIManager.Instance != null ? UIManager.Instance.Open<BoosterPopup>() : null;
                if (popup != null) popup.Select(type);
                return;
            }

            Debug.Log($"[BusRush] Booster {type} pressed (gameplay not implemented yet).");
        }
    }
}
