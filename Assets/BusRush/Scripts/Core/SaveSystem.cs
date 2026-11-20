using System;
using UnityEngine;

namespace BusRush
{
    [Serializable]
    public sealed class SaveData
    {
        public int version = 1;

        public int level = 1;
        public int coins = 100;
        public int lives = 5;

        public bool soundOn = true;
        public bool musicOn = true;
        public bool vibrationOn = true;

        public int boosterVip = 2;
        public int boosterArrange = 2;
        public int boosterShuffle = 2;

        /// <summary>UTC date of the last daily reward claim, ISO 8601 ("o"); empty if never claimed.</summary>
        public string lastDailyClaim = string.Empty;

        /// <summary>Daily reward days claimed in the current 7-day cycle (0..6).</summary>
        public int dailyStreak;

        public bool adsRemoved;

        public static SaveData CreateDefault() => new SaveData();

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
