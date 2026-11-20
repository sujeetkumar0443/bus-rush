using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>Booster info + buy-with-coins. Tabs: VIP / Arrange / Shuffle.</summary>
    public sealed class BoosterPopup : Popup
    {
        [System.Serializable]
        private struct Tab
        {
            public BoosterType type;
            public Button button;
            public Image face;
            public Image lip;
            public TMP_Text label;
            public Sprite icon;
            public string title;
            [TextArea] public string description;
        }

        [SerializeField] private Tab[] tabs = new Tab[0];
        [SerializeField] private Image icon;
        [SerializeField] private Transform iconRoot;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text ownedText;
        [SerializeField] private TMP_Text lockText;
        [SerializeField] private UIButton buyButton;
        [SerializeField] private TMP_Text buyPrice;
        [SerializeField] private TMP_Text feedbackText;

        [SerializeField] private Color selectedFace = new Color32(0x3A, 0x8D, 0xF5, 0xFF);
        [SerializeField] private Color selectedLip = new Color32(0x1F, 0x5B, 0xB5, 0xFF);
        [SerializeField] private Color idleFace = new Color32(0xDD, 0xE3, 0xEE, 0xFF);
        [SerializeField] private Color idleLip = new Color32(0xB7, 0xC0, 0xD3, 0xFF);
        [SerializeField] private Color selectedText = Color.white;
        [SerializeField] private Color idleText = new Color32(0x4A, 0x56, 0x72, 0xFF);

        private BoosterType current = BoosterType.Vip;
        private Coroutine feedbackAnim;

        protected override void Awake()
        {
            base.Awake();
            foreach (Tab tab in tabs)
            {
                BoosterType type = tab.type;
                tab.button.onClick.AddListener(() => Select(type));
            }
            buyButton.Button.onClick.AddListener(Buy);
        }

        protected override void OnOpening()
        {
            feedbackText.text = string.Empty;
            Refresh(false);
        }

        /// <summary>Selects a tab; call right after Open to show a specific booster.</summary>
        public void Select(BoosterType type)
        {
            current = type;
            feedbackText.text = string.Empty;
            Refresh(true);
        }

        private void Refresh(bool animateIcon)
        {
            GameConfig config = GameConfig.Instance;
            foreach (Tab tab in tabs)
            {
                bool selected = tab.type == current;
                tab.face.color = selected ? selectedFace : idleFace;
                tab.lip.color = selected ? selectedLip : idleLip;
                tab.label.color = selected ? selectedText : idleText;

                if (!selected) continue;
                icon.sprite = tab.icon;
                nameText.text = tab.title;
                descriptionText.text = tab.description;
            }

            int owned = SaveSystem.Data.GetBooster(current);
            ownedText.text = "Owned: " + owned;
            buyPrice.text = config != null ? config.GetBoosterCost(current).ToString() : "?";

            int unlock = config != null ? config.GetBoosterUnlockLevel(current) : 1;
            bool locked = SaveSystem.Data.level < unlock;
            lockText.gameObject.SetActive(locked);
            lockText.text = $"Unlocks at level {unlock}";

            if (animateIcon && iconRoot != null)
            {
                Tween.Scale(this, iconRoot, Vector3.one * 0.7f, Vector3.one, 0.3f, Ease.OutBack);
            }
        }

        private void Buy()
        {
            GameConfig config = GameConfig.Instance;
            int cost = config != null ? config.GetBoosterCost(current) : int.MaxValue;
            Tween.Stop(this, ref feedbackAnim);

            if (SaveSystem.TrySpendCoins(cost))
            {
                SaveSystem.AddBooster(current, 1);
                feedbackText.color = new Color32(0x23, 0x86, 0x3D, 0xFF);
                feedbackText.text = "+1 added!";
                Refresh(true);
                Haptics.Medium();
            }
            else
            {
                feedbackText.color = new Color32(0xEF, 0x3B, 0x3B, 0xFF);
                feedbackText.text = "Not enough coins!";
                Tween.Shake(this, (RectTransform)buyButton.transform);
            }

            feedbackAnim = Tween.Scale(this, feedbackText.transform, Vector3.one * 1.2f, Vector3.one, 0.25f, Ease.OutBack);
        }
    }
}
