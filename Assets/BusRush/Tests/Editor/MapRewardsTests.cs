using BusRush.Gameplay;
using NUnit.Framework;

namespace BusRush.Tests
{
    /// <summary>Road Map gift chests (levels ending in 5, once each, rotating booster) and New World screens (once per world).</summary>
    public sealed class MapRewardsTests
    {
        [Test]
        public void Chest_DueOnlyForLevelsEndingIn5_AndOnlyOnce()
        {
            var d = new SaveData();
            for (int level = 1; level <= 100; level++)
            {
                Assert.AreEqual(level % 10 == 5, MapRewards.ChestDue(d, level), $"level {level}");
            }
            Assert.IsTrue(MapRewards.ChestDue(d, 15));
            MapRewards.MarkChestClaimed(d, 15);
            Assert.IsFalse(MapRewards.ChestDue(d, 15), "never given twice");
            Assert.IsTrue(MapRewards.ChestDue(d, 25), "other chests unaffected");
            MapRewards.MarkChestClaimed(d, 15);
            Assert.AreEqual(1, d.giftChestsClaimed.Count);
        }

        [Test]
        public void Chest_RewardRotatesVipArrangeShuffle()
        {
            BoosterType[] expected = { BoosterType.Vip, BoosterType.Arrange, BoosterType.Shuffle, BoosterType.Vip, BoosterType.Arrange };
            for (int i = 0; i < expected.Length; i++)
            {
                int level = 5 + 10 * i;
                RewardSpec r = MapRewards.ChestReward(level);
                Assert.AreEqual(GameTuning.GiftChestCoins, r.coins, $"level {level} coins");
                Assert.AreEqual(expected[i], r.MainBooster, $"level {level} booster");
                Assert.AreEqual(1, r.vip + r.arrange + r.shuffle, "exactly one booster");
            }
        }

        [Test]
        public void World_DueOnlyForFirstLevelOfAWorld_AndOnlyOnce()
        {
            var d = new SaveData();
            for (int level = 1; level <= 100; level++)
            {
                bool first = level > 1 && level % 10 == 1;
                Assert.AreEqual(first, MapRewards.WorldDue(d, level), $"unlocking level {level}");
            }
            Assert.AreEqual(2, MapRewards.WorldOf(11));
            MapRewards.MarkWorldShown(d, 2);
            Assert.IsFalse(MapRewards.WorldDue(d, 11), "world 2 shows once");
            Assert.IsTrue(MapRewards.WorldDue(d, 21));
            MapRewards.MarkWorldShown(d, 1);
            Assert.AreEqual(2, d.worldsShown, "never goes back");
        }

        [Test]
        public void ChestState_ComesFromSavedProgress_NotFromTheWinAnimation()
        {
            var d = new SaveData { level = 20 }; // e.g. set by the debug panel's go-to-level: levels 1-19 count as completed
            Assert.AreEqual(MapRewards.ChestState.Ready, MapRewards.StateOf(d, 5));
            Assert.AreEqual(MapRewards.ChestState.Ready, MapRewards.StateOf(d, 15));
            Assert.AreEqual(MapRewards.ChestState.Locked, MapRewards.StateOf(d, 25));
            MapRewards.MarkChestClaimed(d, 15);
            Assert.AreEqual(MapRewards.ChestState.Claimed, MapRewards.StateOf(d, 15));
            Assert.AreEqual(MapRewards.ChestState.Ready, MapRewards.StateOf(d, 5), "other chests unaffected");
            d.level = 15; // playing level 15: it is not completed yet
            Assert.AreEqual(MapRewards.ChestState.Locked, MapRewards.StateOf(new SaveData { level = 15 }, 15));
            Assert.AreEqual(MapRewards.ChestState.Ready, MapRewards.StateOf(new SaveData { level = 16 }, 15));
        }
    }
}
