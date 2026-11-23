using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BusRush.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Tools > Bus Rush > Apply UI Text Style: applies the text style defined in <see cref="UiTheme"/> to every TMP text in
    /// the UI prefabs and the three scenes.
    ///   BUTTON text (label on a Button, light face): white face, no outline, soft underlay in a darker shade of the button.
    ///   TITLE text (any other text that used an outlined preset): white face, thin outline + small underlay in the header shade.
    ///   Dark text / texts on the plain font materials: untouched (no outline, no underlay).
    /// Materials are rewritten from the theme, so changing a number in UiTheme and re-running restyles every screen.
    /// Env BUSRUSH_STYLE_DRY=1 only reports what it would do. Text size is scaled once (UiTheme.textSizeScale).
    /// </summary>
    public static class UiTextStyler
    {
        private const string ThemePath = "Assets/BusRush/Art/UI_v2/UiTheme.asset";
        private const string FontsDir = "Assets/BusRush/Art/Fonts";
        private const string Prefix = "LilitaOne UIv2 SDF ";

        private enum Role { Keep, Plain, Button, Title }

        private static readonly string[] Families = { "Green", "Orange", "Blue", "Red", "Purple" };

        [MenuItem("Tools/Bus Rush/Apply UI Text Style")]
        public static void Run() => Debug.Log(Apply(false));

        [MenuItem("Tools/Bus Rush/Preview UI Text Style (no changes)")]
        public static void Preview() => Debug.Log(Apply(true));

        public static void RunBatch()
        {
            string report = Apply(System.Environment.GetEnvironmentVariable("BUSRUSH_STYLE_DRY") == "1");
            Debug.Log(report);
            File.WriteAllText("Temp/ui_text_style.txt", report);
            EditorApplication.Exit(0);
        }

        // ------------------------------------------------------------------ run

        private sealed class Ctx
        {
            private UiTheme themeRef;
            /// <summary>Reloaded on demand: opening a scene unloads unused assets, which destroys a cached reference.</summary>
            public UiTheme theme => themeRef != null ? themeRef : themeRef = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
            public bool dry;
            public StringBuilder log = new StringBuilder();
            public Dictionary<string, Material> made = new Dictionary<string, Material>();
            public HashSet<Material> styled = new HashSet<Material>();
            public float sizeFactor;
            public int buttons, titles, plains, kept, resized, infos, sized, glossed, whole;
        }

        public static string Apply(bool dry)
        {
            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
            if (theme == null) return "[UiTextStyler] UiTheme.asset not found.";
            var c = new Ctx { dry = dry };
            c.sizeFactor = theme.textSizeScale / Mathf.Max(0.01f, theme.textSizeScaleApplied);
            c.log.AppendLine($"[UiTextStyler] {(dry ? "PREVIEW" : "APPLY")} sizeFactor={c.sizeFactor:F3}");

            string active = SceneManager.GetActiveScene().path;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/BusRush/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                bool changed = false;
                try
                {
                    changed = Process(root, Path.GetFileNameWithoutExtension(path), c);
                    if (changed && !dry) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/BusRush/Scenes" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Scene s = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                bool changed = false;
                foreach (GameObject go in s.GetRootGameObjects()) changed |= Process(go, Path.GetFileNameWithoutExtension(path), c);
                if (changed && !dry)
                {
                    EditorSceneManager.MarkSceneDirty(s);
                    EditorSceneManager.SaveScene(s);
                }
            }
            if (!string.IsNullOrEmpty(active)) EditorSceneManager.OpenScene(active, OpenSceneMode.Single);

            if (!dry)
            {
                RestyleRemainingMaterials(c);
                c.theme.textSizeScaleApplied = c.theme.textSizeScale;
                EditorUtility.SetDirty(c.theme);
                AssetDatabase.SaveAssets();
            }

            c.log.AppendLine($"[UiTextStyler] button={c.buttons} title={c.titles} plain/stripped={c.plains} untouched={c.kept} resized={c.resized} info={c.infos} rowSized={c.sized} glossFixed={c.glossed} wholePixel={c.whole}");
            return c.log.ToString();
        }

        // ------------------------------------------------------------------ per root

        private static bool Process(GameObject root, string where, Ctx c)
        {
            bool changed = false;
            foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (PrefabUtility.IsPartOfPrefabInstance(t.gameObject)) continue; // nested prefabs are processed as their own asset
                string label = $"{where}/{PathOf(t.transform, root.transform)} \"{Short(t.text)}\"";
                Role role = Decide(t, out string family, out string presetName);
                string oldName = t.fontSharedMaterial != null ? t.fontSharedMaterial.name : "-";

                if ((role == Role.Keep || role == Role.Plain) && IsInfoText(t))
                {
                    changed |= StyleInfo(t, label, c);
                    c.infos++;
                    continue;
                }

                if (role == Role.Keep && t.GetComponentInParent<Button>(true) == null)
                    c.log.AppendLine($"  (keep) {label}: color {ColorUtility.ToHtmlStringRGBA(t.color)} font={(t.font != null ? t.font.name : "-")} size={(t.enableAutoSizing ? t.fontSizeMax : t.fontSize):F1}");
                if (role == Role.Keep)
                {
                    c.kept++;
                    if (t.GetComponentInParent<Button>(true) != null && t.color.maxColorComponent > 0.6f)
                        c.log.AppendLine($"  (kept)  {label}: font={(t.font != null ? t.font.name : "-")} mat={oldName}");
                    continue;
                }

                Material target;
                switch (role)
                {
                    case Role.Button:
                        c.buttons++;
                        target = ButtonMaterial(family, c);
                        break;
                    case Role.Title:
                        c.titles++;
                        target = TitleMaterial(presetName, c);
                        break;
                    default:
                        c.plains++;
                        target = t.font != null ? t.font.material : null;
                        break;
                }

                bool resize = (role == Role.Button || role == Role.Title) && Mathf.Abs(c.sizeFactor - 1f) > 0.001f;
                c.log.AppendLine($"  {role,-6} {label}: {oldName} -> {(target != null ? target.name : "?")}" +
                                 $"{(resize ? $", size {t.fontSize:F1}->{t.fontSize * c.sizeFactor:F1}" : "")}");
                if (c.dry) continue;

                if (target != null && t.fontSharedMaterial != target)
                {
                    t.fontSharedMaterial = target;
                    changed = true;
                }
                if (resize)
                {
                    float min = t.fontSizeMin, max = t.fontSizeMax;
                    t.fontSize = Round(t.fontSize * c.sizeFactor);
                    t.fontSizeMax = Round(max * c.sizeFactor);
                    t.fontSizeMin = Round(min * c.sizeFactor);
                    c.resized++;
                    changed = true;
                }
                if (changed) EditorUtility.SetDirty(t);
            }

            changed |= NormaliseButtonText(root, where, c);
            changed |= TameGloss(root, where, c);
            changed |= WholePixelTexts(root, where, c);
            if (!c.dry) changed |= RemapMaterialFields(root, c);
            return changed;
        }

        private static float Round(float v) => Mathf.Round(v * 2f) / 2f;

        // ------------------------------------------------------------------ info text (small grey text -> navy, bold)

        private static bool IsInfoText(TMP_Text t)
        {
            Color col = t.color;
            Color.RGBToHSV(col, out float hue, out float sat, out float val);
            float lum = 0.2126f * col.r + 0.7152f * col.g + 0.0722f * col.b;
            float size = t.enableAutoSizing ? t.fontSizeMax : t.fontSize;
            if (size > 26f || val >= 0.85f || t.GetComponentInParent<Button>(true) != null) return false;
            string font = t.font != null ? t.font.name : "";
            if (font.StartsWith("Nunito-ExtraBold")) return false;   // already heavy (counters)
            bool grey = sat < 0.35f;
            bool slate = sat >= 0.2f && hue >= 190f / 360f && hue <= 255f / 360f; // blue-grey / navy text only, not gold/brown/purple
            bool lilita = font.StartsWith("LilitaOne");
            if (lilita) return grey;                                  // Lilita numbers keep their colours unless they are pale grey
            return (grey || slate) && (lum < 0.7f || col.a < 0.95f);
        }

        private static bool StyleInfo(TMP_Text t, string label, Ctx c)
        {
            TMP_FontAsset heavy = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontsDir}/Nunito-ExtraBold SDF.asset");
            bool lilita = t.font != null && t.font.name.StartsWith("LilitaOne");
            float size = t.enableAutoSizing ? t.fontSizeMax : t.fontSize;
            float newSize = lilita ? size : Mathf.Max(size, c.theme.infoTextMinSize);
            c.log.AppendLine($"  Info   {label}: color {ColorUtility.ToHtmlStringRGBA(t.color)} font={(t.font != null ? t.font.name : "-")} size {size:F1}" +
                             $" -> {ColorUtility.ToHtmlStringRGB(c.theme.infoTextColor)} {(lilita ? "(Lilita kept)" : $"Nunito-ExtraBold {newSize:F1}")}");
            if (c.dry) return false;
            t.color = c.theme.infoTextColor;
            if (!lilita && heavy != null)
            {
                t.font = heavy;
                t.fontSharedMaterial = heavy.material;
                // Auto-size (never below 14) so the larger text still fits its rect.
                t.enableAutoSizing = true;
                t.fontSizeMax = newSize;
                t.fontSizeMin = Mathf.Min(14f, newSize);
                t.fontSize = newSize;
            }
            t.fontStyle = FontStyles.Normal;
            EditorUtility.SetDirty(t);
            return true;
        }

        // ------------------------------------------------------------------ button text sizes

        private const float PrimaryTextRatio = 32.5f / 66f; // Resume (Pause): 30 x 1.08 text in a 66 high button
        private static readonly string[] SecondaryWords = { "restart", "home", "collect", "continue" };

        /// <summary>
        /// Same text size on every button of one row; secondary buttons (Restart, Home, Collect, Continue) get the primary size
        /// minus at most (1 - secondaryTextMinRatio). Auto-size is on with one shared min/max so words never look tiny.
        /// </summary>
        private static bool NormaliseButtonText(GameObject root, string where, Ctx c)
        {
            var labels = new List<(TMP_Text t, Button bt)>();
            foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (PrefabUtility.IsPartOfPrefabInstance(t.gameObject)) continue;
                Role r = Decide(t, out _, out _);
                Button bt = t.GetComponentInParent<Button>(true);
                if (r == Role.Button && bt != null && (t.name == "Label" || t.transform.parent == bt.transform)) labels.Add((t, bt));
            }
            if (labels.Count == 0) return false;

            float TargetOf(TMP_Text t) => t.enableAutoSizing ? t.fontSizeMax : t.fontSize;
            bool Secondary(TMP_Text t) => SecondaryWords.Contains((t.text ?? "").Trim().ToLowerInvariant());

            float primary = 0f;
            foreach (var l in labels) if (!Secondary(l.t)) primary = Mathf.Max(primary, TargetOf(l.t));

            var size = new Dictionary<TMP_Text, float>();
            foreach (var l in labels)
            {
                float s = TargetOf(l.t);
                if (primary > 0f && Secondary(l.t)) s = Mathf.Clamp(s, primary * c.theme.secondaryTextMinRatio, primary);
                size[l.t] = s;
            }

            // rows: same parent, same anchors, same local y (prefab contents have no canvas layout, world positions are meaningless)
            var rows = labels.GroupBy(l =>
            {
                var r = (RectTransform)l.bt.transform;
                return (r.parent, Mathf.RoundToInt(r.anchorMin.y * 100f), Mathf.RoundToInt(r.anchoredPosition.y / 4f));
            });
            foreach (var row in rows)
            {
                float rowMax = row.Max(l => size[l.t]);
                foreach (var l in row) size[l.t] = rowMax;
            }

            // The Level Failed "Retry" is the primary button of its screen: same text/height ratio as "Resume" on Pause.
            foreach (var l in labels)
            {
                if (l.bt.name != "RetryButton") continue;
                var br = (RectTransform)l.bt.transform;
                float h = br.rect.height > 8f ? br.rect.height : br.sizeDelta.y;
                if (h > 8f) size[l.t] = Mathf.Max(size[l.t], PrimaryTextRatio * h);
            }

            bool changed = false;
            foreach (var l in labels)
            {
                float s = Round(size[l.t]);
                float min = Round(s * 0.85f);
                bool differs = !l.t.enableAutoSizing || Mathf.Abs(l.t.fontSizeMax - s) > 0.01f || Mathf.Abs(l.t.fontSizeMin - min) > 0.01f ||
                               Mathf.Abs(l.t.fontSize - s) > 0.01f;
                if (!differs) continue;
                c.log.AppendLine($"  Size   {where}/{PathOf(l.t.transform, root.transform)} \"{Short(l.t.text)}\": {l.t.fontSize:F1} -> {s:F1} (auto {min:F1}..{s:F1})");
                c.sized++;
                if (c.dry) continue;
                l.t.enableAutoSizing = true;
                l.t.fontSizeMax = s;
                l.t.fontSizeMin = min;
                l.t.fontSize = s;
                EditorUtility.SetDirty(l.t);
                changed = true;
            }
            return changed;
        }

        // ------------------------------------------------------------------ whole-pixel text rects

        /// <summary>Text rects on buttons: scale 1 and whole-number positions/sizes (fractional values blur SDF text).</summary>
        private static bool WholePixelTexts(GameObject root, string where, Ctx c)
        {
            bool changed = false;
            foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (PrefabUtility.IsPartOfPrefabInstance(t.gameObject) || t.GetComponentInParent<Button>(true) == null) continue;
                var rt = (RectTransform)t.transform;
                if (rt.parent != null && rt.parent.GetComponent<LayoutGroup>() != null) continue; // the layout owns its rect
                string name = $"{where}/{PathOf(rt, root.transform)}";

                bool scaleOff = rt.localScale != Vector3.one;
                Vector2 pos = rt.anchoredPosition, size = rt.sizeDelta, omin = rt.offsetMin, omax = rt.offsetMax;
                bool stretchX = rt.anchorMin.x != rt.anchorMax.x, stretchY = rt.anchorMin.y != rt.anchorMax.y;
                Vector2 npos = new Vector2(Mathf.Round(pos.x), Mathf.Round(pos.y));
                Vector2 nsize = new Vector2(Mathf.Round(size.x), Mathf.Round(size.y));
                Vector2 nmin = new Vector2(Mathf.Round(omin.x), Mathf.Round(omin.y));
                Vector2 nmax = new Vector2(Mathf.Round(omax.x), Mathf.Round(omax.y));
                bool fracX = stretchX ? (nmin.x != omin.x || nmax.x != omax.x) : (npos.x != pos.x || nsize.x != size.x);
                bool fracY = stretchY ? (nmin.y != omin.y || nmax.y != omax.y) : (npos.y != pos.y || nsize.y != size.y);
                if (!scaleOff && !fracX && !fracY) continue;

                c.log.AppendLine($"  Whole  {name}: scale={rt.localScale} pos={pos} size={size} offsets={omin}/{omax}");
                c.whole++;
                if (c.dry) continue;
                if (scaleOff) rt.localScale = Vector3.one;
                if (stretchX) { omin.x = nmin.x; omax.x = nmax.x; } else { pos.x = npos.x; size.x = nsize.x; }
                if (stretchY) { omin.y = nmin.y; omax.y = nmax.y; } else { pos.y = npos.y; size.y = nsize.y; }
                if (stretchX || stretchY) { rt.offsetMin = new Vector2(stretchX ? omin.x : rt.offsetMin.x, stretchY ? omin.y : rt.offsetMin.y); rt.offsetMax = new Vector2(stretchX ? omax.x : rt.offsetMax.x, stretchY ? omax.y : rt.offsetMax.y); }
                if (!stretchX || !stretchY) { rt.anchoredPosition = new Vector2(stretchX ? rt.anchoredPosition.x : pos.x, stretchY ? rt.anchoredPosition.y : pos.y); rt.sizeDelta = new Vector2(stretchX ? rt.sizeDelta.x : size.x, stretchY ? rt.sizeDelta.y : size.y); }
                EditorUtility.SetDirty(rt);
                changed = true;
            }
            return changed;
        }

        // ------------------------------------------------------------------ gloss band: top 35 percent of the face only

        private static readonly string[] GlossNames = { "Shine", "Gloss" }; // Highlight / TopBand are the opaque rim behind the face
        private const float GlossTop = 0.35f;

        private static bool TameGloss(GameObject root, string where, Ctx c)
        {
            bool changed = false;
            foreach (Image img in root.GetComponentsInChildren<Image>(true))
            {
                if (!GlossNames.Contains(img.name) || PrefabUtility.IsPartOfPrefabInstance(img.gameObject)) continue;
                if (img.GetComponentInParent<Button>(true) == null || img.transform.parent.name.Contains("Mask")) continue;
                var rt = (RectTransform)img.transform;
                var parent = rt.parent as RectTransform;
                float ph = parent != null ? parent.rect.height : 0f;
                if (ph < 8f || rt.anchorMax.y - rt.anchorMin.y > 0.999f && ph < 8f) continue;

                float bottom = rt.anchorMin.y + rt.offsetMin.y / ph;
                float top = rt.anchorMax.y + rt.offsetMax.y / ph;
                bool tooTall = bottom < 1f - GlossTop - 0.01f && top > 0.8f;
                bool tooStrong = img.color.a > 0.181f;
                if (!tooTall && !tooStrong) continue;
                c.log.AppendLine($"  Gloss  {where}/{PathOf(rt, root.transform)}: bottom {bottom:F2} alpha {img.color.a:F2} -> {(tooTall ? 1f - GlossTop : bottom):F2} / {Mathf.Min(img.color.a, 0.18f):F2}");
                c.glossed++;
                if (c.dry) continue;
                if (tooTall)
                {
                    rt.anchorMin = new Vector2(rt.anchorMin.x, 1f - GlossTop);
                    rt.offsetMin = new Vector2(rt.offsetMin.x, 0f);
                }
                Color col = img.color;
                col.a = Mathf.Min(col.a, 0.18f);
                img.color = col;
                EditorUtility.SetDirty(img);
                changed = true;
            }
            return changed;
        }

        // ------------------------------------------------------------------ classification

        private static Role Decide(TMP_Text t, out string family, out string presetName)
        {
            family = null;
            presetName = null;
            Material m = t.fontSharedMaterial;
            string matPath = m != null ? AssetDatabase.GetAssetPath(m) : "";
            bool isPreset = matPath.StartsWith(FontsDir) && matPath.EndsWith(".mat");
            if (isPreset) presetName = NormalisedPreset(m.name);

            Color col = t.color;
            float lum = 0.2126f * col.r + 0.7152f * col.g + 0.0722f * col.b;
            bool dark = lum < 0.4f;
            if (dark) return isPreset ? Role.Plain : Role.Keep;

            bool heading = t.font != null && t.font.name.StartsWith("LilitaOne");
            Button bt = t.GetComponentInParent<Button>(true);
            if (heading && bt != null)
            {
                // A button preset keeps its own family; the old Navy home-tile preset takes the colour of its button.
                if (isPreset && Families.Contains(presetName)) { family = presetName; return Role.Button; }
                if (isPreset && presetName == "Navy") { family = FamilyOf(bt) ?? "Blue"; return Role.Button; }
                // Unstyled label: only a direct child of its button (the X of a close button), not badges inside a bigger button.
                if (!isPreset && t.transform.parent == bt.transform && bt.transform.Find("Body/Lip") != null)
                {
                    family = FamilyOf(bt);
                    if (family != null) return Role.Button;
                }
            }
            if (isPreset) return Role.Title;
            return Role.Keep;
        }

        private static string NormalisedPreset(string name)
        {
            if (name.StartsWith(Prefix)) name = name.Substring(Prefix.Length);
            if (name.StartsWith("Title ")) name = name.Substring(6);
            return name;
        }

        /// <summary>Button colour family from the hue of its bottom lip (or face): the colour the text shadow is derived from.</summary>
        private static string FamilyOf(Button bt)
        {
            Image img = bt.GetComponentsInChildren<Image>(true).FirstOrDefault(i => i.name == "Lip") ??
                        bt.GetComponentsInChildren<Image>(true).FirstOrDefault(i => i.name == "Face");
            if (img == null) return null;
            Color.RGBToHSV(img.color, out float h, out float s, out float v);
            if (s < 0.25f || v < 0.15f) return null; // white / grey / black
            float deg = h * 360f;
            if (deg < 18f || deg >= 330f) return "Red";
            if (deg < 65f) return "Orange";
            if (deg < 170f) return "Green";
            if (deg < 255f) return "Blue";
            return "Purple";
        }

        // ------------------------------------------------------------------ materials

        private static Color FamilyShade(string family, UiTheme th)
        {
            switch (family)
            {
                case "Green": return UiTheme.ShadeOf(th.greenButton);
                case "Orange": return UiTheme.ShadeOf(th.orangeButton);
                case "Blue": return UiTheme.ShadeOf(th.blueButton);
                case "Red": return UiTheme.ShadeOf(th.redButton);
                case "Purple": return UiTheme.ShadeOf(th.purpleButton);
                default: return th.navy;
            }
        }

        private static Material ButtonMaterial(string family, Ctx c)
        {
            string key = "btn:" + family;
            if (c.made.TryGetValue(key, out Material have) && have != null) return have;
            Material m = LoadOrCreate($"{FontsDir}/{Prefix}{family}.mat", c);
            if (!c.dry)
            {
                c.theme.ApplyButtonText(m, FamilyShade(family, c.theme));
                EditorUtility.SetDirty(m);
                c.styled.Add(m);
            }
            c.made[key] = m;
            return m;
        }

        /// <summary>Title material for an old preset: families get a separate "Title X" copy (the X preset becomes the button style).</summary>
        private static Material TitleMaterial(string preset, Ctx c)
        {
            string key = "title:" + preset;
            if (c.made.TryGetValue(key, out Material have) && have != null) return have;
            bool family = Families.Contains(preset);
            string file = family ? $"{FontsDir}/{Prefix}Title {preset}.mat" : FindPresetPath(preset);
            Material m = LoadOrCreate(file, c);
            if (!c.dry)
            {
                Color shade = family ? FamilyShade(preset, c.theme) : SafeOutline(m, c.theme);
                c.theme.ApplyTitleText(m, shade);
                EditorUtility.SetDirty(m);
                c.styled.Add(m);
            }
            c.made[key] = m;
            return m;
        }

        private static string FindPresetPath(string preset)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { FontsDir }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                string n = Path.GetFileNameWithoutExtension(p);
                if (n == preset || n == Prefix + preset || n == preset + " Outline") return p;
            }
            return $"{FontsDir}/{Prefix}{preset}.mat";
        }

        private static Color SafeOutline(Material m, UiTheme th)
        {
            Color o = m.HasProperty(ShaderUtilities.ID_OutlineColor) ? m.GetColor(ShaderUtilities.ID_OutlineColor) : th.navy;
            return o.maxColorComponent < 0.12f ? th.navy : o; // never black
        }

        private static Material LoadOrCreate(string path, Ctx c)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null || c.dry) return m != null ? m : new Material(c.theme.headingFont.material) { name = Path.GetFileNameWithoutExtension(path) };
            TMP_FontAsset font = c.theme.headingFont;
            m = new Material(font.material) { name = Path.GetFileNameWithoutExtension(path) };
            m.SetTexture(ShaderUtilities.ID_MainTex, font.atlasTexture);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>Plain font materials get no outline/underlay (and no black colours); unused presets get the title style.</summary>
        private static void RestyleRemainingMaterials(Ctx c)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { FontsDir }))
            {
                var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (f == null || f.material == null) continue;
                c.theme.ApplyPlainText(f.material);
                EditorUtility.SetDirty(f.material);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { FontsDir }))
            {
                string mp = AssetDatabase.GUIDToAssetPath(guid);
                if (!mp.EndsWith(".mat")) continue; // materials embedded in font assets stay plain
                var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
                if (m == null || c.styled.Contains(m)) continue;
                c.theme.ApplyTitleText(m, SafeOutline(m, c.theme));
                EditorUtility.SetDirty(m);
                c.log.AppendLine($"  (unused preset restyled as title) {m.name}");
            }
        }

        /// <summary>Scripts that hold preset materials in serialized fields (BoosterPopup.titleMaterial, floatMaterial, ...).</summary>
        private static bool RemapMaterialFields(GameObject root, Ctx c)
        {
            bool changed = false;
            foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || mb is TMP_Text || PrefabUtility.IsPartOfPrefabInstance(mb.gameObject)) continue;
                var so = new SerializedObject(mb);
                SerializedProperty it = so.GetIterator();
                bool any = false;
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference || !(it.objectReferenceValue is Material m)) continue;
                    string p = AssetDatabase.GetAssetPath(m);
                    if (!p.StartsWith(FontsDir) || !p.EndsWith(".mat")) continue;
                    string preset = NormalisedPreset(m.name);
                    if (m.name.Contains("Title ") && Families.Contains(preset)) continue; // already a title material
                    Material title = TitleMaterial(preset, c);
                    if (title == m) continue;
                    c.log.AppendLine($"  Field  {mb.GetType().Name}.{it.propertyPath}: {m.name} -> {title.name}");
                    it.objectReferenceValue = title;
                    any = true;
                }
                if (any)
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }
            }
            return changed;
        }

        // ------------------------------------------------------------------ helpers

        private static string PathOf(Transform t, Transform root)
        {
            var parts = new List<string>();
            for (; t != null && t != root; t = t.parent) parts.Insert(0, t.name);
            return string.Join("/", parts);
        }

        private static string Short(string s)
        {
            s = (s ?? "").Replace("\n", " ");
            return s.Length > 24 ? s.Substring(0, 24) : s;
        }
    }
}
