using System;
using BusRush.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static BusRush.EditorTools.UIKit;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Step R1: the UI v2 splash (Docs/UI_v2_Design/Splash.dc.html, Art/UI_v2/Splash/LAYOUT.txt) as the "Splash"
    /// prefab in the Boot scene. Positions are the design's px in its 390x844 frame (origin top-left).
    /// </summary>
    public static partial class UIBuilder
    {
        private const string UiV2 = "Assets/BusRush/Art/UI_v2/";

        [MenuItem("Tools/Bus Rush/Build Splash (R1)")]
        public static void BuildSplashOnly()
        {
            Load();
            GameObject splash = Save(BuildSplash(), "Splash");
            AssetDatabase.SaveAssets();
            WireBoot(splash);
            AssetDatabase.SaveAssets();
            Debug.Log("[UIBuilder] Splash built.");
        }

        public static void BuildSplashBatch()
        {
            try
            {
                BuildSplashOnly();
            }
            catch (Exception e)
            {
                Debug.LogError("[UIBuilder] FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }

        private static Sprite UiV2Sprite(string path)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(UiV2 + path);
            if (s == null) throw new InvalidOperationException("Sprite missing: " + UiV2 + path + " (run Import UI v2 Art first)");
            return s;
        }

        /// <summary>Places rt by the design's top-left box (left, top, w, h) inside a parent of known size; pivot = CSS transform-origin.</summary>
        private static RectTransform Box(RectTransform rt, float left, float top, float w, float h, float px = 0.5f, float py = 0.5f)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(px, py);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(left + px * w, -(top + (1f - py) * h));
            return rt;
        }

        private static GameObject BuildSplash()
        {
            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(UiV2Importer.ThemePath);
            if (theme == null || theme.headingFont == null) throw new InvalidOperationException("UiTheme missing: run Import UI v2 Art first");

            var root = new GameObject("Splash", typeof(RectTransform));
            RootCanvas(root, 10);
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.matchWidthOrHeight = 1f; // match height: the 844 px design always fits vertically

            // 1. Background: fills the whole screen (cover, centred), ignoring the safe area.
            RectTransform bgArea = Stretch(Rect(root.transform, "Background"));
            Image backing = bgArea.gameObject.AddComponent<Image>();
            backing.color = UIArtGenerator.Hex(0x4AB3F4);
            backing.raycastTarget = false;
            Image bg = Img(bgArea, "SplashBg", UiV2Sprite("Backgrounds/splash_bg.png"), Color.white);
            AspectRatioFitter cover = bg.gameObject.AddComponent<AspectRatioFitter>();
            cover.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            cover.aspectRatio = 941f / 1672f;

            // 2. Sun rays: 1000x1000, centre at (-40,-40) from the screen's top-left corner (texture made at runtime).
            RectTransform raysRt = Rect(root.transform, "Rays");
            RawImage rays = raysRt.gameObject.AddComponent<RawImage>();
            rays.raycastTarget = false;
            Place(raysRt, 0f, 1f, -40f, 40f, 1000f, 1000f);

            // The 390x844 design frame, centred (height always 844 with match-height).
            RectTransform frame = Place(Rect(root.transform, "Frame"), 0.5f, 0.5f, 0f, 0f, RefWidth, RefHeight);

            // 3-4. Bus + shadow in the "drive" box (left 52, top 500, 300x270, origin 50% 90%).
            RectTransform drive = Box(Rect(frame, "Drive"), 52f, 500f, 300f, 270f, 0.5f, 0.1f);
            CanvasGroup driveGroup = drive.gameObject.AddComponent<CanvasGroup>();
            driveGroup.blocksRaycasts = false;
            Image shadow = Img(drive, "BusShadow", UiV2Sprite("Splash/bus_shadow.png"), new Color(1f, 1f, 1f, 0.45f));
            Box(shadow.rectTransform, 20f, 222f, 260f, 40f);
            Image bus = Img(drive, "Bus", UiV2Sprite("Characters/bus_passengers.png"), Color.white);
            Box(bus.rectTransform, 0f, 0f, 300f, 252f, 0.5f, 0.05f); // origin 50% 95%

            // 5. Logo (left 56, top 42, 277x299); the inner image breathes around 50% 60%.
            RectTransform logoIn = Box(Rect(frame, "LogoIn"), 56f, 42f, 277f, 299f);
            CanvasGroup logoGroup = logoIn.gameObject.AddComponent<CanvasGroup>();
            logoGroup.blocksRaycasts = false;
            Image logo = Img(logoIn, "Logo", UiV2Sprite("Splash/splash_logo.png"), Color.white);
            Box(logo.rectTransform, 0f, 0f, 277f, 299f, 0.5f, 0.4f);

            // 6. Sparkles.
            Sprite sparkle = UiV2Sprite("Splash/sparkle.png");
            Color cream = UIArtGenerator.Hex(0xFFF6B0);
            (float x, float y, float size, Color color)[] sparkleSpec =
            {
                (48f, 120f, 22f, Color.white), (326f, 96f, 16f, cream), (330f, 300f, 20f, Color.white), (70f, 330f, 14f, cream)
            };
            var sparkles = new Image[sparkleSpec.Length];
            for (int i = 0; i < sparkleSpec.Length; i++)
            {
                sparkles[i] = Img(frame, "Sparkle" + i, sparkle, sparkleSpec[i].color);
                Box(sparkles[i].rectTransform, sparkleSpec[i].x, sparkleSpec[i].y, sparkleSpec[i].size, sparkleSpec[i].size);
            }

            // 7. Loading bar + text: bottom-anchored inside the safe area (design: bar top 774, text centre 808 of 844).
            RectTransform safe = SafeArea(root.transform);
            Image barFrame = Img(safe, "LoadingBar", UiV2Sprite("Splash/bar_frame.png"), Color.white);
            barFrame.type = Image.Type.Sliced;
            barFrame.pixelsPerUnitMultiplier = 52f / 26f; // 52 px art shown 26 high
            Place(barFrame.rectTransform, 0.5f, 0f, 0f, RefHeight - 774f - 26f, 310f, 26f, 0.5f, 0f);
            RectTransform fillArea = Stretch(Rect(barFrame.transform, "FillArea"), 3f, 3f, 3f, 3f);
            Image fill = Img(fillArea, "Fill", UiV2Sprite("Splash/bar_fill.png"), Color.white);
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = 40f / 20f;
            RectTransform fillRt = fill.rectTransform;
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.anchoredPosition = Vector2.zero;
            fillRt.sizeDelta = new Vector2(20f, 0f);
            fill.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            Image shine = Img(fillRt, "Shine", null, new Color(1f, 1f, 1f, 0.55f));
            RectTransform shineRt = shine.rectTransform;
            shineRt.anchorMin = new Vector2(0f, 0f);
            shineRt.anchorMax = new Vector2(0f, 1f);
            shineRt.pivot = new Vector2(0f, 0.5f);
            shineRt.anchoredPosition = new Vector2(-120f, 0f);
            shineRt.sizeDelta = new Vector2(40f, 0f);
            shine.gameObject.AddComponent<UiSkew>();

            string text = theme.headingFont.HasCharacter('…') ? "Loading…" : "Loading...";
            TextMeshProUGUI loading = Label(safe, "LoadingText", text, 17f, TextStyle.Heading, Color.white);
            loading.font = theme.headingFont;
            loading.fontSharedMaterial = theme.headingOutline != null ? theme.headingOutline : theme.headingFont.material;
            loading.enableAutoSizing = false;
            loading.fontSize = 17f;
            Place(loading.rectTransform, 0.5f, 0f, 0f, RefHeight - 808f, 310f, 26f, 0.5f, 0.5f);

            SplashController controller = root.AddComponent<SplashController>();
            Wire(controller, ("rays", rays), ("drive", drive), ("driveGroup", driveGroup), ("bus", bus.rectTransform),
                ("shadow", shadow.rectTransform), ("shadowImage", shadow), ("logoIn", logoIn), ("logoGroup", logoGroup),
                ("logo", logo.rectTransform), ("sparkles", sparkles), ("fillArea", fillArea), ("fill", fillRt),
                ("shine", shineRt), ("loadingText", loading));
            return root;
        }

        /// <summary>Boot scene: only this splash (old splash roots removed), a plain clear camera, BootLoader wired.</summary>
        private static void WireBoot(GameObject splash)
        {
            Scene boot = EditorSceneManager.OpenScene($"{ScenesDir}/Boot.unity", OpenSceneMode.Single);
            RemoveRoots(boot, "Splash", "Canvas", "HeroStage");
            var splashInstance = (GameObject)PrefabUtility.InstantiatePrefab(splash, boot);
            Camera bootCam = FindRoot(boot, "Main Camera").GetComponent<Camera>();
            bootCam.orthographic = true;
            bootCam.clearFlags = CameraClearFlags.SolidColor;
            bootCam.backgroundColor = UIArtGenerator.Hex(0x4AB3F4);
            bootCam.cullingMask = 1 << 5; // UI only: nothing 3D to draw (screenshot capture renders the canvas through it)
            bootCam.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);
            BootLoader loader = FindRoot(boot, "Boot").GetComponent<BootLoader>();
            Wire(loader, ("splash", splashInstance.GetComponent<SplashController>()));
            EditorSceneManager.SaveScene(boot);
        }
    }
}
