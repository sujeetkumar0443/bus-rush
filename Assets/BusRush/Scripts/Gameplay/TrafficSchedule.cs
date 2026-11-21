using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BusRush.Gameplay
{
    /// <summary>
    /// Route geometry for the scheduled traffic: the waypoint polyline (world XZ, y = 0) with every corner rounded by a
    /// circular arc of one fixed radius, parameterised by arc length. Pure math (no scene).
    /// </summary>
    public sealed class RoutePath
    {
        private readonly List<Vector3> points = new List<Vector3>();
        private readonly List<float> distance = new List<float>();
        private readonly List<Vector3> tangents = new List<Vector3>();

        public float Length { get; }

        /// <summary>The first leg is vertical on screen (a vehicle leaving the lot straight up or down).</summary>
        public bool StartsVertical { get; }

        /// <summary>The route starts inside a bay slot (a departure).</summary>
        public bool StartsInSlot { get; }

        /// <summary>Identity of the geometry (waypoints + radius), for caching samples of identical routes.</summary>
        public string Key { get; }

        public RoutePath(IList<Vector3> waypoints, float cornerRadius)
        {
            var key = new System.Text.StringBuilder();
            key.Append(cornerRadius.ToString("F3"));
            foreach (Vector3 w in waypoints) key.Append('|').Append(w.x.ToString("F3")).Append(',').Append(w.z.ToString("F3"));
            Key = key.ToString();
            StartsVertical = waypoints.Count > 1 &&
                             Mathf.Abs(ScreenRef.WorldToRef(waypoints[0]).x - ScreenRef.WorldToRef(waypoints[1]).x) < 0.5f;
            float startY = ScreenRef.WorldToRef(waypoints[0]).y;
            StartsInSlot = startY >= ScreenRef.SlotTopY && startY <= ScreenRef.SlotBottomY;
            points.Add(Flat(waypoints[0]));
            for (int i = 1; i < waypoints.Count - 1; i++)
            {
                Vector3 a = Flat(waypoints[i - 1]), p = Flat(waypoints[i]), b = Flat(waypoints[i + 1]);
                Vector3 d1 = (p - a).normalized, d2 = (b - p).normalized;
                float cos = Mathf.Clamp(Vector3.Dot(d1, d2), -1f, 1f);
                float turn = Mathf.Acos(cos);                         // deflection angle
                if (turn < 0.01f || (p - a).sqrMagnitude < 1e-8f || (b - p).sqrMagnitude < 1e-8f)
                {
                    points.Add(p);
                    continue;
                }
                float tanHalf = Mathf.Tan(turn * 0.5f);
                float t = Mathf.Min(cornerRadius * tanHalf, Vector3.Distance(a, p) * 0.5f, Vector3.Distance(p, b) * 0.5f);
                float r = t / tanHalf;
                Vector3 p1 = p - d1 * t;
                float side = Mathf.Sign(d1.x * d2.z - d1.z * d2.x);   // +1 = left turn (counter-clockwise in XZ)
                var normal = new Vector3(-d1.z, 0f, d1.x) * side;      // towards the arc centre
                Vector3 centre = p1 + normal * r;
                Vector3 from = p1 - centre;
                int steps = Mathf.Max(4, Mathf.CeilToInt(turn / (Mathf.PI / 24f)));
                for (int k = 0; k <= steps; k++)
                {
                    float ang = side * turn * k / steps;
                    points.Add(centre + Quaternion.AngleAxis(-ang * Mathf.Rad2Deg, Vector3.up) * from);
                }
            }
            points.Add(Flat(waypoints[waypoints.Count - 1]));

            // Drop duplicates, then cumulative distance and per-vertex tangents (interpolated along segments).
            for (int i = points.Count - 1; i > 0; i--)
            {
                if ((points[i] - points[i - 1]).sqrMagnitude < 1e-10f) points.RemoveAt(i);
            }
            distance.Add(0f);
            for (int i = 1; i < points.Count; i++) distance.Add(distance[i - 1] + Vector3.Distance(points[i - 1], points[i]));
            Length = distance[distance.Count - 1];
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 prev = i > 0 ? (points[i] - points[i - 1]).normalized : Vector3.zero;
                Vector3 next = i + 1 < points.Count ? (points[i + 1] - points[i]).normalized : Vector3.zero;
                Vector3 tg = prev + next;
                tangents.Add(tg.sqrMagnitude > 1e-8f ? tg.normalized : Vector3.forward);
            }
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>Position and unit tangent at arc length s; beyond the ends it extends along the end tangents.</summary>
        public void Sample(float s, out Vector3 position, out Vector3 tangent)
        {
            if (points.Count == 1)
            {
                position = points[0];
                tangent = Vector3.forward;
                return;
            }
            if (s <= 0f)
            {
                tangent = (points[1] - points[0]).normalized;
                position = points[0] + tangent * s;
                return;
            }
            if (s >= Length)
            {
                tangent = (points[points.Count - 1] - points[points.Count - 2]).normalized;
                position = points[points.Count - 1] + tangent * (s - Length);
                return;
            }
            int lo = 0, hi = distance.Count - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (distance[mid] <= s) lo = mid; else hi = mid;
            }
            float seg = distance[hi] - distance[lo];
            float k = seg > 0f ? (s - distance[lo]) / seg : 0f;
            position = Vector3.Lerp(points[lo], points[hi], k);
            tangent = Vector3.Slerp(tangents[lo], tangents[hi], k).normalized;
        }

        public Vector3 Start => points[0];
        public Vector3 End => points[points.Count - 1];
    }

    /// <summary>
    /// One vehicle's fixed trajectory: it stands at the route start from <see cref="WaitFrom"/> until <see cref="Start"/>,
    /// then drives the whole route at the constant road speed (0.15 s ramp up from rest, and down into a bay) without
    /// ever stopping, and (if it parks) stands at the end until <see cref="HoldUntil"/>. Pure math (no scene).
    /// </summary>
    public sealed class VehiclePlan
    {
        public readonly int Id;
        public readonly RoutePath Path;
        public readonly float HalfLength;
        public readonly float HalfWidth;
        public readonly float Speed;       // world units / s
        public readonly float RampUp;      // s
        public readonly float RampDown;    // s (0 = drives off at full speed)
        public readonly float Duration;    // moving time, s

        public float WaitFrom;             // tapped / ready: stands at the start from here
        public float Start;                // starts moving (scheduled)
        public float HoldUntil;            // parked at the end until (infinity until it departs; End for drive-offs)

        public float End => Start + Duration;

        private readonly float rampUpDist, rampDownDist, cruiseTime;

        public VehiclePlan(int id, RoutePath path, float halfLength, float halfWidth, float speed, float rampUp, float rampDown,
                           bool parksAtEnd, float waitFrom)
        {
            Id = id;
            Path = path;
            HalfLength = halfLength;
            HalfWidth = halfWidth;
            Speed = speed;
            float L = path.Length;
            // Short routes: shrink the ramps so they never cover more than the route.
            float rampDist = speed * (rampUp + rampDown) * 0.5f;
            float shrink = rampDist > L && rampDist > 0f ? L / rampDist : 1f;
            RampUp = rampUp * shrink;
            RampDown = rampDown * shrink;
            rampUpDist = speed * RampUp * 0.5f;
            rampDownDist = speed * RampDown * 0.5f;
            cruiseTime = Mathf.Max(0f, (L - rampUpDist - rampDownDist) / speed);
            Duration = RampUp + cruiseTime + RampDown;
            WaitFrom = waitFrom;
            Start = waitFrom;
            HoldUntil = parksAtEnd ? float.PositiveInfinity : float.NegativeInfinity;
        }

        /// <summary>Distance driven tau seconds after the start (clamped to [0, Length]).</summary>
        public float DistanceAtRelative(float tau)
        {
            if (tau <= 0f) return 0f;
            if (tau >= Duration) return Path.Length;
            if (tau < RampUp) return 0.5f * Speed / RampUp * tau * tau;
            if (tau < RampUp + cruiseTime) return rampUpDist + Speed * (tau - RampUp);
            float left = Duration - tau;
            return Path.Length - 0.5f * Speed / Mathf.Max(1e-6f, RampDown) * left * left;
        }

        /// <summary>Seconds after the start at which the vehicle has driven s (inverse of DistanceAtRelative).</summary>
        public float RelativeTimeAt(float s)
        {
            if (s <= 0f) return 0f;
            if (s >= Path.Length) return Duration;
            if (s < rampUpDist) return Mathf.Sqrt(2f * s * RampUp / Speed);
            if (s <= Path.Length - rampDownDist) return RampUp + (s - rampUpDist) / Speed;
            float left = Path.Length - s;
            return Duration - Mathf.Sqrt(2f * left * Mathf.Max(1e-6f, RampDown) / Speed);
        }

        public float DistanceAt(float time) => DistanceAtRelative(time - Start);

        /// <summary>True if the vehicle is on screen at this time (waiting, driving, or parked at the end).</summary>
        public bool Present(float time) => time >= WaitFrom && (time <= End || time <= HoldUntil);

        public void PoseAt(float time, out Vector3 centre, out Vector3 forward) => PoseAtDistance(DistanceAt(time), out centre, out forward);

        /// <summary>Reference px over which the lot-exit chord blends out after the tail has left the lot.</summary>
        private const float ChordBlendPx = 20f;

        /// <summary>
        /// Body pose at arc length s. Normally centred on the path, facing along it. Both ends on the path with the body
        /// on the chord between them (so the tail follows the path instead of swinging sideways) in two places:
        /// <list type="bullet">
        /// <item>Turning up into a bay (front between the departure strip and the arrival lane, tail on the arrival lane):
        /// the tail stays on the lane instead of swinging into the lot's top row.</item>
        /// <item>Pulling out of a bay, while the tail is still in its slot: the tail rises straight up out of the slot
        /// instead of swinging into the next bay; the inside of the turn onto the strip is road.</item>
        /// <item>Leaving the lot straight up or down, while the tail is still in the lot (blending out over ChordBlendPx):
        /// the tail stays in its own column instead of swinging into the neighbour's; the inside of that turn is road.</item>
        /// </list>
        /// Side exits stay centred (the chord would cut into the row above). Every switch happens while the vehicle is
        /// straight or blends, so nothing jumps.
        /// </summary>
        public void PoseAtDistance(float s, out Vector3 centre, out Vector3 forward)
        {
            Path.Sample(Mathf.Clamp(s, 0f, Path.Length), out Vector3 onPath, out Vector3 tangent);
            Path.Sample(s + HalfLength, out Vector3 front, out _);
            Path.Sample(s - HalfLength, out Vector3 rear, out _);
            Vector2 r = ScreenRef.WorldToRef(rear);
            float frontY = ScreenRef.WorldToRef(front).y;

            float k = 0f;
            if (frontY < ScreenRef.TopY - 2f && frontY > ScreenRef.DepartY + 2f && Mathf.Abs(r.y - ScreenRef.TopY) < 2f) k = 1f;
            else if (Path.StartsInSlot) k = 1f - Mathf.Clamp01((ScreenRef.SlotTopY - r.y) / ChordBlendPx);
            else if (Path.StartsVertical) k = 1f - Mathf.Clamp01(OutsideLotPx(r) / ChordBlendPx);
            if (k <= 0f)
            {
                centre = onPath;
                forward = tangent;
                return;
            }
            Vector3 chord = front - rear;
            centre = Vector3.Lerp(onPath, (rear + front) * 0.5f, k);
            forward = chord.sqrMagnitude > 1e-8f ? Vector3.Slerp(tangent, chord.normalized, k).normalized : tangent;
        }

        /// <summary>How far (reference px) a point is outside the lot rectangle (0 inside).</summary>
        private static float OutsideLotPx(Vector2 r)
        {
            float left = BoardGeometry.LotLeftPx, top = BoardGeometry.LotTopPx;
            float right = left + BoardGeometry.Columns * BoardGeometry.PixelsPerCell;
            float bottom = top + BoardGeometry.Rows * BoardGeometry.PixelsPerCell * BoardGeometry.SinPitch;
            float dx = Mathf.Max(0f, Mathf.Max(left - r.x, r.x - right));
            float dy = Mathf.Max(0f, Mathf.Max(top - r.y, r.y - bottom));
            return Mathf.Max(dx, dy);
        }
    }

    /// <summary>
    /// Wait-free traffic: a reservation table of vehicle trajectories in space and time. A new plan may only choose its
    /// start time: the earliest one (not before it is ready) at which its whole trajectory, footprint plus margin, never
    /// overlaps any plan already in the table (moving, waiting at its start, or parked at its end). Plans are added in
    /// tap / ready order, so a later vehicle only ever waits for earlier ones (no cycles), and once a vehicle starts it
    /// never waits again. The same rule covers the lot exits, the merge at the corners, the arrival lane in both
    /// directions, the bay entries and the departure strip. Pure math (no scene).
    /// </summary>
    public sealed class TrafficSchedule
    {
        /// <summary>Arc-length step of the occupancy samples (world units).</summary>
        public const float SampleStep = 0.3f;
        /// <summary>Give up searching after this much delay (a permanent conflict, e.g. a route through a parked car).</summary>
        public const float MaxDelay = 30f;
        /// <summary>Samples per chunk; chunks carry a bounding circle so far-apart parts of two routes are skipped.</summary>
        private const int ChunkSize = 8;
        /// <summary>Subdivide wherever the heading turns more than this between samples (corners).</summary>
        private const float MaxTurnPerSampleDeg = 8f;

        private struct Occ
        {
            public Vector2 c, f;
            public float hl, hw;
            public float t0, t1; // relative to the plan's start
        }

        private struct Chunk
        {
            public int first, count;
            public Vector2 centre;
            public float radius;  // covers every sample footprint in the chunk
            public float t0, t1;  // time window of the chunk, relative to the plan's start
        }

        private sealed class Samples
        {
            public Occ atStart, atEnd;     // stationary footprints (waiting / parked)
            public float stationaryRadius;
            public readonly List<Occ> moving = new List<Occ>();
            public readonly List<Chunk> chunks = new List<Chunk>();
        }

        private sealed class Entry
        {
            public VehiclePlan plan;
            public Samples samples;
        }

        /// <summary>Occupancy samples depend only on the route and the speed profile, not on the start time: shared.</summary>
        private static readonly Dictionary<string, Samples> SampleCache = new Dictionary<string, Samples>();

        private readonly List<Entry> entries = new List<Entry>();
        private readonly List<Vector2> forbidden = new List<Vector2>();
        private readonly float marginLong, marginSide;
        private int version;
        private float forbidFrom;

        // Dry-run result, reused when the same plan is committed right after (nothing changed in between).
        private VehiclePlan dryPlan;
        private float dryEarliest, dryStart;
        private int dryVersion;
        private bool dryOk;
        private Entry dryEntry;

        /// <param name="marginLong">Extra clearance kept at each end of a vehicle (world units).</param>
        /// <param name="marginSide">Extra clearance kept at each side (world units).</param>
        public TrafficSchedule(float marginLong, float marginSide)
        {
            this.marginLong = marginLong;
            this.marginSide = marginSide;
        }

        public int Count => entries.Count;

        /// <summary>
        /// Schedules the plan: sets <see cref="VehiclePlan.Start"/> to the earliest conflict-free time at or after
        /// <paramref name="earliest"/> and adds it to the table. Returns false if no start within MaxDelay is free (the
        /// plan is then added at the latest tried start; callers avoid this with <see cref="CanSchedule"/>).
        /// </summary>
        public bool Schedule(VehiclePlan plan, float earliest) => Schedule(plan, earliest, true);

        /// <summary>Dry run: would the plan get a conflict-free start within MaxDelay? (Sets plan.Start, adds nothing.)</summary>
        public bool CanSchedule(VehiclePlan plan, float earliest) => Schedule(plan, earliest, false);

        private bool Schedule(VehiclePlan plan, float earliest, bool commit)
        {
            Prune(earliest);
            if (commit && ReferenceEquals(plan, dryPlan) && dryVersion == version && Mathf.Approximately(dryEarliest, earliest) &&
                Mathf.Approximately(plan.WaitFrom, Mathf.Min(plan.WaitFrom, earliest)))
            {
                plan.Start = dryStart;
                entries.Add(dryEntry);
                version++;
                dryPlan = null;
                return dryOk;
            }

            var e = new Entry { plan = plan, samples = SamplesFor(plan) };
            Samples mine = e.samples;
            forbidden.Clear();
            sources.Clear();
            float from = Mathf.Max(earliest, plan.WaitFrom);
            forbidFrom = from;
            foreach (Entry other in entries)
            {
                if (other.plan.Id == plan.Id) continue;
                VehiclePlan op = other.plan;
                Samples theirs = other.samples;
                bool movingMatters = op.End > from;   // it can only meet us while we drive (from onwards)
                bool startMatters = op.Start > from && op.Start > op.WaitFrom;
                bool endMatters = op.HoldUntil > op.End && op.HoldUntil > from;

                foreach (Chunk ca in mine.chunks)
                {
                    if (movingMatters)
                    {
                        foreach (Chunk cb in theirs.chunks)
                        {
                            // Every conflict from this pair would forbid starts before op.Start + cb.t1 - ca.t0: if that is
                            // not after 'from', they cannot meet (it has passed by the time we could get there).
                            if (op.Start + cb.t1 - ca.t0 <= from) continue;
                            if ((ca.centre - cb.centre).sqrMagnitude > Sq(ca.radius + cb.radius)) continue;
                            for (int i = ca.first; i < ca.first + ca.count; i++)
                            {
                                Occ a = mine.moving[i];
                                for (int j = cb.first; j < cb.first + cb.count; j++)
                                {
                                    Occ b = theirs.moving[j];
                                    if (op.Start + b.t1 - a.t0 <= from) continue;   // passed before we could get there
                                    if (!Overlap(a, b)) continue;
                                    Forbid(op.Start + b.t0 - a.t1, op.Start + b.t1 - a.t0, op.Id, "driving");
                                }
                            }
                        }
                    }
                    // The other vehicle standing at its start (waiting) or at its end (parked).
                    if (startMatters && op.Start - ca.t0 > from &&
                        (ca.centre - theirs.atStart.c).sqrMagnitude <= Sq(ca.radius + theirs.stationaryRadius))
                    {
                        for (int i = ca.first; i < ca.first + ca.count; i++)
                        {
                            Occ a = mine.moving[i];
                            if (Overlap(a, theirs.atStart)) Forbid(op.WaitFrom - a.t1, op.Start - a.t0, op.Id, "waiting");
                        }
                    }
                    if (endMatters && op.HoldUntil - ca.t0 > from &&
                        (ca.centre - theirs.atEnd.c).sqrMagnitude <= Sq(ca.radius + theirs.stationaryRadius))
                    {
                        for (int i = ca.first; i < ca.first + ca.count; i++)
                        {
                            Occ a = mine.moving[i];
                            if (Overlap(a, theirs.atEnd)) Forbid(op.End - a.t1, op.HoldUntil - a.t0, op.Id, "parked");
                        }
                    }
                }
            }

            float start = FirstFree(from, out bool ok);
            plan.Start = start;
            if (commit)
            {
                entries.Add(e);
                version++;
                dryPlan = null;
            }
            else
            {
                dryPlan = plan;
                dryEarliest = earliest;
                dryStart = start;
                dryOk = ok;
                dryVersion = version;
                dryEntry = e;
            }
            return ok;
        }

        /// <summary>The plan no longer occupies anything (destroyed / cancelled).</summary>
        public void Remove(int id)
        {
            if (entries.RemoveAll(x => x.plan.Id == id) > 0) version++;
        }

        /// <summary>Removes plans that are completely in the past (they cannot conflict with anything from now on).</summary>
        private void Prune(float now)
        {
            entries.RemoveAll(x => x.plan.End < now && x.plan.HoldUntil < now);
        }

        private void Forbid(float lo, float hi, int id, string kind)
        {
            if (hi <= lo || hi <= forbidFrom) return;
            forbidden.Add(new Vector2(lo, hi));
            sources.Add((lo, hi, id, kind));
        }

        /// <summary>Why the last Schedule call failed (the blocking plan), for logs.</summary>
        public string LastFailure { get; private set; } = "";
        private readonly List<(float lo, float hi, int id, string kind)> sources = new List<(float, float, int, string)>();

        /// <summary>Smallest start >= from not inside any forbidden (open) interval.</summary>
        private float FirstFree(float from, out bool ok)
        {
            forbidden.Sort((x, y) => x.x.CompareTo(y.x));
            float d = from;
            foreach (Vector2 iv in forbidden) // sorted by start: one pass, d only moves forward
            {
                if (iv.x < d && d < iv.y) d = iv.y;
                else if (iv.x >= d && Mathf.Approximately(iv.x, d) && iv.y > d) d = iv.y;
                if (d - from > MaxDelay || float.IsInfinity(d)) break;
            }
            if (d - from > MaxDelay || float.IsInfinity(d))
            {
                ok = false;
                var worst = sources.OrderByDescending(x => x.hi).First();
                LastFailure = $"blocked by plan {worst.id} ({worst.kind}) until {worst.hi:F1}";
                return from + MaxDelay;
            }
            ok = true;
            return d;
        }

        private Samples SamplesFor(VehiclePlan p)
        {
            string key = $"{p.Path.Key}#{p.HalfLength:F3},{p.HalfWidth:F3},{p.Speed:F3},{p.RampUp:F3},{p.RampDown:F3},{marginLong:F3},{marginSide:F3}";
            if (SampleCache.TryGetValue(key, out Samples cached)) return cached;
            if (SampleCache.Count > 4000) SampleCache.Clear();
            Samples smp = BuildSamples(p);
            SampleCache[key] = smp;
            return smp;
        }

        /// <summary>
        /// Footprints along the route at SampleStep, subdivided in corners (heading change at most MaxTurnPerSampleDeg),
        /// each inflated along its length by half the gap to its neighbours and valid over the matching time window.
        /// </summary>
        private Samples BuildSamples(VehiclePlan p)
        {
            var smp = new Samples();
            float L = p.Path.Length;
            smp.atStart = Stationary(p, 0f);
            smp.atEnd = Stationary(p, L);
            smp.stationaryRadius = smp.atStart.hl + smp.atStart.hw;

            // Arc-length positions: regular steps, halved until the heading change between neighbours is small.
            var at = new List<float> { 0f };
            var poses = new List<(Vector3 c, Vector3 f)>();
            p.PoseAtDistance(0f, out Vector3 c0, out Vector3 f0);
            poses.Add((c0, f0));
            float sNow = 0f;
            while (sNow < L)
            {
                float step = Mathf.Min(SampleStep, L - sNow);
                Vector3 c1, f1;
                while (true)
                {
                    p.PoseAtDistance(sNow + step, out c1, out f1);
                    if (step <= 0.02f || Vector3.Angle(poses[poses.Count - 1].f, f1) <= MaxTurnPerSampleDeg) break;
                    step *= 0.5f;
                }
                sNow += step;
                at.Add(sNow);
                poses.Add((c1, f1));
            }

            for (int i = 0; i < at.Count; i++)
            {
                float lo = i > 0 ? (at[i - 1] + at[i]) * 0.5f : 0f;
                float hi = i + 1 < at.Count ? (at[i] + at[i + 1]) * 0.5f : L;
                float inflate = Mathf.Max(at[i] - lo, hi - at[i]);
                smp.moving.Add(new Occ
                {
                    c = new Vector2(poses[i].c.x, poses[i].c.z),
                    f = new Vector2(poses[i].f.x, poses[i].f.z).normalized,
                    hl = p.HalfLength + marginLong + inflate,
                    hw = p.HalfWidth + marginSide,
                    t0 = p.RelativeTimeAt(lo),
                    t1 = p.RelativeTimeAt(hi)
                });
            }

            for (int first = 0; first < smp.moving.Count; first += ChunkSize)
            {
                int count = Mathf.Min(ChunkSize, smp.moving.Count - first);
                Vector2 centre = Vector2.zero;
                for (int i = first; i < first + count; i++) centre += smp.moving[i].c;
                centre /= count;
                float radius = 0f, t0 = float.MaxValue, t1 = float.MinValue;
                for (int i = first; i < first + count; i++)
                {
                    Occ o = smp.moving[i];
                    radius = Mathf.Max(radius, (o.c - centre).magnitude + o.hl + o.hw);
                    t0 = Mathf.Min(t0, o.t0);
                    t1 = Mathf.Max(t1, o.t1);
                }
                smp.chunks.Add(new Chunk { first = first, count = count, centre = centre, radius = radius, t0 = t0, t1 = t1 });
            }
            return smp;
        }

        private Occ Stationary(VehiclePlan p, float s)
        {
            p.PoseAtDistance(s, out Vector3 c, out Vector3 f);
            return new Occ { c = new Vector2(c.x, c.z), f = new Vector2(f.x, f.z).normalized, hl = p.HalfLength + marginLong, hw = p.HalfWidth + marginSide };
        }

        private static float Sq(float v) => v * v;

        private static bool Overlap(in Occ a, in Occ b)
        {
            float dx = b.c.x - a.c.x, dy = b.c.y - a.c.y;
            float reach = a.hl + a.hw + b.hl + b.hw;
            if (dx * dx + dy * dy > reach * reach) return false;
            return SatOverlap(dx, dy, a.f.x, a.f.y, a.hl, a.hw, b.f.x, b.f.y, b.hl, b.hw);
        }

        /// <summary>Separating-axis test in plain floats: d = c2 - c1, f = unit forward, right = (f.y, -f.x).</summary>
        private static bool SatOverlap(float dx, float dy, float f1x, float f1y, float hl1, float hw1,
                                       float f2x, float f2y, float hl2, float hw2)
        {
            // Projections of each box onto the other's axes; |f1.f2| and |f1.r2| (= |r1.f2|) are shared.
            float ff = Mathf.Abs(f1x * f2x + f1y * f2y);
            float fr = Mathf.Abs(f1x * f2y - f1y * f2x);
            // axis f1
            if (Mathf.Abs(dx * f1x + dy * f1y) >= hl1 + hl2 * ff + hw2 * fr) return false;
            // axis r1 = (f1y, -f1x)
            if (Mathf.Abs(dx * f1y - dy * f1x) >= hw1 + hl2 * fr + hw2 * ff) return false;
            // axis f2
            if (Mathf.Abs(dx * f2x + dy * f2y) >= hl2 + hl1 * ff + hw1 * fr) return false;
            // axis r2 = (f2y, -f2x)
            if (Mathf.Abs(dx * f2y - dy * f2x) >= hw2 + hl1 * fr + hw1 * ff) return false;
            return true;
        }

        /// <summary>Separating-axis test for two oriented rectangles (centre, unit forward, half length, half width).</summary>
        public static bool ObbOverlap(Vector2 c1, Vector2 f1, float hl1, float hw1, Vector2 c2, Vector2 f2, float hl2, float hw2)
        {
            var r1 = new Vector2(f1.y, -f1.x);
            var r2 = new Vector2(f2.y, -f2.x);
            Vector2 d = c2 - c1;
            return Axis(f1) && Axis(r1) && Axis(f2) && Axis(r2);

            bool Axis(Vector2 axis)
            {
                float p1 = hl1 * Mathf.Abs(Vector2.Dot(f1, axis)) + hw1 * Mathf.Abs(Vector2.Dot(r1, axis));
                float p2 = hl2 * Mathf.Abs(Vector2.Dot(f2, axis)) + hw2 * Mathf.Abs(Vector2.Dot(r2, axis));
                return Mathf.Abs(Vector2.Dot(d, axis)) < p1 + p2;
            }
        }

        // ------------------------------------------------------------------ plan factory (game geometry)

        public static float VehicleHalfLength(VehicleType type) =>
            VehicleSpecs.Length(type) * BoardGeometry.CellSize * ScreenRef.VehicleVisualScale * 0.5f;

        public const float VehicleHalfWidth = 0.40f; // the model is 0.86 cells wide with rounded edges

        /// <summary>Arrival: from the lot cell, out of the lot and round the road into the bay; parks at the end.</summary>
        public static VehiclePlan ArrivalPlan(int id, VehicleData v, int bayIndex, float now) =>
            new VehiclePlan(id, new RoutePath(RouteBuilder.ToWorld(RouteBuilder.Build(v, bayIndex)), CornerRadius),
                            VehicleHalfLength(v.type), VehicleHalfWidth, RoadSpeed, GameTuning.LotAccelSeconds,
                            GameTuning.LotAccelSeconds, true, now);

        /// <summary>Departure: forward out of the slot onto the strip above the bays and off to the right.</summary>
        public static VehiclePlan DeparturePlan(int id, VehicleType type, int bayIndex, float now) =>
            new VehiclePlan(id, new RoutePath(RouteBuilder.ToWorld(RouteBuilder.BuildDeparture(type, bayIndex)), CornerRadius),
                            VehicleHalfLength(type), VehicleHalfWidth, RoadSpeed, GameTuning.LotAccelSeconds, 0f, false, now);

        public static float RoadSpeed => GameTuning.RoadSpeedPx * ScreenRef.UnitsPerPx;
        public static float CornerRadius => GameTuning.CornerRadiusPx * ScreenRef.UnitsPerPx;

        /// <summary>The game's schedule: margin GameTuning.GapMarginPx in total along the road, a hair at the sides.</summary>
        public static TrafficSchedule CreateForGame() =>
            new TrafficSchedule(GameTuning.GapMarginPx * 0.5f * ScreenRef.UnitsPerPx, 0.03f);
    }
}
