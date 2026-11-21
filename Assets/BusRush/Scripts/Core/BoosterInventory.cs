using System;
using BusRush.Gameplay;

namespace BusRush
{
    /// <summary>
    /// Booster counts, prices and unlock levels. Counts live in the existing PlayerPrefs save
    /// (<see cref="SaveSystem"/>), so there is one store; this class is the booster-facing API.
    /// </summary>
    public static class BoosterInventory
    {
        /// <summary>Raised whenever counts (or anything else in the save) change: refresh booster UI.</summary>
        public static event Action Changed
        {
            add => SaveSystem.Changed += value;
            remove => SaveSystem.Changed -= value;
        }

        public static int Count(BoosterType type) => SaveSystem.Data.GetBooster(type);

        public static void Add(BoosterType type, int amount = 1)
        {
            if (amount <= 0) return;
            SaveSystem.AddBooster(type, amount);
        }

        /// <summary>Spends one; false (nothing spent) if there is none.</summary>
        public static bool Use(BoosterType type)
        {
            int count = Count(type);
            if (count <= 0) return false;
            SaveSystem.Data.SetBooster(type, count - 1);
            SaveSystem.Commit();
            return true;
        }

        public static int PriceCoins(BoosterType type) => GameTuning.BoosterPriceCoins;

        public static int UnlockLevel(BoosterType type) => type switch
        {
            BoosterType.Vip => GameTuning.VipUnlockLevel,
            BoosterType.Arrange => GameTuning.ArrangeUnlockLevel,
            BoosterType.Shuffle => GameTuning.ShuffleUnlockLevel,
            _ => 1
        };

        public static bool IsUnlocked(BoosterType type, int level) => level >= UnlockLevel(type);

        /// <summary>The booster row is shown from this level on (hidden in the tutorial levels).</summary>
        public static bool RowVisible(int level) => level >= GameTuning.BoosterRowFromLevel;
    }

    /// <summary>Coins, stored in the existing PlayerPrefs save (<see cref="SaveSystem"/>).</summary>
    public static class CoinWallet
    {
        public static event Action Changed
        {
            add => SaveSystem.Changed += value;
            remove => SaveSystem.Changed -= value;
        }

        public static int Coins => SaveSystem.Data.coins;

        public static void Add(int amount) => SaveSystem.AddCoins(amount);

        public static bool TrySpend(int amount) => SaveSystem.TrySpendCoins(amount);
    }

    /// <summary>Rewarded ads. The game talks to this interface only; a real network plugs in later.</summary>
    public interface IAdService
    {
        /// <summary>Shows a rewarded ad; onDone(true) if the reward should be granted.</summary>
        void ShowRewarded(string placement, Action<bool> onDone);
    }

    /// <summary>Stub: no ad network yet, every rewarded ad succeeds at once.</summary>
    public sealed class NoAdService : IAdService
    {
        public void ShowRewarded(string placement, Action<bool> onDone) => onDone?.Invoke(true);
    }

    public static class Ads
    {
        public static IAdService Service { get; set; } = new NoAdService();
    }

    /// <summary>Sound-effect hook (no audio manager yet): calls go through here so clips can be added later.</summary>
    public static class Sfx
    {
        /// <summary>Raised for every request (an audio manager subscribes later).</summary>
        public static event Action<string> Requested;

        public static void Play(string id)
        {
            if (SaveSystem.Data.soundOn) Requested?.Invoke(id); // "Sound" off in Settings mutes every effect at once
        }
    }
}
