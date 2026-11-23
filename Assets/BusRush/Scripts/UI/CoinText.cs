using System.Globalization;
using TMPro;
using UnityEngine;

namespace BusRush.UI
{
    /// <summary>Shows the coin balance and counts up/down when it changes.</summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class CoinText : MonoBehaviour
    {
        public const float TickGap = 0.065f;            // minimum time between ticks
        public const float TickSemitonesPerCoin = 0.3f;
        public const float TickMaxSemitones = 8f;
        public const float DingVolume = 0.9f;

        /// <summary>Set by <see cref="CoinFly"/> around a count-up payout (level complete): the change then ticks and ends with a ding.</summary>
        public static bool CountUpSound;

        [SerializeField] private Transform punchTarget;

        private TMP_Text text;
        private int shown;
        private Coroutine countAnim;
        private Coroutine punchAnim;
        private int coinTicks;
        private float nextCoinTick;

        private void Awake()
        {
            text = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            shown = SaveSystem.Data.coins;
            Render(shown);
            SaveSystem.Changed += OnChanged;
        }

        private void OnDisable()
        {
            SaveSystem.Changed -= OnChanged;
        }

        private void OnChanged()
        {
            int target = SaveSystem.Data.coins;
            if (target == shown) return;

            int from = shown;
            shown = target;
            Tween.Stop(this, ref countAnim);
            coinTicks = 0;
            nextCoinTick = Time.unscaledTime;
            bool counting = target > from && CountUpSound;
            int lastShown = from;
            countAnim = Tween.Value(this, from, target, 0.45f, v =>
            {
                int now = Mathf.RoundToInt(v);
                Render(now);
                if (counting && now > lastShown && Time.unscaledTime >= nextCoinTick)   // one tick per coin step, pitch rising 0.3 semitone each
                {
                    float semis = Mathf.Min(coinTicks++ * TickSemitonesPerCoin, TickMaxSemitones);
                    AudioManager.PlaySemitones(SfxId.CoinTick, semis, 0.8f);
                    nextCoinTick = Time.unscaledTime + TickGap;
                }
                lastShown = now;
            }, Ease.OutCubic, 0f, counting ? (System.Action)(() => AudioManager.Play(SfxId.CoinDing, 1f, DingVolume)) : null);

            Transform punch = punchTarget != null ? punchTarget : transform;
            Tween.Stop(this, ref punchAnim);
            punchAnim = Tween.Scale(this, punch, Vector3.one * 1.18f, Vector3.one, 0.3f, Ease.OutBack);
        }

        private void Render(int value)
        {
            text.text = Format(value);
        }

        /// <summary>Compact coin text: 999, 1.2K, 12K, 123K, 1.2M, 12M (one decimal only below 10, truncated, never rounded up).</summary>
        public static string Format(int value)
        {
            if (value < 1000) return value.ToString(CultureInfo.InvariantCulture);
            return value < 1000000 ? Short(value, 1000, "K") : Short(value, 1000000, "M");
        }

        private static string Short(int value, int unit, string suffix)
        {
            if (value < 10 * unit)
            {
                int tenths = value / (unit / 10);
                return (tenths % 10 == 0 ? (tenths / 10).ToString(CultureInfo.InvariantCulture)
                    : (tenths / 10f).ToString("0.0", CultureInfo.InvariantCulture)) + suffix;
            }
            return (value / unit).ToString(CultureInfo.InvariantCulture) + suffix;
        }
    }
}
