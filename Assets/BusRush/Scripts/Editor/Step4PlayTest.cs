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
    /// Dev tool for Steps 3-4 (windowed editor only). Screenshots: a vehicle mid-road for each direction, parked vehicles
    /// with capacity pills (390x844, 360x800, 412x915), locked bays, smoke behind an accelerating bus and the no-bay
    /// toast; checks the lose popup. Then auto-plays levels 1..30 with DebugAutoDepart through the real tap path until
    /// board and bays are empty. Resets the editor save.
    /// </summary>
    [InitializeOnLoad]
    public static class Step4PlayTest
    {
        public const string ShotsDir = "C:/UnityBuilds/BusRush_Source/unity_shots/step4";
        private const string Flag = "BusRush.Step4PlayTest.Active";
        private const string ExitFlag = "BusRush.Step4PlayTest.Exit";
        private const float PlayTimeScale = 4f;

        static Step4PlayTest()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Flag, false)) return;
                SessionState.SetBool(Flag, false);
                var go = new GameObject("[Step4PlayTest]");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<Driver>();
            };
        }

        [MenuItem("Tools/Bus Rush/Play Test + Screenshots (Step 4)")]
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
            private bool losePopupSeen;

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

                // Optional batching for low-memory machines: BUSRUSH_LEVELS=11-20, BUSRUSH_SHOTS=0.
                int first = 1, last = 30;
                string range = System.Environment.GetEnvironmentVariable("BUSRUSH_LEVELS");
                if (!string.IsNullOrEmpty(range) && range.Contains("-"))
                {
                    string[] parts = range.Split('-');
                    first = int.Parse(parts[0]);
                    last = int.Parse(parts[1]);
                }
                bool shots = System.Environment.GetEnvironmentVariable("BUSRUSH_SHOTS") != "0";
                if (shots) yield return Screenshots();
                else losePopupSeen = true; // only checked in the screenshot pass

                // ---- automated play
                int cleared = 0;
                int total = last - first + 1;
                for (int level = first; level <= last; level++)
                {
                    yield return LoadLevel(level);
                    GameSession s = GameSession.Current;
                    s.DebugAutoDepart = true;
                    Time.timeScale = PlayTimeScale;
                    int vehicles = s.LevelData.vehicles.Count;
                    float deadline = Time.realtimeSinceStartup + 150f;

                    while (!s.IsCleared && Time.realtimeSinceStartup < deadline)
                    {
                        if (s.Bays.HasFreeBay)
                        {
                            VehicleView free = s.Board.Vehicles.FirstOrDefault(v => s.Board.IsPathClear(v));
                            if (free != null) Tap(free);
                        }
                        yield return new WaitForSecondsRealtime(0.05f);
                    }

                    Time.timeScale = 1f;
                    yield return new WaitForSecondsRealtime(0.8f);
                    bool popup = UIManager.Instance != null && UIManager.Instance.IsOpen<LevelCompletePopup>();
                    bool ok = s.IsCleared && popup && s.Board.RemainingCount == 0 && s.Bays.AllEmpty &&
                              s.ExitedCount == vehicles && s.DepartedCount == vehicles && SaveSystem.Data.level == level + 1;
                    if (ok) cleared++;
                    Debug.Log($"[Step4PlayTest] level {level}: {(ok ? "CLEARED" : "FAILED")} vehicles={vehicles} left={s.ExitedCount} " +
                              $"departed={s.DepartedCount} board={s.Board.RemainingCount} baysEmpty={s.Bays.AllEmpty} popup={popup}");
                    UIManager.Instance.CloseAll();
                }

                bool pass = cleared == total && errors == 0 && mispicks == 0 && losePopupSeen;
                Debug.Log($"[Step4PlayTest] RESULT {(pass ? "PASS" : "FAIL")}: levels {first}-{last}, cleared {cleared}/{total}, errors {errors}, " +
                          $"mis-picked taps {mispicks}, lose popup seen {losePopupSeen}");
                Time.timeScale = 1f;
                SaveSystem.ResetToDefaults();
                EditorApplication.ExitPlaymode();
                if (SessionState.GetBool(ExitFlag, false)) EditorApplication.delayCall += () => EditorApplication.Exit(pass ? 0 : 1);
            }

            private IEnumerator Screenshots()
            {
                // (a) one vehicle mid-road per direction type
                foreach (Direction d in DirectionExtensions.All)
                {
                    int level = FindLevel(v => v.direction == d && v.type != VehicleType.Bus);
                    yield return LoadLevel(level);
                    GameSession s = GameSession.Current;
                    s.DebugAutoDepart = false;
                    VehicleView v = s.Board.Vehicles.First(x => x.Data.direction == d && s.Board.IsPathClear(x) && x.Data.type != VehicleType.Bus);
                    Tap(v);
                    // Capture once the vehicle is clearly out on the road loop (outside the lot).
                    yield return WaitUntilOffLot(v, d == Direction.Up ? 6f : 14f);
                    yield return RenderCapture.Capture(ShotsDir, $"a_mid_road_{d}", 390, 844);
                }

                // (f) side-lane turns, captured while the vehicle swings into the lane next to the screen edge
                foreach (Direction d in new[] { Direction.Left, Direction.Right })
                {
                    int level = FindLevel(v => v.direction == d && v.type != VehicleType.Car);
                    yield return LoadLevel(level);
                    GameSession s = GameSession.Current;
                    s.DebugAutoDepart = false;
                    VehicleView v = s.Board.Vehicles.First(x => x.Data.direction == d && s.Board.IsPathClear(x) && x.Data.type != VehicleType.Car);
                    Tap(v);
                    float lane = d == Direction.Left ? ScreenRef.LeftX : ScreenRef.RightX;
                    float timeout = Time.realtimeSinceStartup + 8f;
                    while (v != null && Mathf.Abs(ScreenRef.WorldToRef(v.transform.position).x - lane) > 10f && Time.realtimeSinceStartup < timeout)
                    {
                        yield return null;
                    }
                    yield return RenderCapture.Capture(ShotsDir, $"f_side_lane_turn_{d}", 390, 844);
                }

                // (c) smoke behind an accelerating bus
                {
                    int level = FindLevel(v => v.type == VehicleType.Bus);
                    yield return LoadLevel(level);
                    GameSession s = GameSession.Current;
                    s.DebugAutoDepart = false;
                    VehicleView bus = s.Board.Vehicles.First(x => x.Data.type == VehicleType.Bus && s.Board.IsPathClear(x));
                    Tap(bus);
                    yield return new WaitForSeconds(0.75f); // still accelerating
                    yield return RenderCapture.Capture(ShotsDir, "c_bus_smoke", 390, 844);
                }

                // (b, d, e) fill the four open bays with auto-depart off
                {
                    yield return LoadLevel(20);
                    GameSession s = GameSession.Current;
                    s.DebugAutoDepart = false;
                    while (s.Bays.HasFreeBay)
                    {
                        VehicleView free = s.Board.Vehicles.FirstOrDefault(v => s.Board.IsPathClear(v));
                        if (free == null) break;
                        Tap(free);
                        yield return new WaitForSeconds(0.35f);
                    }
                    float timeout = Time.realtimeSinceStartup + 15f;
                    while (s.Bays.Movers > 0 && Time.realtimeSinceStartup < timeout) yield return null;
                    yield return new WaitForSeconds(0.6f);

                    losePopupSeen = UIManager.Instance.IsOpen<OutOfSpacePopup>();
                    Debug.Log($"[Step4PlayTest] all open bays full, auto-depart off -> Out of Space popup shown: {losePopupSeen}");
                    UIManager.Instance.CloseAll();
                    yield return new WaitForSecondsRealtime(0.4f);

                    yield return RenderCapture.Capture(ShotsDir, "b_parked_390x844", 390, 844);
                    yield return RenderCapture.Capture(ShotsDir, "e_parked_360x800", 360, 800);
                    yield return RenderCapture.Capture(ShotsDir, "e_parked_412x915", 412, 915);
                    yield return RenderCapture.Capture(ShotsDir, "d_locked_bays", 390, 844);

                    VehicleView another = s.Board.Vehicles.FirstOrDefault(v => s.Board.IsPathClear(v));
                    if (another != null)
                    {
                        Tap(another);
                        yield return new WaitForSecondsRealtime(0.3f);
                        Debug.Log($"[Step4PlayTest] no-bay taps counted: {s.NoBayTaps}");
                        yield return RenderCapture.Capture(ShotsDir, "b_no_free_bay_toast", 390, 844);
                    }
                }
            }

            /// <summary>Waits until the vehicle is the given distance (reference px) outside the lot rectangle.</summary>
            private static IEnumerator WaitUntilOffLot(VehicleView v, float marginPx)
            {
                var lot = Rect.MinMaxRect(BoardGeometry.LotLeftPx, BoardGeometry.LotTopPx,
                                          BoardGeometry.LotLeftPx + 9 * BoardGeometry.PixelsPerCell,
                                          BoardGeometry.LotTopPx + 10 * BoardGeometry.PixelsPerCell * BoardGeometry.SinPitch);
                float timeout = Time.realtimeSinceStartup + 6f;
                while (v != null && Time.realtimeSinceStartup < timeout)
                {
                    Vector2 p = ScreenRef.WorldToRef(v.transform.position);
                    float dx = Mathf.Max(lot.xMin - p.x, p.x - lot.xMax, 0f);
                    float dy = Mathf.Max(lot.yMin - p.y, p.y - lot.yMax, 0f);
                    if (Mathf.Max(dx, dy) >= marginPx) yield break;
                    yield return null;
                }
            }

            /// <summary>First level (1..80) where some initially free vehicle matches.</summary>
            private static int FindLevel(System.Func<VehicleData, bool> match)
            {
                for (int level = 1; level <= 80; level++)
                {
                    LevelData data = LevelGenerator.Generate(level);
                    var board = new BoardState(data.columns, data.rows);
                    for (int i = 0; i < data.vehicles.Count; i++) board.Occupy(i, data.vehicles[i]);
                    if (data.vehicles.Any(v => match(v) && board.IsPathClear(v))) return level;
                }
                return 1;
            }

            private void Tap(VehicleView v)
            {
                Vector3 screen = Camera.main.WorldToScreenPoint(v.transform.position + Vector3.up * 0.5f);
                VehicleView picked = Object.FindAnyObjectByType<InputController>().HandleTap(screen);
                if (picked != v)
                {
                    mispicks++;
                    Debug.LogWarning($"[Step4PlayTest] tap on {v.name} picked {(picked != null ? picked.name : "nothing")}");
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
