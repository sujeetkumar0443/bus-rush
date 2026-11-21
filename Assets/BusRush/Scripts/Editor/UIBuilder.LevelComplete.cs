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
    /// Step R4: the UI v2 Level Complete screen (Docs/UI_v2_Design/LevelComplete.dc.html) as the
    /// "LevelCompleteScreen" prefab, an inactive full-screen overlay in the Game scene. Design px, 390x844 frame.
    /// </summary>
    public static partial class UIBuilder
    {
        [MenuItem("Tools/Bus Rush/Build Level Complete (R4)")]
        public static void BuildLevelCompleteOnly()
        {
            Load();
            Save(BuildLevelCompleteScreen(), "LevelCompleteScreen");
            CleanCatalog();
            AssetDatabase.SaveAssets();
            WireGame(AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/GameHUD.prefab"),
                     AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/UIManager.prefab"));
            EditorSceneManager.OpenScene($"{ScenesDir}/Boot.unity", OpenSceneMode.Single);
            Debug.Log("[UIBuilder] Level Complete built.");
        }

        public static void BuildLevelCompleteBatch()
        {
            try
            {
                BuildLevelCompleteOnly();
            }
            catch (Exception e)
            {
                Debug.LogError("[UIBuilder] FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Drops missing popups (deleted prefabs) from the popup catalog.</summary>
        private static void CleanCatalog()
        {
            PopupCatalog catalog = AssetDatabase.LoadAssetAtPath<PopupCatalog>(CatalogPath);
            if (catalog == null) return;
            var so = new SerializedObject(catalog);
            SerializedProperty list = so.FindProperty("popups");
            var keep = new List<Object>();
            for (int i = 0; i < list.arraySize; i++)
            {
                Object o = list.GetArrayElementAtIndex(i).objectReferenceValue;
                if (o != null) keep.Add(o);
            }
            Wire(catalog, ("popups", keep.ToArray()));
            EditorUtility.SetDirty(catalog);
        }

        private static void WireGame(GameObject hud, GameObject uiManager)
        {
            Scene game = EditorSceneManager.OpenScene($"{ScenesDir}/Game.unity", OpenSceneMode.Single);
            RemoveRoots(game, "Canvas", "GameHUD", "UIManager", "LevelCompleteScreen");
            PrefabUtility.InstantiatePrefab(hud, game);
            PrefabUtility.InstantiatePrefab(uiManager, game);
            GameObject lc = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/LevelCompleteScreen.prefab");
            if (lc != null) ((GameObject)PrefabUtility.InstantiatePrefab(lc, game)).SetActive(false);
            EditorSceneManager.SaveScene(game);
        }

        /// <summary>repeating-conic-gradient(rgba(255,214,90,.22) 0-10deg, clear 10-22deg), mask 18% -> 52%.</summary>
        private static Sprite WinRaysSprite() => GeneratedSprite("win_rays", 512, (u, v) =>
        {
            Vector2 d = new Vector2(u - 0.5f, v - 0.5f);
            float r = d.magnitude / 0.7071f;
            float mask = 1f - Mathf.InverseLerp(0.18f, 0.52f, r);
            float deg = (Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg + 360f) % 22f;
            float soft = Mathf.Clamp(57.3f * (1f / 512f) / Mathf.Max(d.magnitude, 0.01f), 0.1f, 3f);
            float wedge = Mathf.Clamp01((deg + soft * 0.5f) / soft) * Mathf.Clamp01((10f - deg + soft * 0.5f) / soft);
            return new Color(1f, 214f / 255f, 90f / 255f, 0.22f * wedge * mask);
        });

        private static GameObject BuildLevelCompleteScreen()
        {
            Color greenEdge = H(0x1D5F12);
            Material greenText = LilitaOutline("Green", greenEdge);
            Material blueText = LilitaOutline("Blue", H(0x103E86));
            Material brownText = LilitaOutline("Brown", H(0x7A4A16), 0.55f, -1.4f);
            Material titleText = LilitaOutline("TitleBrown", H(0x7A2E05), 0.8f, -2.4f);
            Sprite ellipse = SoftEllipseSprite(), star = UiV2Sprite("Icons/star.png"), coinSprite = UiV2Sprite("Icons/coin.png");

            var root = new GameObject("LevelCompleteScreen", typeof(RectTransform));
            RootCanvas(root, 90);
            root.GetComponent<CanvasScaler>().matchWidthOrHeight = 1f;

            // Background: navy, the blurred game grab (filled at runtime), navy 74% over it.
            Image navy = Img(root.transform, "Navy", White, H(0x14204F));
            Stretch(navy.rectTransform);
            navy.raycastTarget = true; // blocks taps to the game
            RectTransform blurRt = Stretch(Rect(root.transform, "Blur"), -10f, -10f, -10f, -10f);
            RawImage blur = blurRt.gameObject.AddComponent<RawImage>();
            blur.raycastTarget = false;
            blur.enabled = false;
            Image dark = Img(root.transform, "Dark", White, new Color(12f / 255f, 20f / 255f, 62f / 255f, 0.74f));
            Stretch(dark.rectTransform);

            RectTransform frame = Place(Rect(root.transform, "Frame"), 0.5f, 0.5f, 0f, 0f, RefWidth, RefHeight);
            Image rays = Img(frame, "Rays", WinRaysSprite(), Color.white);
            Box(rays.rectTransform, -255f, -170f, 900f, 900f);

            // Confetti (design: left, w, h, colour, fall s, sway s, delay s).
            (float l, float w, float h, int c, float fall, float sway, float delay)[] conf =
            {
                (121, 12, 18, 0x2F86E8, 2.81f, 0.94f, 3.21f), (320, 12, 14, 0x2F86E8, 3.57f, 1.19f, 3.18f),
                (132, 12, 14, 0xB266FF, 2.91f, 0.97f, 2.51f), (281, 10, 16, 0xFF5FA8, 3.62f, 1.21f, 0.53f),
                (77, 12, 16, 0xFF4D5E, 3.79f, 1.26f, 2.35f), (81, 12, 14, 0x33D1E0, 3.08f, 1.03f, 0.11f),
                (137, 10, 18, 0xFF5FA8, 3.75f, 1.25f, 3.08f), (218, 10, 18, 0xFFC531, 3.88f, 1.29f, 1.56f),
                (187, 8, 14, 0xFF5FA8, 2.82f, 0.94f, 0.76f), (223, 12, 16, 0xB266FF, 3.27f, 1.09f, 2.92f),
                (179, 12, 18, 0x3DCB4C, 3.25f, 1.08f, 0.81f), (349, 8, 16, 0xFFC531, 4.19f, 1.40f, 2.35f),
                (357, 10, 18, 0xFF5FA8, 4.05f, 1.35f, 1.99f), (335, 8, 18, 0x3DCB4C, 3.93f, 1.31f, 2.01f),
                (63, 8, 16, 0xFF4D5E, 3.97f, 1.32f, 3.46f), (176, 8, 16, 0x2F86E8, 4.04f, 1.35f, 0.07f),
                (212, 8, 14, 0x2F86E8, 3.57f, 1.19f, 2.67f), (367, 12, 16, 0x3DCB4C, 3.48f, 1.16f, 3.23f),
                (258, 8, 14, 0xB266FF, 3.10f, 1.03f, 0.27f), (274, 8, 14, 0x3DCB4C, 4.15f, 1.38f, 1.02f),
                (79, 12, 14, 0x3DCB4C, 4.17f, 1.39f, 1.19f), (70, 10, 16, 0xFF5FA8, 3.34f, 1.11f, 1.82f),
                (304, 12, 18, 0x33D1E0, 2.76f, 0.92f, 3.41f), (259, 10, 16, 0x3DCB4C, 3.61f, 1.20f, 2.50f),
                (223, 10, 18, 0x33D1E0, 3.08f, 1.03f, 1.19f), (212, 12, 16, 0xFF5FA8, 2.63f, 0.88f, 2.16f)
            };
            RectTransform confRoot = Stretch(Rect(frame, "Confetti"));
            var confData = new List<(RectTransform rt, float left, float fall, float sway, float delay)>();
            for (int i = 0; i < conf.Length; i++)
            {
                Image piece = RoundImg(confRoot, "Piece" + i, 3f, H(conf[i].c));
                Box(piece.rectTransform, conf[i].l, -30f, conf[i].w, conf[i].h); // rotates about its centre
                confData.Add((piece.rectTransform, conf[i].l + conf[i].w * 0.5f, conf[i].fall, conf[i].sway, conf[i].delay));
            }

            // Ribbon "LEVEL N" + "COMPLETE!".
            RectTransform ribbon = Box(Rect(frame, "Ribbon"), 115f, 46f, 160f, 40f);
            ribbon.gameObject.AddComponent<CanvasGroup>();
            RectTransform ribbonBox = V2Box(ribbon, "Box", 14f, H(0x7FD3FF), H(0x2F86E8), null, 3f, H(0x103E86), 4f);
            Stretch(ribbonBox);
            TextMeshProUGUI levelText = V2Text(ribbonBox, "Label", "LEVEL 1", 22f, true, Color.white, blueText);
            RectTransform title = Box(Rect(frame, "Title"), 0f, 92f, 390f, 60f);
            title.gameObject.AddComponent<CanvasGroup>();
            V2Text(title, "Label", "COMPLETE!", 54f, true, H(0xFFD43B), titleText);
            TextMeshProUGUI note = V2Text(frame, "Note", "", 15f, false, Color.white);
            Box(note.rectTransform, 0f, 150f, 390f, 20f);

            // Hero + shadow.
            Image shadow = Img(frame, "HeroShadow", ellipse, new Color(0f, 0f, 0f, 0.5f));
            Box(shadow.rectTransform, 120f, 400f, 150f, 26f);
            RectTransform heroIn = Box(Rect(frame, "HeroIn"), 105f, 168f, 180f, 237f);
            heroIn.gameObject.AddComponent<CanvasGroup>();
            Image hero = Img(heroIn, "Hero", UiV2Sprite("Characters/hero_happy.png"), Color.white);
            hero.rectTransform.anchorMin = hero.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            hero.rectTransform.pivot = new Vector2(0.5f, 0f);
            hero.rectTransform.sizeDelta = new Vector2(180f, 237f);
            hero.rectTransform.anchoredPosition = Vector2.zero;

            // Stars with flashes: (58,420,74,-14) (150,398,92,0) (260,420,74,14).
            (float l, float t, float s)[] starSpec = { (58f, 420f, 74f), (150f, 398f, 92f), (260f, 420f, 74f) };
            var stars = new Image[3];
            var flashes = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                flashes[i] = Img(frame, "Flash" + i, ellipse, new Color(1f, 0.96f, 0.72f, 1f));
                Box(flashes[i].rectTransform, starSpec[i].l, starSpec[i].t, starSpec[i].s, starSpec[i].s);
                stars[i] = Img(frame, "Star" + i, star, Color.white);
                Box(stars[i].rectTransform, starSpec[i].l, starSpec[i].t, starSpec[i].s, starSpec[i].s);
            }

            // Reward pill (95,528,200,56).
            RectTransform reward = Box(Rect(frame, "RewardPill"), 95f, 528f, 200f, 56f);
            CanvasGroup rewardGroup = reward.gameObject.AddComponent<CanvasGroup>();
            Image rewardBg = RoundImg(reward, "Bg", 28f, new Color(1f, 1f, 1f, 0.18f));
            Stretch(rewardBg.rectTransform);
            Image rewardCoin = Img(reward, "Coin", coinSprite, Color.white);
            Place(rewardCoin.rectTransform, 0.5f, 0.5f, -34f, 0f, 44f, 47f);
            TextMeshProUGUI rewardText = V2Text(reward, "Amount", "+20", 36f, true, Color.white, brownText, TextAlignmentOptions.Left);
            Place(rewardText.rectTransform, 0.5f, 0.5f, -6f, 2f, 130f, 50f, 0f, 0.5f);

            // Claim x3 (45,616,300,78): green, shine sweep, video icon, label, coin tag.
            RectTransform claim = Box(Rect(frame, "Claim"), 45f, 616f, 300f, 78f);
            CanvasGroup claimGroup = claim.gameObject.AddComponent<CanvasGroup>();
            RectTransform claimBox = V2Box(claim, "Button", 26f, H(0xA4F560), H(0x36A920), H(0x56D134), 3f, greenEdge, 6f, true);
            Stretch(claimBox);
            RectTransform shineMask = Stretch(Rect(claimBox, "ShineMask"), 3f, 3f, 3f, 3f);
            shineMask.gameObject.AddComponent<RectMask2D>();
            Image shine = Img(shineMask, "Shine", null, new Color(1f, 1f, 1f, 0.35f));
            RectTransform shineRt = shine.rectTransform;
            shineRt.anchorMin = new Vector2(0f, 0f);
            shineRt.anchorMax = new Vector2(0f, 1f);
            shineRt.pivot = new Vector2(0f, 0.5f);
            shineRt.sizeDelta = new Vector2(40f, 0f);
            shineRt.anchoredPosition = new Vector2(-80f, 0f);
            shine.gameObject.AddComponent<UiSkew>();
            Image video = Img(claimBox, "Video", UiV2Sprite("Icons/video.png"), Color.white);
            Place(video.rectTransform, 0.5f, 0.5f, -104f, 2f, 40f, 33f);
            TextMeshProUGUI claimLabel = V2Text(claimBox, "Label", "Claim x3", 30f, true, Color.white, greenText);
            Place(claimLabel.rectTransform, 0.5f, 0.5f, -12f, 2f, 140f, 40f);
            RectTransform tag = Rect(claimBox, "CoinTag");
            Place(tag, 0.5f, 0.5f, 92f, 2f, 66f, 32f);
            Image tagBg = RoundImg(tag, "Bg", 14f, new Color(0f, 0f, 0f, 0.18f));
            Stretch(tagBg.rectTransform);
            Image tagCoin = Img(tag, "Coin", coinSprite, Color.white);
            Place(tagCoin.rectTransform, 0f, 0.5f, 4f, 0f, 26f, 28f, 0f, 0.5f);
            TextMeshProUGUI tagText = V2Text(tag, "Amount", "60", 22f, true, Color.white, greenText, TextAlignmentOptions.Left);
            Stretch(tagText.rectTransform, 32f, 0f, 4f, 2f);
            Button claimButton = V2Button(claimBox);

            // Continue with X (95,716,200,44): underlined Fredoka bold 18.
            RectTransform cont = Box(Rect(frame, "Continue"), 95f, 716f, 200f, 44f);
            CanvasGroup contGroup = cont.gameObject.AddComponent<CanvasGroup>();
            TextMeshProUGUI contText = V2Text(cont, "Label", "Continue with 20", 18f, false, Color.white);
            contText.fontStyle = FontStyles.Bold | FontStyles.Underline;
            Button contButton = V2Button(cont);

            // Coin pill (top-right of the safe area): target of the coin fly.
            RectTransform safe = SafeArea(root.transform);
            RectTransform pill = V2Box(safe, "CoinPill", 17f, Color.white, H(0xFFF1CF), null, 3f, H(0x7A4A16), 0f);
            Place(pill, 1f, 1f, -14f, -25f, 80f, 34f, 1f, 1f);
            TextMeshProUGUI coinText = V2Text(pill, "Count", "300", 20f, true, H(0x5A3410), null, TextAlignmentOptions.Left);
            Stretch(coinText.rectTransform, 31f, 0f, 8f, 2f);
            coinText.gameObject.AddComponent<CoinText>();
            Wire(pill.gameObject.AddComponent<UiFitWidthToText>(), ("text", coinText), ("padding", 50f), ("minWidth", 70f));
            Image pillCoin = ShadowedImg(pill, "Coin", coinSprite, 3f, 0.25f);
            Place((RectTransform)pillCoin.transform.parent, 0f, 0.5f, 0f, 0f, 44f, 47f);
            Wire(coinText.GetComponent<CoinText>(), ("punchTarget", pillCoin.transform.parent));

            // Toast ("Video not available").
            RectTransform toast = V2Box(safe, "Toast", 23f, H(0x24345E), H(0x14204F), null, 3f, Color.white, 0f);
            Place(toast, 0.5f, 0.5f, 0f, 60f, 250f, 46f);
            toast.gameObject.AddComponent<CanvasGroup>();
            TextMeshProUGUI toastLabel = V2Text(toast, "Label", "", 20f, false, Color.white);
            Wire(toast.gameObject.AddComponent<Toast>(), ("label", toastLabel));

            LevelCompleteScreen screen = root.AddComponent<LevelCompleteScreen>();
            Wire(screen, ("blurImage", blur), ("rays", rays.rectTransform), ("ribbon", ribbon), ("levelText", levelText),
                ("title", title), ("noteText", note), ("heroIn", heroIn), ("hero", hero.rectTransform), ("heroShadow", shadow),
                ("stars", stars), ("flashes", flashes), ("rewardPill", reward), ("rewardGroup", rewardGroup),
                ("rewardText", rewardText), ("rewardCoin", rewardCoin), ("claim", claim), ("claimGroup", claimGroup),
                ("claimButton", claimButton), ("claimCoinsText", tagText), ("claimShine", shineRt), ("continueRt", cont),
                ("continueGroup", contGroup), ("continueButton", contButton), ("continueText", contText),
                ("coinTarget", (RectTransform)pillCoin.transform.parent),
                ("confetti", new object[confData.Count]));
            for (int i = 0; i < confData.Count; i++)
            {
                string p = $"confetti.Array.data[{i}].";
                Wire(screen, (p + "piece", confData[i].rt), (p + "left", confData[i].left), (p + "fallDuration", confData[i].fall),
                    (p + "swayDuration", confData[i].sway), (p + "delay", confData[i].delay));
            }
            root.SetActive(false);
            return root;
        }
    }
}
