using System.Collections.Generic;
using System.Text;
using BusRush.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace BusRush.Tests
{
    /// <summary>Edit-mode tests for the board model and the level generator.</summary>
    public sealed class LevelGeneratorTests
    {
        private const int LevelCount = 200;

        [Test]
        public void Levels1To200_AreSolvable()
        {
            var failures = new StringBuilder();
            int failed = 0;
            int minVehicles = int.MaxValue, maxVehicles = 0;

            for (int level = 1; level <= LevelCount; level++)
            {
                LevelData data = LevelGenerator.Generate(level, LevelGenerator.SeedFor(level));
                minVehicles = Mathf.Min(minVehicles, data.vehicles.Count);
                maxVehicles = Mathf.Max(maxVehicles, data.vehicles.Count);

                if (data.vehicles.Count == 0)
                {
                    failed++;
                    failures.AppendLine($"Level {level}: no vehicles");
                    continue;
                }
                if (!LevelSolver.IsSolvable(data, out List<int> order))
                {
                    failed++;
                    failures.AppendLine($"Level {level}: stuck after {order.Count}/{data.vehicles.Count} vehicles (seed {data.seed})");
                }
            }

            Debug.Log($"[LevelGeneratorTests] {LevelCount - failed}/{LevelCount} levels solvable; vehicles per level {minVehicles}..{maxVehicles}.");
            if (failed > 0) Debug.LogError("[LevelGeneratorTests] Failures:\n" + failures);
            Assert.AreEqual(0, failed, failures.ToString());
        }

        [Test]
        public void Generator_IsDeterministic()
        {
            for (int level = 1; level <= 30; level++)
            {
                LevelData a = LevelGenerator.Generate(level, LevelGenerator.SeedFor(level));
                LevelData b = LevelGenerator.Generate(level, LevelGenerator.SeedFor(level));
                Assert.AreEqual(a.vehicles.Count, b.vehicles.Count, $"level {level}");
                for (int i = 0; i < a.vehicles.Count; i++)
                {
                    Assert.AreEqual(a.vehicles[i].ToString(), b.vehicles[i].ToString(), $"level {level} vehicle {i}");
                }
            }
        }

        [Test]
        public void Levels_FitTheBoard_AndUseExpectedColours()
        {
            for (int level = 1; level <= LevelCount; level++)
            {
                LevelData data = LevelGenerator.Generate(level, LevelGenerator.SeedFor(level));
                var board = new BoardState(data.columns, data.rows);
                for (int i = 0; i < data.vehicles.Count; i++)
                {
                    Assert.IsTrue(board.CanPlace(data.vehicles[i]), $"level {level}: vehicle {i} overlaps or leaves the lot");
                    board.Occupy(i, data.vehicles[i]);
                }

                Dictionary<GameColor, int> perColor = data.PassengerCountPerColor();
                int expectedColors = Mathf.Min(LevelGenerator.SettingsFor(level).colorCount, data.vehicles.Count);
                Assert.AreEqual(expectedColors, perColor.Count, $"level {level}: colour count");

                int sum = 0;
                foreach (int n in perColor.Values) sum += n;
                Assert.AreEqual(data.TotalPassengers(), sum, $"level {level}: passenger total");
            }
        }

        [Test]
        public void Level1_HasEightVehiclesAndThreeColours()
        {
            LevelData data = LevelGenerator.Generate(1, LevelGenerator.SeedFor(1));
            Assert.AreEqual(8, data.vehicles.Count);
            Assert.AreEqual(3, data.PassengerCountPerColor().Count);
        }

        [Test]
        public void PathCheck_DetectsBlocker()
        {
            var board = new BoardState(9, 10);
            var car = new VehicleData(VehicleType.Car, GameColor.Red, 4, 5, Direction.Up);      // cells (4,5),(4,6)
            var blocker = new VehicleData(VehicleType.Car, GameColor.Blue, 4, 2, Direction.Left); // cells (4,2),(5,2)
            board.Occupy(0, car);
            board.Occupy(1, blocker);

            Assert.IsFalse(board.IsPathClear(car, out int id));
            Assert.AreEqual(1, id);
            board.Free(1, blocker);
            Assert.IsTrue(board.IsPathClear(car));
        }
    }
}
