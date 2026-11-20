using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>Full-screen red vignette flash for wrong taps. Lives on the Game HUD.</summary>
    [RequireComponent(typeof(Image))]
    public sealed class ScreenFlash : MonoBehaviour
    {
        private static ScreenFlash instance;

        [SerializeField, Range(0f, 1f)] private float peakAlpha = 0.35f;
        [SerializeField] private float duration = 0.3f;

        private Image image;
        private Coroutine anim;

        private void Awake()
        {
            instance = this;
            image = GetComponent<Image>();
            image.raycastTarget = false;
            SetAlpha(0f);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        /// <summary>Plays the flash on the active HUD (no-op if there is none).</summary>
        public static void Play()
        {
            if (instance != null && instance.isActiveAndEnabled) instance.Flash();
        }

        public void Flash()
        {
            Tween.Stop(this, ref anim);
            anim = Tween.Run(this, duration, k =>
            {
                // Quick rise (first 25%), then fade out.
                float a = k < 0.25f ? k / 0.25f : 1f - (k - 0.25f) / 0.75f;
                SetAlpha(a * peakAlpha);
            }, Ease.Linear, 0f, () => SetAlpha(0f));
        }

        private void SetAlpha(float a)
        {
            Color c = image.color;
            c.a = a;
            image.color = c;
        }
    }
}
