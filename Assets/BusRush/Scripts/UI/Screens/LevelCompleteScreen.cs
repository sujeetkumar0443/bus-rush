using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>
    /// Level Complete (Docs/UI_v2_Design/LevelComplete.dc.html): full-screen overlay in the Game scene over a blurred,
    /// darkened grab of the game. Rays, confetti, ribbon + "COMPLETE!", jumping hero, stars popping in (earned gold,
    /// missing dim), reward pill, "Claim x3" (rewarded video) and "Continue with X". Coins then fly to the coin pill and
    /// the Road Map opens with this level's unlock. Every animation is the design's CSS keyframes (AnimationCurves).
    /// </summary>
    public sealed class LevelCompleteScreen : MonoBehaviour
    {
        public static LevelCompleteScreen Instance { get; private set; }

        /// <summary>The screen in the loaded Game scene (it starts inactive, so Instance is only set once shown).</summary>
        public static LevelCompleteScreen Find() =>
            Instance != null ? Instance : FindAnyObjectByType<LevelCompleteScreen>(FindObjectsInactive.Include);

        [Serializable]
        private struct Confetti
        {
            public RectTransform piece;
            public float left;
            public float fallDuration;
            public float swayDuration;
            public float delay;
        }

        [Header("Background")]
        [SerializeField] private RawImage blurImage;
        [SerializeField] private RectTransform rays;
        [SerializeField] private Confetti[] confetti = new Confetti[0];

        [Header("Title")]
        [SerializeField] private RectTransform ribbon;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private RectTransform title;
        [SerializeField] private TMP_Text noteText;

        [Header("Hero")]
        [SerializeField] private RectTransform heroIn;
        [SerializeField] private RectTransform hero;
        [SerializeField] private Image heroShadow;

        [Header("Stars")]
        [SerializeField] private Image[] stars = new Image[3];
        [SerializeField] private Image[] flashes = new Image[3];
        [SerializeField] private float[] starRotations = { -14f, 0f, 14f };
        [SerializeField] private float[] starDelays = { 0.7f, 1.0f, 1.3f };

        [Header("Reward + buttons")]
        [SerializeField] private RectTransform rewardPill;
        [SerializeField] private CanvasGroup rewardGroup;
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private Image rewardCoin;
        [SerializeField] private RectTransform claim;
        [SerializeField] private CanvasGroup claimGroup;
        [SerializeField] private Button claimButton;
        [SerializeField] private TMP_Text claimCoinsText;
        [SerializeField] private RectTransform claimShine;
        [SerializeField] private RectTransform continueRt;
        [SerializeField] private CanvasGroup continueGroup;
        [SerializeField] private Button continueButton;
        [SerializeField] private TMP_Text continueText;
        [SerializeField] private RectTransform coinTarget;

        [Header("Look")]
        [SerializeField] private Color dimStar = new Color(0.22f, 0.28f, 0.5f, 0.6f);

        /// <summary>Seconds since the screen opened (scaled time).</summary>
        public float Elapsed { get; private set; }
        public bool IsOpen => gameObject.activeSelf;
        public int Level { get; private set; }
        public int StarsEarned { get; private set; }
        public int Reward { get; private set; }

        private bool finishing;
        private readonly bool[] starSounded = new bool[3];
        private Vector2 ribbonHome, titleHome, heroInHome, rewardHome, claimHome, continueHome;
        private AnimationCurve[] starRot;

        private static readonly Vector4 Pop = new Vector4(0.3f, 1.5f, 0.5f, 1f);
        private static readonly Vector4 EaseOut = new Vector4(0f, 0f, 0.58f, 1f);
        private static AnimationCurve popS, popR, popA, jumpY, jumpSX, jumpSY, shadowS, shadowA, starS, starA,
                                      riseK, pulseS, shineX, swayX;

        private void Awake()
        {
            Instance = this;
            BuildCurves();
            // starIn: rotate(-90deg) -> 60% rotate(10deg) -> rotate(var(--r)) (CSS clockwise; Unity negated).
            starRot = new AnimationCurve[stars.Length];
            for (int i = 0; i < stars.Length; i++)
            {
                starRot[i] = CssCurve.Build(new[] { 0f, 0.6f, 1f }, new[] { 90f, -10f, -starRotations[i] }, Pop);
            }
            ribbonHome = ribbon.anchoredPosition;
            titleHome = title.anchoredPosition;
            heroInHome = heroIn.anchoredPosition;
            rewardHome = rewardPill.anchoredPosition;
            claimHome = claim.anchoredPosition;
            continueHome = continueRt.anchoredPosition;
            claimButton.onClick.AddListener(Claim);
            continueButton.onClick.AddListener(() => Finish(Reward));
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ScreenGrab.Release(blurImage);
        }

        /// <summary>Opens the screen for a won level.</summary>
        public void Show(int level, int starsEarned, int reward, string note = null)
        {
            Level = Mathf.Max(1, level);
            StarsEarned = Mathf.Clamp(starsEarned, 0, 3);
            Reward = Mathf.Max(0, reward);
            levelText.text = "LEVEL " + Level;
            rewardText.text = "+" + Reward;
            claimCoinsText.text = (Reward * 3).ToString();
            continueText.text = "Continue with " + Reward;
            noteText.gameObject.SetActive(!string.IsNullOrEmpty(note));
            noteText.text = note ?? string.Empty;
            for (int i = 0; i < stars.Length; i++)
            {
                stars[i].color = i < StarsEarned ? Color.white : dimStar;
                starSounded[i] = false;
            }
            Elapsed = 0f;
            finishing = false;
            claimButton.interactable = continueButton.interactable = true;
            if (GameManager.Instance != null) GameManager.Instance.SetState(GameState.Won);

            blurImage.enabled = false;
            gameObject.SetActive(true);
            Apply(0f);
            StartCoroutine(ScreenGrab.Blurred(blurImage, GetComponent<Canvas>()));
            Sfx.Play("win_jingle");
        }

        // ------------------------------------------------------------------ buttons

        private void Claim()
        {
            if (finishing) return;
            claimButton.interactable = false;
            Ads.Service.ShowRewarded("level_complete_x3", granted =>
            {
                if (this == null) return;
                if (granted) Finish(Reward * 3);
                else
                {
                    claimButton.interactable = true;
                    Toast.Show("Video not available");
                }
            });
        }

        private void Finish(int coins)
        {
            if (finishing) return;
            finishing = true;
            claimButton.interactable = continueButton.interactable = false;
            SaveSystem.Data.level = Mathf.Max(SaveSystem.Data.level, Level + 1);
            SaveSystem.Commit();
            CoinFly.Play(this, transform, rewardCoin.sprite, rewardCoin.rectTransform.position, coinTarget, coins, 8, () =>
            {
                RoadMapScreen.PendingUnlockLevel = Level;
                SceneLoader.LoadHome();
            });
        }

        // ------------------------------------------------------------------ animation

        private void Update()
        {
            Elapsed += Time.deltaTime;
            Apply(Elapsed);
        }

        private void Apply(float t)
        {
            rays.localRotation = Quaternion.Euler(0f, 0f, -360f * (t / 18f % 1f));

            // Confetti: fall (linear, -40 -> 900 px, 540 deg) + sway (ease-in-out, 18 px), looping after their delays.
            foreach (Confetti c in confetti)
            {
                float f = t < c.delay ? 0f : (t - c.delay) / c.fallDuration % 1f;
                float sw = swayX.Evaluate(t / c.swayDuration % 1f);
                c.piece.anchoredPosition = new Vector2(c.left + sw, 30f - Mathf.Lerp(-40f, 900f, f));
                c.piece.localRotation = Quaternion.Euler(0f, 0f, -540f * f);
            }

            PopIn(ribbon, ribbonHome, Mathf.Clamp01(t / 0.5f));
            PopIn(title, titleHome, Mathf.Clamp01((t - 0.15f) / 0.6f));
            PopIn(heroIn, heroInHome, Mathf.Clamp01((t - 0.35f) / 0.6f));

            // Hero jump (1.1 s from 0.6 s, origin bottom centre) and its shadow.
            float j = t >= 0.6f ? (t - 0.6f) / 1.1f % 1f : 0f;
            hero.anchoredPosition = new Vector2(0f, jumpY.Evaluate(j));
            hero.localScale = new Vector3(jumpSX.Evaluate(j), jumpSY.Evaluate(j), 1f);
            heroShadow.rectTransform.localScale = Vector3.one * shadowS.Evaluate(j);
            SetAlpha(heroShadow, 0.55f * shadowA.Evaluate(j));

            for (int i = 0; i < stars.Length; i++)
            {
                float d = starDelays[i];
                float k = Mathf.Clamp01((t - d) / 0.55f);
                RectTransform s = stars[i].rectTransform;
                s.localScale = Vector3.one * starS.Evaluate(k);
                s.localRotation = Quaternion.Euler(0f, 0f, starRot[i].Evaluate(k));
                SetAlpha(stars[i], (i < StarsEarned ? 1f : dimStar.a) * starA.Evaluate(k));
                bool earned = i < StarsEarned;
                float fk = Mathf.Clamp01((t - d) / 0.6f);
                flashes[i].gameObject.SetActive(earned && t >= d && fk < 1f);
                flashes[i].rectTransform.localScale = Vector3.one * Mathf.Lerp(0.2f, 2.2f, CssCurve.Bezier(EaseOut, fk));
                SetAlpha(flashes[i], 1f - CssCurve.Bezier(EaseOut, fk));
                if (earned && t >= d && !starSounded[i])
                {
                    starSounded[i] = true;
                    Sfx.Play("star_pop");
                    Haptics.Light();
                }
            }

            Rise(rewardPill, rewardGroup, rewardHome, Mathf.Clamp01((t - 1.6f) / 0.45f));
            Rise(claim, claimGroup, claimHome, Mathf.Clamp01((t - 1.9f) / 0.45f));
            Rise(continueRt, continueGroup, continueHome, Mathf.Clamp01((t - 2.6f) / 0.45f));
            claim.localScale = Vector3.one * (t >= 2.4f ? pulseS.Evaluate((t - 2.4f) / 1.2f % 1f) : 1f);
            float sh = t >= 2.4f ? (t - 2.4f) / 2.2f % 1f : 0f;
            claimShine.anchoredPosition = new Vector2(shineX.Evaluate(sh), 0f);
        }

        /// <summary>CSS pop: scale 0 rotate(-12deg) opacity 0 -> 60% scale 1.2 rotate(4deg) -> 1.</summary>
        private static void PopIn(RectTransform rt, Vector2 home, float k)
        {
            rt.anchoredPosition = home;
            rt.localScale = Vector3.one * popS.Evaluate(k);
            rt.localRotation = Quaternion.Euler(0f, 0f, popR.Evaluate(k));
            CanvasGroup g = rt.GetComponent<CanvasGroup>();
            if (g != null) g.alpha = popA.Evaluate(k);
        }

        /// <summary>CSS rise: translateY(30px) opacity 0 -> 0 / 1, ease-out.</summary>
        private static void Rise(RectTransform rt, CanvasGroup g, Vector2 home, float k)
        {
            float e = riseK.Evaluate(k);
            rt.anchoredPosition = home + new Vector2(0f, -30f * (1f - e));
            g.alpha = e;
            g.blocksRaycasts = k >= 1f;
        }

        private static void SetAlpha(Graphic g, float a)
        {
            Color c = g.color;
            c.a = a;
            g.color = c;
        }

        private static void BuildCurves()
        {
            if (popS != null) return;
            float[] pop = { 0f, 0.6f, 1f }, half = { 0f, 0.5f, 1f };
            popS = CssCurve.Build(pop, new[] { 0f, 1.2f, 1f }, Pop);
            popR = CssCurve.Build(pop, new[] { 12f, -4f, 0f }, Pop);
            popA = CssCurve.Build(pop, new[] { 0f, 1f, 1f }, Pop);
            float[] jumpAt = { 0f, 0.1f, 0.45f, 0.8f, 1f };
            jumpY = CssCurve.Build(jumpAt, new[] { 0f, 0f, 34f, 0f, 0f }, CssCurve.EaseInOut);
            jumpSX = CssCurve.Build(jumpAt, new[] { 1f, 1.08f, 0.96f, 1.04f, 1f }, CssCurve.EaseInOut);
            jumpSY = CssCurve.Build(jumpAt, new[] { 1f, 0.92f, 1.05f, 0.96f, 1f }, CssCurve.EaseInOut);
            shadowS = CssCurve.Build(new[] { 0f, 0.45f, 1f }, new[] { 1f, 0.7f, 1f }, CssCurve.EaseInOut);
            shadowA = CssCurve.Build(new[] { 0f, 0.45f, 1f }, new[] { 0.5f / 0.55f, 0.25f / 0.55f, 0.5f / 0.55f }, CssCurve.EaseInOut);
            starS = CssCurve.Build(pop, new[] { 0f, 1.35f, 1f }, Pop);
            starA = CssCurve.Build(pop, new[] { 0f, 1f, 1f }, Pop);
            riseK = CssCurve.Build(new[] { 0f, 1f }, new[] { 0f, 1f }, EaseOut);
            pulseS = CssCurve.Build(half, new[] { 1f, 1.05f, 1f }, CssCurve.EaseInOut);
            shineX = CssCurve.Build(new[] { 0f, 0.6f, 1f }, new[] { -80f, 300f, 300f }, CssCurve.EaseInOut);
            swayX = CssCurve.Build(half, new[] { 0f, 18f, 0f }, CssCurve.EaseInOut);
        }
    }
}
