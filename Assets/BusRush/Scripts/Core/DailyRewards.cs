using System;
using System.Globalization;

namespace BusRush
{
    /// <summary>
    /// 7-day daily reward cycle stored in SaveData (dailyStreak + lastDailyClaim).
    /// A claim unlocks 24 h after the previous one; missing a full day (48 h) restarts the cycle at day 1.
    /// </summary>
    public static class DailyRewards
    {
        public const int Days = 7;

        public static readonly TimeSpan Cooldown = TimeSpan.FromHours(24);
        public static readonly TimeSpan StreakGrace = TimeSpan.FromHours(48);

        /// <summary>Clock source; replaceable for tests.</summary>
        public static Func<DateTime> UtcNow = () => DateTime.UtcNow;

        public static bool TryGetLastClaim(out DateTime lastUtc)
        {
            string raw = SaveSystem.Data.lastDailyClaim;
            if (!string.IsNullOrEmpty(raw) &&
                DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out lastUtc))
            {
                lastUtc = lastUtc.ToUniversalTime();
                return true;
            }
            lastUtc = default;
            return false;
        }

        /// <summary>True if the streak is broken and the next claim restarts at day 1.</summary>
        public static bool StreakExpired => TryGetLastClaim(out DateTime last) && UtcNow() - last >= StreakGrace;

        /// <summary>0-based index of the day that the next claim pays out.</summary>
        public static int NextDayIndex => StreakExpired ? 0 : Math.Clamp(SaveSystem.Data.dailyStreak, 0, Days - 1);

        public static bool CanClaim
        {
            get
            {
                if (!TryGetLastClaim(out DateTime last))
                {
                    return true;
                }
                TimeSpan elapsed = UtcNow() - last;
                // A clock set backwards never unlocks early.
                return elapsed >= Cooldown;
            }
        }

        public static TimeSpan TimeUntilNextClaim
        {
            get
            {
                if (!TryGetLastClaim(out DateTime last))
                {
                    return TimeSpan.Zero;
                }
                TimeSpan elapsed = UtcNow() - last;
                if (elapsed < TimeSpan.Zero)
                {
                    return Cooldown;
                }
                TimeSpan left = Cooldown - elapsed;
                return left > TimeSpan.Zero ? left : TimeSpan.Zero;
            }
        }

        public static GameConfig.DailyReward GetReward(int dayIndex)
        {
            GameConfig config = GameConfig.Instance;
            if (config != null && config.dailyRewards != null && dayIndex >= 0 && dayIndex < config.dailyRewards.Length)
            {
                return config.dailyRewards[dayIndex];
            }
            return new GameConfig.DailyReward { coins = 50 };
        }

        /// <summary>Grants today's reward (times <paramref name="multiplier"/>) and advances the streak.</summary>
        public static bool Claim(int multiplier = 1)
        {
            if (!CanClaim)
            {
                return false;
            }

            int day = NextDayIndex;
            GameConfig.DailyReward reward = GetReward(day);
            SaveData data = SaveSystem.Data;

            data.coins += reward.coins * multiplier;
            if (reward.boosterPack)
            {
                foreach (BoosterType type in (BoosterType[])Enum.GetValues(typeof(BoosterType)))
                {
                    data.SetBooster(type, data.GetBooster(type) + multiplier);
                }
            }

            data.dailyStreak = (day + 1) % Days;
            data.lastDailyClaim = UtcNow().ToString("o", CultureInfo.InvariantCulture);
            SaveSystem.Commit();
            return true;
        }
    }
}
