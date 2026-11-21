using UnityEngine;

namespace BusRush.UI
{
    /// <summary>CSS "0%,100% {translateY(0) rotate(0)} 50% {translateY(-y) rotate(r)}" ease-in-out loop (e.g. the map chest).</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class UiLoopBob : MonoBehaviour
    {
        [Tooltip("Rise at the half-way point (UI units, up).")]
        [SerializeField] private float rise = 4f;
        [Tooltip("CSS rotate at the half-way point (degrees, clockwise positive like CSS).")]
        [SerializeField] private float cssRotate = -3f;
        [SerializeField, Min(0.05f)] private float period = 2f;

        private Vector2 home;
        private bool homeSet;

        private void OnEnable()
        {
            if (homeSet) return;
            home = ((RectTransform)transform).anchoredPosition;
            homeSet = true;
        }

        private void Update()
        {
            float k = Time.time / period % 1f;
            float e = CssCurve.Bezier(CssCurve.EaseInOut, k < 0.5f ? k * 2f : 2f - k * 2f);
            ((RectTransform)transform).anchoredPosition = home + new Vector2(0f, rise * e);
            transform.localRotation = Quaternion.Euler(0f, 0f, -cssRotate * e);
        }
    }
}
