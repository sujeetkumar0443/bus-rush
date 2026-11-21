using System;
using UnityEngine;

namespace BusRush
{
    /// <summary>Global tuning values. Lives at Resources/GameConfig.asset.</summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Bus Rush/Game Config")]
    public sealed class GameConfig : ScriptableObject
    {
        public const string ResourcePath = "GameConfig";

        [Serializable]
        public struct PaletteEntry
        {
            public GameColor id;
            public Color color;
            public Material material;
            [Tooltip("Shared passenger body material for this colour (SRP Batcher friendly, no property blocks).")]
            public Material passengerBody;
            [Tooltip("Shared passenger cap material (darker shade of the body colour).")]
            public Material passengerCap;
        }

        [Header("Grid")]
        [Min(1)] public int gridWidth = 9;
        [Min(1)] public int gridHeight = 10;
        [Min(0.1f)] public float cellSize = 1f;

        [Header("Vehicle capacity (passengers)")]
        [Min(1)] public int carCapacity = 4;
        [Min(1)] public int vanCapacity = 6;
        [Min(1)] public int busCapacity = 8;

        [Header("Vehicle length (cells)")]
        [Min(1)] public int carLength = 2;
        [Min(1)] public int vanLength = 2;
        [Min(1)] public int busLength = 3;

        [Header("Parking bays")]
        [Min(1)] public int openBays = 5;
        [Min(0)] public int lockedBays = 3;

        [Header("Economy")]
        [Min(0)] public int extraBayCost = 300;
        [Min(0)] public int freeCoinsAmount = 50;
        [Min(1)] public int maxLives = 5;

        [Serializable]
        public struct DailyReward
        {
            public int coins;
            [Tooltip("Grants one of each booster.")]
            public bool boosterPack;
        }

        [Header("Daily reward (day 1..7)")]
        public DailyReward[] dailyRewards =
        {
            new DailyReward { coins = 50 },
            new DailyReward { coins = 75 },
            new DailyReward { coins = 100 },
            new DailyReward { coins = 150 },
            new DailyReward { coins = 200 },
            new DailyReward { coins = 300 },
            new DailyReward { coins = 0, boosterPack = true }
        };

        [Serializable]
        public struct Unlock
        {
            public int level;
            public string name;
        }

        [Header("Feature unlocks shown on Home")]
        public Unlock[] unlocks =
        {
            new Unlock { level = 13, name = "Shuffle booster" },
            new Unlock { level = 20, name = "Snow world" },
            new Unlock { level = 40, name = "Beach world" }
        };

        [Header("Colours (one entry per GameColor, in enum order)")]
        public PaletteEntry[] palette = Array.Empty<PaletteEntry>();

        public int TotalBays => openBays + lockedBays;

        /// <summary>Booster price (coins); see GameTuning / BoosterInventory.</summary>
        public int GetBoosterCost(BoosterType type) => BoosterInventory.PriceCoins(type);

        /// <summary>Booster unlock level; see GameTuning / BoosterInventory.</summary>
        public int GetBoosterUnlockLevel(BoosterType type) => BoosterInventory.UnlockLevel(type);

        /// <summary>Next unlock at or above <paramref name="level"/>, and the previous milestone level (for progress).</summary>
        public bool TryGetNextUnlock(int level, out Unlock next, out int previousLevel)
        {
            previousLevel = 1;
            if (unlocks != null)
            {
                foreach (Unlock u in unlocks)
                {
                    if (u.level > level)
                    {
                        next = u;
                        return true;
                    }
                    previousLevel = u.level;
                }
            }
            next = default;
            return false;
        }

        private static GameConfig instance;

        /// <summary>Like <see cref="Instance"/> but returns null silently if the asset is missing.</summary>
        public static GameConfig TryGet()
        {
            if (instance == null) instance = Resources.Load<GameConfig>(ResourcePath);
            return instance;
        }

        public static GameConfig Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = Resources.Load<GameConfig>(ResourcePath);
                    if (instance == null)
                    {
                        Debug.LogError($"[BusRush] GameConfig not found at Resources/{ResourcePath}.");
                    }
                }
                return instance;
            }
        }

        public int GetCapacity(VehicleType type)
        {
            return type switch
            {
                VehicleType.Car => carCapacity,
                VehicleType.Van => vanCapacity,
                VehicleType.Bus => busCapacity,
                _ => carCapacity
            };
        }

        public int GetLength(VehicleType type)
        {
            return type switch
            {
                VehicleType.Car => carLength,
                VehicleType.Van => vanLength,
                VehicleType.Bus => busLength,
                _ => carLength
            };
        }

        public bool TryGetPaletteEntry(GameColor color, out PaletteEntry entry)
        {
            int index = (int)color;
            if (palette != null && index < palette.Length && palette[index].id == color)
            {
                entry = palette[index];
                return true;
            }

            if (palette != null)
            {
                foreach (PaletteEntry e in palette)
                {
                    if (e.id == color)
                    {
                        entry = e;
                        return true;
                    }
                }
            }

            entry = default;
            return false;
        }

        public bool IsPaletteMaterial(Material material)
        {
            if (material == null || palette == null)
            {
                return false;
            }
            foreach (PaletteEntry e in palette)
            {
                if (e.material == material)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
