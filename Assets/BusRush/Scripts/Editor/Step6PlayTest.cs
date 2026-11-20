using System.Collections;
using System.Linq;
using BusRush.Gameplay;
using BusRush.UI;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Dev tool for Steps 5-6 (windowed editor only). Optional perf measurement (level 30, Android quality), screenshots,
    /// then a bot plays a range of levels through the real tap path (same policy as GameSimulator).
    /// Env: BUSRUSH_LEVELS=a-b (default 1-100), BUSRUSH_SHOTS=0|1, BUSRUSH_PERF=0|1, BUSRUSH_SHOTDIR. Resets the editor save.
    /// </summary>
    [InitializeOnLoad]
    public static class Step6PlayTest
    {
        private const string Flag = "BusRush.Step6PlayTest.Active";
        private const string ExitFlag = "BusRush.Step6PlayTest.Exit";
        private const float PlayTimeScale = 6f;

        public static string ShotsDir =>
            System.Environment.GetEnvironmentVariable("BUSRUSH_SHOTDIR") ?? "C:/UnityBuilds/BusRush_Source/unity_shots/step6";

        static Step6PlayTest()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Flag, false)) return;
                SessionState.SetBool(Flag, false);
                var go = new GameObject("[Step6PlayTest]");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<Driver>();
            };
        }

        [MenuItem("Tools/Bus Rush/Play Test + Screenshots (Step 6)")]
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
            private int errors;
            private int mispicks;

            private void OnEnable() => Application.logMessageReceived += OnLog;
            private void OnDisable() => Application.logMessageReceived -= OnLog;

            private void OnLog(string message, string stack, LogType type)
            {
                if (type is LogType.Error or LogType.Exception or LogType.Assert) errors++;
            }

            private static bool Env(string name, bool fallback)
            {
                string v = System.Environment.GetEnvironmentVariable(name);
                return string.IsNullOrEmpty(v) ? fallback : v != "0";
            }

            private IEnumerator Start()
            {
                SaveSystem.ResetToDefaults();
                yield return WaitScene(SceneLoader.HomeScene);

                int first = 1, last = 100;
                string range = System.Environment.GetEnvironmentVariable("BUSRUSH_LEVELS");
                if (!string.IsNullOrEmpty(range) && range.Contains("-"))
                {
                    string[] parts = range.Split('-');
                    first = int.Parse(parts[0]);
                    last = int.Parse(parts[1]);
                }

                if (Env("BUSRUSH_PERF", false))
                {
                    AllocProbe.Install(gameObject);
                    yield return MeasurePerf();
                }
                if (Env("BUSRUSH_SHOTS", false)) yield return Screenshots();

                int won = 0, total = Mathf.Max(0, last - first + 1);
                for (int level = first; level <= last; level++)
                {
                    yield return LoadLevel(level);
                    GameSession s = GameSession.Current;
                    int passengers = s.LevelData.queue.Count;
                    Time.timeScale = PlayTimeScale;
                    float deadline = Time.realtimeSinceStartup + 180f;
                    while (!s.IsCleared && !s.IsLost && Time.realtimeSinceStartup < deadline)
                    {
                        BotStep(s);
                        yield return new WaitForSecondsRealtime(0.04f);
                    }
                    Time.timeScale = 1f;
                    yield return new WaitForSecondsRealtime(1.2f);

                    bool popup = UIManager.Instance != null && UIManager.Instance.IsOpen<LevelCompletePopup>();
                    bool ok = s.IsCleared && popup && s.BoardedCount == passengers && s.PassengersLeft == 0 &&
                              s.Board.RemainingCount == 0 && s.Bays.AllEmpty && SaveSystem.Data.level == level + 1 &&
                              SaveSystem.Data.GetStars(level) == 3;
                    if (ok) won++;
                    string why = s.IsLost ? $" LOST front={(s.Crowd.HasFront ? s.Crowd.FrontColor.ToString() : "-")}" : (!s.IsCleared ? " TIMEOUT" : "");
                    Debug.Log($"[Step6PlayTest] level {level} ({LevelPlan.Get(level).tier}): {(ok ? "WON" : "FAILED")} vehicles={s.LevelData.vehicles.Count} " +
                              $"passengers={passengers} stars={s.Stars} wrongTaps={s.WrongTaps}{why}");
                    UIManager.Instance.CloseAll();
                }

                bool pass = won == total && errors == 0 && mispicks == 0;
                Debug.Log($"[Step6PlayTest] RESULT {(pass ? "PASS" : "FAIL")}: levels {first}-{last}, won {won}/{total}, errors {errors}, mis-picked taps {mispicks}");
                Time.timeScale = 1f;
                SaveSystem.ResetToDefaults();
                EditorApplication.ExitPlaymode();
                if (SessionState.GetBool(ExitFlag, false)) EditorApplication.delayCall += () => EditorApplication.Exit(pass ? 0 : 1);
            }

            // ------------------------------------------------------------------ perf

            private IEnumerator MeasurePerf()
            {
                // The editor (Windows target) cannot select the Android quality level, so apply its URP asset directly.
                var androidPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/Android_RPAsset.asset");
                RenderPipelineAsset previousPipeline = QualitySettings.renderPipeline;
                QualitySettings.renderPipeline = androidPipeline;

                yield return LoadLevel(30);
                yield return new WaitForSecondsRealtime(2.5f); // crowd settled, 70 visible
                GameSession s = GameSession.Current;
                int visible = Object.FindObjectsByType<PassengerView>(FindObjectsInactive.Exclude).Length;
                RenderPipelineAsset active = GraphicsSettings.currentRenderPipeline;
                Debug.Log($"[Step6PlayTest] PERF scene: level 30, vehicles={s.Board.RemainingCount}, visible passengers={visible}, " +
                          $"pipeline={(active != null ? active.name : "none")}");

                bool srp = GraphicsSettings.useScriptableRenderPipelineBatching;
                foreach (bool batcher in new[] { true, false })
                {
                    GraphicsSettings.useScriptableRenderPipelineBatching = batcher;
                    yield return null;
                    yield return Sample(batcher ? "SRP Batcher ON" : "SRP Batcher OFF (GPU instancing)");
                }
                GraphicsSettings.useScriptableRenderPipelineBatching = srp;
                QualitySettings.renderPipeline = previousPipeline;
            }

            private static IEnumerator Sample(string label)
            {
                using var draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
                using var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
                using var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
                using var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
                using var main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);

                const int frames = 120;
                AllocProbe.Reset();
                long d = 0, sp = 0, b = 0, g = 0, dMax = 0;
                double ms = 0;
                for (int i = 0; i < 10; i++) yield return null; // warm up
                for (int i = 0; i < frames; i++)
                {
                    yield return null;
                    d += draws.LastValue;
                    dMax = System.Math.Max(dMax, draws.LastValue);
                    sp += setPass.LastValue;
                    b += batches.LastValue;
                    g += gc.LastValue;
                    ms += main.LastValue / 1e6;
                }
                Debug.Log($"[Step6PlayTest] PERF {label}: draw calls avg {d / frames} (max {dMax}), SetPass {sp / frames}, batches {b / frames}, " +
                          $"GC alloc/frame {g / frames} B (whole editor), script Update..LateUpdate alloc/frame {AllocProbe.AverageBytes:F0} B, " +
                          $"main thread {ms / frames:F2} ms, frame {Time.unscaledDeltaTime * 1000f:F2} ms (editor)");
            }

            // ------------------------------------------------------------------ screenshots

            private IEnumerator Screenshots()
            {
                // Tutorial hand on level 1, blocked hint on level 2 (fresh save: tutorials not done).
                yield return LoadLevel(1);
                yield return new WaitForSecondsRealtime(1.4f);
                yield return Shot("01_tutorial_level1");

                yield return LoadLevel(2);
                yield return new WaitForSecondsRealtime(1.4f);
                yield return Shot("02_blocked_hint_level2");

                // Level Complete with 3 stars (no wrong taps) and with 1 star (3 wrong taps).
                yield return LoadLevel(4);
                yield return PlayToWin(GameSession.Current, 0);
                yield return new WaitForSecondsRealtime(1.8f);
                yield return Shot("03_level_complete_3_stars");
                UIManager.Instance.CloseAll();

                yield return LoadLevel(5);
                yield return PlayToWin(GameSession.Current, 3);
                yield return new WaitForSecondsRealtime(1.8f);
                yield return Shot("04_level_complete_1_star");
                UIManager.Instance.CloseAll();

                // Boss level 10, Snow level 6, Beach level 11 boards.
                yield return LoadLevel(10);
                yield return Shot("05_boss_level10");
                yield return LoadLevel(6);
                yield return Shot("06_snow_level6");
                yield return LoadLevel(11);
                yield return Shot("07_beach_level11");

                // Home with the level path: levels 1-6 done with mixed stars.
                for (int l = 1; l <= 6; l++) SaveSystem.Data.SetBestStars(l, l % 3 + 1);
                SaveSystem.Data.level = 7;
                SaveSystem.Commit();
                SceneLoader.LoadHome();
                yield return null;
                yield return WaitScene(SceneLoader.HomeScene);
                yield return new WaitForSecondsRealtime(0.8f);
                yield return Shot("08_home_level_path");
            }

            private IEnumerator PlayToWin(GameSession s, int wrongTaps)
            {
                for (int i = 0; i < wrongTaps; i++)
                {
                    VehicleView blocked = s.Board.Vehicles.FirstOrDefault(v => !s.Board.IsPathClear(v));
                    if (blocked != null) Tap(blocked, false);
                    yield return new WaitForSeconds(0.3f);
                }
                Time.timeScale = PlayTimeScale;
                float deadline = Time.realtimeSinceStartup + 120f;
                while (!s.IsCleared && Time.realtimeSinceStartup < deadline)
                {
                    BotStep(s);
                    yield return new WaitForSecondsRealtime(0.04f);
                }
                Time.timeScale = 1f;
            }

            private static IEnumerator Shot(string name)
            {
                yield return RenderCapture.Capture(ShotsDir, name + "_390x844", 390, 844);
            }

            // ------------------------------------------------------------------ bot

            private void BotStep(GameSession s)
            {
                if (!s.Crowd.HasFront) return;
                GameColor front = s.Crowd.FrontColor;
                bool served = s.Bays.Bays.Any(b => b.Vehicle != null && !b.Full && !b.Departing &&
                                                   b.Vehicle.Data.color == front && b.Filled + b.SeatsReserved < b.Capacity);
                if (served) return;

                int freeBays = s.Bays.Bays.Count(b => b.IsFree);
                if (freeBays == 0) return;

                var free = s.Board.Vehicles.Where(v => s.Board.IsPathClear(v)).ToList();
                VehicleView pick = free.Where(v => v.Data.color == front).OrderBy(v => v.Id).FirstOrDefault();
                if (pick == null && freeBays > 1)
                {
                    pick = free.OrderBy(v => s.Crowd.DistanceToColor(v.Data.color)).ThenBy(v => v.Id).FirstOrDefault();
                }
                if (pick != null) Tap(pick, true);
            }

            private void Tap(VehicleView v, bool checkPick)
            {
                Vector3 screen = Camera.main.WorldToScreenPoint(v.transform.position + Vector3.up * 0.5f);
                VehicleView picked = Object.FindAnyObjectByType<InputController>().HandleTap(screen);
                if (checkPick && picked != v)
                {
                    mispicks++;
                    Debug.LogWarning($"[Step6PlayTest] tap on {v.name} picked {(picked != null ? picked.name : "nothing")}");
                }
            }

            private static IEnumerator LoadLevel(int level)
            {
                Time.timeScale = 1f;
                if (UIManager.Instance != null) UIManager.Instance.CloseAll();
                SaveSystem.Data.level = level;
                SaveSystem.Commit();
                SceneLoader.LoadGame();
                yield return null;
                yield return WaitScene(SceneLoader.GameScene);
                yield return new WaitForSecondsRealtime(1.7f); // intro: pop-in, crowd walk-in, banner
            }

            private static IEnumerator WaitScene(string scene)
            {
                float timeout = Time.realtimeSinceStartup + 30f;
                while ((SceneManager.GetActiveScene().name != scene || SceneLoader.IsLoading ||
                        (scene == SceneLoader.GameScene && GameSession.Current == null)) && Time.realtimeSinceStartup < timeout)
                {
                    yield return null;
                }
            }
        }
    }
}
