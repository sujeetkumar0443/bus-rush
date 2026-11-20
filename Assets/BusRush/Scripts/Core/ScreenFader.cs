using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush
{
    /// <summary>
    /// Persistent full-screen black overlay used by <see cref="SceneLoader"/>.
    /// Created at runtime; not meant to be placed in a scene by hand.
    /// </summary>
    public sealed class ScreenFader : MonoBehaviour
    {
        private const int SortingOrder = 32000;

        private CanvasGroup group;

        public float Alpha => group != null ? group.alpha : 0f;

        public static ScreenFader Create()
        {
            var go = new GameObject("[TransitionCanvas]");
            DontDestroyOnLoad(go);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            go.AddComponent<GraphicRaycaster>();

            var fader = go.AddComponent<ScreenFader>();
            fader.group = go.AddComponent<CanvasGroup>();

            var imageGo = new GameObject("Black", typeof(RectTransform));
            imageGo.transform.SetParent(go.transform, false);
            var rect = (RectTransform)imageGo.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = imageGo.AddComponent<Image>();
            image.color = Color.black;

            fader.SetAlpha(0f);
            return fader;
        }

        public void SetAlpha(float alpha)
        {
            group.alpha = alpha;
            // Block input whenever the screen is (partly) covered.
            group.blocksRaycasts = alpha > 0.001f;
            group.interactable = false;
        }

        public IEnumerator FadeTo(float target, float duration)
        {
            float start = group.alpha;
            if (duration <= 0f)
            {
                SetAlpha(target);
                yield break;
            }

            group.blocksRaycasts = true;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                SetAlpha(Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / duration)));
                yield return null;
            }
            SetAlpha(target);
        }
    }
}
