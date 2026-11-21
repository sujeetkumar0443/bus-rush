using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>
    /// Booster popup (UI v2, Docs/UI_v2_Design/BoosterPopup.dc.html) over a blurred, dimmed game: ribbon in the
    /// booster's colour, icon floating over rotating rays, a one-line description, "You have N", "Free +1" (rewarded
    /// video) and "Buy 1 [coin] price" (shakes when coins are short). Also the "extra bay" offer for locked bays.
    /// </summary>
    public sealed class BoosterPopup : Popup
    {
        [Serializable]
        private struct Style
        {
            public string title;
            [TextArea] public string description;
            public Sprite icon;
            public Color top;
            public Color bottom;
            public Color edge;
            public Material titleMaterial;
        }

        [Tooltip("Vip, Arrange, Shuffle, Extra bay.")]
        [SerializeField] private Style[] styles = new Style[4];

        [SerializeField] private RawImage blur;
        [SerializeField] private RectTransform ribbon;
        [SerializeField] private CanvasGroup ribbonGroup;
        [SerializeField] private Image ribbonFace;
        [SerializeField] private UiGradient ribbonGradient;
        [SerializeField] private Image[] ribbonEdges = new Image[0];
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private RectTransform rays;
        [SerializeField] private RectTransform iconFloat;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private GameObject ownedPill;
        [SerializeField] private TMP_Text ownedText;
        [SerializeField] private RectTransform freeRoot;
        [SerializeField] private Button freeButton;
        [SerializeField] private RectTransform freeShine;
        [SerializeField] private Button buyButton;
        [SerializeField] private CanvasGroup buyGroup;
        [SerializeField] private TMP_Text buyPrice;

        private const int ExtraBay = 3;
        private BoosterType current = BoosterType.Vip;
        private bool extraBay;
        private float clock;
        private bool busy;

        private static readonly Vector4 Pop = new Vector4(0.3f, 1.5f, 0.5f, 1f);
        private static AnimationCurve dropY, dropS, dropA, floatY, pulseS, shineX;

        protected override void Awake()
        {
            base.Awake();
            freeButton.onClick.AddListener(Free);
            buyButton.onClick.AddListener(Buy);
            ribbonHomeY = ribbon.anchoredPosition.y;
            iconHomeY = iconFloat.anchoredPosition.y;
            if (dropY == null)
            {
                float[] ends = { 0f, 1f }, half = { 0f, 0.5f, 1f };
                dropY = CssCurve.Build(ends, new[] { 40f, 0f }, Pop);
                dropS = CssCurve.Build(ends, new[] { 0.8f, 1f }, Pop);
                dropA = CssCurve.Build(ends, new[] { 0f, 1f }, Pop);
                floatY = CssCurve.Build(half, new[] { 0f, 6f, 0f }, CssCurve.EaseInOut);
                pulseS = CssCurve.Build(half, new[] { 1f, 1.05f, 1f }, CssCurve.EaseInOut);
                shineX = CssCurve.Build(new[] { 0f, 0.6f, 1f }, new[] { -80f, 320f, 320f }, CssCurve.EaseInOut);
            }
        }

        protected override void OnOpening()
        {
            clock = 0f;
            busy = false;
            Canvas canvas = GetComponentInParent<Canvas>();
            if (blur != null && canvas != null) StartCoroutine(ScreenGrab.Blurred(blur, canvas.rootCanvas));
            Refresh();
            Animate();
        }

        protected override void OnClosed()
        {
            extraBay = false;
        }

        private void OnDestroy() => ScreenGrab.Release(blur);

        /// <summary>Shows a booster (call right after Open).</summary>
        public void Select(BoosterType type)
        {
            current = type;
            extraBay = false;
            Refresh();
        }

        /// <summary>Shows the "extra bay" offer (locked bay tapped without enough coins).</summary>
        public void ShowExtraBay()
        {
            extraBay = true;
            Refresh();
        }

        private int StyleIndex => extraBay ? ExtraBay : (int)current;

        private int Price => extraBay
            ? (GameConfig.Instance != null ? GameConfig.Instance.extraBayCost : 200)
            : BoosterInventory.PriceCoins(current);

        private void Refresh()
        {
            Style s = styles[Mathf.Clamp(StyleIndex, 0, styles.Length - 1)];
            titleText.text = s.title;
            if (s.titleMaterial != null) titleText.fontSharedMaterial = s.titleMaterial;
            ribbonGradient.Set(s.top, s.bottom);
            foreach (Image e in ribbonEdges) e.color = s.edge;
            icon.sprite = s.icon;
            descriptionText.text = s.description;
            ownedPill.SetActive(!extraBay);
            ownedText.text = "You have " + BoosterInventory.Count(current);
            buyPrice.text = Price.ToString();
            bool affordable = CoinWallet.Coins >= Price;
            buyGroup.alpha = affordable ? 1f : 0.55f;
        }

        private void Update()
        {
            if (!IsOpen) return;
            clock += Time.unscaledDeltaTime;
            Animate();
        }

        private void Animate()
        {
            float k = Mathf.Clamp01((clock - 0.2f) / 0.45f); // ribbon drop .45s from .2s
            ribbon.anchoredPosition = new Vector2(ribbon.anchoredPosition.x, ribbonHomeY - dropY.Evaluate(k));
            ribbon.localScale = Vector3.one * dropS.Evaluate(k);
            ribbonGroup.alpha = dropA.Evaluate(k);
            rays.localRotation = Quaternion.Euler(0f, 0f, -360f * (clock / 14f % 1f));
            iconFloat.anchoredPosition = new Vector2(iconFloat.anchoredPosition.x, iconHomeY + floatY.Evaluate(clock / 2.2f % 1f));
            freeRoot.localScale = Vector3.one * pulseS.Evaluate(clock / 1.2f % 1f);
            float sh = clock >= 1f ? (clock - 1f) / 2.4f % 1f : 0f;
            freeShine.anchoredPosition = new Vector2(shineX.Evaluate(sh), 0f);
        }

        private float ribbonHomeY, iconHomeY;

        // ------------------------------------------------------------------ actions

        private void Free()
        {
            if (busy) return;
            busy = true;
            bool bay = extraBay;
            BoosterType type = current;
            Ads.Service.ShowRewarded(bay ? "extra_bay" : "booster_" + type, granted =>
            {
                busy = false;
                if (this == null) return;
                if (!granted)
                {
                    Toast.Show("Video not available");
                    return;
                }
                Haptics.Light();
                if (bay)
                {
                    if (Gameplay.GameSession.Current != null) Gameplay.GameSession.Current.UnlockBayFree();
                    Close();
                    return;
                }
                BoosterInventory.Add(type, 1);
                Refresh();
                Tween.Scale(this, iconFloat, Vector3.one * 1.25f, Vector3.one, 0.3f, Ease.OutBack);
            });
        }

        private void Buy()
        {
            if (busy) return;
            if (!CoinWallet.TrySpend(Price))
            {
                Tween.Shake(this, (RectTransform)buyButton.transform, 10f);
                Haptics.Light();
                Toast.Show("Not enough coins!");
                return;
            }
            Haptics.Light();
            if (extraBay)
            {
                if (Gameplay.GameSession.Current != null) Gameplay.GameSession.Current.UnlockBayFree();
                Close();
                return;
            }
            BoosterInventory.Add(current, 1);
            Refresh();
            Tween.Scale(this, iconFloat, Vector3.one * 1.25f, Vector3.one, 0.3f, Ease.OutBack);
        }
    }
}
