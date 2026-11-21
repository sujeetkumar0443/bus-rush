using System.Collections.Generic;

namespace BusRush.Gameplay
{
    /// <summary>
    /// Road Map rewards (pure, edit-mode tested): a gift chest for winning every level that ends in 5 (once per chest)
    /// and the "New World" screen for the first level of every world after the first (once per world).
    /// </summary>
    public static class MapRewards
    {
        public const int LevelsPerWorld = 10;

        public static bool IsChestLevel(int level) => level >= 5 && level % LevelsPerWorld == 5;

        /// <summary>0 for level 5, 1 for 15, ...</summary>
        public static int ChestNumber(int level) => (level - 5) / LevelsPerWorld;

        /// <summary>100 coins + one booster, rotating VIP -> Arrange -> Shuffle by chest number.</summary>
        public static RewardSpec ChestReward(int level)
        {
            var booster = (BoosterType)(ChestNumber(level) % 3);
            RewardSpec r = RewardSpec.Booster(booster);
            r.coins = GameTuning.GiftChestCoins;
            return r;
        }

        public static bool ChestClaimed(SaveData d, int level) => d.giftChestsClaimed != null && d.giftChestsClaimed.Contains(level);

        /// <summary>Winning <paramref name="wonLevel"/> earns its chest (if it has one and it was not given yet).</summary>
        public static bool ChestDue(SaveData d, int wonLevel) => IsChestLevel(wonLevel) && !ChestClaimed(d, wonLevel);

        public static void MarkChestClaimed(SaveData d, int level)
        {
            d.giftChestsClaimed ??= new List<int>();
            if (!d.giftChestsClaimed.Contains(level)) d.giftChestsClaimed.Add(level);
        }

        /// <summary>1-based world of a level.</summary>
        public static int WorldOf(int level) => (level - 1) / LevelsPerWorld + 1;

        public static bool IsWorldStart(int level) => level > 1 && (level - 1) % LevelsPerWorld == 0;

        /// <summary>Unlocking <paramref name="unlockedLevel"/> opens a new world that has not been shown yet.</summary>
        public static bool WorldDue(SaveData d, int unlockedLevel) => IsWorldStart(unlockedLevel) && d.worldsShown < WorldOf(unlockedLevel);

        public static void MarkWorldShown(SaveData d, int world)
        {
            if (d.worldsShown < world) d.worldsShown = world;
        }
    }
}
