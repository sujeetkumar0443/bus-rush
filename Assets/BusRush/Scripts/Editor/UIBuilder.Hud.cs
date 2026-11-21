using System;
using BusRush.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static BusRush.EditorTools.UIKit;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Step R5: the UI v2 gameplay HUD (Docs/UI_v2_Design/HUD.dc.html) and Booster popup (BoosterPopup.dc.html).
    /// Design px in the 390x844 frame; the top bar and booster bar sit in the safe area.
    /// </summary>
    public static partial class UIBuilder
    {
        [MenuItem("Tools/Bus Rush/Build HUD + Booster Popup (R5)")]
        public static void BuildHudOnly()
        {
            Load();
            SavePopup(BuildBoosterPopup()); // same prefab path: the popup catalog keeps pointing at it
            CleanCatalog();
            GameObject hud = Save(BuildHud(), "GameHUD");
            AssetDatabase.SaveAssets();
            WireGame(hud, AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/UIManager.prefab"));
            EditorSceneManager.OpenScene($"{ScenesDir}/Boot.unity", OpenSceneMode.Single);
            Debug.Log("[UIBuilder] HUD + Booster popup built.");
        }

        public static void BuildHudBatch()
        {
            try
            {
                BuildHudOnly();
            }
            catch (Exception e)
            {
                Debug.LogError("[UIBuilder] FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Booster popup rays: repeating-conic-gradient(rgba(255,230,120,.35) 0-10deg, clear 10-24deg), mask 20% -> 68%.</summary>
        private static Sprite BoosterRaysSprite() => GeneratedSprite("booster_rays", 256, (u, v) =>
        {
            Vector2 d = new Vector2(u - 0.5f, v - 0.5f);
            float r = d.magnitude / 0.7071f;
            float mask = 1f - Mathf.InverseLerp(0.2f, 0.68f, r);
            float deg = (Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg + 360f) % 24f;
            float soft = Mathf.Clamp(57.3f * (1f / 256f) / Mathf.Max(d.magnitude, 0.01f), 0.1f, 3f);
            float wedge = Mathf.Clamp01((deg + soft * 0.5f) / soft) * Mathf.Clamp01((10f - deg + soft * 0.5f) / soft);
            return new Color(1f, 230f / 255f, 120f / 255f, 0.35f * wedge * mask);
        });

        /// <summary>Coin pill anchored top-right of the safe area (HUD and Level Complete share the spot).</summary>
        private static RectTransform TopRightCoinPill(RectTransform safe)
        {
            RectTransform pill = V2Box(safe, "CoinPill", 17f, Color.white, H(0xFFF1CF), null, 3f, H(0x7A4A16), 0f);
            // Design: coin at x 300, pill padding 0 10 0 26 (+3 border); kept clear of the 200 px level badge.
            Place(pill, 1f, 1f, -4f, -27f, 74f, 34f, 1f, 1f);
            TextMeshProUGUI coinText = V2Text(pill, "Count", "300", 20f, true, H(0x5A3410), null, TextAlignmentOptions.Left);
            Stretch(coinText.rectTransform, 29f, 0f, 6f, 2f);
            coinText.gameObject.AddComponent<CoinText>();
            Wire(pill.gameObject.AddComponent<UiFitWidthToText>(), ("text", coinText), ("padding", 42f), ("minWidth", 60f));
            Image coin = ShadowedImg(pill, "Coin", UiV2Sprite("Icons/coin.png"), 3f, 0.25f);
            Place((RectTransform)coin.transform.parent, 0f, 0.5f, 0f, 0f, 44f, 47f);
            Wire(coinText.GetComponent<CoinText>(), ("punchTarget", coin.transform.parent));
            return (RectTransform)coin.transform.parent;
        }

        // ------------------------------------------------------------------ HUD

        private static GameObject BuildHud()
        {
            Color blueTop = H(0x7FD3FF), blueBottom = H(0x2F86E8), blueEdge = H(0x103E86), navyEdge = H(0x1B4FA0);
            Material blueText = LilitaOutline("Blue", blueEdge);

            var go = new GameObject("GameHUD", typeof(RectTransform));
            RootCanvas(go, 10);

            // Wrong-tap flash covers the whole screen (outside the safe area too), behind the HUD controls.
            Image flash = Img(go.transform, "WrongTapFlash", Vignette, new Color(1f, 0.1f, 0.1f, 0f));
            Stretch(flash.rectTransform);
            flash.gameObject.AddComponent<ScreenFlash>();

            // Top gradient band (96 px, rgba(20,40,90,.45) -> 0).
            Image band = Img(go.transform, "TopBand", White, Color.white);
            band.rectTransform.anchorMin = new Vector2(0f, 1f);
            band.rectTransform.anchorMax = new Vector2(1f, 1f);
            band.rectTransform.pivot = new Vector2(0.5f, 1f);
            band.rectTransform.sizeDelta = new Vector2(0f, 96f);
            band.rectTransform.anchoredPosition = Vector2.zero;
            Gradient(band, new Color(20f / 255f, 40f / 255f, 90f / 255f, 0.45f), new Color(20f / 255f, 40f / 255f, 90f / 255f, 0f));

            // Locked-bay tags follow the bays on screen (full-screen layer, not the safe area).
            RectTransform bayLocks = Stretch(Rect(go.transform, "BayLocks"));
            Button tagTemplate = BuildBayLockTag(bayLocks);
            Wire(bayLocks.gameObject.AddComponent<BayLockOverlay>(), ("tagTemplate", tagTemplate));

            RectTransform safe = SafeArea(go.transform);

            // Pause (12,14) 56x56: blue, two white bars.
            RectTransform pauseRt = V2Box(safe, "PauseButton", 20f, blueTop, blueBottom, null, 3f, blueEdge, 4f);
            Place(pauseRt, 0f, 1f, 12f, -14f, 56f, 56f, 0f, 1f);
            for (int i = 0; i < 2; i++)
            {
                Image bar = RoundImg(pauseRt, "Bar" + i, 3f, Color.white);
                Place(bar.rectTransform, 0.5f, 0.5f, i == 0 ? -7f : 7f, 0f, 8f, 22f);
            }
            Button pause = V2Button(pauseRt);

            // Level badge (95,12) 200x66: "Level N" + passengers bar 160x18.
            RectTransform badge = V2Box(safe, "LevelBadge", 20f, blueTop, blueBottom, null, 3f, blueEdge, 4f);
            Place(badge, 0.5f, 1f, 0f, -12f, 200f, 66f, 0.5f, 1f);
            TextMeshProUGUI level = V2Text(badge, "Level", "Level 1", 22f, true, Color.white, blueText);
            Place(level.rectTransform, 0.5f, 0.5f, 0f, 11f, 190f, 26f);
            Image barBorder = RoundImg(badge, "Progress", 9f, Color.white);
            Place(barBorder.rectTransform, 0.5f, 0.5f, 0f, -13f, 160f, 18f);
            Image barBg = RoundImg(barBorder.transform, "Track", 7f, H(0x0E2E66));
            Stretch(barBg.rectTransform, 2f, 2f, 2f, 2f);
            Image fill = RoundImg(barBg.transform, "Fill", 7f, Color.white);
            RectTransform fillRt = fill.rectTransform;
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.anchoredPosition = Vector2.zero;
            fillRt.sizeDelta = new Vector2(0f, 0f);
            Gradient(fill, H(0xA4F560), H(0x45C02A));
            TextMeshProUGUI progress = V2Text(barBorder.transform, "Count", "0 / 0", 11f, false, Color.white);
            progress.fontStyle = FontStyles.Bold;

            TopRightCoinPill(safe);

            // Booster bar: 96x84 tiles at x 28 / 147 / 266, top 748 (12 above the bottom).
            RectTransform row = Rect(safe, "BoosterRow");
            Place(row, 0.5f, 0f, 0f, 12f, 334f, 84f, 0.5f, 0f);
            (BoosterType type, string label, string icon, Vector2 size)[] tiles =
            {
                (BoosterType.Vip, "VIP", "Boosters/vip.png", new Vector2(54f, 47f)),
                (BoosterType.Arrange, "Arrange", "Boosters/arrange.png", new Vector2(46f, 55f)),
                (BoosterType.Shuffle, "Shuffle", "Boosters/shuffle.png", new Vector2(58f, 44f))
            };
            for (int i = 0; i < tiles.Length; i++) BoosterTile(row, tiles[i].type, tiles[i].label, tiles[i].icon, tiles[i].size, (i - 1) * 119f, navyEdge);

            BuildDebugPanel(safe);
            LevelBanner banner = BuildLevelBanner(safe);
            BuildHints(go.transform);

            // Toast ("No free bay!").
            RectTransform toast = V2Box(safe, "Toast", 23f, H(0x24345E), H(0x14204F), null, 3f, Color.white, 0f);
            Place(toast, 0.5f, 1f, 0f, -230f, 250f, 46f, 0.5f, 1f);
            toast.gameObject.AddComponent<CanvasGroup>();
            TextMeshProUGUI toastLabel = V2Text(toast, "Label", "No free bay!", 20f, false, Color.white);
            Wire(toast.gameObject.AddComponent<Toast>(), ("label", toastLabel));

            GameHUD hud = go.AddComponent<GameHUD>();
            Wire(hud, ("pauseButton", pause), ("levelText", level), ("progressFill", fillRt), ("progressText", progress),
                ("banner", banner));
            return go;
        }

        /// <summary>Locked bay tag 40x66: lock 26x32 over a navy pill with gold border, coin 14x15 + price.</summary>
        private static Button BuildBayLockTag(RectTransform parent)
        {
            RectTransform tag = Place(Rect(parent, "BayLockTag"), 0.5f, 0.5f, 0f, 0f, 44f, 66f);
            Image hit = tag.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            Image lockIcon = Img(tag, "Lock", UiV2Sprite("Map/lock.png"), Color.white);
            lockIcon.preserveAspect = true;
            Box(lockIcon.rectTransform, 9f, 0f, 26f, 32f);
            RectTransform pill = V2Box(tag, "Price", 10f, H(0x24345E), H(0x24345E), null, 2f, H(0xFFE27A), 0f);
            Box(pill, 0f, 36f, 44f, 20f);
            Image coin = Img(pill, "Coin", UiV2Sprite("Icons/coin.png"), Color.white);
            Place(coin.rectTransform, 0f, 0.5f, 3f, 0f, 14f, 15f, 0f, 0.5f);
            TextMeshProUGUI price = V2Text(pill, "Text", "200", 11f, true, H(0xFFE27A), null, TextAlignmentOptions.Left);
            Stretch(price.rectTransform, 18f, 0f, 3f, 1f);
            Button b = V2Button(tag);
            tag.gameObject.SetActive(false);
            return b;
        }

        private static void BoosterTile(RectTransform row, BoosterType type, string label, string iconPath, Vector2 iconSize, float x,
                                        Color navyEdge)
        {
            RectTransform tile = Place(Rect(row, label + "Booster"), 0.5f, 0f, x, 0f, 96f, 84f, 0.5f, 0f);
            RectTransform unlocked = V2Box(tile, "Tile", 24f, Color.white, H(0xDDF1FF), null, 3f, navyEdge, 5f);
            Stretch(unlocked);
            RectTransform locked = V2Box(tile, "LockedTile", 24f, H(0xEEF1F6), H(0xC9D1DD), null, 3f, H(0x8F9AB2), 5f);
            Stretch(locked);
            Image icon = Img(tile, "Icon", UiV2Sprite(iconPath), Color.white);
            icon.preserveAspect = true;
            Place(icon.rectTransform, 0.5f, 0.5f, 0f, 7f, iconSize.x, iconSize.y);
            TextMeshProUGUI name = V2Text(tile, "Name", label, 13f, true, navyEdge);
            Place(name.rectTransform, 0.5f, 0f, 0f, 4f, 92f, 16f, 0.5f, 0f);

            RectTransform Badge(string n, Color face, string text, float w)
            {
                RectTransform b = V2Box(tile, n, 14f, face, face, null, 3f, Color.white, 0f);
                Place(b, 1f, 1f, 8f, 10f, w, 28f, 1f, 1f);
                V2Text(b, "Text", text, 15f, true, Color.white);
                return b;
            }

            RectTransform count = Badge("CountBadge", H(0xFF3B47), "2", 28f);
            RectTransform plus = Badge("PlusBadge", H(0x3DB52A), "+", 28f);
            RectTransform lockTag = V2Box(tile, "LockTag", 10f, H(0x24345E), H(0x24345E), null, 2f, Color.white, 0f);
            Place(lockTag, 1f, 1f, 8f, 10f, 48f, 22f, 1f, 1f);
            TextMeshProUGUI lockText = V2Text(lockTag, "Text", "LV 13", 12f, true, Color.white);

            Button button = V2Button(tile);
            BoosterButtonView view = tile.gameObject.AddComponent<BoosterButtonView>();
            Wire(view, ("type", type), ("button", button), ("countBadge", count.gameObject),
                ("countText", count.GetComponentInChildren<TextMeshProUGUI>()), ("plusBadge", plus.gameObject),
                ("lockOverlay", lockTag.gameObject), ("lockText", lockText), ("nameLabel", name.gameObject),
                ("unlockedTile", unlocked.gameObject), ("lockedTile", locked.gameObject), ("icon", icon));
        }

        // ------------------------------------------------------------------ Booster popup

        private static GameObject BuildBoosterPopup()
        {
            const float w = 342f, h = 452f, b = 4f;
            Color greenEdge = H(0x1D5F12), orangeEdge = H(0x8A3E05);

            var root = new GameObject("BoosterPopup", typeof(RectTransform));
            root.layer = 5;
            Stretch((RectTransform)root.transform);
            root.AddComponent<CanvasGroup>();
            BoosterPopup popup = root.AddComponent<BoosterPopup>();

            RectTransform blurRt = Stretch(Rect(root.transform, "Blur"), -10f, -10f, -10f, -10f);
            RawImage blur = blurRt.gameObject.AddComponent<RawImage>();
            blur.raycastTarget = false;
            blur.enabled = false;
            Image dim = Img(root.transform, "Dim", White, new Color(12f / 255f, 20f / 255f, 62f / 255f, 1f));
            dim.raycastTarget = true;
            Stretch(dim.rectTransform);
            Button dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;

            RectTransform safe = SafeArea(root.transform);
            RectTransform panel = Rect(safe, "Panel");
            Place(panel, 0.5f, 0.5f, 0f, -10f, w, h);
            RectTransform card = V2Box(panel, "Card", 32f, Color.white, H(0xE3EFFF), null, b, H(0x1B4FA0), 8f);
            Stretch(card);

            Image rays = Img(panel, "Rays", BoosterRaysSprite(), Color.white);
            Box(rays.rectTransform, b + 61f, b + 10f, 220f, 220f);
            RectTransform iconFloat = Box(Rect(panel, "IconFloat"), b + 106f, b + 66f, 130f, 98f);
            Image icon = Img(iconFloat, "Icon", UiV2Sprite("Boosters/shuffle.png"), Color.white);
            icon.preserveAspect = true;
            Stretch(icon.rectTransform);

            TextMeshProUGUI desc = V2Text(panel, "Description", "", 18f, false, H(0x24345E));
            desc.textWrappingMode = TextWrappingModes.Normal;
            desc.fontStyle = FontStyles.Bold;
            desc.alignment = TextAlignmentOptions.Top;
            Box(desc.rectTransform, b + 24f, b + 186f, 286f, 50f);

            Image owned = RoundImg(panel, "Owned", 15f, H(0xE3ECF8));
            Box(owned.rectTransform, b + 111f, b + 244f, 112f, 30f);
            TextMeshProUGUI ownedText = V2Text(owned.transform, "Text", "You have 0", 14f, false, H(0x5A6E9C));
            ownedText.fontStyle = FontStyles.Bold;

            // Free +1 (green, pulse, shine).
            RectTransform freeRoot = Box(Rect(panel, "Free"), b + 24f, b + 292f, 286f, 64f);
            RectTransform freeBox = V2Box(freeRoot, "Button", 22f, H(0xA4F560), H(0x36A920), H(0x56D134), 3f, greenEdge, 6f, true);
            Stretch(freeBox);
            RectTransform shineMask = Stretch(Rect(freeBox, "ShineMask"), 3f, 3f, 3f, 3f);
            shineMask.gameObject.AddComponent<RectMask2D>();
            Image shine = Img(shineMask, "Shine", null, new Color(1f, 1f, 1f, 0.35f));
            RectTransform shineRt = shine.rectTransform;
            shineRt.anchorMin = new Vector2(0f, 0f);
            shineRt.anchorMax = new Vector2(0f, 1f);
            shineRt.pivot = new Vector2(0f, 0.5f);
            shineRt.sizeDelta = new Vector2(40f, 0f);
            shineRt.anchoredPosition = new Vector2(-80f, 0f);
            shine.gameObject.AddComponent<UiSkew>();
            Image video = Img(freeBox, "Video", UiV2Sprite("Icons/video.png"), Color.white);
            Place(video.rectTransform, 0.5f, 0.5f, -50f, 2f, 38f, 31f);
            V2Text(freeBox, "Label", "Free +1", 26f, true, Color.white, LilitaOutline("Green", greenEdge)).rectTransform.anchoredPosition = new Vector2(20f, 2f);
            Button free = V2Button(freeBox);

            // Buy 1 [coin] price (orange).
            RectTransform buyRoot = Box(Rect(panel, "Buy"), b + 24f, b + 370f, 286f, 56f);
            CanvasGroup buyGroup = buyRoot.gameObject.AddComponent<CanvasGroup>();
            RectTransform buyBox = V2Box(buyRoot, "Button", 20f, H(0xFFD36B), H(0xF07F12), H(0xFFA22E), 3f, orangeEdge, 6f, true);
            Stretch(buyBox);
            Material orangeText = LilitaOutline("Orange", orangeEdge);
            TextMeshProUGUI buyLabel = V2Text(buyBox, "Label", "Buy 1", 24f, true, Color.white, orangeText);
            Place(buyLabel.rectTransform, 0.5f, 0.5f, -36f, 2f, 90f, 34f);
            Image buyCoin = Img(buyBox, "Coin", UiV2Sprite("Icons/coin.png"), Color.white);
            Place(buyCoin.rectTransform, 0.5f, 0.5f, 22f, 2f, 28f, 30f);
            TextMeshProUGUI price = V2Text(buyBox, "Price", "100", 24f, true, Color.white, orangeText, TextAlignmentOptions.Left);
            Place(price.rectTransform, 0.5f, 0.5f, 40f, 2f, 80f, 34f, 0f, 0.5f);
            Button buy = V2Button(buyBox);

            // Ribbon (56,-30) 230x60 over the panel top; colour per booster at runtime.
            RectTransform ribbon = Box(Rect(panel, "Ribbon"), 56f, -30f, 230f, 60f);
            CanvasGroup ribbonGroup = ribbon.gameObject.AddComponent<CanvasGroup>();
            RectTransform ribbonBox = V2Box(ribbon, "Box", 20f, H(0xD7A6FF), H(0x8A3FE0), null, 4f, H(0x4A1690), 5f);
            Stretch(ribbonBox);
            Image ribbonFace = ribbonBox.Find("Face").GetComponent<Image>();
            TextMeshProUGUI title = V2Text(ribbonBox, "Title", "SHUFFLE", 30f, true, Color.white, LilitaOutline("Purple", H(0x4A1690)));

            // Close X (308,-18) 46.
            RectTransform close = V2Box(panel, "CloseButton", 23f, H(0xFF8A8A), H(0xD12A33), null, 3f, H(0x7A1016), 4f);
            Box(close, 308f, -18f, 46f, 46f);
            for (int i = 0; i < 2; i++)
            {
                Image bar = RoundImg(close, "X" + i, 1.7f, Color.white);
                Place(bar.rectTransform, 0.5f, 0.5f, 0f, 0f, 20f, 3.5f);
                bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 45f : -45f);
            }
            Button closeButton = V2Button(close);

            (string title, string desc, string icon, uint top, uint bottom, uint edge, string mat)[] styles =
            {
                ("VIP", "Opens the golden VIP bay for this level.", "Boosters/vip.png", 0xFFE27A, 0xF2A20C, 0x8A4A05, "DarkBrown"),
                ("ARRANGE", "Lines up the passengers by colour for the parked cars.", "Boosters/arrange.png", 0x7FD3FF, 0x2F86E8, 0x103E86, "Blue"),
                ("SHUFFLE", "Mixes the colours of the cars still in the parking lot.", "Boosters/shuffle.png", 0xD7A6FF, 0x8A3FE0, 0x4A1690, "Purple"),
                ("EXTRA BAY", "Opens one more parking bay for this level.", "Boosters/extra_bay.png", 0xFFD36B, 0xF07F12, 0x8A3E05, "Orange")
            };
            Wire(popup, ("dim", dim), ("panel", panel), ("closeButton", closeButton), ("dimAlpha", 0.66f),
                ("blur", blur), ("ribbon", ribbon), ("ribbonGroup", ribbonGroup), ("ribbonFace", ribbonFace),
                ("ribbonGradient", ribbonFace.GetComponent<UiGradient>()),
                ("ribbonEdges", new object[] { ribbonBox.Find("Lip").GetComponent<Image>(), ribbonBox.Find("Border").GetComponent<Image>() }),
                ("titleText", title), ("rays", rays.rectTransform), ("iconFloat", iconFloat), ("icon", icon),
                ("descriptionText", desc), ("ownedPill", owned.gameObject), ("ownedText", ownedText), ("freeRoot", freeRoot),
                ("freeButton", free), ("freeShine", shineRt), ("buyButton", buy), ("buyGroup", buyGroup), ("buyPrice", price),
                ("styles", new object[styles.Length]));
            for (int i = 0; i < styles.Length; i++)
            {
                string p = $"styles.Array.data[{i}].";
                Wire(popup, (p + "title", styles[i].title), (p + "description", styles[i].desc), (p + "icon", UiV2Sprite(styles[i].icon)),
                    (p + "top", UIArtGenerator.Hex(styles[i].top)), (p + "bottom", UIArtGenerator.Hex(styles[i].bottom)),
                    (p + "edge", UIArtGenerator.Hex(styles[i].edge)),
                    (p + "titleMaterial", LilitaOutline(styles[i].mat, UIArtGenerator.Hex(styles[i].edge))));
            }
            return root;
        }
    }
}
