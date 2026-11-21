using System.Collections;
using BusRush.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Step R8 check: Daily Reward on day 3 (days 1-2 claimed), Lucky Spin idle and its "You won!" card. Uses a
    /// temporary editor save (restored at the end). Windowed editor only. Command line:
    /// -executeMethod BusRush.EditorTools.DailyScreenshotTool.RunAndExit (no -batchmode, no -quit).
    /// </summary>
    [InitializeOnLoad]
    public static class DailyScreenshotTool
    {
        public const string OutputDir = "C:/UnityBuilds/BusRush_Source/unity_shots/R8";
        private const string SaveKey = "BusRush.Save";
        private const string Flag = "BusRush.DailyShots.Active";
        private const string ExitFlag = "BusRush.DailyShots.Exit";

        static DailyScreenshotTool()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Flag, false)) return;
                SessionState.SetBool(Flag, false);
                var go = new GameObject("[DailyShots]");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<Driver>();
            };
        }

        [MenuItem("Tools/Bus Rush/Capture Daily + Lucky Spin Screenshots (R8)")]
        public static void Run() => Start(false);

        public static void RunAndExit() => Start(true);

        private static void Start(bool exit)
        {
            SessionState.SetBool(Flag, true);
            SessionState.SetBool(ExitFlag, exit);
            // No auto-open during the run: pretend the popup already showed today.
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
                yield return new WaitForSecondsRealtime(1f);
                if (UIManager.Instance == null)
                {
                    ok = false;
                }
                else
                {
                    UIManager.Instance.CloseAll();
                    SaveData d = SaveSystem.Data;
                    d.dailyStreak = 2; // days 1-2 claimed, today = day 3
                    d.lastDailyClaim = DailyRewards.Today.AddDays(-1).ToString("yyyy-MM-dd");
                    d.luckySpinDate = DailyRewards.TodayKey;
                    d.luckySpinsUsed = 1;

                    UIManager.Instance.Open<DailyRewardPopup>();
                    yield return new WaitForSecondsRealtime(1f);
                    yield return RenderCapture.Capture(OutputDir, "daily_day3_390x844", 390, 844);
                    UIManager.Instance.CloseAll();
                    yield return new WaitForSecondsRealtime(0.4f);

                    LuckySpinPopup spin = UIManager.Instance.Open<LuckySpinPopup>();
                    yield return new WaitForSecondsRealtime(1.2f);
                    yield return RenderCapture.Capture(OutputDir, "lucky_spin_idle_390x844", 390, 844);
                    spin.ShowResult(4); // 100 coins
                    yield return new WaitForSecondsRealtime(0.8f);
                    yield return RenderCapture.Capture(OutputDir, "lucky_spin_won_390x844", 390, 844);
                }

                if (string.IsNullOrEmpty(backup)) PlayerPrefs.DeleteKey(SaveKey);
                else PlayerPrefs.SetString(SaveKey, backup);
                PlayerPrefs.Save();
                SaveSystem.Load();

                Debug.Log("[DailyShots] RESULT " + (ok ? "PASS" : "FAIL"));
                bool exit = SessionState.GetBool(ExitFlag, false);
                EditorApplication.ExitPlaymode();
                if (exit) EditorApplication.delayCall += () => EditorApplication.Exit(ok ? 0 : 1);
            }
        }
    }
}
