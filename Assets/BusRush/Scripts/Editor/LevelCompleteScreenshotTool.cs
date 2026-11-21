using System.Collections;
using BusRush.Gameplay;
using BusRush.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Step R4 check: Boot -> Home -> Game, then shows the Level Complete screen over the live level and saves it at
    /// 0.5 s and 2.5 s (3 stars), and at 2.5 s with 2 stars. Nothing is claimed, so the save is untouched.
    /// Windowed editor only. Command line: -executeMethod BusRush.EditorTools.LevelCompleteScreenshotTool.RunAndExit.
    /// </summary>
    [InitializeOnLoad]
    public static class LevelCompleteScreenshotTool
    {
        public const string OutputDir = "C:/UnityBuilds/BusRush_Source/unity_shots/R4";
        private const string Flag = "BusRush.WinShots.Active";
        private const string ExitFlag = "BusRush.WinShots.Exit";

        static LevelCompleteScreenshotTool()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Flag, false)) return;
                SessionState.SetBool(Flag, false);
                var go = new GameObject("[WinShots]");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<Driver>();
            };
        }

        [MenuItem("Tools/Bus Rush/Capture Level Complete Screenshots (R4)")]
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
                for (float wait = 0f; FindAnyObjectByType<HomeScreen>() == null && wait < 15f; wait += Time.unscaledDeltaTime) yield return null;
                yield return new WaitForSecondsRealtime(0.5f);
                SceneLoader.LoadGame();
                LevelCompleteScreen screen = null;
                for (float wait = 0f; (screen == null || GameSession.Current == null) && wait < 20f; wait += Time.unscaledDeltaTime)
                {
                    screen = LevelCompleteScreen.Find();
                    yield return null;
                }
                if (screen == null || GameSession.Current == null)
                {
                    Debug.LogError("[WinShots] Game scene / LevelCompleteScreen not found");
                    ok = false;
                }
                else
                {
                    yield return new WaitForSecondsRealtime(2.5f); // vehicles arrive, crowd settles
                    int level = GameSession.Current.Level;
                    screen.Show(level, 3, GameSession.CoinsFor(3));
                    yield return At(screen, 0.5f, "level_complete_0_5s_390x844");
                    yield return At(screen, 2.5f, "level_complete_2_5s_3stars_390x844");
                    screen.Show(level, 2, GameSession.CoinsFor(2));
                    yield return At(screen, 2.5f, "level_complete_2_5s_2stars_390x844");
                }
                Debug.Log("[WinShots] RESULT " + (ok ? "PASS" : "FAIL"));
                bool exit = SessionState.GetBool(ExitFlag, false);
                EditorApplication.ExitPlaymode();
                if (exit) EditorApplication.delayCall += () => EditorApplication.Exit(ok ? 0 : 1);
            }

            private static IEnumerator At(LevelCompleteScreen screen, float t, string name)
            {
                Time.timeScale = 1f;
                while (screen.Elapsed < t) yield return null;
                Time.timeScale = 0f;
                Debug.Log($"[WinShots] capturing {name} at {screen.Elapsed:0.000}s");
                yield return RenderCapture.Capture(OutputDir, name, 390, 844);
                Time.timeScale = 1f;
            }
        }
    }
}
