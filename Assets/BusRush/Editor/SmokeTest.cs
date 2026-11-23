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
            if (mode == "mapplay") UIBuilder.PatchRoadMapV3(); // seam sign canvas group etc., same launch
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

                if (mode == "normal" || mode == "fixes" || mode == "mapplay" || mode == "audio")
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

                if (mode == "audio")
                {
                    yield return AudioChecks(summary, ok => allPass &= ok);
                    Finish(allPass, summary);
                    yield break;
                }

                if (mode == "mapplay")
                {
                    yield return MapPlay(summary, ok => allPass &= ok);
                    Finish(allPass, summary);
                    yield break;
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

            // ------------------------------------------------------------------ map Play after unlock (BUSRUSH_SMOKE_MODE=mapplay)

            /// <summary>Exactly what LevelCompleteScreen.Finish does after a win: save level+1, remember the unlock, go to Home.</summary>
            private static void RealWin(int level)
            {
                SaveData d = SaveSystem.Data;
                for (int l = 1; l <= level; l++) d.SetBestStars(l, 3);
                d.level = Mathf.Max(d.level, level + 1);
                d.mapUnlockShown = level - 1;
                SaveSystem.Commit();
                BusRush.UI.RoadMapScreen.PendingUnlockLevel = level;
                SceneLoader.LoadHome();
            }

            private IEnumerator HomeMap(System.Action<BusRush.UI.RoadMapScreen> result)
            {
                yield return Frames(2);
                yield return WaitFor(() => SceneManager.GetActiveScene().name == SceneLoader.HomeScene && !SceneLoader.IsLoading, 30f, ok => { });
                yield return Frames(5);
                BusRush.UI.RoadMapScreen map = null;
                for (float w = 0f; map == null && w < 10f; w += Time.unscaledDeltaTime)
                {
                    map = FindAnyObjectByType<BusRush.UI.RoadMapScreen>(FindObjectsInactive.Include);
                    yield return null;
                }
                result(map);
            }

            /// <summary>If the gift chest of the level just won opens (levels 5, 15 ...), collects it like a player would.</summary>
            private IEnumerator CollectChestIfShown(BusRush.UI.RoadMapScreen map)
            {
                var chest = Field<BusRush.UI.GiftChestScreen>(map, "giftChest");
                float t0 = Time.unscaledTime;
                while (chest != null && !chest.gameObject.activeSelf && Time.unscaledTime - t0 < 4f) yield return null;
                if (chest == null || !chest.gameObject.activeSelf) yield break;
                float t1 = Time.unscaledTime;
                while (chest.Elapsed < 2.6f && Time.unscaledTime - t1 < 10f) yield return null;
                Field<UnityEngine.UI.Button>(chest, "collectButton").onClick.Invoke();
                yield return new WaitForSecondsRealtime(2.2f);
            }

            private static string ClickBlocker(UnityEngine.UI.Button btn)
            {
                Vector2 sp = RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)btn.transform).position);
                var ped = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = sp };
                var hits = new List<UnityEngine.EventSystems.RaycastResult>();
                UnityEngine.EventSystems.EventSystem.current.RaycastAll(ped, hits);
                if (hits.Count == 0) return $"n/a (no raycast hits at {sp}, screen {Screen.width}x{Screen.height})";
                GameObject top = hits[0].gameObject;
                return top.transform.IsChildOf(btn.transform) ? "ok" : "BLOCKED by " + top.name;
            }

            /// <summary>Taps a node, checks the card is really on screen and clickable, presses Play and waits for the Game scene.</summary>
            private IEnumerator TapAndPlay(BusRush.UI.RoadMapScreen map, int level, string label, System.Action<bool, string> report)
            {
                var card = map.Card;
                bool tapped = map.TapLevel(level);
                yield return Frames(5);
                bool shown = card.IsOpen && card.gameObject.activeInHierarchy && card.Level == level;
                float t0 = Time.unscaledTime;
                while (card.OpenProgress < 1f && Time.unscaledTime - t0 < 3f) yield return null;
                var play = Field<UnityEngine.UI.Button>(card, "playButton");
                bool clickable = play != null && play.IsActive() && play.interactable && card.gameObject.activeInHierarchy;
                string blocker = clickable ? ClickBlocker(play) : "n/a";
                bool notBlocked = !blocker.StartsWith("BLOCKED");
                if (clickable) UnityEngine.EventSystems.ExecuteEvents.Execute(play.gameObject,
                    new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current), UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
                float t1 = Time.unscaledTime;
                while (!(SceneManager.GetActiveScene().name == SceneLoader.GameScene && !SceneLoader.IsLoading) && Time.unscaledTime - t1 < 25f) yield return null;
                yield return Frames(20);
                int played = GameSession.Current != null ? GameSession.Current.Level : -1;
                bool ok = tapped && shown && clickable && notBlocked && played == level;
                report(ok, $"{label}: tap={tapped} card on screen={shown} play clickable={clickable} raycast={blocker} -> Game level {played}");
            }

            private IEnumerator MapPlay(StringBuilder summary, System.Action<bool> done)
            {
                bool all = true;
                void Result(string name, bool ok, string note)
                {
                    all &= ok;
                    Debug.Log($"[SmokeTest] mapplay {name}: {(ok ? "PASS" : "FAIL")} {note}");
                    summary.AppendLine($"  {name}: {(ok ? "PASS" : "FAIL")} {note}");
                }
                BusRush.UI.RoadMapScreen map = null;
                SaveData d = SaveSystem.Data;
                d.worldIntroShown = 1;

                // E1: level 5 completed the way a real win does it; the sequence plays out by itself, then node 6 must work.
                int before = entries.Count;
                d.level = 5;
                d.giftChestsClaimed.Remove(5);
                RealWin(5);
                yield return HomeMap(m => map = m);
                string note = "";
                bool ok = map != null;
                if (ok)
                {
                    float t0 = Time.unscaledTime;
                    while (!map.SequenceActive && Time.unscaledTime - t0 < 8f) yield return null;
                    bool started = map.SequenceActive;
                    float s0 = Time.unscaledTime;
                    while (map.SequenceActive && Time.unscaledTime - s0 < 10f) yield return null;
                    bool ended = !map.SequenceActive;
                    yield return CollectChestIfShown(map);
                    var catcher = Field<UnityEngine.UI.Button>(map, "skipCatcher");
                    bool catcherOff = !catcher.gameObject.activeSelf;
                    note += $"sequence started={started} ended={ended} skip catcher off={catcherOff}; ";
                    bool played = false; string r = "";
                    yield return TapAndPlay(map, 6, "node 6", (o, n) => { played = o; r = n; });
                    ok = started && ended && catcherOff && played;
                    note += r;
                }
                else note = "no map in Home";
                Result("E1 sequence ends by itself", ok && entries.Count == before, note);

                // E2: level 6 completed, the sequence is skipped by a tap.
                before = entries.Count;
                note = "";
                d.level = 6;
                RealWin(6);
                map = null;
                yield return HomeMap(m => map = m);
                ok = map != null;
                if (ok)
                {
                    float t0 = Time.unscaledTime;
                    while (!map.SequenceActive && Time.unscaledTime - t0 < 8f) yield return null;
                    bool started = map.SequenceActive;
                    yield return new WaitForSecondsRealtime(0.4f);
                    var catcher = Field<UnityEngine.UI.Button>(map, "skipCatcher");
                    bool catcherOn = catcher.gameObject.activeSelf;
                    catcher.onClick.Invoke(); // the tap
                    yield return Frames(4);
                    bool stopped = !map.SequenceActive;
                    bool catcherOff = !catcher.gameObject.activeSelf;
                    note += $"started={started} catcher on during={catcherOn} stopped by tap={stopped} catcher off after={catcherOff}; ";
                    bool played = false; string r = "";
                    yield return TapAndPlay(map, 7, "node 7", (o, n) => { played = o; r = n; });
                    ok = started && catcherOn && stopped && catcherOff && played;
                    note += r;
                }
                else note = "no map in Home";
                Result("E2 sequence skipped by a tap", ok && entries.Count == before, note);

                // E3: the map opened a second time (and re-opened while a card is open): the Play node still works.
                before = entries.Count;
                note = "";
                SceneLoader.LoadHome();
                map = null;
                yield return HomeMap(m => map = m);
                ok = map != null;
                if (ok)
                {
                    int cur = SaveSystem.Data.level;
                    map.Open();
                    yield return Frames(15);
                    map.Open(); // second open
                    yield return Frames(15);
                    bool firstTap = map.TapLevel(cur);
                    yield return Frames(5);
                    bool cardUp = map.Card.IsOpen && map.Card.gameObject.activeInHierarchy;
                    map.Open(); // re-opened while the card is up: it must reset, not stay "open" and block taps
                    yield return Frames(10);
                    bool reset = !map.Card.IsOpen && !map.Card.gameObject.activeSelf;
                    yield return Frames(10);
                    bool played = false; string r = "";
                    yield return TapAndPlay(map, cur, $"node {cur}", (o, n) => { played = o; r = n; });
                    ok = firstTap && cardUp && reset && played;
                    note += $"tap={firstTap} card up={cardUp} reset by reopen={reset}; {r}";
                }
                else note = "no map in Home";
                Result("E3 map opened again", ok && entries.Count == before, note);

                // E4: a boss completes (level 10): road to the world sign, the sign drops in, the New World card zooms out of it, then pin 11 unlocks.
                before = entries.Count;
                note = "";
                d.level = 10;
                d.worldsShown = 1;
                RealWin(10);
                map = null;
                yield return HomeMap(m => map = m);
                ok = map != null;
                if (ok)
                {
                    var nw = Field<BusRush.UI.NewWorldScreen>(map, "newWorld");
                    float t0 = Time.unscaledTime;
                    while (!map.SequenceActive && Time.unscaledTime - t0 < 8f) yield return null;
                    bool started = map.SequenceActive;
                    float tc = Time.unscaledTime;
                    while (!nw.gameObject.activeSelf && Time.unscaledTime - tc < 12f) yield return null;
                    bool cardShown = nw.gameObject.activeSelf && nw.World == 2;
                    bool waiting = map.SequenceActive;                       // the sequence waits for the card
                    float te = Time.unscaledTime;
                    while (nw.Elapsed < 2.2f && Time.unscaledTime - te < 8f) yield return null;
                    Field<UnityEngine.UI.Button>(nw, "goButton").onClick.Invoke();
                    yield return Frames(3);
                    float s0 = Time.unscaledTime;
                    while (map.SequenceActive && Time.unscaledTime - s0 < 10f) yield return null;
                    bool ended = !map.SequenceActive;
                    var catcher = Field<UnityEngine.UI.Button>(map, "skipCatcher");
                    bool catcherOff = !catcher.gameObject.activeSelf && !nw.gameObject.activeSelf;
                    bool worldMarked = SaveSystem.Data.worldsShown >= 2;
                    note += $"started={started} New World card from the sign={cardShown} sequence waited={waiting} resumed+ended={ended} catcher off={catcherOff} world marked={worldMarked}; ";
                    bool played = false; string r = "";
                    yield return TapAndPlay(map, 11, "node 11", (o, n) => { played = o; r = n; });
                    ok = started && cardShown && waiting && ended && catcherOff && worldMarked && played;
                    note += r;
                }
                else note = "no map in Home";
                Result("E4 boss complete + New World card", ok && entries.Count == before, note);

                // E4b: the same boss sequence skipped by a tap before the card: the card still shows afterwards, nothing stays blocked.
                before = entries.Count;
                note = "";
                d.level = 20;
                d.worldsShown = 2;
                RealWin(20);
                map = null;
                yield return HomeMap(m => map = m);
                ok = map != null;
                if (ok)
                {
                    var nw = Field<BusRush.UI.NewWorldScreen>(map, "newWorld");
                    float t0 = Time.unscaledTime;
                    while (!map.SequenceActive && Time.unscaledTime - t0 < 8f) yield return null;
                    bool started = map.SequenceActive;
                    yield return new WaitForSecondsRealtime(0.6f);
                    Field<UnityEngine.UI.Button>(map, "skipCatcher").onClick.Invoke();
                    yield return Frames(4);
                    bool stopped = !map.SequenceActive;
                    float tc = Time.unscaledTime;
                    while (!nw.gameObject.activeSelf && Time.unscaledTime - tc < 8f) yield return null;
                    bool cardAfter = nw.gameObject.activeSelf && nw.World == 3;
                    float te = Time.unscaledTime;
                    while (nw.Elapsed < 2.2f && Time.unscaledTime - te < 8f) yield return null;
                    Field<UnityEngine.UI.Button>(nw, "goButton").onClick.Invoke();
                    yield return new WaitForSecondsRealtime(0.6f);
                    bool clean = !nw.gameObject.activeSelf && !Field<UnityEngine.UI.Button>(map, "skipCatcher").gameObject.activeSelf;
                    note += $"started={started} skipped={stopped} card after skip={cardAfter} clean={clean}; ";
                    bool played = false; string r = "";
                    yield return TapAndPlay(map, 21, "node 21", (o, n) => { played = o; r = n; });
                    ok = started && stopped && cardAfter && clean && played;
                    note += r;
                }
                else note = "no map in Home";
                Result("E4b boss sequence skipped", ok && entries.Count == before, note);

                // E5: all three worlds render in English, Spanish and Hindi; the number and PLAY fonts really draw their characters.
                before = entries.Count;
                note = "";
                SceneLoader.LoadHome();
                map = null;
                yield return HomeMap(m => map = m);
                ok = map != null;
                int worlds = 0, glyphProblems = 0;
                if (ok)
                {
                    foreach (Language lang in new[] { Language.English, Language.Spanish, Language.Hindi })
                    {
                        Localizer.Set(lang);
                        foreach (int lvl in new[] { 5, 15, 25 })       // City, Snow, Beach
                        {
                            d.level = lvl;
                            d.mapUnlockShown = lvl;
                            map.Open();
                            yield return Frames(15);
                            var node = map.NodeView(lvl);
                            if (node == null) { glyphProblems++; continue; }
                            if (lang == Language.English) worlds++;
                            foreach (string field in new[] { "number", "playText" })
                            {
                                var txt = Field<TMP_Text>(node, field);
                                if (txt == null || !txt.gameObject.activeInHierarchy) { glyphProblems++; continue; }
                                txt.ForceMeshUpdate();
                                int visible = 0;
                                for (int i = 0; i < txt.textInfo.characterCount; i++)
                                {
                                    var ci = txt.textInfo.characterInfo[i];
                                    if (char.IsWhiteSpace(ci.character)) continue;
                                    if (!ci.isVisible || ci.elementType != TMP_TextElementType.Character || !ci.fontAsset.HasCharacter(ci.character, false, false)) glyphProblems++;
                                    else visible++;
                                }
                                if (visible == 0) glyphProblems++;
                            }
                        }
                    }
                    Localizer.Set(Language.English);
                    note += $"worlds rendered={worlds}/3 glyph problems (digits / PLAY, en+es+hi)={glyphProblems}";
                    ok = worlds == 3 && glyphProblems == 0;
                }
                else note = "no map in Home";
                PlayerPrefs.DeleteKey(Localizer.PrefsKey);
                Localizer.ResetForTests();
                Result("E5 three worlds + digit fonts", ok && entries.Count == before, note);

                done(all);
            }


            // ------------------------------------------------------------------ audio (BUSRUSH_SMOKE_MODE=audio)

            private IEnumerator AudioChecks(StringBuilder summary, System.Action<bool> done)
            {
                bool all = true;
                void Result(string name, bool ok, string note)
                {
                    all &= ok;
                    Debug.Log($"[SmokeTest] audio {name}: {(ok ? "PASS" : "FAIL")} {note}");
                    summary.AppendLine($"  {name}: {(ok ? "PASS" : "FAIL")} {note}");
                }
                SaveData d = SaveSystem.Data;
                d.soundOn = true;
                d.musicOn = true;
                SaveSystem.Commit();
                AudioManager am = AudioManager.Instance;
                if (am == null || am.Library == null) { Result("manager", false, "no AudioManager / library"); done(false); yield break; }

                // 1. every SfxId (and both music tracks) has a clip
                var missing = am.Library.Missing();
                int ids = System.Enum.GetValues(typeof(SfxId)).Length;
                Result("every SfxId has a clip", missing.Count == 0, $"{ids} ids, missing: [{string.Join(", ", missing)}]");

                // 2. plays when Sound is on; per-sound throttle; the string hook and rising chains reach the manager
                int c0 = am.PlayCount;
                AudioManager.Play(SfxId.UiClick);
                AudioManager.Play(SfxId.UiClick);                       // same frame: throttled
                int afterThrottle = am.PlayCount - c0;
                yield return new WaitForSecondsRealtime(0.06f);
                Sfx.Play("click");
                yield return new WaitForSecondsRealtime(0.06f);
                AudioManager.PlayRising(SfxId.PassengerBoard, 3);
                yield return new WaitForSecondsRealtime(0.06f);
                Sfx.Play("coins");
                int played = am.PlayCount - c0;
                bool deviceWorks = am.ActiveSfxSources > 0;
                Result("plays with Sound on", afterThrottle == 1 && played == 4, $"throttled duplicate -> {afterThrottle} start, chain of 4 calls -> {played} starts (audio device {(deviceWorks ? "present" : "headless")})");

                // 3. the Sound toggle silences everything, live, and brings it back
                d.soundOn = false;
                SaveSystem.Commit();
                int c1 = am.PlayCount;
                yield return new WaitForSecondsRealtime(0.06f);
                AudioManager.Play(SfxId.Coin);
                Sfx.Play("click");
                AudioManager.PlayRising(SfxId.Star, 2);
                AudioManager.StartLoop(SfxId.RoadShimmer);
                bool silent = am.PlayCount == c1 && am.ActiveSfxSources == 0;
                d.soundOn = true;
                SaveSystem.Commit();
                yield return new WaitForSecondsRealtime(0.06f);
                AudioManager.Play(SfxId.Coin);
                bool back = am.PlayCount == c1 + 1;
                Result("Sound toggle", silent && back, $"off -> silent={silent} (no starts, no active sources), on again -> plays={back}");

                // 4. the Music toggle
                d.musicOn = false;
                SaveSystem.Commit();
                yield return new WaitForSecondsRealtime(1.0f);
                bool musicOff = !am.MusicAudible;
                d.musicOn = true;
                SaveSystem.Commit();
                yield return new WaitForSecondsRealtime(1.2f);
                bool musicOn = !deviceWorks || am.MusicAudible;           // without an audio device (headless) only "off" can be proven
                Result("Music toggle", musicOff && musicOn, $"off -> not audible={musicOff}, on -> audible={(deviceWorks ? am.MusicAudible.ToString() : "n/a headless")}; current track {am.CurrentMusic}");

                // 5. the music follows the scene (menu <-> game) and cross-fades without errors
                bool menu = am.CurrentMusic == MusicId.Menu;
                SceneLoader.LoadGame();
                yield return Frames(2);
                yield return WaitFor(() => SceneManager.GetActiveScene().name == SceneLoader.GameScene && !SceneLoader.IsLoading, 30f, ok => { });
                yield return new WaitForSecondsRealtime(0.8f);
                bool game = am.CurrentMusic == MusicId.Game;
                SceneLoader.LoadHome();
                yield return Frames(2);
                yield return WaitFor(() => SceneManager.GetActiveScene().name == SceneLoader.HomeScene && !SceneLoader.IsLoading, 30f, ok => { });
                yield return new WaitForSecondsRealtime(0.8f);
                bool menuAgain = am.CurrentMusic == MusicId.Menu;
                Result("music per scene", menu && game && menuAgain, $"Home={menu}, Game={game}, Home again={menuAgain}");

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
