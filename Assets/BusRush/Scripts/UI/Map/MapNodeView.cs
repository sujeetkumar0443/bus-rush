using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    public enum MapNodeState
    {
        Completed,
        Current,
        Locked
    }

    /// <summary>
    /// One level disc on the Road Map (RoadMap.dc.html): gold + number + stars when completed, green with light rays,
    /// glow, hop and a PLAY bubble when current, grey when locked; every 10th level uses the boss disc with a tag.
    /// <see cref="Tick"/> plays the loops and, during an unlock, the grey shake/burst -> green pop timeline.
    /// </summary>
    public sealed class MapNodeView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [Header("Completed")]
        [SerializeField] private GameObject gold;
        [SerializeField] private TMP_Text goldNumber;
        [SerializeField] private GameObject starsRow;
        [SerializeField] private Image[] stars = new Image[3];
        [Header("Current")]
        [SerializeField] private RectTransform glow;
        [SerializeField] private RectTransform spin;
        [SerializeField] private Image glowSoft;
        [SerializeField] private RectTransform hop;
        [SerializeField] private RectTransform green;
        [SerializeField] private CanvasGroup greenGroup;
        [SerializeField] private TMP_Text greenNumber;
        [SerializeField] private RectTransform play;
        [SerializeField] private CanvasGroup playGroup;
        [SerializeField] private RectTransform playHop;
        [SerializeField] private Button playButton;
        [SerializeField] private Image[] twinkles = new Image[3];
        [SerializeField] private Image[] rays = new Image[9];
        [Header("Locked / boss")]
        [SerializeField] private RectTransform grey;
        [SerializeField] private Image greyImage;
        [SerializeField] private GameObject boss;
        [SerializeField] private TMP_Text bossTag;

        public int Level { get; private set; }
        public MapNodeState State { get; private set; }
        public int Stars { get; private set; }

        /// <summary>Raised with this node when the disc or its PLAY bubble is tapped.</summary>
        public event Action<MapNodeView> Tapped;

        private bool isBoss;
        private static readonly Color StarOff = new Color(0.1f, 0.15f, 0.35f, 0.35f);
        private static readonly float[] TwinkleDelays = { 3.4f, 3.9f, 4.3f };
        private static readonly float[] RayAngles = { 0f, 40f, 80f, 120f, 160f, 200f, 240f, 280f, 320f };

        private static AnimationCurve shake, burstS, burstA, greenS, greenA, glowS, glowA, rayEase, popS, popA, pulseS, hopY,
                                      glowRS, glowRA, twkS, twkR, twkA;
        private static readonly Vector4 Pop = new Vector4(0.3f, 1.5f, 0.5f, 1f);

        private void Awake()
        {
            button.onClick.AddListener(() => Tapped?.Invoke(this));
            if (playButton != null) playButton.onClick.AddListener(() => Tapped?.Invoke(this));
            BuildCurves();
        }

        public void Setup(int level, MapNodeState state, int starCount, bool bossLevel)
        {
            Level = level;
            State = state;
            Stars = starCount;
            isBoss = bossLevel;
            goldNumber.text = greenNumber.text = level.ToString();
            bossTag.text = level.ToString();
            for (int i = 0; i < stars.Length; i++) stars[i].color = i < starCount ? Color.white : StarOff;
            Tick(0f, -1f);
        }

        /// <summary>
        /// t = clock for the loops; unlock = seconds into the unlock timeline (design times) or negative for the
        /// settled state. During an unlock the node is grey until the burst at 3.1 s.
        /// </summary>
        public void Tick(float t, float unlock)
        {
            bool unlocking = unlock >= 0f && State == MapNodeState.Current;
            float u = unlocking ? unlock : 100f; // settled = long after every intro

            bool current = State == MapNodeState.Current;
            bool done = State == MapNodeState.Completed;
            gold.SetActive(done && !isBoss);
            starsRow.SetActive(done);
            boss.SetActive(isBoss && (done || State == MapNodeState.Locked || u >= 3.1f));
            if (boss.activeSelf && boss.transform.parent != (current ? hop : transform))
            {
                boss.transform.SetParent(current ? hop : transform, false);
            }

            // Grey disc: plain when locked; during an unlock shakes at 2.6 s, bursts at 3.1 s.
            bool showGrey = (State == MapNodeState.Locked && !isBoss) || (unlocking && u < 3.45f);
            grey.gameObject.SetActive(showGrey);
            if (showGrey)
            {
                float sk = Mathf.Clamp01((u - 2.6f) / 0.5f);
                grey.localRotation = Quaternion.Euler(0f, 0f, unlocking ? shake.Evaluate(sk) : 0f);
                float bk = Mathf.Clamp01((u - 3.1f) / 0.35f);
                grey.localScale = Vector3.one * (unlocking ? burstS.Evaluate(bk) : 1f);
                SetAlpha(greyImage, unlocking ? burstA.Evaluate(bk) : 1f);
            }

            // Current: glow + rays, green disc (greenIn at 3.1), hop (3.7+), PLAY (popIn 3.6, pulse 4.2+).
            glow.gameObject.SetActive(current);
            hop.gameObject.SetActive(current);
            play.gameObject.SetActive(current);
            foreach (Image tw in twinkles) tw.gameObject.SetActive(current);
            foreach (Image r in rays) r.gameObject.SetActive(unlocking && u >= 3.1f && u < 3.9f);
            if (!current) return;

            float gi = Mathf.Clamp01((u - 3.1f) / 0.5f);
            green.gameObject.SetActive(!isBoss);
            green.localScale = Vector3.one * greenS.Evaluate(gi);
            greenGroup.alpha = greenA.Evaluate(gi);
            float gc = Mathf.Clamp01((u - 3.1f) / 0.4f);
            glow.localScale = Vector3.one * glowS.Evaluate(gc);
            float glowAlpha = glowA.Evaluate(gc);
            spin.localRotation = Quaternion.Euler(0f, 0f, -360f * (t / 6f % 1f));
            float gr = t / 1.6f % 1f;
            glowSoft.rectTransform.localScale = Vector3.one * glowRS.Evaluate(gr);
            SetAlpha(glowSoft, glowRA.Evaluate(gr) * glowAlpha);
            SetAlpha(spin.GetComponent<Graphic>(), glowAlpha);

            float h = u >= 3.7f ? (u - 3.7f + t) % 1f : 0f;
            hop.anchoredPosition = new Vector2(0f, hopY.Evaluate(h));
            playHop.anchoredPosition = new Vector2(0f, hopY.Evaluate(h));

            float pk = Mathf.Clamp01((u - 3.6f) / 0.45f);
            float pulse = u >= 4.2f ? pulseS.Evaluate((u - 4.2f + t) / 1.2f % 1f) : 1f;
            play.localScale = Vector3.one * (popS.Evaluate(pk) * pulse);
            playGroup.alpha = popA.Evaluate(pk);
            playGroup.blocksRaycasts = pk >= 1f;

            for (int i = 0; i < twinkles.Length; i++)
            {
                float d = TwinkleDelays[i];
                bool hidden = unlocking && u < d;
                float k = hidden ? 0f : (unlocking ? (u - d) / 1.8f : (t + i * 0.6f) / 1.8f) % 1f;
                RectTransform r = twinkles[i].rectTransform;
                r.localScale = Vector3.one * twkS.Evaluate(k);
                r.localRotation = Quaternion.Euler(0f, 0f, twkR.Evaluate(k));
                SetAlpha(twinkles[i], hidden ? 0f : twkA.Evaluate(k));
            }

            // Star burst rays: 9 stars fly out 78 px at --a, 0.8 s ease-out from 3.1 s.
            float rk = Mathf.Clamp01((u - 3.1f) / 0.8f);
            for (int i = 0; i < rays.Length; i++)
            {
                if (!rays[i].gameObject.activeSelf) continue;
                float e = rayEase.Evaluate(rk);
                float a = -RayAngles[i] * Mathf.Deg2Rad; // CSS rotate is clockwise
                Vector2 dir = new Vector2(-Mathf.Sin(a), Mathf.Cos(a)); // CSS translateY(-78) after rotate(a)
                RectTransform r = rays[i].rectTransform;
                r.anchoredPosition = rayHome + dir * (78f * e);
                r.localRotation = Quaternion.Euler(0f, 0f, -RayAngles[i]);
                r.localScale = Vector3.one * Mathf.Lerp(0.2f, 1f, e);
                SetAlpha(rays[i], rk < 0.2f ? rk / 0.2f : Mathf.Lerp(1f, 0f, (rk - 0.2f) / 0.8f));
            }
        }

        private Vector2 rayHome;
        private bool rayHomeSet;

        private void OnEnable()
        {
            if (!rayHomeSet && rays.Length > 0 && rays[0] != null)
            {
                rayHome = rays[0].rectTransform.anchoredPosition;
                rayHomeSet = true;
            }
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
            if (shake != null) return;
            float[] ends = { 0f, 1f }, half = { 0f, 0.5f, 1f };
            // shake: 20% -9deg, 40% 8, 60% -6, 80% 5 (CSS clockwise -> Unity negated), ease-in-out
            shake = CssCurve.Build(new[] { 0f, 0.2f, 0.4f, 0.6f, 0.8f, 1f }, new[] { 0f, 9f, -8f, 6f, -5f, 0f }, CssCurve.EaseInOut);
            Vector4 easeOut = new Vector4(0f, 0f, 0.58f, 1f);
            burstS = CssCurve.Build(ends, new[] { 1f, 1.6f }, easeOut);
            burstA = CssCurve.Build(ends, new[] { 1f, 0f }, easeOut);
            greenS = CssCurve.Build(new[] { 0f, 0.6f, 1f }, new[] { 0.4f, 1.18f, 1f }, Pop);
            greenA = CssCurve.Build(new[] { 0f, 0.6f, 1f }, new[] { 0f, 1f, 1f }, Pop);
            glowS = CssCurve.Build(new[] { 0f, 0.6f, 1f }, new[] { 0.4f, 1.18f, 1f }, easeOut);
            glowA = CssCurve.Build(new[] { 0f, 0.6f, 1f }, new[] { 0f, 1f, 1f }, easeOut);
            rayEase = CssCurve.Build(ends, new[] { 0f, 1f }, easeOut);
            popS = CssCurve.Build(new[] { 0f, 0.7f, 1f }, new[] { 0.3f, 1.08f, 1f }, Pop);
            popA = CssCurve.Build(new[] { 0f, 0.7f, 1f }, new[] { 0f, 1f, 1f }, Pop);
            pulseS = CssCurve.Build(half, new[] { 1f, 1.05f, 1f }, CssCurve.EaseInOut);
            hopY = CssCurve.Build(half, new[] { 0f, 6f, 0f }, CssCurve.EaseInOut);
            glowRS = CssCurve.Build(half, new[] { 1f, 1.25f, 1f }, CssCurve.EaseInOut);
            glowRA = CssCurve.Build(half, new[] { 0.85f, 0.45f, 0.85f }, CssCurve.EaseInOut);
            twkS = CssCurve.Build(half, new[] { 0f, 1f, 0f }, CssCurve.EaseInOut);
            twkR = CssCurve.Build(half, new[] { 0f, -90f, 0f }, CssCurve.EaseInOut);
            twkA = CssCurve.Build(half, new[] { 0f, 1f, 0f }, CssCurve.EaseInOut);
        }
    }
}
