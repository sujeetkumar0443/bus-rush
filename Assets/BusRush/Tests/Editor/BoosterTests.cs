using System.Collections.Generic;
using System.Linq;
using BusRush.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace BusRush.Tests
{
    /// <summary>Edit-mode tests for the boosters (inventory, VIP, Arrange, Shuffle). Pure logic, no scene.</summary>
    public sealed class BoosterTests
    {
        private const string SaveKey = "BusRush.Save";

        // ---------------------------------------------------------------- inventory

        [Test]
        public void Inventory_AddUseCount_AndPersists()
        {
            string backup = PlayerPrefs.GetString(SaveKey, null);
            try
            {
                SaveSystem.ResetToDefaults();
                Assert.AreEqual(GameTuning.StartingBoosters, BoosterInventory.Count(BoosterType.Arrange));
                Assert.AreEqual(GameTuning.StartingCoins, CoinWallet.Coins);

                int events = 0;
                void OnChanged() => events++;
                BoosterInventory.Changed += OnChanged;
                BoosterInventory.Add(BoosterType.Arrange, 3);
                Assert.AreEqual(GameTuning.StartingBoosters + 3, BoosterInventory.Count(BoosterType.Arrange));
                Assert.IsTrue(BoosterInventory.Use(BoosterType.Arrange));
                Assert.AreEqual(GameTuning.StartingBoosters + 2, BoosterInventory.Count(BoosterType.Arrange));
                BoosterInventory.Changed -= OnChanged;
                Assert.GreaterOrEqual(events, 2, "UI refresh event raised");

                // Use down to zero: the next use fails and spends nothing.
                while (BoosterInventory.Count(BoosterType.Vip) > 0) BoosterInventory.Use(BoosterType.Vip);
                Assert.IsFalse(BoosterInventory.Use(BoosterType.Vip));
                Assert.AreEqual(0, BoosterInventory.Count(BoosterType.Vip));

                // Buying spends coins.
                Assert.IsTrue(CoinWallet.TrySpend(BoosterInventory.PriceCoins(BoosterType.Vip)));
                Assert.AreEqual(GameTuning.StartingCoins - GameTuning.BoosterPriceCoins, CoinWallet.Coins);

                // Persistence: reload from PlayerPrefs.
                SaveSystem.Load();
                Assert.AreEqual(GameTuning.StartingBoosters + 2, BoosterInventory.Count(BoosterType.Arrange));
                Assert.AreEqual(0, BoosterInventory.Count(BoosterType.Vip));
                Assert.AreEqual(GameTuning.StartingCoins - GameTuning.BoosterPriceCoins, CoinWallet.Coins);
            }
            finally
            {
                if (string.IsNullOrEmpty(backup)) PlayerPrefs.DeleteKey(SaveKey);
                else PlayerPrefs.SetString(SaveKey, backup);
                PlayerPrefs.Save();
                SaveSystem.Load();
            }
        }

        [Test]
        public void UnlockLevels_FromGameTuning()
        {
            Assert.IsFalse(BoosterInventory.RowVisible(3));
            Assert.IsTrue(BoosterInventory.RowVisible(4));
            Assert.IsTrue(BoosterInventory.IsUnlocked(BoosterType.Vip, 4));
            Assert.IsFalse(BoosterInventory.IsUnlocked(BoosterType.Arrange, 6));
            Assert.IsTrue(BoosterInventory.IsUnlocked(BoosterType.Arrange, 7));
            Assert.IsFalse(BoosterInventory.IsUnlocked(BoosterType.Shuffle, 12));
            Assert.IsTrue(BoosterInventory.IsUnlocked(BoosterType.Shuffle, 13));
        }

        // ---------------------------------------------------------------- VIP

        [Test]
        public void Vip_UnlocksOnlyOncePerLevel()
        {
            var level = new BoosterLogic.VipLatch();
            Assert.IsFalse(level.Open);
            Assert.IsTrue(level.TryOpen(), "first use opens the VIP bay");
            Assert.IsTrue(level.Open);
            Assert.IsFalse(level.TryOpen(), "second use in the same level does nothing (nothing spent)");
            Assert.IsTrue(new BoosterLogic.VipLatch().TryOpen(), "a new level starts locked again");
        }

        // ---------------------------------------------------------------- states

        /// <summary>A level in progress: the first <paramref name="parkedCount"/> vehicles of the park order are parked (empty).</summary>
        private static LevelSnapshot MidLevel(int levelNumber, int parkedCount)
        {
            LevelData level = LevelGenerator.Generate(levelNumber);
            var snap = new LevelSnapshot
            {
                columns = level.columns,
                rows = level.rows,
                vehicles = level.vehicles.Select(v => new VehicleData(v.type, v.color, v.headCol, v.headRow, v.direction)).ToList(),
                exitOrder = new List<int>(level.exitOrder),
                queue = new List<GameColor>(level.queue),
                usableBays = 4
            };
            var parkedIds = level.exitOrder.Take(parkedCount).ToList();
            for (int i = 0; i < level.vehicles.Count; i++)
            {
                if (!parkedIds.Contains(i)) snap.onBoard.Add(i);
            }
            foreach (int id in parkedIds)
            {
                snap.parked.Add(new GameSimulator.ParkedState { color = level.vehicles[id].color, capacity = level.vehicles[id].Capacity });
            }
            return snap;
        }

        // ---------------------------------------------------------------- ARRANGE

        [Test]
        public void Arrange_SamePassengers_FrontMatchesParked_StaysWinnable()
        {
            int arranged = 0;
            for (int levelNumber = 7; levelNumber <= 40; levelNumber++)
            {
                LevelSnapshot snap = MidLevel(levelNumber, 2);
                if (!snap.IsWinnable()) continue; // only states the player could actually be in

                List<GameColor> result = BoosterLogic.Arrange(snap, GameTuning.FrontWindow, out bool changed);

                CollectionAssert.AreEquivalent(snap.queue, result, $"level {levelNumber}: same passengers");
                Assert.IsTrue(snap.IsWinnable(result), $"level {levelNumber}: still winnable");
                if (!changed)
                {
                    CollectionAssert.AreEqual(snap.queue, result, $"level {levelNumber}: unchanged when no arrangement helps");
                    continue;
                }
                arranged++;

                // The front window holds as many passengers for the parked vehicles as they have free seats (if available).
                var available = BoosterLogic.Counts(snap.queue);
                int expected = 0;
                foreach (GameSimulator.ParkedState p in snap.parked)
                {
                    int take = Mathf.Min(p.capacity - p.filled, available.TryGetValue(p.color, out int n) ? n : 0);
                    take = Mathf.Min(take, GameTuning.FrontWindow - expected);
                    expected += take;
                    if (available.ContainsKey(p.color)) available[p.color] -= take;
                }
                var parkedColours = new HashSet<GameColor>(snap.parked.Select(p => p.color));
                int matching = result.Take(GameTuning.FrontWindow).Count(parkedColours.Contains);
                Assert.GreaterOrEqual(matching, expected, $"level {levelNumber}: front window matches the parked vehicles");
            }
            Assert.Greater(arranged, 10, "Arrange changed the queue in most levels");
        }

        // ---------------------------------------------------------------- SHUFFLE

        [Test]
        public void Shuffle_KeepsColourCounts_StaysWinnable()
        {
            var rng = new System.Random(7);
            int shuffled = 0;
            for (int levelNumber = 13; levelNumber <= 40; levelNumber++)
            {
                LevelSnapshot snap = MidLevel(levelNumber, 1);
                if (!snap.IsWinnable()) continue;

                var lotBefore = snap.onBoard.Select(id => snap.vehicles[id]).ToList();
                Dictionary<GameColor, int> seatsBefore = BoosterLogic.SeatsPerColour(lotBefore);
                Dictionary<int, GameColor> colours = BoosterLogic.Shuffle(snap, rng, GameTuning.ShuffleRetries);
                if (colours == null) continue; // the game keeps the colours and refunds

                Assert.IsTrue(colours.Keys.All(snap.onBoard.Contains), "only vehicles in the lot change colour");
                Assert.IsTrue(colours.Any(kv => kv.Value != snap.vehicles[kv.Key].color), "something changed");
                var after = snap.vehicles.Select((v, id) => colours.TryGetValue(id, out GameColor c)
                    ? new VehicleData(v.type, c, v.headCol, v.headRow, v.direction) : v).ToList();
                Dictionary<GameColor, int> seatsAfter = BoosterLogic.SeatsPerColour(snap.onBoard.Select(id => after[id]));
                CollectionAssert.AreEquivalent(seatsBefore, seatsAfter, $"level {levelNumber}: seats per colour unchanged");
                Assert.IsTrue(snap.IsWinnable(null, after), $"level {levelNumber}: still winnable");
                shuffled++;
            }
            Assert.Greater(shuffled, 10, "Shuffle found a winnable new colouring in most levels");
        }
    }
}
