using System;

namespace BusRush.Gameplay
{
    /// <summary>
    /// Lucky Spin rules (pure, edit-mode tested): the prize is picked first by weight, then the wheel turns 6 full
    /// turns plus the angle that brings that segment under the top pointer. Angles are CSS-style: clockwise degrees,
    /// segment i spans [45 i, 45 i + 45) clockwise from the top of the wheel. Daily spin count lives in the save.
    /// </summary>
    public static class LuckySpin
    {
        public const int Segments = 8;
        public const float SegmentAngle = 360f / Segments;
        public const int FullTurns = 6;

        public static int TotalWeight
        {
            get
            {
                int sum = 0;
                foreach (int w in GameTuning.LuckySpinWeights) sum += w;
                return sum;
            }
        }

        /// <summary>Picks a segment by GameTuning.LuckySpinWeights.</summary>
        public static int Pick(Random rng)
        {
            int roll = rng.Next(TotalWeight);
            for (int i = 0; i < GameTuning.LuckySpinWeights.Length; i++)
            {
                roll -= GameTuning.LuckySpinWeights[i];
                if (roll < 0) return i;
            }
            return GameTuning.LuckySpinWeights.Length - 1;
        }

        /// <summary>The segment under the top pointer for a wheel turned clockwise by <paramref name="rotation"/>.</summary>
        public static int SegmentAt(float rotation)
        {
            float wheelAngle = Mod(-rotation, 360f); // wheel point now at the top
            return Math.Min(Segments - 1, (int)(wheelAngle / SegmentAngle));
        }

        /// <summary>
        /// Final rotation from <paramref name="current"/>: at least 6 full turns, landing segment <paramref name="segment"/>
        /// under the pointer, <paramref name="jitter"/> (-1..1) moving it off-centre by up to 40% of half a segment.
        /// </summary>
        public static float TargetRotation(float current, int segment, float jitter = 0f)
        {
            float centre = segment * SegmentAngle + SegmentAngle * 0.5f + Math.Clamp(jitter, -1f, 1f) * SegmentAngle * 0.2f;
            float wanted = Mod(-centre, 360f);
            float extra = Mod(wanted - Mod(current, 360f), 360f);
            return current + FullTurns * 360f + extra;
        }

        // ------------------------------------------------------------------ daily count

        public static int SpinsLeftToday
        {
            get
            {
                SaveData d = SaveSystem.Data;
                int used = d.luckySpinDate == DailyRewards.TodayKey ? d.luckySpinsUsed : 0;
                return Math.Max(0, GameTuning.LuckySpinsPerDay - used);
            }
        }

        public static void UseSpin()
        {
            SaveData d = SaveSystem.Data;
            if (d.luckySpinDate != DailyRewards.TodayKey)
            {
                d.luckySpinDate = DailyRewards.TodayKey;
                d.luckySpinsUsed = 0;
            }
            d.luckySpinsUsed++;
            SaveSystem.Commit();
        }

        private static float Mod(float x, float m) => ((x % m) + m) % m;
    }
}
