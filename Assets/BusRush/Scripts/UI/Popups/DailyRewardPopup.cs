using System.Collections;
using BusRush.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>
    /// Daily Reward (UI v2, DailyReward.dc.html) on the blurred popup base: 6 day cards + the Day 7 chest card,
    /// "Claim" and "Claim x2" (rewarded video doubles everything). After a claim the card flips with a pop, the popup
    /// closes, coins fly to the Home coin pill and booster rewards show a toast. Logic: <see cref="DailyRewards"/>.
    /// </summary>
    public sealed class DailyRewardPopup : V2Popup
    {
        [SerializeField] private DailyCardView[] cards = new DailyCardView[DailyRewards.Days];
        [SerializeField] private Button claimButton;
        [SerializeField] private Button doubleButton;
        [SerializeField] private RectTransform claimPulse;
        [SerializeField] private RectTransform chest;
        [SerializeField] private Sprite coinSprite;

        private bool claiming;
        private float clock;

        protected override void Awake()
        {
            base.Awake();
            chestHomeY = chest.anchoredPosition.y;
            claimButton.onClick.AddListener(() => Claim(1));
            doubleButton.onClick.AddListener(() =>
            {
                if (claiming || !DailyRewards.CanClaim) return;
                Ads.Service.ShowRewarded("daily_reward_x2", granted =>
                {
                    if (this == null) return;
                    if (granted) Claim(2);
                    else Toast.Show(Localizer.T("Video not available"));
                });
            });
        }

        protected override void OnOpening()
        {
            base.OnOpening();
            claiming = false;
            clock = 0f;
            Refresh();
        }

        private void Refresh()
        {
            int claimed = DailyRewards.ClaimedInCycle;
            bool canClaim = DailyRewards.CanClaim;
            for (int i = 0; i < cards.Length; i++)
            {
                DailyCardView.State s = i < claimed ? DailyCardView.State.Claimed
                    : i == claimed && canClaim ? DailyCardView.State.Today : DailyCardView.State.Future;
                cards[i].SetState(s);
            }
            claimButton.interactable = doubleButton.interactable = canClaim;
        }

        private void Claim(int multiplier)
        {
            if (claiming) return;
            int day = DailyRewards.NextDayIndex;
            if (!DailyRewards.Claim(multiplier, out RewardSpec reward)) return;
            claiming = true;
            Haptics.Light();
            Sfx.Play("daily_claim");
            cards[day].PlayClaimed();
            StartCoroutine(Finish(cards[day].transform.position, reward));
        }

        private IEnumerator Finish(Vector3 from, RewardSpec reward)
        {
            yield return new WaitForSecondsRealtime(0.55f);
            Close();
            UIManager ui = UIManager.Instance;
            if (reward.coins > 0) CoinFly.Play(ui, ui != null ? ui.transform : null, coinSprite, from, HomeScreen.CoinTarget, reward.coins, 8);
            if (reward.HasBoosters) Toast.Show(reward.BoosterText());
        }

        protected override void Update()
        {
            base.Update();
            if (!IsOpen) return;
            clock += Time.unscaledDeltaTime;
            float pulse = 1f + 0.05f * (0.5f - 0.5f * Mathf.Cos(clock / 1.2f * Mathf.PI * 2f));           // pulse 1.2 s
            claimPulse.localScale = Vector3.one * (claimButton.interactable ? pulse : 1f);
            float floaty = 6f * (0.5f - 0.5f * Mathf.Cos(clock / 2.2f * Mathf.PI * 2f));                   // floaty 2.2 s
            chest.anchoredPosition = new Vector2(chest.anchoredPosition.x, chestHomeY + floaty);
        }

        private float chestHomeY;
    }
}
