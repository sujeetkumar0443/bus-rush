using System.Collections.Generic;
using System.Linq;
using System.Text;
using BusRush.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace BusRush.Tests
{
    /// <summary>
    /// Edit-mode tests for the wait-free traffic schedule: pure-math simulation of real generated boards and real routes
    /// (no scene, no play mode).
    /// </summary>
    public sealed class TrafficScheduleTests
    {
        private static readonly int[] OpenBays = { 1, 2, 3, 4 };
        private static List<LevelData> levels;

        private sealed class LevelGeometry
        {
            public (Vector2 c, Vector2 f, float hl, float hw)[] boardFoot;
            public bool[] topLeft;
            public bool[,] turnOk; // [vehicle, bay]
        }

        private static readonly Dictionary<LevelData, LevelGeometry> geometry = new Dictionary<LevelData, LevelGeometry>();
        private static readonly Dictionary<(VehicleType, int), float> slotClear = new Dictionary<(VehicleType, int), float>();

        private static LevelGeometry GeometryOf(LevelData level)
        {
            if (geometry.TryGetValue(level, out LevelGeometry g)) return g;
            int n = level.vehicles.Count;
            g = new LevelGeometry { boardFoot = new (Vector2, Vector2, float, float)[n], topLeft = new bool[n], turnOk = new bool[n, 8] };
            for (int i = 0; i < n; i++)
            {
                VehiclePlan still = TrafficSchedule.ArrivalPlan(i, level.vehicles[i], 1, 0f);
                still.PoseAtDistance(0f, out Vector3 c, out Vector3 f);
                g.boardFoot[i] = (new Vector2(c.x, c.z), new Vector2(f.x, f.z).normalized, still.HalfLength, still.HalfWidth);
                g.topLeft[i] = UsesTopLeftCorner(level.vehicles[i], 1);
                foreach (int b in OpenBays) g.turnOk[i, b] = RouteBuilder.TurnInRoomOk(level.vehicles[i], b);
            }
            geometry[level] = g;
            return g;
        }

        private static List<LevelData> Levels()
        {
            if (levels != null) return levels;
            levels = new List<LevelData>();
            for (int level = 1; level <= 40; level++) levels.Add(LevelGenerator.Generate(level));
            return levels;
        }

        private static bool UsesTopLeftCorner(VehicleData v, int bay)
        {
            foreach (Vector2 p in RouteBuilder.Build(v, bay))
            {
                if (Mathf.Abs(p.x - ScreenRef.LeftX) < 0.5f && Mathf.Abs(p.y - ScreenRef.TopY) < 0.5f) return true;
            }
            return false;
        }

        [Test]
        public void RouteTimes_LongestAboutTwoSeconds()
        {
            var times = new List<float>();
            foreach (LevelData level in Levels())
            {
                for (int i = 0; i < level.vehicles.Count; i++)
                {
                    foreach (int bay in OpenBays) times.Add(TrafficSchedule.ArrivalPlan(i, level.vehicles[i], bay, 0f).Duration);
                }
            }
            times.Sort();
            float longest = times[times.Count - 1];
            float median = times[times.Count / 2];
            Debug.Log($"[TrafficScheduleTests] RoadSpeed {GameTuning.RoadSpeedPx} px/s: route time longest {longest:F2}s, " +
                      $"median {median:F2}s, shortest {times[0]:F2}s ({times.Count} routes, levels 1-40, bays 1-4)");
            Assert.That(longest, Is.InRange(1.7f, 2.3f), "longest route should take about 2 s");
        }

        [Test]
        public void Plan_NeverStopsOnceStarted()
        {
            LevelData level = Levels()[5];
            VehiclePlan p = TrafficSchedule.ArrivalPlan(0, level.vehicles[0], 2, 1f);
            p.Start = 1.5f;
            float prev = p.DistanceAt(p.Start);
            for (float t = p.Start + 1f / 60f; t < p.End; t += 1f / 60f)
            {
                float d = p.DistanceAt(t);
                Assert.Greater(d, prev, $"t={t:F3}");
                prev = d;
            }
            Assert.AreEqual(p.Path.Length, p.DistanceAt(p.End), 1e-4f);
        }

        private sealed class Ev
        {
            public float time;
            public int vehicle;
            public bool depart;
        }

        [Test]
        public void Scheduler_5000RandomTrials_NoOverlap_NoWaitAfterStart_AllArrive()
        {
            var rng = new System.Random(20261121);
            List<LevelData> pool = Levels();
            int overlaps = 0, failedSchedules = 0, notArrived = 0, waits = 0, cornerBursts = 0, taps = 0, refused = 0, deferred = 0;
            int clipSteps = 0;
            long scheduleTicks = 0, verifyTicks = 0, planTicks = 0, searchTicks = 0;
            float clipMax = 0f;
            var firstProblems = new StringBuilder();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var byKind = new Dictionary<string, int>();
            var maxDepth = new Dictionary<string, float>();

            for (int trial = 0; trial < 5000; trial++)
            {
                long trialStart = sw.ElapsedTicks;
                LevelData level = pool[rng.Next(pool.Count)];
                var board = new BoardState(level.columns, level.rows);
                var onBoard = new HashSet<int>();
                for (int i = 0; i < level.vehicles.Count; i++)
                {
                    board.Occupy(i, level.vehicles[i]);
                    onBoard.Add(i);
                }
                LevelGeometry geo = GeometryOf(level);
                var boardFoot = geo.boardFoot;
                TrafficSchedule schedule = TrafficSchedule.CreateForGame();
                var bayVehicle = new Dictionary<int, int>();         // bay -> vehicle (reserved or parked)
                var bayFreeAt = new Dictionary<int, float>();
                foreach (int b in OpenBays) bayFreeAt[b] = 0f;
                var arrivals = new Dictionary<int, VehiclePlan>();
                var allPlans = new List<VehiclePlan>();
                var tapTime = new Dictionary<int, float>();
                var order = new Dictionary<VehiclePlan, int>();   // scheduling order

                // Taps: a burst of up to 4 vehicles using the top-left corner within 0.2 s, then random taps.
                var events = new List<Ev>();
                var corner = onBoard.Where(i => geo.topLeft[i] && board.IsPathClear(level.vehicles[i]))
                                    .OrderBy(_ => rng.Next()).Take(4).ToList();
                if (corner.Count >= 2) cornerBursts++;
                float burstAt = (float)rng.NextDouble();
                foreach (int i in corner) events.Add(new Ev { time = burstAt + (float)rng.NextDouble() * 0.2f, vehicle = i });
                int extra = 3 + rng.Next(5);
                for (int k = 0; k < extra; k++) events.Add(new Ev { time = (float)rng.NextDouble() * 6f, vehicle = -1 });

                while (events.Count > 0)
                {
                    events.Sort((a, b) => a.time.CompareTo(b.time));
                    Ev ev = events[0];
                    events.RemoveAt(0);
                    float now = ev.time;

                    if (ev.depart)
                    {
                        VehiclePlan arrived = arrivals[ev.vehicle];
                        int bay = bayVehicle.First(kv => kv.Value == ev.vehicle).Key;
                        long pc = sw.ElapsedTicks;
                        VehiclePlan dep = TrafficSchedule.DeparturePlan(ev.vehicle, level.vehicles[ev.vehicle].type, bay, now);
                        planTicks += sw.ElapsedTicks - pc;
                        long sc = sw.ElapsedTicks;
                        bool depOk = schedule.CanSchedule(dep, now);
                        searchTicks += sw.ElapsedTicks - sc;
                        if (!depOk)
                        {
                            deferred++;
                            if (now < 120f) events.Add(new Ev { time = now + 0.25f, vehicle = ev.vehicle, depart = true }); // stays parked, retries
                            else Problem(ref notArrived, $"departure of {ev.vehicle} never possible (trial {trial}): {schedule.LastFailure}");
                            continue;
                        }
                        arrived.HoldUntil = now;
                        if (!schedule.Schedule(dep, now)) Problem(ref failedSchedules, $"departure of {ev.vehicle} from bay {bay} not scheduled (trial {trial}): {schedule.LastFailure}");
                        order[dep] = order.Count;
                        allPlans.Add(dep);
                        VehicleType vt = level.vehicles[ev.vehicle].type;
                        if (!slotClear.TryGetValue((vt, bay), out float clearS)) slotClear[(vt, bay)] = clearS = SlotClearDistance(dep, vt);
                        bayFreeAt[bay] = dep.Start + dep.RelativeTimeAt(clearS);
                        bayVehicle.Remove(bay);
                        continue;
                    }

                    // Tap: a free (path clear) vehicle; random one unless it is part of the corner burst.
                    int v = ev.vehicle;
                    if (v < 0)
                    {
                        var free = onBoard.Where(i => board.IsPathClear(level.vehicles[i])).ToList();
                        if (free.Count == 0) continue;
                        v = free[rng.Next(free.Count)];
                    }
                    if (!onBoard.Contains(v) || !board.IsPathClear(level.vehicles[v])) continue; // blocked: bump, stays
                    // Same bay choice as RoadExitBehaviour: free bays (clean turn-in first), first one that can be scheduled.
                    var freeBays = OpenBays.Where(b => !bayVehicle.ContainsKey(b) && bayFreeAt[b] <= now)
                                           .OrderBy(b => geo.turnOk[v, b] ? 0 : 1).ThenBy(b => b).ToList();
                    if (freeBays.Count == 0) continue; // no free bay: shake, stays
                    int chosen = -1;
                    VehiclePlan plan = null;
                    foreach (int b in freeBays)
                    {
                        long pc = sw.ElapsedTicks;
                        VehiclePlan candidate = TrafficSchedule.ArrivalPlan(v, level.vehicles[v], b, now);
                        planTicks += sw.ElapsedTicks - pc;
                        long sc = sw.ElapsedTicks;
                        bool canOk = schedule.CanSchedule(candidate, now);
                        searchTicks += sw.ElapsedTicks - sc;
                        if (!canOk) continue;
                        chosen = b;
                        plan = candidate;
                        break;
                    }
                    if (chosen < 0) { refused++; continue; } // no reservable bay: shake, stays
                    taps++;
                    board.Free(v, level.vehicles[v]);
                    onBoard.Remove(v);
                    bayVehicle[chosen] = v;
                    if (!schedule.Schedule(plan, now)) Problem(ref failedSchedules, $"arrival of {v} to bay {chosen} not scheduled (trial {trial}): {schedule.LastFailure}");
                    arrivals[v] = plan;
                    allPlans.Add(plan);
                    order[plan] = order.Count;
                    tapTime[v] = now;
                    events.Add(new Ev { time = plan.End + 0.3f + (float)rng.NextDouble() * 1.7f, vehicle = v, depart = true });
                }

                scheduleTicks += sw.ElapsedTicks - trialStart;
                long verifyStart = sw.ElapsedTicks;
                // Every tapped vehicle arrived and left.
                foreach (VehiclePlan p in allPlans)
                {
                    if (float.IsInfinity(p.End) || float.IsNaN(p.End)) Problem(ref notArrived, $"plan {p.Id} never ends (trial {trial})");
                }

                // Verify at 1/60 s. Static footprints (untapped board vehicles, vehicles waiting in their cell, parked
                // ones) cannot change against each other, so every pair with at least one driving vehicle is checked.
                float tEnd = allPlans.Count == 0 ? 0f : allPlans.Max(p => p.End) + 0.1f;
                int np = allPlans.Count;
                var planOrder = new int[np];
                var startFoot = new (Vector2 c, Vector2 f, float hl, float hw)[np];
                var endFoot = new (Vector2 c, Vector2 f, float hl, float hw)[np];
                var lastD = new float[np];
                for (int k = 0; k < np; k++)
                {
                    VehiclePlan p = allPlans[k];
                    planOrder[k] = order[p];
                    p.PoseAtDistance(0f, out Vector3 c0, out Vector3 f0);
                    startFoot[k] = (new Vector2(c0.x, c0.z), new Vector2(f0.x, f0.z).normalized, p.HalfLength, p.HalfWidth);
                    p.PoseAtDistance(p.Path.Length, out Vector3 c1, out Vector3 f1);
                    endFoot[k] = (new Vector2(c1.x, c1.z), new Vector2(f1.x, f1.z).normalized, p.HalfLength, p.HalfWidth);
                    lastD[k] = -1f;
                }
                var boardIds = onBoard.ToList();
                var tapped = tapTime.ToList();
                var moving = new List<(int k, int id, Vector2 c, Vector2 f, float hl, float hw)>();
                var statics = new List<(int k, int id, Vector2 c, Vector2 f, float hl, float hw)>(); // k = plan index, -1 = board

                for (float t = 0f; t <= tEnd; t += 1f / 60f)
                {
                    moving.Clear();
                    for (int k = 0; k < np; k++)
                    {
                        VehiclePlan p = allPlans[k];
                        if (t < p.Start || t > p.End) continue;
                        float d = p.DistanceAt(t);
                        if (lastD[k] >= 0f && t > p.Start + 1f / 60f && d <= lastD[k])
                            Problem(ref waits, $"plan {p.Id} did not advance at t={t:F2} (trial {trial})");
                        lastD[k] = d;
                        p.PoseAt(t, out Vector3 c, out Vector3 f);
                        moving.Add((k, p.Id, new Vector2(c.x, c.z), new Vector2(f.x, f.z).normalized, p.HalfLength, p.HalfWidth));
                    }
                    if (moving.Count == 0) continue;

                    statics.Clear();
                    for (int k = 0; k < np; k++)
                    {
                        VehiclePlan p = allPlans[k];
                        if (!p.Present(t) || (t >= p.Start && t <= p.End)) continue;
                        var ft = t < p.Start ? startFoot[k] : endFoot[k];
                        statics.Add((k, p.Id, ft.c, ft.f, ft.hl, ft.hw));
                    }
                    foreach (int i in boardIds) statics.Add((-1, i, boardFoot[i].c, boardFoot[i].f, boardFoot[i].hl, boardFoot[i].hw));
                    foreach (KeyValuePair<int, float> kv in tapped)
                    {
                        if (kv.Value > t) statics.Add((-1, kv.Key, boardFoot[kv.Key].c, boardFoot[kv.Key].f, boardFoot[kv.Key].hl, boardFoot[kv.Key].hw));
                    }

                    for (int a = 0; a < moving.Count; a++)
                    {
                        var A = moving[a];
                        for (int b = a + 1; b < moving.Count; b++)
                        {
                            var B = moving[b];
                            if (Touch(A.c, A.f, A.hl, A.hw, B.c, B.f, B.hl, B.hw)) Report(A.id, A.c, B.id, B.c, A.f, A.hl, A.hw, B.f, B.hl, B.hw, t, "driving", true);
                        }
                        foreach (var B in statics)
                        {
                            if (B.id == A.id || !Touch(A.c, A.f, A.hl, A.hw, B.c, B.f, B.hl, B.hw)) continue;
                            // A scheduling conflict if the static vehicle was already in the table when the moving one was
                            // scheduled; otherwise it was still an untapped board vehicle then (road geometry, not timing).
                            bool scheduledBefore = B.k >= 0 && planOrder[B.k] < planOrder[A.k];
                            Report(A.id, A.c, B.id, B.c, A.f, A.hl, A.hw, B.f, B.hl, B.hw, t, scheduledBefore ? "waiting/parked" : "board", scheduledBefore);
                        }
                    }
                }

                void Report(int aId, Vector2 ac, int bId, Vector2 bc, Vector2 af, float ahl, float ahw, Vector2 bf, float bhl, float bhw,
                            float t, string kind, bool scheduling)
                {
                    float depthPx = Penetration(ac, af, ahl, ahw, bc, bf, bhl, bhw) / ScreenRef.UnitsPerPx;
                    byKind[kind] = byKind.TryGetValue(kind, out int n) ? n + 1 : 1;
                    maxDepth[kind] = Mathf.Max(maxDepth.TryGetValue(kind, out float md) ? md : 0f, depthPx);
                    if (!scheduling)
                    {
                        clipSteps++;
                        clipMax = Mathf.Max(clipMax, depthPx);
                        return;
                    }
                    Problem(ref overlaps, $"overlap trial {trial} level {level.level} t={t:F2}: vehicle {aId} " +
                            $"ref{ScreenRef.WorldToRef(new Vector3(ac.x, 0f, ac.y))} and {bId} ref{ScreenRef.WorldToRef(new Vector3(bc.x, 0f, bc.y))}");
                }

                verifyTicks += sw.ElapsedTicks - verifyStart;

            }

            double tick = 1.0 / System.Diagnostics.Stopwatch.Frequency;
            Debug.Log($"[TrafficScheduleTests] 5000 trials in {sw.Elapsed.TotalSeconds:F1}s (scheduling {scheduleTicks * tick:F1}s: " +
                      $"plans {planTicks * tick:F1}s, search {searchTicks * tick:F1}s; " +
                      $"checking {verifyTicks * tick:F1}s): {taps} vehicles, {cornerBursts} corner bursts, " +
                      $"{refused} taps refused (no reservable bay), {deferred} departure retries, " +
                      $"road-geometry clipping of vehicles still in the lot: {clipSteps} steps, max {clipMax:F1}px; " +
                      $"overlap steps {overlaps} ({string.Join(", ", byKind.Select(k => $"{k.Key} {k.Value} max {maxDepth[k.Key]:F1}px"))}), " +
                      $"waits after start {waits}, unscheduled {failedSchedules}, not arrived {notArrived}\n{firstProblems}");
            Assert.AreEqual(0, overlaps, firstProblems.ToString());
            Assert.AreEqual(0, waits, firstProblems.ToString());
            Assert.AreEqual(0, failedSchedules, firstProblems.ToString());
            Assert.AreEqual(0, notArrived, firstProblems.ToString());

            void Problem(ref int counter, string message)
            {
                counter++;
                if (firstProblems.Length < 2500) firstProblems.AppendLine(message);
            }
        }

        private static bool Touch(Vector2 ac, Vector2 af, float ahl, float ahw, Vector2 bc, Vector2 bf, float bhl, float bhw)
        {
            float reach = ahl + ahw + bhl + bhw;
            if ((ac - bc).sqrMagnitude > reach * reach) return false;
            return TrafficSchedule.ObbOverlap(ac, af, ahl, ahw, bc, bf, bhl, bhw);
        }

        private static float Penetration(Vector2 c1, Vector2 f1, float hl1, float hw1, Vector2 c2, Vector2 f2, float hl2, float hw2)
        {
            var r1 = new Vector2(f1.y, -f1.x);
            var r2 = new Vector2(f2.y, -f2.x);
            Vector2 d = c2 - c1;
            float min = float.MaxValue;
            foreach (Vector2 axis in new[] { f1, r1, f2, r2 })
            {
                float p1 = hl1 * Mathf.Abs(Vector2.Dot(f1, axis)) + hw1 * Mathf.Abs(Vector2.Dot(r1, axis));
                float p2 = hl2 * Mathf.Abs(Vector2.Dot(f2, axis)) + hw2 * Mathf.Abs(Vector2.Dot(r2, axis));
                min = Mathf.Min(min, p1 + p2 - Mathf.Abs(Vector2.Dot(d, axis)));
            }
            return min;
        }

        private static void Add(List<(int id, Vector2 c, Vector2 f, float hl, float hw)> list, int id,
                                (Vector2 c, Vector2 f, float hl, float hw) foot) => list.Add((id, foot.c, foot.f, foot.hl, foot.hw));

        /// <summary>Distance along a departure at which the tail has left the painted slot (BayManager frees the bay).</summary>
        private static float SlotClearDistance(VehiclePlan dep, VehicleType type)
        {
            float tailPx = ScreenRef.VehicleLengthPx(type) * 0.5f;
            for (float s = 0f; s < dep.Path.Length; s += 0.02f)
            {
                dep.PoseAtDistance(s, out Vector3 c, out _);
                if (ScreenRef.WorldToRef(c).y + tailPx <= ScreenRef.SlotTopY) return s;
            }
            return dep.Path.Length;
        }
    }
}
