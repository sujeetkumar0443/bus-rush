using System;
using System.IO;
using System.Linq;
using BusRush.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static BusRush.EditorTools.UIKit;
using Object = UnityEngine.Object;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Step R3: the animated Road Map (Docs/UI_v2_Design/RoadMap.dc.html) as the "RoadMap" prefab, an inactive
    /// full-screen overlay in the Home scene (HomeScreen.roadMap). Design px; the canvas matches width 390.
    /// </summary>
    public static partial class UIBuilder
    {
        private const string GeneratedDir = "Assets/BusRush/Art/UI_v2/Generated";

        [MenuItem("Tools/Bus Rush/Build Road Map (R3)")]
        public static void BuildRoadMapOnly()
        {
            Load();
            Save(BuildRoadMap(), "RoadMap");
            AssetDatabase.SaveAssets();
            GameObject home = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/HomeScreen.prefab");
            GameObject uiManager = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/UIManager.prefab");
            WireHome(home, uiManager);
            EditorSceneManager.OpenScene($"{ScenesDir}/Boot.unity", OpenSceneMode.Single);
            Debug.Log("[UIBuilder] Road Map built.");
        }

        public static void BuildRoadMapBatch()
        {
            try
            {
                BuildRoadMapOnly();
            }
            catch (Exception e)
            {
                Debug.LogError("[UIBuilder] FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }

        // ------------------------------------------------------------------ generated sprites

        /// <summary>Writes (once) a procedural RGBA sprite into Art/UI_v2/Generated and returns it.</summary>
        private static Sprite GeneratedSprite(string name, int size, Func<float, float, Color> pixel)
        {
            string path = $"{GeneratedDir}/{name}.png";
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(GeneratedDir);
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var px = new Color[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++) px[y * size + x] = pixel((x + 0.5f) / size, (y + 0.5f) / size);
                }
                tex.SetPixels(px);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti.textureType != TextureImporterType.Sprite || ti.mipmapEnabled)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>CSS radial-gradient(circle, #FFF 0%, rgba(255,255,255,.85~.92) 45%, transparent 72%) (farthest corner).</summary>
        private static Sprite SoftCircleSprite() => GeneratedSprite("soft_circle", 128, (u, v) =>
        {
            float d = new Vector2(u - 0.5f, v - 0.5f).magnitude / 0.7071f;
            float a = d < 0.45f ? Mathf.Lerp(1f, 0.9f, d / 0.45f) : Mathf.Lerp(0.9f, 0f, Mathf.InverseLerp(0.45f, 0.72f, d));
            return new Color(1f, 1f, 1f, Mathf.Clamp01(a));
        });

        /// <summary>CSS ellipse glow: radial-gradient(ellipse, color 0%, transparent 70%) (farthest side scaled), white.</summary>
        private static Sprite SoftEllipseSprite() => GeneratedSprite("soft_ellipse", 128, (u, v) =>
        {
            float d = new Vector2(u - 0.5f, v - 0.5f).magnitude / 0.7071f;
            return new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d / 0.7f));
        });

        /// <summary>Current-level light rays: repeating-conic-gradient(rgba(255,255,170,.55) 0-8deg, clear 8-24deg), mask 18%->62%.</summary>
        private static Sprite MapRaysSprite() => GeneratedSprite("map_rays", 256, (u, v) =>
        {
            Vector2 d = new Vector2(u - 0.5f, v - 0.5f);
            float r = d.magnitude / 0.7071f;
            float mask = 1f - Mathf.InverseLerp(0.18f, 0.62f, r);
            float deg = (Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg + 360f) % 24f;
            float soft = Mathf.Clamp(57.3f * (1f / 256f) / Mathf.Max(d.magnitude, 0.01f), 0.1f, 3f);
            float wedge = Mathf.Clamp01((deg + soft * 0.5f) / soft) * Mathf.Clamp01((8f - deg + soft * 0.5f) / soft);
            return new Color(1f, 1f, 170f / 255f, 0.55f * wedge * mask);
        });

        // ------------------------------------------------------------------ Road Map

        private static GameObject BuildRoadMap()
        {
            Color greenEdge = H(0x1D5F12), blueEdge = H(0x103E86), navyEdge = H(0x1B4FA0);
            Material greenText = LilitaOutline("Green", greenEdge);
            Material blueText = LilitaOutline("Blue", blueEdge);
            Material brownText = LilitaOutline("DarkBrown", H(0x8A4A05));
            Sprite soft = SoftCircleSprite(), ellipse = SoftEllipseSprite(), rays = MapRaysSprite();
            Sprite star = UiV2Sprite("Icons/star.png"), sparkle = UiV2Sprite("Splash/sparkle.png");

            var root = new GameObject("RoadMap", typeof(RectTransform));
            RootCanvas(root, 50);
            root.GetComponent<CanvasScaler>().matchWidthOrHeight = 0f; // map tiles are always 476/390 of the width
            Image bgFill = root.AddComponent<Image>();
            bgFill.color = H(0x7CCB4E);

            // --- scroll view + content layers
            RectTransform scrollRt = Stretch(Rect(root.transform, "Scroll"));
            Image hit = scrollRt.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            scrollRt.gameObject.AddComponent<RectMask2D>();
            ScrollRect scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            scrollRt.gameObject.AddComponent<MapDragWatcher>();
            RectTransform content = Rect(scrollRt, "Content");
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(1f, 0f);
            content.pivot = new Vector2(0.5f, 0f);
            content.sizeDelta = new Vector2(0f, RoadMapGeometry.TileHeight * 10f);
            content.anchoredPosition = Vector2.zero;
            scroll.content = content;
            scroll.viewport = scrollRt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.08f;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 30f;

            RectTransform Layer(string name)
            {
                RectTransform l = Stretch(Rect(content, name));
                l.pivot = Vector2.zero;
                return l;
            }

            RectTransform tiles = Layer("Tiles"), road = Layer("Road"), nodes = Layer("Nodes"), chests = Layer("Chests"),
                          seams = Layer("Seams"), fx = Layer("Fx");
            RectTransform templates = Layer("Templates");
            templates.gameObject.SetActive(false);

            RectTransform BottomLeft(RectTransform rt, float w, float h, float px = 0f, float py = 0f)
            {
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.pivot = new Vector2(px, py);
                rt.sizeDelta = new Vector2(w, h);
                rt.anchoredPosition = Vector2.zero;
                return rt;
            }

            // Tile image + dashed road centre line (white, 4 px, dash 11 gap 13, 0.9).
            Image tileT = Img(templates, "Tile", UiV2Sprite("Backgrounds/map_city.png"), Color.white);
            BottomLeft(tileT.rectTransform, RoadMapGeometry.TileWidth, RoadMapGeometry.TileHeight + 1f);
            RectTransform roadRt = BottomLeft(Rect(templates, "RoadLine"), RoadMapGeometry.FrameWidth, RoadMapGeometry.TileHeight);
            roadRt.gameObject.AddComponent<CanvasRenderer>();
            UiPolyline roadLine = roadRt.gameObject.AddComponent<UiPolyline>();
            roadLine.raycastTarget = false;
            roadLine.color = new Color(1f, 1f, 1f, 0.9f);
            roadLine.SetStyle(4f, 11f, 13f);

            Button chestT = BuildMapChest(templates);

            // --- top bar: gradient band, Home, world ribbon, coin pill (safe area)
            Image band = Img(root.transform, "TopBand", White, Color.white);
            Place(band.rectTransform, 0.5f, 1f, 0f, 0f, 0f, 92f, 0.5f, 1f);
            band.rectTransform.anchorMin = new Vector2(0f, 1f);
            band.rectTransform.anchorMax = new Vector2(1f, 1f);
            band.rectTransform.sizeDelta = new Vector2(0f, 92f);
            Gradient(band, new Color(20f / 255f, 60f / 255f, 140f / 255f, 0.55f), new Color(20f / 255f, 60f / 255f, 140f / 255f, 0f));

            RectTransform safe = SafeArea(root.transform);
            RectTransform homeRt = V2Box(safe, "HomeButton", 20f, Color.white, H(0xDDF1FF), null, 3f, navyEdge, 4f, true);
            Place(homeRt, 0f, 1f, 12f, -14f, 56f, 56f, 0f, 1f);
            Image homeIcon = Img(homeRt, "Icon", UiV2Sprite("Icons/home.png"), Color.white);
            Place(homeIcon.rectTransform, 0.5f, 0.5f, 0f, 0f, 40f, 42f);
            Button homeButton = V2Button(homeRt);

            RectTransform ribbon = V2Box(safe, "WorldRibbon", 16f, H(0x7FD3FF), H(0x2F86E8), null, 3f, blueEdge, 4f);
            Place(ribbon, 0f, 1f, 90f, -18f, 150f, 48f, 0f, 1f);
            TextMeshProUGUI worldNum = V2Text(ribbon, "World", "WORLD 1", 11f, true, H(0xD6F0FF));
            worldNum.characterSpacing = 8f;
            Place(worldNum.rectTransform, 0.5f, 0.5f, 0f, 11f, 140f, 14f);
            TextMeshProUGUI worldName = V2Text(ribbon, "Name", "City", 22f, true, Color.white, blueText);
            Place(worldName.rectTransform, 0.5f, 0.5f, 0f, -7f, 140f, 26f);

            RectTransform coinGroup = Place(Rect(safe, "CoinPill"), 0f, 1f, 0f, 0f, 200f, 70f, 0f, 1f);
            RectTransform pill = V2Box(coinGroup, "Pill", 17f, Color.white, H(0xFFF1CF), null, 3f, H(0x7A4A16), 0f);
            Place(pill, 0f, 1f, 280f, -25f, 80f, 34f, 0f, 1f);
            TextMeshProUGUI coinText = V2Text(pill, "Count", "300", 20f, true, H(0x5A3410), null, TextAlignmentOptions.Left);
            Stretch(coinText.rectTransform, 31f, 0f, 8f, 2f);
            coinText.gameObject.AddComponent<CoinText>();
            Wire(pill.gameObject.AddComponent<UiFitWidthToText>(), ("text", coinText), ("padding", 50f), ("minWidth", 70f));
            Image coin = ShadowedImg(coinGroup, "Coin", UiV2Sprite("Icons/coin.png"), 3f, 0.25f);
            Box((RectTransform)coin.transform.parent, 258f, 18.5f, 44f, 47f);
            Wire(coinText.GetComponent<CoinText>(), ("punchTarget", coin.transform.parent));

            // Toast (taps on discs, chests, signs).
            RectTransform toast = V2Box(safe, "Toast", 23f, H(0x24345E), H(0x14204F), null, 3f, Color.white, 0f);
            Place(toast, 0.5f, 0.5f, 0f, 120f, 250f, 46f);
            toast.gameObject.AddComponent<CanvasGroup>();
            TextMeshProUGUI toastLabel = V2Text(toast, "Label", "", 20f, false, Color.white);
            Wire(toast.gameObject.AddComponent<Toast>(), ("label", toastLabel));

            RoadMapScreen screen = root.AddComponent<RoadMapScreen>();
            Wire(screen,
                ("roadJson", AssetDatabase.LoadAssetAtPath<TextAsset>(UiV2 + "Map/road_centerline.json")),
                ("tileSprites", new object[] { UiV2Sprite("Backgrounds/map_city.png"), UiV2Sprite("Backgrounds/map_snow.png"), UiV2Sprite("Backgrounds/map_beach.png") }),
                ("scroll", scroll), ("content", content), ("tilesLayer", tiles), ("roadLayer", road), ("nodesLayer", nodes),
                ("chestLayer", chests), ("fxLayer", fx),
                ("tileTemplate", tileT), ("roadTemplate", roadLine), ("chestTemplate", chestT),
                ("homeButton", homeButton), ("worldNumberText", worldNum), ("worldNameText", worldName),
                ("coinTarget", (RectTransform)coin.transform.parent));
            ApplyRoadMapV3(root); // mist / banner / marker layers, nodes, seams, level-complete sequence, level card
            root.SetActive(false);
            return root;
        }

        private static Button BuildMapChest(RectTransform parent)
        {
            // 60 wide column (top-left anchored at road + (-104, +40)): bouncing chest 56x48 + "Gift" tag.
            RectTransform chest = Rect(parent, "Chest");
            chest.anchorMin = chest.anchorMax = Vector2.zero;
            chest.pivot = new Vector2(0f, 1f);
            chest.sizeDelta = new Vector2(60f, 66f);
            Image hit = chest.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            Image img = ShadowedImg(chest, "ChestImage", UiV2Sprite("Map/chest_closed.png"), 4f, 0.35f);
            RectTransform imgRt = Box((RectTransform)img.transform.parent, 2f, 0f, 56f, 48f, 0.5f, 0.5f);
            UiLoopBob bob = imgRt.gameObject.AddComponent<UiLoopBob>();
            var chestView = chest.gameObject.AddComponent<MapChestView>();
            Wire(chestView, ("image", img), ("shadow", imgRt.Find("DropShadow").GetComponent<Image>()),
                ("bob", bob), ("closedSprite", UiV2Sprite("Map/chest_closed.png")), ("openSprite", UiV2Sprite("Map/chest_open.png")));
            Wire(chestView, ("glow", AddChestGlow(chest)));
            RectTransform tag = V2Box(chest, "Gift", 10f, H(0x7A4A16), H(0x7A4A16), null, 2f, H(0xFFE27A), 0f);
            Box(tag, 11f, 44f, 38f, 18f);
            V2Text(tag, "Text", "Gift", 11f, true, H(0xFFE27A));
            Button b = V2Button(chest);
            chest.gameObject.SetActive(false);
            return b;
        }
    }
}
