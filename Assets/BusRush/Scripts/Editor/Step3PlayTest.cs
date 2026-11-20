using System.Collections;
using System.Linq;
using BusRush.Gameplay;
using BusRush.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Dev tool for Step 3 (windowed editor only): screenshots of levels 1, 5 and 20 at 390x844 / 360x800 / 412x915
    /// (+ 9:16 and 9:21 extremes and a wrong-tap frame), then auto-plays levels 1..30 by tapping free vehicles through
    /// the real <see cref="InputController.HandleTap"/> path until each board is clear. Resets the editor save.
    /// </summary>
    [InitializeOnLoad]
    public static class Step3PlayTest
    {
        public const string ShotsDir = "C:/UnityBuilds/BusRush_Source/unity_shots/step3";
        private const string Flag = "BusRush.Step3PlayTest.Active";
        private const string ExitFlag = "BusRush.Step3PlayTest.Exit";

        static Step3PlayTest()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Flag, false)) return;
                SessionState.SetBool(Flag, false);
                var go = new GameObject("[Step3PlayTest]");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<Driver>();
            };
        }

        [MenuItem("Tools/Bus Rush/Step 3 Play Test + Screenshots")]
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
            private int errors;
            private int mispicks;

            private void OnEnable() => Application.logMessageReceived += OnLog;
            private void OnDisable() => Application.logMessageReceived -= OnLog;

            private void OnLog(string message, string stack, LogType type)
            {
                if (type is LogType.Error or LogType.Exception or LogType.Assert) errors++;
            }

            private IEnumerator Start()
            {
                SaveSystem.ResetToDefaults();
                yield return WaitScene(SceneLoader.HomeScene);

                // ---- screenshots
                foreach (int level in new[] { 1, 5, 20 })
                {
                    yield return LoadLevel(level);
                    yield return RenderCapture.Capture(ShotsDir, $"level{level:00}_390x844", 390, 844);
                    yield return RenderCapture.Capture(ShotsDir, $"level{level:00}_360x800", 360, 800);
                    yield return RenderCapture.Capture(ShotsDir, $"level{level:00}_412x915", 412, 915);
                }
                yield return LoadLevel(1);
                yield return RenderCapture.Capture(ShotsDir, "aspect_9x16_360x640", 360, 640);
                yield return RenderCapture.Capture(ShotsDir, "aspect_9x21_360x840", 360, 840);

                // Wrong tap: tap a blocked vehicle and grab the flash mid-way.
                GameSession s = GameSession.Current;
                VehicleView blocked = s.Board.Vehicles.FirstOrDefault(v => !s.Board.IsPathClear(v));
                if (blocked != null)
                {
                    Tap(blocked);
                    yield return new WaitForSecondsRealtime(0.08f);
                    yield return RenderCapture.Capture(ShotsDir, "wrong_tap_390x844", 390, 844);
                    Debug.Log($"[Step3PlayTest] wrong taps counted: {s.WrongTaps}");
                }

                // ---- automated play, levels 1..30
                int cleared = 0;
                for (int level = 1; level <= 30; level++)
                {
                    yield return LoadLevel(level);
                    GameSession session = GameSession.Current;
                    int vehicles = session.LevelData.vehicles.Count;
                    float deadline = Time.realtimeSinceStartup + 60f;

                    // Exercise the blocked path once per level, then clear the board.
                    VehicleView anyBlocked = session.Board.Vehicles.FirstOrDefault(v => !session.Board.IsPathClear(v));
                    if (anyBlocked != null) Tap(anyBlocked);

                    while (!session.IsCleared && Time.realtimeSinceStartup < deadline)
                    {
                        VehicleView free = session.Board.Vehicles.FirstOrDefault(v => session.Board.IsPathClear(v));
                        if (free != null) Tap(free);
                        yield return new WaitForSecondsRealtime(0.05f);
                    }

                    yield return new WaitForSecondsRealtime(0.8f);
                    bool popup = UIManager.Instance != null && UIManager.Instance.IsOpen<LevelCompletePopup>();
                    bool ok = session.IsCleared && popup && SaveSystem.Data.level == level + 1 && session.ExitedCount == vehicles;
                    if (ok) cleared++;
                    Debug.Log($"[Step3PlayTest] level {level}: {(ok ? "CLEARED" : "FAILED")} vehicles={vehicles} moves={session.Moves} " +
                              $"wrongTaps={session.WrongTaps} exited={session.ExitedCount} popup={popup} savedLevel={SaveSystem.Data.level}");
                    UIManager.Instance.CloseAll();
                }

                bool pass = cleared == 30 && errors == 0 && mispicks == 0;
                Debug.Log($"[Step3PlayTest] RESULT {(pass ? "PASS" : "FAIL")}: cleared {cleared}/30, errors {errors}, mis-picked taps {mispicks}");
                SaveSystem.ResetToDefaults();
                EditorApplication.ExitPlaymode();
                if (SessionState.GetBool(ExitFlag, false)) EditorApplication.delayCall += () => EditorApplication.Exit(pass ? 0 : 1);
            }

            private void Tap(VehicleView v)
            {
                Camera cam = Camera.main;
                Vector3 screen = cam.WorldToScreenPoint(v.transform.position + Vector3.up * 0.5f);
                var input = Object.FindAnyObjectByType<InputController>();
                VehicleView picked = input.HandleTap(screen);
                if (picked != v)
                {
                    mispicks++;
                    Debug.LogWarning($"[Step3PlayTest] tap on {v.name} picked {(picked != null ? picked.name : "nothing")}");
                }
            }

            private static IEnumerator LoadLevel(int level)
            {
                if (UIManager.Instance != null) UIManager.Instance.CloseAll();
                SaveSystem.Data.level = level;
                SaveSystem.Commit();
                SceneLoader.LoadGame();
                yield return null;
                yield return WaitScene(SceneLoader.GameScene);
                // Let every vehicle finish its staggered pop-in (34 * 0.03 s + 0.3 s).
                yield return new WaitForSecondsRealtime(1.6f);
            }

            private static IEnumerator WaitScene(string scene)
            {
                float timeout = Time.realtimeSinceStartup + 30f;
                while ((SceneManager.GetActiveScene().name != scene || SceneLoader.IsLoading || (scene == SceneLoader.GameScene && GameSession.Current == null))
                       && Time.realtimeSinceStartup < timeout)
                {
                    yield return null;
                }
            }
        }
    }
}
