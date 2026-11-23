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
    /// Times (seconds) of the level-complete sequence on the map; <see cref="RoadMapScreen"/> drives it, nodes follow.
    /// A: camera scrolls to the completed pin, it lifts 14 px, drops back with a squash bounce and turns orange, its stars pop in.
    /// B: the road to the next node fills with gold, a bright dot travels along it.
    /// C: the next pin's padlock shakes and breaks, the pin turns green with a drop-in squash bounce and a ring flash, then PLAY + ripples.
    /// </summary>
    public static class MapSequence
    {
        public const float Scroll = 0.45f;
        public const float LiftStart = 0.45f, LiftApex = 0.70f, LiftEnd = 1.00f;   // up 0.25 s, back down 0.3 s (+ squash)
        public const float StarStart = 0.95f, StarStep = 0.12f, StarPop = 0.18f;    // three stars, one by one
        public const float PathStart = 1.20f, PathEnd = 2.00f;                      // 0.8 s
        public const float ShakeStart = 2.00f, BreakAt = 2.25f;                     // padlock shakes, then breaks into 6 pieces
        public const float BounceEnd = 2.70f;                                       // drop-in squash bounce 0.45 s from BreakAt
        public const float PlayAppear = 2.70f, End = 2.90f;                         // PLAY pill + ripples, then the pin is the current one
    }

    /// <summary>
    /// One level "map pin" on the Road Map: a teardrop pin (generated sprite, point facing down) with a white circle holding the number.
    /// DONE orange + 3 stars under it, CURRENT green with a bobbing PLAY pill and two ripples on the ground, LOCKED grey with a padlock,
    /// BOSS (every 10th level) red with a crown (a locked boss is grey with a faded crown). Every pin has a soft ground shadow.
    /// <see cref="Tick"/> plays the loops; <see cref="SeqCompleted"/> / <see cref="SeqNext"/> follow the level-complete sequence
    /// (transform / colour changes only, nothing allocated).
    /// </summary>
    public sealed class MapNodeView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [Header("Pin")]
        [SerializeField] private RectTransform body;          // pin + number + lock + crown; its pivot is the pin's tip (lift / squash / shake)
        [SerializeField] private Image pin;
        [SerializeField] private Sprite pinDone;
        [SerializeField] private Sprite pinCurrent;
        [SerializeField] private Sprite pinLocked;
        [SerializeField] private Sprite pinBoss;
        [SerializeField] private TMP_Text number;
        [SerializeField] private Image lockIcon;
        [SerializeField] private Image crown;
        [SerializeField] private Image shadow;
        [Header("Done")]
        [SerializeField] private RectTransform starsRow;
        [SerializeField] private Image[] stars = new Image[3];
        [Header("Current")]
        [SerializeField] private RectTransform playMarker;
        [SerializeField] private TMP_Text playText;
        [SerializeField] private Image[] ripples = new Image[2];
        [Header("Unlock fx")]
        [SerializeField] private Image flashRing;
        [SerializeField] private Image[] pieces = new Image[6];

        public int Level { get; private set; }
        public MapNodeState State { get; private set; }
        public int Stars { get; private set; }
        public bool IsBoss => isBoss;

        /// <summary>Raised with this node when the pin is tapped.</summary>
        public event Action<MapNodeView> Tapped;

        public static readonly Color NumberDone = new Color32(0xD2, 0x53, 0x0F, 0xFF);
        public static readonly Color NumberCurrent = new Color32(0x15, 0x82, 0x2C, 0xFF);
        public static readonly Color NumberBoss = new Color32(0xC4, 0x1F, 0x31, 0xFF);
        private static readonly Color StarOff = new Color(0.1f, 0.15f, 0.35f, 0.3f);

        private const float CurrentScale = 1.1f;
        private const float LiftHeight = 14f;
        private bool isBoss, sequenced, homeSet;
        private float lastClock, shakeAt = -10f;
        private Vector2 bodyHome, playHome, pieceHome;
        private Vector2[] pieceDir;

        private static AnimationCurve shake, popS, dropS, easeOutC, ringA, rippleS, rippleA, markerS, squashY;
        private static readonly Vector4 Pop = new Vector4(0.3f, 1.5f, 0.5f, 1f);
        private static readonly Vector4 EaseOut = new Vector4(0f, 0f, 0.58f, 1f);

        private void Awake()
        {
            button.onClick.AddListener(() => Tapped?.Invoke(this));
            BuildCurves();
            CacheHome();
            pieceDir = new Vector2[pieces.Length];
            for (int i = 0; i < pieces.Length; i++)
            {
                float a = (i * 60f + 20f) * Mathf.Deg2Rad;
                pieceDir[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            }
        }

        private void CacheHome()
        {
            if (homeSet) return;
            homeSet = true;
            bodyHome = body.anchoredPosition;
            playHome = playMarker.anchoredPosition;
            pieceHome = flashRing.rectTransform.anchoredPosition; // the pieces start at the pin's circle centre
        }

        public void Setup(int level, MapNodeState state, int starCount, bool bossLevel)
        {
            CacheHome();
            Level = level;
            State = state;
            Stars = starCount;
            isBoss = bossLevel;
            number.text = level.ToString();
            if (playText != null) playText.text = Localizer.T("PLAY");
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

            ShowPin(locked ? pinLocked : isBoss ? pinBoss : done ? pinDone : pinCurrent,
                    isBoss ? NumberBoss : done ? NumberDone : NumberCurrent, !locked);
            lockIcon.gameObject.SetActive(locked);
            crown.gameObject.SetActive(isBoss);
            SetAlpha(crown, locked ? 0.45f : 1f);

            starsRow.gameObject.SetActive(done);
            foreach (Image s in stars) s.rectTransform.localScale = Vector3.one;
            playMarker.gameObject.SetActive(current);
            playMarker.localScale = Vector3.one;
            playMarker.anchoredPosition = playHome;
            foreach (Image r in ripples) r.gameObject.SetActive(current);
            flashRing.gameObject.SetActive(false);
            foreach (Image p in pieces) p.gameObject.SetActive(false);

            body.anchoredPosition = bodyHome;
            body.localRotation = Quaternion.identity;
            body.localScale = Vector3.one * (current ? CurrentScale : 1f);
            SetAlpha(shadow, 0.38f);
        }

        private void ShowPin(Sprite sprite, Color numberColor, bool showNumber)
        {
            pin.sprite = sprite;
            number.gameObject.SetActive(showNumber);
            number.color = numberColor;
        }

        /// <summary>Locked pin tapped: a short shake (plus the toast, shown by the screen).</summary>
        public void Shake() => shakeAt = lastClock;

        /// <summary>t = the map clock; loops only (the level-complete sequence is driven through the Seq* methods).</summary>
        public void Tick(float t)
        {
            lastClock = t;
            if (sequenced) return;

            if (State == MapNodeState.Locked)
            {
                float sk = (t - shakeAt) / 0.45f;
                body.localRotation = Quaternion.Euler(0f, 0f, sk >= 0f && sk < 1f ? shake.Evaluate(sk) : 0f);
            }
            if (State == MapNodeState.Current) Loops(t);
        }

        /// <summary>Two expanding ripples on the ground under the point and a bobbing PLAY pill.</summary>
        private void Loops(float t)
        {
            for (int i = 0; i < ripples.Length; i++)
            {
                float k = (t + i * 0.9f) / 1.8f % 1f;
                float s = rippleS.Evaluate(k);
                ripples[i].rectTransform.localScale = new Vector3(s, s * 0.42f, 1f);
                SetAlpha(ripples[i], rippleA.Evaluate(k));
            }
            playMarker.anchoredPosition = playHome + new Vector2(0f, Mathf.Sin(t * 4.2f) * 3f);
        }

        // ------------------------------------------------------------------ level-complete sequence

        /// <summary>
        /// The completed pin: it still looks like the green current pin, lifts 14 px at LiftStart, drops back with a squash bounce, turns
        /// orange at the top of the lift, and its stars pop in one by one.
        /// </summary>
        public void SeqCompleted(float seqTime)
        {
            sequenced = true;
            bool afterApex = seqTime >= MapSequence.LiftApex;
            ShowPin(isBoss ? pinBoss : afterApex ? pinDone : pinCurrent, isBoss ? NumberBoss : afterApex ? NumberDone : NumberCurrent, true);
            lockIcon.gameObject.SetActive(false);
            crown.gameObject.SetActive(isBoss);
            SetAlpha(crown, 1f);
            playMarker.gameObject.SetActive(false);
            foreach (Image r in ripples) r.gameObject.SetActive(false);

            float y = 0f, sx = 1f, sy = 1f;
            if (seqTime >= MapSequence.LiftStart && seqTime < MapSequence.LiftApex)
            {
                float k = (seqTime - MapSequence.LiftStart) / (MapSequence.LiftApex - MapSequence.LiftStart);
                y = LiftHeight * easeOutC.Evaluate(k);
            }
            else if (seqTime >= MapSequence.LiftApex)
            {
                float fall = MapSequence.LiftEnd - 0.12f - MapSequence.LiftApex;
                float k = Mathf.Clamp01((seqTime - MapSequence.LiftApex) / fall);
                y = LiftHeight * (1f - k * k);                                           // falls, accelerating
                float sq = (seqTime - (MapSequence.LiftEnd - 0.12f)) / 0.28f;            // squash on landing
                if (sq >= 0f && sq < 1f)
                {
                    sy = squashY.Evaluate(sq);
                    sx = 2f - sy;
                }
            }
            body.anchoredPosition = bodyHome + new Vector2(0f, y);
            body.localScale = new Vector3(CurrentScale * sx, CurrentScale * sy, 1f);
            body.localRotation = Quaternion.identity;
            SetAlpha(shadow, 0.38f * (1f - y / LiftHeight * 0.45f));

            starsRow.gameObject.SetActive(seqTime >= MapSequence.StarStart);
            for (int i = 0; i < stars.Length; i++)
            {
                float k = (seqTime - (MapSequence.StarStart + i * MapSequence.StarStep)) / MapSequence.StarPop;
                stars[i].rectTransform.localScale = Vector3.one * (k <= 0f ? 0f : k >= 1f ? 1f : popS.Evaluate(k));
                stars[i].color = i < Stars ? Color.white : StarOff;
            }
        }

        /// <summary>
        /// The pin being unlocked: grey with a padlock; the padlock shakes (ShakeStart) and breaks into 6 pieces (BreakAt); the pin turns
        /// green with a drop-in squash bounce (scale 0.6 -> 1.1 -> 1 in 0.45 s) and a ring flash; then the PLAY pill and ripples appear.
        /// </summary>
        public void SeqNext(float seqTime)
        {
            sequenced = true;
            starsRow.gameObject.SetActive(false);
            bool broken = seqTime >= MapSequence.BreakAt;
            ShowPin(broken ? (isBoss ? pinBoss : pinCurrent) : pinLocked, isBoss ? NumberBoss : NumberCurrent, broken);
            lockIcon.gameObject.SetActive(!broken);
            crown.gameObject.SetActive(isBoss);
            SetAlpha(crown, broken ? 1f : 0.45f);

            if (!broken)
            {
                float sk = Mathf.Clamp01((seqTime - MapSequence.ShakeStart) / (MapSequence.BreakAt - MapSequence.ShakeStart));
                body.localRotation = Quaternion.Euler(0f, 0f, seqTime >= MapSequence.ShakeStart ? shake.Evaluate(sk) * 1.4f : 0f);
                body.anchoredPosition = bodyHome;
                body.localScale = Vector3.one;
                playMarker.gameObject.SetActive(false);
                foreach (Image r in ripples) r.gameObject.SetActive(false);
                flashRing.gameObject.SetActive(false);
                foreach (Image p in pieces) p.gameObject.SetActive(false);
                return;
            }

            float bk = Mathf.Clamp01((seqTime - MapSequence.BreakAt) / (MapSequence.BounceEnd - MapSequence.BreakAt));
            float s = dropS.Evaluate(bk);                                   // 0.6 -> 1.1 -> 1: drop-in squash bounce
            body.localRotation = Quaternion.identity;
            body.localScale = new Vector3(CurrentScale * s, CurrentScale * (2f - s), 1f);
            body.anchoredPosition = bodyHome + new Vector2(0f, 18f * (1f - bk) * (1f - bk));

            // 6 pieces of the broken padlock fly out and fade (0.4 s).
            float pk = (seqTime - MapSequence.BreakAt) / 0.4f;
            bool pieceOn = pk >= 0f && pk < 1f;
            for (int i = 0; i < pieces.Length; i++)
            {
                pieces[i].gameObject.SetActive(pieceOn);
                if (!pieceOn) continue;
                float e = easeOutC.Evaluate(pk);
                RectTransform r = pieces[i].rectTransform;
                r.anchoredPosition = pieceHome + pieceDir[i] * (38f * e) + new Vector2(0f, -30f * pk * pk);
                r.localRotation = Quaternion.Euler(0f, 0f, 120f * e * (i % 2 == 0 ? 1f : -1f));
                r.localScale = Vector3.one * Mathf.Lerp(1f, 0.4f, pk);
                SetAlpha(pieces[i], Mathf.Lerp(1f, 0f, pk * pk));
            }

            // Ring flash on the pin.
            float fk = (seqTime - MapSequence.BreakAt) / 0.4f;
            bool flashOn = fk >= 0f && fk < 1f;
            flashRing.gameObject.SetActive(flashOn);
            if (flashOn)
            {
                flashRing.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.9f, easeOutC.Evaluate(fk));
                SetAlpha(flashRing, ringA.Evaluate(fk));
            }

            // PLAY pill + ripples appear after the bounce.
            bool play = seqTime >= MapSequence.PlayAppear;
            playMarker.gameObject.SetActive(play);
            foreach (Image r in ripples) r.gameObject.SetActive(play);
            if (play)
            {
                float mk = Mathf.Clamp01((seqTime - MapSequence.PlayAppear) / (MapSequence.End - MapSequence.PlayAppear));
                playMarker.localScale = Vector3.one * markerS.Evaluate(mk);
                Loops(seqTime);
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
            float[] ends = { 0f, 1f };
            shake = CssCurve.Build(new[] { 0f, 0.2f, 0.4f, 0.6f, 0.8f, 1f }, new[] { 0f, 9f, -8f, 6f, -5f, 0f }, CssCurve.EaseInOut);
            popS = CssCurve.Build(new[] { 0f, 0.6f, 1f }, new[] { 0f, 1.3f, 1f }, Pop);
            dropS = CssCurve.Build(new[] { 0f, 0.6f, 1f }, new[] { 0.6f, 1.1f, 1f }, Pop);
            easeOutC = CssCurve.Build(ends, new[] { 0f, 1f }, EaseOut);
            ringA = CssCurve.Build(new[] { 0f, 0.15f, 1f }, new[] { 0f, 0.9f, 0f }, EaseOut);
            rippleS = CssCurve.Build(ends, new[] { 0.35f, 1.5f }, EaseOut);
            rippleA = CssCurve.Build(new[] { 0f, 0.2f, 1f }, new[] { 0f, 0.7f, 0f }, EaseOut);
            markerS = CssCurve.Build(new[] { 0f, 0.7f, 1f }, new[] { 0f, 1.12f, 1f }, Pop);
            squashY = CssCurve.Build(new[] { 0f, 0.35f, 1f }, new[] { 1f, 0.8f, 1f }, CssCurve.EaseInOut);
        }
    }
}
