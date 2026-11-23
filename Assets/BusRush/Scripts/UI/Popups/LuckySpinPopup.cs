using System.Collections;
using BusRush.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>
    /// Lucky Spin (UI v2, LuckySpin.dc.html) on the blurred popup base: 8-segment wheel with a gold rim and 12 blinking
    /// bulbs, top pointer, slow rays. "Spin" (rewarded video) picks the prize by weight first, then turns the wheel
    /// 6 turns + the landing angle in 4.2 s (cubic-bezier(.12,.75,.15,1)) with a tick per divider; then "You won!"
    /// pops and Collect pays out (coins fly to the pill, boosters go to the inventory). 3 spins per day.
    /// </summary>
    public sealed class LuckySpinPopup : V2Popup
    {
        [SerializeField] private RectTransform wheel;
        [SerializeField] private RectTransform rays;
        [SerializeField] private Image[] bulbs = new Image[12];
        [SerializeField] private RectTransform spinRoot;
        [SerializeField] private Button spinButton;
        [SerializeField] private CanvasGroup spinGroup;
        [SerializeField] private RectTransform spinShine;
        [SerializeField] private TMP_Text spinsText;

        [Header("You won")]
        [SerializeField] private GameObject wonRoot;
        [SerializeField] private RectTransform wonCard;
        [SerializeField] private Image wonIcon;
        [SerializeField] private TMP_Text wonAmount;
        [SerializeField] private Button collectButton;
        [SerializeField] private Sprite coinSprite;
        [SerializeField] private Sprite vipSprite;
        [SerializeField] private Sprite arrangeSprite;
        [SerializeField] private Sprite shuffleSprite;

        private static readonly Vector4 SpinEase = new Vector4(0.12f, 0.75f, 0.15f, 1f);
        private readonly System.Random rng = new System.Random();
        private float rotation; // CSS degrees, clockwise
        private bool spinning;
        private RewardSpec won;
        private float clock;

        public bool IsSpinning => spinning;

        /// <summary>Not while the wheel is committed (ad, turning, or the prize card waiting for Collect).</summary>
        public override bool CanClose => !spinning;
        public float Rotation => rotation;

        protected override void Awake()
        {
            base.Awake();
            spinButton.onClick.AddListener(Spin);
            collectButton.onClick.AddListener(Collect);
        }

        protected override void OnOpening()
        {
            base.OnOpening();
            wonRoot.SetActive(false);
            spinning = false;
            clock = 0f;
            RecoverPending();
            Refresh();
        }

        /// <summary>A result that was never paid (left mid-spin, app killed): pay it now.</summary>
        private void RecoverPending()
        {
            RewardSpec? r = LuckySpin.GrantPending();
            if (r.HasValue) Toast.Show(r.Value.HasBoosters ? r.Value.BoosterText() : Localizer.F("+{0} coins", r.Value.coins));
        }

        private void OnEnable() => Localizer.Changed += Refresh;

        private void OnDisable()
        {
            Localizer.Changed -= Refresh;
            // Leaving mid-spin (scene change, forced close): the result was decided already, so it is still granted.
            if (spinning) LuckySpin.GrantPending();
        }

        private void Refresh()
        {
            int left = LuckySpin.SpinsLeftToday;
            spinsText.text = left <= 0 ? Localizer.T("Come back tomorrow") : left == 1 ? Localizer.T("1 spin left today") : Localizer.F("{0} spins left today", left);
            bool can = left > 0 && !spinning;
            spinButton.interactable = can;
            spinGroup.alpha = left > 0 ? 1f : 0.55f;
            SetCloseInteractable(!spinning);
        }

        private void Spin()
        {
            if (spinning || LuckySpin.SpinsLeftToday <= 0) return;
            spinning = true;
            Refresh();
            Ads.Service.ShowRewarded("lucky_spin", granted =>
            {
                if (this == null) return;
                if (!granted)
                {
                    spinning = false;
                    Refresh();
                    Toast.Show(Localizer.T("Video not available"));
                    return;
                }
                LuckySpin.UseSpin();
                int segment = LuckySpin.Pick(rng);
                LuckySpin.SetPending(segment); // saved before the wheel moves
                StartCoroutine(Turn(segment));
            });
        }

        private IEnumerator Turn(int segment)
        {
            float from = rotation;
            float to = LuckySpin.TargetRotation(from, segment, (float)(rng.NextDouble() * 2.0 - 1.0));
            int lastDivider = Mathf.FloorToInt(from / LuckySpin.SegmentAngle);
            const float duration = 4.2f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                SetRotation(Mathf.LerpUnclamped(from, to, CssCurve.Bezier(SpinEase, t / duration)));
                int divider = Mathf.FloorToInt(rotation / LuckySpin.SegmentAngle);
                if (divider != lastDivider)
                {
                    lastDivider = divider;
                    Sfx.Play("spin_tick");
                    Haptics.Light();
                }
                yield return null;
            }
            SetRotation(to);
            yield return new WaitForSecondsRealtime(0.25f);
            ShowResult(segment);
        }

        private void SetRotation(float cssDegrees)
        {
            rotation = cssDegrees;
            wheel.localRotation = Quaternion.Euler(0f, 0f, -cssDegrees); // CSS clockwise = Unity negative z
        }

        /// <summary>Shows the "You won!" card for a segment (also used by the screenshot tool).</summary>
        public void ShowResult(int segment)
        {
            won = GameTuning.LuckySpinSegments[segment];
            BoosterType? booster = won.MainBooster;
            wonIcon.sprite = booster == BoosterType.Vip ? vipSprite : booster == BoosterType.Arrange ? arrangeSprite
                : booster == BoosterType.Shuffle ? shuffleSprite : coinSprite;
            wonAmount.text = booster.HasValue ? "x1" : "+" + won.coins;
            wonRoot.SetActive(true);
            Tween.Scale(this, wonCard, Vector3.one * 0.5f, Vector3.one, 0.45f, Ease.OutBack);
            Sfx.Play("spin_win");
            Haptics.Light();
        }

        private void Collect()
        {
            if (!wonRoot.activeSelf) return;
            wonRoot.SetActive(false);
            LuckySpin.ClearPending();
            won.GrantBoosters();
            if (won.HasBoosters) Toast.Show(won.BoosterText());
            if (won.coins > 0)
            {
                UIManager ui = UIManager.Instance;
                CoinFly.Play(ui, ui != null ? ui.transform : null, coinSprite, wonIcon.rectTransform.position, HomeScreen.CoinTarget, won.coins, 8);
            }
            won = default;
            spinning = false;
            Refresh();
        }

        protected override void Update()
        {
            base.Update();
            if (!IsOpen) return;
            clock += Time.unscaledDeltaTime;
            rays.localRotation = Quaternion.Euler(0f, 0f, -360f * (clock / 14f % 1f));
            // Bulbs blink .8 s, every other one half a period later: #FFF6B0 + glow <-> white.
            for (int i = 0; i < bulbs.Length; i++)
            {
                float k = Mathf.Repeat((clock - (i % 2) * 0.4f) / 0.8f, 1f);
                float lit = Mathf.Abs(2f * k - 1f); // linear: lit at 0% / 100%, plain white at 50%
                bulbs[i].color = Color.Lerp(Color.white, new Color(1f, 0.96f, 0.69f), lit);
                if (bulbs[i].transform.childCount > 0 && bulbs[i].transform.GetChild(0).TryGetComponent(out Image halo))
                {
                    halo.color = new Color(1f, 0.89f, 0.48f, 0.85f * lit);
                }
            }
            float pulse = 1f + 0.05f * (0.5f - 0.5f * Mathf.Cos(clock / 1.2f * Mathf.PI * 2f));
            spinRoot.localScale = Vector3.one * (spinButton.interactable ? pulse : 1f);
            float sh = clock >= 1f ? (clock - 1f) / 2.4f % 1f : 0f;
            spinShine.anchoredPosition = new Vector2(sh < 0.6f ? Mathf.Lerp(-80f, 320f, CssCurve.Bezier(CssCurve.EaseInOut, sh / 0.6f)) : 320f, 0f);
        }
    }
}
