using System.Collections.Generic;

namespace BusRush.Gameplay
{
    /// <summary>A reward: coins and/or boosters (daily reward days, lucky spin segments).</summary>
    [System.Serializable]
    public struct RewardSpec
    {
        public int coins;
        public int vip;
        public int arrange;
        public int shuffle;

        public static RewardSpec Coins(int amount) => new RewardSpec { coins = amount };

        public static RewardSpec Booster(BoosterType type, int count = 1) => type switch
        {
            BoosterType.Vip => new RewardSpec { vip = count },
            BoosterType.Arrange => new RewardSpec { arrange = count },
            _ => new RewardSpec { shuffle = count }
        };

        public bool HasBoosters => vip + arrange + shuffle > 0;

        public RewardSpec Times(int k) => new RewardSpec { coins = coins * k, vip = vip * k, arrange = arrange * k, shuffle = shuffle * k };

        /// <summary>Adds the boosters to the inventory (coins are added by the caller, usually through the coin fly).</summary>
        public void GrantBoosters()
        {
            if (vip > 0) BoosterInventory.Add(BoosterType.Vip, vip);
            if (arrange > 0) BoosterInventory.Add(BoosterType.Arrange, arrange);
            if (shuffle > 0) BoosterInventory.Add(BoosterType.Shuffle, shuffle);
        }

        /// <summary>"+1 Arrange", "+1 VIP, +1 Arrange, +1 Shuffle"; empty without boosters.</summary>
        public string BoosterText()
        {
            var parts = new List<string>();
            if (vip > 0) parts.Add($"+{vip} VIP");
            if (arrange > 0) parts.Add($"+{arrange} Arrange");
            if (shuffle > 0) parts.Add($"+{shuffle} Shuffle");
            return string.Join(", ", parts);
        }

        /// <summary>The single booster of a booster-only reward (for its icon), or null.</summary>
        public BoosterType? MainBooster => vip > 0 ? BoosterType.Vip : arrange > 0 ? BoosterType.Arrange : shuffle > 0 ? BoosterType.Shuffle : (BoosterType?)null;
    }
}
