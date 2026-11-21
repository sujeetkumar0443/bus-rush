using System.Collections;
using BusRush.Gameplay;
using BusRush.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Step R5 check: the HUD on level 5 and level 14, and the Booster popup for Shuffle. Uses a temporary editor save
    /// (restored at the end). Windowed editor only. Command line:
    /// -executeMethod BusRush.EditorTools.HudScreenshotTool.RunAndExit (no -batchmode, no -quit).
    /// </summary>
    [InitializeOnLoad]
    public static class HudScreenshotTool
    {
        public const string OutputDir = "C:/UnityBuilds/BusRush_Source/unity_shots/R5";
        private const string SaveKey = "BusRush.Save";
        private const string Flag = "BusRush.HudShots.Active";
        private const string ExitFlag = "BusRush.HudShots.Exit";

        static HudScreenshotTool()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Flag, false)) return;
                SessionState.SetBool(Flag, false);
                var go = new GameObject("[HudShots]");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<Driver>();
            };
        }

        [MenuItem("Tools/Bus Rush/Capture HUD Screenshots (R5)")]
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

                foreach (int level in new[] { 5, 14 })
                {
                    SaveSystem.Data.level = level;
                    SceneLoader.LoadGame();
                    for (float wait = 0f; (GameSession.Current == null || GameSession.Current.Level != level) && wait < 20f; wait += Time.unscaledDeltaTime)
                    {
                        yield return null;
                    }
                    if (GameSession.Current == null || GameSession.Current.Level != level)
                    {
                        Debug.LogError($"[HudShots] level {level} did not start");
                        ok = false;
                        break;
                    }
                    yield return new WaitForSecondsRealtime(3.2f); // vehicles in, intro banner gone
                    yield return RenderCapture.Capture(OutputDir, $"hud_level{level}_390x844", 390, 844);
                }

                if (ok && UIManager.Instance != null)
                {
                    BoosterPopup popup = UIManager.Instance.Open<BoosterPopup>();
                    popup.Select(BoosterType.Shuffle);
                    yield return new WaitForSecondsRealtime(1.2f);
                    yield return RenderCapture.Capture(OutputDir, "booster_popup_shuffle_390x844", 390, 844);
                }

                if (string.IsNullOrEmpty(backup)) PlayerPrefs.DeleteKey(SaveKey);
                else PlayerPrefs.SetString(SaveKey, backup);
                PlayerPrefs.Save();
                SaveSystem.Load();

                Debug.Log("[HudShots] RESULT " + (ok ? "PASS" : "FAIL"));
                bool exit = SessionState.GetBool(ExitFlag, false);
                EditorApplication.ExitPlaymode();
                if (exit) EditorApplication.delayCall += () => EditorApplication.Exit(ok ? 0 : 1);
            }
        }
    }
}
