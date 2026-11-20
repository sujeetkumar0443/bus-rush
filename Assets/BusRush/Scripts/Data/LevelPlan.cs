using UnityEngine;

namespace BusRush
{
    public enum LevelTier
    {
        Easy,
        Normal,
        Hard,
        Boss
    }

    /// <summary>Generator parameters for one level.</summary>
    public struct LevelSpec
    {
        public int level;
        public LevelTier tier;
        public int vehicleCount;
        public int colorCount;
        public float vanShare;
        public float busShare;
        /// <summary>Maximum share of the 90 lot cells that may be occupied.</summary>
        public float density;
        /// <summary>How strongly the generator stacks vehicles in front of each other (puzzle depth).</summary>
        public float blockBias;

        public override string ToString() =>
            $"L{level,3} {tier,-6} vehicles {vehicleCount,2} colours {colorCount} vans {vanShare:P0} buses {busShare:P0} density {density:P0}";
    }

    /// <summary>
    /// Difficulty curve for levels 1-100 (all numbers in this file, easy to tweak).
    /// <list type="bullet">
    /// <item>1-5: very easy (8-10 vehicles, 3 colours, mostly cars).</item>
    /// <item>6-60: steady rise to ~30 vehicles and 6-7 colours, more vans/buses and density.</item>
    /// <item>Every 10th level is a Boss (more vehicles and buses, tighter layout); the level after a Boss is a breather.</item>
    /// <item>101+: reuse the Hard (92-99) and Boss (100) parameters; the seed (level * 7919) keeps boards different.</item>
    /// </list>
    /// </summary>
    public static class LevelPlan
    {
        public const int PlannedLevels = 100;

        // ---------------------------------------------------------------- tweakables
        private static readonly int[] EarlyVehicles = { 8, 8, 9, 9, 10 }; // levels 1-5

        private const int StartVehicles = 11;   // level 6
        private const int PlateauVehicles = 30; // reached around level 60
        private const int LateBonusMax = 2;     // +1 at level 80, +2 at level 100

        private const float VanStart = 0.12f, VanEnd = 0.30f;
        private const float BusStart = 0.03f, BusEnd = 0.25f;
        private const float DensityStart = 0.50f, DensityEnd = 0.82f;
        private const float BiasStart = 0.8f, BiasEnd = 3.5f;

        private const int BossExtraVehicles = 5;
        private const float BossExtraBuses = 0.12f;
        private const float BossExtraDensity = 0.06f;
        private const float BossExtraBias = 1.5f;
        private const int MaxVehicles = 36;
        private const float MaxDensity = 0.9f;

        private const int BreatherFewerVehicles = 4;
        private const float BreatherLessDensity = 0.06f;

        /// <summary>Colours by level: 3 (1-5), 4 (6-12), 5 (13-25), 6 (26-45), 7 (46+).</summary>
        private static int ColorsFor(int level) =>
            level <= 5 ? 3 : level <= 12 ? 4 : level <= 25 ? 5 : level <= 45 ? 6 : 7;

        // ---------------------------------------------------------------- lookup

        public static bool IsBoss(int level) => level >= 10 && level % 10 == 0;

        public static bool IsBreather(int level) => level > 10 && level % 10 == 1;

        public static LevelSpec Get(int level)
        {
            level = Mathf.Max(1, level);
            if (level > PlannedLevels)
            {
                // Endless: boss levels reuse level 100, the rest cycle through the Hard levels 92-99
                // (91 is the breather after boss 90).
                int source = IsBoss(level) ? PlannedLevels : 92 + (level - 1) % 8;
                LevelSpec reused = Get(source);
                reused.level = level;
                return reused;
            }

            if (level <= EarlyVehicles.Length)
            {
                return new LevelSpec
                {
                    level = level,
                    tier = LevelTier.Easy,
                    vehicleCount = EarlyVehicles[level - 1],
                    colorCount = 3,
                    vanShare = 0.1f,
                    busShare = 0f,
                    density = 0.42f,
                    blockBias = 0.4f
                };
            }

            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((level - 6) / 54f));
            int late = level > 60 ? Mathf.Min(LateBonusMax, (level - 60) / 20) : 0;
            var spec = new LevelSpec
            {
                level = level,
                tier = level >= 40 ? LevelTier.Hard : LevelTier.Normal,
                vehicleCount = Mathf.RoundToInt(Mathf.Lerp(StartVehicles, PlateauVehicles, t)) + late,
                colorCount = ColorsFor(level),
                vanShare = Mathf.Lerp(VanStart, VanEnd, t),
                busShare = Mathf.Lerp(BusStart, BusEnd, t),
                density = Mathf.Lerp(DensityStart, DensityEnd, t),
                blockBias = Mathf.Lerp(BiasStart, BiasEnd, t)
            };

            if (IsBoss(level))
            {
                spec.tier = LevelTier.Boss;
                spec.vehicleCount = Mathf.Min(MaxVehicles, spec.vehicleCount + BossExtraVehicles);
                spec.busShare += BossExtraBuses;
                spec.density = Mathf.Min(MaxDensity, spec.density + BossExtraDensity);
                spec.blockBias += BossExtraBias;
            }
            else if (IsBreather(level))
            {
                spec.tier = LevelTier.Easy;
                spec.vehicleCount -= BreatherFewerVehicles;
                spec.density -= BreatherLessDensity;
            }
            return spec;
        }

        /// <summary>World (City, Snow, Beach, repeat) changes every 5 levels.</summary>
        public static int WorldIndex(int level, int worldCount = 3) =>
            worldCount <= 0 ? 0 : ((Mathf.Max(1, level) - 1) / 5) % worldCount;
    }
}
