using System.Collections.Generic;
using UnityEngine;

namespace BusRush.Gameplay
{
    /// <summary>
    /// Arc-length path follower on the ground plane. Corners are rounded with quadratic fillets; a precomputed speed
    /// profile (max speed, corner speed, stop at the end) is limited by the acceleration both ways, so vehicles speed up,
    /// slow down before corners and ease into a stop.
    /// </summary>
    public sealed class PathFollower
    {
        public struct Settings
        {
            public float maxSpeed;
            public float acceleration;
            public float cornerRadius;
            /// <summary>Optional fillet radius per waypoint (overrides cornerRadius at that corner).</summary>
            public float[] cornerRadii;
            public float cornerSpeedFactor;
            public bool stopAtEnd;
            public float startSpeed;
        }

        private readonly List<Vector3> points = new List<Vector3>();
        private readonly List<float> distance = new List<float>();
        private readonly List<float> profile = new List<float>();
        private readonly Settings settings;
        private const float MinCrawl = 0.12f;

        public float Length { get; }
        public float Distance { get; private set; }
        public float Speed { get; private set; }
        public bool IsBraking { get; private set; }
        public bool Done { get; private set; }
        public float MaxSpeed => settings.maxSpeed;
        public Vector3 Position { get; private set; }
        public Vector3 Tangent { get; private set; } = Vector3.forward;

        public PathFollower(IList<Vector3> waypoints, Settings s)
        {
            settings = s;
            BuildSmoothed(waypoints, s.cornerRadius, s.cornerRadii);

            distance.Add(0f);
            for (int i = 1; i < points.Count; i++) distance.Add(distance[i - 1] + Vector3.Distance(points[i - 1], points[i]));
            Length = distance[distance.Count - 1];
            BuildProfile();

            Speed = Mathf.Min(s.startSpeed, profile.Count > 0 ? profile[0] : 0f);
            Sample(0f, out Vector3 p, out Vector3 t);
            Position = p;
            Tangent = t;
            Done = Length <= 0.0001f;
        }

        /// <summary>Advances along the path. <paramref name="speedCap"/> limits speed (e.g. traffic spacing).</summary>
        public void Step(float dt, float speedCap = float.MaxValue)
        {
            if (Done || dt <= 0f) return;

            float target = Mathf.Min(ProfileAt(Distance), speedCap);
            float previous = Speed;
            if (Speed < target) Speed = Mathf.Min(target, Speed + settings.acceleration * dt);
            else Speed = Mathf.Max(target, Speed - settings.acceleration * 2.5f * dt);

            float remaining = Length - Distance;
            if (settings.stopAtEnd && Speed < MinCrawl && remaining > 0.002f && speedCap > MinCrawl) Speed = MinCrawl;
            IsBraking = Speed < previous - 0.0001f;

            Distance = Mathf.Min(Length, Distance + Speed * dt);
            Sample(Distance, out Vector3 p, out Vector3 t);
            Position = p;
            Tangent = t;
            if (Distance >= Length - 0.0005f)
            {
                Done = true;
                if (settings.stopAtEnd) Speed = 0f;
            }
        }

        private void BuildSmoothed(IList<Vector3> wp, float radius, float[] radii)
        {
            if (wp.Count == 0) return;
            points.Add(wp[0]);
            for (int i = 1; i < wp.Count - 1; i++)
            {
                Vector3 a = wp[i - 1], p = wp[i], b = wp[i + 1];
                Vector3 d1 = (p - a).normalized, d2 = (b - p).normalized;
                float cornerRadius = radii != null && i < radii.Length ? radii[i] : radius;
                float r = Mathf.Min(cornerRadius,Vector3.Distance(a, p) * 0.5f, Vector3.Distance(p, b) * 0.5f);
                if (Vector3.Dot(d1, d2) > 0.999f || r <= 0.001f)
                {
                    points.Add(p);
                    continue;
                }

                Vector3 p1 = p - d1 * r, p2 = p + d2 * r;
                const int samples = 8;
                for (int k = 0; k <= samples; k++)
                {
                    float t = k / (float)samples;
                    Vector3 q = (1 - t) * (1 - t) * p1 + 2 * (1 - t) * t * p + t * t * p2;
                    points.Add(q);
                    cornerFlags.Add(points.Count - 1);
                }
            }
            points.Add(wp[wp.Count - 1]);
        }

        private readonly HashSet<int> cornerFlags = new HashSet<int>();

        private void BuildProfile()
        {
            int n = points.Count;
            for (int i = 0; i < n; i++)
            {
                profile.Add(cornerFlags.Contains(i) ? settings.maxSpeed * settings.cornerSpeedFactor : settings.maxSpeed);
            }
            if (settings.stopAtEnd && n > 0) profile[n - 1] = 0f;

            // Backward pass: never require more deceleration than the acceleration allows.
            for (int i = n - 2; i >= 0; i--)
            {
                float ds = distance[i + 1] - distance[i];
                profile[i] = Mathf.Min(profile[i], Mathf.Sqrt(profile[i + 1] * profile[i + 1] + 2f * settings.acceleration * ds));
            }
        }

        private int Segment(float d)
        {
            int lo = 0, hi = distance.Count - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (distance[mid] <= d) lo = mid; else hi = mid;
            }
            return lo;
        }

        private float ProfileAt(float d)
        {
            if (profile.Count == 0) return 0f;
            int i = Segment(d);
            if (i >= profile.Count - 1) return profile[profile.Count - 1];
            float seg = distance[i + 1] - distance[i];
            float k = seg > 0f ? (d - distance[i]) / seg : 0f;
            return Mathf.Lerp(profile[i], profile[i + 1], k);
        }

        public void Sample(float d, out Vector3 position, out Vector3 tangent)
        {
            if (points.Count == 1)
            {
                position = points[0];
                tangent = Tangent;
                return;
            }
            int i = Mathf.Min(Segment(d), points.Count - 2);
            float seg = distance[i + 1] - distance[i];
            float k = seg > 0f ? Mathf.Clamp01((d - distance[i]) / seg) : 0f;
            position = Vector3.Lerp(points[i], points[i + 1], k);
            Vector3 t = points[i + 1] - points[i];
            tangent = t.sqrMagnitude > 1e-8f ? t.normalized : Tangent;
        }
    }
}
