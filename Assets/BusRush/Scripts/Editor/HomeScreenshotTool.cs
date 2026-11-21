using System.Collections;
using BusRush.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Step R2 check: Boot -> Home, then saves Home at 1 s and 4 s of its animation clock (frozen with timeScale 0)
    /// and with the FREE COINS popup open. Windowed editor only.
    /// Command line: -executeMethod BusRush.EditorTools.HomeScreenshotTool.RunAndExit (no -batchmode, no -quit).
    /// </summary>
    [InitializeOnLoad]
    public static class HomeScreenshotTool
    {
        public const string OutputDir = "C:/UnityBuilds/BusRush_Source/unity_shots/R2";
        private const string Flag = "BusRush.HomeShots.Active";
        private const string ExitFlag = "BusRush.HomeShots.Exit";

        static HomeScreenshotTool()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Flag, false)) return;
                SessionState.SetBool(Flag, false);
                var go = new GameObject("[HomeShots]");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<Driver>();
            };
        }

        [MenuItem("Tools/Bus Rush/Capture Home Screenshots (R2)")]
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
                HomeAmbience home = null;
                for (float wait = 0f; home == null && wait < 15f; wait += Time.unscaledDeltaTime)
                {
                    home = FindAnyObjectByType<HomeAmbience>();
                    yield return null;
                }
                if (home == null)
                {
                    Debug.LogError("[HomeShots] Home did not load");
                    ok = false;
                }
                else
                {
                    foreach (float t in new[] { 1f, 4f })
                    {
                        Time.timeScale = 1f;
                        while (home.Elapsed < t) yield return null;
                        Time.timeScale = 0f;
                        Debug.Log($"[HomeShots] capturing at {home.Elapsed:0.000}s");
                        yield return RenderCapture.Capture(OutputDir, $"home_{t:0}s_390x844", 390, 844);
                    }
                    UIManager.Instance.Open<FreeCoinsPopup>();
                    yield return new WaitForSecondsRealtime(0.6f);
                    yield return RenderCapture.Capture(OutputDir, "home_free_coins_390x844", 390, 844);
                    Time.timeScale = 1f;
                }
                Debug.Log("[HomeShots] RESULT " + (ok ? "PASS" : "FAIL"));
                bool exit = SessionState.GetBool(ExitFlag, false);
                EditorApplication.ExitPlaymode();
                if (exit) EditorApplication.delayCall += () => EditorApplication.Exit(ok ? 0 : 1);
            }
        }
    }
}
