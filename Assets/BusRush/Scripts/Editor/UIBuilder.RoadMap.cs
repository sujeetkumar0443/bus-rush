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

            MapNodeView nodeT = BuildMapNode(templates, BottomLeft, greenText, brownText, soft, ellipse, rays, star, sparkle);
            Button chestT = BuildMapChest(templates);
            MapSeamView seamT = BuildMapSeam(templates, soft, blueText);

            // Unlock trail: glow (#B8FF4A, 12 px, .35) + marching dashes (#E6FF9A, 6 px, 12/7).
            UiPolyline Trail(string name, Color c, float w, float d, float g)
            {
                RectTransform rt = Stretch(Rect(fx, name));
                rt.pivot = Vector2.zero;
                rt.gameObject.AddComponent<CanvasRenderer>();
                UiPolyline line = rt.gameObject.AddComponent<UiPolyline>();
                line.raycastTarget = false;
                line.color = c;
                line.SetStyle(w, d, g);
                return line;
            }

            UiPolyline trailGlow = Trail("TrailGlow", new Color(0.72f, 1f, 0.29f, 0.35f), 12f, 0f, 0f);
            UiPolyline trailMarch = Trail("TrailMarch", H(0xE6FF9A), 6f, 12f, 7f);

            // Bus: 112x112 frame, anchored at 50% 70% on the road; puffs behind, rumble on the sprite.
            RectTransform bus = BottomLeft(Rect(fx, "Bus"), 112f, 112f, 0.5f, 0.3f);
            var puffs = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                puffs[i] = Img(bus, "Puff" + i, soft, new Color(0.94f, 0.94f, 0.96f, 0f));
                RectTransform p = puffs[i].rectTransform;
                p.anchorMin = p.anchorMax = new Vector2(0.5f, 0.3f);
                p.sizeDelta = new Vector2(28f, 28f);
            }
            RectTransform rumble = Stretch(Rect(bus, "Rumble"));
            Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(UiV2 + "Map/map_bus_32dir.png").OfType<Sprite>()
                .OrderBy(s => s.name, StringComparer.Ordinal).ToArray();
            if (frames.Length != RoadMapGeometry.BusFrames) throw new InvalidOperationException($"map bus: {frames.Length} frames");
            Image busImg = Img(rumble, "Sprite", frames[5], Color.white);
            Stretch(busImg.rectTransform);

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
                ("busFrames", frames),
                ("scroll", scroll), ("content", content), ("tilesLayer", tiles), ("roadLayer", road), ("nodesLayer", nodes),
                ("chestLayer", chests), ("seamLayer", seams), ("fxLayer", fx),
                ("tileTemplate", tileT), ("roadTemplate", roadLine), ("nodeTemplate", nodeT), ("chestTemplate", chestT),
                ("seamTemplate", seamT),
                ("bus", bus), ("busRumble", rumble), ("busImage", busImg), ("busPuffs", puffs),
                ("trailGlow", trailGlow), ("trailMarch", trailMarch),
                ("homeButton", homeButton), ("worldNumberText", worldNum), ("worldNameText", worldName), ("toastFontProbe", toastLabel),
                ("coinTarget", (RectTransform)coin.transform.parent));
            root.SetActive(false);
            return root;
        }

        private static MapNodeView BuildMapNode(RectTransform parent, Func<RectTransform, float, float, float, float, RectTransform> bottomLeft,
                                                Material greenText, Material brownText, Sprite soft, Sprite ellipse, Sprite rays,
                                                Sprite star, Sprite sparkle)
        {
            Color greenEdge = H(0x1D5F12);
            RectTransform node = bottomLeft(Rect(parent, "Node"), 66f, 54f, 0.5f, 0.5f);
            Image hit = node.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            Button button = node.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            Wire(node.gameObject.AddComponent<UIButton>(), ("pressedScale", 0.92f), ("lightHaptic", true));

            // Current: glow (134x134 at -34,-44) with spinning rays + soft green glow.
            RectTransform glow = Box(Rect(node, "Glow"), -34f, -44f, 134f, 134f);
            Image spin = Img(glow, "Rays", rays, Color.white);
            Stretch(spin.rectTransform);
            Image glowSoft = Img(glow, "Soft", ellipse, new Color(210f / 255f, 1f, 120f / 255f, 0.95f));
            Box(glowSoft.rectTransform, 27f, 30f, 80f, 66f);

            RectTransform hop = Stretch(Rect(node, "Hop"));
            RectTransform green = Box(Rect(hop, "Green"), -3f, -3f, 72f, 58f);
            CanvasGroup greenGroup = green.gameObject.AddComponent<CanvasGroup>();
            Image greenDisc = ShadowedImg(green, "Disc", UiV2Sprite("Map/disc_green.png"), 4f, 0.3f);
            Stretch((RectTransform)greenDisc.transform.parent);
            TextMeshProUGUI greenNum = V2Text(green, "Number", "5", 26f, true, Color.white, greenText);
            Box(greenNum.rectTransform, 0f, 2f, 72f, 38f);

            // Completed: gold disc + number + stars.
            RectTransform gold = Stretch(Rect(node, "Gold"));
            Image goldDisc = ShadowedImg(gold, "Disc", UiV2Sprite("Map/disc_gold.png"), 4f, 0.3f);
            Stretch((RectTransform)goldDisc.transform.parent);
            TextMeshProUGUI goldNum = V2Text(gold, "Number", "1", 23f, true, Color.white, brownText);
            Box(goldNum.rectTransform, 0f, 2f, 66f, 36f);
            RectTransform starsRow = Box(Rect(node, "Stars"), 3f, 42f, 60f, 22f);
            var stars = new Image[3];
            (float x, float y, float s)[] starSpec = { (0f, 2f, 19f), (19f, 4f, 21f), (41f, 2f, 19f) };
            for (int i = 0; i < 3; i++)
            {
                stars[i] = Img(starsRow, "Star" + i, star, Color.white);
                Box(stars[i].rectTransform, starSpec[i].x, starSpec[i].y, starSpec[i].s, starSpec[i].s);
            }

            // Locked: grey disc 62x50 (shake origin 50% 60%).
            Image greyImg = Img(node, "Grey", UiV2Sprite("Map/disc_grey_locked.png"), Color.white);
            Box(greyImg.rectTransform, 2f, 2f, 62f, 50f, 0.5f, 0.4f);

            // Boss: 66x64 disc (5 px higher) + red number tag.
            RectTransform boss = Box(Rect(node, "Boss"), 0f, -10f, 66f, 64f);
            Image bossDisc = ShadowedImg(boss, "Disc", UiV2Sprite("Map/disc_boss.png"), 4f, 0.3f);
            Stretch((RectTransform)bossDisc.transform.parent);
            RectTransform tag = V2Box(boss, "Tag", 10f, H(0x7A1016), H(0x7A1016), null, 2f, Color.white, 0f);
            Box(tag, 13f, 56f, 40f, 20f);
            TextMeshProUGUI tagText = V2Text(tag, "Text", "10", 12f, true, Color.white);

            // Unlock star burst (9 stars 18x18 at 24,2) and twinkles.
            var burst = new Image[9];
            for (int i = 0; i < 9; i++)
            {
                burst[i] = Img(node, "Ray" + i, star, Color.white);
                Box(burst[i].rectTransform, 24f, 2f, 18f, 18f);
            }
            var tw = new Image[3];
            (float x, float y, float s, Color c)[] twSpec = { (-14f, -10f, 16f, Color.white), (66f, -6f, 12f, H(0xFFF6B0)), (58f, 44f, 14f, Color.white) };
            for (int i = 0; i < 3; i++)
            {
                tw[i] = Img(node, "Twinkle" + i, sparkle, twSpec[i].c);
                Box(tw[i].rectTransform, twSpec[i].x, twSpec[i].y, twSpec[i].s, twSpec[i].s);
            }

            // PLAY bubble (100x40 at -17,-58), pointer below.
            RectTransform play = Box(Rect(node, "Play"), -17f, -58f, 100f, 40f);
            CanvasGroup playGroup = play.gameObject.AddComponent<CanvasGroup>();
            RectTransform playHop = Stretch(Rect(play, "Hop"));
            Image pointer = Img(playHop, "Pointer", White, greenEdge);
            Place(pointer.rectTransform, 0.5f, 0f, 0f, -3f, 13f, 13f);
            pointer.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            RectTransform bubble = V2Box(playHop, "Bubble", 14f, H(0xA4F560), H(0x45C02A), null, 3f, greenEdge, 4f, true);
            Stretch(bubble);
            V2Text(bubble, "Label", "PLAY", 20f, true, Color.white, greenText);
            Button playButton = V2Button(play, playHop);

            MapNodeView view = node.gameObject.AddComponent<MapNodeView>();
            Wire(view, ("button", button), ("gold", gold.gameObject), ("goldNumber", goldNum), ("starsRow", starsRow.gameObject),
                ("stars", stars), ("glow", glow), ("spin", spin.rectTransform), ("glowSoft", glowSoft), ("hop", hop),
                ("green", green), ("greenGroup", greenGroup), ("greenNumber", greenNum), ("play", play), ("playGroup", playGroup),
                ("playHop", playHop), ("playButton", playButton), ("twinkles", tw), ("rays", burst),
                ("grey", greyImg.rectTransform), ("greyImage", greyImg), ("boss", boss.gameObject), ("bossTag", tagText));
            node.gameObject.SetActive(false);
            return view;
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

        private static MapSeamView BuildMapSeam(RectTransform parent, Sprite soft, Material blueText)
        {
            // Seam rect 390x140 centred on the tile seam; design y = seam - 70 + top.
            RectTransform seam = Rect(parent, "Seam");
            seam.anchorMin = seam.anchorMax = Vector2.zero;
            seam.pivot = new Vector2(0f, 0.5f);
            seam.sizeDelta = new Vector2(RoadMapGeometry.FrameWidth, 140f);
            const float top0 = 1691.5f - 70f;
            (float l, float t, float w, float h)[] clouds =
            {
                (-85f, 1656f, 150f, 80f), (-15f, 1643f, 170f, 86f), (70f, 1658f, 180f, 84f),
                (165f, 1644f, 170f, 88f), (250f, 1658f, 160f, 80f), (325f, 1653f, 150f, 78f)
            };
            for (int i = 0; i < clouds.Length; i++)
            {
                Image c = Img(seam, "Cloud" + i, soft, Color.white);
                Box(c.rectTransform, clouds[i].l, clouds[i].t - top0, clouds[i].w, clouds[i].h);
            }

            RectTransform sign = V2Box(seam, "WorldSign", 20f, H(0x7FD3FF), H(0x2F86E8), null, 4f, H(0x103E86), 5f);
            Box(sign, 85f, 1662f - top0, 220f, 60f);
            TextMeshProUGUI num = V2Text(sign, "World", "WORLD 2", 11f, true, H(0xD6F0FF));
            num.characterSpacing = 8f;
            Place(num.rectTransform, 0.5f, 0.5f, 0f, 11f, 200f, 14f);
            TextMeshProUGUI name = V2Text(sign, "Name", "Snow", 22f, true, Color.white, blueText);
            Place(name.rectTransform, 0.5f, 0.5f, 0f, -7f, 200f, 26f);
            Button signButton = V2Button(sign);

            RectTransform badge = V2Box(sign, "Lock", 12f, H(0x24345E), H(0x24345E), null, 2f, Color.white, 0f);
            Place(badge, 1f, 1f, 16f, 16f, 80f, 28f, 1f, 1f);
            Image lockIcon = Img(badge, "Icon", UiV2Sprite("Map/lock.png"), Color.white);
            Place(lockIcon.rectTransform, 0f, 0.5f, 6f, 0f, 16f, 20f, 0f, 0.5f);
            TextMeshProUGUI lockText = V2Text(badge, "Text", "Level 11", 12f, true, Color.white, null, TextAlignmentOptions.Left);
            Stretch(lockText.rectTransform, 24f, 0f, 4f, 0f);

            MapSeamView view = seam.gameObject.AddComponent<MapSeamView>();
            Wire(view, ("signButton", signButton), ("worldNumber", num), ("worldName", name), ("lockBadge", badge.gameObject),
                ("lockText", lockText));
            seam.gameObject.SetActive(false);
            return view;
        }
    }
}
