using System.Collections;
using BusRush.Gameplay;
using BusRush.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Step R6 check: Pause on level 5 with every switch on, Pause after tapping Music off, then Settings on Home.
    /// Uses a temporary editor save (restored at the end). Windowed editor only. Command line:
    /// -executeMethod BusRush.EditorTools.SettingsScreenshotTool.RunAndExit (no -batchmode, no -quit).
    /// </summary>
    [InitializeOnLoad]
    public static class SettingsScreenshotTool
    {
        public const string OutputDir = "C:/UnityBuilds/BusRush_Source/unity_shots/R6";
        private const string SaveKey = "BusRush.Save";
        private const string Flag = "BusRush.SettingsShots.Active";
        private const string ExitFlag = "BusRush.SettingsShots.Exit";

        static SettingsScreenshotTool()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Flag, false)) return;
                SessionState.SetBool(Flag, false);
                var go = new GameObject("[SettingsShots]");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<Driver>();
            };
        }

        [MenuItem("Tools/Bus Rush/Capture Pause + Settings Screenshots (R6)")]
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

                SaveData d = SaveSystem.Data;
                d.level = 5;
                d.soundOn = d.musicOn = d.vibrationOn = true;
                SceneLoader.LoadGame();
                for (float wait = 0f; (GameSession.Current == null || GameSession.Current.Level != 5) && wait < 20f; wait += Time.unscaledDeltaTime)
                {
                    yield return null;
                }
                if (GameSession.Current == null || UIManager.Instance == null)
                {
                    Debug.LogError("[SettingsShots] game did not start");
                    ok = false;
                }
                else
                {
                    yield return new WaitForSecondsRealtime(3f);
                    PausePopup pause = UIManager.Instance.Open<PausePopup>();
                    yield return new WaitForSecondsRealtime(0.9f);
                    Debug.Log($"[SettingsShots] paused: state={GameManager.Instance.State} timeScale={Time.timeScale}");
                    yield return RenderCapture.Capture(OutputDir, "pause_all_on_390x844", 390, 844);

                    foreach (SettingsRow row in pause.GetComponentsInChildren<SettingsRow>())
                    {
                        if (row.Kind == SettingsRow.Setting.Music) row.GetComponent<Button>().onClick.Invoke();
                    }
                    yield return new WaitForSecondsRealtime(0.5f);
                    Debug.Log($"[SettingsShots] music in save: {SaveSystem.Data.musicOn}");
                    yield return RenderCapture.Capture(OutputDir, "pause_music_off_390x844", 390, 844);
                    pause.Close();
                    yield return new WaitForSecondsRealtime(0.3f);

                    SceneLoader.LoadHome();
                    for (float wait = 0f; FindAnyObjectByType<HomeScreen>() == null && wait < 15f; wait += Time.unscaledDeltaTime) yield return null;
                    yield return new WaitForSecondsRealtime(1.2f);
                    UIManager.Instance.Open<SettingsPopup>();
                    yield return new WaitForSecondsRealtime(0.9f);
                    yield return RenderCapture.Capture(OutputDir, "settings_390x844", 390, 844);
                }

                if (string.IsNullOrEmpty(backup)) PlayerPrefs.DeleteKey(SaveKey);
                else PlayerPrefs.SetString(SaveKey, backup);
                PlayerPrefs.Save();
                SaveSystem.Load();

                Debug.Log("[SettingsShots] RESULT " + (ok ? "PASS" : "FAIL"));
                bool exit = SessionState.GetBool(ExitFlag, false);
                EditorApplication.ExitPlaymode();
                if (exit) EditorApplication.delayCall += () => EditorApplication.Exit(ok ? 0 : 1);
            }
        }
    }
}
