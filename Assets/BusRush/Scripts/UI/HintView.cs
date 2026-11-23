using TMPro;
using UnityEngine;

namespace BusRush.UI
{
    /// <summary>
    /// Tutorial / idle hint overlay on the HUD: a pointing hand that taps toward a screen point, and a speech bubble.
    /// Driven by the gameplay tutorial controller; positions are given in screen pixels every frame.
    /// </summary>
    public sealed class HintView : MonoBehaviour
    {
        public static HintView Instance { get; private set; }

        [SerializeField] private RectTransform area;
        [SerializeField] private RectTransform hand;
        [SerializeField] private RectTransform bubble;
        [SerializeField] private TMP_Text bubbleText;
        [SerializeField] private RectTransform bubbleTail;
        [SerializeField] private float bubbleGap = 70f;
        [SerializeField] private float tapDistance = 14f;

        private float time;
        private bool softPulse;

        private void Awake()
        {
            Instance = this;
            Hide();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Points the fingertip at a screen position. soft = idle hint (gentle pulse instead of tapping).</summary>
        public void ShowHand(Vector2 screenPoint, bool soft = false)
        {
            softPulse = soft;
            if (!hand.gameObject.activeSelf)
            {
                hand.gameObject.SetActive(true);
                time = 0f;
            }
            hand.anchoredPosition = ToLocal(screenPoint);
        }

        public void HideHand() => hand.gameObject.SetActive(false);

        /// <summary>Speech bubble above (or below, if there is no room) a screen position.</summary>
        public void ShowBubble(string text, Vector2 screenPoint, bool preferBelow = false)
        {
            string shown = Localizer.T(text);
            if (bubbleText.text != shown) bubbleText.text = shown;
            bubble.gameObject.SetActive(true);

            Vector2 local = ToLocal(screenPoint);
            Rect r = area.rect;
            Vector2 size = bubble.sizeDelta;
            bool above = !preferBelow && local.y + bubbleGap + size.y < r.yMax - 10f;
            float y = above ? local.y + bubbleGap + size.y * 0.5f : local.y - bubbleGap - size.y * 0.5f;
            float x = Mathf.Clamp(local.x, r.xMin + size.x * 0.5f + 8f, r.xMax - size.x * 0.5f - 8f);
            bubble.anchoredPosition = new Vector2(x, y);
            if (bubbleTail != null)
            {
                bubbleTail.anchoredPosition = new Vector2(Mathf.Clamp(local.x - x, -size.x * 0.5f + 20f, size.x * 0.5f - 20f),
                                                          above ? -size.y * 0.5f + 2f : size.y * 0.5f - 2f);
                bubbleTail.localRotation = Quaternion.Euler(0f, 0f, above ? 45f : 225f);
            }
        }

        public void HideBubble() => bubble.gameObject.SetActive(false);

        public void Hide()
        {
            HideHand();
            HideBubble();
        }

        public bool IsShowing => hand.gameObject.activeSelf || bubble.gameObject.activeSelf;

        private void Update()
        {
            if (!hand.gameObject.activeSelf && !bubble.gameObject.activeSelf) return;
            time += Time.unscaledDeltaTime;

            if (hand.gameObject.activeSelf)
            {
                Transform visual = hand.GetChild(0);
                if (softPulse)
                {
                    visual.localPosition = Vector3.zero;
                    visual.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(time * 4f));
                }
                else
                {
                    // Tap motion: press toward the target, lift away.
                    float k = Mathf.Abs(Mathf.Sin(time * 3.2f));
                    visual.localPosition = new Vector3(tapDistance * k * 0.6f, -tapDistance * k, 0f);
                    visual.localScale = Vector3.one * (1f - 0.08f * (1f - k));
                }
            }
            if (bubble.gameObject.activeSelf)
            {
                bubble.localScale = Vector3.one * (1f + 0.03f * Mathf.Sin(time * 3f));
            }
        }

        private Vector2 ToLocal(Vector2 screenPoint)
        {
            Canvas root = area.GetComponentInParent<Canvas>().rootCanvas;
            Camera cam = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screenPoint, cam, out Vector2 local);
            return local;
        }
    }
}
