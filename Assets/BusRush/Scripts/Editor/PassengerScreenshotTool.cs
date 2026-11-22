using System.Collections;
using System.Linq;
using BusRush.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Step P1 check: level 1 and level 20 crowds and a mid-boarding moment at 390x844, plus the on-screen size of a
    /// standing passenger and the editor render stats (draw calls / SetPass / batches) with the crowd on screen.
    /// Editor save restored. Windowed editor only. Command line:
    /// -executeMethod BusRush.EditorTools.PassengerScreenshotTool.RunAndExit (no -batchmode, no -quit).
    /// </summary>
    [InitializeOnLoad]
    public static class PassengerScreenshotTool
    {
        public const string OutputDir = "C:/UnityBuilds/BusRush_Source/unity_shots/P1";
        private const string SaveKey = "BusRush.Save";
        private const string Flag = "BusRush.PassengerShots.Active";
        private const string ExitFlag = "BusRush.PassengerShots.Exit";

        static PassengerScreenshotTool()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Flag, false)) return;
                SessionState.SetBool(Flag, false);
                var go = new GameObject("[PassengerShots]");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<Driver>();
            };
        }

        [MenuItem("Tools/Bus Rush/Capture Passenger Screenshots (P1)")]
        public static void Run() => Start(false);

        public static void RunAndExit() => Start(true);

        private static void Start(bool exit)
        {
            SessionState.SetBool(Flag, true);
            SessionState.SetBool(ExitFlag, exit);
            EditorSceneManager.OpenScene("Assets/BusRush/Scenes/Boot.unity");
            EditorApplication.EnterPlaymode();
        }

        private sealed class Driver : MonoBehaviour
        {
            private IEnumerator Start()
            {
                bool ok = true;
                string backup = PlayerPrefs.GetString(SaveKey, null);
                SaveSystem.Data.dailyAutoShown = DailyRewards.TodayKey;
                for (float wait = 0f; FindAnyObjectByType<UI.HomeScreen>() == null && wait < 15f; wait += Time.unscaledDeltaTime) yield return null;
                yield return new WaitForSecondsRealtime(0.5f);

                foreach (int level in new[] { 1, 20 })
                {
                    SaveSystem.Data.level = level;
                    SceneLoader.LoadGame();
                    for (float w = 0f; (GameSession.Current == null || GameSession.Current.Level != level) && w < 20f; w += Time.unscaledDeltaTime) yield return null;
                    if (GameSession.Current == null) { ok = false; break; }
                    yield return new WaitForSecondsRealtime(3.5f);
                    if (level == 1) Measure();
                    Stats($"level {level}");
                    yield return RenderCapture.Capture(OutputDir, $"crowd_level{level}_390x844", 390, 844);
                }

                // Mid-boarding: tap vehicles until someone is running to a parked one, then capture.
                GameSession s = GameSession.Current;
                if (ok && s != null)
                {
                    InputController input = FindAnyObjectByType<InputController>();
                    Camera cam = Camera.main;
                    for (float w = 0f; s.Crowd.WalkerCount == 0 && w < 12f; w += 0.35f)
                    {
                        foreach (VehicleView v in s.Board.Vehicles.ToList())
                        {
                            if (v == null || v.IsExiting) continue;
                            input.HandleTap(cam.WorldToScreenPoint(v.transform.position));
                            break;
                        }
                        yield return new WaitForSecondsRealtime(0.35f);
                    }
                    for (float w = 0f; s.Crowd.WalkerCount < 2 && w < 4f; w += Time.unscaledDeltaTime) yield return null;
                    Time.timeScale = 0f;
                    Stats("boarding");
                    yield return RenderCapture.Capture(OutputDir, "mid_boarding_390x844", 390, 844);
                    Time.timeScale = 1f;
                }

                if (string.IsNullOrEmpty(backup)) PlayerPrefs.DeleteKey(SaveKey);
                else PlayerPrefs.SetString(SaveKey, backup);
                PlayerPrefs.Save();
                SaveSystem.Load();
                Debug.Log("[PassengerShots] RESULT " + (ok ? "PASS" : "FAIL"));
                bool exit = SessionState.GetBool(ExitFlag, false);
                EditorApplication.ExitPlaymode();
                if (exit) EditorApplication.delayCall += () => EditorApplication.Exit(ok ? 0 : 1);
            }

            /// <summary>Projected size of a standing passenger in 390x844 reference px.</summary>
            private static void Measure()
            {
                Camera cam = Camera.main;
                PassengerView sample = FindObjectsByType<PassengerView>(FindObjectsInactive.Exclude).FirstOrDefault(v => v.isActiveAndEnabled);
                if (sample == null || cam == null) return;
                // A test passenger standing on the plaza (middle of the U), at rest, facing the camera.
                PassengerView p = Object.Instantiate(sample);
                p.ResetPose();
                p.transform.SetPositionAndRotation(ScreenRef.RefToWorld(CrowdView.EllipseCenter + new Vector2(0f, CrowdView.EllipseRy)),
                    Quaternion.LookRotation(Vector3.back, Vector3.up));
                p.transform.localScale = CrowdView.PassengerScale(p);
                Bounds b = p.GetComponentsInChildren<MeshRenderer>().Where(r => r.name != "Shadow").Select(r => r.bounds)
                    .Aggregate((x, y) => { x.Encapsulate(y); return x; });
                Object.DestroyImmediate(p.gameObject);
                float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 sp = cam.WorldToViewportPoint(c);
                    minX = Mathf.Min(minX, sp.x); maxX = Mathf.Max(maxX, sp.x);
                    minY = Mathf.Min(minY, sp.y); maxY = Mathf.Max(maxY, sp.y);
                }
                // Viewport -> reference px (the camera fits the 390x844 layout).
                // Calibrate with the layout itself: two plaza ground points 34 ref px apart, horizontally and vertically.
                Vector2 at = CrowdView.EllipseCenter + new Vector2(0f, CrowdView.EllipseRy);
                Vector3 o = cam.WorldToViewportPoint(ScreenRef.RefToWorld(at));
                float perRefX = Mathf.Abs(cam.WorldToViewportPoint(ScreenRef.RefToWorld(at + new Vector2(34f, 0f))).x - o.x) / 34f;
                float perRefY = Mathf.Abs(cam.WorldToViewportPoint(ScreenRef.RefToWorld(at + new Vector2(0f, 34f))).y - o.y) / 34f;
                float w = (maxX - minX) / perRefX, h = (maxY - minY) / perRefY;
                Debug.Log($"[PassengerShots] standing passenger box ~{w:F1} x {h:F1} ref px (bounds box, {Screen.width}x{Screen.height} game view)");
            }

            private static void Stats(string label)
            {
                int active = FindObjectsByType<PassengerView>(FindObjectsInactive.Exclude).Count(v => v.isActiveAndEnabled);
                Debug.Log($"[PassengerShots] {label}: {active} passengers; drawCalls {UnityStats.drawCalls}, " +
                          $"setPass {UnityStats.setPassCalls}, tris {UnityStats.triangles}");
            }
        }
    }
}
