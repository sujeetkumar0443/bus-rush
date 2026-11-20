using System.Collections.Generic;
using System.Text;
using BusRush.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace BusRush.Tests
{
    /// <summary>Edit-mode tests for the passenger queue and the full-game greedy simulation.</summary>
    public sealed class CrowdTests
    {
        [Test]
        public void QueuePerColour_EqualsCapacityPerColour_200Levels()
        {
            for (int level = 1; level <= 200; level++)
            {
                LevelData data = LevelGenerator.Generate(level);
                Dictionary<GameColor, int> cap = data.PassengerCountPerColor();
                Dictionary<GameColor, int> queue = data.QueueCountPerColor();
                Assert.AreEqual(cap.Count, queue.Count, $"level {level}: colour count");
                foreach (KeyValuePair<GameColor, int> kv in cap)
                {
                    Assert.IsTrue(queue.TryGetValue(kv.Key, out int n), $"level {level}: no passengers for {kv.Key}");
                    Assert.AreEqual(kv.Value, n, $"level {level}: {kv.Key} passengers vs capacity");
                }
            }
        }

        [Test]
        public void GreedyGame_Wins200Levels_WithFourOpenBays()
        {
            var failures = new StringBuilder();
            int failed = 0;
            for (int level = 1; level <= 200; level++)
            {
                LevelData data = LevelGenerator.Generate(level);
                GameSimulator.Result r = GameSimulator.Run(data, 4);
                if (!r.won)
                {
                    failed++;
                    failures.AppendLine($"Level {level}: {r.failReason}");
                }
            }
            Debug.Log($"[CrowdTests] greedy simulation won {200 - failed}/200 levels with 4 open bays.");
            if (failed > 0) Debug.LogError("[CrowdTests] Failures:\n" + failures);
            Assert.AreEqual(0, failed, failures.ToString());
        }

        [Test]
        public void Queue_IsDeterministic()
        {
            LevelData a = LevelGenerator.Generate(17);
            LevelData b = LevelGenerator.Generate(17);
            CollectionAssert.AreEqual(a.queue, b.queue);
        }
    }
}
