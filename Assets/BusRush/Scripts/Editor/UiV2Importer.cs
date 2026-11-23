using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BusRush.UI;
using TMPro;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Step R0: import settings for the UI v2 art pack (Assets/BusRush/Art/UI_v2), the 32-direction map bus sheet,
    /// the loading bar 9-slices, the Lilita One / Fredoka TMP font assets and the UiTheme asset.
    /// Safe to run again: everything is updated in place.
    /// </summary>
    public static class UiV2Importer
    {
        public const string ArtDir = "Assets/BusRush/Art/UI_v2";
        public const string FontsDir = "Assets/BusRush/Art/Fonts";
        public const string ThemePath = ArtDir + "/UiTheme.asset";
        public const string HeadingFontPath = FontsDir + "/LilitaOne UIv2 SDF.asset";
        public const string HeadingOutlinePath = FontsDir + "/LilitaOne UIv2 SDF Outline.mat";
        public const string BodyFontPath = FontsDir + "/Fredoka UIv2 SDF.asset";

        /// <summary>Latin (ASCII + Latin-1), digits and common symbols.</summary>
        private static readonly string Charset = BuildCharset();

        [MenuItem("Tools/Bus Rush/Import UI v2 Art (Step R0)")]
        public static void ImportAll()
        {
            AssetDatabase.Refresh();
            int textures = ApplyTextureSettings();
            TMP_FontAsset heading = MakeStaticFont("LilitaOne-Regular", HeadingFontPath);
            TMP_FontAsset body = MakeStaticFont("Fredoka-Variable", BodyFontPath);
            Material outline = MakeHeadingOutline(heading);
            RefreshOldLilitaAsset();
            MakeTheme(heading, outline, body);
            AssetDatabase.SaveAssets();
            Debug.Log($"[UiV2Importer] Done: {textures} textures, fonts + UiTheme created.");
        }

        public static void ImportAllBatch()
        {
            try
            {
                ImportAll();
            }
            catch (Exception e)
            {
                Debug.LogError("[UiV2Importer] FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }

        // ------------------------------------------------------------------ textures

        private static int ApplyTextureSettings()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ArtDir });
            int n = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);

                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.maxTextureSize = 2048;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.filterMode = FilterMode.Bilinear;

                string file = Path.GetFileName(path);
                if (file == "bar_frame.png") ti.spriteBorder = new Vector4(26, 26, 26, 26);
                else if (file == "bar_fill.png") ti.spriteBorder = new Vector4(20, 20, 20, 20);
                if (file == "bar_frame.png" || file == "bar_fill.png")
                {
                    var settings = new TextureImporterSettings();
                    ti.ReadTextureSettings(settings);
                    settings.spriteMeshType = SpriteMeshType.FullRect; // 9-slice needs full-rect meshes
                    ti.SetTextureSettings(settings);
                }

                ti.SaveAndReimport();
                n++;
            }
            return n;
        }

        // ------------------------------------------------------------------ fonts

        /// <summary>A static SDF font asset (padding 9) with the Latin + digits + symbols charset baked in.</summary>
        private static TMP_FontAsset MakeStaticFont(string file, string assetPath)
        {
            string ttfPath = $"{FontsDir}/{file}.ttf";
            var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (font == null) throw new InvalidOperationException("Font missing: " + ttfPath);

            // Only characters the font actually has are required to be in the atlas.
            FontEngine.InitializeFontEngine();
            FontEngine.LoadFontFace(font, 64);
            string required = new string(Charset.Where(c => FontEngine.TryGetGlyphIndex(c, out uint g) && g != 0).ToArray());

            (int w, int h)[] sizes = { (1024, 1024), (2048, 1024), (2048, 2048) };
            TMP_FontAsset fa = null;
            foreach ((int w, int h) in sizes)
            {
                fa = TMP_FontAsset.CreateFontAsset(font, 64, 9, GlyphRenderMode.SDFAA, w, h, AtlasPopulationMode.Dynamic, false);
                fa.TryAddCharacters(required, out string missing);
                if (string.IsNullOrEmpty(missing)) break;
                if (h == 2048) throw new InvalidOperationException($"{file}: glyphs do not fit: '{missing}'");
                UnityEngine.Object.DestroyImmediate(fa.atlasTexture);
                UnityEngine.Object.DestroyImmediate(fa.material);
                UnityEngine.Object.DestroyImmediate(fa);
            }

            fa.atlasPopulationMode = AtlasPopulationMode.Static;
            fa.name = Path.GetFileNameWithoutExtension(assetPath);

            // Re-created on every run (UiTheme is re-pointed below).
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null) AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.CreateAsset(fa, assetPath);
            fa.atlasTextures[0].name = fa.name + " Atlas";
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            fa.material.name = fa.name + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();

            int absent = Charset.Length - required.Length;
            Debug.Log($"[UiV2Importer] {fa.name}: {required.Length} glyphs, atlas {fa.atlasWidth}x{fa.atlasHeight}, " +
                      $"{absent} charset characters not in the font.");
            return fa;
        }

        /// <summary>Material preset for Lilita One: UiTheme title style (thin dark-blue outline, small shadow).</summary>
        private static Material MakeHeadingOutline(TMP_FontAsset font)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(HeadingOutlinePath);
            if (m == null)
            {
                m = new Material(font.material);
                AssetDatabase.CreateAsset(m, HeadingOutlinePath);
            }
            m.name = Path.GetFileNameWithoutExtension(HeadingOutlinePath);
            m.shader = font.material.shader;
            m.CopyPropertiesFromMaterial(font.material);
            m.SetTexture(ShaderUtilities.ID_MainTex, font.atlasTexture);
            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
            if (theme == null) theme = ScriptableObject.CreateInstance<UiTheme>();
            theme.ApplyTitleText(m, UiTheme.ShadeOf(theme.blueButton)); // title style: thin dark-blue outline + small shadow
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>The pack's LilitaOne-Regular.ttf replaced the old file: rebuild the old dynamic asset's glyph cache.</summary>
        private static void RefreshOldLilitaAsset()
        {
            var old = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontsDir + "/LilitaOne-Regular SDF.asset");
            if (old == null || old.atlasPopulationMode != AtlasPopulationMode.Dynamic) return;
            old.ClearFontAssetData(true);
            old.TryAddCharacters(" !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~", out string _);
            EditorUtility.SetDirty(old);
        }

        private static string BuildCharset()
        {
            var chars = new List<char>();
            for (char c = (char)0x20; c <= (char)0x7E; c++) chars.Add(c);
            for (char c = (char)0xA0; c <= (char)0xFF; c++) chars.Add(c);
            chars.AddRange("–—‘’‚“”„•…‰‹›€™←↑→↓★☆✓✔♥×");
            return new string(chars.Distinct().ToArray());
        }

        // ------------------------------------------------------------------ theme

        private static void MakeTheme(TMP_FontAsset heading, Material outline, TMP_FontAsset body)
        {
            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<UiTheme>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }
            theme.headingFont = heading;
            theme.headingOutline = outline;
            theme.bodyFont = body;
            EditorUtility.SetDirty(theme);
        }
    }
}
