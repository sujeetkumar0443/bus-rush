using System.Globalization;
using TMPro;
using UnityEngine;

namespace BusRush.UI
{
    /// <summary>Shows the coin balance and counts up/down when it changes.</summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class CoinText : MonoBehaviour
    {
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
            bool gained = target > from;
            countAnim = Tween.Value(this, from, target, 0.45f, v =>
            {
                Render(Mathf.RoundToInt(v));
                if (gained && coinTicks < 6 && Time.unscaledTime >= nextCoinTick)   // coin clinks, each a semitone higher, during the count-up
                {
                    AudioManager.PlayRising(SfxId.Coin, coinTicks++, 0.7f);
                    nextCoinTick = Time.unscaledTime + 0.07f;
                }
            }, Ease.OutCubic);

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
