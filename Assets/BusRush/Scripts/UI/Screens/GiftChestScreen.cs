using System;
using BusRush.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>
    /// Gift Chest (UI v2, GiftChest.dc.html), Home-scene overlay over the Road Map: the closed chest shakes and grows,
    /// flashes, opens, 12 coins burst out, then the coin + booster reward cards pop in. "Collect x2" (rewarded video)
    /// or "Collect": boosters go to the inventory (toast), coins fly to the map's coin pill. Saved per chest.
    /// </summary>
    public sealed class GiftChestScreen : MonoBehaviour
    {
        [Serializable]
        private struct BurstCoin
        {
            public RectTransform coin;
            public Vector2 offset; // CSS --dx / --dy (y down)
            public float delay;
        }

        [SerializeField] private RawImage blur;
        [SerializeField] private Image dark;
        [SerializeField] private RectTransform rays;
        [SerializeField] private Graphic raysGraphic;
        [SerializeField] private RectTransform title;
        [SerializeField] private CanvasGroup titleGroup;
        [SerializeField] private RectTransform subtitle;
        [SerializeField] private CanvasGroup subtitleGroup;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private Image flash;
        [SerializeField] private RectTransform chestClosed;
        [SerializeField] private CanvasGroup chestClosedGroup;
        [SerializeField] private RectTransform chestOpen;
        [SerializeField] private CanvasGroup chestOpenGroup;
        [SerializeField] private BurstCoin[] coins = new BurstCoin[0];
        [SerializeField] private RectTransform[] rewardCards = new RectTransform[2];
        [SerializeField] private TMP_Text coinAmountText;
        [SerializeField] private Image boosterIcon;
        [SerializeField] private TMP_Text boosterAmountText;
        [SerializeField] private Sprite[] boosterSprites = new Sprite[3];
        [SerializeField] private Image rewardCoinIcon;
        [SerializeField] private RectTransform collectX2;
        [SerializeField] private CanvasGroup collectX2Group;
        [SerializeField] private Button collectX2Button;
        [SerializeField] private RectTransform collectX2Shine;
        [SerializeField] private RectTransform collect;
        [SerializeField] private CanvasGroup collectGroup;
        [SerializeField] private Button collectButton;
        [SerializeField, Range(0f, 1f)] private float darkAlpha = 0.82f;

        public float Elapsed { get; private set; }
        public int Level { get; private set; }

        private RewardSpec reward;
        private Action closed;
        private RectTransform coinTarget;
        private bool busy;
        private Vector2 titleHome, subtitleHome, x2Home, collectHome;
        private static readonly Vector4 Pop = new Vector4(0.3f, 1.5f, 0.5f, 1f), EaseOut = new Vector4(0f, 0f, 0.58f, 1f);
        private static AnimationCurve shakeR, shakeS, dropY, dropS, popS, openS, openA, rise;

        private void Awake()
        {
            titleHome = title.anchoredPosition;
            subtitleHome = subtitle.anchoredPosition;
            x2Home = collectX2.anchoredPosition;
            collectHome = collect.anchoredPosition;
            collectX2Button.onClick.AddListener(() =>
            {
                if (busy) return;
                busy = true;
                Ads.Service.ShowRewarded("gift_chest_x2", granted =>
                {
                    busy = false;
                    if (this == null) return;
                    if (granted) Collect(2);
                    else Toast.Show(Localizer.T("Video not available"));
                });
            });
            collectButton.onClick.AddListener(() => Collect(1));
            BuildCurves();
        }

        private void OnDestroy() => ScreenGrab.Release(blur);

        /// <summary>Opens the chest earned by winning <paramref name="level"/>; <paramref name="onClosed"/> runs after Collect.</summary>
        public void Show(int level, RectTransform coinPill, Action onClosed)
        {
            Level = level;
            reward = MapRewards.ChestReward(level);
            coinTarget = coinPill;
            closed = onClosed;
            busy = false;
            Elapsed = 0f;
            subtitleText.text = Localizer.F("Level {0} reward", level);
            coinAmountText.text = "+" + reward.coins;
            BoosterType booster = reward.MainBooster ?? BoosterType.Vip;
            boosterIcon.sprite = boosterSprites[(int)booster];
            boosterAmountText.text = "x1";
            gameObject.SetActive(true);
            Apply(0f);
            StartCoroutine(ScreenGrab.Blurred(blur, GetComponent<Canvas>()));
            Sfx.Play("chest_open");
        }

        private void Collect(int multiplier)
        {
            if (!gameObject.activeSelf || Elapsed < 1.7f) return;
            RewardSpec r = reward.Times(multiplier);
            MapRewards.MarkChestClaimed(SaveSystem.Data, Level);
            r.GrantBoosters();
            SaveSystem.Commit();
            Haptics.Light();
            Vector3 from = rewardCoinIcon.rectTransform.position;
            Sprite coinSprite = rewardCoinIcon.sprite;
            ScreenGrab.Release(blur);
            gameObject.SetActive(false);
            Toast.Show(r.BoosterText());
            RoadMapScreen map = RoadMapScreen.Instance;
            Transform layer = map != null ? map.transform : transform.parent;
            CoinFly.Play(map, layer, coinSprite, from, coinTarget, r.coins, 8, () => closed?.Invoke());
        }

        private void Update()
        {
            Elapsed += Time.deltaTime;
            Apply(Elapsed);
        }

        private void Apply(float t)
        {
            SetAlpha(dark, darkAlpha * Mathf.Clamp01(t / 0.25f));
            rays.localRotation = Quaternion.Euler(0f, 0f, -360f * (t / 14f % 1f));
            SetAlpha(raysGraphic, Mathf.Clamp01((t - 1.25f) / 0.4f));

            float d = Mathf.Clamp01((t - 0.2f) / 0.45f); // drop .45s from .2s
            title.anchoredPosition = titleHome - new Vector2(0f, dropY.Evaluate(d));
            title.localScale = Vector3.one * dropS.Evaluate(d);
            titleGroup.alpha = Mathf.Clamp01(d * 3f);
            Rise(subtitle, subtitleGroup, subtitleHome, (t - 0.2f) / 0.45f, 24f);

            // Closed chest: chShake 1.1s from .2s, then hide .12s at 1.25s.
            float s = Mathf.Clamp01((t - 0.2f) / 1.1f);
            float hide = Mathf.Clamp01((t - 1.25f) / 0.12f);
            chestClosed.localRotation = Quaternion.Euler(0f, 0f, shakeR.Evaluate(s));
            chestClosed.localScale = Vector3.one * (shakeS.Evaluate(s) * Mathf.Lerp(1f, 1.2f, hide));
            chestClosedGroup.alpha = 1f - hide;

            float f = Mathf.Clamp01((t - 1.25f) / 0.7f);
            flash.gameObject.SetActive(t >= 1.25f && f < 1f);
            flash.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.2f, 3f, CssCurve.Bezier(EaseOut, f));
            SetAlpha(flash, 1f - CssCurve.Bezier(EaseOut, f));

            float o = Mathf.Clamp01((t - 1.25f) / 0.45f);
            chestOpen.localScale = Vector3.one * openS.Evaluate(o);
            chestOpenGroup.alpha = openA.Evaluate(o);

            foreach (BurstCoin c in coins)
            {
                float k = Mathf.Clamp01((t - c.delay) / 1.1f);
                float e = CssCurve.Bezier(EaseOut, k);
                c.coin.gameObject.SetActive(t >= c.delay && k < 1f);
                c.coin.anchoredPosition = new Vector2(c.offset.x, -c.offset.y) * e;
                c.coin.localScale = Vector3.one * Mathf.Lerp(0.4f, 1f, e);
                c.coin.localRotation = Quaternion.Euler(0f, 0f, -360f * e);
                SetAlpha(c.coin.GetComponent<Graphic>(), k < 0.15f ? k / 0.15f : 1f - (k - 0.15f) / 0.85f);
            }

            for (int i = 0; i < rewardCards.Length; i++)
            {
                float p = Mathf.Clamp01((t - (1.7f + 0.2f * i)) / 0.4f);
                rewardCards[i].localScale = Vector3.one * popS.Evaluate(p);
                rewardCards[i].GetComponent<CanvasGroup>().alpha = Mathf.Clamp01(p * 1.4f);
            }

            Rise(collectX2, collectX2Group, x2Home, (t - 2.1f) / 0.45f, 24f);
            Rise(collect, collectGroup, collectHome, (t - 2.3f) / 0.45f, 24f);
            float sh = t >= 1f ? (t - 1f) / 2.4f % 1f : 0f;
            collectX2Shine.anchoredPosition = new Vector2(sh < 0.6f ? Mathf.Lerp(-80f, 320f, CssCurve.Bezier(CssCurve.EaseInOut, sh / 0.6f)) : 320f, 0f);
        }

        private static void Rise(RectTransform rt, CanvasGroup g, Vector2 home, float k, float distance)
        {
            k = Mathf.Clamp01(k);
            float e = rise.Evaluate(k);
            rt.anchoredPosition = home - new Vector2(0f, distance * (1f - e));
            g.alpha = e;
            g.blocksRaycasts = k >= 1f;
        }

        private static void SetAlpha(Graphic g, float a)
        {
            if (g == null) return;
            Color c = g.color;
            c.a = a;
            g.color = c;
        }

        private static void BuildCurves()
        {
            if (shakeR != null) return;
            float[] at = { 0f, 0.15f, 0.3f, 0.45f, 0.6f, 0.8f, 1f };
            shakeR = CssCurve.Build(at, new[] { 0f, 6f, -6f, 7f, -7f, 3f, 0f }, CssCurve.EaseInOut); // CSS deg, negated
            shakeS = CssCurve.Build(at, new[] { 1f, 1.03f, 1.05f, 1.07f, 1.08f, 1.1f, 1f }, CssCurve.EaseInOut);
            dropY = CssCurve.Build(new[] { 0f, 1f }, new[] { 40f, 0f }, Pop);
            dropS = CssCurve.Build(new[] { 0f, 1f }, new[] { 0.8f, 1f }, Pop);
            popS = CssCurve.Build(new[] { 0f, 0.7f, 1f }, new[] { 0.5f, 1.05f, 1f }, Pop);
            openS = CssCurve.Build(new[] { 0f, 0.6f, 1f }, new[] { 0.6f, 1.15f, 1f }, Pop);
            openA = CssCurve.Build(new[] { 0f, 0.6f, 1f }, new[] { 0f, 1f, 1f }, Pop);
            rise = CssCurve.Build(new[] { 0f, 1f }, new[] { 0f, 1f }, EaseOut);
        }
    }
}
