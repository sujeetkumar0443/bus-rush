using System;
using System.IO;
using BusRush.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static BusRush.EditorTools.UIKit;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Home "Level" hero button: 290x85 (was 74), 10 px dark-green lip, lighter top band, thin white inner stroke, soft
    /// green glow, masked shine streak, corner sparkles, Play triangle + "Level N" in the theme button text style.
    /// <see cref="BuildLevelButton"/> is used by the full Home build and by the in-place patch
    /// (Tools > Bus Rush > Rebuild Home Level Button), which leaves every other Home change untouched.
    /// </summary>
    public static partial class UIBuilder
    {
        private const float LevelW = 290f, LevelH = 85f, LevelLip = 10f, LevelRadius = 28f;

        /// <summary>LevelPulse centre height above the safe-area bottom: the bottom edge stays where the 74 px button was.</summary>
        private static float LevelPulseY() => Mathf.Max(28f, RefHeight - 700f - 74f) + LevelH * 0.5f;

        [MenuItem("Tools/Bus Rush/Rebuild Home Level Button")]
        public static void RebuildHomeLevelButton()
        {
            Load();
            string path = $"{PrefabDir}/HomeScreen.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform safe = root.transform.Find("SafeArea");
                var pulse = (RectTransform)safe.Find("LevelPulse");
                for (int i = pulse.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(pulse.GetChild(i).gameObject);
                pulse.sizeDelta = new Vector2(LevelW, LevelH);
                pulse.anchoredPosition = new Vector2(0f, LevelPulseY());

                var built = BuildLevelButton(pulse);
                Wire(root.GetComponent<HomeScreen>(), ("levelButton", built.button), ("levelText", built.text));

                // Side buttons: 6 px more gap between the icon frame and its label (hit area grows with it).
                foreach (string side in new[] { "DailyButton", "RoadMapButton", "LuckySpinButton" })
                {
                    var wrap = (RectTransform)safe.Find(side);
                    wrap.sizeDelta = new Vector2(wrap.sizeDelta.x, SideWrapHeight);
                    var label = (RectTransform)wrap.Find("Label");
                    label.anchoredPosition = new Vector2(label.anchoredPosition.x, -SideLabelY);
                    label.sizeDelta = new Vector2(SideLabelWidth, label.sizeDelta.y);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[UIBuilder] Home level button rebuilt in HomeScreen.prefab.");
        }

        public static void RebuildHomeLevelButtonBatch()
        {
            try
            {
                RebuildHomeLevelButton();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[UIBuilder] FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }

        // Side-button label: frame 76 + 5 lip + 1 = 82 before; 6 px more now. Label rect is only as wide as the text needs
        // (110 reached past the screen edge for the buttons that sit 12 px from it).
        private const float SideLabelY = 88f, SideWrapHeight = 112f, SideLabelWidth = 96f;

        // ------------------------------------------------------------------ the button

        private static (Button button, TextMeshProUGUI text, HomeLevelButtonFx fx) BuildLevelButton(RectTransform pulse)
        {
            Color top = H(0xA4F560), mid = H(0x56D134), bottom = H(0x36A920), edge = H(0x1D5F12);
            Sprite sparkle = GeneratedSprite("ui_sparkle", 64, 64, PaintSparkle);
            Sprite play = GeneratedSprite("ui_play", 64, 64, PaintPlay);
            Sprite streakSprite = GeneratedSprite("ui_streak", 128, 8, PaintStreak);

            // Soft green glow under the whole button (outside the squashed button, so it does not sink when pressed).
            Image glow = Img(pulse, "Glow", UIKit.Shadow, new Color(0.34f, 0.85f, 0.2f, 0.55f));
            glow.type = Image.Type.Sliced;
            glow.pixelsPerUnitMultiplier = 3f;
            Stretch(glow.rectTransform, -16f, -(LevelLip + 20f), -16f, 22f);
            glow.raycastTarget = false;

            RectTransform btn = Stretch(Rect(pulse, "LevelButton"));

            Image drop = Img(btn, "DropShadow", UIKit.Shadow, new Color(10f / 255f, 20f / 255f, 50f / 255f, 0.3f));
            drop.type = Image.Type.Sliced;
            drop.pixelsPerUnitMultiplier = 4f;
            Stretch(drop.rectTransform, -7f, -(LevelLip + 9f), -7f, 1f);

            Stretch(RoundImg(btn, "Lip", LevelRadius, edge).rectTransform, 0f, -LevelLip, 0f, LevelLip);   // thick dark-green lip
            Stretch(RoundImg(btn, "Border", LevelRadius, edge).rectTransform);
            Stretch(RoundImg(btn, "Stroke", LevelRadius - 1f, new Color(1f, 1f, 1f, 0.92f)).rectTransform, 3f, 3f, 3f, 3f); // thin white inner stroke
            const float s = 5f; // border + stroke
            Stretch(RoundImg(btn, "Shade", LevelRadius - s, Color.Lerp(bottom, Color.black, 0.13f)).rectTransform, s, s, s, s);
            Stretch(RoundImg(btn, "TopBand", LevelRadius - s, Color.Lerp(top, Color.white, 0.55f)).rectTransform, s, s + 9f, s, s); // lighter top band
            Image face = RoundImg(btn, "Face", LevelRadius - s, Color.white);
            Stretch(face.rectTransform, s, s + 9f, s, s + 6f);
            Gradient(face, Color.Lerp(top, mid, 0.12f), bottom, mid);

            Image gloss = RoundImg(btn, "Gloss", LevelRadius - s, new Color(1f, 1f, 1f, 0.14f));
            gloss.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            gloss.rectTransform.anchorMax = new Vector2(1f, 1f);
            gloss.rectTransform.offsetMin = new Vector2(s + 5f, 0f);
            gloss.rectTransform.offsetMax = new Vector2(-(s + 5f), -(s + 9f));

            // Shine: the streak lives under a Mask with the face's shape, so it can never leave the button.
            Image maskImg = RoundImg(btn, "ShineMask", LevelRadius - s, Color.white);
            Stretch(maskImg.rectTransform, s, s + 9f, s, s + 6f);
            maskImg.raycastTarget = false;
            maskImg.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            RectTransform streak = Rect(maskImg.rectTransform, "Streak");
            Place(streak, 0.5f, 0.5f, -LevelW, 0f, 90f, LevelH * 1.9f);
            streak.localRotation = Quaternion.Euler(0f, 0f, -20f);
            Image wide = Img(streak, "Soft", streakSprite, new Color(1f, 1f, 1f, 0.34f));
            Place(wide.rectTransform, 0.5f, 0.5f, 0f, 0f, 62f, LevelH * 1.9f);
            Image core = Img(streak, "Core", streakSprite, new Color(1f, 1f, 1f, 0.6f));
            Place(core.rectTransform, 0.5f, 0.5f, 40f, 0f, 14f, LevelH * 1.9f);
            wide.raycastTarget = core.raycastTarget = false;
            streak.gameObject.SetActive(false);

            // Content: Play triangle, then "Level N", centred as one group (layout rebuilds only when the text changes).
            RectTransform content = Stretch(Rect(btn, "Content"), 10f, s + 9f, 10f, s + 6f);
            var group = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.childAlignment = TextAnchor.MiddleCenter;
            group.spacing = 12f;
            group.childControlWidth = true;
            group.childControlHeight = false;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;

            RectTransform icon = Rect(content, "Play");
            icon.sizeDelta = new Vector2(34f, 38f);
            var iconSize = icon.gameObject.AddComponent<LayoutElement>();
            iconSize.preferredWidth = 34f;
            iconSize.preferredHeight = 38f;
            Image iconShadow = Img(icon, "Shadow", play, new Color(edge.r, edge.g, edge.b, 0.65f));
            Stretch(iconShadow.rectTransform, 0f, -3f, 0f, 3f);
            Img(icon, "Icon", play, Color.white);
            Stretch((RectTransform)icon.Find("Icon"));
            iconShadow.raycastTarget = false;
            icon.Find("Icon").GetComponent<Image>().raycastTarget = false;

            // Text: the theme's button style (white face, soft dark-green shadow) through the Green button material.
            TextMeshProUGUI label = V2Text(content, "Label", "Level 1", 39f, true, Color.white, LilitaOutline("Green", edge));
            Place(label.rectTransform, 0.5f, 0.5f, 0f, 0f, 150f, 56f);
            label.alignment = TextAlignmentOptions.Center;

            // Corner sparkles (twinkle at random times; outside the mask, so they can sit on the corners).
            RectTransform[] sparkles =
            {
                Corner(btn, "SparkleTL", sparkle, 0f, 1f, 22f, -6f, 22f),
                Corner(btn, "SparkleTR", sparkle, 1f, 1f, -18f, -8f, 16f),
                Corner(btn, "SparkleBR", sparkle, 1f, 0f, -34f, 20f, 19f),
            };

            Button button = V2Button(btn);
            Wire(btn.GetComponent<UIButton>(), ("pressedScale", 0.94f), ("pushDepth", 6f));

            var fx = btn.gameObject.AddComponent<HomeLevelButtonFx>();
            Wire(fx, ("streak", streak), ("sparkles", sparkles));
            return (button, label, fx);
        }

        private static RectTransform Corner(RectTransform parent, string name, Sprite sprite, float ax, float ay, float x, float y, float size)
        {
            Image img = Img(parent, name, sprite, new Color(1f, 1f, 1f, 0.95f));
            img.raycastTarget = false;
            Place(img.rectTransform, ax, ay, x, y, size, size);
            img.rectTransform.localScale = Vector3.zero;
            return img.rectTransform;
        }

        // ------------------------------------------------------------------ generated sprites

        private static Sprite GeneratedSprite(string name, int w, int h, Func<float, float, float> alpha)
        {
            string path = $"{GeneratedDir}/{name}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            Directory.CreateDirectory(GeneratedDir);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float a = Mathf.Clamp01(alpha((x + 0.5f) / w * 2f - 1f, (y + 0.5f) / h * 2f - 1f));
                    px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(px);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 100f;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Four-point star: concave astroid with a soft round core. (x, y) in -1..1.</summary>
        private static float PaintSparkle(float x, float y)
        {
            float f = Mathf.Sqrt(Mathf.Abs(x)) + Mathf.Sqrt(Mathf.Abs(y));
            float star = Mathf.SmoothStep(1f, 0.55f, f);
            float core = Mathf.SmoothStep(0.34f, 0f, Mathf.Sqrt(x * x + y * y));
            return Mathf.Max(star, core * 0.9f);
        }

        /// <summary>Right-pointing triangle with rounded corners (distance to a slightly smaller triangle, minus a radius).</summary>
        private static float PaintPlay(float x, float y)
        {
            var a = new Vector2(-0.38f, -0.5f);
            var b = new Vector2(-0.38f, 0.5f);
            var c = new Vector2(0.52f, 0f);
            Vector2 p = new Vector2(x, y);
            float d = DistanceToTriangle(p, a, b, c);
            const float r = 0.17f;
            return Mathf.Clamp01((r - d) / 0.05f + 0.5f);
        }

        private static float DistanceToTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;
            bool inside = Cross(b - a, p - a) <= 0f == (Cross(c - b, p - b) <= 0f) && Cross(c - b, p - b) <= 0f == (Cross(a - c, p - c) <= 0f);
            if (inside) return 0f;
            return Mathf.Min(Seg(p, a, b), Mathf.Min(Seg(p, b, c), Seg(p, c, a)));
        }

        private static float Seg(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        /// <summary>Soft horizontal band: transparent at both sides, brightest in the middle (also fades along its length).</summary>
        private static float PaintStreak(float x, float y)
        {
            float u = 1f - Mathf.Abs(x);
            return u * u * (3f - 2f * u);
        }
    }
}
