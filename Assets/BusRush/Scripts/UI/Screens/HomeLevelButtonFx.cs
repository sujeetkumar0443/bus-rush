using UnityEngine;

namespace BusRush.UI
{
    /// <summary>
    /// Idle shine of the Home "Level" button: a glossy diagonal streak sweeps across it every <see cref="sweepPeriod"/>
    /// seconds (the streak lives under a Mask shaped like the button face, so nothing overflows) and a few small
    /// stars twinkle at the corners at random times. Everything is transform-only (no mesh/colour changes, no
    /// allocations per frame) and runs on unscaled time. The breathing pulse is HomeAmbience's "pulse" (1.0 to 1.04).
    /// </summary>
    public sealed class HomeLevelButtonFx : MonoBehaviour
    {
        [Header("Shine")]
        [Tooltip("Parent of the streak images (moved across the button, tilted by its own rotation).")]
        [SerializeField] private RectTransform streak;
        [SerializeField, Min(0.5f)] private float sweepPeriod = 2.5f;
        [SerializeField, Min(0.1f)] private float sweepDuration = 0.6f;
        [Tooltip("Streak centre travels from -travel to +travel (UI units from the button centre).")]
        [SerializeField] private float travel = 230f;
        [SerializeField, Min(0f)] private float firstSweepDelay = 0.8f;

        [Header("Sparkles")]
        [SerializeField] private RectTransform[] sparkles = new RectTransform[0];
        [SerializeField, Min(0.1f)] private float twinkleSeconds = 0.7f;
        [SerializeField] private Vector2 twinkleGap = new Vector2(0.6f, 2.4f);
        [SerializeField] private float twinkleScale = 1f;
        [SerializeField] private float twinkleSpin = 50f;

        private AnimationCurve sweep, twinkle;
        private float[] next, born;
        private float t0;
        private bool streakOn;

        private void Awake()
        {
            // Streak: ease-in-out across the button; twinkle: 0 -> 1 -> 0 with ease in/out on both halves.
            sweep = CssCurve.Build(new[] { 0f, 1f }, new[] { 0f, 1f }, CssCurve.EaseInOut);
            twinkle = CssCurve.Build(new[] { 0f, 0.5f, 1f }, new[] { 0f, 1f, 0f }, CssCurve.EaseInOut);
            next = new float[sparkles.Length];
            born = new float[sparkles.Length];
        }

        private void OnEnable()
        {
            t0 = Time.unscaledTime + firstSweepDelay - sweepPeriod;
            SetStreak(false);
            float now = Time.unscaledTime;
            for (int i = 0; i < sparkles.Length; i++)
            {
                next[i] = now + Random.Range(0.2f, twinkleGap.y);
                born[i] = -1f;
                if (sparkles[i] != null) sparkles[i].localScale = Vector3.zero;
            }
        }

        private void Update()
        {
            float now = Time.unscaledTime;

            if (streak != null)
            {
                float p = (now - t0) % sweepPeriod;
                if (p < 0f) p += sweepPeriod;
                bool on = now >= t0 + sweepPeriod && p < sweepDuration;
                if (on != streakOn) SetStreak(on);
                if (on)
                {
                    float x = Mathf.LerpUnclamped(-travel, travel, sweep.Evaluate(p / sweepDuration));
                    streak.anchoredPosition = new Vector2(x, 0f);
                }
            }

            for (int i = 0; i < sparkles.Length; i++)
            {
                RectTransform s = sparkles[i];
                if (s == null) continue;
                if (born[i] < 0f)
                {
                    if (now < next[i]) continue;
                    born[i] = now;
                }
                float k = (now - born[i]) / twinkleSeconds;
                if (k >= 1f)
                {
                    s.localScale = Vector3.zero;
                    born[i] = -1f;
                    next[i] = now + Random.Range(twinkleGap.x, twinkleGap.y);
                    continue;
                }
                float size = twinkle.Evaluate(k) * twinkleScale;
                s.localScale = new Vector3(size, size, 1f);
                s.localRotation = Quaternion.Euler(0f, 0f, twinkleSpin * k);
            }
        }

        private void SetStreak(bool on)
        {
            streakOn = on;
            if (streak != null) streak.gameObject.SetActive(on);
        }
    }
}
