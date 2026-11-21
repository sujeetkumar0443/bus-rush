using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BusRush
{
    /// <summary>
    /// Hidden performance counter for phone testing, present in every build (release included). Five quick taps in the
    /// top-left corner (left 25% of the width, top 15% of the height, not on a button: in the game, the plaza just
    /// below the Restart button) within 1.5 s toggle it: FPS, frame time (ms, average and worst over the last half
    /// second) and SetPass / draw calls. Render counters a player build does not provide show as "n/a".
    /// Also pins the frame rate (60 fps, vSync off) before the first scene loads.
    /// </summary>
    public sealed class PerfOverlay : MonoBehaviour
    {
        private const int TapsToToggle = 5;
        private const float TapWindowSeconds = 1.5f;
        private const float ZoneWidth = 0.25f;   // of the screen width, from the left edge
        private const float ZoneHeight = 0.15f;  // of the screen height, from the top edge
        private const float SampleSeconds = 0.5f;

        private bool visible;
        private int taps;
        private float firstTapTime;

        private ProfilerRecorder setPassRecorder;
        private ProfilerRecorder drawCallsRecorder;

        private float sampleTime;
        private int sampleFrames;
        private float worstMs;
        private string text = "";
        private GUIStyle style;
        private Texture2D background;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            var go = new GameObject("[PerfOverlay]");
            DontDestroyOnLoad(go);
            go.AddComponent<PerfOverlay>();
        }

        private void OnEnable()
        {
            setPassRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            drawCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
        }

        private void OnDisable()
        {
            setPassRecorder.Dispose();
            drawCallsRecorder.Dispose();
        }

        private void OnDestroy()
        {
            if (background != null) Destroy(background);
        }

        private void Update()
        {
            DetectTaps();
            if (!visible) return;

            float ms = Time.unscaledDeltaTime * 1000f;
            sampleTime += Time.unscaledDeltaTime;
            sampleFrames++;
            if (ms > worstMs) worstMs = ms;
            if (sampleTime < SampleSeconds) return;

            float avgMs = sampleTime * 1000f / sampleFrames;
            string setPass = setPassRecorder.Valid ? setPassRecorder.LastValue.ToString() : "n/a";
            string draws = drawCallsRecorder.Valid ? drawCallsRecorder.LastValue.ToString() : "n/a";
            text = $"FPS {sampleFrames / sampleTime:F0}\n{avgMs:F1} ms (worst {worstMs:F1})\nSetPass {setPass}  Draws {draws}";
            sampleTime = 0f;
            sampleFrames = 0;
            worstMs = 0f;
        }

        /// <summary>Counts presses that start in the top-left corner; five within the window toggle the overlay.</summary>
        private void DetectTaps()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return;
            Vector2 p = pointer.position.ReadValue();
            bool inZone = p.x <= Screen.width * ZoneWidth && p.y >= Screen.height * (1f - ZoneHeight);
            // Taps on UI (e.g. the Restart button) do not count, so toggling never presses a button.
            bool onUi = UnityEngine.EventSystems.EventSystem.current != null &&
                        UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
            if (!inZone || onUi)
            {
                taps = 0;
                return;
            }
            if (taps == 0 || Time.unscaledTime - firstTapTime > TapWindowSeconds)
            {
                taps = 0;
                firstTapTime = Time.unscaledTime;
            }
            if (++taps < TapsToToggle) return;
            taps = 0;
            visible = !visible;
            sampleTime = 0f;
            sampleFrames = 0;
            worstMs = 0f;
            text = "measuring...";
        }

        private void OnGUI()
        {
            if (!visible) return;
            if (style == null)
            {
                background = new Texture2D(1, 1);
                background.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.6f));
                background.Apply();
                style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Max(14, Screen.height / 55),
                    normal = { textColor = Color.white, background = background },
                    padding = new RectOffset(10, 10, 6, 6)
                };
            }
            // Under the tap zone (safe area), so the zone stays tappable.
            Rect safe = Screen.safeArea;
            float top = Mathf.Max(Screen.height - safe.yMax, Screen.height * ZoneHeight) + 8f;
            GUI.Label(new Rect(safe.xMin + 8f, top, Screen.width * 0.55f, style.fontSize * 4.6f), text, style);
        }
    }
}
