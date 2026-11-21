using TMPro;
using UnityEngine;

namespace BusRush.UI
{
    /// <summary>Short message pill on the HUD ("No free bay!"). Pops in, holds, fades out.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class Toast : MonoBehaviour
    {
        /// <summary>All toasts alive (Home, Road Map, HUD); the newest active one shows the message.</summary>
        private static readonly System.Collections.Generic.List<Toast> instances = new System.Collections.Generic.List<Toast>();

        [SerializeField] private TMP_Text label;
        [SerializeField] private float holdTime = 1.1f;

        private CanvasGroup group;
        private Coroutine scaleAnim;
        private Coroutine fadeAnim;

        private void Awake()
        {
            if (!instances.Contains(this)) instances.Add(this);
            group = GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        private void OnDestroy()
        {
            instances.Remove(this);
        }

        public static void Show(string message)
        {
            for (int i = instances.Count - 1; i >= 0; i--)
            {
                Toast t = instances[i];
                if (t == null || !t.isActiveAndEnabled) continue;
                t.Display(message);
                return;
            }
        }

        private void Display(string message)
        {
            label.text = message;
            Tween.Stop(this, ref scaleAnim);
            Tween.Stop(this, ref fadeAnim);
            group.alpha = 1f;
            scaleAnim = Tween.Scale(this, transform, Vector3.one * 0.6f, Vector3.one, 0.22f, Ease.OutBack);
            fadeAnim = Tween.Fade(this, group, 1f, 0f, 0.3f, Ease.InQuad, holdTime);
        }
    }
}
