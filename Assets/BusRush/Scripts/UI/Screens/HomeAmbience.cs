using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>
    /// Home idle animations from Home.dc.html's CSS keyframes (as AnimationCurves, on one scaled-time clock):
    /// airplane fly + bob with a pooled smoke trail, bus idle bounce + exhaust puffs, floating side buttons, pulsing
    /// Level button and the Daily dot.
    /// </summary>
    public sealed class HomeAmbience : MonoBehaviour
    {
        [Header("Airplane")]
        [SerializeField] private RectTransform plane;
        [SerializeField] private RectTransform planeBody;
        [SerializeField] private RectTransform trailRoot;
        [Tooltip("Tail position from the plane's top-left corner (design px, y down).")]
        [SerializeField] private Vector2 tailOffset = new Vector2(6f, 34f);
        [SerializeField] private float puffSpacing = 26f;
        [SerializeField] private float puffSize = 16f;
        [SerializeField] private float puffLife = 2.5f;
        [SerializeField] private int puffPool = 12;

        [Header("Bus")]
        [SerializeField] private RectTransform bus;
        [SerializeField] private Image[] exhaust = new Image[0];

        [Header("Buttons")]
        [SerializeField] private RectTransform[] floaters = new RectTransform[0];
        [SerializeField] private float[] floatDelays = { 0f, 0.5f, 0.25f };
        [SerializeField] private RectTransform pulse;
        [SerializeField] private RectTransform dot;

        public float Elapsed { get; private set; }

        private const float FlyLoop = 10f, FlyTime = 7.5f;
        private Vector2 planeHome, busHome;
        private readonly Vector2[] floatHome = new Vector2[8];
        private readonly List<Puff> puffs = new List<Puff>();
        private float lastSpawnX = float.NaN;
        private int lastLoop = -1;

        private AnimationCurve bobY, bobRot, idleY, idleScaleY, exhaustT, pulseScale, dotScale, floatY;
        private static Sprite softSprite;

        private sealed class Puff
        {
            public Image image;
            public float born = -100f;
        }

        private void Awake()
        {
            planeHome = plane.anchoredPosition;
            busHome = bus.anchoredPosition;
            for (int i = 0; i < floaters.Length && i < floatHome.Length; i++) floatHome[i] = floaters[i].anchoredPosition;

            float[] half = { 0f, 0.5f, 1f };
            bobY = CssCurve.Build(half, new[] { 0f, 5f, 0f }, CssCurve.EaseInOut);         // bobP 1.6 s: translateY(-5px)
            bobRot = CssCurve.Build(half, new[] { 2f, -1f, 2f }, CssCurve.EaseInOut);      // rotate(-2deg) -> 1deg
            idleY = CssCurve.Build(half, new[] { 0f, 4f, 0f }, CssCurve.EaseInOut);        // idle 1.4 s: translateY(-4px)
            idleScaleY = CssCurve.Build(half, new[] { 1f, 1.01f, 1f }, CssCurve.EaseInOut);
            exhaustT = CssCurve.Build(new[] { 0f, 1f }, new[] { 0f, 1f }, new Vector4(0f, 0f, 0.58f, 1f)); // ease-out
            pulseScale = CssCurve.Build(half, new[] { 1f, 1.04f, 1f }, CssCurve.EaseInOut); // breathing pulse 1.2 s, 1.0 -> 1.04
            dotScale = CssCurve.Build(half, new[] { 1f, 1.25f, 1f }, CssCurve.EaseInOut);   // dot 1 s
            floatY = CssCurve.Build(half, new[] { 0f, 4f, 0f }, CssCurve.EaseInOut);        // floaty 2 s

            Sprite soft = SoftCircle();
            for (int i = 0; i < puffPool; i++)
            {
                var go = new GameObject("Puff", typeof(RectTransform));
                go.layer = gameObject.layer;
                var rt = (RectTransform)go.transform;
                rt.SetParent(trailRoot, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(puffSize, puffSize);
                var img = go.AddComponent<Image>();
                img.sprite = soft;
                img.raycastTarget = false;
                img.color = new Color(1f, 1f, 1f, 0f);
                puffs.Add(new Puff { image = img });
            }
            foreach (Image e in exhaust)
            {
                exhaustHome[e.rectTransform] = e.rectTransform.anchoredPosition;
                e.sprite = soft;
                e.color = new Color(235f / 255f, 238f / 255f, 245f / 255f, 0f);
            }
            Apply(0f);
        }

        private void OnEnable() => StartCoroutine(Run());

        private IEnumerator Run()
        {
            while (true)
            {
                Elapsed += Time.deltaTime;
                Apply(Elapsed);
                yield return null;
            }
        }

        private void Apply(float t)
        {
            // fly 10 s linear: 0-75% translate(-150,0) -> (430,-30), then hold.
            int loop = Mathf.FloorToInt(t / FlyLoop);
            float ft = t - loop * FlyLoop;
            float k = Mathf.Clamp01(ft / FlyTime);
            float x = Mathf.Lerp(-150f, 430f, k);
            float y = Mathf.Lerp(0f, 30f, k);
            plane.anchoredPosition = planeHome + new Vector2(x, y);
            float b = t / 1.6f % 1f;
            planeBody.anchoredPosition = new Vector2(planeBody.anchoredPosition.x, bobY.Evaluate(b));
            planeBody.localRotation = Quaternion.Euler(0f, 0f, bobRot.Evaluate(b));

            // Trail: a puff behind the tail every puffSpacing px of travel; each one stays where it spawned.
            if (loop != lastLoop)
            {
                lastLoop = loop;
                lastSpawnX = float.NaN;
            }
            if (k < 1f && (float.IsNaN(lastSpawnX) || x - lastSpawnX >= puffSpacing))
            {
                lastSpawnX = float.IsNaN(lastSpawnX) ? x : lastSpawnX + puffSpacing;
                Vector2 tail = plane.anchoredPosition + new Vector2(tailOffset.x, -tailOffset.y + bobY.Evaluate(b));
                Spawn(tail, t);
            }
            foreach (Puff p in puffs) AnimatePuff(p, t);

            // Bus idle 1.4 s (origin bottom centre) and three exhaust puffs (1.5 s ease-out, delays 0 / .5 / 1).
            float idle = t / 1.4f % 1f;
            bus.anchoredPosition = busHome + new Vector2(0f, idleY.Evaluate(idle));
            bus.localScale = new Vector3(1f, idleScaleY.Evaluate(idle), 1f);
            for (int i = 0; i < exhaust.Length; i++)
            {
                float delay = i * 0.5f;
                RectTransform r = exhaust[i].rectTransform;
                if (t < delay)
                {
                    SetAlpha(exhaust[i], 0f);
                    continue;
                }
                float e = exhaustT.Evaluate((t - delay) / 1.5f % 1f);
                r.anchoredPosition = new Vector2(-46f * e, 34f * e) + ExhaustHome(r);
                r.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.7f, e);
                SetAlpha(exhaust[i], Mathf.Lerp(0.85f, 0f, e));
            }

            // Floating side buttons (2 s, staggered), pulsing Level button (1.2 s), Daily dot (1 s).
            for (int i = 0; i < floaters.Length && i < floatHome.Length; i++)
            {
                float d = i < floatDelays.Length ? floatDelays[i] : 0f;
                float f = t >= d ? (t - d) / 2f % 1f : 0f;
                floaters[i].anchoredPosition = floatHome[i] + new Vector2(0f, floatY.Evaluate(f));
            }
            if (pulse != null) pulse.localScale = Vector3.one * pulseScale.Evaluate(t / 1.2f % 1f);
            if (dot != null) dot.localScale = Vector3.one * dotScale.Evaluate(t % 1f);
        }

        private readonly Dictionary<RectTransform, Vector2> exhaustHome = new Dictionary<RectTransform, Vector2>();

        private Vector2 ExhaustHome(RectTransform r)
        {
            if (!exhaustHome.TryGetValue(r, out Vector2 home))
            {
                home = r.anchoredPosition;
                exhaustHome[r] = home;
            }
            return home;
        }

        private void Spawn(Vector2 at, float t)
        {
            Puff oldest = puffs[0];
            foreach (Puff p in puffs)
            {
                if (p.born < oldest.born) oldest = p;
            }
            oldest.born = t;
            oldest.image.rectTransform.anchoredPosition = at;
        }

        /// <summary>CSS trail: 0% opacity 0 scale .4; 1% (0.1 s) opacity .95 scale .6; then to opacity 0 scale 2.2.</summary>
        private void AnimatePuff(Puff p, float t)
        {
            float age = t - p.born;
            if (age < 0f || age >= puffLife)
            {
                SetAlpha(p.image, 0f);
                return;
            }
            const float inTime = 0.1f;
            float a, s;
            if (age < inTime)
            {
                float u = age / inTime;
                a = Mathf.Lerp(0f, 0.95f, u);
                s = Mathf.Lerp(0.4f, 0.6f, u);
            }
            else
            {
                float u = (age - inTime) / (puffLife - inTime);
                a = Mathf.Lerp(0.95f, 0f, u);
                s = Mathf.Lerp(0.6f, 2.2f, u);
            }
            SetAlpha(p.image, a);
            p.image.rectTransform.localScale = Vector3.one * s;
        }

        private static void SetAlpha(Graphic g, float a)
        {
            Color c = g.color;
            c.a = a;
            g.color = c;
        }

        /// <summary>
        /// Soft white puff (CSS radial-gradient(circle, #FFF 0%, rgba(255,255,255,.85) 45%, transparent 72%) of the
        /// farthest-corner radius), generated once.
        /// </summary>
        public static Sprite SoftCircle()
        {
            if (softSprite != null) return softSprite;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "SoftPuff" };
            var px = new Color32[size * size];
            float half = size * 0.5f, corner = half * 1.41421f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude / corner;
                    float a = d < 0.45f ? Mathf.Lerp(1f, 0.85f, d / 0.45f) : Mathf.Lerp(0.85f, 0f, Mathf.InverseLerp(0.45f, 0.72f, d));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            softSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            softSprite.name = "SoftPuff";
            return softSprite;
        }
    }
}
