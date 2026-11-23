using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>
    /// Level Start card of the Road Map: opens when an unlocked node is tapped (instead of starting the level). It zooms in from
    /// the tapped node (scale 0.2 -> 1.05 -> 1.0 in 0.35 s) with a quick Y flip (scaleX goes from mirrored through 0 to 1) over a
    /// dim backdrop, shows the world header, "Level N", a boss tag on every 10th level, the best stars, the win reward and a big
    /// green Play button. Backdrop / X close it with the reverse animation, Play shrinks it for 0.25 s and then starts the level.
    /// The first time a world's first level is started it shows the world intro variant (art strip + "Welcome to ... World").
    /// Everything is transform / colour driven by the map's clock (Time.deltaTime), nothing allocated per frame.
    /// </summary>
    public sealed class LevelStartCard : MonoBehaviour
    {
        [SerializeField] private Image dim;
        [SerializeField] private Button dimButton;
        [SerializeField] private RectTransform card;
        [SerializeField] private CanvasGroup cardGroup;
        [SerializeField] private RectTransform content;
        [SerializeField] private Image headerImage;
        [SerializeField] private TMP_Text headerText;
        [SerializeField] private Material[] headerMaterials = new Material[3];
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private GameObject bossTag;
        [SerializeField] private TMP_Text bossText;
        [SerializeField] private Image[] stars = new Image[3];
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private GameObject introStrip;
        [SerializeField] private Image introArt;
        [SerializeField] private TMP_Text introText;
        [SerializeField] private Button playButton;
        [SerializeField] private TMP_Text playLabel;
        [SerializeField] private CanvasGroup playGroup;
        [SerializeField] private Button closeButton;
        [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.62f;

        /// <summary>Header colour per world (City, Snow, Beach).</summary>
        public static readonly Color[] WorldColors =
        {
            new Color32(0x2F, 0x86, 0xE8, 0xFF), new Color32(0x39, 0xA9, 0xC9, 0xFF), new Color32(0xF0, 0x8A, 0x24, 0xFF)
        };

        private const float OpenSeconds = 0.35f, CloseSeconds = 0.25f, PlaySeconds = 0.25f;
        private const float IntroExtra = 70f;

        private enum Phase { Closed, Opening, Open, Closing, Playing }

        private Phase phase = Phase.Closed;
        private float t;
        private Vector2 startPos;
        private float baseHeight = -1f;
        private Action onPlay;
        private int shownLevel;
        private bool canPlay;
        private static AnimationCurve scaleIn, ease;

        public bool IsOpen => phase != Phase.Closed;
        public int Level => shownLevel;
        public bool IsIntro { get; private set; }
        /// <summary>Normalised open progress (0..1) while animating, for tests and screenshots.</summary>
        public float OpenProgress => phase == Phase.Opening ? Mathf.Clamp01(t / OpenSeconds) : phase == Phase.Open ? 1f : 0f;

        /// <summary>Raised once the card is fully closed (X, backdrop or after Play's shrink).</summary>
        public event Action Closed;

        private void Awake()
        {
            BuildCurves();
            dimButton.onClick.AddListener(RequestClose);
            closeButton.onClick.AddListener(RequestClose);
            playButton.onClick.AddListener(RequestPlay);
            // NOT gameObject.SetActive(false) here: Awake runs on the first Show()'s SetActive(true), and hiding the card inside it
            // left the phase at "Opening" forever, so IsOpen stayed true and every node tap was ignored. The builder saves it inactive.
        }

        private void OnDisable()
        {
            // Whatever hides the card (map closed, reopened, scene change), it can never stay "open" while it is not on screen.
            phase = Phase.Closed;
            onPlay = null;
        }

        /// <summary>Hides the card at once and resets its state (map reopened / closed / sequence ended).</summary>
        public void HideImmediate()
        {
            phase = Phase.Closed;
            onPlay = null;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Opens the card for <paramref name="level"/>, zooming out of <paramref name="from"/> (the tapped node).
        /// <paramref name="startAction"/> runs 0.25 s after Play (the card shrinks first); null / canPlay=false for levels that cannot be played.
        /// </summary>
        public void Show(RectTransform from, int level, int worldIndex, string worldName, int bestStars, bool boss, int reward,
                         bool intro, bool playable, Sprite worldArt, Action startAction)
        {
            if (baseHeight < 0f) baseHeight = card.sizeDelta.y;
            shownLevel = level;
            IsIntro = intro;
            canPlay = playable;
            onPlay = startAction;

            Color worldColor = WorldColors[worldIndex % WorldColors.Length];
            headerImage.color = worldColor;
            headerText.text = Localizer.T(worldName);
            Material m = headerMaterials != null && headerMaterials.Length > 0 ? headerMaterials[worldIndex % headerMaterials.Length] : null;
            if (m != null) headerText.fontSharedMaterial = m;
            levelText.text = Localizer.F("Level {0}", level);
            bossTag.SetActive(boss);
            if (boss) bossText.text = Localizer.T("BOSS");
            for (int i = 0; i < stars.Length; i++) stars[i].color = i < bestStars ? Color.white : new Color(0.1f, 0.15f, 0.35f, 0.3f);
            rewardText.text = "+" + reward;

            introStrip.SetActive(intro);
            if (intro)
            {
                introArt.sprite = worldArt;
                introText.text = Localizer.F("Welcome to {0} World", Localizer.T(worldName));
            }
            card.sizeDelta = new Vector2(card.sizeDelta.x, baseHeight + (intro ? IntroExtra : 0f));
            content.anchoredPosition = new Vector2(0f, intro ? -IntroExtra : 0f);

            playGroup.alpha = playable ? 1f : 0.5f;
            playLabel.text = Localizer.T("PLAY");

            // The card opens out of the tapped node: card-layer local position of the node's centre.
            var layer = (RectTransform)card.parent;
            startPos = from != null ? (Vector2)layer.InverseTransformPoint(from.position) : Vector2.zero;
            gameObject.SetActive(true);
            phase = Phase.Opening;
            t = 0f;
            Apply(0f, true);
        }

        /// <summary>X, backdrop tap or Back: reverse animation, then hidden. Ignored while Play is starting the level.</summary>
        public void RequestClose()
        {
            if (phase == Phase.Closed || phase == Phase.Closing || phase == Phase.Playing) return;
            phase = Phase.Closing;
            t = 0f;
        }

        public void RequestPlay()
        {
            if (phase != Phase.Open && phase != Phase.Opening) return;
            if (!canPlay)
            {
                Toast.Show(Localizer.T("Already completed"));
                return;
            }
            phase = Phase.Playing;
            t = 0f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            switch (phase)
            {
                case Phase.Opening:
                    t += dt;
                    Apply(Mathf.Clamp01(t / OpenSeconds), true);
                    if (t >= OpenSeconds) phase = Phase.Open;
                    break;
                case Phase.Closing:
                    t += dt;
                    Apply(1f - Mathf.Clamp01(t / CloseSeconds), false);
                    if (t >= CloseSeconds) Finish();
                    break;
                case Phase.Playing:
                    t += dt;
                    Apply(1f - Mathf.Clamp01(t / PlaySeconds), false);
                    if (t >= PlaySeconds)
                    {
                        Action go = onPlay;
                        Finish();
                        go?.Invoke();
                    }
                    break;
            }
        }

        private void Finish()
        {
            phase = Phase.Closed;
            gameObject.SetActive(false);
            Closed?.Invoke();
        }

        /// <summary>k: 0 = a small mirrored card at the node, 1 = the card centred. flip: the Y-flip (scaleX) while opening.</summary>
        private void Apply(float k, bool flip)
        {
            float s = scaleIn.Evaluate(k);
            float e = ease.Evaluate(k);
            float sx = flip ? Mathf.Cos(Mathf.Lerp(100f, 0f, e) * Mathf.Deg2Rad) : 1f; // scaleX trick: mirrored -> edge-on -> front
            card.localScale = new Vector3(s * sx, s, 1f);
            card.anchoredPosition = Vector2.Lerp(startPos, Vector2.zero, e);
            cardGroup.alpha = Mathf.Clamp01(k * 4f);
            Color c = dim.color;
            c.a = dimAlpha * Mathf.Clamp01(k * 2f);
            dim.color = c;
        }

        private static void BuildCurves()
        {
            if (scaleIn != null) return;
            Vector4 easeOut = new Vector4(0f, 0f, 0.58f, 1f);
            scaleIn = CssCurve.Build(new[] { 0f, 0.7f, 1f }, new[] { 0.2f, 1.05f, 1f }, easeOut);
            ease = CssCurve.Build(new[] { 0f, 1f }, new[] { 0f, 1f }, easeOut);
        }
    }
}
