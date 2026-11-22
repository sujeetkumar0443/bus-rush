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
                SavePopup(BuildSettingsPopup()),
                SavePopup(BuildPausePopup()),
                SavePopup(BuildDailyPopup()),
                SavePopup(BuildLuckySpinPopup()),
                SavePopup(BuildBoosterPopup()),
                SavePopup(BuildFreeCoins())
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
            Save(BuildGiftChestScreen(), "GiftChestScreen");
            Save(BuildNewWorldScreen(), "NewWorldScreen");
            Save(BuildRoadMap(), "RoadMap");
            Save(BuildLevelCompleteScreen(), "LevelCompleteScreen");
            Save(BuildLevelFailedScreen(), "LevelFailedScreen");

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

        private static void BuildDebugPanel(RectTransform safe)
        {
            // Under the Pause button / level badge, above the bays; hidden by default (DebugOnly).
            Image panel = Img(safe, "DebugPanel (TEMP)", Round, new Color(0.12f, 0.16f, 0.27f, 0.85f), 12f);
            panel.raycastTarget = true;
            Place(panel.rectTransform, 0f, 1f, 10f, -84f, 172f, 232f, 0f, 1f);
            TextMeshProUGUI title = Label(panel.transform, "Title", "DEBUG", 12f, TextStyle.BodyPlain, new Color(1f, 1f, 1f, 0.8f));
            Top(title.rectTransform, 4f, 160f, 16f);

            Button Small(string name, string text, float x, float y, float w, Color face, Color lip)
            {
                Chunk b = Chunky(panel.transform, name, face, lip, w, 26f, 8f, 3f, true, false, false);
                Place(b.Root, 0f, 1f, x, -y, w, 26f, 0f, 1f);
                TextMeshProUGUI t = Label(b.Face, "Label", text, 13f, TextStyle.BodyPlain, DarkNavy);
                Stretch(t.rectTransform, 4f, 0f, 4f, 0f);
                return b.Button;
            }

            Button minus = Small("LevelMinus", "-1", 6f, 22f, 50f, Grey, GreyLip);
            Button plus = Small("LevelPlus", "+1", 61f, 22f, 50f, Grey, GreyLip);
            Button plusTen = Small("LevelPlus10", "+10", 116f, 22f, 50f, Grey, GreyLip);
            Button go = Small("GoToLevel", "Go to level 1", 6f, 52f, 160f, Gold, GoldLip);
            Button coins = Small("AddCoins", "+1000 coins", 6f, 82f, 160f, Grey, GreyLip);
            Button boosters = Small("AddBoosters", "+5 of each booster", 6f, 112f, 160f, Grey, GreyLip);
            Button win = Small("WinLevel", "Win level", 6f, 142f, 78f, Green, GreenLip);
            Button fail = Small("FailLevel", "Fail level", 88f, 142f, 78f, Red, RedLip);
            Button reset = Small("ResetSave", "Reset save", 6f, 196f, 160f, Red, RedLip);

            panel.gameObject.AddComponent<DebugOnly>();
            DebugPopupPanel debug = panel.gameObject.AddComponent<DebugPopupPanel>();
            Wire(debug, ("minusButton", minus), ("plusButton", plus), ("plusTenButton", plusTen), ("goButton", go),
                ("goLabel", go.transform.Find("Body/Face/Label").GetComponent<TextMeshProUGUI>()), ("coinsButton", coins),
                ("boostersButton", boosters), ("winButton", win), ("failButton", fail), ("resetButton", reset));
        }

        // ------------------------------------------------------------------ scenes

        private static void WireScenes(GameObject splash, GameObject home, GameObject hud, GameObject uiManager)
        {
            WireBoot(splash);

            WireHome(home, uiManager);

            WireGame(hud, uiManager);

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
