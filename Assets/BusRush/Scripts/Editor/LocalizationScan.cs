using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Lists every UI string for the localization table: static TMP texts of the prefabs and scenes (flagging the ones a script
    /// writes to at runtime, which must call Localizer instead) and serialized string fields of components (booster texts...).
    /// Menu: Tools > Bus Rush > Scan UI Strings. Batch: LocalizationScan.RunBatch -> Temp/loc_scan.txt.
    /// </summary>
    public static class LocalizationScan
    {
        [MenuItem("Tools/Bus Rush/Scan UI Strings")]
        public static void Run() => Debug.Log(Scan());

        public static void RunBatch()
        {
            File.WriteAllText("Temp/loc_scan.txt", Scan());
            EditorApplication.Exit(0);
        }

        /// <summary>Texts that scripts reference through a serialized field (they set them at runtime).</summary>
        public static HashSet<TMP_Text> ScriptDriven(GameObject root)
        {
            var set = new HashSet<TMP_Text>();
            foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || mb is TMP_Text) continue;
                var so = new SerializedObject(mb);
                SerializedProperty it = so.GetIterator();
                while (it.Next(true))
                {
                    if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue is TMP_Text t) set.Add(t);
                }
            }
            return set;
        }

        private static string Scan()
        {
            var sb = new StringBuilder();
            var statics = new SortedDictionary<string, List<string>>();
            var driven = new SortedDictionary<string, List<string>>();
            var strings = new SortedDictionary<string, List<string>>();

            void Add(IDictionary<string, List<string>> d, string text, string where)
            {
                if (!d.TryGetValue(text, out List<string> l)) d[text] = l = new List<string>();
                if (l.Count < 3) l.Add(where);
            }

            void Inspect(GameObject root, string where)
            {
                HashSet<TMP_Text> drivenSet = ScriptDriven(root);
                foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(t.gameObject) && root.scene.IsValid() && false) continue;
                    string txt = (t.text ?? "").Trim();
                    if (txt.Length == 0 || !txt.Any(char.IsLetter)) continue;
                    Add(drivenSet.Contains(t) ? driven : statics, txt, $"{where}/{t.name}");
                }
                foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null || mb is TMP_Text) continue;
                    var so = new SerializedObject(mb);
                    SerializedProperty it = so.GetIterator();
                    while (it.Next(true))
                    {
                        if (it.propertyType != SerializedPropertyType.String) continue;
                        string v = it.stringValue;
                        if (string.IsNullOrWhiteSpace(v) || !v.Any(char.IsLetter) || v.Length > 80) continue;
                        if (it.propertyPath.EndsWith("m_Name") || it.propertyPath.Contains("Guid") || it.propertyPath.Contains("m_Text")) continue;
                        Add(strings, v, $"{where}/{mb.GetType().Name}.{it.propertyPath}");
                    }
                }
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/BusRush/Prefabs" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(p);
                try { Inspect(root, Path.GetFileNameWithoutExtension(p)); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            string active = SceneManager.GetActiveScene().path;
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/BusRush/Scenes" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                Scene s = EditorSceneManager.OpenScene(p, OpenSceneMode.Single);
                foreach (GameObject go in s.GetRootGameObjects())
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(go)) continue;
                    Inspect(go, Path.GetFileNameWithoutExtension(p));
                }
            }
            if (!string.IsNullOrEmpty(active)) EditorSceneManager.OpenScene(active, OpenSceneMode.Single);

            void Dump(string title, SortedDictionary<string, List<string>> d)
            {
                sb.AppendLine($"=== {title} ({d.Count})");
                foreach (var kv in d) sb.AppendLine($"{kv.Key.Replace("\n", "\\n")}\t{string.Join(" | ", kv.Value)}");
            }
            Dump("STATIC texts (LocalizedText component)", statics);
            Dump("SCRIPT-DRIVEN texts (default text only, scripts set them)", driven);
            Dump("SERIALIZED string fields", strings);
            return sb.ToString();
        }
    }
}
