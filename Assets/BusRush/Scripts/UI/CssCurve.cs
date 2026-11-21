using UnityEngine;

namespace BusRush.UI
{
    /// <summary>
    /// Turns CSS @keyframes into AnimationCurves: per property, keyframe offsets (0..1) and values, with the CSS
    /// timing function applied to every segment between keyframes (as browsers do). Curves are sampled finely, so
    /// overshooting cubic-beziers (e.g. cubic-bezier(.3,1.5,.5,1)) are reproduced.
    /// </summary>
    public static class CssCurve
    {
        public static readonly Vector4 Linear = new Vector4(0f, 0f, 1f, 1f);
        public static readonly Vector4 EaseInOut = new Vector4(0.42f, 0f, 0.58f, 1f);

        /// <summary>Curve over normalized time 0..1. <paramref name="bezier"/> = (x1, y1, x2, y2).</summary>
        public static AnimationCurve Build(float[] offsets, float[] values, Vector4 bezier, int samplesPerSegment = 24)
        {
            var keys = new System.Collections.Generic.List<Keyframe>();
            for (int i = 0; i < offsets.Length - 1; i++)
            {
                for (int s = i == 0 ? 0 : 1; s <= samplesPerSegment; s++)
                {
                    float u = s / (float)samplesPerSegment;
                    float t = Mathf.Lerp(offsets[i], offsets[i + 1], u);
                    float v = Mathf.LerpUnclamped(values[i], values[i + 1], Bezier(bezier, u));
                    keys.Add(new Keyframe(t, v));
                }
            }

            // Linear tangents between the samples.
            for (int i = 0; i < keys.Count; i++)
            {
                Keyframe k = keys[i];
                k.inTangent = i > 0 ? Slope(keys[i - 1], k) : 0f;
                k.outTangent = i < keys.Count - 1 ? Slope(k, keys[i + 1]) : 0f;
                keys[i] = k;
            }
            return new AnimationCurve(keys.ToArray());
        }

        private static float Slope(Keyframe a, Keyframe b) => b.time > a.time ? (b.value - a.value) / (b.time - a.time) : 0f;

        /// <summary>CSS cubic-bezier(x1, y1, x2, y2) at progress x (0..1).</summary>
        public static float Bezier(Vector4 c, float x)
        {
            if (x <= 0f) return 0f;
            if (x >= 1f) return 1f;
            // Solve X(u) = x (Newton, then bisection fallback), return Y(u).
            float u = x;
            for (int i = 0; i < 8; i++)
            {
                float err = Cubic(c.x, c.z, u) - x;
                if (Mathf.Abs(err) < 1e-5f) return Cubic(c.y, c.w, u);
                float d = CubicDerivative(c.x, c.z, u);
                if (Mathf.Abs(d) < 1e-6f) break;
                u -= err / d;
            }
            float lo = 0f, hi = 1f;
            u = x;
            for (int i = 0; i < 30; i++)
            {
                float xu = Cubic(c.x, c.z, u);
                if (Mathf.Abs(xu - x) < 1e-5f) break;
                if (xu < x) lo = u;
                else hi = u;
                u = (lo + hi) * 0.5f;
            }
            return Cubic(c.y, c.w, u);
        }

        private static float Cubic(float p1, float p2, float u)
        {
            float v = 1f - u;
            return 3f * v * v * u * p1 + 3f * v * u * u * p2 + u * u * u;
        }

        private static float CubicDerivative(float p1, float p2, float u)
        {
            float v = 1f - u;
            return 3f * v * v * p1 + 6f * v * u * (p2 - p1) + 3f * u * u * (1f - p2);
        }
    }
}
