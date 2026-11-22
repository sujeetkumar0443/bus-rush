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
        public const float ArmStart = GameTuning.CrowdArmStartPx;
        public const float RowSpacing = GameTuning.CrowdRowSpacingPx;
        public const int LanesPerArm = 3;
        private static readonly float[] Lanes = { -GameTuning.CrowdLaneSpacingPx, 0f, GameTuning.CrowdLaneSpacingPx };
        /// <summary>An eligible person may board once within this distance of their slot (not while still walking in).</summary>
        public const float ReadySlackPx = 24f;
        /// <summary>Boarding runners use the outer edge of their arm, outside the outer lane.</summary>
        public const float OuterEdgePx = GameTuning.CrowdOuterEdgePx;
        /// <summary>Eligible people hop this high (world units) at this rate.</summary>
        private const float HopHeight = 0.07f;
        private const float HopHz = 1.6f;

        [SerializeField] private PassengerView passengerPrefab;
        private const int maxVisible = GameTuning.CrowdMaxVisible;
        private const float MinBoardTime = 0.15f;
        [Tooltip("Level intro: the crowd starts this far back and walks in (at the shuffle speed, ~0.7 s).")]
        [SerializeField] private float introWalkPx = 85f;

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
            public Vector2 moveFrom;   // Arrange: animate from here to the slot
            public float moveTime;     // seconds left of that animation (0 = none)
            public float moveDuration;
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

        /// <summary>Boarded passengers finishing their squash-and-hop into the vehicle before going back to the pool.</summary>
        private struct Hopper
        {
            public PassengerView view;
            public float time;
            public Vector3 basePos;
            public Vector3 baseScale;
        }

        private readonly List<Hopper> hoppers = new List<Hopper>(16);
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

        /// <summary>Colours of everyone waiting (front first; not those already walking to a vehicle).</summary>
        public List<GameColor> QueueColors()
        {
            var list = new List<GameColor>(queue.Count);
            foreach (Person p in queue) list.Add(p.color);
            return list;
        }

        /// <summary>
        /// Arrange booster: reorders the waiting passengers to <paramref name="colors"/> (same passengers, new order).
        /// Slots (row, arm, lane, distance) stay where they are; each passenger takes over the slot of the queue index
        /// they move to and animates there in <paramref name="duration"/>. Passengers of one colour keep their relative
        /// order. Nobody already walking to a vehicle is touched.
        /// </summary>
        public void Rearrange(IReadOnlyList<GameColor> colors, float duration)
        {
            if (colors.Count != queue.Count) return;
            var byColour = new Dictionary<GameColor, Queue<Person>>();
            foreach (Person p in queue)
            {
                if (!byColour.TryGetValue(p.color, out Queue<Person> q)) byColour[p.color] = q = new Queue<Person>();
                q.Enqueue(p);
            }
            var slots = new List<(int row, int arm, int lane, float s, float lateral, float target)>(queue.Count);
            foreach (Person p in queue) slots.Add((p.row, p.arm, p.lane, p.s, p.lateral, p.target));
            var current = new List<Vector2>(queue.Count);
            foreach (Person p in queue) current.Add(ArmPoint(p.arm, p.s, p.lateral, out _));

            var next = new List<Person>(queue.Count);
            for (int k = 0; k < colors.Count; k++)
            {
                if (!byColour.TryGetValue(colors[k], out Queue<Person> q) || q.Count == 0) return; // not the same multiset
                next.Add(q.Dequeue());
            }
            var from = new Dictionary<Person, Vector2>();
            for (int k = 0; k < queue.Count; k++) from[queue[k]] = current[k];

            for (int k = 0; k < next.Count; k++)
            {
                Person p = next[k];
                var slot = slots[k];
                bool moves = p.row != slot.row || p.arm != slot.arm || p.lane != slot.lane;
                p.row = slot.row;
                p.arm = slot.arm;
                p.lane = slot.lane;
                p.s = slot.s;
                p.lateral = slot.lateral;
                p.target = slot.target;
                if (moves && p.view != null)
                {
                    p.moveFrom = from[p];
                    p.moveTime = duration;
                    p.moveDuration = duration;
                }
            }
            queue.Clear();
            queue.AddRange(next);
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
            foreach (Hopper h in hoppers) ReleaseView(h.view);
            queue.Clear();
            walkers.Clear();
            hoppers.Clear();

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
            float speed = GameTuning.PassengerRunSpeedPx;
            walkers.Add(new Walker
            {
                person = p,
                path = path,
                lengths = lengths,
                length = length,
                duration = Mathf.Max(MinBoardTime, length / speed),
                onArrive = onArrive
            });
            return true;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            float walk = GameTuning.QueueShuffleSpeedPx;
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
                if (p.moveTime > 0f)
                {
                    // Arrange: glide from the old spot to the new slot (ease out).
                    p.moveTime = Mathf.Max(0f, p.moveTime - dt);
                    float u = 1f - p.moveTime / Mathf.Max(0.0001f, p.moveDuration);
                    refPos = Vector2.Lerp(p.moveFrom, refPos, 1f - (1f - u) * (1f - u));
                }
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
                w.person.view.Pose(dt, GameTuning.PassengerRunSpeedPx, true);
                if (k >= 1f)
                {
                    walkers.RemoveAt(i);
                    // Seated now (gameplay timing unchanged); the view squashes and hops into the vehicle, then pools.
                    PassengerView view = w.person.view;
                    w.person.view = null;
                    hoppers.Add(new Hopper { view = view, time = 0f, basePos = view.transform.position, baseScale = view.transform.localScale });
                    w.onArrive?.Invoke();
                }
            }

            // Squash (first 35%), then hop up and shrink into the vehicle.
            for (int i = hoppers.Count - 1; i >= 0; i--)
            {
                Hopper h = hoppers[i];
                h.time += dt;
                float k = Mathf.Clamp01(h.time / GameTuning.BoardHopSeconds);
                Vector3 s = h.baseScale;
                if (k < 0.35f)
                {
                    float q = k / 0.35f;
                    h.view.transform.localScale = new Vector3(s.x * (1f + 0.18f * q), s.y * (1f - 0.3f * q), s.z * (1f + 0.18f * q));
                    h.view.transform.position = h.basePos;
                }
                else
                {
                    float q = (k - 0.35f) / 0.65f;
                    float shrink = 1f - q;
                    h.view.transform.localScale = new Vector3(s.x * shrink, s.y * (0.7f + 0.5f * Mathf.Sin(q * Mathf.PI)) * shrink, s.z * shrink);
                    h.view.transform.position = h.basePos + Vector3.up * (0.45f * Mathf.Sin(q * Mathf.PI));
                }
                if (k >= 1f)
                {
                    hoppers.RemoveAt(i);
                    ReleaseView(h.view);
                    continue;
                }
                hoppers[i] = h;
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
            v.transform.localScale = PassengerScale(v);
            v.SetColor(p.color);
            v.ResetPose();
            if (walkIn) p.s = Mathf.Max(p.s, spawnS); // appear above the top edge and walk in, never pop in view
            p.view = v;
            Vector2 refPos = ArmPoint(p.arm, p.s, p.lateral, out Vector2 t);
            v.transform.SetPositionAndRotation(ScreenRef.RefToWorld(refPos),
                Quaternion.LookRotation(ScreenRef.RefToWorld(refPos - t) - ScreenRef.RefToWorld(refPos), Vector3.up));
        }

        /// <summary>Uniform scale that makes the model GameTuning.PassengerWidthPx wide, stretched to the target height.</summary>
        public static Vector3 PassengerScale(PassengerView v)
        {
            float k = GameTuning.PassengerWidthPx * ScreenRef.UnitsPerPx / Mathf.Max(0.01f, v.ModelWidth);
            return new Vector3(k, k * GameTuning.PassengerHeightScale, k * GameTuning.PassengerDepthScale);
        }

        private void Release(Person p)
        {
            if (p.view == null) return;
            ReleaseView(p.view);
            p.view = null;
        }

        private void ReleaseView(PassengerView v)
        {
            v.gameObject.SetActive(false);
            pool.Push(v);
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
