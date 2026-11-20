using System;
using System.Collections.Generic;
using UnityEngine;

namespace BusRush.Gameplay
{
    /// <summary>
    /// The passenger queue on the U-shaped track around the island. Two arms start at the gate, curve around the bottom
    /// of the ellipse, up its sides and straight up off screen. Person n uses arm n%2 and lane (n/2)%5; the person at
    /// queue index i targets 6 + (i/2)*10.5 (+5.25 on odd lanes) ref px from the gate and walks there at 85 px/s, so
    /// the crowd flows forward as the front boards. At most <see cref="maxVisible"/> people are instantiated (pooled);
    /// the rest exist only logically.
    /// </summary>
    public sealed class CrowdView : MonoBehaviour
    {
        public static readonly Vector2 Gate = new Vector2(195f, 228f);
        public static readonly Vector2 EllipseCenter = new Vector2(195f, 118f);
        public const float EllipseRx = 107f;
        public const float EllipseRy = 72f;
        public const float ArmTopY = -420f;
        public const float FrontRadius = 40f;
        private static readonly float[] Lanes = { -25f, -12.5f, 0f, 12.5f, 25f };

        [SerializeField] private PassengerView passengerPrefab;
        [SerializeField] private int maxVisible = 60;
        [SerializeField] private float walkSpeedPx = 85f;
        [SerializeField] private float boardSpeedPx = 420f;
        [SerializeField] private float minBoardTime = 0.25f;
        [SerializeField] private float passengerHeight = 0.45f;
        [Tooltip("Model height of the Passenger prefab (for scaling to passengerHeight).")]
        [SerializeField] private float modelHeight = 0.78f;

        private sealed class Person
        {
            public GameColor color;
            public int arm;
            public int lane;
            public float s;
            public float lateral;
            public PassengerView view;
        }

        private sealed class Walker
        {
            public Person person;
            public List<Vector2> path;
            public float length;
            public float time;
            public float duration;
            public Action onArrive;
        }

        private readonly List<Person> queue = new List<Person>();
        private readonly List<Walker> walkers = new List<Walker>();
        private readonly Stack<PassengerView> pool = new Stack<PassengerView>();
        private readonly List<Vector2>[] arms = new List<Vector2>[2];
        private readonly List<float>[] armLengths = new List<float>[2];
        private float spawnS;
        private float sharedLength;

        public int QueueCount => queue.Count;
        public int WalkerCount => walkers.Count;
        public int PassengersLeft => queue.Count + walkers.Count;
        public bool HasFront => queue.Count > 0;
        public GameColor FrontColor => queue[0].color;
        public bool FrontAtGate => queue.Count > 0 && queue[0].s <= FrontRadius;

        /// <summary>Queue index of the next passenger of this colour (int.MaxValue if none).</summary>
        public int DistanceToColor(GameColor color)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                if (queue[i].color == color) return i;
            }
            return int.MaxValue;
        }

        private void Awake()
        {
            BuildArms();
        }

        public void Build(IList<GameColor> colors)
        {
            foreach (Person p in queue) Release(p);
            foreach (Walker w in walkers) Release(w.person);
            queue.Clear();
            walkers.Clear();

            for (int n = 0; n < colors.Count; n++)
            {
                var p = new Person { color = colors[n], arm = n % 2, lane = (n / 2) % 5 };
                p.s = TargetS(n, p.lane);
                p.lateral = TargetLateral(p);
                queue.Add(p);
            }
            for (int i = 0; i < queue.Count && i < maxVisible; i++) Spawn(queue[i], false);
            Update();
        }

        /// <summary>Sends the front person to a seat; returns false if there is nobody at the gate.</summary>
        public bool BoardFront(Vector2 targetRef, Action onArrive)
        {
            if (!FrontAtGate) return false;
            Person p = queue[0];
            queue.RemoveAt(0);
            if (p.view == null) Spawn(p, false);

            var path = new List<Vector2> { ArmPoint(p.arm, p.s, p.lateral, out _), Gate, targetRef };
            float length = Vector2.Distance(path[0], path[1]) + Vector2.Distance(path[1], path[2]);
            walkers.Add(new Walker
            {
                person = p,
                path = path,
                length = length,
                duration = Mathf.Max(minBoardTime, length / boardSpeedPx),
                onArrive = onArrive
            });
            return true;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            for (int i = 0; i < queue.Count; i++)
            {
                Person p = queue[i];
                float target = TargetS(i, p.lane);
                float before = p.s;
                p.s = Mathf.MoveTowards(p.s, target, walkSpeedPx * dt);
                p.lateral = Mathf.MoveTowards(p.lateral, TargetLateral(p), walkSpeedPx * 0.5f * dt);

                if (p.view == null && i < maxVisible) Spawn(p, true);
                if (p.view == null) continue;

                Vector2 refPos = ArmPoint(p.arm, p.s, p.lateral, out Vector2 tangent);
                float moved = p.s - before;
                // Face the way they walk; standing people face the gate (down the arm).
                Vector2 facing = Mathf.Abs(moved) > 0.0001f ? tangent * Mathf.Sign(moved) : -tangent;
                Place(p.view, refPos, facing, dt);
                p.view.Animate(Mathf.Abs(moved) / Mathf.Max(0.0001f, walkSpeedPx * dt), dt);
            }

            for (int i = walkers.Count - 1; i >= 0; i--)
            {
                Walker w = walkers[i];
                w.time += dt;
                float k = Mathf.Clamp01(w.time / w.duration);
                Vector2 pos = PolylinePoint(w.path, k * w.length, out Vector2 dir);
                Place(w.person.view, pos, dir, dt);
                w.person.view.Animate(1f, dt);
                if (k >= 1f)
                {
                    walkers.RemoveAt(i);
                    Release(w.person);
                    w.onArrive?.Invoke();
                }
            }
        }

        // ------------------------------------------------------------------ track geometry (reference px)

        private void BuildArms()
        {
            Vector2 bottom = new Vector2(EllipseCenter.x, EllipseCenter.y + EllipseRy);
            for (int arm = 0; arm < 2; arm++)
            {
                var pts = new List<Vector2> { Gate, bottom };
                float end = arm == 0 ? 180f : 0f;
                const int steps = 16;
                for (int k = 1; k <= steps; k++)
                {
                    float a = Mathf.Lerp(90f, end, k / (float)steps) * Mathf.Deg2Rad;
                    pts.Add(new Vector2(EllipseCenter.x + EllipseRx * Mathf.Cos(a), EllipseCenter.y + EllipseRy * Mathf.Sin(a)));
                }
                pts.Add(new Vector2(pts[pts.Count - 1].x, ArmTopY));
                arms[arm] = pts;

                var lengths = new List<float> { 0f };
                for (int i = 1; i < pts.Count; i++) lengths.Add(lengths[i - 1] + Vector2.Distance(pts[i - 1], pts[i]));
                armLengths[arm] = lengths;
            }

            sharedLength = Vector2.Distance(Gate, bottom);
            // People who join the visible crowd appear just above the top edge and walk in.
            spawnS = ArmSAtY(0, -30f);
        }

        private float ArmSAtY(int arm, float y)
        {
            List<Vector2> pts = arms[arm];
            Vector2 top = pts[pts.Count - 2];
            return armLengths[arm][pts.Count - 2] + (top.y - y);
        }

        /// <summary>
        /// Distance from the gate for queue index i. The two arms alternate (one person per arm per row) and the five
        /// lanes cycle along each arm, so each arm advances one row (10.5 px) per person and long queues run up the
        /// sides of the island and off the top of the screen.
        /// </summary>
        public static float TargetS(int queueIndex, int lane)
        {
            return 6f + (queueIndex / 2) * 10.5f + (lane % 2 == 1 ? 5.25f : 0f);
        }

        /// <summary>
        /// Lane offset. Both arms share the short stretch between the gate and the bottom of the ellipse, so there the
        /// left arm keeps to the left half of the track and the right arm to the right half.
        /// </summary>
        private float TargetLateral(Person p)
        {
            float full = Lanes[p.lane];
            float shared = (p.arm == 0 ? -1f : 1f) * (12.5f + full * 0.5f);
            float k = Mathf.InverseLerp(sharedLength, sharedLength + 30f, p.s);
            return Mathf.Lerp(shared, full * (p.arm == 0 ? 1f : -1f), k);
        }

        private Vector2 ArmPoint(int arm, float s, float lateral, out Vector2 tangent)
        {
            Vector2 c = PolylinePoint(arms[arm], s, out tangent, armLengths[arm]);
            // Offset along the track normal; on the shared stretch (tangent = up) positive lateral is screen-right.
            // The left/right mirroring of the lanes is done in TargetLateral.
            Vector2 normal = new Vector2(-tangent.y, tangent.x);
            return c + normal * lateral;
        }

        private static Vector2 PolylinePoint(List<Vector2> pts, float s, out Vector2 dir, List<float> lengths = null)
        {
            if (lengths == null)
            {
                lengths = new List<float> { 0f };
                for (int i = 1; i < pts.Count; i++) lengths.Add(lengths[i - 1] + Vector2.Distance(pts[i - 1], pts[i]));
            }
            float total = lengths[lengths.Count - 1];
            if (s >= total)
            {
                dir = (pts[pts.Count - 1] - pts[pts.Count - 2]).normalized;
                return pts[pts.Count - 1] + dir * (s - total);
            }
            for (int i = 1; i < pts.Count; i++)
            {
                if (s > lengths[i]) continue;
                float seg = lengths[i] - lengths[i - 1];
                dir = seg > 0f ? (pts[i] - pts[i - 1]) / seg : Vector2.up;
                return Vector2.Lerp(pts[i - 1], pts[i], seg > 0f ? (s - lengths[i - 1]) / seg : 0f);
            }
            dir = (pts[1] - pts[0]).normalized;
            return pts[0];
        }

        // ------------------------------------------------------------------ views

        private void Place(PassengerView view, Vector2 refPos, Vector2 refDir, float dt)
        {
            Vector3 world = ScreenRef.RefToWorld(refPos);
            view.transform.position = world;
            if (refDir.sqrMagnitude > 1e-6f)
            {
                Vector3 ahead = ScreenRef.RefToWorld(refPos + refDir.normalized) - world;
                ahead.y = 0f;
                if (ahead.sqrMagnitude > 1e-8f)
                {
                    Quaternion want = Quaternion.LookRotation(ahead.normalized, Vector3.up);
                    view.transform.rotation = Quaternion.RotateTowards(view.transform.rotation, want, 540f * dt);
                }
            }
        }

        private void Spawn(Person p, bool walkIn)
        {
            PassengerView v = pool.Count > 0 ? pool.Pop() : Instantiate(passengerPrefab, transform);
            v.gameObject.SetActive(true);
            v.transform.localScale = Vector3.one * (passengerHeight / modelHeight);
            v.SetColor(p.color);
            v.ResetPose();
            if (walkIn) p.s = Mathf.Max(p.s, spawnS); // appear above the top edge and walk in, never pop in view
            p.view = v;
            Vector2 refPos = ArmPoint(p.arm, p.s, p.lateral, out Vector2 t);
            v.transform.SetPositionAndRotation(ScreenRef.RefToWorld(refPos),
                Quaternion.LookRotation(ScreenRef.RefToWorld(refPos - t) - ScreenRef.RefToWorld(refPos), Vector3.up));
        }

        private void Release(Person p)
        {
            if (p.view == null) return;
            p.view.gameObject.SetActive(false);
            pool.Push(p.view);
            p.view = null;
        }

        private void OnDrawGizmos()
        {
            if (arms[0] == null) BuildArms();
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
            for (int arm = 0; arm < 2; arm++)
            {
                for (int i = 1; i < arms[arm].Count; i++)
                {
                    Gizmos.DrawLine(ScreenRef.RefToWorld(arms[arm][i - 1]), ScreenRef.RefToWorld(arms[arm][i]));
                }
            }
            Gizmos.DrawSphere(ScreenRef.RefToWorld(Gate), 0.12f);
        }
    }
}
