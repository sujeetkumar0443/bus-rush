using TMPro;
using UnityEngine;

namespace BusRush.Gameplay
{
    /// <summary>Visuals of one bay: painted slot decal (VIP / open / locked) and the capacity pill above a parked vehicle.</summary>
    public sealed class BayView : MonoBehaviour
    {
        [SerializeField] private GameObject openGroup;
        [SerializeField] private GameObject vipGroup;
        [SerializeField] private GameObject lockedGroup;
        [SerializeField] private TMP_Text lockedPriceText;
        [SerializeField] private GameObject pill;
        [SerializeField] private TMP_Text pillText;
        [SerializeField] private Collider tapCollider;

        public BayKind Kind { get; private set; }
        public int Index { get; private set; }

        public void Setup(int index, BayKind kind, int unlockCost)
        {
            Index = index;
            name = $"Bay{index}_{kind}";
            if (lockedPriceText != null) lockedPriceText.text = unlockCost.ToString();
            SetKind(kind);
            ShowPill(false);
        }

        public void SetKind(BayKind kind)
        {
            Kind = kind;
            openGroup.SetActive(kind != BayKind.Locked);
            vipGroup.SetActive(kind == BayKind.Vip);
            lockedGroup.SetActive(kind == BayKind.Locked && !lockLabelHidden);
            if (tapCollider != null) tapCollider.enabled = kind == BayKind.Locked;
        }

        public void ShowPill(bool visible) => pill.SetActive(visible);

        public Vector3 PillWorldPosition => pill.transform.position;

        public void SetPill(int filled, int capacity) => pillText.text = $"{filled}/{capacity}";

        /// <summary>VIP booster: a short glow pulse on the crown and label.</summary>
        public void PlayVipOpen()
        {
            if (vipGroup == null) return;
            BusRush.UI.Tween.Scale(this, vipGroup.transform, Vector3.one * 1.45f, Vector3.one, 0.5f, BusRush.UI.Ease.OutBack);
        }

        /// <summary>Tap on a locked bay: buy it (or offer the extra-bay popup).</summary>
        public void RequestUnlock()
        {
            if (GameSession.Current != null) GameSession.Current.RequestBayUnlock();
        }

        /// <summary>The HUD draws the lock + price tag for locked bays; the world label is then hidden.</summary>
        public void HideLockLabel()
        {
            lockLabelHidden = true;
            SetKind(Kind);
        }

        private bool lockLabelHidden;
    }
}
