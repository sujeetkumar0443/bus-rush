using System;
using System.Collections.Generic;
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
    /// Step R2: the UI v2 Home screen and the FREE COINS popup (Docs/UI_v2_Design/Home.dc.html). Positions are the
    /// design's px in its 390x844 frame (origin top-left). Also holds the small UI v2 kit (gradient boxes, outlined
    /// Lilita text) used by the v2 screens.
    /// </summary>
    public static partial class UIBuilder
    {
        [MenuItem("Tools/Bus Rush/Build Home (R2)")]
        public static void BuildHomeOnly()
        {
            Load();
            Popup freeCoins = SavePopup(BuildFreeCoins());
            PopupCatalog catalog = AssetDatabase.LoadAssetAtPath<PopupCatalog>(CatalogPath);
            if (catalog == null) throw new InvalidOperationException("PopupCatalog missing: run Build UI first");
            var so = new SerializedObject(catalog);
            SerializedProperty list = so.FindProperty("popups");
            var popups = new List<Object>();
            for (int i = 0; i < list.arraySize; i++)
            {
                Object o = list.GetArrayElementAtIndex(i).objectReferenceValue;
                if (o != null && !(o is FreeCoinsPopup)) popups.Add(o);
            }
            popups.Add(freeCoins);
            Wire(catalog, ("popups", popups.ToArray()));
            EditorUtility.SetDirty(catalog);

            GameObject home = Save(BuildHome(), "HomeScreen");
            GameObject uiManager = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/UIManager.prefab");
            AssetDatabase.SaveAssets();
            WireHome(home, uiManager);
            EditorSceneManager.OpenScene($"{ScenesDir}/Boot.unity", OpenSceneMode.Single);
            Debug.Log("[UIBuilder] Home built.");
        }

        public static void BuildHomeBatch()
        {
            try
            {
                BuildHomeOnly();
            }
            catch (Exception e)
            {
                Debug.LogError("[UIBuilder] FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }

        private static void WireHome(GameObject home, GameObject uiManager)
        {
            Scene homeScene = EditorSceneManager.OpenScene($"{ScenesDir}/Home.unity", OpenSceneMode.Single);
            RemoveRoots(homeScene, "Canvas", "HomeScreen", "UIManager");
            RemoveRoots(homeScene, "RoadMap");
            var homeInstance = (GameObject)PrefabUtility.InstantiatePrefab(home, homeScene);
            PrefabUtility.InstantiatePrefab(uiManager, homeScene);
            GameObject mapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/RoadMap.prefab");
            if (mapPrefab != null)
            {
                var map = (GameObject)PrefabUtility.InstantiatePrefab(mapPrefab, homeScene);
                map.SetActive(false); // opened by the Road Map button / after a win
                Wire(homeInstance.GetComponent<HomeScreen>(), ("roadMap", map.GetComponent<RoadMapScreen>()));
                // Reward screens over the map (Step R9).
                RemoveRoots(homeScene, "GiftChestScreen", "NewWorldScreen");
                GameObject chestPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/GiftChestScreen.prefab");
                GameObject worldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/NewWorldScreen.prefab");
                if (chestPrefab != null && worldPrefab != null)
                {
                    var chest = (GameObject)PrefabUtility.InstantiatePrefab(chestPrefab, homeScene);
                    var world = (GameObject)PrefabUtility.InstantiatePrefab(worldPrefab, homeScene);
                    chest.SetActive(false);
                    world.SetActive(false);
                    Wire(map.GetComponent<RoadMapScreen>(), ("giftChest", chest.GetComponent<GiftChestScreen>()),
                        ("newWorld", world.GetComponent<NewWorldScreen>()));
                }
            }
            FindRoot(homeScene, "Main Camera").GetComponent<Camera>().backgroundColor = UIArtGenerator.Hex(0x4AB3F4);
            EditorSceneManager.SaveScene(homeScene);
        }

        // ------------------------------------------------------------------ UI v2 kit

        private static UiTheme theme;

        private static UiTheme Theme
        {
            get
            {
                if (theme == null) theme = AssetDatabase.LoadAssetAtPath<UiTheme>(UiV2Importer.ThemePath);
                if (theme == null || theme.headingFont == null || theme.bodyFont == null)
                    throw new InvalidOperationException("UiTheme missing: run Import UI v2 Art first");
                return theme;
            }
        }

        private static Color H(int rgb) => UIArtGenerator.Hex((uint)rgb);

        /// <summary>Rounded image (white 9-slice) of the given corner radius.</summary>
        private static Image RoundImg(Transform parent, string name, float radius, Color color)
        {
            Image img = Img(parent, name, Round, color);
            MakeRounded(img, Mathf.Max(1f, radius));
            return img;
        }

        private static void Gradient(Graphic g, Color top, Color bottom, Color? middle = null)
        {
            g.color = Color.white;
            g.gameObject.AddComponent<UiGradient>().Set(top, bottom, middle);
        }

        /// <summary>
        /// CSS box: border-radius, linear-gradient face, solid border, "0 Npx 0 lip" box-shadow and (buttons) the
        /// inset highlights "inset 0 4px 0 rgba(255,255,255,.5), inset 0 -7px 0 rgba(0,0,0,.13)". Children stretch
        /// to the root; size the root afterwards.
        /// </summary>
        private static RectTransform V2Box(Transform parent, string name, float radius, Color top, Color bottom, Color? middle,
                                           float border, Color edge, float lip, bool insets = false)
        {
            RectTransform root = Rect(parent, name);
            if (insets && lip > 0f)
            {
                // Soft drop shadow (CSS 0 12px 18px rgba(10,20,50,.3)) under the 3D bottom edge.
                Image sh = Img(root, "DropShadow", UIKit.Shadow, new Color(10f / 255f, 20f / 255f, 50f / 255f, 0.3f));
                sh.type = Image.Type.Sliced;
                sh.pixelsPerUnitMultiplier = 4f;
                Stretch(sh.rectTransform, -7f, -(lip + 9f), -7f, 1f);
            }
            if (lip > 0f)
            {
                Image l = RoundImg(root, "Lip", radius, edge);
                Stretch(l.rectTransform, 0f, -lip, 0f, lip);
            }
            Image b = RoundImg(root, "Border", radius, edge);
            Stretch(b.rectTransform);
            float r = Mathf.Max(1f, radius - border);
            if (insets)
            {
                Image shade = RoundImg(root, "Shade", r, Color.Lerp(bottom, Color.black, 0.13f));
                Stretch(shade.rectTransform, border, border, border, border);
                Image hi = RoundImg(root, "Highlight", r, Color.Lerp(top, Color.white, 0.5f));
                Stretch(hi.rectTransform, border, border + 7f, border, border);
                Image face = RoundImg(root, "Face", r, Color.white);
                Stretch(face.rectTransform, border, border + 7f, border, border + 4f);
                Gradient(face, Color.Lerp(top, middle ?? top, 0.12f), bottom, middle);
                // White inner highlight on the top half.
                Image gloss = RoundImg(root, "Gloss", r, new Color(1f, 1f, 1f, 0.18f));
                gloss.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                gloss.rectTransform.anchorMax = new Vector2(1f, 1f);
                gloss.rectTransform.offsetMin = new Vector2(border + 3f, 0f);
                gloss.rectTransform.offsetMax = new Vector2(-(border + 3f), -(border + 4f));
            }
            else
            {
                Image face = RoundImg(root, "Face", r, Color.white);
                Stretch(face.rectTransform, border, border, border, border);
                Gradient(face, top, bottom, middle);
            }
            return root;
        }

        /// <summary>Lilita One material with a coloured outline + drop (CSS text-shadow outline), cached per colour.</summary>
        private static Material LilitaOutline(string key, Color edge, float width = 0.6f, float dropY = -1.2f)
        {
            string path = $"{UiV2Importer.FontsDir}/LilitaOne UIv2 SDF {key}.mat";
            TMP_FontAsset font = Theme.headingFont;
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(font.material);
                AssetDatabase.CreateAsset(m, path);
            }
            m.name = System.IO.Path.GetFileNameWithoutExtension(path);
            m.shader = font.material.shader;
            m.CopyPropertiesFromMaterial(font.material);
            m.SetTexture(ShaderUtilities.ID_MainTex, font.atlasTexture);
            m.EnableKeyword(ShaderUtilities.Keyword_Outline);
            m.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
            m.SetFloat(ShaderUtilities.ID_FaceDilate, width * 0.5f); // outline grows outward; the letters keep their size
            m.SetColor(ShaderUtilities.ID_OutlineColor, edge);
            m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            m.SetColor(ShaderUtilities.ID_UnderlayColor, edge);
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, dropY);
            m.SetFloat(ShaderUtilities.ID_UnderlayDilate, width);
            m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static TextMeshProUGUI V2Text(Transform parent, string name, string text, float size, bool heading, Color color,
                                              Material material = null, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            RectTransform rt = Stretch(Rect(parent, name));
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = heading ? Theme.headingFont : Theme.bodyFont;
            if (material != null) t.fontSharedMaterial = material;
            t.text = text;
            t.fontSize = size;
            t.enableAutoSizing = false;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Image with a CSS drop-shadow (a dark silhouette copy behind, offset down).</summary>
        private static Image ShadowedImg(Transform parent, string name, Sprite sprite, float dropY, float alpha)
        {
            RectTransform holder = Rect(parent, name);
            Image shadow = Img(holder, "DropShadow", sprite, new Color(0.04f, 0.1f, 0.3f, alpha));
            Stretch(shadow.rectTransform, 0f, -dropY, 0f, dropY);
            Image img = Img(holder, "Image", sprite, Color.white);
            Stretch(img.rectTransform);
            return img;
        }

        /// <summary>Makes a rect a button with the UI v2 press feel (0.92 squash, click hook, light haptic).</summary>
        private static Button V2Button(RectTransform rt, Transform squash = null)
        {
            Image hit = rt.GetComponent<Image>();
            if (hit == null)
            {
                hit = rt.gameObject.AddComponent<Image>();
                hit.color = new Color(1f, 1f, 1f, 0f);
            }
            hit.raycastTarget = true;
            var button = rt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hit;
            UIButton feel = rt.gameObject.AddComponent<UIButton>();
            Transform target = squash != null ? squash : rt;
            // Chunky buttons (with a 3D bottom edge) also sink onto that edge while pressed.
            Transform lip = target.Find("Lip");
            float depth = lip != null ? Mathf.Min(4f, -((RectTransform)lip).offsetMin.y) : 0f;
            Wire(feel, ("target", target), ("pressedScale", 0.92f), ("lightHaptic", true), ("pushDepth", depth));
            return button;
        }

        // ------------------------------------------------------------------ Home

        private static GameObject BuildHome()
        {
            Color green0 = H(0xA4F560), green1 = H(0x56D134), green2 = H(0x36A920), greenEdge = H(0x1D5F12);
            Color navyEdge = H(0x1B4FA0);
            Material greenText = LilitaOutline("Green", greenEdge);
            Material navyText = LilitaOutline("Navy", navyEdge, 0.9f, -1.6f); // 16 px labels need a wider SDF outline

            var go = new GameObject("HomeScreen", typeof(RectTransform));
            RootCanvas(go, 0);
            go.GetComponent<CanvasScaler>().matchWidthOrHeight = 1f; // the 844 px design always fits vertically
            Transform root = go.transform;

            // Background (cover, full screen).
            RectTransform bgArea = Stretch(Rect(root, "Background"));
            Image backing = bgArea.gameObject.AddComponent<Image>();
            backing.color = H(0x4AB3F4);
            backing.raycastTarget = false;
            Image bg = Img(bgArea, "HomeBg", UiV2Sprite("Backgrounds/home_bg.png"), Color.white);
            AspectRatioFitter cover = bg.gameObject.AddComponent<AspectRatioFitter>();
            cover.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            cover.aspectRatio = 941f / 1672f;

            // Airplane + smoke trail (full screen, top-left based; puffs are pooled at runtime).
            RectTransform sky = Stretch(Rect(root, "Sky"));
            RectTransform trail = Stretch(Rect(sky, "Trail"));
            RectTransform plane = Rect(sky, "Airplane");
            Place(plane, 0f, 1f, 0f, -118f, 124f, 64f, 0f, 1f);
            Image planeImg = Img(plane, "Body", UiV2Sprite("Icons/airplane.png"), Color.white);
            Place(planeImg.rectTransform, 0.5f, 0.5f, 0f, 0f, 124f, 64f);

            // Design frame (bus) and the safe area (top bar, side buttons, Level button).
            RectTransform frame = Place(Rect(root, "Frame"), 0.5f, 0.5f, 0f, 0f, RefWidth, RefHeight);
            RectTransform safe = SafeArea(root);

            // --- Bus box: left 64, top 508, 270x236.
            // R11: on the road right in front of the station: top 440, 280 wide (the 270 px design box scaled up).
            const float busScale = 280f / 270f;
            RectTransform busBox = Box(Rect(frame, "BusBox"), 199f - 135f, 440f, 270f, 236f, 0.5f, 1f);
            busBox.localScale = Vector3.one * busScale;
            Image shadow = Img(busBox, "Shadow", UiV2Sprite("Splash/bus_shadow.png"), new Color(1f, 1f, 1f, 0.86f));
            Box(shadow.rectTransform, 16f, 196f, 240f, 34f);
            var exhaust = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                exhaust[i] = Img(busBox, "Exhaust" + i, null, new Color(1f, 1f, 1f, 0f));
                Box(exhaust[i].rectTransform, -4f, 150f, 26f, 26f);
            }
            Image bus = Img(busBox, "Bus", UiV2Sprite("Characters/bus_passengers.png"), Color.white);
            Box(bus.rectTransform, 0f, 0f, 270f, 227f, 0.5f, 0f); // origin 50% 100%

            // --- Coin pill (top-left): coin 44x47, pill 36 high (radius 18), green "+" 34x34.
            RectTransform topLeft = Place(Rect(safe, "CoinPill"), 0f, 1f, 0f, 0f, 200f, 70f, 0f, 1f);
            RectTransform pill = V2Box(topLeft, "Pill", 18f, Color.white, H(0xFFF1CF), null, 3f, H(0x7A4A16), 0f);
            Place(pill, 0f, 1f, 36f, -23f, 113f, 36f, 0f, 1f);
            TextMeshProUGUI coinText = V2Text(pill, "Count", "300", 21f, true, H(0x5A3410), null, TextAlignmentOptions.Left);
            Stretch(coinText.rectTransform, 33f, 0f, 10f, 2f);
            coinText.gameObject.AddComponent<CoinText>();
            Wire(pill.gameObject.AddComponent<UiFitWidthToText>(), ("text", coinText), ("padding", 80f), ("minWidth", 100f));
            Image coin = ShadowedImg(topLeft, "Coin", UiV2Sprite("Icons/coin.png"), 3f, 0.25f);
            Box((RectTransform)coin.transform.parent, 14f, 17.5f, 44f, 47f);
            Wire(coinText.GetComponent<CoinText>(), ("punchTarget", coin.transform.parent));
            RectTransform plusRt = V2Box(pill, "AddCoinsButton", 12f, green0, H(0x45C02A), null, 3f, greenEdge, 3f, true);
            Place(plusRt, 1f, 0.5f, -4f, 0f, 34f, 34f, 1f, 0.5f);
            V2Text(plusRt, "Label", "+", 24f, true, Color.white, greenText);
            Button plus = V2Button(plusRt);

            // --- Settings gear (top-right): right 14, top 12, 54x56.
            RectTransform gearRt = Place(Rect(safe, "SettingsButton"), 1f, 1f, -14f, -12f, 56f, 56f, 1f, 1f);
            Image gear = ShadowedImg(gearRt, "Gear", UiV2Sprite("Icons/settings.png"), 4f, 0.35f);
            Place((RectTransform)gear.transform.parent, 0f, 1f, 0f, 0f, 54f, 56f, 0f, 1f);
            Button settings = V2Button(gearRt);

            // --- Side buttons: 76x76 rounded white frames (radius 26, border 3 + 5 px lip #1B4FA0), label 16 below.
            (RectTransform wrap, Button button, RectTransform frameBox) Side(string name, string label, string icon, Vector2 iconSize,
                                                                             bool right, float top)
            {
                RectTransform wrap = Place(Rect(safe, name), right ? 1f : 0f, 1f, right ? -12f : 12f, -top, 78f, 106f,
                                           right ? 1f : 0f, 1f);
                RectTransform box = V2Box(wrap, "Frame", 26f, Color.white, H(0xDDF1FF), null, 3f, navyEdge, 5f, true);
                Place(box, 0.5f, 1f, 0f, 0f, 76f, 76f, 0.5f, 1f);
                Image ic = Img(box, "Icon", UiV2Sprite(icon), Color.white);
                Place(ic.rectTransform, 0.5f, 0.5f, 0f, 0f, iconSize.x, iconSize.y);
                TextMeshProUGUI t = V2Text(wrap, "Label", label, 16f, true, Color.white, navyText);
                Place(t.rectTransform, 0.5f, 1f, 0f, -82f, 110f, 20f, 0.5f, 1f);
                Button b = V2Button(wrap, box);
                return (wrap, b, box);
            }

            var daily = Side("DailyButton", "Daily", "Icons/daily.png", new Vector2(52f, 59f), false, 96f);
            Image dotBorder = Img(daily.frameBox, "Dot", Circle, Color.white);
            Place(dotBorder.rectTransform, 1f, 1f, 6f, 6f, 20f, 20f, 1f, 1f);
            Image dotFace = Img(dotBorder.transform, "Face", Circle, H(0xFF3B47));
            Stretch(dotFace.rectTransform, 3f, 3f, 3f, 3f);
            var roadMap = Side("RoadMapButton", "Road Map", "Icons/map.png", new Vector2(56f, 54f), false, 210f);
            var lucky = Side("LuckySpinButton", "Lucky Spin", "Icons/lucky_spin.png", new Vector2(50f, 59f), true, 96f);
            Image video = Img(lucky.frameBox, "VideoBadge", UiV2Sprite("Icons/video.png"), Color.white);
            Place(video.rectTransform, 1f, 0f, 10f, -8f, 30f, 25f, 1f, 0f);

            // --- Level button: left 50, top 752, 290x74, radius 26, pulsing; bottom-anchored in the safe area.
            // R11: top 700 of 844 = 70 above the bottom of the safe area (always >= 28 px clear of it).
            const float levelTop = 700f, minAboveSafeBottom = 28f;
            float levelBottom = Mathf.Max(minAboveSafeBottom, RefHeight - levelTop - 74f);
            RectTransform pulse = Place(Rect(safe, "LevelPulse"), 0.5f, 0f, 0f, levelBottom + 37f, 290f, 74f, 0.5f, 0.5f);
            RectTransform levelRt = V2Box(pulse, "LevelButton", 26f, green0, green2, green1, 3f, greenEdge, 6f, true);
            Stretch(levelRt);
            TextMeshProUGUI levelText = V2Text(levelRt, "Label", "Level 1", 36f, true, Color.white, greenText);
            Stretch(levelText.rectTransform, 18f, 6f, 18f, 2f);
            Button level = V2Button(levelRt);

            // --- Toast ("Coming soon").
            RectTransform toast = V2Box(safe, "Toast", 23f, H(0x24345E), H(0x14204F), null, 3f, Color.white, 0f);
            Place(toast, 0.5f, 0.5f, 0f, 40f, 240f, 46f);
            toast.gameObject.AddComponent<CanvasGroup>();
            TextMeshProUGUI toastLabel = V2Text(toast, "Label", "", 20f, false, Color.white);
            Wire(toast.gameObject.AddComponent<Toast>(), ("label", toastLabel));

            HomeScreen screen = go.AddComponent<HomeScreen>();
            Wire(screen, ("levelButton", level), ("levelText", levelText), ("addCoinsButton", plus),
                ("coinIcon", (RectTransform)coin.transform.parent), ("settingsButton", settings),
                ("dailyButton", daily.button), ("dailyDot", dotBorder.gameObject), ("roadMapButton", roadMap.button),
                ("luckySpinButton", lucky.button));

            HomeAmbience ambience = go.AddComponent<HomeAmbience>();
            Wire(ambience, ("plane", plane), ("planeBody", planeImg.rectTransform), ("trailRoot", trail),
                ("bus", bus.rectTransform), ("exhaust", exhaust),
                ("floaters", new object[] { daily.wrap, roadMap.wrap, lucky.wrap }), ("pulse", pulse),
                ("dot", dotBorder.rectTransform));
            Wire(ambience, ("floatDelays", new object[] { 0f, 0.5f, 0.25f }));
            return go;
        }

        // ------------------------------------------------------------------ FREE COINS popup

        private static GameObject BuildFreeCoins()
        {
            const float w = 318f, h = 397f;
            Color greenEdge = H(0x1D5F12);

            var root = new GameObject("FreeCoinsPopup", typeof(RectTransform));
            root.layer = 5;
            Stretch((RectTransform)root.transform);
            root.AddComponent<CanvasGroup>();
            FreeCoinsPopup popup = root.AddComponent<FreeCoinsPopup>();

            Image dim = Img(root.transform, "Dim", White, H(0x0A1437));
            dim.raycastTarget = true;
            Stretch(dim.rectTransform);
            Button dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;

            RectTransform safe = SafeArea(root.transform);
            RectTransform panel = Rect(safe, "Panel");
            Place(panel, 0.5f, 0.5f, 0f, 0f, w, h);

            RectTransform card = V2Box(panel, "Card", 30f, Color.white, H(0xE6F2FF), null, 4f, H(0x1B4FA0), 8f);
            Stretch(card);

            // Ribbon: 250x56 at (34, -26) of the padding box.
            RectTransform ribbon = V2Box(panel, "Ribbon", 18f, H(0x7FD3FF), H(0x2F86E8), null, 3f, H(0x103E86), 5f);
            Box(ribbon, 38f, -22f, 250f, 56f);
            V2Text(ribbon, "Title", "FREE COINS", 28f, true, Color.white, LilitaOutline("Blue", H(0x103E86)));

            // Close X: 44 circle at right -14, top -14 of the padding box.
            RectTransform close = V2Box(panel, "CloseButton", 22f, H(0xFF8A8A), H(0xD12A33), null, 3f, H(0x7A1016), 4f, true);
            Box(close, w - 4f + 14f - 44f, -10f, 44f, 44f);
            V2Text(close, "X", "X", 22f, true, Color.white);
            Button closeButton = V2Button(close);

            // Column: coin 96x102, "+50", text, Watch button (gap 12, padding 64 / 22 / 24).
            Image coin = ShadowedImg(panel, "Coin", UiV2Sprite("Icons/coin.png"), 6f, 0.25f);
            Box((RectTransform)coin.transform.parent, (w - 96f) / 2f, 68f, 96f, 102f);
            TextMeshProUGUI amount = V2Text(panel, "Amount", "+50", 44f, true, H(0xFFC531), LilitaOutline("Brown", H(0x7A4A16), 0.55f, -1.4f));
            Box(amount.rectTransform, 0f, 182f, w, 53f);
            TextMeshProUGUI body = V2Text(panel, "Body", "Watch a short video to get 50 coins.", 17f, false, H(0x24345E));
            body.textWrappingMode = TextWrappingModes.Normal;
            body.fontStyle = FontStyles.Bold;
            Box(body.rectTransform, 26f, 247f, w - 52f, 42f);

            RectTransform watchRt = V2Box(panel, "WatchButton", 22f, H(0xA4F560), H(0x36A920), H(0x56D134), 3f, greenEdge, 6f, true);
            Box(watchRt, (w - 250f) / 2f, 307f, 250f, 62f);
            Image vid = Img(watchRt, "Video", UiV2Sprite("Icons/video.png"), Color.white);
            Place(vid.rectTransform, 0.5f, 0.5f, -40f, 2f, 36f, 30f);
            TextMeshProUGUI watchLabel = V2Text(watchRt, "Label", "Watch", 26f, true, Color.white, LilitaOutline("Green", greenEdge));
            Place(watchLabel.rectTransform, 0.5f, 0.5f, 23f, 2f, 120f, 40f);
            Button watch = V2Button(watchRt);

            Wire(popup, ("dim", dim), ("panel", panel), ("closeButton", closeButton), ("dimAlpha", 0.62f),
                ("watchButton", watch), ("coinImage", coin), ("reward", 50));
            return root;
        }
    }
}
