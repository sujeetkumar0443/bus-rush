using System;
using System.Collections.Generic;
using UnityEngine;

namespace BusRush
{
    [Serializable]
    public sealed class SaveData
    {
        public int version = 1;

        public int level = 1;
        public int coins = Gameplay.GameTuning.StartingCoins;
        public int lives = 5;

        public bool soundOn = true;
        public bool musicOn = true;
        public bool vibrationOn = true;

        public int boosterVip = Gameplay.GameTuning.StartingBoosters;
        public int boosterArrange = Gameplay.GameTuning.StartingBoosters;
        public int boosterShuffle = Gameplay.GameTuning.StartingBoosters;

        /// <summary>UTC date of the last daily reward claim, ISO 8601 ("o"); empty if never claimed.</summary>
        public string lastDailyClaim = string.Empty;

        /// <summary>Daily reward days claimed in the current 7-day cycle (0..6).</summary>
        public int dailyStreak;

        /// <summary>Local date ("yyyy-MM-dd") the Daily popup last opened by itself on Home.</summary>
        public string dailyAutoShown = string.Empty;

        /// <summary>Local date of the Lucky Spin counter, and spins used on that date.</summary>
        public string luckySpinDate = string.Empty;
        public int luckySpinsUsed;

        /// <summary>Highest level whose Road Map unlock animation has already played (never replays).</summary>
        public int mapUnlockShown;

        /// <summary>Levels whose Road Map gift chest was collected (5, 15, 25...).</summary>
        public List<int> giftChestsClaimed = new List<int>();

        /// <summary>Highest world (1-based) whose "New World" screen has been shown; world 1 needs none.</summary>
        public int worldsShown = 1;

        /// <summary>Best stars per level (index = level - 1; 0 = not completed).</summary>
        public List<int> levelStars = new List<int>();

        /// <summary>Bit n set = tutorial of level n already completed (never shown again).</summary>
        public int tutorialFlags;

        /// <summary>"You finished all levels" note already shown.</summary>
        public bool finishedAllShown;

        public static SaveData CreateDefault() => new SaveData();

        public int GetStars(int level)
        {
            int i = level - 1;
            return levelStars != null && i >= 0 && i < levelStars.Count ? levelStars[i] : 0;
        }

        /// <summary>Stores the stars if better than the previous best. Returns the best.</summary>
        public int SetBestStars(int level, int stars)
        {
            levelStars ??= new List<int>();
            int i = level - 1;
            if (i < 0) return 0;
            while (levelStars.Count <= i) levelStars.Add(0);
            levelStars[i] = Mathf.Max(levelStars[i], Mathf.Clamp(stars, 0, 3));
            return levelStars[i];
        }

        public bool IsTutorialDone(int level) => level >= 1 && level < 31 && (tutorialFlags & (1 << level)) != 0;

        public void MarkTutorialDone(int level)
        {
            if (level >= 1 && level < 31) tutorialFlags |= 1 << level;
        }

        public int GetBooster(BoosterType type)
        {
            return type switch
            {
                BoosterType.Vip => boosterVip,
                BoosterType.Arrange => boosterArrange,
                BoosterType.Shuffle => boosterShuffle,
                _ => 0
            };
        }

        public void SetBooster(BoosterType type, int count)
        {
            count = Mathf.Max(0, count);
            switch (type)
            {
                case BoosterType.Vip: boosterVip = count; break;
                case BoosterType.Arrange: boosterArrange = count; break;
                case BoosterType.Shuffle: boosterShuffle = count; break;
            }
        }
    }

    /// <summary>PlayerPrefs-backed JSON save.</summary>
    public static class SaveSystem
    {
        private const string PrefsKey = "BusRush.Save";

        private static SaveData data;

        /// <summary>Current save data; loads on first access.</summary>
        public static SaveData Data => data ??= Load();

        public static event Action<SaveData> Saved;

        /// <summary>Raised after any gameplay-visible value changed (coins, lives, boosters, level, settings).</summary>
        public static event Action Changed;

        /// <summary>Saves and raises <see cref="Changed"/>. Call after modifying <see cref="Data"/> directly.</summary>
        public static void Commit()
        {
            Save();
            Changed?.Invoke();
        }

        public static void AddCoins(int amount)
        {
            Data.coins = Mathf.Max(0, Data.coins + amount);
            Commit();
        }

        public static bool TrySpendCoins(int amount)
        {
            if (amount < 0 || Data.coins < amount)
            {
                return false;
            }
            Data.coins -= amount;
            Commit();
            return true;
        }

        public static void AddBooster(BoosterType type, int amount)
        {
            Data.SetBooster(type, Data.GetBooster(type) + amount);
            Commit();
        }

        public static SaveData Load()
        {
            string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
            SaveData loaded = null;

            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    loaded = JsonUtility.FromJson<SaveData>(json);
                }
                catch (ArgumentException e)
                {
                    Debug.LogWarning($"[BusRush] Corrupt save data, resetting. {e.Message}");
                }
            }

            data = loaded ?? SaveData.CreateDefault();
            return data;
        }

        public static void Save()
        {
            if (data == null)
            {
                return;
            }
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
            Saved?.Invoke(data);
        }

        public static void ResetToDefaults()
        {
            data = SaveData.CreateDefault();
            Commit();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            data = null;
            Saved = null;
            Changed = null;
        }
    }
}
