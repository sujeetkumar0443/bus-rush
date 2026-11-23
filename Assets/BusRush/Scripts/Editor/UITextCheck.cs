using BusRush.UI;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Tools > Bus Rush > Check UI Text: lists every TMP text (UI prefabs + the three scenes) with a black / invisible
    /// face colour, a missing font or material, a material whose atlas is not its font's atlas, a font whose glyphs are
    /// generated on the device (dynamic atlas cleared on build), or a shader that is neither always included nor safe.
    /// </summary>
    public static class UITextCheck
    {
        private static readonly string[] Roots = { "Assets/BusRush/Prefabs", "Assets/BusRush/Scenes" };

        [MenuItem("Tools/Bus Rush/Check UI Text")]
        public static void Run()
        {
            string report = Check(out int problems);
            Debug.Log(report);
            if (!Application.isBatchMode) EditorUtility.DisplayDialog("Check UI Text", problems == 0 ? "No problems found." : $"{problems} problem(s): see the Console.", "OK");
        }

        public static void RunBatch()
        {
            string report = Check(out int problems);
            Debug.Log(report);
            File.WriteAllText("Temp/ui_text_check.txt", report);
            EditorApplication.Exit(problems == 0 ? 0 : 1); // any problem (including a missing glyph) fails the run
        }

        public static string Check(out int problems)
        {
            var sb = new StringBuilder("[UITextCheck] ");
            var issues = new List<string>();
            var included = AlwaysIncludedShaders();
            var shaderNotIncluded = new HashSet<string>();
            var fonts = new HashSet<TMP_FontAsset>();
            int texts = 0;

            void Inspect(TMP_Text t, string where)
            {
                texts++;
                string path = where + " / " + PathOf(t.transform);
                if (t.font == null) issues.Add($"{path}: no font asset");
                else
                {
                    fonts.Add(t.font);
                    string missing = MissingGlyphs(t.font, t.text);
                    if (missing.Length > 0) issues.Add($"{path}: character(s) missing from font '{t.font.name}' (and its fallbacks): {Describe(missing)} in \"{t.text}\"");
                }
                Material m = t.fontSharedMaterial;
                if (m == null)
                {
                    issues.Add($"{path}: no material");
                    return;
                }
                Color face = t.color;
                if (face.a > 0.01f && face.maxColorComponent < 0.08f) issues.Add($"{path}: black face colour {face}");
                if (m.HasProperty(ShaderUtilities.ID_FaceColor))
                {
                    Color mf = m.GetColor(ShaderUtilities.ID_FaceColor);
                    if (mf.maxColorComponent < 0.08f) issues.Add($"{path}: material '{m.name}' has a black _FaceColor");
                }
                if (m.IsKeywordEnabled(ShaderUtilities.Keyword_Outline) && m.HasProperty(ShaderUtilities.ID_OutlineColor) &&
                    m.GetColor(ShaderUtilities.ID_OutlineColor).maxColorComponent < 0.08f && m.GetFloat(ShaderUtilities.ID_OutlineWidth) > 0f)
                {
                    issues.Add($"{path}: material '{m.name}' has a black outline");
                }
                if (m.IsKeywordEnabled(ShaderUtilities.Keyword_Underlay) && m.HasProperty(ShaderUtilities.ID_UnderlayColor) &&
                    m.GetColor(ShaderUtilities.ID_UnderlayColor).maxColorComponent < 0.08f)
                {
                    issues.Add($"{path}: material '{m.name}' has a black underlay (shadow)");
                }
                if (m.shader == null || !m.shader.isSupported) issues.Add($"{path}: material '{m.name}' shader missing/unsupported");
                else if (!included.Contains(m.shader) && !m.shader.name.StartsWith("TextMeshPro/"))
                {
                    issues.Add($"{path}: shader '{m.shader.name}' (material '{m.name}') is not in Always Included Shaders and not a TMP shader");
                }
                else if (!included.Contains(m.shader)) shaderNotIncluded.Add(m.shader.name);
                if (t.font != null && t.font.atlasPopulationMode != AtlasPopulationMode.Static && m != t.font.material)
                {
                    issues.Add($"{path}: preset material '{m.name}' on DYNAMIC font '{t.font.name}' - on a device it keeps sampling the cleared build atlas (renders as dark blocks)");
                }
                if (t.font != null && m.mainTexture != t.font.atlasTexture)
                {
                    issues.Add($"{path}: material '{m.name}' samples '{(m.mainTexture != null ? m.mainTexture.name : "nothing")}', not the font atlas '{t.font.atlasTexture?.name}'");
                }
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { Roots[0] }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                foreach (TMP_Text t in prefab.GetComponentsInChildren<TMP_Text>(true)) Inspect(t, Path.GetFileNameWithoutExtension(p));
            }
            string active = SceneManager.GetActiveScene().path;
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { Roots[1] }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                Scene s = EditorSceneManager.OpenScene(p, OpenSceneMode.Single);
                foreach (GameObject go in s.GetRootGameObjects())
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(go)) continue; // prefabs were checked above
                    foreach (TMP_Text t in go.GetComponentsInChildren<TMP_Text>(true)) Inspect(t, Path.GetFileNameWithoutExtension(p));
                }
            }
            if (!string.IsNullOrEmpty(active)) EditorSceneManager.OpenScene(active, OpenSceneMode.Single);

            foreach (TMP_FontAsset f in fonts.Where(f => f != null))
            {
                if (f.atlasPopulationMode != AtlasPopulationMode.Static)
                {
                    bool cleared = new SerializedObject(f).FindProperty("m_ClearDynamicDataOnBuild")?.boolValue ?? false;
                    issues.Add($"font '{f.name}': DYNAMIC atlas{(cleared ? " cleared on build" : "")} - glyphs are rasterised on the device at runtime");
                }
                if (f.atlasTexture == null) issues.Add($"font '{f.name}': no atlas texture");
                foreach (TMP_FontAsset fb in f.fallbackFontAssetTable ?? new List<TMP_FontAsset>())
                {
                    if (fb != null && fb.atlasPopulationMode != AtlasPopulationMode.Static) issues.Add($"font '{f.name}': fallback '{fb.name}' is dynamic");
                }
            }

            CheckCodeStrings(issues);

            foreach (string sh in shaderNotIncluded) issues.Add($"shader '{sh}' is not in Always Included Shaders (run Fix UI Text)");
            problems = issues.Count;
            sb.Append($"{texts} texts, {fonts.Count} fonts, {problems} problem(s)");
            foreach (string i in issues.Distinct()) sb.Append("\n  - ").Append(i);
            return sb.ToString();
        }

        // ------------------------------------------------------------------ missing glyphs

        private static readonly System.Text.RegularExpressions.Regex RichTag = new System.Text.RegularExpressions.Regex("<[^>]*>");

        /// <summary>Characters of <paramref name="text"/> (rich-text tags removed) that the font and its fallbacks cannot draw.</summary>
        private static string MissingGlyphs(TMP_FontAsset font, string text)
        {
            if (font == null || string.IsNullOrEmpty(text)) return "";
            var missing = new StringBuilder();
            foreach (char ch in RichTag.Replace(text, ""))
            {
                if (char.IsWhiteSpace(ch) || char.IsControl(ch) || char.IsSurrogate(ch)) continue;
                if (!font.HasCharacter(ch, true, true) && missing.ToString().IndexOf(ch) < 0) missing.Append(ch);
            }
            return missing.ToString();
        }

        private static string Describe(string chars) => string.Join(" ", chars.Select(c => $"'{c}' U+{(int)c:X4}"));

        /// <summary>Non-ASCII characters in string literals of runtime scripts must exist in the heading and body fonts.</summary>
        private static void CheckCodeStrings(List<string> issues)
        {
            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>("Assets/BusRush/Art/UI_v2/UiTheme.asset");
            if (theme == null) return;
            var fontsToTest = new[] { theme.headingFont, theme.bodyFont }.Where(f => f != null).ToArray();
            var literal = new System.Text.RegularExpressions.Regex(@"""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)'");
            foreach (string file in Directory.GetFiles("Assets/BusRush/Scripts", "*.cs", SearchOption.AllDirectories))
            {
                if (file.Replace('\\', '/').Contains("/Editor/")) continue;
                int line = 0;
                foreach (string src in File.ReadAllLines(file))
                {
                    line++;
                    string code = src.TrimStart();
                    if (code.StartsWith("//") || code.StartsWith("*") || code.StartsWith("///")) continue;
                    foreach (System.Text.RegularExpressions.Match m in literal.Matches(src))
                    {
                        string v = m.Value;
                        foreach (TMP_FontAsset f in fontsToTest)
                        {
                            string missing = MissingGlyphs(f, new string(v.Where(c => c > 0x7E).ToArray()));
                            if (missing.Length > 0) issues.Add($"{file.Replace('\\', '/')}:{line}: string literal uses {Describe(missing)}, missing from font '{f.name}'");
                        }
                    }
                }
            }
        }

        // ------------------------------------------------------------------ fix

        private const string FontsDir = "Assets/BusRush/Art/Fonts";

        [MenuItem("Tools/Bus Rush/Fix UI Text (bake fonts, include shaders)")]
        public static void Fix()
        {
            Debug.Log(FixAll());
        }

        public static void FixBatch()
        {
            Debug.Log(FixAll());
            Debug.Log(Check(out _));
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// Bakes every dynamic TMP font asset into a static atlas (Latin + digits + symbols, no "clear on build"), points
        /// every material preset of a font at its atlas, switches TMP materials to the Mobile SDF shader and adds the TMP
        /// SDF shaders to Always Included Shaders.
        /// </summary>
        public static string FixAll()
        {
            var sb = new StringBuilder("[UITextCheck] Fix:");
            string chars = Charset();
            Shader mobile = Shader.Find("TextMeshPro/Mobile/Distance Field");
            foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { FontsDir }))
            {
                var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (f == null) continue;
                if (f.atlasPopulationMode != AtlasPopulationMode.Static)
                {
                    f.TryAddCharacters(chars, out string missing);
                    var so = new SerializedObject(f);
                    so.FindProperty("m_AtlasPopulationMode").intValue = (int)AtlasPopulationMode.Static;
                    SerializedProperty clear = so.FindProperty("m_ClearDynamicDataOnBuild");
                    if (clear != null) clear.boolValue = false;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    sb.AppendLine().Append($"  baked '{f.name}' to a static atlas ({f.characterTable.Count} glyphs, {f.atlasTextures.Length} atlas)");
                }
                if (mobile != null && f.material != null && f.material.shader != mobile)
                {
                    f.material.shader = mobile;
                    sb.AppendLine().Append($"  '{f.material.name}' -> Mobile SDF");
                }
                EditorUtility.SetDirty(f);
            }

            // Presets (outline colour variants) sample their font's atlas.
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { FontsDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                string stem = Path.GetFileNameWithoutExtension(path);
                foreach (string fguid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { FontsDir }))
                {
                    var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(fguid));
                    if (f == null || !stem.StartsWith(f.name + " ")) continue;
                    if (m.GetTexture(ShaderUtilities.ID_MainTex) != f.atlasTexture)
                    {
                        m.SetTexture(ShaderUtilities.ID_MainTex, f.atlasTexture);
                        sb.AppendLine().Append($"  '{m.name}' -> atlas of '{f.name}'");
                    }
                    if (mobile != null && m.shader != mobile)
                    {
                        m.shader = mobile;
                        sb.AppendLine().Append($"  '{m.name}' -> Mobile SDF");
                    }
                    EditorUtility.SetDirty(m);
                }
            }

            // Always Included Shaders: TMP Mobile SDF + SDF (cheap insurance against stripping).
            var gs = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
            var gso = new SerializedObject(gs);
            SerializedProperty list = gso.FindProperty("m_AlwaysIncludedShaders");
            foreach (string name in new[] { "TextMeshPro/Mobile/Distance Field", "TextMeshPro/Distance Field" })
            {
                Shader sh = Shader.Find(name);
                if (sh == null) continue;
                bool has = false;
                for (int i = 0; i < list.arraySize; i++) has |= list.GetArrayElementAtIndex(i).objectReferenceValue == sh;
                if (has) continue;
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = sh;
                sb.AppendLine().Append($"  always include '{name}'");
            }
            gso.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return sb.ToString();
        }

        private static string Charset()
        {
            var c = new StringBuilder();
            for (char ch = (char)0x20; ch <= (char)0x7E; ch++) c.Append(ch);
            for (char ch = (char)0xA0; ch <= (char)0xFF; ch++) c.Append(ch);
            c.Append("–—‘’“”•…›★☆×");
            return c.ToString();
        }

        private static HashSet<Shader> AlwaysIncludedShaders()
        {
            var set = new HashSet<Shader>();
            var gs = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
            var so = new SerializedObject(gs);
            SerializedProperty list = so.FindProperty("m_AlwaysIncludedShaders");
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue is Shader s) set.Add(s);
            }
            return set;
        }

        private static string PathOf(Transform t)
        {
            var parts = new List<string>();
            for (; t != null; t = t.parent) parts.Insert(0, t.name);
            return string.Join("/", parts.Skip(1));
        }
    }
}
