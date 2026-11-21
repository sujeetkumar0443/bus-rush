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
    /// Step R7: the UI v2 Level Failed / Out of Space screen (Docs/UI_v2_Design/LevelFailed.dc.html) as the
    /// "LevelFailedScreen" prefab, an inactive full-screen overlay in the Game scene. Design px, 390x844 frame.
    /// </summary>
    public static partial class UIBuilder
    {
        [MenuItem("Tools/Bus Rush/Build Level Failed (R7)")]
        public static void BuildLevelFailedOnly()
        {
            Load();
            Save(BuildLevelFailedScreen(), "LevelFailedScreen");
            CleanCatalog(); // drops the deleted OutOfSpacePopup
            AssetDatabase.SaveAssets();
            WireGame(AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/GameHUD.prefab"),
                     AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/UIManager.prefab"));
            EditorSceneManager.OpenScene($"{ScenesDir}/Boot.unity", OpenSceneMode.Single);
            Debug.Log("[UIBuilder] Level Failed built.");
        }

        public static void BuildLevelFailedBatch()
        {
            try
            {
                BuildLevelFailedOnly();
            }
            catch (Exception e)
            {
                Debug.LogError("[UIBuilder] FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }

        private static GameObject BuildLevelFailedScreen()
        {
            Color greenEdge = H(0x1D5F12), orangeEdge = H(0x8A3E05);

            var root = new GameObject("LevelFailedScreen", typeof(RectTransform));
            RootCanvas(root, 90);
            root.GetComponent<CanvasScaler>().matchWidthOrHeight = 1f;

            Image navy = Img(root.transform, "Navy", White, H(0x14204F));
            Stretch(navy.rectTransform);
            navy.raycastTarget = true; // blocks taps to the game
            RectTransform blurRt = Stretch(Rect(root.transform, "Blur"), -10f, -10f, -10f, -10f);
            RawImage blur = blurRt.gameObject.AddComponent<RawImage>();
            blur.raycastTarget = false;
            blur.enabled = false;
            Image overlay = Img(root.transform, "Overlay", White, new Color(40f / 255f, 12f / 255f, 40f / 255f, 0.7f));
            Stretch(overlay.rectTransform);

            RectTransform frame = Place(Rect(root.transform, "Frame"), 0.5f, 0.5f, 0f, 0f, RefWidth, RefHeight);

            // "OUT OF SPACE!" (0,70) 46 px red, dark red outline; subtitle (0,130).
            RectTransform title = Box(Rect(frame, "Title"), 0f, 70f, 390f, 52f);
            CanvasGroup titleGroup = title.gameObject.AddComponent<CanvasGroup>();
            V2Text(title, "Label", "OUT OF SPACE!", 46f, true, H(0xFF5A5F), LilitaOutline("DarkRed", H(0x5A0A10), 0.75f, -2f));
            RectTransform subtitle = Box(Rect(frame, "Subtitle"), 0f, 130f, 390f, 24f);
            CanvasGroup subtitleGroup = subtitle.gameObject.AddComponent<CanvasGroup>();
            TextMeshProUGUI sub = V2Text(subtitle, "Label", "All parking bays are full", 18f, false, Color.white);
            sub.fontStyle = FontStyles.Bold;

            // Hero (115,176) 160x290, sways around its bottom centre; tears (44,98) and (106,98), 8x12.
            RectTransform heroBox = Box(Rect(frame, "HeroBox"), 115f, 176f, 160f, 292f);
            Image hero = Img(heroBox, "Hero", UiV2Sprite("Characters/hero_sad.png"), Color.white);
            hero.preserveAspect = true;
            Box(hero.rectTransform, 0f, 0f, 160f, 290f, 0.5f, 0f);
            var tears = new Image[2];
            for (int i = 0; i < 2; i++)
            {
                tears[i] = Img(heroBox, "Tear" + i, Circle, H(0x7FD3FF));
                Box(tears[i].rectTransform, i == 0 ? 44f : 106f, 98f, 8f, 12f);
            }

            // Info box (45,494) 300x74: extra_bay icon + "Get 1 extra bay and keep playing!".
            RectTransform info = Box(Rect(frame, "InfoBox"), 45f, 494f, 300f, 74f);
            CanvasGroup infoGroup = info.gameObject.AddComponent<CanvasGroup>();
            Image infoBg = RoundImg(info, "Bg", 22f, new Color(1f, 1f, 1f, 0.18f));
            Stretch(infoBg.rectTransform);
            Image bayIcon = Img(info, "Icon", UiV2Sprite("Boosters/extra_bay.png"), Color.white);
            bayIcon.preserveAspect = true;
            Place(bayIcon.rectTransform, 0f, 0.5f, 16f, 0f, 36f, 52f, 0f, 0.5f);
            TextMeshProUGUI infoText = V2Text(info, "Text", "Get <color=#FFE27A><b>1 extra bay</b></color> and keep playing!", 16f, false,
                                              Color.white, null, TextAlignmentOptions.Left);
            infoText.textWrappingMode = TextWrappingModes.Normal;
            infoText.fontStyle = FontStyles.Bold;
            infoText.lineSpacing = -4f;
            Stretch(infoText.rectTransform, 64f, 4f, 16f, 4f);

            // Extra bay (45,594) 300x74: green, video icon, shine + pulse.
            RectTransform extraRoot = Box(Rect(frame, "ExtraBay"), 45f, 594f, 300f, 74f);
            RectTransform extraBox = V2Box(extraRoot, "Button", 26f, H(0xA4F560), H(0x36A920), H(0x56D134), 3f, greenEdge, 6f, true);
            Stretch(extraBox);
            RectTransform shineMask = Stretch(Rect(extraBox, "ShineMask"), 3f, 3f, 3f, 3f);
            shineMask.gameObject.AddComponent<RectMask2D>();
            Image shine = Img(shineMask, "Shine", null, new Color(1f, 1f, 1f, 0.35f));
            RectTransform shineRt = shine.rectTransform;
            shineRt.anchorMin = new Vector2(0f, 0f);
            shineRt.anchorMax = new Vector2(0f, 1f);
            shineRt.pivot = new Vector2(0f, 0.5f);
            shineRt.sizeDelta = new Vector2(40f, 0f);
            shineRt.anchoredPosition = new Vector2(-80f, 0f);
            shine.gameObject.AddComponent<UiSkew>();
            Image video = Img(extraBox, "Video", UiV2Sprite("Icons/video.png"), Color.white);
            Place(video.rectTransform, 0.5f, 0.5f, -64f, 2f, 40f, 33f);
            V2Text(extraBox, "Label", "Extra bay", 28f, true, Color.white, LilitaOutline("Green", greenEdge)).rectTransform.anchoredPosition = new Vector2(24f, 2f);
            Button extra = V2Button(extraBox);

            // Retry (45,690) 300x60 orange; Home (145,768) underlined text.
            RectTransform retryBox = V2Box(frame, "RetryButton", 22f, H(0xFFD36B), H(0xF07F12), H(0xFFA22E), 3f, orangeEdge, 6f, true);
            Box(retryBox, 45f, 690f, 300f, 60f);
            V2Text(retryBox, "Label", "Retry", 24f, true, Color.white, LilitaOutline("Orange", orangeEdge)).rectTransform.anchoredPosition = new Vector2(0f, 2f);
            Button retry = V2Button(retryBox);
            RectTransform homeRt = Box(Rect(frame, "HomeButton"), 145f, 768f, 100f, 40f);
            TextMeshProUGUI homeText = V2Text(homeRt, "Label", "Home", 17f, false, Color.white);
            homeText.fontStyle = FontStyles.Bold | FontStyles.Underline;
            Button home = V2Button(homeRt);

            // Toast ("Video not available").
            RectTransform safe = SafeArea(root.transform);
            RectTransform toast = V2Box(safe, "Toast", 23f, H(0x24345E), H(0x14204F), null, 3f, Color.white, 0f);
            Place(toast, 0.5f, 0.5f, 0f, 60f, 250f, 46f);
            toast.gameObject.AddComponent<CanvasGroup>();
            Wire(toast.gameObject.AddComponent<Toast>(), ("label", V2Text(toast, "Label", "", 20f, false, Color.white)));

            LevelFailedScreen screen = root.AddComponent<LevelFailedScreen>();
            Wire(screen, ("blur", blur), ("overlay", overlay), ("title", title), ("titleGroup", titleGroup), ("subtitle", subtitle),
                ("subtitleGroup", subtitleGroup), ("hero", hero.rectTransform), ("tears", tears), ("infoBox", info),
                ("infoGroup", infoGroup), ("extraRoot", extraRoot), ("extraButton", extra), ("extraShine", shineRt),
                ("retryButton", retry), ("homeButton", home));
            root.SetActive(false);
            return root;
        }
    }
}
