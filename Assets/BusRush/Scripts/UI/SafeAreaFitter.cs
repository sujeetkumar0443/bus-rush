using UnityEngine;

namespace BusRush.UI
{
    /// <summary>Stretches this RectTransform to the device safe area (notches, rounded corners, gesture bars).</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        /// <summary>Optional normalized safe area override (0..1), used by editor previews to simulate a notch.</summary>
        public static Rect? Simulated;

        private RectTransform rect;
        private Rect applied = new Rect(-1f, -1f, -1f, -1f);

        private void Awake()
        {
            rect = (RectTransform)transform;
            Apply();
        }

        private void OnEnable() => Apply();

        private void LateUpdate() => Apply();

        public static Rect CurrentNormalized()
        {
            if (Simulated.HasValue) return Simulated.Value;
            if (Screen.width <= 0 || Screen.height <= 0) return new Rect(0f, 0f, 1f, 1f);
            Rect s = Screen.safeArea;
            return new Rect(s.x / Screen.width, s.y / Screen.height, s.width / Screen.width, s.height / Screen.height);
        }

        private void Apply()
        {
            if (rect == null) rect = (RectTransform)transform;
            Rect n = CurrentNormalized();
            if (n == applied) return;

            applied = n;
            rect.anchorMin = n.min;
            rect.anchorMax = n.max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
