using System;
using System.Linq;
using BusRush.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static BusRush.EditorTools.UIKit;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Road Map v3: layer order (tiles, mist, path dots, sequence fx, banner, nodes, chests, current-node marker), the red Play node,
    /// small lock badges on locked nodes, a lower mist, the level-complete sequence parts (grey / gold path, travelling dot, skip catcher)
    /// and the Level Start card. <see cref="ApplyRoadMapV3"/> is idempotent: it is called by the full Road Map build and by the in-place
    /// patch (Tools > Bus Rush > Patch Road Map v3) that edits RoadMap.prefab without replacing it (the Home scene keeps its wiring).
    /// </summary>
    public static partial class UIBuilder
    {
        private const string MapArt = "Assets/BusRush/Art/UI_v2/Map";

        [MenuItem("Tools/Bus Rush/Patch Road Map v3")]
        public static void PatchRoadMapV3()
        {
            Load();
            string path = $"{PrefabDir}/RoadMap.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ApplyRoadMapV3(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[UIBuilder] Road Map v3 applied to RoadMap.prefab.");
        }

        public static void PatchRoadMapV3Batch()
        {
            try
            {
                PatchRoadMapV3();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[UIBuilder] FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }

        private static void EnsureSpriteImport(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti == null || (ti.textureType == TextureImporterType.Sprite && ti.spriteImportMode == SpriteImportMode.Single)) return;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.SaveAndReimport();
        }

        /// <summary>Soft ring (annulus) for the current node's pulse and the unlock pop.</summary>
        private static Sprite RingSprite() => GeneratedSprite("map_ring", 128, (u, v) =>
        {
            float d = new Vector2(u - 0.5f, v - 0.5f).magnitude / 0.5f;
            float a = Smooth(0.66f, 0.78f, d) * Smooth(0.99f, 0.9f, d);
            return new Color(1f, 1f, 1f, Mathf.Clamp01(a));
        });

        // ------------------------------------------------------------------ apply

        private static void ApplyRoadMapV3(GameObject root)
        {
            Material greenText = LilitaOutline("Green", H(0x1D5F12));
            Material blueText = LilitaOutline("Blue", H(0x103E86));
            Material brownText = LilitaOutline("DarkBrown", H(0x8A4A05));
            EnsureSpriteImport(MapArt + "/disc_red.png");
            Sprite soft = SoftCircleSprite(), ellipse = SoftEllipseSprite(), rays = MapRaysSprite(), ring = RingSprite();
            Sprite star = UiV2Sprite("Icons/star.png"), sparkle = UiV2Sprite("Splash/sparkle.png"), coin = UiV2Sprite("Icons/coin.png");

            var screen = root.GetComponent<RoadMapScreen>();
            var content = (RectTransform)root.transform.Find("Scroll/Content");
            var templates = (RectTransform)content.Find("Templates");

            // Old pieces: node / seam templates are rebuilt, the bus and the old trail are gone for good.
            foreach (string old in new[] { "Node", "Seam", "GoldRoadLine" })
            {
                Transform t = templates.Find(old);
                if (t != null) UnityEngine.Object.DestroyImmediate(t.gameObject);
            }
            Transform oldSeams = content.Find("Seams");
            if (oldSeams != null) UnityEngine.Object.DestroyImmediate(oldSeams.gameObject);
            Transform fxOld = content.Find("Fx");
            if (fxOld != null)
            {
                for (int i = fxOld.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(fxOld.GetChild(i).gameObject);
            }

            // Layers, back to front: tiles, mist, road (path dots), fx, banner (above the dashes so they never cross its text), nodes, chests, marker.
            RectTransform Layer(string name)
            {
                Transform existing = content.Find(name);
                if (existing != null) return (RectTransform)existing;
                RectTransform l = Stretch(Rect(content, name));
                l.pivot = Vector2.zero;
                return l;
            }
            RectTransform tiles = Layer("Tiles"), mist = Layer("Mist"), banner = Layer("Banner"), road = Layer("Road"), fx = Layer("Fx"),
                          nodes = Layer("Nodes"), chests = Layer("Chests"), marker = Layer("Marker");
            string[] order = { "Tiles", "Mist", "Road", "Fx", "Banner", "Nodes", "Chests", "Marker", "Templates" };
            for (int i = 0; i < order.Length; i++) content.Find(order[i]).SetSiblingIndex(i);

            // Templates.
            MapNodeView nodeT = BuildMapNode(templates, greenText, brownText, soft, ellipse, rays, ring, star, sparkle);
            MapSeamView seamT = BuildMapSeam(templates, soft, blueText);
            Transform roadLineT = templates.Find("RoadLine");
            GameObject goldGo = UnityEngine.Object.Instantiate(roadLineT.gameObject, templates);
            goldGo.name = "GoldRoadLine";
            UiPolyline goldLine = goldGo.GetComponent<UiPolyline>();
            goldLine.color = H(0xFFC531);
            goldLine.SetStyle(5f, 11f, 13f);
            goldLine.raycastTarget = false;
            goldGo.SetActive(false);

            // Level-complete sequence parts: grey dashes under gold dashes + glow, a travelling dot.
            UiPolyline Line(string name, Color c, float w, float d, float g)
            {
                RectTransform rt = Stretch(Rect(fx, name));
                rt.pivot = Vector2.zero;
                rt.gameObject.AddComponent<CanvasRenderer>();
                UiPolyline line = rt.gameObject.AddComponent<UiPolyline>();
                line.raycastTarget = false;
                line.color = c;
                line.SetStyle(w, d, g);
                rt.gameObject.SetActive(false);
                return line;
            }
            UiPolyline greyPath = Line("GreyPath", H(0x8E98AB), 5f, 11f, 13f);
            UiPolyline goldGlow = Line("GoldGlow", new Color(1f, 0.82f, 0.25f, 0.35f), 12f, 0f, 0f);
            UiPolyline goldPath = Line("GoldPath", H(0xFFC531), 5f, 11f, 13f);
            Image dotImage = Img(fx, "Dot", soft, new Color(1f, 0.93f, 0.55f, 0.95f));
            dotImage.raycastTarget = false;
            RectTransform dot = dotImage.rectTransform;
            dot.anchorMin = dot.anchorMax = Vector2.zero;
            dot.pivot = new Vector2(0.5f, 0.5f);
            dot.sizeDelta = new Vector2(46f, 46f);
            dot.gameObject.SetActive(false);

            // Skip catcher (tap anywhere during the sequence): above the map, below the top bar.
            Transform oldCatcher = root.transform.Find("SkipCatcher");
            if (oldCatcher != null) UnityEngine.Object.DestroyImmediate(oldCatcher.gameObject);
            Image catcherImg = Img(root.transform, "SkipCatcher", White, new Color(0f, 0f, 0f, 0f));
            Stretch(catcherImg.rectTransform);
            Button catcher = catcherImg.gameObject.AddComponent<Button>();
            catcher.transition = Selectable.Transition.None;
            catcher.targetGraphic = catcherImg;
            catcher.gameObject.SetActive(false);
            catcher.transform.SetSiblingIndex(root.transform.Find("Scroll").GetSiblingIndex() + 1);

            // Level Start card: the last child of the map, over everything.
            Transform oldCard = root.transform.Find("LevelCard");
            if (oldCard != null) UnityEngine.Object.DestroyImmediate(oldCard.gameObject);
            LevelStartCard card = BuildLevelCard((RectTransform)root.transform, star, coin);

            Wire(screen,
                ("tilesLayer", tiles), ("mistLayer", mist), ("bannerLayer", banner), ("roadLayer", road), ("fxLayer", fx),
                ("nodesLayer", nodes), ("chestLayer", chests), ("markerLayer", marker),
                ("nodeTemplate", nodeT), ("seamTemplate", seamT), ("goldRoadTemplate", goldLine),
                ("greyPath", greyPath), ("goldGlow", goldGlow), ("goldPath", goldPath), ("dot", dot), ("dotImage", dotImage),
                ("skipCatcher", catcher), ("levelCard", card));
            if (templates.Find("Chest") != null) Wire(screen, ("chestTemplate", templates.Find("Chest").GetComponent<Button>()));
        }

        // ------------------------------------------------------------------ node

        private static MapNodeView BuildMapNode(RectTransform parent, Material greenText, Material brownText, Sprite soft, Sprite ellipse,
                                                Sprite rays, Sprite ring, Sprite star, Sprite sparkle)
        {
            RectTransform node = Rect(parent, "Node");
            node.anchorMin = node.anchorMax = Vector2.zero;
            node.pivot = new Vector2(0.5f, 0.5f);
            node.sizeDelta = new Vector2(66f, 54f);
            Image hit = node.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            Button button = node.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            Wire(node.gameObject.AddComponent<UIButton>(), ("pressedScale", 0.92f), ("lightHaptic", true));

            // Current: slowly turning light rays + a soft warm glow behind the red disc.
            RectTransform glow = Box(Rect(node, "Glow"), -34f, -44f, 134f, 134f);
            Image spin = Img(glow, "Rays", rays, new Color(1f, 0.85f, 0.7f, 1f));
            spin.raycastTarget = false;
            Stretch(spin.rectTransform);
            Image glowSoft = Img(glow, "Soft", ellipse, new Color(1f, 0.55f, 0.45f, 0.7f));
            glowSoft.raycastTarget = false;
            Box(glowSoft.rectTransform, 27f, 30f, 80f, 66f);

            // Pulsing ring (also the unlock pop's ring), centred on the disc.
            Image ringImg = Img(node, "Ring", ring, new Color(1f, 0.78f, 0.72f, 0f));
            ringImg.raycastTarget = false;
            Box(ringImg.rectTransform, -15f, -21f, 96f, 96f);

            // Current: 125 % red Play disc with a white triangle, bounces every 2 s.
            RectTransform hop = Stretch(Rect(node, "Hop"));
            RectTransform red = Box(Rect(hop, "Red"), -3f, -3f, 72f, 58f);
            Image redDisc = ShadowedImg(red, "Disc", UiV2Sprite("Map/disc_red.png"), 4f, 0.3f);
            Stretch((RectTransform)redDisc.transform.parent);
            Image playIcon = Img(hop, "PlayIcon", GeneratedSprite("ui_play", 64, 64, PaintPlay), Color.white);
            playIcon.raycastTarget = false;
            Box(playIcon.rectTransform, 20f, 10f, 30f, 32f);

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

            // Locked: grey disc 62x50 + a small lock badge on its top right (it belongs to the node, never to a banner).
            Image greyImg = Img(node, "Grey", UiV2Sprite("Map/disc_grey_locked.png"), Color.white);
            Box(greyImg.rectTransform, 2f, 2f, 62f, 50f, 0.5f, 0.4f);
            RectTransform badge = V2Box(node, "LockBadge", 9f, H(0x4A5A8E), H(0x24345E), null, 2f, Color.white, 0f);
            Box(badge, 45f, -8f, 24f, 26f);
            Image lockIcon = Img(badge, "Icon", UiV2Sprite("Map/lock.png"), Color.white);
            lockIcon.raycastTarget = false;
            Place(lockIcon.rectTransform, 0.5f, 0.5f, 0f, 0f, 13f, 17f);

            // The two halves of the cracked lock disc (filled left / right half of the grey disc).
            Image halfL = Img(node, "HalfLeft", UiV2Sprite("Map/disc_grey_locked.png"), Color.white);
            Box(halfL.rectTransform, 2f, 2f, 62f, 50f, 0.5f, 0.4f);
            halfL.type = Image.Type.Filled;
            halfL.fillMethod = Image.FillMethod.Horizontal;
            halfL.fillOrigin = (int)Image.OriginHorizontal.Left;
            halfL.fillAmount = 0.5f;
            halfL.raycastTarget = false;
            Image halfR = Img(node, "HalfRight", UiV2Sprite("Map/disc_grey_locked.png"), Color.white);
            Box(halfR.rectTransform, 2f, 2f, 62f, 50f, 0.5f, 0.4f);
            halfR.type = Image.Type.Filled;
            halfR.fillMethod = Image.FillMethod.Horizontal;
            halfR.fillOrigin = (int)Image.OriginHorizontal.Right;
            halfR.fillAmount = 0.5f;
            halfR.raycastTarget = false;

            // Boss: 66x64 disc (5 px higher) + red number tag.
            RectTransform boss = Box(Rect(node, "Boss"), 0f, -10f, 66f, 64f);
            Image bossDisc = ShadowedImg(boss, "Disc", UiV2Sprite("Map/disc_boss.png"), 4f, 0.3f);
            Stretch((RectTransform)bossDisc.transform.parent);
            RectTransform tag = V2Box(boss, "Tag", 10f, H(0x7A1016), H(0x7A1016), null, 2f, Color.white, 0f);
            Box(tag, 13f, 56f, 40f, 20f);
            TextMeshProUGUI tagText = V2Text(tag, "Text", "10", 12f, true, Color.white);

            // Star burst (9 stars) and twinkles, white flash.
            var burst = new Image[9];
            for (int i = 0; i < 9; i++)
            {
                burst[i] = Img(node, "Ray" + i, star, Color.white);
                burst[i].raycastTarget = false;
                Box(burst[i].rectTransform, 24f, 2f, 18f, 18f);
            }
            var tw = new Image[3];
            (float x, float y, float s, Color c)[] twSpec = { (-14f, -10f, 16f, Color.white), (66f, -6f, 12f, H(0xFFF6B0)), (58f, 44f, 14f, Color.white) };
            for (int i = 0; i < 3; i++)
            {
                tw[i] = Img(node, "Twinkle" + i, sparkle, twSpec[i].c);
                tw[i].raycastTarget = false;
                Box(tw[i].rectTransform, twSpec[i].x, twSpec[i].y, twSpec[i].s, twSpec[i].s);
            }
            Image flash = Img(node, "Flash", soft, new Color(1f, 1f, 1f, 0f));
            flash.raycastTarget = false;
            Box(flash.rectTransform, -22f, -28f, 110f, 110f);

            MapNodeView view = node.gameObject.AddComponent<MapNodeView>();
            Wire(view, ("button", button), ("gold", gold.gameObject), ("goldNumber", goldNum), ("starsRow", starsRow.gameObject),
                ("stars", stars), ("glow", glow), ("spin", spin.rectTransform), ("glowSoft", glowSoft), ("ring", ringImg), ("hop", hop),
                ("red", red.gameObject), ("playIcon", playIcon.rectTransform), ("twinkles", tw), ("rays", burst), ("flash", flash),
                ("grey", greyImg.rectTransform), ("greyImage", greyImg), ("lockBadge", badge.gameObject), ("halfLeft", halfL),
                ("halfRight", halfR), ("boss", boss.gameObject), ("bossTag", tagText));
            node.gameObject.SetActive(false);
            return view;
        }

        // ------------------------------------------------------------------ seam (mist + banner, shown by two instances)

        private static MapSeamView BuildMapSeam(RectTransform parent, Sprite soft, Material blueText)
        {
            // Seam rect 390x140 centred on the tile seam; design y = seam - 70 + top.
            RectTransform seam = Rect(parent, "Seam");
            seam.anchorMin = seam.anchorMax = Vector2.zero;
            seam.pivot = new Vector2(0f, 0.5f);
            seam.sizeDelta = new Vector2(RoadMapGeometry.FrameWidth, 140f);
            const float top0 = 1691.5f - 70f;
            // Mist: the same six clouds, about 45 % as tall and a bit lighter, centred on the seam so it never reaches a node.
            (float l, float t, float w, float h)[] clouds =
            {
                (-85f, 1656f, 150f, 80f), (-15f, 1643f, 170f, 86f), (70f, 1658f, 180f, 84f),
                (165f, 1644f, 170f, 88f), (250f, 1658f, 160f, 80f), (325f, 1653f, 150f, 78f)
            };
            var cloudObjects = new GameObject[clouds.Length];
            for (int i = 0; i < clouds.Length; i++)
            {
                float h = clouds[i].h * 0.45f;
                float cy = clouds[i].t + clouds[i].h * 0.5f; // original vertical centre
                Image c = Img(seam, "Cloud" + i, soft, new Color(1f, 1f, 1f, 0.85f));
                c.raycastTarget = false;
                Box(c.rectTransform, clouds[i].l, cy - h * 0.5f - top0, clouds[i].w, h);
                cloudObjects[i] = c.gameObject;
            }

            RectTransform sign = V2Box(seam, "WorldSign", 20f, H(0x7FD3FF), H(0x2F86E8), null, 4f, H(0x103E86), 5f);
            Box(sign, 85f, 1662f - top0, MapSeamView.BannerWidth, MapSeamView.BannerHeight);
            TextMeshProUGUI num = V2Text(sign, "World", "WORLD 2", 11f, true, H(0xD6F0FF));
            num.characterSpacing = 8f;
            Place(num.rectTransform, 0.5f, 0.5f, 0f, 11f, 200f, 14f);
            TextMeshProUGUI name = V2Text(sign, "Name", "Snow", 22f, true, Color.white, blueText);
            Place(name.rectTransform, 0.5f, 0.5f, 0f, -7f, 200f, 26f);
            Button signButton = V2Button(sign);

            // Old lock badge: kept (inactive) so serialized references stay valid; locked nodes carry their own lock now.
            RectTransform badge = V2Box(sign, "Lock", 12f, H(0x24345E), H(0x24345E), null, 2f, Color.white, 0f);
            Place(badge, 1f, 1f, 16f, 16f, 80f, 28f, 1f, 1f);
            TextMeshProUGUI lockText = V2Text(badge, "Text", "Level 11", 12f, true, Color.white, null, TextAlignmentOptions.Left);
            Stretch(lockText.rectTransform, 24f, 0f, 4f, 0f);
            badge.gameObject.SetActive(false);

            MapSeamView view = seam.gameObject.AddComponent<MapSeamView>();
            Wire(view, ("signButton", signButton), ("worldNumber", num), ("worldName", name), ("lockBadge", badge.gameObject),
                ("lockText", lockText), ("clouds", cloudObjects), ("sign", sign));
            seam.gameObject.SetActive(false);
            return view;
        }

        // ------------------------------------------------------------------ level card

        private static LevelStartCard BuildLevelCard(RectTransform root, Sprite star, Sprite coin)
        {
            Color navy = H(0x1B2A5C), navyEdge = H(0x1B4FA0);
            RectTransform layer = Stretch(Rect(root, "LevelCard"));

            Image dim = Img(layer, "Dim", White, new Color(0.04f, 0.08f, 0.22f, 0f));
            Stretch(dim.rectTransform);
            dim.raycastTarget = true;
            Button dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.targetGraphic = dim;

            RectTransform card = Place(Rect(layer, "Card"), 0.5f, 0.5f, 0f, 0f, 300f, 380f);
            CanvasGroup cardGroup = card.gameObject.AddComponent<CanvasGroup>();
            RectTransform body = V2Box(card, "Body", 28f, Color.white, H(0xE3EFFF), null, 4f, navyEdge, 8f, true);
            Stretch(body);

            // World header in the world's colour (tinted at runtime).
            Image header = RoundImg(card, "Header", 18f, LevelStartCard.WorldColors[0]);
            Box(header.rectTransform, 14f, 14f, 272f, 56f);
            TextMeshProUGUI headerText = V2Text(header.transform, "Name", "City", 30f, true, Color.white, LilitaOutline("Blue", H(0x103E86)));
            Place(headerText.rectTransform, 0.5f, 0.5f, 0f, 0f, 260f, 44f);

            // World intro variant: art strip + "Welcome to ... World" (the content below moves down when it shows).
            RectTransform intro = Box(Rect(card, "IntroStrip"), 14f, 80f, 272f, 108f);
            RectTransform strip = Box(Rect(intro, "Mask"), 0f, 0f, 272f, 74f);
            strip.gameObject.AddComponent<RectMask2D>();
            Image artFrame = Img(strip, "Frame", White, navyEdge);
            Stretch(artFrame.rectTransform);
            Image art = Img(strip, "Art", null, Color.white);
            art.preserveAspect = true;
            art.raycastTarget = false;
            RectTransform artRt = art.rectTransform;
            artRt.anchorMin = artRt.anchorMax = new Vector2(0.5f, 0.5f);
            artRt.sizeDelta = new Vector2(340f, 604f);
            artRt.anchoredPosition = new Vector2(0f, -70f);
            Stretch(artFrame.rectTransform, 0f, 0f, 0f, 0f);
            artFrame.transform.SetAsFirstSibling();
            TextMeshProUGUI introText = V2Text(intro, "Text", "Welcome to Snow World", 21f, true, navy);
            Box(introText.rectTransform, 0f, 76f, 272f, 32f);
            intro.gameObject.SetActive(false);

            RectTransform content = Stretch(Rect(card, "Content"));

            TextMeshProUGUI levelText = V2Text(content, "Level", "Level 1", 58f, true, navy);
            Box(levelText.rectTransform, 0f, 80f, 300f, 70f);

            RectTransform boss = V2Box(content, "Boss", 12f, H(0xFF8A8A), H(0xD12A33), null, 2f, H(0x7A1016), 3f);
            Box(boss, 104f, 148f, 92f, 26f);
            TextMeshProUGUI bossText = V2Text(boss, "Text", "BOSS", 15f, true, Color.white);
            Stretch(bossText.rectTransform);

            var stars = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                stars[i] = Img(content, "Star" + i, star, Color.white);
                stars[i].raycastTarget = false;
                Box(stars[i].rectTransform, 69f + i * 58f, 182f, 46f, 46f);
            }

            Image coinImg = Img(content, "Coin", coin, Color.white);
            coinImg.raycastTarget = false;
            Box(coinImg.rectTransform, 98f, 236f, 32f, 34f);
            TextMeshProUGUI rewardText = V2Text(content, "Reward", "+60", 28f, true, H(0x5A3410), null, TextAlignmentOptions.Left);
            Box(rewardText.rectTransform, 136f, 236f, 100f, 36f);

            // Big green Play button: the Home hero button (new button style, shine sweep, sparkles), scaled to fit the card.
            RectTransform playPulse = Box(Rect(content, "Play"), 5f, 276f, LevelW, LevelH);
            playPulse.localScale = Vector3.one * 0.92f;
            var built = BuildLevelButton(playPulse);
            CanvasGroup playGroup = playPulse.gameObject.AddComponent<CanvasGroup>();

            RectTransform close = V2Box(card, "Close", 22f, H(0xFF8A8A), H(0xD12A33), null, 3f, H(0x7A1016), 5f, true);
            Box(close, 264f, -16f, 46f, 50f);
            V2Text(close, "X", "X", 26f, true, Color.white);
            Button closeButton = V2Button(close);

            var view = layer.gameObject.AddComponent<LevelStartCard>();
            Material Mat(string n) => AssetDatabase.LoadAssetAtPath<Material>($"Assets/BusRush/Art/Fonts/LilitaOne UIv2 SDF {n}.mat");
            Wire(view, ("dim", dim), ("dimButton", dimButton), ("card", card), ("cardGroup", cardGroup), ("content", content),
                ("headerImage", header), ("headerText", headerText), ("headerMaterials", new object[] { Mat("Title Blue"), Mat("Title Blue"), Mat("Title Orange") }),
                ("levelText", levelText), ("bossTag", boss.gameObject), ("bossText", bossText), ("stars", stars), ("rewardText", rewardText),
                ("introStrip", intro.gameObject), ("introArt", art), ("introText", introText), ("playButton", built.button),
                ("playLabel", built.text), ("playGroup", playGroup), ("closeButton", closeButton));
            layer.gameObject.SetActive(false);
            return view;
        }
    }
}
