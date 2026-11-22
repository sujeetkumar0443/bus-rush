using System.Collections;
using BusRush.Gameplay;
using BusRush.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Step R11 layout check: Home, HUD and Level Complete at 1080x1920, 1080x2340, 1080x2400 and 1080x2520. The tall
    /// shapes simulate a notch (top) and gesture bar (bottom) safe area. Editor save restored. Windowed editor only.
    /// Command line: -executeMethod BusRush.EditorTools.AspectScreenshotTool.RunAndExit (no -batchmode, no -quit).
    /// </summary>
    [InitializeOnLoad]
    public static class AspectScreenshotTool
    {
        public const string OutputDir = "C:/UnityBuilds/BusRush_Source/unity_shots/R11fix";
        private const string SaveKey = "BusRush.Save";
        private const string Flag = "BusRush.AspectShots.Active";
        private const string ExitFlag = "BusRush.AspectShots.Exit";

        private static readonly (int w, int h, float topInset, float bottomInset)[] Shapes =
        {
            (1080, 1920, 0f, 0f),        // 9:16, no notch
            (1080, 2340, 110f, 60f),     // 9:19.5
            (1080, 2400, 120f, 60f),     // 9:20
            (1080, 2520, 130f, 70f)      // 9:21
        };

        static AspectScreenshotTool()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Flag, false)) return;
                SessionState.SetBool(Flag, false);
                var go = new GameObject("[AspectShots]");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<Driver>();
            };
        }

        [MenuItem("Tools/Bus Rush/Capture Phone Shapes (R11)")]
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
                for (float wait = 0f; FindAnyObjectByType<HomeScreen>() == null && wait < 15f; wait += Time.unscaledDeltaTime) yield return null;
                yield return new WaitForSecondsRealtime(1.5f);
                if (UIManager.Instance != null) UIManager.Instance.CloseAll();
                yield return AllShapes("home");

                SaveSystem.Data.level = 14; // full booster bar
                SceneLoader.LoadGame();
                for (float wait = 0f; GameSession.Current == null && wait < 20f; wait += Time.unscaledDeltaTime) yield return null;
                if (GameSession.Current == null)
                {
                    ok = false;
                }
                else
                {
                    yield return new WaitForSecondsRealtime(3.2f);
                    yield return AllShapes("hud");
                    LevelCompleteScreen lc = LevelCompleteScreen.Find();
                    lc.Show(GameSession.Current.Level, 3, GameSession.CoinsFor(3));
                    while (lc.Elapsed < 2.6f) yield return null;
                    Time.timeScale = 0f;
                    yield return AllShapes("level_complete");
                    Time.timeScale = 1f;
                }

                SafeAreaFitter.Simulated = null;
                if (string.IsNullOrEmpty(backup)) PlayerPrefs.DeleteKey(SaveKey);
                else PlayerPrefs.SetString(SaveKey, backup);
                PlayerPrefs.Save();
                SaveSystem.Load();
                Debug.Log("[AspectShots] RESULT " + (ok ? "PASS" : "FAIL"));
                bool exit = SessionState.GetBool(ExitFlag, false);
                EditorApplication.ExitPlaymode();
                if (exit) EditorApplication.delayCall += () => EditorApplication.Exit(ok ? 0 : 1);
            }

            private static IEnumerator AllShapes(string screen)
            {
                foreach ((int w, int h, float top, float bottom) in Shapes)
                {
                    SafeAreaFitter.Simulated = top <= 0f && bottom <= 0f ? (Rect?)null
                        : new Rect(0f, bottom / h, 1f, 1f - (top + bottom) / h);
                    yield return null;
                    yield return RenderCapture.Capture(OutputDir, $"{screen}_{w}x{h}", w, h);
                }
                SafeAreaFitter.Simulated = null;
            }
        }
    }
}
