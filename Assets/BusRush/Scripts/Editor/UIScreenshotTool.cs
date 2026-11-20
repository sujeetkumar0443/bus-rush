using System.Collections;
using System.Collections.Generic;
using System.IO;
using BusRush.Gameplay;
using BusRush.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Dev tool: plays Boot -> Home -> every popup -> Game -> debug popups in Play mode and saves screenshots
    /// at exact resolutions into BusRush_Source/unity_shots. Must run in the windowed editor (not -batchmode).
    /// NOTE: resets the editor save (PlayerPrefs) to new-game values at the start of each run.
    /// Menu: Tools/Bus Rush/Capture UI Screenshots.
    /// </summary>
    [InitializeOnLoad]
    public static class UIScreenshotTool
    {
        public const string OutputDir = "C:/UnityBuilds/BusRush_Source/unity_shots";
        private const string Flag = "BusRush.UIScreenshotTool.Active";
        private const string ExitFlag = "BusRush.UIScreenshotTool.ExitWhenDone";

        static UIScreenshotTool()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("Tools/Bus Rush/Capture UI Screenshots")]
        public static void Run() => Start(false);

        /// <summary>Command line: -executeMethod BusRush.EditorTools.UIScreenshotTool.RunAndExit (no -batchmode, no -quit).</summary>
        public static void RunAndExit() => Start(true);

        private static void Start(bool exitWhenDone)
        {
            SessionState.SetBool(Flag, true);
            SessionState.SetBool(ExitFlag, exitWhenDone);
            EditorSceneManager.OpenScene("Assets/BusRush/Scenes/Boot.unity");
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Flag, false)) return;
            SessionState.SetBool(Flag, false);
            var go = new GameObject("[UIScreenshotDriver]");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Driver>();
        }

        internal static void Finish(bool ok)
        {
            Debug.Log("[UIScreenshotTool] RESULT " + (ok ? "PASS" : "FAIL"));
            bool exit = SessionState.GetBool(ExitFlag, false);
            EditorApplication.ExitPlaymode();
            if (exit) EditorApplication.delayCall += () => EditorApplication.Exit(ok ? 0 : 1);
        }

        private sealed class Driver : MonoBehaviour
        {
            private int errors;

            private void OnEnable() => Application.logMessageReceived += OnLog;

            private void OnDisable() => Application.logMessageReceived -= OnLog;

            private void OnLog(string message, string stack, LogType type)
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                {
                    errors++;
                }
            }

            private IEnumerator Start()
            {
                Directory.CreateDirectory(OutputDir);
                // Repeatable shots: start from a new-game save (editor PlayerPrefs only).
                SaveSystem.ResetToDefaults();

                // Splash mid-animation.
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Capture("01_splash", 390, 844);

                yield return WaitForScene(SceneLoader.HomeScene);
                yield return new WaitForSecondsRealtime(0.8f);
                yield return Capture("02_home_390x844", 390, 844);
                yield return Capture("02_home_360x800", 360, 800);
                yield return Capture("02_home_412x915", 412, 915);
                SafeAreaFitter.Simulated = new Rect(0f, 0.03f, 1f, 0.92f); // notch + gesture bar
                yield return Capture("02_home_412x915_notch", 412, 915);
                SafeAreaFitter.Simulated = null;

                yield return OpenAndCapture<SettingsPopup>("03_settings");
                yield return OpenAndCapture<ShopPopup>("04_shop");

                yield return OpenAndCapture<DailyRewardPopup>("05_daily", false);
                ClickButton(UIManager.Instance.Get<DailyRewardPopup>().transform, "ClaimButton");
                yield return new WaitForSecondsRealtime(0.5f);
                yield return Capture("05_daily_claimed", 390, 844);
                UIManager.Instance.CloseAll();
                yield return new WaitForSecondsRealtime(0.3f);

                BoosterPopup booster = UIManager.Instance.Open<BoosterPopup>();
                booster.Select(BoosterType.Shuffle);
                yield return new WaitForSecondsRealtime(0.5f);
                yield return Capture("06_booster", 390, 844);
                UIManager.Instance.CloseAll();
                yield return new WaitForSecondsRealtime(0.3f);

                SceneLoader.LoadGame();
                yield return WaitForScene(SceneLoader.GameScene);
                yield return new WaitForSecondsRealtime(0.8f);
                yield return Capture("07_game_390x844", 390, 844);
                yield return Capture("07_game_360x800", 360, 800);
                yield return Capture("07_game_412x915", 412, 915);

                Transform debug = GameObject.Find("DebugPanel (TEMP)").transform;
                ClickButton(debug, "SettingsButton");
                yield return new WaitForSecondsRealtime(0.5f);
                Debug.Log($"[UIScreenshotTool] Pause: state={GameManager.Instance.State}, timeScale={Time.timeScale}");
                yield return Capture("08_pause", 390, 844);
                ClickButton(UIManager.Instance.Get<SettingsPopup>().transform, "ResumeButton");
                yield return new WaitForSecondsRealtime(0.3f);
                Debug.Log($"[UIScreenshotTool] Resume: state={GameManager.Instance.State}, timeScale={Time.timeScale}");

                ClickButton(debug, "LevelCompleteButton");
                yield return new WaitForSecondsRealtime(1.8f);
                yield return Capture("09_level_complete", 390, 844);
                UIManager.Instance.CloseAll();
                yield return new WaitForSecondsRealtime(0.3f);

                ClickButton(debug, "OutOfSpaceButton");
                yield return new WaitForSecondsRealtime(0.6f);
                yield return Capture("10_out_of_space", 390, 844);
                UIManager.Instance.CloseAll();

                Debug.Log($"[UIScreenshotTool] Done. Errors logged during run: {errors}. Save: level={SaveSystem.Data.level} coins={SaveSystem.Data.coins} streak={SaveSystem.Data.dailyStreak}");
                Finish(errors == 0);
            }

            private static IEnumerator WaitForScene(string scene)
            {
                float timeout = Time.realtimeSinceStartup + 30f;
                while ((SceneManager.GetActiveScene().name != scene || SceneLoader.IsLoading) && Time.realtimeSinceStartup < timeout)
                {
                    yield return null;
                }
            }

            private IEnumerator OpenAndCapture<T>(string name, bool close = true) where T : Popup
            {
                UIManager.Instance.Open<T>();
                yield return new WaitForSecondsRealtime(0.5f);
                yield return Capture(name, 390, 844);
                if (close)
                {
                    UIManager.Instance.CloseAll();
                    yield return new WaitForSecondsRealtime(0.3f);
                }
            }

            private static void ClickButton(Transform root, string name)
            {
                foreach (Button b in root.GetComponentsInChildren<Button>(true))
                {
                    if (b.name == name)
                    {
                        b.onClick.Invoke();
                        return;
                    }
                }
                Debug.LogError($"[UIScreenshotTool] Button '{name}' not found under {root.name}");
            }

            private static IEnumerator Capture(string name, int w, int h) => RenderCapture.Capture(OutputDir, name, w, h);
        }
    }
}
