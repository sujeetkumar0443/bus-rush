using UnityEngine;
using UnityEngine.InputSystem;

namespace BusRush.UI
{
    /// <summary>
    /// Marks temporary debug UI. Destroyed in non-development builds; in development builds it starts hidden so the
    /// screen looks like the real game. Toggle it with a three-finger tap or a 1.2 s long-press in the top-left corner.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class DebugOnly : MonoBehaviour
    {
        [SerializeField] private bool startVisible;
        [SerializeField] private float cornerSizePx = 90f;
        [SerializeField] private float longPressSeconds = 1.2f;

        private CanvasGroup group;
        private float pressStart = -1f;
        private bool visible;

        private void Awake()
        {
            if (!Debug.isDebugBuild)
            {
                Destroy(gameObject);
                return;
            }
            group = GetComponent<CanvasGroup>();
            SetVisible(startVisible);
        }

        public void SetVisible(bool show)
        {
            visible = show;
            group.alpha = show ? 1f : 0f;
            group.interactable = show;
            group.blocksRaycasts = show;
        }

        private void Update()
        {
            Touchscreen touch = Touchscreen.current;
            if (touch != null)
            {
                int down = 0;
                bool began = false;
                foreach (var t in touch.touches)
                {
                    if (!t.press.isPressed) continue;
                    down++;
                    began |= t.press.wasPressedThisFrame;
                }
                if (down >= 3 && began)
                {
                    SetVisible(!visible);
                    return;
                }
            }

            Pointer pointer = Pointer.current;
            if (pointer == null) return;
            Vector2 p = pointer.position.ReadValue();
            bool inCorner = p.x < cornerSizePx && p.y > Screen.height - cornerSizePx;

            if (pointer.press.wasPressedThisFrame && inCorner) pressStart = Time.unscaledTime;
            if (!pointer.press.isPressed || !inCorner) pressStart = -1f;
            if (pressStart >= 0f && Time.unscaledTime - pressStart >= longPressSeconds)
            {
                pressStart = -1f;
                SetVisible(!visible);
            }
        }
    }
}
