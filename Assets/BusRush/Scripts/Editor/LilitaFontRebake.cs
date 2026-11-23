using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Tools > Bus Rush > Rebake Lilita UIv2 Font: checks the static "LilitaOne UIv2 SDF" atlas and, if the sampling size is
    /// below 72 or the padding below 9, re-bakes it IN PLACE (same asset, same GUID, so every material/prefab keeps working)
    /// at sampling size 90, padding 9, atlas 2048x2048, SDFAA, and leaves it Static (no runtime glyph generation, so Android
    /// never gets the black-text bug). Fallback fonts are untouched. Env BUSRUSH_FONT_FORCE=1 re-bakes even if the check passes.
    /// </summary>
    public static class LilitaFontRebake
    {
        private const string FontPath = "Assets/BusRush/Art/Fonts/LilitaOne UIv2 SDF.asset";
        private const string FontsDir = "Assets/BusRush/Art/Fonts";
        private const int PointSize = 90, Padding = 9, Atlas = 2048;

        [MenuItem("Tools/Bus Rush/Rebake Lilita UIv2 Font")]
        public static void Run() => Debug.Log(Rebake(false));

        public static void RunBatch()
        {
            string r = Rebake(System.Environment.GetEnvironmentVariable("BUSRUSH_FONT_FORCE") == "1");
            Debug.Log(r);
            File.WriteAllText("Temp/font_rebake.txt", r);
            EditorApplication.Exit(0);
        }

        private static string Describe(TMP_FontAsset f) =>
            $"sampling={f.faceInfo.pointSize} padding={f.atlasPadding} atlas={f.atlasWidth}x{f.atlasHeight} " +
            $"mode={f.atlasRenderMode} population={f.atlasPopulationMode} glyphs={f.characterTable.Count} atlases={f.atlasTextures.Length}";

        private static string Rebake(bool force)
        {
            var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (fa == null) return "[FontRebake] font asset not found: " + FontPath;
            var sb = new StringBuilder();
            sb.AppendLine("[FontRebake] before: " + Describe(fa));
            bool weak = fa.faceInfo.pointSize < 72 || fa.atlasPadding < 9 || fa.atlasWidth < Atlas;
            if (!weak && !force)
            {
                sb.AppendLine("[FontRebake] sampling >= 72 and padding >= 9: nothing to do.");
                return sb.ToString();
            }
            Font source = fa.sourceFontFile;
            if (source == null)
            {
                // Static assets often lose the editor reference to the .ttf: restore it (same family, Lilita One).
                source = AssetDatabase.LoadAssetAtPath<Font>(FontsDir + "/LilitaOne-Regular.ttf");
                if (source == null) return sb + "[FontRebake] source font LilitaOne-Regular.ttf not found; aborting.";
                var fix = new SerializedObject(fa);
                SerializedProperty sp = fix.FindProperty("m_SourceFontFile");
                if (sp == null) return sb + "[FontRebake] m_SourceFontFile missing; aborting.";
                sp.objectReferenceValue = source;
                SerializedProperty g = fix.FindProperty("m_CreationSettings.sourceFontFileGUID");
                if (g != null) g.stringValue = AssetDatabase.AssetPathToGUID(FontsDir + "/LilitaOne-Regular.ttf");
                fix.ApplyModifiedPropertiesWithoutUndo();
                sb.AppendLine("[FontRebake] restored source font reference: LilitaOne-Regular.ttf");
            }

            // Characters: everything already in the atlas + Latin, digits, Latin-1 and the common UI punctuation.
            var set = new System.Collections.Generic.SortedSet<uint>(fa.characterTable.Select(c => c.unicode));
            for (uint u = 32; u <= 126; u++) set.Add(u);
            for (uint u = 160; u <= 255; u++) set.Add(u);
            foreach (char ch in "‘’“”–—•…×✓★☆▶") set.Add(ch);
            string chars = new string(set.Select(u => (char)u).ToArray());

            // New bake settings (face info + creation settings), then rebuild the static atlas.
            var so = new SerializedObject(fa);
            SetInt(so, "m_FaceInfo.m_PointSize", PointSize);
            SetInt(so, "m_AtlasPadding", Padding);
            SetInt(so, "m_AtlasWidth", Atlas);
            SetInt(so, "m_AtlasHeight", Atlas);
            SetInt(so, "m_AtlasRenderMode", (int)GlyphRenderMode.SDFAA);
            SetInt(so, "m_CreationSettings.pointSize", PointSize);
            SetInt(so, "m_CreationSettings.padding", Padding);
            SetInt(so, "m_CreationSettings.atlasWidth", Atlas);
            SetInt(so, "m_CreationSettings.atlasHeight", Atlas);
            SetInt(so, "m_CreationSettings.renderMode", (int)GlyphRenderMode.SDFAA);
            so.ApplyModifiedPropertiesWithoutUndo();

            fa.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            fa.ClearFontAssetData(false);
            bool ok = fa.TryAddCharacters(chars, out string missing);
            sb.AppendLine($"[FontRebake] TryAddCharacters ok={ok} requested={chars.Length} missing=[{missing}]");
            fa.atlasPopulationMode = AtlasPopulationMode.Static; // static again: nothing is generated at runtime

            // Materials that point at this atlas carry their own copy of the atlas size / gradient scale.
            int mats = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { FontsDir }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m == null || !m.HasProperty(ShaderUtilities.ID_MainTex) || m.GetTexture(ShaderUtilities.ID_MainTex) != fa.atlasTexture) continue;
                m.SetFloat(ShaderUtilities.ID_GradientScale, Padding + 1);
                m.SetFloat(ShaderUtilities.ID_TextureWidth, Atlas);
                m.SetFloat(ShaderUtilities.ID_TextureHeight, Atlas);
                EditorUtility.SetDirty(m);
                mats++;
            }
            if (fa.material != null)
            {
                fa.material.SetFloat(ShaderUtilities.ID_GradientScale, Padding + 1);
                fa.material.SetFloat(ShaderUtilities.ID_TextureWidth, Atlas);
                fa.material.SetFloat(ShaderUtilities.ID_TextureHeight, Atlas);
                EditorUtility.SetDirty(fa.material);
            }

            EditorUtility.SetDirty(fa);
            foreach (Texture2D t in fa.atlasTextures) EditorUtility.SetDirty(t);
            AssetDatabase.SaveAssets();
            sb.AppendLine($"[FontRebake] materials updated: {mats}");
            sb.AppendLine("[FontRebake] after:  " + Describe(fa));
            return sb.ToString();
        }

        private static void SetInt(SerializedObject so, string path, int v)
        {
            SerializedProperty p = so.FindProperty(path);
            if (p == null) Debug.LogWarning("[FontRebake] property not found: " + path);
            else if (p.propertyType == SerializedPropertyType.Float) p.floatValue = v; // FaceInfo.m_PointSize is a float
            else p.intValue = v;
        }
    }
}
