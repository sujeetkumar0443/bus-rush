using UnityEngine;
using UnityEngine.InputSystem;

namespace BusRush.UI
{
    /// <summary>
    /// Marks temporary debug UI. Kept in the editor, development builds and test builds (BUSRUSH_TEST_TOOLS, set by
    /// Tools > Bus Rush > Build Android APK); destroyed otherwise. Starts hidden so the screen looks like the real game.
    /// Toggle it with a three-finger tap or a 1.2 s long-press in the top-left corner.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class DebugOnly : MonoBehaviour
    {
        [SerializeField] private bool startVisible;
        [Tooltip("Corner size in pixels; at least 18% of the screen width on big screens.")]
        [SerializeField] private float cornerSizePx = 90f;
        [SerializeField] private float longPressSeconds = 1.2f;

        private CanvasGroup group;
        private float pressStart = -1f;
        private bool visible;
        private static bool swallowing;
        private static int swallowClearFrame = -1;

        /// <summary>
        /// True from a long-press toggle until just after that finger lifts: the button under the corner (Pause) must
        /// ignore the click that ends the long-press.
        /// </summary>
        public static bool Swallowing => swallowing;

        public static bool IncludedInBuild
        {
            get
            {
#if BUSRUSH_TEST_TOOLS
                return true;
#else
                return Debug.isDebugBuild;
#endif
            }
        }

        private void Awake()
        {
            if (!IncludedInBuild)
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
            if (swallowing)
            {
                if (pointer.press.isPressed) swallowClearFrame = -1;
                else if (swallowClearFrame < 0) swallowClearFrame = Time.frameCount + 2;
                else if (Time.frameCount >= swallowClearFrame) swallowing = false;
            }
            Vector2 p = pointer.position.ReadValue();
            float corner = Mathf.Max(cornerSizePx, Screen.width * 0.18f);
            bool inCorner = p.x < corner && p.y > Screen.height - corner;

            if (pointer.press.wasPressedThisFrame && inCorner) pressStart = Time.unscaledTime;
            if (!pointer.press.isPressed || !inCorner) pressStart = -1f;
            if (pressStart >= 0f && Time.unscaledTime - pressStart >= longPressSeconds)
            {
                pressStart = -1f;
                swallowing = true;
                swallowClearFrame = -1;
                SetVisible(!visible);
            }
        }
    }
}
