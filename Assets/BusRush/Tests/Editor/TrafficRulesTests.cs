using System.Collections.Generic;
using BusRush.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace BusRush.Tests
{
    /// <summary>Edit-mode tests for the traffic priority rule and the hard overlap guard (pure logic, no scene).</summary>
    public sealed class TrafficRulesTests
    {
        [Test]
        public void Priority_NetworkOutranksLotAndParked()
        {
            long road = TrafficRules.NetworkKey(500);
            Assert.IsTrue(TrafficRules.MayWaitFor(TrafficRules.LotKey(1), road), "a car in the lot waits for road traffic");
            Assert.IsFalse(TrafficRules.MayWaitFor(road, TrafficRules.LotKey(1)), "road traffic never waits for the lot");
            Assert.IsTrue(TrafficRules.MayWaitFor(TrafficRules.ParkedKey(1), road), "a parked car waits before pulling out");
            Assert.IsFalse(TrafficRules.MayWaitFor(road, TrafficRules.ParkedKey(1)));
        }

        [Test]
        public void Priority_FirstToEnterGoesFirst()
        {
            Assert.IsTrue(TrafficRules.MayWaitFor(TrafficRules.NetworkKey(8), TrafficRules.NetworkKey(3)));
            Assert.IsFalse(TrafficRules.MayWaitFor(TrafficRules.NetworkKey(3), TrafficRules.NetworkKey(8)));
            Assert.IsTrue(TrafficRules.MayWaitFor(TrafficRules.LotKey(8), TrafficRules.LotKey(3)));
            Assert.IsFalse(TrafficRules.MayWaitFor(TrafficRules.NetworkKey(4), TrafficRules.NetworkKey(4)), "never waits for itself");
        }

        [Test]
        public void WaitGraph_UnderPriorityRule_HasNoCycle()
        {
            var rng = new System.Random(12345);
            for (int trial = 0; trial < 200; trial++)
            {
                int n = 2 + rng.Next(14);
                var keys = new long[n];
                for (int i = 0; i < n; i++)
                {
                    int tier = rng.Next(3);
                    long seq = rng.Next(1, 50);
                    keys[i] = tier == 0 ? TrafficRules.NetworkKey(seq) : tier == 1 ? TrafficRules.LotKey(seq) : TrafficRules.ParkedKey(seq);
                }
                // Every vehicle would like to wait for a random other one (a lane, a gate, a departure...);
                // the rule keeps only waits for a higher-priority vehicle.
                var waitsFor = new int[n];
                for (int i = 0; i < n; i++)
                {
                    int j = rng.Next(n);
                    waitsFor[i] = j != i && TrafficRules.MayWaitFor(keys[i], keys[j]) ? j : -1;
                }
                for (int start = 0; start < n; start++)
                {
                    var seen = new HashSet<int>();
                    for (int v = start; v >= 0; v = waitsFor[v])
                    {
                        Assert.IsTrue(seen.Add(v), $"trial {trial}: wait cycle through vehicle {v}");
                    }
                }
            }
        }

        [Test]
        public void LaneRuns_ConflictOnlyWhenOppositeAndOverlapping()
        {
            Assert.IsTrue(TrafficRules.LaneRunsConflict(1, 0f, 200f, -1, 150f, 300f));
            Assert.IsFalse(TrafficRules.LaneRunsConflict(1, 0f, 200f, 1, 150f, 300f), "same direction: following, not a conflict");
            Assert.IsFalse(TrafficRules.LaneRunsConflict(1, 0f, 100f, -1, 150f, 300f), "separate stretches");
        }

        private static readonly Vector2 Up = Vector2.up;
        private const float Hl = 0.92f, Hw = 0.4f;

        [Test]
        public void HardGuard_BlocksMovingIntoAVehicle()
        {
            // Other car ahead, nose to tail with a 0.05 gap; stepping 0.1 forward would overlap.
            Vector2 other = new Vector2(0f, 2 * Hl + 0.05f);
            Assert.IsTrue(TrafficRules.HardBlocks(Vector2.zero, Up, new Vector2(0f, 0.1f), Up, Hl, Hw, other, Up, Hl, Hw));
        }

        [Test]
        public void HardGuard_CloseIsNotOverlapping()
        {
            // 0.05 gap, stepping 0.04: still apart, so it may move (close is not overlapping).
            Vector2 other = new Vector2(0f, 2 * Hl + 0.05f);
            Assert.IsFalse(TrafficRules.HardBlocks(Vector2.zero, Up, new Vector2(0f, 0.04f), Up, Hl, Hw, other, Up, Hl, Hw));
            Assert.Less(TrafficRules.Penetration(Vector2.zero, Up, Hl, Hw, other, Up, Hl, Hw), 0f);
        }

        [Test]
        public void HardGuard_AlwaysAllowsMovingAway()
        {
            // Already overlapping by 0.1 (e.g. after a recovery): backing away or driving off sideways is allowed.
            Vector2 other = new Vector2(0f, 2 * Hl - 0.1f);
            Assert.Greater(TrafficRules.Penetration(Vector2.zero, Up, Hl, Hw, other, Up, Hl, Hw), 0f);
            Assert.IsFalse(TrafficRules.HardBlocks(Vector2.zero, Up, new Vector2(0f, -0.05f), Up, Hl, Hw, other, Up, Hl, Hw));
            Assert.IsTrue(TrafficRules.HardBlocks(Vector2.zero, Up, new Vector2(0f, 0.05f), Up, Hl, Hw, other, Up, Hl, Hw), "deeper is blocked");
        }
    }
}
