using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>
    /// Level Failed / Out of Space (UI v2, Docs/UI_v2_Design/LevelFailed.dc.html): full-screen overlay in the Game
    /// scene over the blurred game. "OUT OF SPACE!" pops in and shakes, the sad hero sways with falling tears, and the
    /// player gets "Extra bay" (rewarded video, once per level, only while a locked bay is left), Retry or Home.
    /// </summary>
    public sealed class LevelFailedScreen : MonoBehaviour
    {
        public static LevelFailedScreen Instance { get; private set; }

        /// <summary>The screen in the loaded Game scene (it starts inactive).</summary>
        public static LevelFailedScreen Find() =>
            Instance != null ? Instance : FindAnyObjectByType<LevelFailedScreen>(FindObjectsInactive.Include);

        [SerializeField] private RawImage blur;
        [SerializeField] private Image overlay;
        [Tooltip("Design: rgba(40,12,40,.70). UI blends in linear colour space here, which reads lighter than CSS, so a higher alpha gives the design's look.")]
        [SerializeField, Range(0f, 1f)] private float overlayAlpha = 0.86f;
        [SerializeField] private RectTransform title;
        [SerializeField] private CanvasGroup titleGroup;
        [SerializeField] private RectTransform subtitle;
        [SerializeField] private CanvasGroup subtitleGroup;
        [SerializeField] private RectTransform hero;
        [SerializeField] private Image[] tears = new Image[2];
        [SerializeField] private RectTransform infoBox;
        [SerializeField] private CanvasGroup infoGroup;
        [SerializeField] private RectTransform extraRoot;
        [SerializeField] private Button extraButton;
        [SerializeField] private RectTransform extraShine;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button homeButton;

        public float Elapsed { get; private set; }
        public bool IsOpen => gameObject.activeSelf;
        public bool OffersExtraBay { get; private set; }

        private Vector2 titleHome, subtitleHome, infoHome;
        private Vector2[] tearHomes;
        private bool busy;
        private static readonly Vector4 Pop = new Vector4(0.3f, 1.5f, 0.5f, 1f);
        private static AnimationCurve popS, popA, shakeX, swayR, tearY, rise, pulseS, shineX;

        private void Awake()
        {
            Instance = this;
            titleHome = title.anchoredPosition;
            subtitleHome = subtitle.anchoredPosition;
            infoHome = infoBox.anchoredPosition;
            tearHomes = new Vector2[tears.Length];
            for (int i = 0; i < tears.Length; i++) tearHomes[i] = tears[i].rectTransform.anchoredPosition;
            extraButton.onClick.AddListener(ExtraBay);
            retryButton.onClick.AddListener(() =>
            {
                Hide();
                SceneLoader.LoadGame();
            });
            homeButton.onClick.AddListener(() =>
            {
                if (GameManager.Instance != null) GameManager.Instance.SetState(GameState.Lost);
                Hide();
                SceneLoader.LoadHome();
            });
            BuildCurves();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ScreenGrab.Release(blur);
        }

        /// <summary>Opens the screen; offerExtraBay shows the info box and the "Extra bay" button.</summary>
        public void Show(bool offerExtraBay)
        {
            OffersExtraBay = offerExtraBay;
            Elapsed = 0f;
            busy = false;
            gameObject.SetActive(true);
            infoBox.gameObject.SetActive(offerExtraBay);
            extraRoot.gameObject.SetActive(offerExtraBay);
            Apply(0f);
            StartCoroutine(ScreenGrab.Blurred(blur, GetComponent<Canvas>()));
            Sfx.Play("level_failed");
            Haptics.Medium();
        }

        public void Hide()
        {
            ScreenGrab.Release(blur);
            gameObject.SetActive(false);
        }

        private void ExtraBay()
        {
            if (busy) return;
            busy = true;
            Ads.Service.ShowRewarded("level_failed_extra_bay", granted =>
            {
                busy = false;
                if (this == null) return;
                if (!granted)
                {
                    Toast.Show(Localizer.T("Video not available"));
                    return;
                }
                Gameplay.GameSession session = Gameplay.GameSession.Current;
                if (session != null && session.UseExtraBay()) Hide(); // play continues with the new bay
                else
                {
                    infoBox.gameObject.SetActive(false);
                    extraRoot.gameObject.SetActive(false);
                }
            });
        }

        private void Update()
        {
            Elapsed += Time.deltaTime;
            Apply(Elapsed);
        }

        private void Apply(float t)
        {
            SetAlpha(overlay, overlayAlpha * Mathf.Clamp01(t / 0.25f));

            // Title: popIn .45s, then shakeT .5s from .5s.
            float p = Mathf.Clamp01(t / 0.45f);
            title.localScale = Vector3.one * popS.Evaluate(p);
            titleGroup.alpha = popA.Evaluate(p);
            title.anchoredPosition = titleHome + new Vector2(shakeX.Evaluate(Mathf.Clamp01((t - 0.5f) / 0.5f)), 0f);

            Rise(subtitle, subtitleGroup, subtitleHome, Mathf.Clamp01((t - 0.2f) / 0.45f));
            if (infoBox.gameObject.activeSelf) Rise(infoBox, infoGroup, infoHome, Mathf.Clamp01((t - 0.5f) / 0.45f));

            hero.localRotation = Quaternion.Euler(0f, 0f, swayR.Evaluate(t / 2.4f % 1f));
            for (int i = 0; i < tears.Length; i++)
            {
                float k = Mathf.Repeat((t - i * 0.7f) / 1.4f, 1f);
                bool started = t >= i * 0.7f;
                tears[i].rectTransform.anchoredPosition = tearHomes[i] - new Vector2(0f, tearY.Evaluate(k));
                SetAlpha(tears[i], started ? (k < 0.2f ? k / 0.2f : Mathf.Lerp(1f, 0f, (k - 0.2f) / 0.8f)) : 0f);
            }

            extraRoot.localScale = Vector3.one * pulseS.Evaluate(t / 1.2f % 1f);
            float sh = t >= 1f ? (t - 1f) / 2.4f % 1f : 0f;
            extraShine.anchoredPosition = new Vector2(shineX.Evaluate(sh), 0f);
        }

        private void Rise(RectTransform rt, CanvasGroup g, Vector2 home, float k)
        {
            float e = rise.Evaluate(k);
            rt.anchoredPosition = home - new Vector2(0f, 24f * (1f - e));
            g.alpha = e;
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
            float[] half = { 0f, 0.5f, 1f };
            popS = CssCurve.Build(new[] { 0f, 0.7f, 1f }, new[] { 0.5f, 1.05f, 1f }, Pop);
            popA = CssCurve.Build(new[] { 0f, 0.7f, 1f }, new[] { 0f, 1f, 1f }, Pop);
            shakeX = CssCurve.Build(new[] { 0f, 0.2f, 0.4f, 0.6f, 0.8f, 1f }, new[] { 0f, -6f, 6f, -4f, 4f, 0f }, CssCurve.EaseInOut);
            swayR = CssCurve.Build(half, new[] { 3f, -3f, 3f }, CssCurve.EaseInOut); // CSS rotate(-3deg) <-> 3deg
            tearY = CssCurve.Build(new[] { 0f, 1f }, new[] { 0f, 40f }, new Vector4(0.42f, 0f, 1f, 1f)); // ease-in
            rise = CssCurve.Build(new[] { 0f, 1f }, new[] { 0f, 1f }, new Vector4(0f, 0f, 0.58f, 1f));
            pulseS = CssCurve.Build(half, new[] { 1f, 1.05f, 1f }, CssCurve.EaseInOut);
            shineX = CssCurve.Build(new[] { 0f, 0.6f, 1f }, new[] { -80f, 320f, 320f }, CssCurve.EaseInOut);
        }
    }
}
