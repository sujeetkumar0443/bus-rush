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
            private int overlapFrames;

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
                if (Env("BUSRUSH_CITYV2", false)) yield return CityV2Checks();

                int won = 0, total = Mathf.Max(0, last - first + 1);
                for (int level = first; level <= last; level++)
                {
                    yield return LoadLevel(level);
                    GameSession s = GameSession.Current;
                    int passengers = s.LevelData.queue.Count;
                    if (level == 1) StartCoroutine(DiagnoseFirstPark(s));
                    Time.timeScale = PlayTimeScale;
                    float levelStartReal = Time.realtimeSinceStartup, levelStartGame = Time.time;
                    float deadline = Time.realtimeSinceStartup + 180f;
                    while (!s.IsCleared && !s.IsLost && Time.realtimeSinceStartup < deadline)
                    {
                        BotStep(s);
                        CheckOverlaps(level);
                        yield return new WaitForSecondsRealtime(0.04f);
                    }
                    float levelReal = Time.realtimeSinceStartup - levelStartReal, levelGame = Time.time - levelStartGame;
                    Time.timeScale = 1f;
                    yield return new WaitForSecondsRealtime(1.2f);

                    LevelCompleteScreen complete = LevelCompleteScreen.Find();
                    bool popup = complete != null && complete.IsOpen;
                    bool ok = s.IsCleared && popup && s.BoardedCount == passengers && s.PassengersLeft == 0 &&
                              s.Board.RemainingCount == 0 && s.Bays.AllEmpty && SaveSystem.Data.level == level + 1 &&
                              SaveSystem.Data.GetStars(level) == 3;
                    if (ok) won++;
                    if (!s.IsCleared && !s.IsLost)
                    {
                        Debug.LogWarning($"[Step6PlayTest] TIMEOUT state level {level}: traffic:{VehicleMotion.DescribeTraffic()}");
                        Debug.LogWarning($"[Step6PlayTest] TIMEOUT bays: " + string.Join(", ", s.Bays.Bays.Select(b =>
                            $"bay{b.Index} {(b.Vehicle != null ? b.Vehicle.Data.color + " " + b.Filled + "+" + b.SeatsReserved + "r/" + b.Capacity : "empty")}" +
                            $"{(b.Reserved ? " reserved" : "")}{(b.Departing ? " departing" : "")}{(b.Full ? " full" : "")}{(b.IsFree ? " free" : "")}")) +
                            $"; movers={s.Bays.Movers} walking={s.Crowd.WalkerCount} board={s.Board.RemainingCount} " +
                            $"clear={string.Join(",", s.Board.Vehicles.Where(v => s.Board.IsPathClear(v)).Select(v => v.Data.color.ToString()))}; " +
                            $"window: {s.Crowd.DescribeFront(8)}");
                    }
                    string why = s.IsLost ? $" LOST front={(s.Crowd.HasFront ? s.Crowd.FrontColor.ToString() : "-")}" : (!s.IsCleared ? " TIMEOUT" : "");
                    Debug.Log($"[Step6PlayTest] level {level} ({LevelPlan.Get(level).tier}): {(ok ? "WON" : "FAILED")} vehicles={s.LevelData.vehicles.Count} " +
                              $"passengers={passengers} stars={s.Stars} wrongTaps={s.WrongTaps} time={levelGame:F1}s game ({levelReal:F1}s real){why}");
                    UIManager.Instance.CloseAll();
                }

                bool pass = won == total && errors == 0 && mispicks == 0 && overlapFrames == 0;
                Debug.Log($"[Step6PlayTest] RESULT {(pass ? "PASS" : "FAIL")}: levels {first}-{last}, won {won}/{total}, errors {errors}, " +
                          $"mis-picked taps {mispicks}, vehicle overlap frames {overlapFrames}");
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

            /// <summary>Fails the run if any two off-board vehicles (moving or parked) overlap (footprints shrunk 10%).</summary>
            private void CheckOverlaps(int level)
            {
                int n = VehicleMotion.CountOverlaps(0.9f);
                if (n == 0) return;
                overlapFrames++;
                if (overlapFrames <= 12) Debug.LogWarning($"[Step6PlayTest] level {level}: {n} overlapping vehicle pair(s):{VehicleMotion.DescribeOverlaps(0.9f)}");
            }

            // ------------------------------------------------------------------ City v2 (step 6b)

            private IEnumerator CityV2Checks()
            {
                // 1) Background-only render for the alignment check (board, crowd, bays and UI hidden).
                yield return LoadLevel(1);
                yield return new WaitForSecondsRealtime(1.5f);
                var hidden = new System.Collections.Generic.List<Renderer>();
                foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
                {
                    if (r.GetComponent<WorldBackground>() != null || !r.enabled) continue;
                    r.enabled = false;
                    hidden.Add(r);
                }
                var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude).Where(c => c.enabled).ToList();
                foreach (Canvas c in canvases) c.enabled = false;
                yield return RenderCapture.Capture(ShotsDir, "bg_only_390x844", 390, 844);
                foreach (Renderer r in hidden) if (r != null) r.enabled = true;
                foreach (Canvas c in canvases) if (c != null) c.enabled = true;

                // 2) Passenger size on a 390x844 screen (standing people, projected renderer bounds).
                yield return MeasurePassengers();

                // 3) Full crowd at level 1 (three resolutions).
                yield return Three("01_level1_full_crowd");

                // 4) Several vehicles arriving at bays at once.
                GameSession s = GameSession.Current;
                int sent = 0;
                foreach (VehicleView v in s.Board.Vehicles.Where(v => s.Board.IsPathClear(v)).ToList())
                {
                    if (sent >= 4 || !s.Bays.HasFreeBay) break;
                    Tap(v, false);
                    sent++;
                    yield return new WaitForSeconds(0.12f);
                }
                yield return new WaitForSeconds(0.9f);
                CheckOverlaps(1);
                yield return Three("03_vehicles_arriving");

                // 5) Mid-boarding.
                float t0 = Time.realtimeSinceStartup;
                while (s.Crowd.WalkerCount < 2 && Time.realtimeSinceStartup - t0 < 20f)
                {
                    BotStep(s);
                    CheckOverlaps(1);
                    yield return null;
                }
                yield return Three("02_mid_boarding");

                // 6) Denser crowd: level 20 (boss, 100 passengers) right after the intro walk-in.
                yield return LoadLevel(20);
                yield return new WaitForSecondsRealtime(2.5f);
                yield return RenderCapture.Capture(ShotsDir, "04_level20_crowd_390x844", 390, 844);
            }

            private IEnumerator MeasurePassengers()
            {
                Camera cam = Camera.main;
                var rt = new RenderTexture(390, 844, 24);
                cam.targetTexture = rt;
                cam.GetComponent<CameraFitter>().Fit(390f / 844f);
                yield return null;
                float wSum = 0f, hSum = 0f;
                int n = 0;
                foreach (PassengerView p in Object.FindObjectsByType<PassengerView>(FindObjectsInactive.Exclude))
                {
                    Bounds b = default;
                    bool first = true;
                    foreach (Renderer r in p.GetComponentsInChildren<Renderer>())
                    {
                        if (r.name == "Shadow") continue;
                        if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
                    }
                    if (first) continue;
                    float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                        Vector3 sp = cam.WorldToScreenPoint(corner);
                        minX = Mathf.Min(minX, sp.x); maxX = Mathf.Max(maxX, sp.x);
                        minY = Mathf.Min(minY, sp.y); maxY = Mathf.Max(maxY, sp.y);
                    }
                    wSum += maxX - minX;
                    hSum += maxY - minY;
                    n++;
                }
                cam.targetTexture = null;
                cam.GetComponent<CameraFitter>().Fit();
                rt.Release();
                Object.Destroy(rt);
                Debug.Log($"[Step6PlayTest] PASSENGER SIZE on 390x844: {wSum / Mathf.Max(1, n):F1} x {hSum / Mathf.Max(1, n):F1} px " +
                          $"(projected bounds, mean of {n} people; target 24 x 30)");
            }

            private static IEnumerator Three(string name)
            {
                yield return RenderCapture.Capture(ShotsDir, name + "_390x844", 390, 844);
                yield return RenderCapture.Capture(ShotsDir, name + "_360x800", 360, 800);
                yield return RenderCapture.Capture(ShotsDir, name + "_412x915", 412, 915);
            }

            /// <summary>Level 1: what the queue front is when the first vehicle parks, and whether its colour boards.</summary>
            private static IEnumerator DiagnoseFirstPark(GameSession s)
            {
                BayState first = null;
                string atPark = null;
                void Handler(BayState b)
                {
                    if (first != null) return;
                    first = b;
                    GameColor c = b.Vehicle.Data.color;
                    BayState seat = s.FindSeat(s.Crowd.FrontColor);
                    atPark = $"{c} {b.Vehicle.Data.type} parked in bay {b.Index}; front={s.Crowd.FrontColor} frontReady={s.Crowd.IsReady(0)} " +
                             $"seatForFront={(seat != null ? "bay " + seat.Index : "none")}; first {c} at queue index {s.Crowd.DistanceToColor(c)}; " +
                             $"walking={s.Crowd.WalkerCount}; queue: {s.Crowd.DescribeFront(8)}";
                }
                s.OnVehicleParked += Handler;
                while (first == null && s != null) yield return null;
                if (s == null) yield break;
                s.OnVehicleParked -= Handler;
                Debug.Log("[Step6PlayTest] DIAG level 1 at first park: " + atPark);
                float t0 = Time.time;
                for (int k = 1; k <= 4; k++)
                {
                    yield return new WaitForSeconds(0.75f);
                    if (s == null) yield break;
                    Debug.Log($"[Step6PlayTest] DIAG level 1 +{Time.time - t0:F2}s: boarded {s.BoardedCount}, walking {s.Crowd.WalkerCount}, parked=[" +
                              string.Join(", ", s.Bays.Bays.Where(b => b.IsParked).Select(b => $"bay{b.Index} {b.Vehicle.Data.color} {b.Filled}+{b.SeatsReserved}r/{b.Capacity}")) +
                              $"], queue: {s.Crowd.DescribeFront(6)}");
                }
            }

            // ------------------------------------------------------------------ bot

            /// <summary>
            /// Mirrors <see cref="GameSimulator"/> (window rule): while someone in the window can board, let them. Otherwise,
            /// with a free bay, park the free vehicle whose colour is needed soonest (skipping colours a parked or
            /// arriving vehicle still has room for); the last free bay is only used for a colour already in the window.
            /// </summary>
            private void BotStep(GameSession s)
            {
                if (!s.Crowd.HasFront || s.AnyoneCanBoard()) return;

                int freeBays = s.Bays.Bays.Count(b => b.IsFree);
                if (freeBays == 0) return;

                var free = s.Board.Vehicles.Where(v => s.Board.IsPathClear(v)).ToList();
                if (free.Count == 0) return;
                var pending = new System.Collections.Generic.HashSet<GameColor>(s.Bays.Bays
                    .Where(b => b.Vehicle != null && !b.Full && !b.Departing && b.Filled + b.SeatsReserved < b.Capacity)
                    .Select(b => b.Vehicle.Data.color));

                VehicleView pick = free.Where(v => !pending.Contains(v.Data.color))
                                       .OrderBy(v => s.Crowd.DistanceToColor(v.Data.color)).ThenBy(v => v.Id).FirstOrDefault();
                if (pick == null)
                {
                    if (freeBays < 2) return;
                    pick = free.OrderBy(v => s.Crowd.DistanceToColor(v.Data.color)).ThenBy(v => v.Id).First();
                }
                if (s.Crowd.DistanceToColor(pick.Data.color) >= s.Crowd.WindowCount && freeBays < 2) return;
                Tap(pick, true);
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
