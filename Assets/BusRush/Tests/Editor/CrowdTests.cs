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
            Debug.Log($"[CrowdTests] greedy simulation (window {GameTuning.FrontWindow}) won {200 - failed}/200 levels with 4 open bays.");
            if (failed > 0) Debug.LogError("[CrowdTests] Failures:\n" + failures);
            Assert.AreEqual(0, failed, failures.ToString());
        }

        /// <summary>Queue of 12: Red x4, Blue (position 5), Red x6, Green (position 12).</summary>
        private static List<GameColor> WindowQueue()
        {
            var q = new List<GameColor>();
            for (int i = 0; i < 4; i++) q.Add(GameColor.Red);
            q.Add(GameColor.Blue);
            for (int i = 0; i < 6; i++) q.Add(GameColor.Red);
            q.Add(GameColor.Green);
            Assert.AreEqual(12, q.Count);
            return q;
        }

        [Test]
        public void Window_Position5_BoardsMatchingParkedVehicle()
        {
            Assert.AreEqual(8, GameTuning.FrontWindow);
            List<GameColor> q = WindowQueue();
            int boarded = GameSimulator.BoardOne(q, GameColor.Blue); // a blue vehicle is parked with space
            Assert.AreEqual(4, boarded, "the blue passenger at position 5 boards");
            Assert.AreEqual(11, q.Count);
            CollectionAssert.DoesNotContain(q, GameColor.Blue);
            Assert.AreEqual(GameColor.Red, q[4], "the people behind closed the gap");
            Assert.AreEqual(GameColor.Green, q[10]);
        }

        [Test]
        public void Window_Position12_DoesNotBoard()
        {
            List<GameColor> q = WindowQueue();
            int boarded = GameSimulator.BoardOne(q, GameColor.Green); // a green vehicle is parked with space
            Assert.AreEqual(-1, boarded, "the green passenger at position 12 is outside the window of 8");
            Assert.AreEqual(12, q.Count);
            Assert.AreEqual(GameColor.Green, q[11]);
        }

        [Test]
        public void Window_EarliestOfColourBoardsFirst()
        {
            var q = new List<GameColor> { GameColor.Red, GameColor.Blue, GameColor.Yellow, GameColor.Blue };
            Assert.AreEqual(1, BoardingRule.EligibleIndex(q, GameColor.Blue));
            Assert.AreEqual(-1, BoardingRule.EligibleIndex(q, GameColor.Green));
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
