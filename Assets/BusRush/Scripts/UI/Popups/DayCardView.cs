using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>One day card in the daily reward popup.</summary>
    public sealed class DayCardView : MonoBehaviour
    {
        public enum State
        {
            Claimed,
            Today,
            Upcoming
        }

        [SerializeField] private Image face;
        [SerializeField] private Image lip;
        [SerializeField] private TMP_Text dayLabel;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text amount;
        [SerializeField] private GameObject check;
        [SerializeField] private GameObject highlight;
        [SerializeField] private Transform content;

        private static readonly Color UpcomingFace = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
        private static readonly Color UpcomingLip = new Color32(0xC3, 0xCB, 0xDB, 0xFF);
        private static readonly Color TodayFace = new Color32(0xFF, 0xF1, 0xC2, 0xFF);
        private static readonly Color TodayLip = new Color32(0xD6, 0x9A, 0x12, 0xFF);
        private static readonly Color ClaimedFace = new Color32(0xCF, 0xF2, 0xD8, 0xFF);
        private static readonly Color ClaimedLip = new Color32(0x23, 0x86, 0x3D, 0xFF);

        private Coroutine pulse;

        public void Set(int dayIndex, GameConfig.DailyReward reward, Sprite coinSprite, Sprite packSprite, State state)
        {
            dayLabel.text = "DAY " + (dayIndex + 1);
            icon.sprite = reward.boosterPack ? packSprite : coinSprite;
            amount.text = reward.boosterPack ? "BOOSTERS" : reward.coins.ToString();

            switch (state)
            {
                case State.Claimed:
                    face.color = ClaimedFace;
                    lip.color = ClaimedLip;
                    break;
                case State.Today:
                    face.color = TodayFace;
                    lip.color = TodayLip;
                    break;
                default:
                    face.color = UpcomingFace;
                    lip.color = UpcomingLip;
                    break;
            }

            check.SetActive(state == State.Claimed);
            highlight.SetActive(state == State.Today);
            icon.color = state == State.Claimed ? new Color(1f, 1f, 1f, 0.45f) : Color.white;

            Tween.Stop(this, ref pulse);
            if (content != null) content.localScale = Vector3.one;
            if (state == State.Today && content != null && isActiveAndEnabled)
            {
                pulse = Tween.Pulse(this, content, 0.05f, 1f);
            }
        }

        public void PlayClaimed()
        {
            if (content != null) Tween.Scale(this, content, Vector3.one * 1.25f, Vector3.one, 0.35f, Ease.OutBack);
        }
    }
}
