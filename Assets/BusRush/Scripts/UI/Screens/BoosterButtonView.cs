using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>
    /// HUD booster button: count badge, "+" badge when empty (opens the booster popup) and a lock ("LV n") before its
    /// unlock level. Hidden in the tutorial levels (before GameTuning.BoosterRowFromLevel). A tap uses the booster through
    /// <see cref="BoosterController"/>; if it cannot be used right now (board busy, nothing to do) the button shakes and
    /// nothing is spent.
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

        private System.Collections.IEnumerator Start()
        {
            yield return null; // the session picks its level in Start
            if (!BoosterInventory.RowVisible(Level)) gameObject.SetActive(false);
            else Refresh();
        }

        private void OnEnable()
        {
            BoosterInventory.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            BoosterInventory.Changed -= Refresh;
        }

        /// <summary>The level being played (the save already points at the next one once a level is won).</summary>
        private static int Level
        {
            get
            {
                Gameplay.GameSession session = Gameplay.GameSession.Current;
                return session != null && session.Level > 0 ? session.Level : SaveSystem.Data.level;
            }
        }

        private bool IsLocked => !BoosterInventory.IsUnlocked(type, Level);

        private void Refresh()
        {
            int count = BoosterInventory.Count(type);
            bool locked = IsLocked;
            int unlock = BoosterInventory.UnlockLevel(type);

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
                Toast.Show($"Unlocks at level {BoosterInventory.UnlockLevel(type)}");
                Shake();
                return;
            }

            if (BoosterInventory.Count(type) <= 0)
            {
                OpenShop();
                return;
            }

            Gameplay.BoosterController controller = Gameplay.BoosterController.Current;
            if (controller == null)
            {
                Shake();
                return;
            }
            controller.Request(type, outcome =>
            {
                if (outcome == Gameplay.BoosterOutcome.Used) Tween.Scale(this, transform, Vector3.one * 1.15f, Vector3.one, 0.25f, Ease.OutBack);
                else if (outcome == Gameplay.BoosterOutcome.NoneLeft) OpenShop();
                else Shake();
            });
        }

        private void OpenShop()
        {
            BoosterPopup popup = UIManager.Instance != null ? UIManager.Instance.Open<BoosterPopup>() : null;
            if (popup != null) popup.Select(type);
        }

        private void Shake()
        {
            Tween.Shake(this, (RectTransform)transform, 8f);
            Haptics.Short();
        }
    }
}
