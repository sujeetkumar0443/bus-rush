using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BusRush;
using BusRush.Gameplay;
using TMPro;
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

                if (mode == "normal" || mode == "fixes")
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

                if (mode == "fixes")
                {
                    yield return Fixes(summary, ok => allPass &= ok);
                    Finish(allPass, summary);
                    yield break;
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

            // ------------------------------------------------------------------ fix checks (BUSRUSH_SMOKE_MODE=fixes)

            private static T Field<T>(Object obj, string name) where T : Object
            {
                SerializedProperty p = new SerializedObject(obj).FindProperty(name);
                return p != null ? p.objectReferenceValue as T : null;
            }

            private IEnumerator Fixes(StringBuilder summary, System.Action<bool> done)
            {
                bool all = true;
                void Result(string name, bool ok, string note)
                {
                    all &= ok;
                    Debug.Log($"[SmokeTest] fix {name}: {(ok ? "PASS" : "FAIL")} {note}");
                    summary.AppendLine($"  {name}: {(ok ? "PASS" : "FAIL")} {note}");
                }

                // ---- A: gift chest state comes from the save (progress set like the debug panel's go-to-level) ----
                int before = entries.Count;
                string aNote = "";
                bool aOk = true;
                SaveData d = SaveSystem.Data;
                d.level = 20;
                d.giftChestsClaimed.Clear();
                d.mapUnlockShown = 20;
                SaveSystem.Commit();
                var map = FindAnyObjectByType<BusRush.UI.RoadMapScreen>(FindObjectsInactive.Include);
                var chest = FindAnyObjectByType<BusRush.UI.GiftChestScreen>(FindObjectsInactive.Include);
                if (map == null || chest == null) { aOk = false; aNote = "RoadMapScreen / GiftChestScreen not found in Home"; }
                else
                {
                    aOk &= MapRewards.StateOf(d, 5) == MapRewards.ChestState.Ready && MapRewards.StateOf(d, 15) == MapRewards.ChestState.Ready &&
                           MapRewards.StateOf(d, 25) == MapRewards.ChestState.Locked;
                    if (!aOk) aNote += "StateOf wrong (want 5,15 READY, 25 LOCKED); ";
                    map.Open(0);
                    yield return Frames(20);
                    int ready = 0, locked = 0;
                    foreach (var v in FindObjectsByType<BusRush.UI.MapChestView>(FindObjectsInactive.Exclude))
                    {
                        if (v.State == MapRewards.ChestState.Ready) ready++;
                        if (v.State == MapRewards.ChestState.Locked) locked++;
                    }
                    aNote += $"visible chests ready={ready} locked={locked}; ";
                    aOk &= ready >= 1;

                    int coins0 = d.coins, vip0 = d.boosterVip, arr0 = d.boosterArrange, shuf0 = d.boosterShuffle;
                    map.OnChestTapped(25); // LOCKED: toast only
                    yield return Frames(5);
                    bool lockedOpened = chest.gameObject.activeSelf;
                    map.OnChestTapped(15); // READY: opens the screen
                    yield return Frames(5);
                    bool opened = chest.gameObject.activeSelf;
                    aOk &= !lockedOpened && opened;
                    aNote += $"locked tap opened={lockedOpened}, ready tap opened={opened}; ";
                    if (opened)
                    {
                        float t0 = Time.unscaledTime;
                        while (chest.Elapsed < 2.6f && Time.unscaledTime - t0 < 10f) yield return null;
                        Field<UnityEngine.UI.Button>(chest, "collectButton").onClick.Invoke();
                        yield return new WaitForSecondsRealtime(1.8f); // coin fly pays out
                    }
                    bool claimed = MapRewards.StateOf(d, 15) == MapRewards.ChestState.Claimed && d.giftChestsClaimed.Contains(15);
                    int boosterGain = (d.boosterVip - vip0) + (d.boosterArrange - arr0) + (d.boosterShuffle - shuf0);
                    aOk &= claimed && d.coins - coins0 >= GameTuning.GiftChestCoins && boosterGain == 1;
                    aNote += $"claimed15={claimed} coins+{d.coins - coins0} boosters+{boosterGain}; ";
                    int coinsAfter = d.coins;
                    map.OnChestTapped(15); // CLAIMED: nothing
                    yield return new WaitForSecondsRealtime(0.3f);
                    bool again = chest.gameObject.activeSelf || d.coins != coinsAfter;
                    bool five = MapRewards.StateOf(d, 5) == MapRewards.ChestState.Ready;
                    aOk &= !again && five;
                    aNote += $"claimed tap does nothing={!again}; chest5 still READY={five}";
                    map.Close();
                }
                Result("A chest state", aOk && entries.Count == before, aNote);

                // ---- B: Lucky Spin cannot be closed mid-spin, the result is always granted ----
                before = entries.Count;
                string bNote = "";
                bool bOk = true;
                d.luckySpinDate = string.Empty;
                d.luckySpinsUsed = 0;
                d.luckySpinPending = -1;
                SaveSystem.Commit();
                var ui = BusRush.UI.UIManager.Instance;
                var popup = ui != null ? ui.Open<BusRush.UI.LuckySpinPopup>() : null;
                if (popup == null) { bOk = false; bNote = "LuckySpinPopup did not open"; }
                else
                {
                    yield return new WaitForSecondsRealtime(1.2f);
                    var spin = Field<UnityEngine.UI.Button>(popup, "spinButton");
                    var closeBtn = Field<UnityEngine.UI.Button>(popup, "closeButton");
                    var dim = Field<UnityEngine.UI.Image>(popup, "dim");
                    var collect = Field<UnityEngine.UI.Button>(popup, "collectButton");
                    var wonGo = new SerializedObject(popup).FindProperty("wonRoot").objectReferenceValue as GameObject;
                    int coins0 = d.coins, vip0 = d.boosterVip, arr0 = d.boosterArrange, shuf0 = d.boosterShuffle;
                    spin.onClick.Invoke();
                    yield return new WaitForSecondsRealtime(0.6f);
                    int pending = d.luckySpinPending;
                    bool spinning = popup.IsSpinning;
                    popup.Close();
                    ui.CloseTop();
                    ui.HandleBack();
                    if (dim != null && dim.TryGetComponent(out UnityEngine.UI.Button dimButton)) dimButton.onClick.Invoke();
                    closeBtn.onClick.Invoke();
                    yield return Frames(10);
                    bool stillOpen = popup.IsOpen && popup.gameObject.activeInHierarchy;
                    bool xDisabled = closeBtn != null && !closeBtn.interactable;
                    bOk &= spinning && pending >= 0 && stillOpen && xDisabled;
                    bNote += $"spinning={spinning} pending={pending} open after close/back/dim/X={stillOpen} X disabled={xDisabled}; ";

                    // wait for the result card: still not closable; Collect pays and unlocks closing
                    float t1 = Time.unscaledTime;
                    while (!wonGo.activeSelf && Time.unscaledTime - t1 < 12f) yield return null;
                    bool cardShown = wonGo.activeSelf;
                    popup.Close();
                    yield return Frames(5);
                    bool openWithCard = popup.IsOpen;
                    collect.onClick.Invoke();
                    yield return new WaitForSecondsRealtime(1.8f);
                    RewardSpec r = pending >= 0 ? GameTuning.LuckySpinSegments[pending] : default;
                    int gain = (d.boosterVip - vip0) + (d.boosterArrange - arr0) + (d.boosterShuffle - shuf0);
                    bool paid = d.coins - coins0 == r.coins && gain == r.vip + r.arrange + r.shuffle && d.luckySpinPending == -1;
                    bool canClose = popup.CanClose;
                    popup.Close();
                    yield return new WaitForSecondsRealtime(0.4f);
                    bOk &= cardShown && openWithCard && paid && canClose && !popup.IsOpen;
                    bNote += $"card shown={cardShown}, still open before Collect={openWithCard}, paid={paid} (coins+{d.coins - coins0}, boosters+{gain}), closes after Collect={!popup.IsOpen}; ";

                    // B2: leaving mid-spin (scene leaves / object disabled) still grants the already-decided result
                    d.luckySpinDate = string.Empty;
                    d.luckySpinsUsed = 0;
                    SaveSystem.Commit();
                    popup = ui.Open<BusRush.UI.LuckySpinPopup>();
                    yield return new WaitForSecondsRealtime(1.0f);
                    spin = Field<UnityEngine.UI.Button>(popup, "spinButton");
                    coins0 = d.coins; vip0 = d.boosterVip; arr0 = d.boosterArrange; shuf0 = d.boosterShuffle;
                    spin.onClick.Invoke();
                    yield return new WaitForSecondsRealtime(0.5f);
                    pending = d.luckySpinPending;
                    popup.gameObject.SetActive(false); // what a scene unload does to it
                    yield return Frames(3);
                    r = pending >= 0 ? GameTuning.LuckySpinSegments[pending] : default;
                    gain = (d.boosterVip - vip0) + (d.boosterArrange - arr0) + (d.boosterShuffle - shuf0);
                    bool forced = pending >= 0 && d.luckySpinPending == -1 && d.coins - coins0 == r.coins && gain == r.vip + r.arrange + r.shuffle;
                    bOk &= forced;
                    bNote += $"leave mid-spin granted={forced} (segment {pending}, coins+{d.coins - coins0}, boosters+{gain})";
                    ui.CloseAll();
                }
                Result("B lucky spin", bOk && entries.Count == before, bNote);

                // ---- C: language selection works live, is saved, covers every string, Hindi draws with the static fallback ----
                before = entries.Count;
                string cNote = "";
                bool cOk = true;
                PlayerPrefs.DeleteKey(Localizer.PrefsKey);
                Localizer.ResetForTests();
                cOk &= Localizer.Current == Language.English;

                // table completeness: every key has a translation in every language
                int holes = 0;
                foreach (string key in Localizer.Keys.ToList())
                    for (int l = 1; l < Localizer.Count; l++)
                        if (string.IsNullOrEmpty(Localizer.Get(key, (Language)l))) holes++;
                cOk &= holes == 0;
                cNote += $"table keys={Localizer.Keys.Count()} untranslated cells={holes}; ";

                var settings = ui != null ? ui.Open<BusRush.UI.SettingsPopup>() : null;
                if (settings == null) { cOk = false; cNote += "SettingsPopup did not open"; }
                else
                {
                    yield return new WaitForSecondsRealtime(0.8f);
                    BusRush.UI.SettingsRow langRow = null;
                    foreach (var row in settings.GetComponentsInChildren<BusRush.UI.SettingsRow>(true))
                        if (row.Kind == BusRush.UI.SettingsRow.Setting.Language) langRow = row;
                    var langButton = Field<UnityEngine.UI.Button>(langRow, "button");
                    TMP_Text TextOf(string key)
                    {
                        foreach (var lt in FindObjectsByType<BusRush.UI.LocalizedText>(FindObjectsInactive.Exclude))
                            if (lt.Key == key) return lt.GetComponent<TMP_Text>();
                        return null;
                    }
                    var title = TextOf("SETTINGS");
                    var home = TextOf("Daily"); // Home screen label behind the popup
                    var level = FindAnyObjectByType<BusRush.UI.HomeScreen>();
                    var levelText = Field<TMP_Text>(level, "levelText");
                    var valueText = Field<TMP_Text>(langRow, "valueText");
                    cOk &= title != null && home != null && levelText != null && valueText != null && title.text == "SETTINGS";
                    cNote += $"English: title='{title?.text}' value='{valueText?.text}'; ";

                    foreach (Language lang in new[] { Language.Spanish, Language.Portuguese, Language.French, Language.Hindi })
                    {
                        langButton.onClick.Invoke(); // the real selection path: the row cycles to the next language
                        yield return Frames(3);
                        string wantTitle = Localizer.Get("SETTINGS", lang), wantDaily = Localizer.Get("Daily", lang);
                        string wantLevel = Localizer.F("Level {0}", SaveSystem.Data.level);
                        bool live = Localizer.Current == lang && title.text == wantTitle && home.text == wantDaily && levelText.text == wantLevel &&
                                    valueText.text == Localizer.NativeName(lang) + "  >";
                        bool saved = PlayerPrefs.GetInt(Localizer.PrefsKey, -1) == (int)lang;
                        // every character must really be drawn: no missing-glyph squares
                        int bad = 0;
                        foreach (TMP_Text t in new[] { title, home, levelText, valueText })
                        {
                            t.ForceMeshUpdate();
                            for (int i = 0; i < t.textInfo.characterCount; i++)
                            {
                                var ci = t.textInfo.characterInfo[i];
                                if (char.IsWhiteSpace(ci.character)) continue;
                                if (!ci.isVisible || ci.elementType != TMP_TextElementType.Character || !ci.fontAsset.HasCharacter(ci.character, false, false)) bad++;
                            }
                        }
                        cOk &= live && saved && bad == 0;
                        cNote += $"{Localizer.Code(lang)}: live={live} saved={saved} missingGlyphs={bad}; ";
                    }

                    // saved in PlayerPrefs and applied on startup: a new session reads it back
                    Localizer.ResetForTests();
                    bool restart = Localizer.Current == Language.Hindi;
                    cOk &= restart;
                    cNote += $"after restart language={Localizer.Current}; ";

                    langButton.onClick.Invoke(); // Hindi -> English again
                    yield return Frames(3);
                    cOk &= Localizer.Current == Language.English && title.text == "SETTINGS" && levelText.text == "Level " + SaveSystem.Data.level;
                    cNote += $"back to English title='{title.text}'";
                    ui.CloseAll();
                }
                PlayerPrefs.DeleteKey(Localizer.PrefsKey);
                Localizer.ResetForTests();
                Result("C language", cOk && entries.Count == before, cNote);

                // ---- D: Road Map v3: card on tap, Play starts the level, level-complete sequence, no bus ----
                before = entries.Count;
                string dNote = "";
                bool dOk = true;
                {
                    var map3 = FindAnyObjectByType<BusRush.UI.RoadMapScreen>(FindObjectsInactive.Include);
                    SaveData sd = SaveSystem.Data;
                    for (int lv = 1; lv < 20; lv++) sd.SetBestStars(lv, 3);
                    sd.worldIntroShown = 1;

                    // D1: level 20 just completed -> the sequence ends with node 21 current and node 20 completed
                    sd.level = 21;
                    sd.mapUnlockShown = 19;
                    int finished = 0;
                    System.Action<int> onFinished = l => finished = l;
                    map3.SequenceFinished += onFinished;
                    map3.Open(20);
                    bool started = map3.SequenceActive;
                    float s0 = Time.unscaledTime;
                    float seenMax = 0f;
                    while (map3.SequenceActive && Time.unscaledTime - s0 < 10f)
                    {
                        seenMax = Mathf.Max(seenMax, map3.SequenceTime);
                        yield return null;
                    }
                    yield return Frames(3);
                    map3.SequenceFinished -= onFinished;
                    var n20 = map3.NodeView(20);
                    var n21 = map3.NodeView(21);
                    bool seqOk = started && finished == 20 && n20 != null && n21 != null &&
                                 n20.State == BusRush.UI.MapNodeState.Completed && n21.State == BusRush.UI.MapNodeState.Current &&
                                 Time.unscaledTime - s0 < 6f;
                    dNote += $"sequence started={started} reached {seenMax:0.00}s finished={finished} node20={n20?.State} node21={n21?.State}; ";
                    dOk &= seqOk;

                    // D1b: tap skips (a second sequence: pretend level 21 completes right away)
                    sd.level = 22;
                    sd.mapUnlockShown = 20;
                    map3.Open(21);
                    yield return Frames(10);
                    bool running = map3.SequenceActive;
                    map3.Skip();
                    yield return Frames(3);
                    bool skipped = running && !map3.SequenceActive && map3.NodeView(22) != null && map3.NodeView(22).State == BusRush.UI.MapNodeState.Current;
                    dOk &= skipped;
                    dNote += $"skip ok={skipped}; ";

                    // D2: map at level 20, tap node 20 -> the card opens (not the level), X closes it, Play starts level 20
                    sd.level = 20;
                    sd.mapUnlockShown = 20;
                    map3.Open();
                    yield return Frames(20);
                    var card = map3.Card;
                    bool tapped = map3.TapLevel(20);
                    yield return Frames(5);
                    bool opened = card.IsOpen && card.Level == 20;
                    bool stillHome = SceneManager.GetActiveScene().name == SceneLoader.HomeScene;
                    float t1 = Time.unscaledTime;
                    while (card.OpenProgress < 1f && Time.unscaledTime - t1 < 3f) yield return null;
                    dNote += $"tap20={tapped} card open={opened} still on Home={stillHome}; ";
                    dOk &= tapped && opened && stillHome;

                    // locked node: toast + no card
                    card.RequestClose();
                    yield return new WaitForSecondsRealtime(0.5f);
                    bool closed = !card.IsOpen;
                    map3.TapLevel(25);
                    yield return Frames(5);
                    bool lockedNoCard = !card.IsOpen;
                    dOk &= closed && lockedNoCard;
                    dNote += $"X closes={closed} locked tap opens no card={lockedNoCard}; ";

                    map3.TapLevel(20);
                    yield return new WaitForSecondsRealtime(0.6f);
                    Field<UnityEngine.UI.Button>(card, "playButton").onClick.Invoke();
                    float t2 = Time.unscaledTime;
                    while (!(SceneManager.GetActiveScene().name == SceneLoader.GameScene && !SceneLoader.IsLoading) && Time.unscaledTime - t2 < 20f) yield return null;
                    yield return Frames(30);
                    bool inGame = SceneManager.GetActiveScene().name == SceneLoader.GameScene && GameSession.Current != null;
                    int playedLevel = GameSession.Current != null ? GameSession.Current.Level : -1;
                    dOk &= inGame && playedLevel == 20;
                    dNote += $"Play -> Game scene={inGame} level={playedLevel}";

                    // no bus anywhere
                    dOk &= AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/BusRush/Art/UI_v2/Map/map_bus_32dir.png") == null;
                }
                Result("D road map v3", dOk && entries.Count == before, dNote);

                done(all);
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
