using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>
    /// SVG-style stroke along a polyline (local space): width, optional dash pattern ("stroke-dasharray") with an
    /// offset ("stroke-dashoffset") and round-ish caps (each dash is a quad strip with small end extensions).
    /// </summary>
    public sealed class UiPolyline : MaskableGraphic
    {
        [SerializeField] private float width = 4f;
        [Tooltip("Dash length; 0 = solid line.")]
        [SerializeField] private float dash;
        [SerializeField] private float gap;
        [SerializeField] private float dashOffset;

        private readonly List<Vector2> points = new List<Vector2>();
        private readonly List<Vector2> run = new List<Vector2>(32); // reused by every rebuild: no allocation per frame
        private float totalLength;
        private float fill = 1f;

        public void SetPoints(IList<Vector2> pts)
        {
            points.Clear();
            points.AddRange(pts);
            totalLength = 0f;
            for (int i = 1; i < points.Count; i++) totalLength += Vector2.Distance(points[i - 1], points[i]);
            SetVerticesDirty();
        }

        /// <summary>Fraction (0..1) of the line that is drawn, from its first point: the gold path filling along the road.</summary>
        public float Fill
        {
            get => fill;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Abs(fill - value) < 0.0004f) return;
                fill = value;
                SetVerticesDirty();
            }
        }

        public void SetStyle(float strokeWidth, float dashLength, float gapLength)
        {
            width = strokeWidth;
            dash = dashLength;
            gap = gapLength;
            SetVerticesDirty();
        }

        public float DashOffset
        {
            get => dashOffset;
            set
            {
                if (Mathf.Approximately(dashOffset, value)) return;
                dashOffset = value;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (points.Count < 2) return;
            float half = width * 0.5f;
            bool dashed = dash > 0f && gap > 0f;
            float period = dash + gap;
            // SVG: positive offset shifts the pattern backwards along the path.
            float phase = dashed ? Mod(dashOffset, period) : 0f;

            float along = 0f;
            float limit = totalLength * fill;
            run.Clear();
            for (int i = 1; i < points.Count; i++)
            {
                if (along >= limit) break;
                Vector2 a = points[i - 1], b = points[i];
                float len = Vector2.Distance(a, b);
                if (len < 1e-4f) continue;
                float end = Mathf.Min(len, limit - along);
                float s = 0f;
                while (s < end)
                {
                    float pos = along + s;
                    bool on = !dashed || Mod(pos + phase, period) < dash;
                    float toEdge = dashed ? (on ? dash - Mod(pos + phase, period) : period - Mod(pos + phase, period)) : end - s;
                    float step = Mathf.Min(end - s, Mathf.Max(toEdge, 1e-3f));
                    if (on)
                    {
                        if (run.Count == 0) run.Add(Vector2.Lerp(a, b, s / len));
                        run.Add(Vector2.Lerp(a, b, (s + step) / len));
                    }
                    else if (run.Count > 0)
                    {
                        Strip(vh, run, half);
                        run.Clear();
                    }
                    s += step;
                }
                along += len;
            }
            if (run.Count > 0) Strip(vh, run, half);
        }

        private void Strip(VertexHelper vh, List<Vector2> run, float half)
        {
            if (run.Count < 2) return;
            Color32 c = color;
            int start = vh.currentVertCount;
            for (int i = 0; i < run.Count; i++)
            {
                Vector2 dir = i == 0 ? run[1] - run[0] : i == run.Count - 1 ? run[i] - run[i - 1] : run[i + 1] - run[i - 1];
                dir = dir.sqrMagnitude > 1e-8f ? dir.normalized : Vector2.right;
                Vector2 n = new Vector2(-dir.y, dir.x) * half;
                Vector2 p = run[i];
                // Round-cap stand-in: extend the ends by half the width.
                if (i == 0) p -= dir * half;
                else if (i == run.Count - 1) p += dir * half;
                vh.AddVert(p + n, c, Vector4.zero);
                vh.AddVert(p - n, c, Vector4.zero);
            }
            for (int i = 0; i < run.Count - 1; i++)
            {
                int v = start + i * 2;
                vh.AddTriangle(v, v + 1, v + 3);
                vh.AddTriangle(v, v + 3, v + 2);
            }
        }

        private static float Mod(float x, float m) => ((x % m) + m) % m;
    }
}
