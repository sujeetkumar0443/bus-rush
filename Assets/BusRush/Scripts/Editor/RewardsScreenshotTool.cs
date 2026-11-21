using System.Collections;
using BusRush.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Step R9 check: over the Road Map, the Gift Chest at 0.6 s (shaking) and 2.5 s (open, rewards), and New World
    /// (Snow) at 2.0 s. Nothing is collected; the editor save is restored. Windowed editor only. Command line:
    /// -executeMethod BusRush.EditorTools.RewardsScreenshotTool.RunAndExit (no -batchmode, no -quit).
    /// </summary>
    [InitializeOnLoad]
    public static class RewardsScreenshotTool
    {
        public const string OutputDir = "C:/UnityBuilds/BusRush_Source/unity_shots/R9";
        private const string SaveKey = "BusRush.Save";
        private const string Flag = "BusRush.RewardShots.Active";
        private const string ExitFlag = "BusRush.RewardShots.Exit";

        static RewardsScreenshotTool()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Flag, false)) return;
                SessionState.SetBool(Flag, false);
                var go = new GameObject("[RewardShots]");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<Driver>();
            };
        }

        [MenuItem("Tools/Bus Rush/Capture Gift Chest + New World Screenshots (R9)")]
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
                SaveSystem.Data.dailyAutoShown = DailyRewards.TodayKey; // no Daily popup in the way
                for (float wait = 0f; FindAnyObjectByType<HomeScreen>() == null && wait < 15f; wait += Time.unscaledDeltaTime) yield return null;
                yield return new WaitForSecondsRealtime(0.8f);
                if (UIManager.Instance != null) UIManager.Instance.CloseAll();

                RoadMapScreen map = FindAnyObjectByType<RoadMapScreen>(FindObjectsInactive.Include);
                GiftChestScreen chest = FindAnyObjectByType<GiftChestScreen>(FindObjectsInactive.Include);
                NewWorldScreen world = FindAnyObjectByType<NewWorldScreen>(FindObjectsInactive.Include);
                if (map == null || chest == null || world == null)
                {
                    Debug.LogError("[RewardShots] map / chest / world screen missing in Home");
                    ok = false;
                }
                else
                {
                    SaveSystem.Data.level = 6;
                    map.Open();
                    yield return new WaitForSecondsRealtime(1f);
                    chest.Show(5, null, null);
                    yield return At(() => chest.Elapsed, 0.6f, "gift_chest_0_6s_390x844");
                    yield return At(() => chest.Elapsed, 2.5f, "gift_chest_2_5s_390x844");
                    chest.gameObject.SetActive(false);
                    world.Show(2, null);
                    yield return At(() => world.Elapsed, 2.0f, "new_world_snow_2_0s_390x844");
                }

                if (string.IsNullOrEmpty(backup)) PlayerPrefs.DeleteKey(SaveKey);
                else PlayerPrefs.SetString(SaveKey, backup);
                PlayerPrefs.Save();
                SaveSystem.Load();

                Debug.Log("[RewardShots] RESULT " + (ok ? "PASS" : "FAIL"));
                bool exit = SessionState.GetBool(ExitFlag, false);
                EditorApplication.ExitPlaymode();
                if (exit) EditorApplication.delayCall += () => EditorApplication.Exit(ok ? 0 : 1);
            }

            private static IEnumerator At(System.Func<float> clock, float t, string name)
            {
                Time.timeScale = 1f;
                while (clock() < t) yield return null;
                Time.timeScale = 0f;
                Debug.Log($"[RewardShots] capturing {name} at {clock():0.000}s");
                yield return RenderCapture.Capture(OutputDir, name, 390, 844);
                Time.timeScale = 1f;
            }
        }
    }
}
