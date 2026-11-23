using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BusRush.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Tools > Bus Rush > Setup Localization (run after Tools/Localization/build_localization.py):
    ///   1. bakes the pre-shaped Hindi cluster font into a STATIC SDF font asset ("HindiClusters SDF", 2048 atlas, SDFAA,
    ///      Mobile SDF shader, no dynamic population: no black-text bug on Android),
    ///   2. adds it to the fallback table of every UI font (Lilita One, Fredoka, Nunito),
    ///   3. puts a LocalizedText on every static TMP text of the prefabs and scenes whose English text is in the string table.
    /// Texts a script writes at runtime are not touched: those scripts call Localizer.T / Localizer.F.
    /// Safe to run again.
    /// </summary>
    public static class LocalizationSetup
    {
        private const string FontsDir = "Assets/BusRush/Art/Fonts";
        private const string HindiDir = "Assets/BusRush/Art/Fonts/Hindi";
        private const string Ttf = HindiDir + "/HindiClusters.ttf";
        private const string Chars = HindiDir + "/HindiClusters.chars.txt";
        private const string FontAssetPath = HindiDir + "/HindiClusters SDF.asset";

        // Fonts that draw UI text: each gets the Hindi fallback.
        private static readonly string[] TextFonts =
        {
            FontsDir + "/LilitaOne UIv2 SDF.asset", FontsDir + "/Fredoka UIv2 SDF.asset", FontsDir + "/Nunito-Bold SDF.asset",
            FontsDir + "/Nunito-ExtraBold SDF.asset", FontsDir + "/LilitaOne-Regular SDF.asset"
        };

        private static readonly Regex DayLabel = new Regex(@"^Day (\d)$");

        [MenuItem("Tools/Bus Rush/Setup Localization")]
        public static void Run() => Debug.Log(Setup());

        public static void RunBatch()
        {
            string report = Setup();
            Debug.Log(report);
            File.WriteAllText("Temp/loc_setup.txt", report);
            EditorApplication.Exit(0);
        }

        /// <summary>Batch helper for screenshots: BUSRUSH_LANG=en|es|pt|fr|hi writes the language into PlayerPrefs (the next play session starts in it).</summary>
        public static void SetLanguageFromEnv()
        {
            string code = System.Environment.GetEnvironmentVariable("BUSRUSH_LANG") ?? "en";
            int index = System.Array.IndexOf(new[] { "en", "es", "pt", "fr", "hi" }, code);
            PlayerPrefs.SetInt(Localizer.PrefsKey, Mathf.Max(0, index));
            PlayerPrefs.Save();
            Debug.Log($"[LocalizationSetup] language for the next session: {code}");
            EditorApplication.Exit(0);
        }

        private static string Setup()
        {
            var sb = new StringBuilder("[LocalizationSetup]\n");
            TMP_FontAsset hindi = BakeHindiFont(sb);
            AddFallbacks(hindi, sb);
            AttachTexts(sb);
            AssetDatabase.SaveAssets();
            return sb.ToString();
        }

        // ------------------------------------------------------------------ font

        private static TMP_FontAsset BakeHindiFont(StringBuilder sb)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(Ttf);
            if (font == null) throw new System.InvalidOperationException("HindiClusters.ttf not imported: run build_localization.py first");
            string chars = File.ReadAllText(Chars, Encoding.UTF8);

            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null)
            {
                bool hasAll = chars.All(c => existing.HasCharacter(c));
                if (hasAll && existing.atlasPopulationMode == AtlasPopulationMode.Static)
                {
                    sb.AppendLine($"  font asset up to date ({existing.characterTable.Count} glyphs)");
                    return existing;
                }
                AssetDatabase.DeleteAsset(FontAssetPath); // rebuilt below
            }

            TMP_FontAsset fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, false);
            fa.name = "HindiClusters SDF";
            bool ok = fa.TryAddCharacters(chars, out string missing);
            sb.AppendLine($"  baked {fa.characterTable.Count}/{chars.Length} cluster glyphs, ok={ok}, missing='{missing}'");

            AssetDatabase.CreateAsset(fa, FontAssetPath);
            fa.material.name = "HindiClusters SDF Material";
            Shader mobile = Shader.Find("TextMeshPro/Mobile/Distance Field");
            if (mobile != null) fa.material.shader = mobile;
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            for (int i = 0; i < fa.atlasTextures.Length; i++)
            {
                fa.atlasTextures[i].name = "HindiClusters SDF Atlas" + (i == 0 ? "" : " " + i);
                AssetDatabase.AddObjectToAsset(fa.atlasTextures[i], fa);
            }

            var so = new SerializedObject(fa);
            so.FindProperty("m_AtlasPopulationMode").intValue = (int)AtlasPopulationMode.Static; // static: nothing is generated on the device
            SerializedProperty clear = so.FindProperty("m_ClearDynamicDataOnBuild");
            if (clear != null) clear.boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(fa);
            EditorUtility.SetDirty(fa.material);
            foreach (Texture2D t in fa.atlasTextures) EditorUtility.SetDirty(t);
            AssetDatabase.SaveAssets();
            sb.AppendLine($"  saved {FontAssetPath} (population={fa.atlasPopulationMode}, atlases={fa.atlasTextures.Length}, {fa.atlasWidth}x{fa.atlasHeight})");
            return fa;
        }

        private static void AddFallbacks(TMP_FontAsset hindi, StringBuilder sb)
        {
            foreach (string path in TextFonts)
            {
                var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (f == null)
                {
                    sb.AppendLine($"  (font not found: {path})");
                    continue;
                }
                f.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
                f.fallbackFontAssetTable.RemoveAll(x => x == null);
                if (!f.fallbackFontAssetTable.Contains(hindi)) f.fallbackFontAssetTable.Add(hindi);
                EditorUtility.SetDirty(f);
                sb.AppendLine($"  fallback on '{f.name}': {string.Join(", ", f.fallbackFontAssetTable.Select(x => x.name))}");
            }
        }

        // ------------------------------------------------------------------ texts

        private static void AttachTexts(StringBuilder sb)
        {
            Localizer.ResetForTests();
            int added = 0, kept = 0;
            var unmatched = new SortedSet<string>();

            bool Process(GameObject root, string where)
            {
                bool changed = false;
                HashSet<TMP_Text> driven = LocalizationScan.ScriptDriven(root);
                foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(t.gameObject)) continue; // nested prefabs are their own asset
                    if (where == "SettingsRow")
                    {
                        // its label differs per instance (override): SettingsRow localizes it by Setting, not by a fixed key
                        var stale = t.GetComponent<LocalizedText>();
                        if (stale != null)
                        {
                            Object.DestroyImmediate(stale, true);
                            changed = true;
                        }
                        continue;
                    }
                    string txt = (t.text ?? "").Trim();
                    if (txt.Length == 0 || !txt.Any(char.IsLetter)) continue;

                    string key = txt;
                    string[] args = new string[0];
                    Match day = DayLabel.Match(txt);
                    if (day.Success)
                    {
                        key = "Day {0}";
                        args = new[] { day.Groups[1].Value };
                    }
                    bool scriptDriven = driven.Contains(t);
                    bool neverAssigned = day.Success || txt == "Day 7 · Big chest" || txt == "Loading...";
                    if (scriptDriven && !neverAssigned) continue;     // the script localizes it
                    if (!Localizer.Has(key))
                    {
                        unmatched.Add($"{where}/{t.name}: \"{txt}\"");
                        continue;
                    }
                    var lt = t.GetComponent<LocalizedText>();
                    if (lt == null)
                    {
                        lt = t.gameObject.AddComponent<LocalizedText>();
                        added++;
                    }
                    else kept++;
                    lt.Set(key, args);
                    EditorUtility.SetDirty(lt);
                    changed = true;
                }
                return changed;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/BusRush/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (Process(root, Path.GetFileNameWithoutExtension(path))) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            string active = SceneManager.GetActiveScene().path;
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/BusRush/Scenes" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Scene s = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                bool changed = false;
                foreach (GameObject go in s.GetRootGameObjects())
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(go)) continue;
                    changed |= Process(go, Path.GetFileNameWithoutExtension(path));
                }
                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(s);
                    EditorSceneManager.SaveScene(s);
                }
            }
            if (!string.IsNullOrEmpty(active)) EditorSceneManager.OpenScene(active, OpenSceneMode.Single);

            sb.AppendLine($"  LocalizedText added={added}, already there={kept}");
            sb.AppendLine($"  static texts with no table entry ({unmatched.Count}, debug panel and symbols are expected):");
            foreach (string u in unmatched) sb.AppendLine("    " + u);
        }
    }
}
