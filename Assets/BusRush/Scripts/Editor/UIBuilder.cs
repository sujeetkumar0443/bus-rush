using System;
using BusRush.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static BusRush.EditorTools.UIKit;
using Object = UnityEngine.Object;
using SplashScreen = BusRush.UI.SplashScreen;

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
                SavePopup(BuildShop()),
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

            // Coins pill (+ opens shop)
            Chunk coins = Chunky(top, "CoinsPill", Navy, NavyLip, 132f, 44f, -1f, 5f, false, true, false);
            // Anchored to the gear so it never collides with the lives pill on narrow screens.
            Place(coins.Root, 1f, 0.5f, -74f, 0f, 132f, 44f, 1f, 0.5f);
            Image coinIcon = IconImg(coins.Root, "Coin", "coin", 48f);
            Place(coinIcon.rectTransform, 0f, 0.5f, -24f, 2f, 48f, 48f, 0f, 0.5f);
            TextMeshProUGUI coinCount = Label(coins.Face, "Count", "100", 22f, TextStyle.Heading);
            Stretch(coinCount.rectTransform, 26f, 0f, 36f, 0f);
            Wire(coinCount.gameObject.AddComponent<CoinText>(), ("punchTarget", coinIcon.transform));
            Chunk plus = IconButton(coins.Root, "AddCoinsButton", "plus", Green, GreenLip, 34f, 36f, 20f, 10f);
            Place(plus.Root, 1f, 0.5f, 10f, 2f, 34f, 36f, 1f, 0.5f);

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

            Chunk shop = SideButton("ShopButton", "cart", "SHOP", 0f, 84f);
            Chunk daily = SideButton("DailyButton", "gift", "DAILY", 0f, 200f);
            Chunk noAds = SideButton("NoAdsButton", "noads", "NO ADS", 1f, 84f);
            Chunk boosts = SideButton("BoostsButton", "crown", "BOOSTS", 1f, 200f);

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
                ("addCoinsButton", plus.Button), ("settingsButton", gear.Button), ("shopButton", shop.Button),
                ("dailyButton", daily.Button), ("dailyBadge", badge.gameObject), ("noAdsButton", noAds.Button),
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
            Chunk levelPill = Chunky(safe, "LevelPill", Navy, NavyLip, 170f, 50f, -1f, 6f, false, true, false);
            Place(levelPill.Root, 0.5f, 1f, 0f, -14f, 170f, 50f, 0.5f, 1f);
            TextMeshProUGUI level = Label(levelPill.Face, "Label", "Level 1", 28f, TextStyle.Heading);
            Stretch(level.rectTransform, 10f, 0f, 10f, 0f);

            // Booster row
            RectTransform row = Rect(safe, "BoosterRow");
            Bottom(row, 18f, 320f, 96f);
            var types = new[] { BoosterType.Vip, BoosterType.Arrange, BoosterType.Shuffle };
            string[] icons = { "crown", "arrange", "shuffle" };
            string[] names = { "VIP", "ARRANGE", "SHUFFLE" };
            for (int i = 0; i < 3; i++) BoosterButton(row, types[i], icons[i], names[i], (i - 1) * 104f);

            BuildDebugPanel(safe);

            GameHUD hud = go.AddComponent<GameHUD>();
            Wire(hud, ("restartButton", restart.Button), ("pauseButton", pause.Button), ("levelText", level));
            return go;
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
            Place(panel.rectTransform, 0f, 1f, 14f, -82f, 104f, 196f, 0f, 1f);
            TextMeshProUGUI title = Label(panel.transform, "Title", "DEBUG", 12f, TextStyle.BodyPlain, new Color(1f, 1f, 1f, 0.8f));
            Top(title.rectTransform, 4f, 96f, 16f);

            Button Small(string name, string text, float y)
            {
                Chunk b = Chunky(panel.transform, name, Grey, GreyLip, 92f, 30f, 8f, 3f, true, false, false);
                Top(b.Root, y, 92f, 30f);
                TextMeshProUGUI t = Label(b.Face, "Label", text, 13f, TextStyle.BodyPlain, DarkNavy);
                Stretch(t.rectTransform, 4f, 0f, 4f, 0f);
                return b.Button;
            }

            Button settings = Small("SettingsButton", "Settings", 22f);
            Button win = Small("LevelCompleteButton", "Level Complete", 56f);
            Button space = Small("OutOfSpaceButton", "Out of Space", 90f);
            Button next = Small("NextLevelButton", "Next level", 124f);
            Button clear = Small("ClearBoardButton", "Clear board", 158f);

            panel.gameObject.AddComponent<DebugOnly>();
            DebugPopupPanel debug = panel.gameObject.AddComponent<DebugPopupPanel>();
            Wire(debug, ("settingsButton", settings), ("levelCompleteButton", win), ("outOfSpaceButton", space),
                ("nextLevelButton", next), ("clearBoardButton", clear));
        }

        // ------------------------------------------------------------------ Splash

        private static GameObject BuildSplash()
        {
            var root = new GameObject("Splash");

            // 3D stage with its own light and ground.
            var stage = new GameObject("HeroStage").transform;
            stage.SetParent(root.transform, false);
            var lightGo = new GameObject("Directional Light");
            lightGo.transform.SetParent(stage, false);
            lightGo.transform.rotation = Quaternion.Euler(50f, 140f, 0f);
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.5f;

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ground.name = "Ground";
            Object.DestroyImmediate(ground.GetComponent<Collider>());
            ground.transform.SetParent(stage, false);
            // Deep enough that its far edge is above the top of the screen.
            ground.transform.SetLocalPositionAndRotation(new Vector3(0f, -0.01f, 140f), Quaternion.Euler(90f, 0f, 0f));
            ground.transform.localScale = new Vector3(240f, 300f, 1f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = SplashGroundMaterial();
            ground.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            // UI
            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(root.transform, false);
            RootCanvas(canvasGo, 10);
            RectTransform safe = SafeArea(canvasGo.transform);

            RectTransform logo = Rect(safe, "Logo");
            Place(logo, 0.5f, 1f, 0f, -150f, 340f, 220f, 0.5f, 0.5f);
            logo.localRotation = Quaternion.Euler(0f, 0f, 5f);
            TextMeshProUGUI bus = Label(logo, "Bus", "BUS", 104f, TextStyle.Heading);
            Place(bus.rectTransform, 0.5f, 0.5f, -10f, 48f, 330f, 110f);
            TextMeshProUGUI rush = Label(logo, "Rush", "RUSH", 104f, TextStyle.Heading, Gold);
            Place(rush.rectTransform, 0.5f, 0.5f, 14f, -46f, 330f, 110f);

            TextMeshProUGUI loading = Label(safe, "LoadingText", "Loading", 20f, TextStyle.Body);
            Bottom(loading.rectTransform, 112f, 260f, 28f);
            Image track = Img(safe, "LoadingTrack", Circle, new Color(0.12f, 0.16f, 0.27f, 0.55f));
            MakePill(track, 26f);
            Bottom(track.rectTransform, 78f, 260f, 26f);
            Image fill = Img(track.transform, "Fill", Circle, Green);
            MakePill(fill, 20f);
            RectTransform fillRt = fill.rectTransform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(0.06f, 1f);
            fillRt.offsetMin = new Vector2(3f, 3f);
            fillRt.offsetMax = new Vector2(-3f, -3f);
            Image fillShine = Img(fill.transform, "Shine", Circle, new Color(1f, 1f, 1f, 0.3f));
            MakePill(fillShine, 7f);
            fillShine.rectTransform.anchorMin = new Vector2(0f, 0.55f);
            fillShine.rectTransform.anchorMax = new Vector2(1f, 0.9f);
            fillShine.rectTransform.offsetMin = new Vector2(6f, 0f);
            fillShine.rectTransform.offsetMax = new Vector2(-6f, 0f);

            TextMeshProUGUI version = Label(safe, "Version", "v1.0", 14f, TextStyle.BodyBoldPlain, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.BottomRight);
            Place(version.rectTransform, 1f, 0f, -12f, 10f, 120f, 20f, 1f, 0f);

            SplashScreen splash = root.AddComponent<SplashScreen>();
            Wire(splash, ("logo", logo), ("loadingFill", fillRt), ("loadingText", loading), ("versionText", version),
                ("stage", stage), ("heroes", new object[3]));
            GameObject Prefab(string n) => AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/BusRush/Prefabs/Vehicles/{n}.prefab");
            (string prefab, GameColor color, Vector3 pos, float yaw, float from, float delay)[] heroes =
            {
                ("Van", GameColor.Blue, new Vector3(-0.4f, 0f, 2.6f), 90f, -9f, 0.35f),
                ("Bus", GameColor.Yellow, new Vector3(0f, 0f, 0f), 90f, -8f, 0.15f),
                ("Car", GameColor.Red, new Vector3(0.6f, 0f, -2.5f), -90f, 9f, 0.55f)
            };
            for (int i = 0; i < heroes.Length; i++)
            {
                string p = $"heroes.Array.data[{i}].";
                Wire(splash, (p + "prefab", Prefab(heroes[i].prefab)), (p + "color", heroes[i].color), (p + "position", heroes[i].pos),
                    (p + "yaw", heroes[i].yaw), (p + "enterFromX", heroes[i].from), (p + "delay", heroes[i].delay));
            }
            return root;
        }

        private static Material SplashGroundMaterial()
        {
            const string path = "Assets/BusRush/Art/Materials/Splash_Ground.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Splash_Ground" };
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", UIArtGenerator.Hex(0x8CCBF7));
            m.SetFloat("_Smoothness", 0.1f);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ scenes

        private static void WireScenes(GameObject splash, GameObject home, GameObject hud, GameObject uiManager)
        {
            // Boot
            Scene boot = EditorSceneManager.OpenScene($"{ScenesDir}/Boot.unity", OpenSceneMode.Single);
            RemoveRoots(boot, "Splash");
            var splashInstance = (GameObject)PrefabUtility.InstantiatePrefab(splash, boot);
            Camera bootCam = FindRoot(boot, "Main Camera").GetComponent<Camera>();
            bootCam.orthographic = false;
            bootCam.fieldOfView = 50f;
            bootCam.backgroundColor = UIArtGenerator.Hex(0x5FB6F7);
            // Steep enough that the horizon stays above the screen; far enough to frame all three vehicles.
            bootCam.transform.SetPositionAndRotation(new Vector3(0f, 6.5f, -11.3f), Quaternion.Euler(30f, 0f, 0f));
            bootCam.nearClipPlane = 0.3f;
            bootCam.farClipPlane = 100f;
            BootLoader loader = FindRoot(boot, "Boot").GetComponent<BootLoader>();
            Wire(loader, ("splash", splashInstance.GetComponent<SplashScreen>()));
            EditorSceneManager.SaveScene(boot);

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
