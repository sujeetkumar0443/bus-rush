using System.Text;
using BusRush.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace BusRush.Tests
{
    /// <summary>Edit-mode tests for the 100-level difficulty plan.</summary>
    public sealed class LevelPlanTests
    {
        [Test]
        public void Levels1To100_WinnableWithFourBays_AndCurveHolds()
        {
            var table = new StringBuilder();
            table.AppendLine("level | tier   | plan veh | actual veh | colours | passengers | buses");
            var failures = new StringBuilder();
            int previousVehicles = -1;
            bool previousWasBoss = false;

            for (int level = 1; level <= LevelPlan.PlannedLevels; level++)
            {
                LevelSpec spec = LevelPlan.Get(level);
                LevelData data = LevelGenerator.Generate(level);
                int vehicles = data.vehicles.Count;
                int buses = data.vehicles.FindAll(v => v.type == VehicleType.Bus).Count;
                int colours = data.PassengerCountPerColor().Count;
                table.AppendLine($"{level,5} | {spec.tier,-6} | {spec.vehicleCount,8} | {vehicles,10} | {colours,7} | {data.queue.Count,10} | {buses,5}");

                GameSimulator.Result r = GameSimulator.Run(data, 4);
                if (!r.won) failures.AppendLine($"Level {level}: not winnable with 4 bays: {r.failReason}");

                if (previousVehicles >= 0 && !previousWasBoss && previousVehicles - vehicles > 6)
                {
                    failures.AppendLine($"Level {level}: vehicle count drops {previousVehicles} -> {vehicles} (> 6, not after a Boss)");
                }
                previousVehicles = vehicles;
                previousWasBoss = spec.tier == LevelTier.Boss;
            }

            Debug.Log("[LevelPlanTests] Difficulty table\n" + table);
            if (failures.Length > 0) Debug.LogError("[LevelPlanTests] Failures:\n" + failures);
            Assert.IsEmpty(failures.ToString());
        }

        [Test]
        public void Plan_HasBossesBreathersAndEasyStart()
        {
            for (int level = 1; level <= 5; level++)
            {
                LevelSpec s = LevelPlan.Get(level);
                Assert.AreEqual(LevelTier.Easy, s.tier, $"level {level}");
                Assert.AreEqual(3, s.colorCount, $"level {level}");
                Assert.That(s.vehicleCount, Is.InRange(8, 10), $"level {level}");
            }
            for (int level = 10; level <= 100; level += 10)
            {
                Assert.AreEqual(LevelTier.Boss, LevelPlan.Get(level).tier, $"level {level}");
                Assert.Greater(LevelPlan.Get(level).vehicleCount, LevelPlan.Get(level - 1).vehicleCount, $"boss {level} is bigger");
                if (level < 100)
                {
                    Assert.Less(LevelPlan.Get(level + 1).vehicleCount, LevelPlan.Get(level).vehicleCount, $"breather after {level}");
                }
            }
            Assert.That(LevelPlan.Get(65).colorCount, Is.InRange(6, 7));
            Assert.That(LevelPlan.Get(65).vehicleCount, Is.InRange(28, 34));
        }

        [Test]
        public void EndlessLevels_ReuseHardAndBoss_AndStayWinnable()
        {
            for (int level = 101; level <= 130; level++)
            {
                LevelSpec s = LevelPlan.Get(level);
                Assert.IsTrue(s.tier == LevelTier.Hard || s.tier == LevelTier.Boss, $"level {level} tier {s.tier}");
                Assert.IsTrue(GameSimulator.Run(LevelGenerator.Generate(level), 4).won, $"level {level}");
            }
        }

        [Test]
        public void World_ChangesEveryFiveLevels()
        {
            Assert.AreEqual(0, LevelPlan.WorldIndex(1));
            Assert.AreEqual(0, LevelPlan.WorldIndex(5));
            Assert.AreEqual(1, LevelPlan.WorldIndex(6));
            Assert.AreEqual(2, LevelPlan.WorldIndex(11));
            Assert.AreEqual(0, LevelPlan.WorldIndex(16));
        }
    }
}
