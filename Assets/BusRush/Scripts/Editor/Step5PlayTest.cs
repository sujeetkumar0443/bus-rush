using System.Collections;
using System.Linq;
using BusRush.Gameplay;
using BusRush.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Dev tool for Step 5 (windowed editor only). Screenshots of the crowd loop at 390x844 and 360x800, then a bot plays
    /// levels through the real tap path: board when possible, otherwise tap a free vehicle matching the queue front, else
    /// the free vehicle needed soonest while keeping one bay spare (same policy as GameSimulator).
    /// Env: BUSRUSH_LEVELS=a-b (default 1-30), BUSRUSH_SHOTS=0 to skip screenshots. Resets the editor save.
    /// </summary>
    [InitializeOnLoad]
    public static class Step5PlayTest
    {
        /// <summary>Output folder; override with BUSRUSH_SHOTDIR (e.g. .../unity_shots/step5b).</summary>
        public static string ShotsDir =>
            System.Environment.GetEnvironmentVariable("BUSRUSH_SHOTDIR") ?? "C:/UnityBuilds/BusRush_Source/unity_shots/step5";
        private const string Flag = "BusRush.Step5PlayTest.Active";
        private const string ExitFlag = "BusRush.Step5PlayTest.Exit";
        private const float PlayTimeScale = 4f;

        static Step5PlayTest()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Flag, false)) return;
                SessionState.SetBool(Flag, false);
                var go = new GameObject("[Step5PlayTest]");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<Driver>();
            };
        }

        [MenuItem("Tools/Bus Rush/Play Test + Screenshots (Step 5)")]
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

            private IEnumerator Start()
            {
                SaveSystem.ResetToDefaults();
                yield return WaitScene(SceneLoader.HomeScene);

                int first = 1, last = 30;
                string range = System.Environment.GetEnvironmentVariable("BUSRUSH_LEVELS");
                if (!string.IsNullOrEmpty(range) && range.Contains("-"))
                {
                    string[] parts = range.Split('-');
                    first = int.Parse(parts[0]);
                    last = int.Parse(parts[1]);
                }
                if (System.Environment.GetEnvironmentVariable("BUSRUSH_SHOTS") != "0") yield return Screenshots();

                int won = 0, total = last - first + 1;
                for (int level = first; level <= last; level++)
                {
                    yield return LoadLevel(level);
                    GameSession s = GameSession.Current;
                    int passengers = s.LevelData.queue.Count;
                    Time.timeScale = PlayTimeScale;
                    float deadline = Time.realtimeSinceStartup + 240f;
                    while (!s.IsCleared && !s.IsLost && Time.realtimeSinceStartup < deadline)
                    {
                        BotStep(s);
                        yield return new WaitForSecondsRealtime(0.05f);
                    }
                    Time.timeScale = 1f;
                    yield return new WaitForSecondsRealtime(1.2f);

                    bool popup = UIManager.Instance != null && UIManager.Instance.IsOpen<LevelCompletePopup>();
                    bool ok = s.IsCleared && popup && s.BoardedCount == passengers && s.PassengersLeft == 0 &&
                              s.Board.RemainingCount == 0 && s.Bays.AllEmpty && SaveSystem.Data.level == level + 1;
                    if (ok) won++;
                    string why = s.IsLost ? $" LOST front={(s.Crowd.HasFront ? s.Crowd.FrontColor.ToString() : "-")} " +
                                            $"bays=[{string.Join(", ", s.Bays.Bays.Where(b => b.Vehicle != null).Select(b => $"{b.Vehicle.Data.color} {b.Filled}/{b.Capacity}"))}]"
                                          : (!s.IsCleared ? " TIMEOUT" : "");
                    Debug.Log($"[Step5PlayTest] level {level}: {(ok ? "WON" : "FAILED")} vehicles={s.LevelData.vehicles.Count} " +
                              $"passengers={passengers} boarded={s.BoardedCount} departed={s.DepartedCount} taps={s.Moves}{why}");
                    UIManager.Instance.CloseAll();
                }

                bool pass = won == total && errors == 0 && mispicks == 0;
                Debug.Log($"[Step5PlayTest] RESULT {(pass ? "PASS" : "FAIL")}: levels {first}-{last}, won {won}/{total}, errors {errors}, mis-picked taps {mispicks}");
                Time.timeScale = 1f;
                SaveSystem.ResetToDefaults();
                EditorApplication.ExitPlaymode();
                if (SessionState.GetBool(ExitFlag, false)) EditorApplication.delayCall += () => EditorApplication.Exit(pass ? 0 : 1);
            }

            /// <summary>One bot decision (mirrors GameSimulator).</summary>
            private void BotStep(GameSession s)
            {
                if (!s.Crowd.HasFront) return;
                GameColor front = s.Crowd.FrontColor;

                // Wait while the front can (or soon will) board a vehicle that is parked or driving to a bay.
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
                if (pick != null) Tap(pick);
            }

            private IEnumerator Screenshots()
            {
                // Full queue at level 1
                yield return LoadLevel(1);
                GameSession s = GameSession.Current;
                yield return Both("01_full_queue_level1");

                // Mid-walk, boarding with pill, full vehicle leaving
                float t0 = Time.realtimeSinceStartup;
                bool walk = false, boarding = false, leaving = false;
                while (!(walk && boarding && leaving) && Time.realtimeSinceStartup - t0 < 60f)
                {
                    BotStep(s);
                    if (!walk && s.Crowd.WalkerCount >= 2)
                    {
                        walk = true;
                        yield return Both("02_mid_walk");
                        yield return RenderCapture.Capture(ShotsDir, "02_mid_walk_3x", 1170, 2532); // for a close-up crop
                    }
                    if (!boarding && s.Bays.Bays.Any(b => b.Vehicle != null && b.Filled >= 2 && b.Filled < b.Capacity))
                    {
                        boarding = true;
                        yield return Both("03_boarding_pill");
                        yield return RenderCapture.Capture(ShotsDir, "03_boarding_pill_3x", 1170, 2532);
                    }
                    if (!leaving && s.Bays.Bays.Any(b => b.Departing))
                    {
                        leaving = true;
                        yield return new WaitForSeconds(0.45f);
                        yield return Both("04_full_vehicle_leaving");
                    }
                    yield return null;
                }

                // Win confetti: finish level 1
                while (!s.IsCleared && Time.realtimeSinceStartup - t0 < 120f)
                {
                    BotStep(s);
                    yield return null;
                }
                yield return new WaitForSecondsRealtime(0.25f);
                yield return Both("05_win_confetti");
                yield return new WaitForSecondsRealtime(1f);

                // Lose: park vehicles nobody at the front wants until the bays are full.
                yield return LoadLevel(8);
                s = GameSession.Current;
                float t1 = Time.realtimeSinceStartup;
                while (!s.IsLost && Time.realtimeSinceStartup - t1 < 40f)
                {
                    if (s.Bays.HasFreeBay && s.Crowd.HasFront)
                    {
                        GameColor front = s.Crowd.FrontColor;
                        VehicleView wrong = s.Board.Vehicles.FirstOrDefault(v => s.Board.IsPathClear(v) && v.Data.color != front);
                        if (wrong != null) Tap(wrong);
                    }
                    yield return new WaitForSeconds(0.3f);
                }
                Debug.Log($"[Step5PlayTest] forced lose reached: {s.IsLost}, out-of-space popup: {UIManager.Instance.IsOpen<OutOfSpacePopup>()}");
                yield return new WaitForSecondsRealtime(0.4f);
                yield return Both("06_lose_state");
            }

            private static IEnumerator Both(string name)
            {
                yield return RenderCapture.Capture(ShotsDir, name + "_390x844", 390, 844);
                yield return RenderCapture.Capture(ShotsDir, name + "_360x800", 360, 800);
            }

            private void Tap(VehicleView v)
            {
                Vector3 screen = Camera.main.WorldToScreenPoint(v.transform.position + Vector3.up * 0.5f);
                VehicleView picked = Object.FindAnyObjectByType<InputController>().HandleTap(screen);
                if (picked != v)
                {
                    mispicks++;
                    Debug.LogWarning($"[Step5PlayTest] tap on {v.name} picked {(picked != null ? picked.name : "nothing")}");
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
                yield return new WaitForSecondsRealtime(1.6f);
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
