using System.Collections;
using BusRush.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Road Map v3 screenshots (Boot -> Home): the map at level 20, the Level Start card zooming in (3 frames), the world intro card
    /// (level 21, first of Snow World) and 4 frames of the level-complete sequence (level 20 won). Uses a temporary editor save
    /// (restored at the end). Windowed editor only; BUSRUSH_SHOTDIR / BUSRUSH_SHOTSCALE=2 give unity_shots/map_v3 at 780x1688.
    /// Command line: -executeMethod BusRush.EditorTools.RoadMapScreenshotTool.RunAndExit (no -batchmode, no -quit).
    /// </summary>
    [InitializeOnLoad]
    public static class RoadMapScreenshotTool
    {
        public const string OutputDir = "C:/UnityBuilds/Bus Escape Puzzle/unity_shots/map_v3";
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

        [MenuItem("Tools/Bus Rush/Capture Road Map Screenshots (v3)")]
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
                for (float wait = 0f; map == null && wait < 20f; wait += Time.unscaledDeltaTime)
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
                    for (int level = 1; level < 20; level++) d.SetBestStars(level, level % 3 == 0 ? 2 : 3);
                    d.level = 20;
                    d.mapUnlockShown = 20;
                    d.worldIntroShown = 1;
                    d.giftChestsClaimed.Clear();
                    d.giftChestsClaimed.Add(5);
                    map.Open();
                    yield return new WaitForSecondsRealtime(1f);
                    yield return Shot("map_level20");

                    // Level Start card: 3 frames of the zoom out of node 20.
                    map.TapLevel(20);
                    LevelStartCard card = map.Card;
                    foreach ((float p, string name) in new[] { (0.25f, "card_zoom_1"), (0.6f, "card_zoom_2"), (1f, "card_zoom_3") })
                    {
                        while (card.OpenProgress < p && card.OpenProgress < 1f) yield return null;
                        Time.timeScale = 0f;
                        Debug.Log($"[MapShots] {name} at open progress {card.OpenProgress:0.00}");
                        yield return Shot(name);
                        Time.timeScale = 1f;
                    }
                    yield return new WaitForSecondsRealtime(0.4f);
                    card.RequestClose();
                    yield return new WaitForSecondsRealtime(0.5f);

                    // World intro card: level 21 is the first level of Snow World.
                    d.level = 21;
                    d.SetBestStars(20, 3);
                    d.mapUnlockShown = 21;
                    map.Open();
                    yield return new WaitForSecondsRealtime(1f);
                    map.TapLevel(21);
                    yield return new WaitForSecondsRealtime(0.8f);
                    yield return Shot("card_world_intro");
                    card.RequestClose();
                    yield return new WaitForSecondsRealtime(0.5f);

                    // Level 20 just won: the level-complete sequence, 4 frames.
                    d.level = 21;
                    d.mapUnlockShown = 19;
                    map.Open(20);
                    foreach ((float t, string name) in new[] { (0.30f, "unlock_1_scroll"), (0.75f, "unlock_2_pop"), (1.35f, "unlock_3_path"), (2.07f, "unlock_4_crack") })
                    {
                        while (map.SequenceActive && map.SequenceTime < t) yield return null;
                        Time.timeScale = 0f;
                        Debug.Log($"[MapShots] {name} at sequence {map.SequenceTime:0.000}s");
                        yield return Shot(name);
                        Time.timeScale = 1f;
                    }
                    yield return new WaitForSecondsRealtime(1f);
                    yield return Shot("unlock_5_done");
                }

                if (string.IsNullOrEmpty(backup)) PlayerPrefs.DeleteKey(SaveKey);
                else PlayerPrefs.SetString(SaveKey, backup);
                PlayerPrefs.Save();
                SaveSystem.Load();
                Time.timeScale = 1f;

                Debug.Log("[MapShots] RESULT " + (ok ? "PASS" : "FAIL"));
                bool exit = SessionState.GetBool(ExitFlag, false);
                EditorApplication.ExitPlaymode();
                if (exit) EditorApplication.delayCall += () => EditorApplication.Exit(ok ? 0 : 1);
            }

            private static IEnumerator Shot(string name) => RenderCapture.Capture(OutputDir, name + "_390x844", 390, 844);
        }
    }
}
