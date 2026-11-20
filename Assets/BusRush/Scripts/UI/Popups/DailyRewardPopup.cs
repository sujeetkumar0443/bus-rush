using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>7-day daily reward. Claim is real (see <see cref="DailyRewards"/>); x2 is an ad placeholder that only logs.</summary>
    public sealed class DailyRewardPopup : Popup
    {
        [SerializeField] private DayCardView[] cards = new DayCardView[DailyRewards.Days];
        [SerializeField] private UIButton claimButton;
        [SerializeField] private TMP_Text claimLabel;
        [SerializeField] private UIButton doubleButton;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private Sprite coinSprite;
        [SerializeField] private Sprite packSprite;

        private float nextTimerUpdate;

        protected override void Awake()
        {
            base.Awake();
            claimButton.Button.onClick.AddListener(Claim);
            doubleButton.Button.onClick.AddListener(() => MonetizationPlaceholder.ShowRewarded("daily_reward_x2"));
        }

        protected override void OnOpening()
        {
            Refresh();
        }

        private void Update()
        {
            if (!IsOpen || Time.unscaledTime < nextTimerUpdate) return;
            nextTimerUpdate = Time.unscaledTime + 0.5f;
            UpdateTimer();
        }

        private void Claim()
        {
            int day = DailyRewards.NextDayIndex;
            if (!DailyRewards.Claim()) return;

            Haptics.Medium();
            Refresh();
            if (day >= 0 && day < cards.Length) cards[day].PlayClaimed();
        }

        private void Refresh()
        {
            int next = DailyRewards.NextDayIndex;
            bool canClaim = DailyRewards.CanClaim;
            if (!canClaim && next == 0 && DailyRewards.TryGetLastClaim(out _))
            {
                // Day 7 was just claimed: show the finished cycle until the next day unlocks.
                next = DailyRewards.Days;
            }

            for (int i = 0; i < cards.Length; i++)
            {
                DayCardView.State state;
                if (i < next) state = DayCardView.State.Claimed;
                else if (i == next && canClaim) state = DayCardView.State.Today;
                else state = DayCardView.State.Upcoming;

                cards[i].Set(i, DailyRewards.GetReward(i), coinSprite, packSprite, state);
            }

            claimButton.SetInteractable(canClaim);
            doubleButton.SetInteractable(canClaim);
            claimLabel.text = canClaim ? "CLAIM" : "CLAIMED";
            UpdateTimer();
        }

        private void UpdateTimer()
        {
            if (DailyRewards.CanClaim)
            {
                timerText.text = "Your daily gift is ready!";
                if (!claimButton.Button.interactable) Refresh();
                return;
            }

            TimeSpan left = DailyRewards.TimeUntilNextClaim;
            timerText.text = $"Next gift in {(int)left.TotalHours:00}:{left.Minutes:00}:{left.Seconds:00}";
        }
    }
}
