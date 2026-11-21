using System;
using BusRush.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static BusRush.EditorTools.UIKit;
using Object = UnityEngine.Object;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Step 2: builds every UI prefab (screens + popups) and wires them into Boot / Home / Game.
    /// Idempotent. Run after Step 1 setup. Menu: Tools/Bus Rush/Build UI (Step 2).
    /// </summary>
    public static partial class UIBuilder
    {
        private const string PrefabDir = "Assets/BusRush/Prefabs/UI";
        private const string ScenesDir = "Assets/BusRush/Scenes";
        private const string CatalogPath = PrefabDir + "/PopupCatalog.asset";

        [MenuItem("Tools/Bus Rush/Build UI (Step 2)")]
        public static void BuildAll()
        {
            UIArtGenerator.GenerateAll();
            Load();

            Popup[] popups =
            {
                SavePopup(BuildSettings()),
                SavePopup(BuildDaily()),
                SavePopup(BuildBoosters()),
                SavePopup(BuildLevelComplete()),
                SavePopup(BuildOutOfSpace())
            };

            PopupCatalog catalog = AssetDatabase.LoadAssetAtPath<PopupCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<PopupCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            Wire(catalog, ("popups", popups));
            EditorUtility.SetDirty(catalog);

            GameObject uiManager = Save(BuildUIManager(catalog), "UIManager");
            GameObject home = Save(BuildHome(), "HomeScreen");
            GameObject hud = Save(BuildHud(), "GameHUD");
            GameObject splash = Save(BuildSplash(), "Splash");

            AssetDatabase.SaveAssets();
            WireScenes(splash, home, hud, uiManager);
            Debug.Log("[UIBuilder] UI built.");
        }

        public static void BuildAllBatch()
        {
            try
            {
                BuildAll();
            }
            catch (Exception e)
            {
                Debug.LogError("[UIBuilder] FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }

        private static Popup SavePopup(GameObject root) => Save(root, root.name).GetComponent<Popup>();

        private static GameObject Save(GameObject root, string name)
        {
            string path = $"{PrefabDir}/{name}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ------------------------------------------------------------------ UIManager

        private static GameObject BuildUIManager(PopupCatalog catalog)
        {
            var go = new GameObject("UIManager");
            RootCanvas(go, 100);
            UIManager manager = go.AddComponent<UIManager>();
            Wire(manager, ("catalog", catalog), ("popupRoot", go.transform));
            return go;
        }

        // ------------------------------------------------------------------ Home

        private static GameObject BuildHome()
        {
            var go = new GameObject("HomeScreen");
            RootCanvas(go, 0);
            Transform root = go.transform;

            Image sky = Img(root, "Sky", Sky, Color.white);
            Stretch(sky.rectTransform);
            AddCloud(root, 0.08f, 0.86f, 150f, 0.85f);
            AddCloud(root, 0.86f, 0.74f, 120f, 0.7f);
            AddCloud(root, 0.18f, 0.5f, 110f, 0.55f);
            AddCloud(root, 0.9f, 0.36f, 140f, 0.6f);

            RectTransform safe = SafeArea(root);

            // --- top bar
            RectTransform top = Rect(safe, "TopBar");
            Place(top, 0.5f, 1f, 0f, -10f, 0f, 56f, 0.5f, 1f);
            top.anchorMin = new Vector2(0f, 1f);
            top.anchorMax = new Vector2(1f, 1f);
            top.offsetMin = new Vector2(14f, top.offsetMin.y);
            top.offsetMax = new Vector2(-14f, top.offsetMax.y);

            // Lives pill
            Chunk lives = Chunky(top, "LivesPill", Navy, NavyLip, 100f, 44f, -1f, 5f, false, true, false);
            Place(lives.Root, 0f, 0.5f, 24f, 0f, 100f, 44f, 0f, 0.5f);
            Image heart = IconImg(lives.Root, "Heart", "heart", 54f);
            Place(heart.rectTransform, 0f, 0.5f, -26f, 2f, 54f, 54f, 0f, 0.5f);
            TextMeshProUGUI livesCount = Label(heart.transform, "Count", "5", 22f, TextStyle.Heading);
            Stretch(livesCount.rectTransform, 0f, 4f, 0f, 0f);
            TextMeshProUGUI livesTimer = Label(lives.Face, "Timer", "FULL", 17f, TextStyle.Body);
            Stretch(livesTimer.rectTransform, 30f, 0f, 8f, 0f);
            LivesText livesText = livesCount.gameObject.AddComponent<LivesText>();
            Wire(livesText, ("timerText", livesTimer));

            // Coins pill (coins come from rewards only)
            Chunk coins = Chunky(top, "CoinsPill", Navy, NavyLip, 132f, 44f, -1f, 5f, false, true, false);
            // Anchored to the gear so it never collides with the lives pill on narrow screens.
            Place(coins.Root, 1f, 0.5f, -74f, 0f, 132f, 44f, 1f, 0.5f);
            Image coinIcon = IconImg(coins.Root, "Coin", "coin", 48f);
            Place(coinIcon.rectTransform, 0f, 0.5f, -24f, 2f, 48f, 48f, 0f, 0.5f);
            TextMeshProUGUI coinCount = Label(coins.Face, "Count", "100", 22f, TextStyle.Heading);
            Stretch(coinCount.rectTransform, 26f, 0f, 12f, 0f);
            Wire(coinCount.gameObject.AddComponent<CoinText>(), ("punchTarget", coinIcon.transform));

            // Settings gear
            Chunk gear = IconButton(top, "SettingsButton", "gear", Blue, BlueLip, 50f, 54f, 34f, 16f);
            Place(gear.Root, 1f, 0.5f, 0f, 0f, 50f, 54f, 1f, 0.5f);

            // --- side buttons
            Chunk SideButton(string name, string icon, string label, float ax, float y)
            {
                Chunk b = Chunky(safe, name, Color.white, PanelLip, 66f, 70f, 20f, 6f, true);
                Place(b.Root, ax, 1f, ax < 0.5f ? 14f : -14f, -y, 66f, 70f, ax, 1f);
                Image i = IconImg(b.Face, "Icon", icon, 46f);
                Place(i.rectTransform, 0.5f, 0.5f, 0f, 2f, 46f, 46f);
                TextMeshProUGUI t = Label(b.Root, "Label", label, 17f, TextStyle.Heading);
                Place(t.rectTransform, 0.5f, 0f, 0f, -16f, 90f, 22f, 0.5f, 0.5f);
                return b;
            }

            Chunk daily = SideButton("DailyButton", "gift", "DAILY", 0f, 84f);
            Chunk boosts = SideButton("BoostsButton", "crown", "BOOSTS", 1f, 84f);

            Image badge = Badge(daily.Root, "Badge", Red, 26f, out RectTransform badgeContent);
            Place(badge.rectTransform, 1f, 1f, 6f, 6f, 26f, 26f, 0.5f, 0.5f);
            TextMeshProUGUI excl = Label(badgeContent, "Text", "!", 18f, TextStyle.Heading);
            Stretch(excl.rectTransform, 0f, 1f, 0f, 0f);

            // --- level path (between the top area and the bottom group)
            RectTransform path = Rect(safe, "LevelPath");
            Stretch(path, 90f, 252f, 90f, 86f);
            Image pathGlow = Img(path, "Glow", Glow, new Color(1f, 1f, 1f, 0.35f));
            Stretch(pathGlow.rectTransform, -60f, -20f, -60f, -20f);
            BuildPathTemplates(path, out RectTransform nodeTemplate, out RectTransform dotTemplate);
            LevelPathView pathView = path.gameObject.AddComponent<LevelPathView>();
            Wire(pathView, ("nodeTemplate", nodeTemplate), ("dotTemplate", dotTemplate));

            // --- bottom group: banner slot, PLAY, next unlock
            RectTransform banner = Rect(safe, "BannerSlot");
            Bottom(banner, 0f, 320f, 50f);

            Chunk play = Chunky(safe, "PlayButton", Green, GreenLipDark, 250f, 92f, 26f, 9f, true);
            Bottom(play.Root, 64f, 250f, 92f);
            TextMeshProUGUI playLabel = Label(play.Face, "Label", "PLAY", 46f, TextStyle.Heading);
            Stretch(playLabel.rectTransform, 10f, 22f, 10f, 2f);
            TextMeshProUGUI playLevel = Label(play.Face, "Level", "Level 1", 18f, TextStyle.Body);
            Stretch(playLevel.rectTransform, 10f, 6f, 10f, 52f);

            Chunk unlock = Chunky(safe, "NextUnlock", Color.white, PanelLip, 300f, 64f, 18f, 6f, false, true, false);
            Bottom(unlock.Root, 172f, 300f, 64f);
            TextMeshProUGUI unlockText = Label(unlock.Face, "Title", "Next: Shuffle booster", 16f, TextStyle.BodyPlain, DarkNavy, TextAlignmentOptions.Left);
            Stretch(unlockText.rectTransform, 16f, 28f, 16f, 6f);
            Image track = Img(unlock.Face, "Track", Circle, PanelSoft);
            MakePill(track, 18f);
            Place(track.rectTransform, 0f, 0f, 16f, 10f, 214f, 18f, 0f, 0f);
            Image fill = Img(track.transform, "Fill", Circle, Gold);
            MakePill(fill, 14f);
            RectTransform fillRt = fill.rectTransform;
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(0.4f, 1f);
            fillRt.offsetMin = new Vector2(2f, 2f);
            fillRt.offsetMax = new Vector2(-2f, -2f);
            TextMeshProUGUI unlockProgress = Label(unlock.Face, "Progress", "1/13", 16f, TextStyle.BodyPlain, TextGrey, TextAlignmentOptions.Right);
            Stretch(unlockProgress.rectTransform, 230f, 4f, 16f, 30f);

            HomeScreen screen = go.AddComponent<HomeScreen>();
            Wire(screen, ("playButton", play.Button), ("playPulseTarget", play.Root), ("playLevelText", playLevel),
                ("settingsButton", gear.Button),
                ("dailyButton", daily.Button), ("dailyBadge", badge.gameObject),
                ("boostsButton", boosts.Button), ("unlockFill", fillRt), ("unlockText", unlockText),
                ("unlockProgressText", unlockProgress));
            return go;
        }

        private static void AddCloud(Transform root, float ax, float ay, float width, float alpha)
        {
            Image c = Img(root, "Cloud", Cloud, new Color(1f, 1f, 1f, alpha));
            c.preserveAspect = true;
            Place(c.rectTransform, ax, ay, 0f, 0f, width, width * 0.5f);
        }

        private static void BuildPathTemplates(RectTransform path, out RectTransform node, out RectTransform dot)
        {
            Image d = Img(path, "DotTemplate", Circle, new Color(1f, 1f, 1f, 0.9f));
            d.type = Image.Type.Simple;
            d.preserveAspect = true;
            Place(d.rectTransform, 0.5f, 0.5f, 0f, 0f, 12f, 12f);
            d.gameObject.SetActive(false);
            dot = d.rectTransform;

            node = Rect(path, "NodeTemplate");
            Place(node, 0.5f, 0.5f, 0f, 0f, 60f, 66f);
            Image ring = Img(node, "Ring", Glow, new Color(1f, 0.86f, 0.3f, 1f));
            Stretch(ring.rectTransform, -30f, -30f, -30f, -30f);
            Image lip = Img(node, "Lip", Circle, Grey);
            lip.preserveAspect = true;
            Place(lip.rectTransform, 0.5f, 0f, 0f, 0f, 60f, 60f, 0.5f, 0f);
            Image face = Img(node, "Face", Circle, PanelSoft);
            face.preserveAspect = true;
            Place(face.rectTransform, 0.5f, 1f, 0f, 0f, 60f, 60f, 0.5f, 1f);
            Image shine = Img(face.transform, "Shine", Circle, new Color(1f, 1f, 1f, 0.25f));
            shine.preserveAspect = true;
            Place(shine.rectTransform, 0.5f, 1f, 0f, -5f, 38f, 22f, 0.5f, 1f);
            shine.type = Image.Type.Sliced;
            MakePill(shine, 22f);
            Image nodeLock = IconImg(node, "Lock", "lock", 22f);
            Place(nodeLock.rectTransform, 1f, 0f, 2f, 2f, 22f, 22f, 1f, 0f);
            RectTransform starRow = Rect(node, "Stars");
            Place(starRow, 0.5f, 0f, 0f, -16f, 60f, 18f, 0.5f, 0.5f);
            for (int k = 0; k < 3; k++)
            {
                Image st = IconImg(starRow, "Star" + (k + 1), "star", 18f);
                Place(st.rectTransform, 0.5f, 0.5f, (k - 1) * 18f, k == 1 ? 3f : 0f, 18f, 18f);
            }
            TextMeshProUGUI number = Label(node, "Number", "1", 28f, TextStyle.Heading);
            Place(number.rectTransform, 0.5f, 1f, 0f, -2f, 60f, 58f, 0.5f, 1f);
            node.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ Game HUD

        private static GameObject BuildHud()
        {
            var go = new GameObject("GameHUD");
            RootCanvas(go, 10);

            // Wrong-tap flash covers the whole screen (outside the safe area too), behind the HUD controls.
            Image flash = Img(go.transform, "WrongTapFlash", Vignette, new Color(1f, 0.1f, 0.1f, 0f));
            Stretch(flash.rectTransform);
            flash.gameObject.AddComponent<ScreenFlash>();

            RectTransform safe = SafeArea(go.transform);

            Chunk restart = IconButton(safe, "RestartButton", "restart", Blue, BlueLip, 54f, 58f, 34f, 16f);
            Place(restart.Root, 0f, 1f, 14f, -10f, 54f, 58f, 0f, 1f);
            Chunk pause = IconButton(safe, "PauseButton", "pause", Blue, BlueLip, 54f, 58f, 32f, 16f);
            Place(pause.Root, 1f, 1f, -14f, -10f, 54f, 58f, 1f, 1f);
            // Level badge: "Level N" with a small "N passengers left" second line (kept off the crowd).
            Chunk levelPill = Chunky(safe, "LevelPill", Navy, NavyLip, 170f, 60f, 26f, 6f, false, true, false);
            Place(levelPill.Root, 0.5f, 1f, 0f, -12f, 170f, 60f, 0.5f, 1f);
            TextMeshProUGUI level = Label(levelPill.Face, "Label", "Level 1", 26f, TextStyle.Heading);
            Stretch(level.rectTransform, 10f, 18f, 10f, 2f);
            TextMeshProUGUI passengers = Label(levelPill.Face, "PassengersLeft", "0 passengers left", 13f, TextStyle.Body);
            Stretch(passengers.rectTransform, 10f, 3f, 10f, 34f);

            // Booster row
            RectTransform row = Rect(safe, "BoosterRow");
            Bottom(row, 18f, 320f, 96f);
            var types = new[] { BoosterType.Vip, BoosterType.Arrange, BoosterType.Shuffle };
            string[] icons = { "crown", "arrange", "shuffle" };
            string[] names = { "VIP", "ARRANGE", "SHUFFLE" };
            for (int i = 0; i < 3; i++) BoosterButton(row, types[i], icons[i], names[i], (i - 1) * 104f);

            BuildDebugPanel(safe);
            LevelBanner banner = BuildLevelBanner(safe);
            BuildHints(go.transform);

            // Toast pill ("No free bay!") above the lot.
            Chunk toast = Chunky(safe, "Toast", DarkNavy, NavyLip, 230f, 46f, -1f, 4f, false, true, false);
            Place(toast.Root, 0.5f, 1f, 0f, -230f, 230f, 46f, 0.5f, 1f);
            TextMeshProUGUI toastLabel = Label(toast.Face, "Label", "No free bay!", 22f, TextStyle.Body);
            Stretch(toastLabel.rectTransform, 12f, 0f, 12f, 0f);
            toast.Root.gameObject.AddComponent<CanvasGroup>();
            Wire(toast.Root.gameObject.AddComponent<Toast>(), ("label", toastLabel));

            GameHUD hud = go.AddComponent<GameHUD>();
            Wire(hud, ("restartButton", restart.Button), ("pauseButton", pause.Button), ("levelText", level),
                ("passengersText", passengers), ("banner", banner));
            return go;
        }

        private static LevelBanner BuildLevelBanner(RectTransform safe)
        {
            RectTransform holder = Rect(safe, "LevelBanner");
            Place(holder, 0.5f, 0.5f, 0f, 120f, 300f, 90f);
            Chunk pill = Chunky(holder, "Banner", Navy, NavyLip, 280f, 78f, 26f, 7f, false, true, true);
            Place(pill.Root, 0.5f, 0.5f, 0f, 0f, 280f, 78f);
            TextMeshProUGUI title = Label(pill.Face, "Title", "Level 1", 40f, TextStyle.Heading);
            Stretch(title.rectTransform, 10f, 4f, 10f, 4f);
            Chunk tag = Chunky(pill.Root, "BossTag", Red, RedLip, 150f, 32f, -1f, 4f, false, false, false);
            Place(tag.Root, 0.5f, 0f, 0f, -14f, 150f, 32f, 0.5f, 0.5f);
            TextMeshProUGUI subtitle = Label(tag.Face, "Label", "BOSS LEVEL", 18f, TextStyle.Heading);
            Stretch(subtitle.rectTransform, 6f, 0f, 6f, 0f);

            LevelBanner banner = holder.gameObject.AddComponent<LevelBanner>();
            Wire(banner, ("banner", pill.Root), ("title", title), ("subtitle", subtitle));
            return banner;
        }

        /// <summary>Tutorial / idle hint layer: pointing hand + speech bubble (white, navy outline, Lilita One).</summary>
        private static void BuildHints(Transform canvasRoot)
        {
            RectTransform area = Stretch(Rect(canvasRoot, "Hints"));

            RectTransform hand = Rect(area, "Hand");
            Place(hand, 0.5f, 0.5f, 0f, 0f, 0f, 0f);
            Image handImg = IconImg(hand, "Visual", "hand", 72f);
            // Fingertip of the icon (~0.34, 0.9 of the image) sits on the hand's anchor point.
            handImg.rectTransform.pivot = new Vector2(0.34f, 0.9f);
            handImg.rectTransform.anchoredPosition = Vector2.zero;

            RectTransform bubble = Rect(area, "Bubble");
            Place(bubble, 0.5f, 0.5f, 0f, 0f, 240f, 62f);
            Image tail = Img(bubble, "Tail", Round, Navy, 3f);
            Place(tail.rectTransform, 0.5f, 0.5f, 0f, -29f, 20f, 20f);
            Image tailFill = Img(tail.transform, "Fill", Round, Color.white, 2f);
            Stretch(tailFill.rectTransform, 3f, 3f, 3f, 3f);
            Image outline = Img(bubble, "Outline", Round, Navy, 20f);
            Stretch(outline.rectTransform);
            Image fill = Img(outline.transform, "Fill", Round, Color.white, 17f);
            Stretch(fill.rectTransform, 3f, 3f, 3f, 3f);
            TextMeshProUGUI text = Label(fill.transform, "Text", "Tap a car!", 21f, TextStyle.HeadingPlain, Navy);
            text.textWrappingMode = TextWrappingModes.Normal;
            Stretch(text.rectTransform, 10f, 4f, 10f, 4f);
            tail.transform.SetAsFirstSibling();

            HintView view = area.gameObject.AddComponent<HintView>();
            Wire(view, ("area", area), ("hand", hand), ("bubble", bubble), ("bubbleText", text), ("bubbleTail", tail.rectTransform));
        }

        private static void BoosterButton(RectTransform row, BoosterType type, string icon, string label, float x)
        {
            Chunk b = Chunky(row, label + "Booster", Color.white, PanelLip, 84f, 92f, 22f, 7f, true);
            Place(b.Root, 0.5f, 0f, x, 0f, 84f, 92f, 0.5f, 0f);
            Image ic = IconImg(b.Face, "Icon", icon, 52f);
            Place(ic.rectTransform, 0.5f, 0.5f, 0f, 9f, 52f, 52f);
            Image icon3D = Img(b.Face, "Icon3D", null, new Color(1f, 1f, 1f, 0f));
            Place(icon3D.rectTransform, 0.5f, 0.5f, 0f, 9f, 64f, 64f);
            TextMeshProUGUI name = Label(b.Face, "Name", label, 13f, TextStyle.BodyPlain, TextGrey);
            Place(name.rectTransform, 0.5f, 0f, 0f, 5f, 80f, 18f, 0.5f, 0f);

            Image count = Badge(b.Root, "CountBadge", Red, 32f, out RectTransform countContent);
            Place(count.rectTransform, 1f, 1f, 6f, 6f, 32f, 32f, 0.5f, 0.5f);
            TextMeshProUGUI countText = Label(countContent, "Count", "2", 19f, TextStyle.Heading);
            Stretch(countText.rectTransform, 0f, 1f, 0f, 0f);

            Image plus = Badge(b.Root, "PlusBadge", Green, 32f, out RectTransform plusContent);
            Place(plus.rectTransform, 1f, 1f, 6f, 6f, 32f, 32f, 0.5f, 0.5f);
            Image plusIcon = IconImg(plusContent, "Plus", "plus", 20f);
            Place(plusIcon.rectTransform, 0.5f, 0.5f, 0f, 0f, 20f, 20f);

            Image lockOverlay = Img(b.Body, "LockOverlay", Round, new Color(0.12f, 0.16f, 0.27f, 0.88f), 22f);
            Stretch(lockOverlay.rectTransform);
            Image lockIcon = IconImg(lockOverlay.transform, "Lock", "lock", 40f);
            Place(lockIcon.rectTransform, 0.5f, 0.5f, 0f, 10f, 40f, 40f);
            TextMeshProUGUI lockText = Label(lockOverlay.transform, "Text", "LV 13", 17f, TextStyle.Heading);
            Place(lockText.rectTransform, 0.5f, 0f, 0f, 9f, 80f, 22f, 0.5f, 0f); // same slot as the name label

            BoosterButtonView view = b.Root.gameObject.AddComponent<BoosterButtonView>();
            Wire(view, ("type", type), ("button", b.Button), ("countBadge", count.gameObject), ("countText", countText),
                ("plusBadge", plus.gameObject), ("lockOverlay", lockOverlay.gameObject), ("lockText", lockText),
                ("nameLabel", name.gameObject));
        }

        private static void BuildDebugPanel(RectTransform safe)
        {
            Image panel = Img(safe, "DebugPanel (TEMP)", Round, new Color(0.12f, 0.16f, 0.27f, 0.75f), 12f);
            panel.raycastTarget = true;
            Place(panel.rectTransform, 0f, 1f, 14f, -78f, 104f, 202f, 0f, 1f); // ends above the bays
            TextMeshProUGUI title = Label(panel.transform, "Title", "DEBUG", 12f, TextStyle.BodyPlain, new Color(1f, 1f, 1f, 0.8f));
            Top(title.rectTransform, 4f, 96f, 16f);

            Button Small(string name, string text, float y)
            {
                Chunk b = Chunky(panel.transform, name, Grey, GreyLip, 92f, 26f, 8f, 3f, true, false, false);
                Top(b.Root, y, 92f, 26f);
                TextMeshProUGUI t = Label(b.Face, "Label", text, 13f, TextStyle.BodyPlain, DarkNavy);
                Stretch(t.rectTransform, 4f, 0f, 4f, 0f);
                return b.Button;
            }

            Button settings = Small("SettingsButton", "Settings", 20f);
            Button win = Small("LevelCompleteButton", "Level Complete", 50f);
            Button space = Small("OutOfSpaceButton", "Out of Space", 80f);
            Button next = Small("NextLevelButton", "Next level", 110f);
            Button clear = Small("ClearBoardButton", "Clear board", 140f);
            Button autoDepart = Small("AutoDepartButton", "Auto depart: ON", 170f);

            panel.gameObject.AddComponent<DebugOnly>();
            DebugPopupPanel debug = panel.gameObject.AddComponent<DebugPopupPanel>();
            Wire(debug, ("settingsButton", settings), ("levelCompleteButton", win), ("outOfSpaceButton", space),
                ("nextLevelButton", next), ("clearBoardButton", clear), ("autoDepartButton", autoDepart),
                ("autoDepartLabel", autoDepart.transform.Find("Body/Face/Label").GetComponent<TextMeshProUGUI>()));
        }

        // ------------------------------------------------------------------ scenes

        private static void WireScenes(GameObject splash, GameObject home, GameObject hud, GameObject uiManager)
        {
            WireBoot(splash);

            // Home
            Scene homeScene = EditorSceneManager.OpenScene($"{ScenesDir}/Home.unity", OpenSceneMode.Single);
            RemoveRoots(homeScene, "Canvas", "HomeScreen", "UIManager");
            PrefabUtility.InstantiatePrefab(home, homeScene);
            PrefabUtility.InstantiatePrefab(uiManager, homeScene);
            FindRoot(homeScene, "Main Camera").GetComponent<Camera>().backgroundColor = UIArtGenerator.Hex(0x8FD3F4);
            EditorSceneManager.SaveScene(homeScene);

            // Game
            Scene game = EditorSceneManager.OpenScene($"{ScenesDir}/Game.unity", OpenSceneMode.Single);
            RemoveRoots(game, "Canvas", "GameHUD", "UIManager");
            PrefabUtility.InstantiatePrefab(hud, game);
            PrefabUtility.InstantiatePrefab(uiManager, game);
            EditorSceneManager.SaveScene(game);

            EditorSceneManager.OpenScene($"{ScenesDir}/Boot.unity", OpenSceneMode.Single);
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.name == name) return go;
            }
            throw new InvalidOperationException($"'{name}' not found in scene {scene.name}");
        }

        private static void RemoveRoots(Scene scene, params string[] names)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (Array.IndexOf(names, go.name) >= 0) Object.DestroyImmediate(go);
            }
        }
    }
}
