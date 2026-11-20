using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Generates all UI sprites procedurally (no external sprite packs): 9-slice rounded shapes at 4x resolution,
    /// sun rays, soft shadow, sky gradient, clouds and a cartoon icon set drawn with signed distance fields.
    /// Menu: Tools/Bus Rush/Generate UI Sprites.
    /// </summary>
    public static class UIArtGenerator
    {
        public const string Dir = "Assets/BusRush/Art/Sprites/UI";
        public const string IconDir = Dir + "/Icons";

        /// <summary>Source pixels per UI unit for 9-slice sprites; set Image.pixelsPerUnitMultiplier = 4 for radius 16.</summary>
        public const int SliceScale = 4;
        public const int RoundRadiusPx = 64;   // => 16 units at multiplier 4
        public const int IconSize = 256;       // 4x of a 64 unit icon

        private static readonly Color Outline = Hex(0x1E2A44);
        private const float OutlineWidth = 0.05f;

        private delegate float Sdf(Vector2 p);

        private struct Layer
        {
            public Sdf Shape;
            public Color Color;
            public bool Outlined;
        }

        [MenuItem("Tools/Bus Rush/Generate UI Sprites")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(Dir);
            Directory.CreateDirectory(IconDir);

            // ---- 9-slice / utility sprites
            SaveSliced("ui_round", RoundedRect(160, RoundRadiusPx, 0f), RoundRadiusPx);
            SaveSliced("ui_circle", RoundedRect(256, 128, 0f), 128);
            SaveSliced("ui_shadow", RoundedRect(192, 48, 28f), 84);
            SaveSliced("ui_white", Solid(8), 0);
            Save("ui_sky", VerticalGradient(8, 256, Hex(0x5FB6F7), Hex(0xBFE6FF)), false);
            Save("ui_rays", Rays(512, 14), false);
            Save("ui_glow", Glow(256), false);
            Save("ui_ring", Ring(256, 0.42f, 0.06f), false);
            Save("ui_cloud", Cloud(256, 128), false);
            Save("ui_lip_shine", VerticalGradient(8, 64, new Color(1, 1, 1, 0.0f), new Color(1, 1, 1, 0.35f)), false);

            // ---- icons
            foreach (KeyValuePair<string, Layer[]> icon in Icons())
            {
                Save("icon_" + icon.Key, Paint(IconSize, icon.Value), true);
            }

            AssetDatabase.Refresh();
            ConfigureImporters();
            Debug.Log("[UIArtGenerator] UI sprites generated.");
        }

        // ------------------------------------------------------------------ importers

        private static readonly Dictionary<string, int> Borders = new Dictionary<string, int>();

        private static void ConfigureImporters()
        {
            foreach (string path in Directory.GetFiles(Dir, "*.png", SearchOption.AllDirectories))
            {
                string assetPath = path.Replace('\\', '/');
                var ti = (TextureImporter)AssetImporter.GetAtPath(assetPath);
                if (ti == null) continue;
                bool isIcon = assetPath.Contains("/Icons/");

                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = 100f;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.filterMode = FilterMode.Bilinear;
                ti.maxTextureSize = 512;
                ti.textureCompression = isIcon ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Uncompressed;
                ti.npotScale = TextureImporterNPOTScale.None;

                string name = Path.GetFileNameWithoutExtension(assetPath);
                int b = Borders.TryGetValue(name, out int border) ? border : 0;
                ti.spriteBorder = new Vector4(b, b, b, b);
                ti.SaveAndReimport();
            }
        }

        private static void SaveSliced(string name, Texture2D tex, int border)
        {
            Borders[name] = border;
            Save(name, tex, false);
        }

        private static void Save(string name, Texture2D tex, bool icon)
        {
            string path = $"{(icon ? IconDir : Dir)}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        // ------------------------------------------------------------------ basic textures

        private static Texture2D NewTex(int w, int h) => new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };

        private static float Coverage(float d, float px) => Mathf.Clamp01(0.5f - d / px);

        /// <summary>White rounded rect filling the texture; with blur > 0 a soft shadow shape instead.</summary>
        private static Texture2D RoundedRect(int size, float radiusPx, float blurPx)
        {
            Texture2D t = NewTex(size, size);
            var px = new Color[size * size];
            float half = size * 0.5f - blurPx;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f);
                    float d = BoxDist(p, new Vector2(half, half), radiusPx);
                    float a = blurPx > 0f
                        ? Mathf.Pow(1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-blurPx * 0.4f, blurPx, d)), 1.5f)
                        : Coverage(d, 1f);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        private static Texture2D Solid(int size)
        {
            Texture2D t = NewTex(size, size);
            var px = new Color[size * size];
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        private static Texture2D VerticalGradient(int w, int h, Color bottom, Color top)
        {
            Texture2D t = NewTex(w, h);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                Color c = Color.Lerp(bottom, top, y / (h - 1f));
                for (int x = 0; x < w; x++) px[y * w + x] = c;
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        private static Texture2D Rays(int size, int count)
        {
            Texture2D t = NewTex(size, size);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2((x + 0.5f) / size - 0.5f, (y + 0.5f) / size - 0.5f) * 2f;
                    float r = p.magnitude;
                    float ang = Mathf.Atan2(p.y, p.x);
                    float s = Mathf.Cos(ang * count);                        // -1..1
                    float pxAng = 1.5f / (size * 0.5f * Mathf.Max(r, 0.05f)); // anti-alias width in angle space
                    float ray = Mathf.Clamp01((s + 0.15f) / (pxAng * count) + 0.5f);
                    float fade = Mathf.Clamp01(1f - r) * Mathf.Clamp01(r * 3f);
                    px[y * size + x] = new Color(1f, 1f, 1f, ray * Mathf.Sqrt(fade));
                }
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        private static Texture2D Glow(int size)
        {
            Texture2D t = NewTex(size, size);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float r = new Vector2((x + 0.5f) / size - 0.5f, (y + 0.5f) / size - 0.5f).magnitude * 2f;
                    float a = Mathf.Clamp01(1f - r);
                    px[y * size + x] = new Color(1f, 1f, 1f, a * a);
                }
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        private static Texture2D Ring(int size, float radius, float thickness)
        {
            Texture2D t = NewTex(size, size);
            var px = new Color[size * size];
            float pxSize = 1f / size;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2((x + 0.5f) / size - 0.5f, (y + 0.5f) / size - 0.5f);
                    float d = Mathf.Abs(p.magnitude - radius) - thickness * 0.5f;
                    px[y * size + x] = new Color(1f, 1f, 1f, Coverage(d, pxSize));
                }
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        private static Texture2D Cloud(int w, int h)
        {
            // Unit = texture width, so y spans 0..h/w (0.5 for a 2:1 texture) and circles stay round.
            Sdf s = Union(C(0.3f, 0.2f, 0.14f), C(0.52f, 0.27f, 0.19f), C(0.74f, 0.2f, 0.13f), B(0.5f, 0.12f, 0.4f, 0.1f, 0.1f));
            Texture2D t = NewTex(w, h);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var p = new Vector2((x + 0.5f) / w, (y + 0.5f) / w);
                    float d = s(p);
                    float a = Coverage(d, 1f / w);
                    px[y * w + x] = new Color(1f, 1f, 1f, a);
                }
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        // ------------------------------------------------------------------ icon painter

        private static Texture2D Paint(int size, Layer[] layers)
        {
            Texture2D t = NewTex(size, size);
            var px = new Color[size * size];
            float pxSize = 1f / size;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                    Color c = Color.clear;

                    float outlineD = float.MaxValue;
                    foreach (Layer l in layers)
                    {
                        if (l.Outlined) outlineD = Mathf.Min(outlineD, l.Shape(p));
                    }
                    if (outlineD < float.MaxValue)
                    {
                        c = Over(c, Outline, Coverage(outlineD - OutlineWidth, pxSize));
                    }

                    foreach (Layer l in layers)
                    {
                        c = Over(c, l.Color, Coverage(l.Shape(p), pxSize));
                    }
                    px[y * size + x] = c;
                }
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        private static Color Over(Color dst, Color src, float coverage)
        {
            float a = src.a * coverage;
            if (a <= 0f) return dst;
            float outA = a + dst.a * (1f - a);
            if (outA <= 0f) return Color.clear;
            Color rgb = (src * a + dst * dst.a * (1f - a)) / outA;
            rgb.a = outA;
            return rgb;
        }

        private static Layer L(Sdf s, Color c, bool outlined = true) => new Layer { Shape = s, Color = c, Outlined = outlined };

        private static Layer Shine(Sdf s, float alpha = 0.55f) => L(s, new Color(1f, 1f, 1f, alpha), false);

        // ------------------------------------------------------------------ SDF primitives (unit space, y up)

        private static float BoxDist(Vector2 p, Vector2 half, float r)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - half + new Vector2(r, r);
            return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        private static Sdf C(float x, float y, float r) => p => (p - new Vector2(x, y)).magnitude - r;

        private static Sdf B(float x, float y, float hx, float hy, float r = 0f)
        {
            var c = new Vector2(x, y);
            var h = new Vector2(hx, hy);
            return p => BoxDist(p - c, h, r);
        }

        private static Sdf Seg(float ax, float ay, float bx, float by, float r)
        {
            var a = new Vector2(ax, ay);
            var b = new Vector2(bx, by);
            return p =>
            {
                Vector2 pa = p - a, ba = b - a;
                float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
                return (pa - ba * h).magnitude - r;
            };
        }

        private static Sdf Poly(float round, params float[] xy)
        {
            int n = xy.Length / 2;
            var v = new Vector2[n];
            for (int i = 0; i < n; i++) v[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
            return p => PolyDist(p, v) - round;
        }

        private static float PolyDist(Vector2 p, Vector2[] v)
        {
            float d = Vector2.Dot(p - v[0], p - v[0]);
            float s = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                Vector2 e = v[j] - v[i];
                Vector2 w = p - v[i];
                Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, Vector2.Dot(b, b));
                bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }

        private static Sdf Arc(float cx, float cy, float radius, float fromDeg, float toDeg, float r, int steps = 28)
        {
            var parts = new Sdf[steps];
            for (int i = 0; i < steps; i++)
            {
                float a0 = Mathf.Deg2Rad * Mathf.Lerp(fromDeg, toDeg, i / (float)steps);
                float a1 = Mathf.Deg2Rad * Mathf.Lerp(fromDeg, toDeg, (i + 1) / (float)steps);
                parts[i] = Seg(cx + Mathf.Cos(a0) * radius, cy + Mathf.Sin(a0) * radius,
                               cx + Mathf.Cos(a1) * radius, cy + Mathf.Sin(a1) * radius, r);
            }
            return Union(parts);
        }

        private static Sdf Star(float cx, float cy, float outer, float inner, float round)
        {
            var xy = new float[20];
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.Deg2Rad * (90f + i * 36f);
                float rr = i % 2 == 0 ? outer : inner;
                xy[i * 2] = cx + Mathf.Cos(a) * rr;
                xy[i * 2 + 1] = cy + Mathf.Sin(a) * rr;
            }
            return Poly(round, xy);
        }

        private static Sdf Union(params Sdf[] s) => p =>
        {
            float d = float.MaxValue;
            foreach (Sdf f in s) d = Mathf.Min(d, f(p));
            return d;
        };

        private static Sdf Sub(Sdf a, Sdf b) => p => Mathf.Max(a(p), -b(p));

        private static Sdf Rot(Sdf s, float deg, float cx = 0.5f, float cy = 0.5f)
        {
            float r = -deg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(r), sin = Mathf.Sin(r);
            var c = new Vector2(cx, cy);
            return p =>
            {
                Vector2 q = p - c;
                return s(c + new Vector2(q.x * cos - q.y * sin, q.x * sin + q.y * cos));
            };
        }

        private static Sdf ArrowHead(float tipX, float tipY, float dirX, float dirY, float length, float halfWidth)
        {
            var d = new Vector2(dirX, dirY).normalized;
            var n = new Vector2(-d.y, d.x);
            Vector2 tip = new Vector2(tipX, tipY);
            Vector2 b = tip - d * length;
            Vector2 l = b + n * halfWidth, r = b - n * halfWidth;
            return Poly(0.015f, tip.x, tip.y, l.x, l.y, r.x, r.y);
        }

        public static Color Hex(uint rgb, float a = 1f)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);
        }

        // ------------------------------------------------------------------ icon set

        private static Dictionary<string, Layer[]> Icons()
        {
            Color white = Hex(0xFFFFFF), red = Hex(0xEF3B3B), redLight = Hex(0xFF6A6A), gold = Hex(0xFFC93C),
                  goldDark = Hex(0xE6A21B), goldLight = Hex(0xFFE07A), blue = Hex(0x3A8DF5), navy = Hex(0x1E2A44),
                  grey = Hex(0xB7C0D3), green = Hex(0x3CC35B), purple = Hex(0x8E5AD8), glass = Hex(0x2B3646);

            var icons = new Dictionary<string, Layer[]>
            {
                ["heart"] = new[]
                {
                    L(Union(C(0.355f, 0.6f, 0.2f), C(0.645f, 0.6f, 0.2f), Poly(0.02f, 0.175f, 0.54f, 0.825f, 0.54f, 0.5f, 0.16f)), red),
                    Shine(C(0.33f, 0.66f, 0.065f), 0.7f)
                },
                ["coin"] = Coin(0.5f, 0.5f, 0.4f, gold, goldDark, goldLight),
                ["gear"] = new[]
                {
                    L(Sub(Union(C(0.5f, 0.5f, 0.29f), Teeth(8, 0.335f, 0.075f, 0.075f)), C(0.5f, 0.5f, 0.11f)), white),
                },
                ["cart"] = new[]
                {
                    L(Union(Seg(0.08f, 0.76f, 0.2f, 0.76f, 0.04f), Seg(0.2f, 0.76f, 0.3f, 0.36f, 0.04f)), navy),
                    L(Poly(0.03f, 0.22f, 0.66f, 0.86f, 0.66f, 0.77f, 0.38f, 0.31f, 0.38f), blue),
                    Shine(B(0.54f, 0.6f, 0.24f, 0.025f, 0.02f), 0.5f),
                    L(C(0.37f, 0.22f, 0.075f), navy),
                    L(C(0.7f, 0.22f, 0.075f), navy)
                },
                ["gift"] = new[]
                {
                    L(Union(C(0.38f, 0.75f, 0.085f), C(0.62f, 0.75f, 0.085f)), gold),
                    L(B(0.5f, 0.36f, 0.29f, 0.21f, 0.03f), red),
                    L(B(0.5f, 0.6f, 0.34f, 0.08f, 0.03f), redLight),
                    L(B(0.5f, 0.45f, 0.065f, 0.3f, 0.01f), gold, false),
                    Shine(B(0.32f, 0.62f, 0.1f, 0.025f, 0.02f), 0.5f)
                },
                ["noads"] = new[]
                {
                    L(B(0.5f, 0.5f, 0.3f, 0.21f, 0.05f), white),
                    L(Union(Seg(0.3f, 0.39f, 0.37f, 0.61f, 0.032f), Seg(0.37f, 0.61f, 0.44f, 0.39f, 0.032f), Seg(0.33f, 0.47f, 0.41f, 0.47f, 0.028f),
                            Seg(0.53f, 0.39f, 0.53f, 0.61f, 0.032f), Arc(0.56f, 0.5f, 0.11f, -90f, 90f, 0.032f, 14),
                            Seg(0.53f, 0.61f, 0.56f, 0.61f, 0.032f), Seg(0.53f, 0.39f, 0.56f, 0.39f, 0.032f)), navy, false),
                    L(Union(Sub(C(0.5f, 0.5f, 0.42f), C(0.5f, 0.5f, 0.34f)), Rot(B(0.5f, 0.5f, 0.38f, 0.04f, 0.02f), -45f)), red)
                },
                ["play"] = new[] { L(Poly(0.06f, 0.34f, 0.24f, 0.34f, 0.76f, 0.78f, 0.5f), white) },
                ["pause"] = new[] { L(Union(B(0.36f, 0.5f, 0.085f, 0.27f, 0.05f), B(0.64f, 0.5f, 0.085f, 0.27f, 0.05f)), white) },
                ["restart"] = new[]
                {
                    // Arc open on the right; arrowhead at the 50 deg end points clockwise into the gap.
                    L(Union(Arc(0.5f, 0.5f, 0.27f, 50f, 340f, 0.06f), ArrowHead(0.804f, 0.598f, 0.766f, -0.643f, 0.2f, 0.15f)), white)
                },
                ["home"] = new[]
                {
                    L(Union(Poly(0.03f, 0.13f, 0.52f, 0.5f, 0.85f, 0.87f, 0.52f), B(0.71f, 0.74f, 0.05f, 0.09f, 0.01f),
                            Sub(B(0.5f, 0.35f, 0.26f, 0.2f, 0.03f), B(0.5f, 0.24f, 0.075f, 0.11f, 0.03f))), white)
                },
                ["lock"] = new[]
                {
                    L(Union(Arc(0.5f, 0.57f, 0.17f, 0f, 180f, 0.05f, 18), Seg(0.33f, 0.57f, 0.33f, 0.45f, 0.05f), Seg(0.67f, 0.57f, 0.67f, 0.45f, 0.05f)), grey),
                    L(B(0.5f, 0.34f, 0.28f, 0.22f, 0.06f), gold),
                    L(Union(C(0.5f, 0.38f, 0.05f), B(0.5f, 0.3f, 0.025f, 0.06f, 0.02f)), navy, false),
                    Shine(B(0.36f, 0.48f, 0.09f, 0.025f, 0.02f), 0.6f)
                },
                ["plus"] = new[] { L(Union(B(0.5f, 0.5f, 0.3f, 0.095f, 0.07f), B(0.5f, 0.5f, 0.095f, 0.3f, 0.07f)), white) },
                ["check"] = new[] { L(Union(Seg(0.22f, 0.5f, 0.42f, 0.3f, 0.075f), Seg(0.42f, 0.3f, 0.8f, 0.7f, 0.075f)), white) },
                ["close"] = new[] { L(Union(Seg(0.29f, 0.29f, 0.71f, 0.71f, 0.085f), Seg(0.29f, 0.71f, 0.71f, 0.29f, 0.085f)), white) },
                ["star"] = new[]
                {
                    L(Star(0.5f, 0.47f, 0.42f, 0.2f, 0.035f), gold),
                    Shine(C(0.4f, 0.6f, 0.06f), 0.65f)
                },
                ["crown"] = new[]
                {
                    L(Poly(0.03f, 0.16f, 0.3f, 0.14f, 0.68f, 0.33f, 0.5f, 0.5f, 0.78f, 0.67f, 0.5f, 0.86f, 0.68f, 0.84f, 0.3f), gold),
                    L(B(0.5f, 0.28f, 0.35f, 0.075f, 0.035f), goldDark),
                    L(Union(C(0.14f, 0.7f, 0.055f), C(0.5f, 0.8f, 0.06f), C(0.86f, 0.7f, 0.055f)), red),
                    L(C(0.5f, 0.28f, 0.04f), red, false),
                    Shine(B(0.32f, 0.45f, 0.05f, 0.08f, 0.04f), 0.45f)
                },
                ["arrange"] = new[]
                {
                    L(Union(Seg(0.16f, 0.65f, 0.66f, 0.65f, 0.055f), ArrowHead(0.88f, 0.65f, 1f, 0f, 0.2f, 0.15f)), blue),
                    L(Union(Seg(0.34f, 0.35f, 0.84f, 0.35f, 0.055f), ArrowHead(0.12f, 0.35f, -1f, 0f, 0.2f, 0.15f)), blue)
                },
                ["shuffle"] = new[]
                {
                    L(Union(Seg(0.12f, 0.32f, 0.36f, 0.32f, 0.05f), Seg(0.36f, 0.32f, 0.6f, 0.68f, 0.05f), Seg(0.6f, 0.68f, 0.7f, 0.68f, 0.05f),
                            ArrowHead(0.9f, 0.68f, 1f, 0f, 0.2f, 0.14f)), green),
                    L(Union(Seg(0.12f, 0.68f, 0.36f, 0.68f, 0.05f), Seg(0.36f, 0.68f, 0.6f, 0.32f, 0.05f), Seg(0.6f, 0.32f, 0.7f, 0.32f, 0.05f),
                            ArrowHead(0.9f, 0.32f, 1f, 0f, 0.2f, 0.14f)), purple)
                },
                ["car"] = new[]
                {
                    L(B(0.5f, 0.5f, 0.2f, 0.37f, 0.1f), white),
                    L(B(0.5f, 0.62f, 0.15f, 0.07f, 0.03f), glass, false),
                    L(B(0.5f, 0.3f, 0.13f, 0.05f, 0.03f), glass, false)
                },
                ["sound"] = new[]
                {
                    L(Poly(0.03f, 0.16f, 0.38f, 0.3f, 0.38f, 0.5f, 0.2f, 0.5f, 0.8f, 0.3f, 0.62f, 0.16f, 0.62f), white),
                    L(Union(Arc(0.5f, 0.5f, 0.17f, -45f, 45f, 0.045f, 10), Arc(0.5f, 0.5f, 0.31f, -45f, 45f, 0.045f, 14)), white)
                },
                ["music"] = new[]
                {
                    L(Union(C(0.31f, 0.27f, 0.11f), C(0.69f, 0.35f, 0.11f), Seg(0.4f, 0.3f, 0.4f, 0.74f, 0.045f), Seg(0.78f, 0.38f, 0.78f, 0.82f, 0.045f),
                            Poly(0.02f, 0.38f, 0.78f, 0.8f, 0.86f, 0.8f, 0.71f, 0.38f, 0.63f)), white)
                },
                ["vibrate"] = new[]
                {
                    L(B(0.5f, 0.5f, 0.15f, 0.3f, 0.05f), white),
                    L(B(0.5f, 0.52f, 0.09f, 0.19f, 0.02f), blue, false),
                    L(Union(Arc(0.5f, 0.5f, 0.27f, 155f, 205f, 0.035f, 8), Arc(0.5f, 0.5f, 0.36f, 160f, 200f, 0.035f, 8),
                            Arc(0.5f, 0.5f, 0.27f, -25f, 25f, 0.035f, 8), Arc(0.5f, 0.5f, 0.36f, -20f, 20f, 0.035f, 8)), white)
                },
                ["video"] = new[]
                {
                    L(B(0.44f, 0.5f, 0.3f, 0.22f, 0.06f), white),
                    L(Poly(0.02f, 0.7f, 0.5f, 0.9f, 0.68f, 0.9f, 0.32f), white),
                    L(Poly(0.02f, 0.36f, 0.38f, 0.36f, 0.62f, 0.56f, 0.5f), blue, false)
                }
            };

            // Coin piles for shop packs.
            var pile = new List<Layer>();
            pile.AddRange(Coin(0.34f, 0.36f, 0.24f, gold, goldDark, goldLight));
            pile.AddRange(Coin(0.66f, 0.36f, 0.24f, gold, goldDark, goldLight));
            pile.AddRange(Coin(0.5f, 0.62f, 0.24f, gold, goldDark, goldLight));
            icons["coins_pile"] = pile.ToArray();

            var heap = new List<Layer>();
            heap.AddRange(Coin(0.24f, 0.28f, 0.19f, gold, goldDark, goldLight));
            heap.AddRange(Coin(0.5f, 0.26f, 0.19f, gold, goldDark, goldLight));
            heap.AddRange(Coin(0.76f, 0.28f, 0.19f, gold, goldDark, goldLight));
            heap.AddRange(Coin(0.37f, 0.5f, 0.19f, gold, goldDark, goldLight));
            heap.AddRange(Coin(0.63f, 0.5f, 0.19f, gold, goldDark, goldLight));
            heap.AddRange(Coin(0.5f, 0.73f, 0.19f, gold, goldDark, goldLight));
            icons["coins_heap"] = heap.ToArray();

            return icons;
        }

        private static Layer[] Coin(float x, float y, float r, Color gold, Color dark, Color light)
        {
            return new[]
            {
                L(C(x, y, r), gold),
                L(Sub(C(x, y, r * 0.74f), C(x, y, r * 0.6f)), dark, false),
                L(B(x, y, r * 0.09f, r * 0.34f, r * 0.08f), dark, false),
                Shine(C(x - r * 0.42f, y + r * 0.42f, r * 0.16f), 0.75f)
            };
        }

        private static Sdf Teeth(int count, float distance, float halfW, float halfH)
        {
            var parts = new Sdf[count];
            for (int i = 0; i < count; i++)
            {
                parts[i] = Rot(B(0.5f, 0.5f + distance, halfW, halfH, 0.02f), i * 360f / count);
            }
            return Union(parts);
        }
    }
}
