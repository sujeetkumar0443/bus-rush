using System.Collections;
using BusRush.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Step R1 check: plays the Boot scene and saves the splash at exact moments (the splash clock is frozen with
    /// timeScale 0 while capturing). Windowed editor only (batch mode renders wrong colours).
    /// Command line: -executeMethod BusRush.EditorTools.SplashScreenshotTool.RunAndExit (no -batchmode, no -quit).
    /// </summary>
    [InitializeOnLoad]
    public static class SplashScreenshotTool
    {
        public const string OutputDir = "C:/UnityBuilds/BusRush_Source/unity_shots/R1";
        private static readonly float[] Moments = { 0.6f, 2.5f };
        private const string Flag = "BusRush.SplashShots.Active";
        private const string ExitFlag = "BusRush.SplashShots.Exit";

        static SplashScreenshotTool()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Flag, false)) return;
                SessionState.SetBool(Flag, false);
                var go = new GameObject("[SplashShots]");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<Driver>();
            };
        }

        [MenuItem("Tools/Bus Rush/Capture Splash Screenshots (R1)")]
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
                SplashController splash = null;
                for (float wait = 0f; splash == null && wait < 5f; wait += Time.unscaledDeltaTime)
                {
                    splash = FindAnyObjectByType<SplashController>();
                    yield return null;
                }
                if (splash == null)
                {
                    Debug.LogError("[SplashShots] no SplashController in Boot");
                    ok = false;
                }
                else
                {
                    foreach (float t in Moments)
                    {
                        while (splash != null && splash.Elapsed < t) yield return null;
                        if (splash == null) break;
                        Time.timeScale = 0f;
                        string name = $"splash_{t:0.0}s_390x844".Replace('.', '_');
                        Debug.Log($"[SplashShots] capturing at {splash.Elapsed:0.000}s");
                        yield return RenderCapture.Capture(OutputDir, name, 390, 844);
                        Time.timeScale = 1f;
                    }
                }
                Debug.Log("[SplashShots] RESULT " + (ok ? "PASS" : "FAIL"));
                bool exit = SessionState.GetBool(ExitFlag, false);
                EditorApplication.ExitPlaymode();
                if (exit) EditorApplication.delayCall += () => EditorApplication.Exit(ok ? 0 : 1);
            }
        }
    }
}
