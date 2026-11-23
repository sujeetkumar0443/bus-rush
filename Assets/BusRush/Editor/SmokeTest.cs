using System.Collections;
using System.Collections.Generic;
using System.Text;
using BusRush.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Batchmode play-mode smoke test. Editor-only; changes no game code or assets.
    ///   Unity.exe -batchmode -projectPath ... -executeMethod BusRush.EditorTools.SmokeTest.Run -logFile smoke.log
    /// Env BUSRUSH_SMOKE_MODE = normal (default: Boot -> Splash -> Home -> Game, the real startup path)
    ///                          direct (opens Game.unity as the first scene)
    /// Env BUSRUSH_SMOKE_LEVELS = comma list, default "1,20". Env BUSRUSH_SMOKE_FRAMES = frames per level, default 600.
    /// Logs every Error / Exception / Assert with its stack trace, prints PASS or FAIL per level and overall, exits 0 / 1.
    /// </summary>
    [InitializeOnLoad]
    public static class SmokeTest
    {
        private const string Flag = "BusRush.SmokeTest.Active";

        static SmokeTest()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Flag, false)) return;
                SessionState.SetBool(Flag, false);
                var go = new GameObject("[SmokeTest]");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<Driver>();
            };
        }

        public static void Run()
        {
            string mode = Env("BUSRUSH_SMOKE_MODE", "normal");
            Debug.Log($"[SmokeTest] START mode={mode} batchMode={Application.isBatchMode} " +
                      $"graphicsDevice={SystemInfo.graphicsDeviceType} ({SystemInfo.graphicsDeviceName})");
            SessionState.SetBool(Flag, true);
            EditorSceneManager.OpenScene(mode == "direct"
                ? "Assets/BusRush/Scenes/Game.unity"
                : "Assets/BusRush/Scenes/Boot.unity");
            EditorApplication.EnterPlaymode();
        }

        private static string Env(string name, string fallback)
        {
            string v = System.Environment.GetEnvironmentVariable(name);
            return string.IsNullOrEmpty(v) ? fallback : v;
        }

        private sealed class Driver : MonoBehaviour
        {
            private struct Entry
            {
                public LogType type;
                public string message;
                public string stack;
                public int frame;
            }

            private readonly List<Entry> entries = new List<Entry>();

            private void OnEnable() => Application.logMessageReceivedThreaded += OnLog;
            private void OnDisable() => Application.logMessageReceivedThreaded -= OnLog;

            private void OnLog(string message, string stack, LogType type)
            {
                if (type is not (LogType.Error or LogType.Exception or LogType.Assert)) return;
                lock (entries)
                {
                    entries.Add(new Entry { type = type, message = message, stack = stack, frame = Time.frameCount });
                }
            }

            private IEnumerator Start()
            {
                string mode = Env("BUSRUSH_SMOKE_MODE", "normal");
                int frames = int.Parse(Env("BUSRUSH_SMOKE_FRAMES", "600"));
                var levels = new List<int>();
                foreach (string s in Env("BUSRUSH_SMOKE_LEVELS", "1,20").Split(',')) levels.Add(int.Parse(s.Trim()));

                bool allPass = true;
                var summary = new StringBuilder();

                if (mode == "normal")
                {
                    // Real startup path: Boot scene -> BootLoader -> SplashController -> Home.
                    int before = entries.Count;
                    bool reachedHome = false;
                    yield return WaitFor(() => SceneManager.GetActiveScene().name == SceneLoader.HomeScene && !SceneLoader.IsLoading,
                        60f, ok => reachedHome = ok);
                    yield return Frames(60);
                    bool startupPass = reachedHome && entries.Count == before;
                    Report("startup Boot->Splash->Home", startupPass, before, reachedHome ? "" : "timeout waiting for Home scene");
                    allPass &= startupPass;
                    summary.AppendLine($"  startup (Boot->Splash->Home): {(startupPass ? "PASS" : "FAIL")}");
                    if (!reachedHome)
                    {
                        Finish(false, summary);
                        yield break;
                    }
                }
                else
                {
                    SaveSystem.Load();
                }

                foreach (int level in levels)
                {
                    int before = entries.Count;
                    SaveSystem.Data.level = level;
                    SaveSystem.Commit();

                    bool loaded = false;
                    if (mode == "normal")
                    {
                        if (SceneManager.GetActiveScene().name != SceneLoader.HomeScene)
                        {
                            SceneLoader.LoadHome();
                            yield return Frames(2);
                            yield return WaitFor(() => SceneManager.GetActiveScene().name == SceneLoader.HomeScene && !SceneLoader.IsLoading,
                                30f, ok => { });
                        }
                        SceneLoader.LoadGame(); // same call HomeScreen's play button makes
                    }
                    else
                    {
                        SceneManager.LoadScene(SceneLoader.GameScene, LoadSceneMode.Single);
                    }

                    yield return Frames(2);
                    yield return WaitFor(() => SceneManager.GetActiveScene().name == SceneLoader.GameScene && !SceneLoader.IsLoading &&
                                               GameSession.Current != null && GameSession.Current.LevelData != null,
                        30f, ok => loaded = ok);

                    if (loaded) yield return Frames(frames);

                    int sessionLevel = GameSession.Current != null ? GameSession.Current.Level : -1;
                    bool pass = loaded && sessionLevel == level && entries.Count == before;
                    string note = !loaded ? "Game scene/GameSession not ready within 30s" :
                        sessionLevel != level ? $"GameSession.Level={sessionLevel}, expected {level}" : "";
                    Report($"level {level} ({mode}) after {frames} frames", pass, before, note);
                    summary.AppendLine($"  level {level}: {(pass ? "PASS" : "FAIL")}");
                    allPass &= pass;
                }

                Finish(allPass, summary);
            }

            private void Report(string label, bool pass, int firstIndex, string note)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"[SmokeTest] {label}: {(pass ? "PASS" : "FAIL")} {note}");
                lock (entries)
                {
                    for (int i = firstIndex; i < entries.Count; i++)
                    {
                        Entry e = entries[i];
                        sb.AppendLine($"[SmokeTest]   #{i - firstIndex + 1} {e.type} @frame {e.frame}: {e.message}");
                        sb.AppendLine(e.stack);
                    }
                }
                Debug.Log(sb.ToString());
            }

            private void Finish(bool pass, StringBuilder summary)
            {
                Debug.Log($"[SmokeTest] RESULT {(pass ? "PASS" : "FAIL")} (total error/exception log entries: {entries.Count})\n{summary}");
                EditorApplication.ExitPlaymode();
                EditorApplication.delayCall += () => EditorApplication.Exit(pass ? 0 : 1);
            }

            private static IEnumerator Frames(int n)
            {
                for (int i = 0; i < n; i++) yield return null;
            }

            private static IEnumerator WaitFor(System.Func<bool> cond, float timeoutSeconds, System.Action<bool> result)
            {
                float deadline = Time.realtimeSinceStartup + timeoutSeconds;
                while (!cond())
                {
                    if (Time.realtimeSinceStartup > deadline)
                    {
                        result(false);
                        yield break;
                    }
                    yield return null;
                }
                result(true);
            }
        }
    }
}
