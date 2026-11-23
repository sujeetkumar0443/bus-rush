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
        [Header("UI v2 locked look")]
        [Tooltip("White tile shown while unlocked.")]
        [SerializeField] private GameObject unlockedTile;
        [Tooltip("Grey tile shown while locked.")]
        [SerializeField] private GameObject lockedTile;
        [SerializeField] private Image icon;
        [SerializeField] private Color lockedIconTint = new Color(0.55f, 0.6f, 0.68f, 0.55f);

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
            Localizer.Changed += Refresh;
            BoosterInventory.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            Localizer.Changed -= Refresh;
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
            lockText.text = Localizer.F("LV {0}", unlock);
            if (nameLabel != null) nameLabel.SetActive(!locked || unlockedTile != null);
            if (unlockedTile != null) unlockedTile.SetActive(!locked);
            if (lockedTile != null) lockedTile.SetActive(locked);
            if (icon != null) icon.color = locked ? lockedIconTint : Color.white;
            countBadge.SetActive(!locked && count > 0);
            plusBadge.SetActive(!locked && count == 0);
            countText.text = count.ToString();
        }

        private void OnClick()
        {
            if (IsLocked)
            {
                Toast.Show(Localizer.F("Unlocks at level {0}", BoosterInventory.UnlockLevel(type)));
                Shake();
                return;
            }

            if (BoosterInventory.Count(type) <= 0)
            {
                OpenBoosterPopup();
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
                else if (outcome == Gameplay.BoosterOutcome.NoneLeft) OpenBoosterPopup();
                else Shake();
            });
        }

        private void OpenBoosterPopup()
        {
            BoosterPopup popup = UIManager.Instance != null ? UIManager.Instance.Open<BoosterPopup>() : null;
            if (popup != null) popup.Select(type);
        }

        private void Shake()
        {
            Tween.Shake(this, (RectTransform)transform, 8f);
            Haptics.Light();
        }
    }
}
