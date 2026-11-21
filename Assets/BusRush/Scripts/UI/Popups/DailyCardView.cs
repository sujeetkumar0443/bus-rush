using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>
    /// One Daily Reward day card (DailyReward.dc.html): claimed = green with a white tick in a green circle, today =
    /// gold with a pulsing glow, future = plain (85%). Day 7 keeps its purple look and only gets the tick / glow.
    /// </summary>
    public sealed class DailyCardView : MonoBehaviour
    {
        public enum State
        {
            Claimed,
            Today,
            Future
        }

        [SerializeField] private UiGradient face;
        [SerializeField] private Image border;
        [SerializeField] private TMP_Text label;
        [SerializeField] private GameObject claimedOverlay;
        [SerializeField] private RectTransform tick;
        [SerializeField] private Image glow;
        [SerializeField] private GameObject glowRing;
        [SerializeField] private CanvasGroup group;
        [Tooltip("Day 7: keep the purple face, only show the tick / glow.")]
        [SerializeField] private bool keepColours;

        public State Current { get; private set; }

        private static readonly Color ClaimedTop = new Color32(0xDF, 0xF7, 0xD2, 0xFF), ClaimedBottom = new Color32(0xA8, 0xE2, 0x8F, 0xFF),
                                      ClaimedEdge = new Color32(0x2E, 0x7D, 0x1E, 0xFF);
        private static readonly Color TodayTop = new Color32(0xFF, 0xF4, 0xC7, 0xFF), TodayBottom = new Color32(0xFF, 0xD8, 0x6B, 0xFF),
                                      TodayEdge = new Color32(0x8A, 0x4A, 0x05, 0xFF);
        private static readonly Color FutureTop = Color.white, FutureBottom = new Color32(0xD9, 0xE7, 0xFA, 0xFF),
                                      FutureEdge = new Color32(0x8F, 0xA8, 0xCF, 0xFF);

        public void SetState(State state)
        {
            Current = state;
            if (!keepColours)
            {
                (Color top, Color bottom, Color edge) c = state switch
                {
                    State.Claimed => (ClaimedTop, ClaimedBottom, ClaimedEdge),
                    State.Today => (TodayTop, TodayBottom, TodayEdge),
                    _ => (FutureTop, FutureBottom, FutureEdge)
                };
                face.Set(c.top, c.bottom);
                border.color = c.edge;
                label.color = c.edge;
                group.alpha = state == State.Future ? 0.85f : 1f;
            }
            claimedOverlay.SetActive(state == State.Claimed);
            glow.gameObject.SetActive(state == State.Today);
            glowRing.SetActive(state == State.Today);
            tick.localScale = Vector3.one;
        }

        /// <summary>Today -> claimed with a pop of the tick.</summary>
        public void PlayClaimed()
        {
            SetState(State.Claimed);
            Tween.Scale(this, tick, Vector3.zero, Vector3.one, 0.35f, Ease.OutBack);
            Tween.Scale(this, transform, Vector3.one * 1.12f, Vector3.one, 0.3f, Ease.OutBack);
        }

        private void Update()
        {
            if (Current != State.Today) return;
            // glowB 1.2 s: 0 0 18px 4px rgba(255,214,60,.9) <-> 0 0 6px 1px rgba(255,214,60,.5)
            float k = 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime / 1.2f * Mathf.PI * 2f);
            Color c = glow.color;
            c.a = Mathf.Lerp(0.9f, 0.5f, k);
            glow.color = c;
            glow.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 0.92f, k);
        }
    }
}
