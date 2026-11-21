using System;
using System.Collections.Generic;
using BusRush.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static BusRush.EditorTools.UIKit;
using Object = UnityEngine.Object;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Step R6: the reusable SettingsRow prefab, the Pause popup (Pause.dc.html) and the Settings popup
    /// (Settings.dc.html). Row icons are the design's SVG paths rasterised once into Art/UI_v2/Generated.
    /// </summary>
    public static partial class UIBuilder
    {
        [MenuItem("Tools/Bus Rush/Build Pause + Settings (R6)")]
        public static void BuildSettingsOnly()
        {
            Load();
            Popup pause = SavePopup(BuildPausePopup());
            SavePopup(BuildSettingsPopup()); // same prefab path as before: the catalog keeps it
            AddToCatalog(pause);
            CleanCatalog();
            // The coin pill (compact text, width cap) lives in the HUD and the Level Complete screen.
            Save(BuildLevelCompleteScreen(), "LevelCompleteScreen");
            GameObject hud = Save(BuildHud(), "GameHUD");
            AssetDatabase.SaveAssets();
            WireGame(hud, AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/UIManager.prefab"));
            EditorSceneManager.OpenScene($"{ScenesDir}/Boot.unity", OpenSceneMode.Single);
            Debug.Log("[UIBuilder] Pause + Settings built.");
        }

        public static void BuildSettingsBatch()
        {
            try
            {
                BuildSettingsOnly();
            }
            catch (Exception e)
            {
                Debug.LogError("[UIBuilder] FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }

        private static void AddToCatalog(Popup popup)
        {
            PopupCatalog catalog = AssetDatabase.LoadAssetAtPath<PopupCatalog>(CatalogPath);
            var so = new SerializedObject(catalog);
            SerializedProperty list = so.FindProperty("popups");
            var items = new List<Object>();
            for (int i = 0; i < list.arraySize; i++)
            {
                Object o = list.GetArrayElementAtIndex(i).objectReferenceValue;
                if (o != null && o.GetType() != popup.GetType()) items.Add(o);
            }
            items.Add(popup);
            Wire(catalog, ("popups", items.ToArray()));
            EditorUtility.SetDirty(catalog);
        }

        // ------------------------------------------------------------------ icons (SVG 24x24 viewBox, white)

        private delegate float Sdf(Vector2 p);

        private static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        private static float Polyline(Vector2 p, params Vector2[] pts)
        {
            float d = float.MaxValue;
            for (int i = 1; i < pts.Length; i++) d = Mathf.Min(d, Segment(p, pts[i - 1], pts[i]));
            return d;
        }

        private static bool InPolygon(Vector2 p, Vector2[] poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x) inside = !inside;
            }
            return inside;
        }

        /// <summary>Arc stroke distance: circle (c, r) between angles a0..a1 (degrees, y down like SVG).</summary>
        private static float Arc(Vector2 p, Vector2 c, float r, float a0, float a1)
        {
            float ang = Mathf.Atan2(p.y - c.y, p.x - c.x) * Mathf.Rad2Deg;
            if (ang >= a0 && ang <= a1) return Mathf.Abs(Vector2.Distance(p, c) - r);
            Vector2 e0 = c + new Vector2(Mathf.Cos(a0 * Mathf.Deg2Rad), Mathf.Sin(a0 * Mathf.Deg2Rad)) * r;
            Vector2 e1 = c + new Vector2(Mathf.Cos(a1 * Mathf.Deg2Rad), Mathf.Sin(a1 * Mathf.Deg2Rad)) * r;
            return Mathf.Min(Vector2.Distance(p, e0), Vector2.Distance(p, e1));
        }

        /// <summary>Rasterises a white icon: shape(p) returns coverage 0..1 at SVG point p (24x24, y down).</summary>
        private static Sprite IconSprite(string name, Func<Vector2, float, float> shape)
        {
            const int size = 96;
            const float unitsPerPx = 24f / size;
            return GeneratedSprite(name, size, (u, v) =>
            {
                var p = new Vector2(u * 24f, (1f - v) * 24f);
                return new Color(1f, 1f, 1f, Mathf.Clamp01(shape(p, unitsPerPx)));
            });
        }

        private static float Stroke(float d, float width, float px) => Mathf.Clamp01((width * 0.5f - d) / px + 0.5f);

        private static float Fill(bool inside, float edgeDistance, float px) =>
            Mathf.Clamp01((inside ? edgeDistance : -edgeDistance) / px + 0.5f);

        private static Sprite SoundIcon() => IconSprite("icon_sound", (p, px) =>
        {
            Vector2[] body = { new Vector2(4, 9), new Vector2(8, 9), new Vector2(13, 5), new Vector2(13, 19), new Vector2(8, 15), new Vector2(4, 15) };
            float edge = Polyline(p, body[0], body[1], body[2], body[3], body[4], body[5], body[0]);
            float speaker = Fill(InPolygon(p, body), edge, px);
            float w1 = Stroke(Arc(p, new Vector2(13.6f, 12f), 4.3f, -55f, 55f), 2.2f, px);
            float w2 = Stroke(Arc(p, new Vector2(13.2f, 12f), 7.6f, -52f, 52f), 2.2f, px);
            return Mathf.Max(speaker, Mathf.Max(w1, w2));
        });

        private static Sprite MusicIcon() => IconSprite("icon_music", (p, px) =>
        {
            float stem = Stroke(Polyline(p, new Vector2(9, 17), new Vector2(9, 6), new Vector2(19, 4), new Vector2(19, 15)), 2.4f, px);
            float n1 = Fill(Vector2.Distance(p, new Vector2(7, 17.5f)) < 2.8f, Mathf.Abs(Vector2.Distance(p, new Vector2(7, 17.5f)) - 2.8f), px);
            float n2 = Fill(Vector2.Distance(p, new Vector2(17, 15.5f)) < 2.8f, Mathf.Abs(Vector2.Distance(p, new Vector2(17, 15.5f)) - 2.8f), px);
            return Mathf.Max(stem, Mathf.Max(n1, n2));
        });

        private static Sprite VibrationIcon() => IconSprite("icon_vibration", (p, px) =>
        {
            // Rounded rect x8 y3 w8 h18 r2 (stroke 2.4) + side lines (stroke 2).
            Vector2 c = new Vector2(12f, 12f), half = new Vector2(4f, 9f);
            Vector2 q = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half + new Vector2(2f, 2f);
            float box = Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - 2f;
            float phone = Stroke(Mathf.Abs(box), 2.4f, px);
            float lines = Mathf.Min(Mathf.Min(Segment(p, new Vector2(4, 8), new Vector2(4, 16)), Segment(p, new Vector2(20, 8), new Vector2(20, 16))),
                                    Mathf.Min(Segment(p, new Vector2(1.5f, 10), new Vector2(1.5f, 14)), Segment(p, new Vector2(22.5f, 10), new Vector2(22.5f, 14))));
            return Mathf.Max(phone, Stroke(lines, 2f, px));
        });

        private static Sprite GlobeIcon() => IconSprite("icon_globe", (p, px) =>
        {
            Vector2 c = new Vector2(12f, 12f);
            float ring = Stroke(Mathf.Abs(Vector2.Distance(p, c) - 9f), 2.2f, px);
            float equator = Stroke(Segment(p, new Vector2(3, 12), new Vector2(21, 12)), 2f, px);
            // Meridian ellipse rx 3.1, ry 9 (the two SVG curves).
            Vector2 e = new Vector2((p.x - c.x) / 3.1f, (p.y - c.y) / 9f);
            float meridian = Stroke(Mathf.Abs(e.magnitude - 1f) * 3.1f, 2f, px);
            return Mathf.Max(ring, Mathf.Max(equator, meridian));
        });

        // ------------------------------------------------------------------ SettingsRow prefab

        private static GameObject BuildSettingsRowPrefab()
        {
            const float w = 282f, h = 58f;
            var root = new GameObject("SettingsRow", typeof(RectTransform));
            root.layer = 5;
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(w, h);

            Image border = RoundImg(rt, "Border", 18f, H(0xC9DCF5));
            Stretch(border.rectTransform);
            border.raycastTarget = true;
            Image bg = RoundImg(rt, "Bg", 16f, Color.white);
            Stretch(bg.rectTransform, 2f, 2f, 2f, 2f);

            // Icon square 40x40 radius 13, border 2, 3 px lip.
            RectTransform iconBox = Place(Rect(rt, "IconBox"), 0f, 0.5f, 14f, 0f, 40f, 40f, 0f, 0.5f);
            Image lip = RoundImg(iconBox, "Lip", 13f, H(0x8A3E05));
            Stretch(lip.rectTransform, 0f, -3f, 0f, 3f);
            Image edge = RoundImg(iconBox, "Edge", 13f, H(0x8A3E05));
            Stretch(edge.rectTransform);
            Image face = RoundImg(iconBox, "Face", 11f, H(0xFF8A1F));
            Stretch(face.rectTransform, 2f, 2f, 2f, 2f);
            Image icon = Img(iconBox, "Icon", SoundIcon(), Color.white);
            Place(icon.rectTransform, 0.5f, 0.5f, 0f, 0f, 24f, 24f);

            TextMeshProUGUI label = V2Text(rt, "Label", "Sound", 21f, true, H(0x24345E), null, TextAlignmentOptions.Left);
            Stretch(label.rectTransform, 66f, 0f, 84f, 0f);

            // Switch 62x34: navy border 2, track (colour by state), white knob 26 sliding 2 -> 30.
            RectTransform sw = Place(Rect(rt, "Switch"), 1f, 0.5f, -12f, 0f, 62f, 34f, 1f, 0.5f);
            Image swBorder = RoundImg(sw, "Border", 17f, H(0x24345E));
            Stretch(swBorder.rectTransform);
            Image track = RoundImg(sw, "Track", 15f, H(0x4CC92F));
            Stretch(track.rectTransform, 2f, 2f, 2f, 2f);
            Image inset = RoundImg(track.transform, "InnerShadow", 15f, new Color(0f, 0f, 0f, 0.15f));
            Stretch(inset.rectTransform, 0f, 27f, 0f, 0f);
            RectTransform knob = Place(Rect(track.transform, "Knob"), 0f, 0.5f, 30f, 0f, 26f, 26f, 0f, 0.5f);
            Image knobShadow = Img(knob, "Shadow", Circle, new Color(0f, 0f, 0f, 0.25f));
            Stretch(knobShadow.rectTransform, 0f, -2f, 0f, 2f);
            Image knobFace = Img(knob, "Face", Circle, Color.white);
            Stretch(knobFace.rectTransform);

            // Value (Language): "English >".
            RectTransform value = Stretch(Rect(rt, "Value"), 150f, 0f, 14f, 0f);
            TextMeshProUGUI valueText = V2Text(value, "Text", "English  ›", 15f, false, H(0x5A6E9C), null, TextAlignmentOptions.Right);
            valueText.fontStyle = FontStyles.Bold;
            value.gameObject.SetActive(false);

            Button button = V2Button(rt);
            button.targetGraphic = border;
            SettingsRow row = root.AddComponent<SettingsRow>();
            Wire(row, ("button", button), ("switchRoot", sw.gameObject), ("track", track), ("knob", knob),
                ("valueRoot", value.gameObject), ("valueText", valueText));
            return root;
        }

        private static GameObject settingsRowPrefab;

        private static GameObject SettingsRowPrefab()
        {
            if (settingsRowPrefab == null) settingsRowPrefab = Save(BuildSettingsRowPrefab(), "SettingsRow");
            return settingsRowPrefab;
        }

        /// <summary>Places a SettingsRow prefab instance (top-left design box) and sets its label, icon and setting.</summary>
        private static void SettingsRowAt(RectTransform parent, SettingsRow.Setting setting, float left, float top, float w,
                                          float labelSize)
        {
            var row = (GameObject)PrefabUtility.InstantiatePrefab(SettingsRowPrefab(), parent);
            var rt = (RectTransform)row.transform;
            Box(rt, left, top, w, 58f);
            row.name = setting + "Row";
            (string label, Sprite icon, int face, int edge) look = setting switch
            {
                SettingsRow.Setting.Sound => ("Sound", SoundIcon(), 0xFF8A1F, 0x8A3E05),
                SettingsRow.Setting.Music => ("Music", MusicIcon(), 0xB266FF, 0x5A1F9E),
                SettingsRow.Setting.Vibration => ("Vibration", VibrationIcon(), 0x2F86E8, 0x103E86),
                _ => ("Language", GlobeIcon(), 0x3DB52A, 0x1D5F12)
            };
            TextMeshProUGUI label = rt.Find("Label").GetComponent<TextMeshProUGUI>();
            label.text = look.label;
            label.fontSize = labelSize;
            rt.Find("IconBox/Icon").GetComponent<Image>().sprite = look.icon;
            rt.Find("IconBox/Face").GetComponent<Image>().color = H(look.face);
            rt.Find("IconBox/Edge").GetComponent<Image>().color = H(look.edge);
            rt.Find("IconBox/Lip").GetComponent<Image>().color = H(look.edge);
            Wire(row.GetComponent<SettingsRow>(), ("setting", setting));
        }

        // ------------------------------------------------------------------ popup shell

        private struct V2Shell
        {
            public GameObject Root;
            public RectTransform Panel;
        }

        private static V2Shell V2PopupShell<T>(string name, float w, float h, float centreOffsetY, float ribbonLeft, float ribbonWidth,
                                              string title, float titleSize, float closeLeft, float dimAlpha) where T : Popup
        {
            const float b = 4f;
            var root = new GameObject(name, typeof(RectTransform));
            root.layer = 5;
            Stretch((RectTransform)root.transform);
            root.AddComponent<CanvasGroup>();
            T popup = root.AddComponent<T>();

            RectTransform blurRt = Stretch(Rect(root.transform, "Blur"), -10f, -10f, -10f, -10f);
            RawImage blur = blurRt.gameObject.AddComponent<RawImage>();
            blur.raycastTarget = false;
            blur.enabled = false;
            Image dim = Img(root.transform, "Dim", White, new Color(12f / 255f, 20f / 255f, 62f / 255f, 1f));
            dim.raycastTarget = true;
            Stretch(dim.rectTransform);
            Button dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;

            RectTransform safe = SafeArea(root.transform);
            RectTransform panel = Rect(safe, "Panel");
            Place(panel, 0.5f, 0.5f, 0f, centreOffsetY, w, h);
            RectTransform card = V2Box(panel, "Card", 32f, Color.white, H(0xE3EFFF), null, b, H(0x1B4FA0), 8f);
            Stretch(card);

            RectTransform ribbon = Box(Rect(panel, "Ribbon"), ribbonLeft, -30f, ribbonWidth, 60f);
            CanvasGroup ribbonGroup = ribbon.gameObject.AddComponent<CanvasGroup>();
            RectTransform ribbonBox = V2Box(ribbon, "Box", 20f, H(0x7FD3FF), H(0x2F86E8), null, 4f, H(0x103E86), 5f);
            Stretch(ribbonBox);
            V2Text(ribbonBox, "Title", title, titleSize, true, Color.white, LilitaOutline("Blue", H(0x103E86)));

            RectTransform close = V2Box(panel, "CloseButton", 23f, H(0xFF8A8A), H(0xD12A33), null, 3f, H(0x7A1016), 4f);
            Box(close, closeLeft, -18f, 46f, 46f);
            for (int i = 0; i < 2; i++)
            {
                Image bar = RoundImg(close, "X" + i, 1.7f, Color.white);
                Place(bar.rectTransform, 0.5f, 0.5f, 0f, 0f, 20f, 3.5f);
                bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 45f : -45f);
            }
            Button closeButton = V2Button(close);

            Wire(popup, ("dim", dim), ("panel", panel), ("closeButton", closeButton), ("dimAlpha", dimAlpha),
                ("blur", blur), ("ribbon", ribbon), ("ribbonGroup", ribbonGroup));
            return new V2Shell { Root = root, Panel = panel };
        }

        private static Button ChunkyButton(RectTransform panel, string name, string text, float left, float top, float w, float h,
                                           float radius, float size, int top0, int mid, int bottom, int edge, string mat)
        {
            RectTransform box = V2Box(panel, name, radius, H(top0), H(bottom), H(mid), 3f, H(edge), 6f, true);
            Box(box, left, top, w, h);
            V2Text(box, "Label", text, size, true, Color.white, LilitaOutline(mat, H(edge))).rectTransform.anchoredPosition = new Vector2(0f, 2f);
            return V2Button(box);
        }

        // ------------------------------------------------------------------ Pause

        private static GameObject BuildPausePopup()
        {
            const float b = 4f;
            V2Shell s = V2PopupShell<PausePopup>("PausePopup", 330f, 470f, -9f, 55f, 220f, "PAUSED", 32f, 296f, 0.62f);
            TextMeshProUGUI level = V2Text(s.Panel, "Level", "Level 1", 16f, false, H(0x5A6E9C));
            level.fontStyle = FontStyles.Bold;
            Box(level.rectTransform, b, b + 46f, 322f, 20f);

            SettingsRowAt(s.Panel, SettingsRow.Setting.Sound, b + 24f, b + 80f, 282f, 21f);
            SettingsRowAt(s.Panel, SettingsRow.Setting.Music, b + 24f, b + 148f, 282f, 21f);
            SettingsRowAt(s.Panel, SettingsRow.Setting.Vibration, b + 24f, b + 216f, 282f, 21f);

            Button resume = ChunkyButton(s.Panel, "ResumeButton", "Resume", b + 24f, b + 300f, 282f, 66f, 22f, 30f,
                                         0xA4F560, 0x56D134, 0x36A920, 0x1D5F12, "Green");
            Button restart = ChunkyButton(s.Panel, "RestartButton", "Restart", b + 24f, b + 384f, 134f, 58f, 20f, 22f,
                                          0xFFD36B, 0xFFA22E, 0xF07F12, 0x8A3E05, "Orange");
            Button home = ChunkyButton(s.Panel, "HomeButton", "Home", b + 172f, b + 384f, 134f, 58f, 20f, 22f,
                                       0x7FD3FF, 0x3A9BF5, 0x1F74D6, 0x103E86, "Blue");

            Wire(s.Root.GetComponent<PausePopup>(), ("levelText", level), ("resumeButton", resume), ("restartButton", restart),
                ("homeButton", home));
            return s.Root;
        }

        // ------------------------------------------------------------------ Settings

        private static GameObject BuildSettingsPopup()
        {
            const float b = 4f;
            V2Shell s = V2PopupShell<SettingsPopup>("SettingsPopup", 342f, 452f, 0f, 56f, 230f, "SETTINGS", 30f, 308f, 0.66f);

            SettingsRowAt(s.Panel, SettingsRow.Setting.Sound, b + 22f, b + 62f, 290f, 20f);
            SettingsRowAt(s.Panel, SettingsRow.Setting.Music, b + 22f, b + 130f, 290f, 20f);
            SettingsRowAt(s.Panel, SettingsRow.Setting.Vibration, b + 22f, b + 198f, 290f, 20f);
            SettingsRowAt(s.Panel, SettingsRow.Setting.Language, b + 22f, b + 266f, 290f, 20f);

            Button Link(string name, string text, float left)
            {
                Image border = RoundImg(s.Panel, name, 16f, H(0x8FA8CF));
                Box(border.rectTransform, left, b + 348f, 140f, 44f);
                Image face = RoundImg(border.transform, "Face", 14f, Color.white);
                Stretch(face.rectTransform, 2f, 2f, 2f, 2f);
                TextMeshProUGUI t = V2Text(border.transform, "Label", text, 14f, false, H(0x1B4FA0));
                t.fontStyle = FontStyles.Bold;
                return V2Button(border.rectTransform);
            }

            Button privacy = Link("PrivacyButton", "Privacy Policy", b + 22f);
            Button terms = Link("TermsButton", "Terms of Use", b + 172f);
            TextMeshProUGUI version = V2Text(s.Panel, "Version", "Version 0.1.0", 12f, false, H(0x8FA0C0));
            version.fontStyle = FontStyles.Bold;
            Box(version.rectTransform, b, b + 408f, 334f, 16f);

            Wire(s.Root.GetComponent<SettingsPopup>(), ("privacyButton", privacy), ("termsButton", terms), ("versionText", version));
            return s.Root;
        }
    }
}
