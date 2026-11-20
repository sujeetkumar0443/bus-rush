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
            countAnim = Tween.Value(this, from, target, 0.45f, v => Render(Mathf.RoundToInt(v)), Ease.OutCubic);

            Transform punch = punchTarget != null ? punchTarget : transform;
            Tween.Stop(this, ref punchAnim);
            punchAnim = Tween.Scale(this, punch, Vector3.one * 1.18f, Vector3.one, 0.3f, Ease.OutBack);
        }

        private void Render(int value)
        {
            text.text = Format(value);
        }

        public static string Format(int value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }
    }
}
