using System;
using System.Collections.Generic;
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
    /// Step R9: Gift Chest (GiftChest.dc.html) and New World (WorldUnlocked.dc.html) overlays in the Home scene,
    /// driven by the Road Map. Design px, 390x844 frame.
    /// </summary>
    public static partial class UIBuilder
    {
        [MenuItem("Tools/Bus Rush/Build Gift Chest + New World (R9)")]
        public static void BuildRewardsOnly()
        {
            Load();
            Save(BuildGiftChestScreen(), "GiftChestScreen");
            Save(BuildNewWorldScreen(), "NewWorldScreen");
            Save(BuildRoadMap(), "RoadMap"); // chest view + coin target
            AssetDatabase.SaveAssets();
            WireHome(AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/HomeScreen.prefab"),
                     AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/UIManager.prefab"));
            EditorSceneManager.OpenScene($"{ScenesDir}/Boot.unity", OpenSceneMode.Single);
            Debug.Log("[UIBuilder] Gift Chest + New World built.");
        }

        public static void BuildRewardsBatch()
        {
            try
            {
                BuildRewardsOnly();
            }
            catch (Exception e)
            {
                Debug.LogError("[UIBuilder] FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }

        private static GameObject OverlayRoot(string name, int order, out RectTransform frame)
        {
            var root = new GameObject(name, typeof(RectTransform));
            RootCanvas(root, order);
            root.GetComponent<CanvasScaler>().matchWidthOrHeight = 1f;
            Image navy = Img(root.transform, "Navy", White, H(0x14204F));
            Stretch(navy.rectTransform);
            navy.raycastTarget = true; // blocks taps to the map
            frame = Place(Rect(root.transform, "Frame"), 0.5f, 0.5f, 0f, 0f, RefWidth, RefHeight);
            return root;
        }

        private static RectTransform GreenButton(RectTransform parent, string name, string text, float left, float top, float w, float h,
                                                 float size, bool video, out Button button, out RectTransform shine)
        {
            RectTransform box = V2Box(parent, name, 26f, H(0xA4F560), H(0x36A920), H(0x56D134), 3f, H(0x1D5F12), 6f, true);
            Box(box, left, top, w, h);
            shine = null;
            if (video)
            {
                RectTransform mask = Stretch(Rect(box, "ShineMask"), 3f, 3f, 3f, 3f);
                mask.gameObject.AddComponent<RectMask2D>();
                Image s = Img(mask, "Shine", null, new Color(1f, 1f, 1f, 0.35f));
                shine = s.rectTransform;
                shine.anchorMin = new Vector2(0f, 0f);
                shine.anchorMax = new Vector2(0f, 1f);
                shine.pivot = new Vector2(0f, 0.5f);
                shine.sizeDelta = new Vector2(40f, 0f);
                shine.anchoredPosition = new Vector2(-80f, 0f);
                s.gameObject.AddComponent<UiSkew>();
                Image v = Img(box, "Video", UiV2Sprite("Icons/video.png"), Color.white);
                Place(v.rectTransform, 0.5f, 0.5f, -70f, 2f, 40f, 33f);
            }
            V2Text(box, "Label", text, size, true, Color.white, LilitaOutline("Green", H(0x1D5F12))).rectTransform.anchoredPosition =
                new Vector2(video ? 22f : 0f, 2f);
            button = V2Button(box);
            return box;
        }

        // ------------------------------------------------------------------ Gift chest

        private static GameObject BuildGiftChestScreen()
        {
            GameObject root = OverlayRoot("GiftChestScreen", 60, out RectTransform frame);
            RectTransform blurRt = Stretch(Rect(root.transform, "Blur"), -10f, -10f, -10f, -10f);
            blurRt.SetSiblingIndex(1);
            RawImage blur = blurRt.gameObject.AddComponent<RawImage>();
            blur.raycastTarget = false;
            blur.enabled = false;
            Image dark = Img(root.transform, "Dark", White, new Color(12f / 255f, 20f / 255f, 62f / 255f, 0.82f));
            Stretch(dark.rectTransform);
            dark.transform.SetSiblingIndex(2);

            Image rays = Img(frame, "Rays", BoosterRaysSprite(), new Color(1f, 0.86f, 0.45f, 1f));
            Box(rays.rectTransform, -65f, 80f, 520f, 520f);

            RectTransform title = Box(Rect(frame, "Title"), 0f, 96f, 390f, 56f);
            CanvasGroup titleGroup = title.gameObject.AddComponent<CanvasGroup>();
            V2Text(title, "Label", "GIFT CHEST!", 48f, true, H(0xFFD43B), LilitaOutline("TitleBrown", H(0x7A2E05), 0.8f, -2.4f));
            RectTransform subtitle = Box(Rect(frame, "Subtitle"), 0f, 160f, 390f, 22f);
            CanvasGroup subtitleGroup = subtitle.gameObject.AddComponent<CanvasGroup>();
            TextMeshProUGUI sub = V2Text(subtitle, "Label", "Level 5 reward", 17f, false, Color.white);
            sub.fontStyle = FontStyles.Bold;

            Image flash = Img(frame, "Flash", SoftEllipseSprite(), new Color(1f, 0.97f, 0.75f, 1f));
            Box(flash.rectTransform, 95f, 250f, 200f, 200f);
            RectTransform closed = Box(Rect(frame, "ChestClosed"), 95f, 250f, 200f, 172f);
            CanvasGroup closedGroup = closed.gameObject.AddComponent<CanvasGroup>();
            Image closedImg = Img(closed, "Image", UiV2Sprite("Map/chest_closed.png"), Color.white);
            closedImg.preserveAspect = true;
            Stretch(closedImg.rectTransform);
            RectTransform open = Box(Rect(frame, "ChestOpen"), 95f, 226f, 200f, 208f);
            CanvasGroup openGroup = open.gameObject.AddComponent<CanvasGroup>();
            Image openImg = Img(open, "Image", UiV2Sprite("Map/chest_open.png"), Color.white);
            openImg.preserveAspect = true;
            Stretch(openImg.rectTransform);

            // 12 coins bursting from (195, 351.5): (dx, dy, delay) from the design.
            (float dx, float dy, float delay)[] burst =
            {
                (-20, -169, 1.53f), (121, -253, 1.54f), (-23, -247, 1.57f), (-93, -165, 1.41f), (-24, -163, 1.44f), (143, -197, 1.25f),
                (-40, -156, 1.35f), (49, -220, 1.52f), (-114, -225, 1.47f), (77, -228, 1.30f), (-148, -207, 1.52f), (-66, -218, 1.35f)
            };
            RectTransform burstRoot = Box(Rect(frame, "Burst"), 195f, 351.5f, 0f, 0f);
            Sprite coin = UiV2Sprite("Icons/coin.png");
            var coinData = new List<(RectTransform rt, Vector2 offset, float delay)>();
            for (int i = 0; i < burst.Length; i++)
            {
                Image c = Img(burstRoot, "Coin" + i, coin, Color.white);
                Place(c.rectTransform, 0.5f, 0.5f, 0f, 0f, 40f, 43f);
                coinData.Add((c.rectTransform, new Vector2(burst[i].dx, burst[i].dy), burst[i].delay));
            }

            // Reward cards 120x110 at x 67 / 203, top 470.
            Material white = LilitaOutline("Brown", H(0x7A4A16), 0.55f, -1.4f);
            RectTransform RewardCard(string name, float left, Sprite icon, Vector2 size, string text, out Image iconImg, out TextMeshProUGUI label)
            {
                RectTransform card = Box(Rect(frame, name), left, 470f, 120f, 110f);
                card.gameObject.AddComponent<CanvasGroup>();
                Image bg = RoundImg(card, "Bg", 22f, new Color(1f, 1f, 1f, 0.18f));
                Stretch(bg.rectTransform);
                iconImg = Img(card, "Icon", icon, Color.white);
                iconImg.preserveAspect = true;
                Place(iconImg.rectTransform, 0.5f, 0.5f, 0f, 16f, size.x, size.y);
                label = V2Text(card, "Amount", text, 26f, true, Color.white, white);
                Place(label.rectTransform, 0.5f, 0.5f, 0f, -32f, 110f, 32f);
                return card;
            }

            RectTransform coinCard = RewardCard("CoinReward", 67f, coin, new Vector2(52f, 55f), "+100", out Image rewardCoin, out TextMeshProUGUI coinAmount);
            RectTransform boosterCard = RewardCard("BoosterReward", 203f, UiV2Sprite("Boosters/vip.png"), new Vector2(48f, 53f), "x1",
                                                   out Image boosterIcon, out TextMeshProUGUI boosterAmount);

            RectTransform x2 = GreenButton(frame, "CollectX2", "Collect x2", 55f, 624f, 280f, 72f, 28f, true, out Button x2Button, out RectTransform shine);
            CanvasGroup x2Group = x2.gameObject.AddComponent<CanvasGroup>();
            RectTransform collect = Box(Rect(frame, "Collect"), 115f, 716f, 160f, 44f);
            CanvasGroup collectGroup = collect.gameObject.AddComponent<CanvasGroup>();
            TextMeshProUGUI collectText = V2Text(collect, "Label", "Collect", 18f, false, Color.white);
            collectText.fontStyle = FontStyles.Bold | FontStyles.Underline;
            Button collectButton = V2Button(collect);

            GiftChestScreen screen = root.AddComponent<GiftChestScreen>();
            Wire(screen, ("blur", blur), ("dark", dark), ("rays", rays.rectTransform), ("raysGraphic", rays), ("title", title),
                ("titleGroup", titleGroup), ("subtitle", subtitle), ("subtitleGroup", subtitleGroup), ("subtitleText", sub),
                ("flash", flash), ("chestClosed", closed), ("chestClosedGroup", closedGroup), ("chestOpen", open),
                ("chestOpenGroup", openGroup), ("rewardCards", new object[] { coinCard, boosterCard }), ("coinAmountText", coinAmount),
                ("boosterIcon", boosterIcon), ("boosterAmountText", boosterAmount),
                ("boosterSprites", new object[] { UiV2Sprite("Boosters/vip.png"), UiV2Sprite("Boosters/arrange.png"), UiV2Sprite("Boosters/shuffle.png") }),
                ("rewardCoinIcon", rewardCoin), ("collectX2", x2), ("collectX2Group", x2Group), ("collectX2Button", x2Button),
                ("collectX2Shine", shine), ("collect", collect), ("collectGroup", collectGroup), ("collectButton", collectButton),
                ("coins", new object[coinData.Count]));
            for (int i = 0; i < coinData.Count; i++)
            {
                string p = $"coins.Array.data[{i}].";
                Wire(screen, (p + "coin", coinData[i].rt), (p + "offset", (Vector3)coinData[i].offset), (p + "delay", coinData[i].delay));
            }
            root.SetActive(false);
            return root;
        }

        // ------------------------------------------------------------------ New world

        private static Sprite FlakeSprite() => GeneratedSprite("flake", 32, (u, v) =>
        {
            float d = new Vector2(u - 0.5f, v - 0.5f).magnitude * 2f;
            return new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 16f));
        });

        private static Sprite BubbleSprite() => GeneratedSprite("bubble", 64, (u, v) =>
        {
            Vector2 p = new Vector2(u - 0.5f, v - 0.5f);
            float d = p.magnitude * 2f;
            float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 0.88f) * 12f);
            float body = d < 1f ? 0.18f : 0f;
            float glint = Mathf.Clamp01(1f - (p - new Vector2(-0.16f, 0.16f)).magnitude * 9f);
            return new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Max(ring, Mathf.Max(body, glint))));
        });

        private static GameObject BuildNewWorldScreen()
        {
            GameObject root = OverlayRoot("NewWorldScreen", 61, out RectTransform frame);
            RectTransform bgRt = Rect(root.transform, "Background");
            bgRt.SetSiblingIndex(1);
            Stretch(bgRt);
            RectTransform bgCover = Rect(bgRt, "Cover");
            RawImage bg = bgCover.gameObject.AddComponent<RawImage>();
            bg.raycastTarget = false;
            AspectRatioFitter fit = bgCover.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = 941f / 1672f;
            Image dark = Img(root.transform, "Dark", White, new Color(12f / 255f, 30f / 255f, 80f / 255f, 0.72f));
            Stretch(dark.rectTransform);
            dark.transform.SetSiblingIndex(2);

            Image rays = Img(frame, "Rays", BoosterRaysSprite(), new Color(0.8f, 0.92f, 1f, 0.9f));
            Box(rays.rectTransform, -65f, 100f, 520f, 520f);

            // 30 particles (design: left, size, duration, negative delay).
            (float l, float s, float dur, float delay)[] flakes =
            {
                (107, 10, 5.6f, -2.7f), (22, 8, 4.8f, -7.1f), (210, 8, 4.2f, -3.0f), (251, 6, 4.6f, -1.9f), (376, 6, 7.6f, -0.9f),
                (305, 5, 4.0f, -6.1f), (101, 8, 7.6f, -1.6f), (4, 10, 7.6f, -1.1f), (8, 5, 6.0f, -4.6f), (275, 8, 7.7f, -3.4f),
                (203, 5, 4.8f, -6.1f), (358, 10, 6.3f, -1.4f), (311, 6, 6.9f, -0.3f), (138, 10, 6.2f, -4.9f), (76, 10, 7.0f, -5.9f),
                (294, 6, 8.0f, -1.0f), (361, 6, 5.6f, -7.2f), (82, 6, 6.4f, -0.3f), (187, 5, 7.4f, -4.2f), (289, 5, 7.1f, -0.7f),
                (177, 6, 4.4f, -7.1f), (187, 5, 5.8f, -3.2f), (294, 10, 7.0f, -3.0f), (311, 5, 5.3f, -3.5f), (109, 8, 4.9f, -2.2f),
                (45, 6, 5.2f, -2.3f), (264, 6, 7.1f, -0.2f), (286, 10, 7.2f, -4.0f), (363, 5, 7.1f, -4.5f), (33, 8, 5.7f, -1.8f)
            };
            RectTransform particleRoot = Stretch(Rect(frame, "Particles"));
            var parts = new List<(Image img, float left, float dur, float delay)>();
            for (int i = 0; i < flakes.Length; i++)
            {
                Image f = Img(particleRoot, "P" + i, FlakeSprite(), Color.white);
                float size = flakes[i].s * 1.6f; // sparkles / bubbles read better a little bigger than the 5-10 px flakes
                Box(f.rectTransform, 0f, 0f, size, size, 0f, 1f);
                parts.Add((f, flakes[i].l, flakes[i].dur, flakes[i].delay));
            }

            RectTransform title = Box(Rect(frame, "Title"), 0f, 64f, 390f, 58f);
            CanvasGroup titleGroup = title.gameObject.AddComponent<CanvasGroup>();
            V2Text(title, "Label", "NEW WORLD!", 50f, true, Color.white, LilitaOutline("Blue", H(0x103E86)));

            // Card (60,150) 270x360: white 6 px frame, 10 px #9BC3EE lip, the world map clipped inside.
            RectTransform card = Box(Rect(frame, "Card"), 60f, 150f, 270f, 360f);
            CanvasGroup cardGroup = card.gameObject.AddComponent<CanvasGroup>();
            Image lip = RoundImg(card, "Lip", 28f, H(0x9BC3EE));
            Stretch(lip.rectTransform, 0f, -10f, 0f, 10f);
            Image frameImg = RoundImg(card, "Frame", 28f, Color.white);
            Stretch(frameImg.rectTransform);
            Image clip = RoundImg(card, "Clip", 22f, Color.white);
            Stretch(clip.rectTransform, 6f, 6f, 6f, 6f);
            clip.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            Image map = Img(clip.transform, "Map", UiV2Sprite("Backgrounds/map_snow.png"), Color.white);
            Box(map.rectTransform, -60f, -40f, 380f, 676f);
            Image shade = Img(clip.transform, "Shade", White, Color.white);
            shade.rectTransform.anchorMin = new Vector2(0f, 0f);
            shade.rectTransform.anchorMax = new Vector2(1f, 0f);
            shade.rectTransform.pivot = new Vector2(0.5f, 0f);
            shade.rectTransform.sizeDelta = new Vector2(0f, 90f);
            shade.rectTransform.anchoredPosition = Vector2.zero;
            Gradient(shade, new Color(16f / 255f, 62f / 255f, 134f / 255f, 0f), new Color(16f / 255f, 62f / 255f, 134f / 255f, 0.92f),
                     new Color(16f / 255f, 62f / 255f, 134f / 255f, 0.6f));
            TextMeshProUGUI worldNum = V2Text(clip.transform, "World", "WORLD 2", 13f, true, H(0xD6F0FF));
            worldNum.characterSpacing = 8f;
            Place(worldNum.rectTransform, 0.5f, 0f, 0f, 52f, 250f, 16f, 0.5f, 0f);
            TextMeshProUGUI worldName = V2Text(clip.transform, "Name", "Snow", 34f, true, Color.white, LilitaOutline("Blue", H(0x103E86)));
            Place(worldName.rectTransform, 0.5f, 0f, 0f, 10f, 250f, 40f, 0.5f, 0f);

            RectTransform padlock = Box(Rect(frame, "Padlock"), 165f, 290f, 60f, 74f);
            CanvasGroup padGroup = padlock.gameObject.AddComponent<CanvasGroup>();
            Image pad = ShadowedImg(padlock, "Lock", UiV2Sprite("Map/lock.png"), 6f, 0.35f);
            Stretch((RectTransform)pad.transform.parent);

            RectTransform levels = Box(Rect(frame, "Levels"), 0f, 540f, 390f, 24f);
            CanvasGroup levelsGroup = levels.gameObject.AddComponent<CanvasGroup>();
            TextMeshProUGUI levelsText = V2Text(levels, "Label", "Levels 11-20 are open", 18f, false, Color.white);
            levelsText.fontStyle = FontStyles.Bold;

            RectTransform go = GreenButton(frame, "LetsGo", "Let's go!", 55f, 600f, 280f, 74f, 30f, false, out Button goButton, out _);
            CanvasGroup goGroup = go.gameObject.AddComponent<CanvasGroup>();

            NewWorldScreen screen = root.AddComponent<NewWorldScreen>();
            Wire(screen, ("background", bg), ("dark", dark), ("rays", rays.rectTransform), ("title", title), ("titleGroup", titleGroup),
                ("card", card), ("cardGroup", cardGroup), ("cardMap", map), ("worldNumberText", worldNum), ("worldNameText", worldName),
                ("padlock", padlock), ("padlockGroup", padGroup), ("levels", levels), ("levelsGroup", levelsGroup),
                ("levelsText", levelsText), ("go", go), ("goGroup", goGroup), ("goButton", goButton),
                ("worldMaps", new object[] { UiV2Sprite("Backgrounds/map_city.png"), UiV2Sprite("Backgrounds/map_snow.png"), UiV2Sprite("Backgrounds/map_beach.png") }),
                ("flakeSprite", FlakeSprite()), ("sparkleSprite", UiV2Sprite("Splash/sparkle.png")), ("bubbleSprite", BubbleSprite()),
                ("particles", new object[parts.Count]));
            for (int i = 0; i < parts.Count; i++)
            {
                string p = $"particles.Array.data[{i}].";
                Wire(screen, (p + "image", parts[i].img), (p + "left", parts[i].left), (p + "duration", parts[i].dur), (p + "delay", parts[i].delay));
            }
            root.SetActive(false);
            return root;
        }
    }
}
