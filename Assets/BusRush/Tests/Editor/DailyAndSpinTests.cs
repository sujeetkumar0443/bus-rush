using System;
using BusRush.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace BusRush.Tests
{
    /// <summary>Daily reward cycle (local calendar days, no reset on a missed day) and Lucky Spin landing / weights.</summary>
    public sealed class DailyAndSpinTests
    {
        private const string SaveKey = "BusRush.Save";
        private string backup;
        private Func<DateTime> clock;
        private DateTime now;

        [SetUp]
        public void SetUp()
        {
            backup = PlayerPrefs.GetString(SaveKey, null);
            clock = DailyRewards.Now;
            SaveSystem.ResetToDefaults();
            now = new DateTime(2026, 3, 10, 9, 30, 0, DateTimeKind.Local);
            DailyRewards.Now = () => now;
        }

        [TearDown]
        public void TearDown()
        {
            DailyRewards.Now = clock;
            if (string.IsNullOrEmpty(backup)) PlayerPrefs.DeleteKey(SaveKey);
            else PlayerPrefs.SetString(SaveKey, backup);
            PlayerPrefs.Save();
            SaveSystem.Load();
        }

        [Test]
        public void Daily_CycleContinues_NoResetOnMissedDays_WrapsAfterDay7()
        {
            Assert.IsTrue(DailyRewards.CanClaim, "first launch: claimable");
            Assert.AreEqual(0, DailyRewards.NextDayIndex);
            Assert.IsTrue(DailyRewards.Claim(1, out RewardSpec day1));
            Assert.AreEqual(GameTuning.DailyRewards[0].coins, day1.coins);
            Assert.IsFalse(DailyRewards.CanClaim, "one claim per calendar day");
            Assert.IsFalse(DailyRewards.Claim(1, out _));

            now = now.Date.AddDays(1).AddHours(0.1); // just after midnight: a new calendar day
            Assert.IsTrue(DailyRewards.CanClaim);
            Assert.AreEqual(1, DailyRewards.NextDayIndex, "next day = day 2");
            Assert.IsTrue(DailyRewards.Claim(1, out _));

            now = now.AddDays(4); // skipped 3 days
            Assert.AreEqual(2, DailyRewards.NextDayIndex, "a missed day does not reset the streak");
            int arrange = BoosterInventory.Count(BoosterType.Arrange);
            Assert.IsTrue(DailyRewards.Claim(2, out RewardSpec day3));
            Assert.AreEqual(2, day3.arrange, "x2 doubles the booster too");
            Assert.AreEqual(arrange + 2, BoosterInventory.Count(BoosterType.Arrange), "boosters granted on claim");

            for (int day = 3; day < DailyRewards.Days; day++)
            {
                now = now.AddDays(1);
                Assert.AreEqual(day, DailyRewards.NextDayIndex);
                Assert.IsTrue(DailyRewards.Claim(1, out _));
            }
            Assert.AreEqual(DailyRewards.Days, DailyRewards.ClaimedInCycle, "day 7 done: the cycle shows complete today");
            now = now.AddDays(1);
            Assert.AreEqual(0, DailyRewards.NextDayIndex, "after day 7 it starts again at day 1");
            Assert.IsTrue(DailyRewards.CanClaim);
        }

        [Test]
        public void Daily_ClockSetBackwards_DoesNotUnlock()
        {
            Assert.IsTrue(DailyRewards.Claim(1, out _));
            now = now.AddDays(-2);
            Assert.IsFalse(DailyRewards.CanClaim);
        }

        [Test]
        public void Spin_AlwaysStopsOnTheChosenSegment()
        {
            var rng = new System.Random(3);
            for (int start = 0; start < 50; start++)
            {
                float current = (float)(rng.NextDouble() * 5000.0);
                for (int segment = 0; segment < LuckySpin.Segments; segment++)
                {
                    float jitter = (float)(rng.NextDouble() * 2.0 - 1.0);
                    float target = LuckySpin.TargetRotation(current, segment, jitter);
                    Assert.AreEqual(segment, LuckySpin.SegmentAt(target), $"start {current}, segment {segment}");
                    Assert.GreaterOrEqual(target - current, LuckySpin.FullTurns * 360f, "at least 6 full turns");
                    Assert.Less(target - current, (LuckySpin.FullTurns + 1) * 360f);
                }
            }
        }

        [Test]
        public void Spin_WeightsAddUpTo100_AndPickFollowsThem()
        {
            Assert.AreEqual(100, LuckySpin.TotalWeight);
            Assert.AreEqual(LuckySpin.Segments, GameTuning.LuckySpinWeights.Length);
            Assert.AreEqual(LuckySpin.Segments, GameTuning.LuckySpinSegments.Length);
            var counts = new int[LuckySpin.Segments];
            var rng = new System.Random(11);
            const int n = 20000;
            for (int i = 0; i < n; i++) counts[LuckySpin.Pick(rng)]++;
            for (int i = 0; i < counts.Length; i++)
            {
                Assert.AreEqual(GameTuning.LuckySpinWeights[i] / 100.0, counts[i] / (double)n, 0.015, $"segment {i}");
            }
        }
    }
}
