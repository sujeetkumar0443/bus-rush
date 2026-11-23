using System;
using System.Collections.Generic;
using BusRush.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BusRush.EditorTools
{
    /// <summary>Editor-side style sheet and construction helpers for the Bus Rush chunky toy UI.</summary>
    internal static class UIKit
    {
        // ------------------------------------------------------------------ palette (from the brief)
        public static readonly Color Navy = UIArtGenerator.Hex(0x1F3F8F);
        public static readonly Color NavyLip = UIArtGenerator.Hex(0x152C66);
        public static readonly Color DarkNavy = UIArtGenerator.Hex(0x1E2A44);
        public static readonly Color Blue = UIArtGenerator.Hex(0x3A8DF5);
        public static readonly Color BlueLip = UIArtGenerator.Hex(0x1F5BB5);
        public static readonly Color Red = UIArtGenerator.Hex(0xEF3B3B);
        public static readonly Color RedLip = UIArtGenerator.Hex(0xA92424);
        public static readonly Color Green = UIArtGenerator.Hex(0x3CC35B);
        public static readonly Color GreenLip = UIArtGenerator.Hex(0x23863D);
        public static readonly Color GreenLipDark = UIArtGenerator.Hex(0x1C6B31);
        public static readonly Color Gold = UIArtGenerator.Hex(0xFFC93C);
        public static readonly Color GoldLip = UIArtGenerator.Hex(0xD69A12);
        public static readonly Color Grey = UIArtGenerator.Hex(0xB7C0D3);
        public static readonly Color GreyLip = UIArtGenerator.Hex(0x8F9AB2);
        public static readonly Color PanelWhite = Color.white;
        public static readonly Color PanelSoft = UIArtGenerator.Hex(0xEEF1F6);
        public static readonly Color PanelLip = UIArtGenerator.Hex(0xC3CBDB);
        public static readonly Color TextGrey = UIArtGenerator.Hex(0x4A5672);

        public const float RefWidth = 390f;
        public const float RefHeight = 844f;

        private const string FontsDir = "Assets/BusRush/Art/Fonts";

        public static Sprite Round, Circle, Shadow, White, Sky, Rays, Glow, Ring, Cloud, Vignette;
        public static TMP_FontAsset Heading, Body, BodyBold, Fallback;
        public static Material HeadingOutline, BodyOutline;

        private static readonly Dictionary<string, Sprite> IconCache = new Dictionary<string, Sprite>();

        public enum TextStyle
        {
            Heading,        // Lilita One, white face, navy outline + drop
            HeadingPlain,   // Lilita One, no outline
            Body,           // Nunito ExtraBold, white face, navy outline
            BodyPlain,      // Nunito ExtraBold, coloured, no outline
            BodyBoldPlain   // Nunito Bold, coloured, no outline
        }

        // ------------------------------------------------------------------ loading

        public static void Load()
        {
            const string d = UIArtGenerator.Dir;
            Round = LoadSprite($"{d}/ui_round.png");
            Circle = LoadSprite($"{d}/ui_circle.png");
            Shadow = LoadSprite($"{d}/ui_shadow.png");
            White = LoadSprite($"{d}/ui_white.png");
            Sky = LoadSprite($"{d}/ui_sky.png");
            Rays = LoadSprite($"{d}/ui_rays.png");
            Glow = LoadSprite($"{d}/ui_glow.png");
            Ring = LoadSprite($"{d}/ui_ring.png");
            Cloud = LoadSprite($"{d}/ui_cloud.png");
            Vignette = LoadSprite($"{d}/ui_vignette.png");
            IconCache.Clear();

            Fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontsDir}/Fredoka-SemiBold SDF.asset");
            Heading = MakeFont("LilitaOne-Regular", 14);
            Body = MakeFont("Nunito-ExtraBold", 12);
            BodyBold = MakeFont("Nunito-Bold", 10);

            HeadingOutline = MakeOutlineMaterial(Heading, 0.3f, -0.9f);
            BodyOutline = MakeOutlineMaterial(Body, 0.3f, -0.8f);

            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/TextMesh Pro/Resources/TMP Settings.asset");
            if (settings != null)
            {
                var so = new SerializedObject(settings);
                so.FindProperty("m_defaultFontAsset").objectReferenceValue = Body;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        public static Sprite Icon(string name)
        {
            if (!IconCache.TryGetValue(name, out Sprite s))
            {
                s = LoadSprite($"{UIArtGenerator.IconDir}/icon_{name}.png");
                IconCache[name] = s;
            }
            return s;
        }

        private static Sprite LoadSprite(string path)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s == null) throw new InvalidOperationException("Sprite missing: " + path);
            return s;
        }

        private static TMP_FontAsset MakeFont(string file, int padding)
        {
            string assetPath = $"{FontsDir}/{file} SDF.asset";
            var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (fa == null)
            {
                var font = AssetDatabase.LoadAssetAtPath<Font>($"{FontsDir}/{file}.ttf");
                if (font == null) throw new InvalidOperationException("Font missing: " + file);

                fa = TMP_FontAsset.CreateFontAsset(font, 90, padding, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                fa.name = file + " SDF";
                fa.TryAddCharacters(Ascii, out string _);
                AssetDatabase.CreateAsset(fa, assetPath);
                fa.atlasTextures[0].name = file + " Atlas";
                AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
                fa.material.name = file + " Material";
                AssetDatabase.AddObjectToAsset(fa.material, fa);
            }

            var fallbacks = new List<TMP_FontAsset>();
            if (Fallback != null) fallbacks.Add(Fallback);
            fa.fallbackFontAssetTable = fallbacks;
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();

            fa.HasCharacters("0123456789+x/!?%:.,-[]", out List<char> missing);
            Debug.Log($"[UIKit] Font {file}: required glyph check missing=[{(missing == null ? "" : new string(missing.ToArray()))}]");
            return fa;
        }

        private const string Ascii = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";

        /// <summary>Outlined label material (HUD / world labels): the UiTheme title style (thin navy outline, small shadow).</summary>
        private static Material MakeOutlineMaterial(TMP_FontAsset font, float outline, float dropY)
        {
            string path = $"{FontsDir}/{font.name} Outline.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(font.material) { name = font.name + " Outline" };
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = font.material.shader;
            m.SetTexture(ShaderUtilities.ID_MainTex, font.atlasTexture);
            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>("Assets/BusRush/Art/UI_v2/UiTheme.asset");
            if (theme != null) theme.ApplyTitleText(m, UiTheme.ShadeOf(theme.blueButton));
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ rect helpers

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Stretch to parent with offsets (left, bottom, right, top) measured inward.</summary>
        public static RectTransform Stretch(RectTransform rt, float l = 0f, float b = 0f, float r = 0f, float t = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        /// <summary>Single anchor point (ax, ay) in parent, pivot (px, py), position and size.</summary>
        public static RectTransform Place(RectTransform rt, float ax, float ay, float x, float y, float w, float h, float px = 0.5f, float py = 0.5f)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
            rt.pivot = new Vector2(px, py);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>Anchored to the parent's top centre; y is the distance from the top edge to this element's top.</summary>
        public static RectTransform Top(RectTransform rt, float y, float w, float h, float x = 0f)
        {
            return Place(rt, 0.5f, 1f, x, -y, w, h, 0.5f, 1f);
        }

        /// <summary>Anchored to the parent's bottom centre; y is the distance from the bottom edge.</summary>
        public static RectTransform Bottom(RectTransform rt, float y, float w, float h, float x = 0f)
        {
            return Place(rt, 0.5f, 0f, x, y, w, h, 0.5f, 0f);
        }

        // ------------------------------------------------------------------ images

        public static Image Img(Transform parent, string name, Sprite sprite, Color color, float radius = 0f)
        {
            RectTransform rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            if (radius > 0f) MakeRounded(img, radius);
            return img;
        }

        /// <summary>Rounded 9-slice with a corner radius in UI units (sharp: the sprite is authored at 4x).</summary>
        public static void MakeRounded(Image img, float radius)
        {
            img.sprite = Round;
            img.type = Image.Type.Sliced;
            img.fillCenter = true;
            img.pixelsPerUnitMultiplier = UIArtGenerator.RoundRadiusPx / Mathf.Max(1f, radius);
        }

        /// <summary>Pill / circle 9-slice for an element of the given height.</summary>
        public static void MakePill(Image img, float height)
        {
            img.sprite = Circle;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 256f / Mathf.Max(2f, height);
        }

        public static Image IconImg(Transform parent, string name, string icon, float size)
        {
            Image img = Img(parent, name, Icon(icon), Color.white);
            img.preserveAspect = true;
            img.rectTransform.sizeDelta = new Vector2(size, size);
            return img;
        }

        // ------------------------------------------------------------------ text

        public static TextMeshProUGUI Label(Transform parent, string name, string text, float size, TextStyle style,
                                            Color? color = null, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            RectTransform rt = Rect(parent, name);
            Stretch(rt);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.fontSize = size;
            t.enableAutoSizing = true;
            t.fontSizeMax = size;
            t.fontSizeMin = Mathf.Max(8f, size * 0.55f);
            t.extraPadding = true;

            switch (style)
            {
                case TextStyle.Heading:
                    t.font = Heading;
                    t.fontSharedMaterial = HeadingOutline;
                    t.color = color ?? Color.white;
                    break;
                case TextStyle.HeadingPlain:
                    t.font = Heading;
                    t.color = color ?? TextGrey;
                    break;
                case TextStyle.Body:
                    t.font = Body;
                    t.fontSharedMaterial = BodyOutline;
                    t.color = color ?? Color.white;
                    break;
                case TextStyle.BodyPlain:
                    t.font = Body;
                    t.color = color ?? TextGrey;
                    break;
                default:
                    t.font = BodyBold;
                    t.color = color ?? TextGrey;
                    break;
            }
            return t;
        }

        // ------------------------------------------------------------------ chunky elements

        public struct Chunk
        {
            public RectTransform Root;
            public RectTransform Body;
            public RectTransform Face;
            public Image FaceImage;
            public Image LipImage;
            public Button Button;
            public UIButton UIButton;
        }

        /// <summary>
        /// Rounded face sitting on a darker "3D lip", with a soft drop shadow and a top shine.
        /// The caller positions <see cref="Chunk.Root"/>; add content to <see cref="Chunk.Face"/>.
        /// radius &lt; 0 makes a pill (radius = half the height).
        /// </summary>
        public static Chunk Chunky(Transform parent, string name, Color face, Color lip, float w, float h, float radius,
                                   float lipHeight = 6f, bool button = false, bool shadow = true, bool shine = true)
        {
            var c = new Chunk { Root = Rect(parent, name) };
            c.Root.sizeDelta = new Vector2(w, h);
            bool pill = radius < 0f;

            if (button)
            {
                var hit = c.Root.gameObject.AddComponent<Image>();
                hit.sprite = Round;
                hit.color = new Color(1f, 1f, 1f, 0f);
                hit.raycastTarget = true;
                c.Button = c.Root.gameObject.AddComponent<Button>();
                c.Button.transition = Selectable.Transition.None;
                c.Button.targetGraphic = hit;
                var nav = c.Button.navigation;
                nav.mode = Navigation.Mode.None;
                c.Button.navigation = nav;
            }

            c.Body = Stretch(Rect(c.Root, "Body"));

            if (shadow)
            {
                Image sh = Img(c.Body, "Shadow", Shadow, new Color(DarkNavy.r, DarkNavy.g, DarkNavy.b, 0.3f));
                sh.type = Image.Type.Sliced;
                sh.pixelsPerUnitMultiplier = 4f;
                Stretch(sh.rectTransform, -7f, -9f, -7f, 1f);
            }

            c.LipImage = Img(c.Body, "Lip", Round, lip);
            Stretch(c.LipImage.rectTransform);
            c.FaceImage = Img(c.Body, "Face", Round, face);
            c.Face = Stretch(c.FaceImage.rectTransform, 0f, lipHeight, 0f, 0f);

            if (pill)
            {
                MakePill(c.LipImage, h - lipHeight);
                MakePill(c.FaceImage, h - lipHeight);
            }
            else
            {
                MakeRounded(c.LipImage, radius);
                MakeRounded(c.FaceImage, radius);
            }

            if (shine)
            {
                Image s = Img(c.Face, "Shine", Round, new Color(1f, 1f, 1f, 0.22f));
                float faceH = h - lipHeight;
                float inset = Mathf.Clamp(faceH * 0.1f, 3f, 7f);
                RectTransform srt = s.rectTransform;
                srt.anchorMin = new Vector2(0f, 0.5f);
                srt.anchorMax = new Vector2(1f, 1f);
                srt.offsetMin = new Vector2(inset * 1.6f, 0f);
                srt.offsetMax = new Vector2(-inset * 1.6f, -inset);
                if (pill) MakePill(s, faceH * 0.5f - inset);
                else MakeRounded(s, Mathf.Max(4f, radius - inset));
            }

            if (button)
            {
                c.UIButton = c.Root.gameObject.AddComponent<UIButton>();
                CanvasGroup g = c.Body.gameObject.AddComponent<CanvasGroup>();
                Wire(c.UIButton, ("target", c.Body), ("disabledGroup", g));
            }
            return c;
        }

        /// <summary>Chunky button with a centred label.</summary>
        public static Chunk TextButton(Transform parent, string name, string text, Color face, Color lip, float w, float h,
                                       float fontSize, float radius = 18f)
        {
            Chunk c = Chunky(parent, name, face, lip, w, h, radius, 7f, true);
            TextMeshProUGUI label = Label(c.Face, "Label", text, fontSize, TextStyle.Heading);
            Stretch(label.rectTransform, 10f, 2f, 10f, 2f);
            return c;
        }

        /// <summary>Chunky button with a centred icon.</summary>
        public static Chunk IconButton(Transform parent, string name, string icon, Color face, Color lip, float w, float h,
                                       float iconSize, float radius = 16f)
        {
            Chunk c = Chunky(parent, name, face, lip, w, h, radius, 6f, true);
            Image i = IconImg(c.Face, "Icon", icon, iconSize);
            Place(i.rectTransform, 0.5f, 0.5f, 0f, 0f, iconSize, iconSize);
            return c;
        }

        /// <summary>Round red close button with an X.</summary>
        public static Chunk CloseButton(Transform parent)
        {
            Chunk c = Chunky(parent, "CloseButton", Red, RedLip, 50f, 54f, -1f, 5f, true);
            Image i = IconImg(c.Face, "Icon", "close", 30f);
            Place(i.rectTransform, 0.5f, 0.5f, 0f, 0f, 30f, 30f);
            return c;
        }

        /// <summary>Small round badge (count / "!" / plus).</summary>
        public static Image Badge(Transform parent, string name, Color face, float size, out RectTransform content)
        {
            Image outline = Img(parent, name, Circle, Color.white);
            outline.type = Image.Type.Simple;
            outline.preserveAspect = true;
            outline.rectTransform.sizeDelta = new Vector2(size, size);
            Image fill = Img(outline.transform, "Fill", Circle, face);
            fill.type = Image.Type.Simple;
            Stretch(fill.rectTransform, 2.5f, 2.5f, 2.5f, 2.5f);
            content = fill.rectTransform;
            return outline;
        }

        // ------------------------------------------------------------------ canvases

        public static Canvas RootCanvas(GameObject go, int sortingOrder)
        {
            if (go.GetComponent<RectTransform>() == null) go.AddComponent<RectTransform>();
            go.layer = 5;
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform SafeArea(Transform parent)
        {
            RectTransform rt = Stretch(Rect(parent, "SafeArea"));
            rt.gameObject.AddComponent<SafeAreaFitter>();
            return rt;
        }

        // ------------------------------------------------------------------ serialized wiring

        public static void Wire(Object target, params (string path, object value)[] values)
        {
            var so = new SerializedObject(target);
            foreach ((string path, object value) in values)
            {
                SerializedProperty p = so.FindProperty(path);
                if (p == null) throw new InvalidOperationException($"{target.GetType().Name}: field '{path}' not found");
                Assign(p, value);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Assign(SerializedProperty p, object value)
        {
            switch (value)
            {
                case null:
                    // null = clear an object reference; for struct elements it only reserves the array slot.
                    if (p.propertyType == SerializedPropertyType.ObjectReference) p.objectReferenceValue = null;
                    break;
                case Component comp when p.propertyType == SerializedPropertyType.ObjectReference:
                    // Allow passing a Component where a GameObject is expected and vice versa.
                    p.objectReferenceValue = comp;
                    if (p.objectReferenceValue == null) p.objectReferenceValue = comp.gameObject;
                    if (p.objectReferenceValue == null) throw new InvalidOperationException($"'{p.propertyPath}' rejected {comp.GetType().Name}");
                    break;
                case Object o:
                    p.objectReferenceValue = o;
                    if (p.objectReferenceValue == null) throw new InvalidOperationException($"'{p.propertyPath}' rejected {o.GetType().Name}");
                    break;
                case string s:
                    p.stringValue = s;
                    break;
                case Enum e:
                    p.intValue = Convert.ToInt32(e);
                    break;
                case int i:
                    p.intValue = i;
                    break;
                case float f:
                    p.floatValue = f;
                    break;
                case bool b:
                    p.boolValue = b;
                    break;
                case Vector3 v3 when p.propertyType == SerializedPropertyType.Vector2:
                    p.vector2Value = v3;
                    break;
                case Vector3 v3:
                    p.vector3Value = v3;
                    break;
                case Color col:
                    p.colorValue = col;
                    break;
                case Array arr:
                    p.arraySize = arr.Length;
                    for (int i = 0; i < arr.Length; i++) Assign(p.GetArrayElementAtIndex(i), arr.GetValue(i));
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported value type {value.GetType()} for '{p.propertyPath}'");
            }
        }
    }
}
