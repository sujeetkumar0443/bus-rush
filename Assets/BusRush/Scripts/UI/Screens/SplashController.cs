using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>
    /// Boot splash (Docs/UI_v2_Design/Splash.dc.html + Art/UI_v2/Splash/LAYOUT.txt). Every animation is the design's
    /// CSS @keyframes turned into AnimationCurves (<see cref="CssCurve"/>) and played by coroutines on one clock.
    /// <see cref="Play"/> preloads Home (allowSceneActivation false), drives the loading bar with the real progress,
    /// keeps the splash up for at least <see cref="minDuration"/> and then fades to Home.
    /// </summary>
    public sealed class SplashController : MonoBehaviour
    {
        [Header("Layers")]
        [SerializeField] private RawImage rays;
        [SerializeField] private RectTransform drive;
        [SerializeField] private CanvasGroup driveGroup;
        [SerializeField] private RectTransform bus;
        [SerializeField] private RectTransform shadow;
        [SerializeField] private Image shadowImage;
        [SerializeField] private RectTransform logoIn;
        [SerializeField] private CanvasGroup logoGroup;
        [SerializeField] private RectTransform logo;
        [SerializeField] private Image[] sparkles = new Image[0];
        [SerializeField] private float[] sparkleDelays = { 1.8f, 2.4f, 3f, 3.5f };

        [Header("Loading bar")]
        [SerializeField] private RectTransform fillArea;
        [SerializeField] private RectTransform fill;
        [SerializeField] private RectTransform shine;
        [SerializeField] private TMP_Text loadingText;
        [Tooltip("Smallest fill width (the rounded ends), UI units.")]
        [SerializeField] private float minFillWidth = 20f;

        [Header("Timing")]
        [SerializeField, Min(0f)] private float minDuration = 2.6f;
        [SerializeField, Min(0f)] private float fadeDuration = 0.3f;
        [Tooltip("The bar can show at most eased(elapsed / this), so it fills across the minimum time (full just before the switch).")]
        [SerializeField, Min(0.1f)] private float fillDuration = 2.4f;

        /// <summary>Seconds since the splash started (scaled time, so a screenshot tool can freeze it).</summary>
        public float Elapsed { get; private set; }

        private float shownProgress;
        private Texture2D raysTexture;

        // CSS: driveIn 1.2s cubic-bezier(.25,.8,.35,1.08) .2s both
        private static readonly Vector4 DriveEase = new Vector4(0.25f, 0.8f, 0.35f, 1.08f);
        // CSS: pop .9s cubic-bezier(.3,1.5,.5,1) 1.1s both
        private static readonly Vector4 PopEase = new Vector4(0.3f, 1.5f, 0.5f, 1f);

        private AnimationCurve driveX, driveY, driveScale, driveAlpha;
        private AnimationCurve bounceY, bounceRot, shadowScale, shadowAlpha;
        private AnimationCurve popScale, popY, popAlpha, breatheScale, breatheRot;
        private AnimationCurve twinkleScale, twinkleRot, twinkleAlpha, shineX, blinkAlpha;

        private void Awake()
        {
            BuildCurves();
            raysTexture = MakeRaysTexture(512);
            rays.texture = raysTexture;
            ApplyAll(0f);
            SetFill(0f);
        }

        private void OnDestroy()
        {
            if (raysTexture != null) Destroy(raysTexture);
        }

        private void Update() => Elapsed += Time.deltaTime;

        private void OnEnable()
        {
            StartCoroutine(Animate());
        }

        /// <summary>Preloads Home, fills the bar, waits the minimum time, then fades to Home (runs on past this object).</summary>
        public IEnumerator Play()
        {
            AsyncOperation op = SceneManager.LoadSceneAsync(SceneLoader.HomeScene, LoadSceneMode.Single);
            if (op == null)
            {
                Debug.LogError("[BusRush] Home scene could not be loaded. Is it in Build Settings?");
                yield break;
            }
            op.allowSceneActivation = false;

            while (true)
            {
                float real = Mathf.Clamp01(op.progress / 0.9f); // stops at 0.9 until activation
                float timeCap = CssCurve.Bezier(CssCurve.EaseInOut, Elapsed / fillDuration);
                shownProgress = Mathf.Max(shownProgress, Mathf.Min(real, timeCap)); // never goes back
                SetFill(shownProgress);
                if (Elapsed >= minDuration && real >= 1f && shownProgress >= 1f) break;
                yield return null;
            }
            SceneLoader.ActivatePreloaded(op, SceneLoader.HomeScene, fadeDuration);
        }

        // ------------------------------------------------------------------ animation

        private IEnumerator Animate()
        {
            while (true)
            {
                ApplyAll(Elapsed);
                yield return null;
            }
        }

        private void ApplyAll(float t)
        {
            // Sun rays: one turn per 70 s (CSS clockwise = negative z).
            rays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -360f * (t / 70f % 1f));

            // Bus drives in (fill-mode both).
            float d = Mathf.Clamp01((t - 0.2f) / 1.2f);
            drive.anchoredPosition = driveHome + new Vector2(driveX.Evaluate(d), driveY.Evaluate(d));
            drive.localScale = Vector3.one * driveScale.Evaluate(d);
            driveGroup.alpha = driveAlpha.Evaluate(d);

            // Bounce + shadow, 0.9 s loop from 1.4 s.
            float b = t >= 1.4f ? (t - 1.4f) / 0.9f % 1f : 0f;
            bus.anchoredPosition = busHome + new Vector2(0f, bounceY.Evaluate(b));
            bus.localRotation = Quaternion.Euler(0f, 0f, bounceRot.Evaluate(b));
            shadow.localScale = Vector3.one * shadowScale.Evaluate(b);
            SetAlpha(shadowImage, shadowAlpha.Evaluate(b));

            // Logo pops (both), then breathes from 2.1 s (3 s loop).
            float p = Mathf.Clamp01((t - 1.1f) / 0.9f);
            float s = popScale.Evaluate(p);
            logoIn.localScale = Vector3.one * s;
            logoIn.anchoredPosition = logoHome + new Vector2(0f, -s * popY.Evaluate(p)); // scale() translateY(): offset is scaled
            logoGroup.alpha = popAlpha.Evaluate(p);
            float br = t >= 2.1f ? (t - 2.1f) / 3f % 1f : 0f;
            logo.localScale = Vector3.one * breatheScale.Evaluate(br);
            logo.localRotation = Quaternion.Euler(0f, 0f, breatheRot.Evaluate(br));

            // Sparkles: 2.2 s twinkle loops, staggered; hidden until their first start.
            for (int i = 0; i < sparkles.Length; i++)
            {
                float delay = i < sparkleDelays.Length ? sparkleDelays[i] : 0f;
                float k = t >= delay ? (t - delay) / 2.2f % 1f : 0f;
                RectTransform r = sparkles[i].rectTransform;
                r.localScale = Vector3.one * twinkleScale.Evaluate(k);
                r.localRotation = Quaternion.Euler(0f, 0f, twinkleRot.Evaluate(k));
                SetAlpha(sparkles[i], twinkleAlpha.Evaluate(k));
            }

            // Loading bar shine (1.6 s, linear) and "Loading..." blink (1.4 s).
            shine.anchoredPosition = new Vector2(shineX.Evaluate(t / 1.6f % 1f), shine.anchoredPosition.y);
            loadingText.alpha = blinkAlpha.Evaluate(t / 1.4f % 1f);
        }

        private Vector2 driveHome, busHome, logoHome;
        private bool homesCaptured;

        private void BuildCurves()
        {
            if (!homesCaptured)
            {
                driveHome = drive.anchoredPosition;
                busHome = bus.anchoredPosition;
                logoHome = logoIn.anchoredPosition;
                homesCaptured = true;
            }

            float[] ends = { 0f, 1f };
            // driveIn: translate(-8px,-175px) scale(.28) opacity 0 -> 12% opacity 1 -> identity. (CSS y down = Unity -y.)
            driveX = CssCurve.Build(ends, new[] { -8f, 0f }, DriveEase);
            driveY = CssCurve.Build(ends, new[] { 175f, 0f }, DriveEase);
            driveScale = CssCurve.Build(ends, new[] { 0.28f, 1f }, DriveEase);
            driveAlpha = CssCurve.Build(new[] { 0f, 0.12f, 1f }, new[] { 0f, 1f, 1f }, DriveEase);

            // bounce: 0/100% 0,0deg; 25% -10px,-1.5deg; 55% -3px,0.8deg (ease-in-out).
            float[] bounceAt = { 0f, 0.25f, 0.55f, 1f };
            bounceY = CssCurve.Build(bounceAt, new[] { 0f, 10f, 3f, 0f }, CssCurve.EaseInOut);
            bounceRot = CssCurve.Build(bounceAt, new[] { 0f, 1.5f, -0.8f, 0f }, CssCurve.EaseInOut);
            // shadow: 0/100% scale 1 opacity .45; 25% scale .86 opacity .3.
            float[] shadowAt = { 0f, 0.25f, 1f };
            shadowScale = CssCurve.Build(shadowAt, new[] { 1f, 0.86f, 1f }, CssCurve.EaseInOut);
            shadowAlpha = CssCurve.Build(shadowAt, new[] { 0.45f, 0.3f, 0.45f }, CssCurve.EaseInOut);

            // pop: 0% scale(.2) translateY(40px) opacity 0; 60% scale(1.08) translateY(0) opacity 1; 80% scale(.97); 100% scale(1).
            float[] popAt = { 0f, 0.6f, 0.8f, 1f };
            popScale = CssCurve.Build(popAt, new[] { 0.2f, 1.08f, 0.97f, 1f }, PopEase);
            popY = CssCurve.Build(popAt, new[] { 40f, 0f, 0f, 0f }, PopEase);
            popAlpha = CssCurve.Build(new[] { 0f, 0.6f, 1f }, new[] { 0f, 1f, 1f }, PopEase);
            // breathe: 50% scale(1.035) rotate(-1deg).
            float[] halfAt = { 0f, 0.5f, 1f };
            breatheScale = CssCurve.Build(halfAt, new[] { 1f, 1.035f, 1f }, CssCurve.EaseInOut);
            breatheRot = CssCurve.Build(halfAt, new[] { 0f, 1f, 0f }, CssCurve.EaseInOut);

            // twinkle: 0/100% scale 0 rot 0 opacity 0; 50% scale 1 rot 90deg opacity 1.
            twinkleScale = CssCurve.Build(halfAt, new[] { 0f, 1f, 0f }, CssCurve.EaseInOut);
            twinkleRot = CssCurve.Build(halfAt, new[] { 0f, -90f, 0f }, CssCurve.EaseInOut);
            twinkleAlpha = CssCurve.Build(halfAt, new[] { 0f, 1f, 0f }, CssCurve.EaseInOut);

            // shine: translateX(-120px) -> 340px, linear. blink: 50% opacity .4.
            shineX = CssCurve.Build(ends, new[] { -120f, 340f }, CssCurve.Linear, 2);
            blinkAlpha = CssCurve.Build(halfAt, new[] { 1f, 0.4f, 1f }, CssCurve.EaseInOut);
        }

        private void SetFill(float progress)
        {
            float w = Mathf.Max(minFillWidth, progress * fillArea.rect.width);
            fill.sizeDelta = new Vector2(w, fill.sizeDelta.y);
        }

        private static void SetAlpha(Graphic g, float a)
        {
            Color c = g.color;
            c.a = a;
            g.color = c;
        }

        // ------------------------------------------------------------------ procedural rays

        /// <summary>
        /// CSS: 1000x1000 circle, repeating-conic-gradient(rgba(255,255,240,.16) 0-6deg, clear 6-18deg), masked by
        /// radial-gradient(circle, opaque 0-10%, transparent at 50%) of the farthest-corner radius (707 px).
        /// </summary>
        private static Texture2D MakeRaysTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "SplashRays" };
            var px = new Color32[size * size];
            float pxToCss = 1000f / size;
            const float farCorner = 707.1f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) * pxToCss - 500f;
                    float dy = (y + 0.5f) * pxToCss - 500f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float mask = 1f - Mathf.InverseLerp(0.1f * farCorner, 0.5f * farCorner, r);
                    float a = 0f;
                    if (mask > 0f)
                    {
                        float deg = (Mathf.Atan2(dx, dy) * Mathf.Rad2Deg + 360f) % 18f;
                        float soft = Mathf.Clamp(Mathf.Rad2Deg * pxToCss / Mathf.Max(r, 1f), 0.05f, 3f);
                        float wedge = Mathf.Clamp01((deg + soft * 0.5f) / soft) * Mathf.Clamp01((6f - deg + soft * 0.5f) / soft);
                        a = 0.16f * wedge * mask;
                    }
                    px[y * size + x] = new Color32(255, 255, 240, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }
    }
}
