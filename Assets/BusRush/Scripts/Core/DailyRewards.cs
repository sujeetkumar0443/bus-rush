using System;
using System.Globalization;
using BusRush.Gameplay;

namespace BusRush
{
    /// <summary>
    /// 7-day daily reward cycle (rewards in GameTuning.DailyRewards), stored in SaveData: dailyStreak = 0-based day the
    /// next claim pays, lastDailyClaim = date of the last claim. One claim per calendar day (device local date). A
    /// missed day does not reset the cycle; after day 7 it starts again at day 1. Older saves (UTC timestamps) still load.
    /// </summary>
    public static class DailyRewards
    {
        public const int Days = 7;
        private const string DateFormat = "yyyy-MM-dd";

        /// <summary>Local clock; replaceable for tests.</summary>
        public static Func<DateTime> Now = () => DateTime.Now;

        public static DateTime Today => Now().Date;

        public static string TodayKey => Today.ToString(DateFormat, CultureInfo.InvariantCulture);

        /// <summary>Local date of the last claim.</summary>
        public static bool TryGetLastClaim(out DateTime lastDate)
        {
            string raw = SaveSystem.Data.lastDailyClaim;
            if (!string.IsNullOrEmpty(raw) &&
                DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime parsed))
            {
                lastDate = (parsed.Kind == DateTimeKind.Utc ? parsed.ToLocalTime() : parsed).Date;
                return true;
            }
            lastDate = default;
            return false;
        }

        /// <summary>Today is unclaimed (a clock set backwards never unlocks early).</summary>
        public static bool CanClaim => !TryGetLastClaim(out DateTime last) || Today > last;

        /// <summary>0-based index of the day the next claim pays out.</summary>
        public static int NextDayIndex => Math.Clamp(SaveSystem.Data.dailyStreak, 0, Days - 1);

        /// <summary>Days already claimed in the current cycle (shown as ticked cards).</summary>
        public static int ClaimedInCycle
        {
            get
            {
                // Right after day 7 the cycle shows complete until tomorrow.
                if (!CanClaim && NextDayIndex == 0 && TryGetLastClaim(out _)) return Days;
                return NextDayIndex;
            }
        }

        public static RewardSpec GetReward(int dayIndex) =>
            GameTuning.DailyRewards[Math.Clamp(dayIndex, 0, GameTuning.DailyRewards.Length - 1)];

        /// <summary>
        /// Claims today's reward times <paramref name="multiplier"/>: grants the boosters, advances the cycle and
        /// returns the reward. Coins are NOT added here (the popup adds them when the flying coins land).
        /// </summary>
        public static bool Claim(int multiplier, out RewardSpec reward)
        {
            reward = default;
            if (!CanClaim) return false;
            int day = NextDayIndex;
            reward = GetReward(day).Times(Math.Max(1, multiplier));
            reward.GrantBoosters();
            SaveData data = SaveSystem.Data;
            data.dailyStreak = (day + 1) % Days;
            data.lastDailyClaim = TodayKey;
            SaveSystem.Commit();
            return true;
        }

        /// <summary>True once per day: the Daily popup should open by itself on Home (today unclaimed, not shown yet).</summary>
        public static bool ShouldAutoOpen => CanClaim && SaveSystem.Data.dailyAutoShown != TodayKey;

        public static void MarkAutoOpened()
        {
            SaveSystem.Data.dailyAutoShown = TodayKey;
            SaveSystem.Commit();
        }
    }
}
