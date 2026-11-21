using System.Collections;
using BusRush.Gameplay;
using BusRush.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Step R7 check: the Level Failed screen over a live level at 1.5 s with the Extra bay offer, and again after every
    /// locked bay was opened (offer hidden). Uses a temporary editor save (restored). Windowed editor only.
    /// Command line: -executeMethod BusRush.EditorTools.LevelFailedScreenshotTool.RunAndExit (no -batchmode, no -quit).
    /// </summary>
    [InitializeOnLoad]
    public static class LevelFailedScreenshotTool
    {
        public const string OutputDir = "C:/UnityBuilds/BusRush_Source/unity_shots/R7";
        private const string SaveKey = "BusRush.Save";
        private const string Flag = "BusRush.FailShots.Active";
        private const string ExitFlag = "BusRush.FailShots.Exit";

        static LevelFailedScreenshotTool()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Flag, false)) return;
                SessionState.SetBool(Flag, false);
                var go = new GameObject("[FailShots]");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<Driver>();
            };
        }

        [MenuItem("Tools/Bus Rush/Capture Level Failed Screenshots (R7)")]
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
                for (float wait = 0f; FindAnyObjectByType<HomeScreen>() == null && wait < 15f; wait += Time.unscaledDeltaTime) yield return null;
                yield return new WaitForSecondsRealtime(0.5f);
                SaveSystem.Data.level = 5;
                SceneLoader.LoadGame();
                LevelFailedScreen screen = null;
                for (float wait = 0f; (screen == null || GameSession.Current == null || GameSession.Current.Level != 5) && wait < 20f; wait += Time.unscaledDeltaTime)
                {
                    screen = LevelFailedScreen.Find();
                    yield return null;
                }
                if (screen == null || GameSession.Current == null)
                {
                    Debug.LogError("[FailShots] Game scene / LevelFailedScreen not found");
                    ok = false;
                }
                else
                {
                    yield return new WaitForSecondsRealtime(3f);
                    GameSession session = GameSession.Current;
                    Debug.Log($"[FailShots] locked bays {session.LockedBayCount}, offered {session.ExtraBayOffered}");
                    screen.Show(session.ExtraBayOffered);
                    yield return At(screen, 1.5f, "level_failed_extra_bay_390x844");

                    screen.Hide();
                    while (session.Bays.UnlockNextBay()) { }
                    Debug.Log($"[FailShots] after unlocking: locked bays {session.LockedBayCount}, offered {session.ExtraBayOffered}");
                    yield return new WaitForSecondsRealtime(0.3f);
                    screen.Show(session.ExtraBayOffered);
                    yield return At(screen, 1.5f, "level_failed_no_bay_left_390x844");
                }

                if (string.IsNullOrEmpty(backup)) PlayerPrefs.DeleteKey(SaveKey);
                else PlayerPrefs.SetString(SaveKey, backup);
                PlayerPrefs.Save();
                SaveSystem.Load();

                Debug.Log("[FailShots] RESULT " + (ok ? "PASS" : "FAIL"));
                bool exit = SessionState.GetBool(ExitFlag, false);
                EditorApplication.ExitPlaymode();
                if (exit) EditorApplication.delayCall += () => EditorApplication.Exit(ok ? 0 : 1);
            }

            private static IEnumerator At(LevelFailedScreen screen, float t, string name)
            {
                Time.timeScale = 1f;
                while (screen.Elapsed < t) yield return null;
                Time.timeScale = 0f;
                Debug.Log($"[FailShots] capturing {name} at {screen.Elapsed:0.000}s");
                yield return RenderCapture.Capture(OutputDir, name, 390, 844);
                Time.timeScale = 1f;
            }
        }
    }
}
