using System;
using System.Collections.Generic;
using UnityEngine;

namespace BusRush.Gameplay
{
    /// <summary>
    /// The passenger queue on the round plaza (City v2). The track is the ellipse centre (195,112), rx 112, ry 82;
    /// the right arm starts 14 px from the bottom centre, goes round the right side and straight up the alley at
    /// x = 307 off screen; the left arm mirrors via x = 83. Three lanes per arm at -21 / 0 / +21 px, rows 22 px apart.
    /// <para>Person n joins as row n/3, lane n%3, arm row%2 (so the front of the queue stands at the bottom of the U next
    /// to the gate, and rows alternate between the arms). Each arm/lane pair is a column: a person stands at
    /// 14 + k*22 where k = people still ahead of them in the same column, which at the start is 14 + (row/2)*22. When
    /// someone boards, the people behind them in their column step forward (shuffle speed) to close the gap; everyone
    /// keeps their arm and lane, so nobody cuts across the plaza.</para>
    /// <para>Boarding window: the first <see cref="GameTuning.FrontWindow"/> people in queue order are eligible (they
    /// bounce gently). A boarding passenger steps out to the outer edge of their arm, runs along it to the gate and on
    /// to the vehicle.</para>
    /// At most <see cref="maxVisible"/> people are instantiated (pooled); the rest exist only logically up the alleys.
    /// </summary>
    public sealed class CrowdView : MonoBehaviour
    {
        public static readonly Vector2 Gate = new Vector2(195f, 228f);
        public static readonly Vector2 EllipseCenter = new Vector2(195f, 112f);
        public const float EllipseRx = 112f;
        public const float EllipseRy = 82f;
        public const float ArmTopY = -420f;
        /// <summary>Arms start this far (along the track) from the bottom centre so they never overlap at the gate.</summary>
        public const float ArmStart = 14f;
        public const float RowSpacing = 22f;
        public const int LanesPerArm = 3;
        private static readonly float[] Lanes = { -21f, 0f, 21f };
        /// <summary>An eligible person may board once within this distance of their slot (not while still walking in).</summary>
        public const float ReadySlackPx = 24f;
        /// <summary>Boarding runners use the outer edge of their arm, outside lane +21.</summary>
        public const float OuterEdgePx = 40f;
        /// <summary>Eligible people hop this high (world units) at this rate.</summary>
        private const float HopHeight = 0.07f;
        private const float HopHz = 1.6f;

        [SerializeField] private PassengerView passengerPrefab;
        [SerializeField] private int maxVisible = 80;
        [SerializeField] private float walkSpeedPx = 85f;
        [SerializeField] private float boardSpeedPx = 420f;
        [SerializeField] private float minBoardTime = 0.25f;
        [Tooltip("Level intro: the crowd starts this far back and walks in (85 px = 1 s).")]
        [SerializeField] private float introWalkPx = 85f;
        [SerializeField] private float passengerHeight = 0.81f;
        [Tooltip("Model height of the Passenger prefab (for scaling to passengerHeight).")]
        [SerializeField] private float modelHeight = 0.779f;
        [Tooltip("Extra width (X/Z) so a standing person reads ~24 x 30 px on a 390x844 screen (the rig alone measures 17.8 x 29.4).")]
        [SerializeField] private float widthFactor = 1.35f;
        [Tooltip("Depth and height factors keep the projected height at ~30 px (depth shows at the 58 degree pitch).")]
        [SerializeField] private float depthFactor = 1.35f;
        [SerializeField] private float heightFactor = 0.8f;

        private sealed class Person
        {
            public GameColor color;
            public int row;
            public int arm;
            public int lane;
            public float s;
            public float target;
            public float lateral;
            public PassengerView view;
        }

        private sealed class Walker
        {
            public Person person;
            public List<Vector2> path;
            public List<float> lengths; // cumulative, computed once (no per-frame allocation)
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
        private readonly int[] columnAhead = new int[2 * LanesPerArm];

        public int QueueCount => queue.Count;
        public int WalkerCount => walkers.Count;
        public int PassengersLeft => queue.Count + walkers.Count;
        public bool HasFront => queue.Count > 0;
        public GameColor FrontColor => queue[0].color;

        /// <summary>Number of people currently in the boarding window.</summary>
        public int WindowCount => Mathf.Min(GameTuning.FrontWindow, queue.Count);

        public GameColor ColorAt(int queueIndex) => queue[queueIndex].color;

        /// <summary>In the window and standing at (or nearly at) their slot.</summary>
        public bool IsReady(int queueIndex) =>
            queueIndex < WindowCount && queue[queueIndex].s <= queue[queueIndex].target + ReadySlackPx;

        private readonly List<GameColor> windowColors = new List<GameColor>(GameTuning.FrontWindow);

        /// <summary>Colours of the people in the window, in queue order (shared list, no allocation).</summary>
        public IReadOnlyList<GameColor> WindowColors()
        {
            windowColors.Clear();
            for (int i = 0; i < WindowCount; i++) windowColors.Add(queue[i].color);
            return windowColors;
        }

        /// <summary>Queue index of the earliest ready person of this colour in the window, or -1.</summary>
        public int FindEligible(GameColor color)
        {
            int n = WindowCount;
            for (int i = 0; i < n; i++)
            {
                if (queue[i].color == color && IsReady(i)) return i;
            }
            return -1;
        }

        /// <summary>World position of the person at the front of the queue (the gate if nobody is visible).</summary>
        public Vector3 FrontWorldPosition =>
            queue.Count > 0 && queue[0].view != null ? queue[0].view.transform.position : ScreenRef.RefToWorld(Gate);

        /// <summary>Debug: the first n people in queue order as "colour(s px)".</summary>
        public string DescribeFront(int n)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < queue.Count && i < n; i++)
            {
                sb.Append(i == 0 ? "" : ", ").Append(i).Append(':').Append(queue[i].color).Append('(').Append(queue[i].s.ToString("F0")).Append(')');
            }
            return sb.ToString();
        }

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
                int row = n / LanesPerArm;
                var p = new Person { color = colors[n], row = row, arm = row % 2, lane = n % LanesPerArm };
                // Level intro: everyone starts a little further up the track and walks in (~1 s at walk speed).
                p.s = ArmStart + (row / 2) * RowSpacing + introWalkPx;
                p.lateral = TargetLateral(p);
                queue.Add(p);
            }
            for (int i = 0; i < queue.Count && i < maxVisible; i++) Spawn(queue[i], false);
            Update();
        }

        /// <summary>
        /// Sends the person at queue index i (must be <see cref="IsReady"/>) to a seat: out to the outer edge of their
        /// arm, along it to the gate, then to targetRef. Returns false if they are not ready.
        /// </summary>
        public bool Board(int queueIndex, Vector2 targetRef, Action onArrive)
        {
            if (!IsReady(queueIndex)) return false;
            Person p = queue[queueIndex];
            queue.RemoveAt(queueIndex);
            if (p.view == null) Spawn(p, false);

            float outer = OuterEdgePx * (p.arm == 0 ? 1f : -1f);
            var path = new List<Vector2> { ArmPoint(p.arm, p.s, p.lateral, out _) };
            for (float s = p.s; s > ArmStart; s -= 8f) path.Add(ArmPoint(p.arm, s, outer, out _));
            path.Add(ArmPoint(p.arm, ArmStart, outer, out _));
            path.Add(Gate);
            path.Add(targetRef);

            var lengths = new List<float>(path.Count) { 0f };
            for (int i = 1; i < path.Count; i++) lengths.Add(lengths[i - 1] + Vector2.Distance(path[i - 1], path[i]));
            float length = lengths[lengths.Count - 1];
            float speed = boardSpeedPx * GameTuning.PassengerBoardSpeed;
            walkers.Add(new Walker
            {
                person = p,
                path = path,
                lengths = lengths,
                length = length,
                duration = Mathf.Max(minBoardTime / GameTuning.PassengerBoardSpeed, length / speed),
                onArrive = onArrive
            });
            return true;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            float walk = walkSpeedPx * GameTuning.QueueShuffleSpeed;
            int window = WindowCount;

            for (int c = 0; c < columnAhead.Length; c++) columnAhead[c] = 0;
            for (int i = 0; i < queue.Count; i++)
            {
                Person p = queue[i];
                int column = p.arm * LanesPerArm + p.lane;
                p.target = ArmStart + columnAhead[column]++ * RowSpacing;
                float before = p.s;
                p.s = Mathf.MoveTowards(p.s, p.target, walk * dt);
                p.lateral = Mathf.MoveTowards(p.lateral, TargetLateral(p), walk * 0.5f * dt);

                if (p.view == null && i < maxVisible) Spawn(p, true);
                if (p.view == null) continue;

                Vector2 refPos = ArmPoint(p.arm, p.s, p.lateral, out Vector2 tangent);
                float moved = p.s - before;
                // Face the way they walk; standing people face the gate (down the arm).
                Vector2 facing = Mathf.Abs(moved) > 0.0001f ? tangent * Mathf.Sign(moved) : -tangent;
                // Eligible (first FrontWindow, at their slot): a small rhythmic hop, staggered by queue index.
                float hop = 0f;
                if (i < window && p.s <= p.target + ReadySlackPx)
                {
                    hop = HopHeight * Mathf.Abs(Mathf.Sin((Time.time * HopHz + i * 0.13f) * Mathf.PI));
                }
                Place(p.view, refPos, facing, dt, hop);
                p.view.Pose(dt, dt > 0f ? Mathf.Abs(moved) / dt : 0f, false);
            }

            for (int i = walkers.Count - 1; i >= 0; i--)
            {
                Walker w = walkers[i];
                w.time += dt;
                float k = Mathf.Clamp01(w.time / w.duration);
                Vector2 pos = PolylinePoint(w.path, k * w.length, out Vector2 dir, w.lengths);
                Place(w.person.view, pos, dir, dt);
                w.person.view.Pose(dt, boardSpeedPx * GameTuning.PassengerBoardSpeed, true);
                if (k >= 1f)
                {
                    walkers.RemoveAt(i);
                    Release(w.person);
                    w.onArrive?.Invoke();
                }
            }
        }

        // ------------------------------------------------------------------ track geometry (reference px)

        /// <summary>Arm 0 = right (round the right side, up x = 307), arm 1 = left (up x = 83). s = 0 at the bottom centre.</summary>
        private void BuildArms()
        {
            Vector2 bottom = new Vector2(EllipseCenter.x, EllipseCenter.y + EllipseRy);
            for (int arm = 0; arm < 2; arm++)
            {
                var pts = new List<Vector2> { bottom };
                float end = arm == 0 ? 0f : 180f;
                const int steps = 24;
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

            // People who join the visible crowd come down the alleys from just above the top edge.
            spawnS = ArmSAtY(0, -30f);
        }

        private float ArmSAtY(int arm, float y)
        {
            List<Vector2> pts = arms[arm];
            Vector2 top = pts[pts.Count - 2];
            return armLengths[arm][pts.Count - 2] + (top.y - y);
        }

        /// <summary>Layout formula for a full queue: queue index i -> 14 + (row/2)*22 with row = i/3.</summary>
        public static float TargetS(int queueIndex)
        {
            return ArmStart + (queueIndex / LanesPerArm / 2) * RowSpacing;
        }

        /// <summary>Lane offset perpendicular to the track; mirrored on the left arm so lane 0 is inside on both.</summary>
        private static float TargetLateral(Person p)
        {
            return Lanes[p.lane] * (p.arm == 0 ? 1f : -1f);
        }

        private Vector2 ArmPoint(int arm, float s, float lateral, out Vector2 tangent)
        {
            Vector2 c = PolylinePoint(arms[arm], s, out tangent, armLengths[arm]);
            // Offset along the track normal (mirroring between the arms is done in TargetLateral).
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

        private void Place(PassengerView view, Vector2 refPos, Vector2 refDir, float dt, float hop = 0f)
        {
            Vector3 world = ScreenRef.RefToWorld(refPos);
            view.transform.position = world + Vector3.up * hop;
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
            float k = passengerHeight / modelHeight;
            v.transform.localScale = new Vector3(k * widthFactor, k * heightFactor, k * depthFactor);
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
