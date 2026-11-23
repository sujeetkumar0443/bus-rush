using System;
using BusRush.Gameplay;
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
    /// Step R8: Daily Reward (DailyReward.dc.html) and Lucky Spin (LuckySpin.dc.html) popups on the blurred V2 base.
    /// Design px in the 390x844 frame.
    /// </summary>
    public static partial class UIBuilder
    {
        [MenuItem("Tools/Bus Rush/Build Daily + Lucky Spin (R8)")]
        public static void BuildDailyOnly()
        {
            Load();
            AddToCatalog(SavePopup(BuildDailyPopup()));
            AddToCatalog(SavePopup(BuildLuckySpinPopup()));
            CleanCatalog();
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene($"{ScenesDir}/Boot.unity", OpenSceneMode.Single);
            Debug.Log("[UIBuilder] Daily + Lucky Spin built.");
        }

        public static void BuildDailyBatch()
        {
            try
            {
                BuildDailyOnly();
            }
            catch (Exception e)
            {
                Debug.LogError("[UIBuilder] FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }

        // ------------------------------------------------------------------ generated sprites

        /// <summary>Claimed tick: green circle r18 (white 3 px stroke) with a white check (SVG 40x40).</summary>
        private static Sprite TickSprite() => GeneratedSprite("tick_badge", 120, (u, v) =>
        {
            const float px = 40f / 120f;
            var p = new Vector2(u * 40f, (1f - v) * 40f);
            float d = Vector2.Distance(p, new Vector2(20f, 20f));
            float fill = Mathf.Clamp01((19.5f - d) / px + 0.5f);
            float ring = Stroke(Mathf.Abs(d - 18f), 3f, px);
            float check = Stroke(Polyline(p, new Vector2(11, 21), new Vector2(17, 27), new Vector2(29, 14)), 4.5f, px);
            Color green = new Color32(0x3D, 0xB5, 0x2A, 0xFF);
            Color c = Color.Lerp(green, Color.white, Mathf.Max(ring, check));
            c.a = Mathf.Max(fill, ring);
            return c;
        });

        /// <summary>Wheel face: 8 conic segments (clockwise from the top), white dividers (0.8 deg) and a 3 px white rim.</summary>
        private static Sprite WheelSprite() => GeneratedSprite("wheel_segments", 512, (u, v) =>
        {
            uint[] colours = { 0xFF4D5E, 0xFFC531, 0x3DCB4C, 0x2F86E8, 0xB266FF, 0xFF8A1F, 0x33C3D6, 0xFF5FA8 };
            Vector2 d = new Vector2(u - 0.5f, v - 0.5f);
            float r = d.magnitude * 2f; // 1 at the edge
            float edgePx = 1f / 256f;
            float alpha = Mathf.Clamp01((1f - r) / edgePx + 0.5f);
            float ang = (Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg + 360f) % 360f; // clockwise from the top
            Color c = UIArtGenerator.Hex(colours[Mathf.Min(7, (int)(ang / 45f))]);
            float inSeg = ang % 45f;
            float arcPx = Mathf.Rad2Deg * edgePx / Mathf.Max(r * 0.5f, 0.01f);
            float divider = Mathf.Clamp01((0.8f + arcPx * 0.5f - inSeg) / arcPx) * 0.9f;
            float rim = Mathf.Clamp01((r - (1f - 3f / 140f)) / edgePx + 0.5f);
            c = Color.Lerp(c, Color.white, Mathf.Max(divider, rim));
            c.a = alpha;
            return c;
        });

        /// <summary>Pointer: SVG "M23 52 L4 14 A20 20 0 1 1 42 14 Z" (#E8363F, 3 px #7A1016) + white dot r7 at (23,18).</summary>
        private static Sprite PointerSprite() => GeneratedSprite("wheel_pointer", 138, (u, v) =>
        {
            // 46x54 box mapped into a 138 px square (centred horizontally).
            const float scale = 54f / 138f;
            var p = new Vector2(u * 138f * scale - (54f - 46f) * 0.5f, (1f - v) * 54f);
            Vector2[] tri = { new Vector2(23, 52), new Vector2(4, 14), new Vector2(42, 14) };
            float dc = Vector2.Distance(p, new Vector2(23f, 20f)) - 20f;
            bool inTri = InPolygon(p, tri);
            float dt = Polyline(p, tri[0], tri[1], tri[2], tri[0]);
            float sd = Mathf.Min(dc, inTri ? -dt : dt); // signed distance of the union (approx.)
            float fill = Mathf.Clamp01((1.5f - sd) / scale + 0.5f); // includes the stroke (outer 1.5)
            float stroke = Mathf.Clamp01((sd + 1.5f) / scale + 0.5f);
            float dot = Mathf.Clamp01((7f - Vector2.Distance(p, new Vector2(23f, 18f))) / scale + 0.5f);
            Color c = Color.Lerp(UIArtGenerator.Hex(0xE8363F), UIArtGenerator.Hex(0x7A1016), stroke);
            c = Color.Lerp(c, Color.white, dot);
            c.a = fill;
            return c;
        });

        // ------------------------------------------------------------------ Daily reward

        private static GameObject BuildDailyPopup()
        {
            const float b = 4f;
            V2Shell s = V2PopupShell<DailyRewardPopup>("DailyRewardPopup", 342f, 570f, -13f, 41f, 260f, "DAILY REWARD", 28f, 308f, 0.66f,
                                                      0xFFE27A, 0xFFA91F, 0x8A4A05, "DarkBrown");
            Sprite coin = UiV2Sprite("Icons/coin.png"), tick = TickSprite();
            var cards = new DailyCardView[DailyRewards.Days];
            (float x, float y)[] slots = { (22f, 82f), (122f, 82f), (222f, 82f), (22f, 192f), (122f, 192f), (222f, 192f) };
            for (int i = 0; i < 6; i++)
            {
                RewardSpec r = GameTuning.DailyRewards[i];
                BoosterType? booster = r.MainBooster;
                (Sprite icon, Vector2 size) look = booster == BoosterType.Arrange ? (UiV2Sprite("Boosters/arrange.png"), new Vector2(26f, 31f))
                    : booster == BoosterType.Vip ? (UiV2Sprite("Boosters/vip.png"), new Vector2(34f, 30f))
                    : booster == BoosterType.Shuffle ? (UiV2Sprite("Boosters/shuffle.png"), new Vector2(36f, 27f))
                    : (coin, new Vector2(30f, 32f));
                cards[i] = DayCard(s.Panel, $"Day{i + 1}", $"Day {i + 1}", b + slots[i].x, b + slots[i].y, 92f, 100f, 16f,
                                   look.icon, look.size, booster.HasValue ? "x1" : r.coins.ToString(), tick, false, out _);
            }
            cards[6] = DayCard(s.Panel, "Day7", "", b + 22f, b + 304f, 292f, 96f, 18f, null, Vector2.zero, "", tick, true, out RectTransform chest);

            RectTransform claimPulse = Box(Rect(s.Panel, "Claim"), b + 22f, b + 420f, 292f, 62f);
            RectTransform claimBox = V2Box(claimPulse, "ClaimButton", 22f, H(0xA4F560), H(0x36A920), H(0x56D134), 3f, H(0x1D5F12), 6f, true);
            Stretch(claimBox);
            V2Text(claimBox, "Label", "Claim", 26f, true, Color.white, LilitaOutline("Green", H(0x1D5F12))).rectTransform.anchoredPosition = new Vector2(0f, 2f);
            Button claim = V2Button(claimBox);

            RectTransform x2 = V2Box(s.Panel, "DoubleButton", 20f, H(0x7FD3FF), H(0x1F74D6), H(0x3A9BF5), 3f, H(0x103E86), 6f, true);
            Box(x2, b + 22f, b + 494f, 292f, 52f);
            Image video = Img(x2, "Video", UiV2Sprite("Icons/video.png"), Color.white);
            Place(video.rectTransform, 0.5f, 0.5f, -52f, 2f, 32f, 26f);
            V2Text(x2, "Label", "Claim x2", 21f, true, Color.white, LilitaOutline("Blue", H(0x103E86))).rectTransform.anchoredPosition = new Vector2(16f, 2f);
            Button doubleButton = V2Button(x2);

            Wire(s.Root.GetComponent<DailyRewardPopup>(), ("cards", cards), ("claimButton", claim), ("doubleButton", doubleButton),
                ("claimPulse", claimPulse), ("chest", chest), ("coinSprite", coin));
            return s.Root;
        }

        /// <summary>A day card; Day 7 (wide) gets the purple face, the bouncing chest and its two text lines.</summary>
        private static DailyCardView DayCard(RectTransform panel, string name, string label, float left, float top, float w, float h, float radius,
                                             Sprite icon, Vector2 iconSize, string amount, Sprite tick, bool day7, out RectTransform chest)
        {
            RectTransform card = Box(Rect(panel, name), left, top, w, h);
            CanvasGroup group = card.gameObject.AddComponent<CanvasGroup>();

            Image glow = Img(card, "Glow", Glow, new Color(1f, 0.84f, 0.24f, 0.9f));
            Stretch(glow.rectTransform, -22f, -22f, -22f, -22f);
            Image ring = RoundImg(card, "GlowRing", radius + 3f, H(0xFFE27A));
            Stretch(ring.rectTransform, -3f, -3f, -3f, -3f);
            Image border = RoundImg(card, "Border", radius, day7 ? H(0x4A1690) : H(0x8FA8CF));
            Stretch(border.rectTransform);
            Image face = RoundImg(card, "Face", radius - 3f, Color.white);
            Stretch(face.rectTransform, 3f, 3f, 3f, 3f);
            UiGradient gradient = face.gameObject.AddComponent<UiGradient>();
            gradient.Set(day7 ? H(0xE9D7FF) : Color.white, day7 ? H(0xB98CFF) : H(0xD9E7FA));

            TextMeshProUGUI title;
            chest = null;
            if (day7)
            {
                chest = Rect(card, "Chest");
                Place(chest, 0f, 0.5f, 14f, 0f, 86f, 74f, 0f, 0.5f);
                Image chestImg = Img(chest, "Image", UiV2Sprite("Map/chest_closed.png"), Color.white);
                chestImg.preserveAspect = true;
                Stretch(chestImg.rectTransform);
                title = V2Text(card, "Title", "Day 7 · Big chest", 18f, true, Color.white, LilitaOutline("Purple", H(0x4A1690)),
                               TextAlignmentOptions.Left);
                Stretch(title.rectTransform, 112f, 46f, 8f, 18f);
                TextMeshProUGUI sub = V2Text(card, "Subtitle", "200 coins + 3 boosters", 14f, false, H(0x2A0E5C), null, TextAlignmentOptions.Left);
                sub.fontStyle = FontStyles.Bold;
                Stretch(sub.rectTransform, 112f, 20f, 8f, 50f);
            }
            else
            {
                title = V2Text(card, "Label", label, 13f, true, H(0x8FA8CF));
                Box(title.rectTransform, 0f, 6f, w, 16f);
                Image ic = Img(card, "Icon", icon, Color.white);
                ic.preserveAspect = true;
                Place(ic.rectTransform, 0.5f, 0.5f, 0f, 2f, iconSize.x, iconSize.y);
                TextMeshProUGUI amt = V2Text(card, "Amount", amount, 15f, true, H(0x24345E));
                Box(amt.rectTransform, 0f, h - 25f, w, 18f);
            }

            Image overlay = RoundImg(card, "Claimed", radius, new Color(40f / 255f, 140f / 255f, 40f / 255f, 0.35f));
            Stretch(overlay.rectTransform);
            Image tickImg = Img(overlay.transform, "Tick", tick, Color.white);
            Place(tickImg.rectTransform, 0.5f, 0.5f, 0f, 0f, 40f, 40f);
            overlay.gameObject.SetActive(false);

            DailyCardView view = card.gameObject.AddComponent<DailyCardView>();
            Wire(view, ("face", gradient), ("border", border), ("label", title), ("claimedOverlay", overlay.gameObject),
                ("tick", tickImg.rectTransform), ("glow", glow), ("glowRing", ring.gameObject), ("group", group), ("keepColours", day7));
            return view;
        }

        // ------------------------------------------------------------------ Lucky spin

        private static GameObject BuildLuckySpinPopup()
        {
            // Panel = the 300 px column from the ribbon (y 92) to the text under the button (y ~680); x 45..345.
            V2Shell s = V2PopupShell<LuckySpinPopup>("LuckySpinPopup", 300f, 590f, 35f, 25f, 250f, "LUCKY SPIN", 30f, 287f, 0.66f,
                                                    0xD7A6FF, 0x8A3FE0, 0x4A1690, "Purple", 0f, 12f, false);
            RectTransform p = s.Panel;
            Sprite coin = UiV2Sprite("Icons/coin.png"), soft = SoftCircleSprite();

            Image rays = Img(p, "Rays", BoosterRaysSprite(), new Color(1f, 1f, 1f, 0.8f));
            Box(rays.rectTransform, -70f, 68f, 440f, 440f);
            rays.transform.SetAsFirstSibling();

            // Gold rim (300, border 4, 8 px lip) with 12 bulbs, the wheel (280) and the hub (60).
            RectTransform rim = V2Box(p, "Rim", 150f, H(0xFFE27A), H(0xF2A20C), null, 4f, H(0x8A4A05), 8f);
            Box(rim, 0f, 138f, 300f, 300f);
            (float x, float y)[] bulbPos =
            {
                (144.0f, 5.0f), (213.5f, 23.6f), (264.4f, 74.5f), (283.0f, 144.0f), (264.4f, 213.5f), (213.5f, 264.4f),
                (144.0f, 283.0f), (74.5f, 264.4f), (23.6f, 213.5f), (5.0f, 144.0f), (23.6f, 74.5f), (74.5f, 23.6f)
            };
            var bulbs = new Image[12];
            for (int i = 0; i < 12; i++)
            {
                bulbs[i] = Img(rim, "Bulb" + i, Circle, Color.white);
                Box(bulbs[i].rectTransform, 4f + bulbPos[i].x, 4f + bulbPos[i].y, 12f, 12f);
                Image halo = Img(bulbs[i].transform, "Halo", soft, new Color(1f, 0.89f, 0.48f, 0.85f));
                Stretch(halo.rectTransform, -9f, -9f, -9f, -9f);
                halo.transform.SetAsFirstSibling();
            }

            RectTransform wheel = Box(Rect(rim, "Wheel"), 10f, 10f, 280f, 280f);
            Image face = Img(wheel, "Face", WheelSprite(), Color.white);
            Stretch(face.rectTransform);
            Material labelMat = LilitaOutline("Wheel", H(0x24345E), 0.5f, -1.2f);
            for (int i = 0; i < LuckySpin.Segments; i++)
            {
                RewardSpec r = GameTuning.LuckySpinSegments[i];
                BoosterType? booster = r.MainBooster;
                (Sprite icon, Vector2 size) look = booster == BoosterType.Arrange ? (UiV2Sprite("Boosters/arrange.png"), new Vector2(26f, 31f))
                    : booster == BoosterType.Vip ? (UiV2Sprite("Boosters/vip.png"), new Vector2(34f, 30f))
                    : booster == BoosterType.Shuffle ? (UiV2Sprite("Boosters/shuffle.png"), new Vector2(36f, 27f))
                    : (coin, new Vector2(30f, 32f));
                RectTransform slice = Stretch(Rect(wheel, "Segment" + i));
                slice.localRotation = Quaternion.Euler(0f, 0f, -(22.5f + 45f * i)); // CSS rotate(22.5 + 45 i deg)
                Image ic = Img(slice, "Icon", look.icon, Color.white);
                ic.preserveAspect = true;
                Place(ic.rectTransform, 0.5f, 0.5f, 0f, 102f, look.size.x, look.size.y);
                TextMeshProUGUI t = V2Text(slice, "Label", booster.HasValue ? "x1" : r.coins.ToString(), 16f, true, Color.white, labelMat);
                Place(t.rectTransform, 0.5f, 0.5f, 0f, 74f, 60f, 22f);
            }

            Image hubEdge = Img(rim, "Hub", Circle, H(0x8A4A05));
            Box(hubEdge.rectTransform, 120f, 120f, 60f, 60f);
            Image hubFace = Img(hubEdge.transform, "Face", Circle, H(0xFFC531));
            Stretch(hubFace.rectTransform, 3f, 3f, 3f, 3f);
            Gradient(hubFace, H(0xFFF3B0), H(0xC97A06), H(0xFFC531));

            Image pointer = Img(p, "Pointer", PointerSprite(), Color.white);
            Box(pointer.rectTransform, 127f - 4f, 114f, 54f, 54f);

            // Spin (10,478) 280x72: green, video icon, shine + pulse; spins-left text below.
            RectTransform spinRoot = Box(Rect(p, "Spin"), 10f, 478f, 280f, 72f);
            CanvasGroup spinGroup = spinRoot.gameObject.AddComponent<CanvasGroup>();
            RectTransform spinBox = V2Box(spinRoot, "SpinButton", 26f, H(0xA4F560), H(0x36A920), H(0x56D134), 3f, H(0x1D5F12), 6f, true);
            Stretch(spinBox);
            RectTransform shineMask = Stretch(Rect(spinBox, "ShineMask"), 3f, 3f, 3f, 3f);
            shineMask.gameObject.AddComponent<RectMask2D>();
            Image shine = Img(shineMask, "Shine", null, new Color(1f, 1f, 1f, 0.35f));
            RectTransform shineRt = shine.rectTransform;
            shineRt.anchorMin = new Vector2(0f, 0f);
            shineRt.anchorMax = new Vector2(0f, 1f);
            shineRt.pivot = new Vector2(0f, 0.5f);
            shineRt.sizeDelta = new Vector2(40f, 0f);
            shineRt.anchoredPosition = new Vector2(-80f, 0f);
            shine.gameObject.AddComponent<UiSkew>();
            Image video = Img(spinBox, "Video", UiV2Sprite("Icons/video.png"), Color.white);
            Place(video.rectTransform, 0.5f, 0.5f, -34f, 2f, 40f, 33f);
            V2Text(spinBox, "Label", "Spin", 30f, true, Color.white, LilitaOutline("Green", H(0x1D5F12))).rectTransform.anchoredPosition = new Vector2(24f, 2f);
            Button spin = V2Button(spinBox);
            TextMeshProUGUI spinsText = V2Text(p, "SpinsLeft", "3 spins left today", 18f, false, SpinsTextColor);
            spinsText.fontStyle = FontStyles.Bold;
            Box(spinsText.rectTransform, -45f, 568f, 390f, 20f);
            AddSpinsLeftPill(spinsText.rectTransform); // light pill behind it: navy text on the dark blurred scene

            // "You won!" card over its own dim (above the panel).
            RectTransform won = Stretch(Rect(s.Root.transform, "Won"));
            Image wonDim = Img(won, "Dim", White, new Color(10f / 255f, 20f / 255f, 55f / 255f, 0.6f));
            Stretch(wonDim.rectTransform);
            wonDim.raycastTarget = true;
            RectTransform wonCard = V2Box(won, "Card", 28f, Color.white, H(0xE3EFFF), null, 4f, H(0x1B4FA0), 8f);
            Place(wonCard, 0.5f, 0.5f, 0f, 0f, 280f, 306f);
            TextMeshProUGUI wonTitle = V2Text(wonCard, "Title", "You won!", 26f, true, H(0x1B4FA0));
            Box(wonTitle.rectTransform, 0f, 28f, 280f, 32f);
            Image wonIcon = Img(wonCard, "Icon", coin, Color.white);
            wonIcon.preserveAspect = true;
            Box(wonIcon.rectTransform, 100f, 72f, 80f, 85f);
            TextMeshProUGUI wonAmount = V2Text(wonCard, "Amount", "+100", 40f, true, H(0xFFC531), LilitaOutline("Brown", H(0x7A4A16), 0.55f, -1.4f));
            Box(wonAmount.rectTransform, 0f, 167f, 280f, 48f);
            RectTransform collectBox = V2Box(wonCard, "CollectButton", 20f, H(0xA4F560), H(0x36A920), H(0x56D134), 3f, H(0x1D5F12), 6f, true);
            Box(collectBox, 30f, 225f, 220f, 56f);
            V2Text(collectBox, "Label", "Collect", 24f, true, Color.white, LilitaOutline("Green", H(0x1D5F12))).rectTransform.anchoredPosition = new Vector2(0f, 2f);
            Button collect = V2Button(collectBox);
            won.gameObject.SetActive(false);

            Wire(s.Root.GetComponent<LuckySpinPopup>(), ("wheel", wheel), ("rays", rays.rectTransform), ("bulbs", bulbs),
                ("spinRoot", spinRoot), ("spinButton", spin), ("spinGroup", spinGroup), ("spinShine", shineRt), ("spinsText", spinsText),
                ("wonRoot", won.gameObject), ("wonCard", wonCard), ("wonIcon", wonIcon), ("wonAmount", wonAmount),
                ("collectButton", collect), ("coinSprite", coin), ("vipSprite", UiV2Sprite("Boosters/vip.png")),
                ("arrangeSprite", UiV2Sprite("Boosters/arrange.png")), ("shuffleSprite", UiV2Sprite("Boosters/shuffle.png")));
            return s.Root;
        }
    }
}
