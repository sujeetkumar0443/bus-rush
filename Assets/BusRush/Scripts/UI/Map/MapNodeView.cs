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

    /// <summary>Times (seconds) of the level-complete sequence on the map; <see cref="RoadMapScreen"/> drives it, nodes follow.</summary>
    public static class MapSequence
    {
        public const float Scroll = 0.45f;       // camera scrolls to the just-completed node
        public const float PopStart = 0.45f;     // node pops with a star burst and a white flash
        public const float PopEnd = 0.95f;
        public const float PathStart = 0.95f;    // grey dashes fill gold, a glowing dot travels the road
        public const float PathEnd = 1.75f;      // 0.8 s
        public const float ShakeStart = 1.75f;   // next node's lock shakes ...
        public const float CrackAt = 1.95f;      // ... cracks, the halves fly off and fade ...
        public const float OpenStart = 2.0f;     // ... and the node pops open to gold with a ring
        public const float End = 2.3f;           // then it becomes the current node with the Play marker
    }

    /// <summary>
    /// One level disc on the Road Map: gold + number + stars when completed, grey with a small lock badge when locked,
    /// a big red Play disc (125 %) with a white triangle, a soft pulsing ring and a small bounce every 2 s when current.
    /// Every 10th level uses the boss disc. <see cref="Tick"/> plays the loops; <see cref="SeqCompleted"/> /
    /// <see cref="SeqNext"/> follow the level-complete sequence (transform / colour changes only, nothing allocated).
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
        [SerializeField] private Image ring;
        [SerializeField] private RectTransform hop;
        [SerializeField] private GameObject red;
        [SerializeField] private RectTransform playIcon;
        [SerializeField] private Image[] twinkles = new Image[3];
        [SerializeField] private Image[] rays = new Image[9];
        [SerializeField] private Image flash;
        [Header("Locked / boss")]
        [SerializeField] private RectTransform grey;
        [SerializeField] private Image greyImage;
        [SerializeField] private GameObject lockBadge;
        [SerializeField] private Image halfLeft;
        [SerializeField] private Image halfRight;
        [SerializeField] private GameObject boss;
        [SerializeField] private TMP_Text bossTag;

        public int Level { get; private set; }
        public MapNodeState State { get; private set; }
        public int Stars { get; private set; }
        public bool IsBoss => isBoss;

        /// <summary>Current node is drawn 125 % (Play disc).</summary>
        public const float CurrentScale = 1.25f;

        /// <summary>Raised with this node when the disc is tapped.</summary>
        public event Action<MapNodeView> Tapped;

        private bool isBoss;
        private float lastClock, shakeAt = -10f, arriveAt = -10f;
        private bool sequenced;
        private static readonly Color StarOff = new Color(0.1f, 0.15f, 0.35f, 0.35f);
        private static readonly float[] TwinkleDelays = { 3.4f, 3.9f, 4.3f };
        private static readonly float[] RayAngles = { 0f, 40f, 80f, 120f, 160f, 200f, 240f, 280f, 320f };

        private static AnimationCurve shake, popS, hopY, bounceY, ringS, ringA, glowRS, glowRA, twkS, twkR, twkA, burstA, halfA,
                                      arriveS, openS, easeOutC;
        private static readonly Vector4 Pop = new Vector4(0.3f, 1.5f, 0.5f, 1f);
        private static readonly Vector4 EaseOut = new Vector4(0f, 0f, 0.58f, 1f);

        private void Awake()
        {
            button.onClick.AddListener(() => Tapped?.Invoke(this));
            BuildCurves();
        }

        public void Setup(int level, MapNodeState state, int starCount, bool bossLevel)
        {
            Level = level;
            State = state;
            Stars = starCount;
            isBoss = bossLevel;
            goldNumber.text = level.ToString();
            bossTag.text = level.ToString();
            for (int i = 0; i < stars.Length; i++) stars[i].color = i < starCount ? Color.white : StarOff;
            SeqClear();
        }

        /// <summary>Restores the settled look of the node's state (also after a sequence).</summary>
        public void SeqClear()
        {
            sequenced = false;
            bool current = State == MapNodeState.Current;
            bool done = State == MapNodeState.Completed;
            bool locked = State == MapNodeState.Locked;

            gold.SetActive(done && !isBoss);
            gold.transform.localScale = Vector3.one;
            starsRow.SetActive(done);
            boss.SetActive(isBoss && !current); // the current boss node is the red Play disc like any other (its card shows the Boss tag)
            boss.transform.localScale = Vector3.one;
            grey.gameObject.SetActive(locked && !isBoss);
            grey.localRotation = Quaternion.identity;
            grey.localScale = Vector3.one;
            SetAlpha(greyImage, 1f);
            lockBadge.SetActive(locked);

            glow.gameObject.SetActive(current);
            hop.gameObject.SetActive(current);
            hop.localScale = Vector3.one * (current ? CurrentScale : 1f);
            red.SetActive(current);
            playIcon.gameObject.SetActive(current);
            ring.gameObject.SetActive(current);
            foreach (Image tw in twinkles) tw.gameObject.SetActive(current);
            foreach (Image r in rays) r.gameObject.SetActive(false);
            flash.gameObject.SetActive(false);
            halfLeft.gameObject.SetActive(false);
            halfRight.gameObject.SetActive(false);
            transform.localScale = Vector3.one;
        }

        /// <summary>Locked node tapped: a short shake (plus the toast, shown by the screen).</summary>
        public void Shake() => shakeAt = lastClock;

        /// <summary>The node just became current after the unlock sequence: the Play disc pops in.</summary>
        public void PlayArrive() => arriveAt = lastClock;

        /// <summary>t = the map clock; loops only (the level-complete sequence is driven through the Seq* methods).</summary>
        public void Tick(float t)
        {
            lastClock = t;
            if (sequenced) return;

            if (State == MapNodeState.Locked && !isBoss)
            {
                float sk = (t - shakeAt) / 0.45f;
                grey.localRotation = Quaternion.Euler(0f, 0f, sk >= 0f && sk < 1f ? shake.Evaluate(sk) : 0f);
            }
            if (State != MapNodeState.Current) return;

            spin.localRotation = Quaternion.Euler(0f, 0f, -360f * (t / 6f % 1f));
            float gr = t / 1.6f % 1f;
            glowSoft.rectTransform.localScale = Vector3.one * glowRS.Evaluate(gr);
            SetAlpha(glowSoft, glowRA.Evaluate(gr) * 0.7f);

            // Soft pulsing ring (1.2 s) and a small bounce every 2 s.
            float rk = t / 1.2f % 1f;
            ring.rectTransform.localScale = Vector3.one * ringS.Evaluate(rk);
            SetAlpha(ring, ringA.Evaluate(rk));
            float bk = t % 2f / 0.55f;
            hop.anchoredPosition = new Vector2(0f, bk < 1f ? bounceY.Evaluate(bk) : 0f);

            // Arrival pop after the unlock (Play disc grows in).
            float ak = (t - arriveAt) / 0.4f;
            hop.localScale = Vector3.one * (CurrentScale * (ak >= 0f && ak < 1f ? arriveS.Evaluate(ak) : 1f));

            for (int i = 0; i < twinkles.Length; i++)
            {
                float k = (t + i * 0.6f) / 1.8f % 1f;
                RectTransform r = twinkles[i].rectTransform;
                r.localScale = Vector3.one * twkS.Evaluate(k);
                r.localRotation = Quaternion.Euler(0f, 0f, twkR.Evaluate(k));
                SetAlpha(twinkles[i], twkA.Evaluate(k));
            }
        }

        // ------------------------------------------------------------------ level-complete sequence

        /// <summary>The just-completed node: gold disc pops with a star burst and a short white flash at PopStart.</summary>
        public void SeqCompleted(float seqTime)
        {
            sequenced = true;
            float k = Mathf.Clamp01((seqTime - MapSequence.PopStart) / (MapSequence.PopEnd - MapSequence.PopStart));
            bool popped = seqTime >= MapSequence.PopStart;
            GameObject disc = isBoss ? boss : gold;
            disc.SetActive(true);
            starsRow.SetActive(true);
            disc.transform.localScale = Vector3.one * (popped ? popS.Evaluate(k) : 1f);
            for (int i = 0; i < stars.Length; i++)
            {
                Color c = i < Stars ? Color.white : StarOff;
                c.a = i < Stars && !popped ? 0.35f : c.a;
                stars[i].color = c;
            }

            flash.gameObject.SetActive(popped && k < 0.6f);
            if (flash.gameObject.activeSelf) SetAlpha(flash, Mathf.Lerp(0.95f, 0f, k / 0.6f));
            for (int i = 0; i < rays.Length; i++)
            {
                bool on = popped && k < 1f;
                rays[i].gameObject.SetActive(on);
                if (!on) continue;
                float e = easeOutC.Evaluate(k);
                float a = -RayAngles[i] * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(-Mathf.Sin(a), Mathf.Cos(a));
                RectTransform r = rays[i].rectTransform;
                r.anchoredPosition = RayHome + dir * (78f * e);
                r.localRotation = Quaternion.Euler(0f, 0f, -RayAngles[i]);
                r.localScale = Vector3.one * Mathf.Lerp(0.2f, 1f, e);
                SetAlpha(rays[i], burstA.Evaluate(k));
            }
        }

        /// <summary>The node being unlocked: lock shakes, cracks, halves fly off and fade, then it pops open to gold with a ring.</summary>
        public void SeqNext(float seqTime)
        {
            sequenced = true;
            glow.gameObject.SetActive(false);
            hop.gameObject.SetActive(false);
            ring.gameObject.SetActive(false);
            foreach (Image tw in twinkles) tw.gameObject.SetActive(false);

            bool cracked = seqTime >= MapSequence.CrackAt;
            bool opened = seqTime >= MapSequence.OpenStart;
            GameObject disc = isBoss ? boss : gold;

            // Grey disc + lock: plain, then shaking (0.2 s), then gone at the crack.
            bool showGrey = !cracked;
            grey.gameObject.SetActive(showGrey && !isBoss);
            lockBadge.SetActive(showGrey);
            if (showGrey)
            {
                float sk = Mathf.Clamp01((seqTime - MapSequence.ShakeStart) / (MapSequence.CrackAt - MapSequence.ShakeStart));
                float rot = seqTime >= MapSequence.ShakeStart ? shake.Evaluate(sk) : 0f;
                grey.localRotation = Quaternion.Euler(0f, 0f, rot);
                lockBadge.transform.localRotation = Quaternion.Euler(0f, 0f, rot);
            }

            // Two halves fly apart and fade (0.25 s).
            float hk = Mathf.Clamp01((seqTime - MapSequence.CrackAt) / 0.28f);
            bool halves = cracked && hk < 1f && !isBoss;
            halfLeft.gameObject.SetActive(halves);
            halfRight.gameObject.SetActive(halves);
            if (halves)
            {
                float e = easeOutC.Evaluate(hk);
                halfLeft.rectTransform.anchoredPosition = HalfHomeL + new Vector2(-46f * e, -34f * e);
                halfRight.rectTransform.anchoredPosition = HalfHomeR + new Vector2(46f * e, -34f * e);
                halfLeft.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 38f * e);
                halfRight.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -38f * e);
                SetAlpha(halfLeft, halfA.Evaluate(hk));
                SetAlpha(halfRight, halfA.Evaluate(hk));
            }

            // Pop open to gold + ring.
            float ok = Mathf.Clamp01((seqTime - MapSequence.OpenStart) / (MapSequence.End - MapSequence.OpenStart));
            disc.SetActive(isBoss || opened); // a locked boss keeps its red boss disc, a normal node shows gold only once opened
            starsRow.SetActive(false);
            disc.transform.localScale = Vector3.one * (opened ? openS.Evaluate(ok) : 1f);
            ring.gameObject.SetActive(opened && ok < 1f);
            if (ring.gameObject.activeSelf)
            {
                ring.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.7f, easeOutC.Evaluate(ok));
                SetAlpha(ring, Mathf.Lerp(0.9f, 0f, ok));
            }
        }

        private Vector2 rayHomeCache;
        private bool rayHomeSet;
        private Vector2 RayHome
        {
            get
            {
                if (!rayHomeSet && rays.Length > 0 && rays[0] != null)
                {
                    rayHomeCache = rays[0].rectTransform.anchoredPosition;
                    rayHomeSet = true;
                }
                return rayHomeCache;
            }
        }

        private Vector2 halfHomeL, halfHomeR;
        private bool halfHomeSet;
        private Vector2 HalfHomeL { get { CacheHalves(); return halfHomeL; } }
        private Vector2 HalfHomeR { get { CacheHalves(); return halfHomeR; } }

        private void CacheHalves()
        {
            if (halfHomeSet) return;
            halfHomeL = halfLeft.rectTransform.anchoredPosition;
            halfHomeR = halfRight.rectTransform.anchoredPosition;
            halfHomeSet = true;
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
            shake = CssCurve.Build(new[] { 0f, 0.2f, 0.4f, 0.6f, 0.8f, 1f }, new[] { 0f, 9f, -8f, 6f, -5f, 0f }, CssCurve.EaseInOut);
            popS = CssCurve.Build(new[] { 0f, 0.4f, 1f }, new[] { 1f, 1.35f, 1f }, Pop);
            hopY = CssCurve.Build(half, new[] { 0f, 6f, 0f }, CssCurve.EaseInOut);
            bounceY = CssCurve.Build(half, new[] { 0f, 9f, 0f }, CssCurve.EaseInOut);
            ringS = CssCurve.Build(ends, new[] { 0.85f, 1.45f }, EaseOut);
            ringA = CssCurve.Build(new[] { 0f, 0.15f, 1f }, new[] { 0f, 0.8f, 0f }, EaseOut);
            glowRS = CssCurve.Build(half, new[] { 1f, 1.25f, 1f }, CssCurve.EaseInOut);
            glowRA = CssCurve.Build(half, new[] { 0.85f, 0.45f, 0.85f }, CssCurve.EaseInOut);
            twkS = CssCurve.Build(half, new[] { 0f, 1f, 0f }, CssCurve.EaseInOut);
            twkR = CssCurve.Build(half, new[] { 0f, -90f, 0f }, CssCurve.EaseInOut);
            twkA = CssCurve.Build(half, new[] { 0f, 1f, 0f }, CssCurve.EaseInOut);
            burstA = CssCurve.Build(new[] { 0f, 0.2f, 1f }, new[] { 0f, 1f, 0f }, EaseOut);
            halfA = CssCurve.Build(new[] { 0f, 0.5f, 1f }, new[] { 1f, 0.9f, 0f }, EaseOut);
            arriveS = CssCurve.Build(new[] { 0f, 0.6f, 1f }, new[] { 0.3f, 1.15f, 1f }, Pop);
            openS = CssCurve.Build(new[] { 0f, 0.6f, 1f }, new[] { 0.4f, 1.18f, 1f }, Pop);
            easeOutC = CssCurve.Build(ends, new[] { 0f, 1f }, EaseOut);
        }
    }
}
