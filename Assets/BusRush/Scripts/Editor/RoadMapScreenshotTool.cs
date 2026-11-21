using System.Collections;
using BusRush.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Step R3 check: Boot -> Home, then the Road Map at rest with level 5 current, the unlock of level 5 at 1.2 s
    /// (bus mid-drive) and 2.5 s later. Uses a temporary editor save (restored at the end). Windowed editor only.
    /// Command line: -executeMethod BusRush.EditorTools.RoadMapScreenshotTool.RunAndExit (no -batchmode, no -quit).
    /// </summary>
    [InitializeOnLoad]
    public static class RoadMapScreenshotTool
    {
        public const string OutputDir = "C:/UnityBuilds/BusRush_Source/unity_shots/R3";
        private const string SaveKey = "BusRush.Save";
        private const string Flag = "BusRush.MapShots.Active";
        private const string ExitFlag = "BusRush.MapShots.Exit";

        static RoadMapScreenshotTool()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Flag, false)) return;
                SessionState.SetBool(Flag, false);
                var go = new GameObject("[MapShots]");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<Driver>();
            };
        }

        [MenuItem("Tools/Bus Rush/Capture Road Map Screenshots (R3)")]
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
                RoadMapScreen map = null;
                for (float wait = 0f; map == null && wait < 15f; wait += Time.unscaledDeltaTime)
                {
                    if (FindAnyObjectByType<HomeScreen>() != null) map = FindAnyObjectByType<RoadMapScreen>(FindObjectsInactive.Include);
                    yield return null;
                }
                if (map == null)
                {
                    Debug.LogError("[MapShots] no RoadMapScreen in Home");
                    ok = false;
                }
                else
                {
                    yield return new WaitForSecondsRealtime(0.5f);
                    SaveData d = SaveSystem.Data;
                    d.level = 5;
                    int[] stars = { 3, 3, 2, 3 };
                    for (int i = 0; i < stars.Length; i++) d.SetBestStars(i + 1, stars[i]);
                    d.mapUnlockShown = 4;
                    map.Open();
                    yield return new WaitForSecondsRealtime(1f);
                    yield return Shot("map_rest_level5_390x844");

                    // Level 5 won with 3 stars: the map opens with its unlock.
                    d.level = 6;
                    d.SetBestStars(5, 3);
                    map.Open(5);
                    foreach ((float t, string name) in new[] { (1.2f, "map_unlock_1_2s_390x844"), (3.7f, "map_unlock_after_390x844") })
                    {
                        while (map.UnlockTime < t) yield return null;
                        Time.timeScale = 0f;
                        Debug.Log($"[MapShots] capturing {name} at unlock {map.UnlockTime:0.000}s");
                        yield return Shot(name);
                        Time.timeScale = 1f;
                    }
                }

                if (string.IsNullOrEmpty(backup)) PlayerPrefs.DeleteKey(SaveKey);
                else PlayerPrefs.SetString(SaveKey, backup);
                PlayerPrefs.Save();
                SaveSystem.Load();

                Debug.Log("[MapShots] RESULT " + (ok ? "PASS" : "FAIL"));
                bool exit = SessionState.GetBool(ExitFlag, false);
                EditorApplication.ExitPlaymode();
                if (exit) EditorApplication.delayCall += () => EditorApplication.Exit(ok ? 0 : 1);
            }

            private static IEnumerator Shot(string name) => RenderCapture.Capture(OutputDir, name, 390, 844);
        }
    }
}
