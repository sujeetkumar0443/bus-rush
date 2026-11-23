using System;
using System.IO;
using System.Linq;
using BusRush.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static BusRush.EditorTools.UIKit;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Road Map v3: layer order (tiles, mist, path dots, sequence fx, banner, nodes, chests, current-node marker), the red Play node,
    /// small lock badges on locked nodes, a lower mist, the level-complete sequence parts (grey / gold path, travelling dot, skip catcher)
    /// and the Level Start card. <see cref="ApplyRoadMapV3"/> is idempotent: it is called by the full Road Map build and by the in-place
    /// patch (Tools > Bus Rush > Patch Road Map v3) that edits RoadMap.prefab without replacing it (the Home scene keeps its wiring).
    /// </summary>
    public static partial class UIBuilder
    {
        private const string MapArt = "Assets/BusRush/Art/UI_v2/Map";

        [MenuItem("Tools/Bus Rush/Patch Road Map v3")]
        public static void PatchRoadMapV3()
        {
            Load();
            string path = $"{PrefabDir}/RoadMap.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ApplyRoadMapV3(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[UIBuilder] Road Map v3 applied to RoadMap.prefab.");
        }

        public static void PatchRoadMapV3Batch()
        {
            try
            {
                PatchRoadMapV3();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[UIBuilder] FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }

        private static void EnsureSpriteImport(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti == null || (ti.textureType == TextureImporterType.Sprite && ti.spriteImportMode == SpriteImportMode.Single)) return;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.SaveAndReimport();
        }

        /// <summary>Soft ring (annulus) for the current node's pulse and the unlock pop.</summary>
        private static Sprite RingSprite() => GeneratedSprite("map_ring", 128, (u, v) =>
        {
            float d = new Vector2(u - 0.5f, v - 0.5f).magnitude / 0.5f;
            float a = Smooth(0.66f, 0.78f, d) * Smooth(0.99f, 0.9f, d);
            return new Color(1f, 1f, 1f, Mathf.Clamp01(a));
        });

        // ------------------------------------------------------------------ apply

        private static void ApplyRoadMapV3(GameObject root)
        {
            Material greenText = LilitaOutline("Green", H(0x1D5F12));
            Material blueText = LilitaOutline("Blue", H(0x103E86));
            Material brownText = LilitaOutline("DarkBrown", H(0x8A4A05));
            EnsureSpriteImport(MapArt + "/disc_red.png");
            Sprite soft = SoftCircleSprite(), ellipse = SoftEllipseSprite(), rays = MapRaysSprite(), ring = RingSprite();
            Sprite star = UiV2Sprite("Icons/star.png"), sparkle = UiV2Sprite("Splash/sparkle.png"), coin = UiV2Sprite("Icons/coin.png");

            var screen = root.GetComponent<RoadMapScreen>();
            var content = (RectTransform)root.transform.Find("Scroll/Content");
            var templates = (RectTransform)content.Find("Templates");

            // Old pieces: node / seam templates are rebuilt, the bus and the old trail are gone for good.
            foreach (string old in new[] { "Node", "Seam", "GoldRoadLine" })
            {
                Transform t = templates.Find(old);
                if (t != null) UnityEngine.Object.DestroyImmediate(t.gameObject);
            }
            Transform oldSeams = content.Find("Seams");
            if (oldSeams != null) UnityEngine.Object.DestroyImmediate(oldSeams.gameObject);
            Transform fxOld = content.Find("Fx");
            if (fxOld != null)
            {
                for (int i = fxOld.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(fxOld.GetChild(i).gameObject);
            }

            // Layers, back to front: tiles, mist, road (path dots), fx, banner (above the dashes so they never cross its text), nodes, chests, marker.
            RectTransform Layer(string name)
            {
                Transform existing = content.Find(name);
                if (existing != null) return (RectTransform)existing;
                RectTransform l = Stretch(Rect(content, name));
                l.pivot = Vector2.zero;
                return l;
            }
            RectTransform tiles = Layer("Tiles"), mist = Layer("Mist"), banner = Layer("Banner"), road = Layer("Road"), fx = Layer("Fx"),
                          nodes = Layer("Nodes"), chests = Layer("Chests"), marker = Layer("Marker");
            string[] order = { "Tiles", "Mist", "Road", "Fx", "Banner", "Nodes", "Chests", "Marker", "Templates" };
            for (int i = 0; i < order.Length; i++) content.Find(order[i]).SetSiblingIndex(i);

            // Templates.
            MapNodeView nodeT = BuildMapNode(templates, ellipse, ring, soft, star);
            MapSeamView seamT = BuildMapSeam(templates, soft, blueText);
            Transform roadLineT = templates.Find("RoadLine");
            GameObject goldGo = UnityEngine.Object.Instantiate(roadLineT.gameObject, templates);
            goldGo.name = "GoldRoadLine";
            UiPolyline goldLine = goldGo.GetComponent<UiPolyline>();
            goldLine.color = H(0xFFC531);
            goldLine.SetStyle(5f, 11f, 13f);
            goldLine.raycastTarget = false;
            goldGo.SetActive(false);

            // Level-complete sequence parts: grey dashes under gold dashes + glow, a travelling dot.
            UiPolyline Line(string name, Color c, float w, float d, float g)
            {
                RectTransform rt = Stretch(Rect(fx, name));
                rt.pivot = Vector2.zero;
                rt.gameObject.AddComponent<CanvasRenderer>();
                UiPolyline line = rt.gameObject.AddComponent<UiPolyline>();
                line.raycastTarget = false;
                line.color = c;
                line.SetStyle(w, d, g);
                rt.gameObject.SetActive(false);
                return line;
            }
            UiPolyline greyPath = Line("GreyPath", H(0x8E98AB), 5f, 11f, 13f);
            UiPolyline goldGlow = Line("GoldGlow", new Color(1f, 0.82f, 0.25f, 0.35f), 12f, 0f, 0f);
            UiPolyline goldPath = Line("GoldPath", H(0xFFC531), 5f, 11f, 13f);
            Image dotImage = Img(fx, "Dot", soft, new Color(1f, 0.93f, 0.55f, 0.95f));
            dotImage.raycastTarget = false;
            RectTransform dot = dotImage.rectTransform;
            dot.anchorMin = dot.anchorMax = Vector2.zero;
            dot.pivot = new Vector2(0.5f, 0.5f);
            dot.sizeDelta = new Vector2(46f, 46f);
            dot.gameObject.SetActive(false);

            // Skip catcher (tap anywhere during the sequence): above the map, below the top bar.
            Transform oldCatcher = root.transform.Find("SkipCatcher");
            if (oldCatcher != null) UnityEngine.Object.DestroyImmediate(oldCatcher.gameObject);
            Image catcherImg = Img(root.transform, "SkipCatcher", White, new Color(0f, 0f, 0f, 0f));
            Stretch(catcherImg.rectTransform);
            Button catcher = catcherImg.gameObject.AddComponent<Button>();
            catcher.transition = Selectable.Transition.None;
            catcher.targetGraphic = catcherImg;
            catcher.gameObject.SetActive(false);
            catcher.transform.SetSiblingIndex(root.transform.Find("Scroll").GetSiblingIndex() + 1);

            // Level Start card: the last child of the map, over everything.
            Transform oldCard = root.transform.Find("LevelCard");
            if (oldCard != null) UnityEngine.Object.DestroyImmediate(oldCard.gameObject);
            LevelStartCard card = BuildLevelCard((RectTransform)root.transform, star, coin);

            Wire(screen,
                ("tilesLayer", tiles), ("mistLayer", mist), ("bannerLayer", banner), ("roadLayer", road), ("fxLayer", fx),
                ("nodesLayer", nodes), ("chestLayer", chests), ("markerLayer", marker),
                ("nodeTemplate", nodeT), ("seamTemplate", seamT), ("goldRoadTemplate", goldLine),
                ("greyPath", greyPath), ("goldGlow", goldGlow), ("goldPath", goldPath), ("dot", dot), ("dotImage", dotImage),
                ("skipCatcher", catcher), ("levelCard", card));
            if (templates.Find("Chest") != null) Wire(screen, ("chestTemplate", templates.Find("Chest").GetComponent<Button>()));
        }

        // ------------------------------------------------------------------ node: "map pin" (style D)

        private const float PinUnit = 48f;                 // design px per normalized sprite unit (the pin sprite covers 96 x 96 design px)
        private const float PinHalf = 26f;                 // the pin is a 52 px square rotated -45 degrees: radius 26 round part, tip 36.8 px below its centre
        private const float NodeW = 72f, NodeH = 100f, NodeAbove = 72f;   // tap box: 72 x 100, 72 px above the tip and 28 below (>= 64 px)

        /// <summary>
        /// Signed distance (negative inside) of the pin in normalized coordinates (y up): a square with corner radii 50% 50% 50% 0 (the
        /// sharp corner bottom-left), rotated -45 degrees so that corner is the point facing down.
        /// </summary>
        private static float PinSdf(float x, float y)
        {
            float h = PinHalf / PinUnit, c = 0.70710678f;
            float px = x * c + y * c, py = -x * c + y * c;          // back into the unrotated square
            float r = (px > 0f || py > 0f) ? h : 0f;               // TR, BR, TL round; BL sharp
            float qx = Mathf.Abs(px) - h + r, qy = Mathf.Abs(py) - h + r;
            return Mathf.Min(Mathf.Max(qx, qy), 0f) + new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude - r;
        }

        /// <summary>White-edged gradient pin with its white 40 px number circle and a bottom lip (the pin moved 6 px down in a darker colour).</summary>
        private static Color PinPixel(float u, float v, Color top, Color bottom, Color lip)
        {
            float x = (u - 0.5f) * 2f, y = (v - 0.5f) * 2f;
            float h = PinHalf / PinUnit;
            float db = PinSdf(x, y) * PinUnit;                    // px, negative inside
            float dl = PinSdf(x, y + 6f / PinUnit) * PinUnit;     // lip
            float aBody = Mathf.Clamp01(0.5f - db), aLip = Mathf.Clamp01(0.5f - dl);
            Color grad = Color.Lerp(top, bottom, Mathf.InverseLerp(h, -1.4142f * h, y));
            Color body = Color.Lerp(Color.white, grad, Smooth(-2.5f, -3.6f, db));         // ~2.5 px white edge
            float dc = new Vector2(x, y).magnitude * PinUnit - 20f;                        // number circle: 40 px, upright
            Color circle = Color.Lerp(Color.white, new Color(0.86f, 0.89f, 0.95f), Smooth(-2.2f, 0f, dc));
            body = Color.Lerp(body, circle, Mathf.Clamp01(0.5f - dc));
            Color c = Color.Lerp(lip, body, aBody);
            return new Color(c.r, c.g, c.b, Mathf.Max(aLip, aBody));
        }

        private static readonly Vector2[] CrownPoly =
        {
            new Vector2(0.10f, 0.18f), new Vector2(0.10f, 0.68f), new Vector2(0.31f, 0.44f), new Vector2(0.50f, 0.82f),
            new Vector2(0.69f, 0.44f), new Vector2(0.90f, 0.68f), new Vector2(0.90f, 0.18f)
        };

        private static float PolyDistance(Vector2 p, Vector2[] v)
        {
            float d = (p - v[0]).sqrMagnitude, sign = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                Vector2 e = v[j] - v[i], w = p - v[i];
                Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, b.sqrMagnitude);
                bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) sign = -sign;
            }
            return sign * Mathf.Sqrt(d);
        }

        /// <summary>Small gold crown: three peaks, three balls, a darker outline.</summary>
        private static Color CrownPixel(float u, float v)
        {
            var p = new Vector2(u, v);
            float d = PolyDistance(p, CrownPoly);
            d = Mathf.Min(d, (p - new Vector2(0.10f, 0.73f)).magnitude - 0.075f);
            d = Mathf.Min(d, (p - new Vector2(0.50f, 0.87f)).magnitude - 0.085f);
            d = Mathf.Min(d, (p - new Vector2(0.90f, 0.73f)).magnitude - 0.075f);
            float px = d * 128f;                                           // px at 128
            float a = Mathf.Clamp01(0.5f - px);
            Color gold = Color.Lerp(new Color32(0xF5, 0xA6, 0x23, 0xFF), new Color32(0xFF, 0xE2, 0x7A, 0xFF), Mathf.InverseLerp(0.15f, 0.9f, v));
            Color c = Color.Lerp(new Color32(0xB8, 0x73, 0x0F, 0xFF), gold, Smooth(-1.2f, -3.6f, px));
            if (v < 0.34f && v > 0.18f && u > 0.14f && u < 0.86f) c = Color.Lerp(c, new Color32(0xC9, 0x2B, 0x3A, 0xFF), 0.85f); // red band
            return new Color(c.r, c.g, c.b, a);
        }

        /// <summary>Writes (always, so a tweak shows) a coloured procedural sprite into Art/UI_v2/Generated.</summary>
        private static Sprite GeneratedColorSprite(string name, int size, Func<float, float, Color> pixel)
        {
            string path = $"{GeneratedDir}/{name}.png";
            Directory.CreateDirectory(GeneratedDir);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++) px[y * size + x] = pixel((x + 0.5f) / size, (y + 0.5f) / size);
            }
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Sprite PinSprite(string name, int topHex, int bottomHex, int lipHex)
        {
            Color top = H(topHex), bottom = H(bottomHex), lip = H(lipHex);
            return GeneratedColorSprite(name, 192, (u, v) => PinPixel(u, v, top, bottom, lip));
        }

        /// <summary>Lilita One digits with a hard 1 px underlay (no outline, never black): crisp on the white circle. Lives outside the font folder.</summary>
        private static Material PinNumberMaterial()
        {
            string path = MapArt + "/LilitaOne PinNumber.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Theme.headingFont.material) { name = "LilitaOne PinNumber" };
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture(ShaderUtilities.ID_MainTex, Theme.headingFont.atlasTexture);
            Theme.ApplyPlainText(m);
            m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0.1f, 0.16f, 0.4f, 0.45f));
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
            m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
            m.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static MapNodeView BuildMapNode(RectTransform parent, Sprite ellipse, Sprite ring, Sprite soft, Sprite star)
        {
            Sprite pinDone = PinSprite("map_pin_done", 0xFFB04D, 0xF2711C, 0xC0500C);
            Sprite pinCurrent = PinSprite("map_pin_current", 0x7CE05B, 0x2CB43A, 0x16792A);
            Sprite pinLocked = PinSprite("map_pin_locked", 0xC9D2E3, 0x95A2BC, 0x6B7894);
            Sprite pinBoss = PinSprite("map_pin_boss", 0xFF8383, 0xDB2B3B, 0x8A1020);
            Sprite crownSprite = GeneratedColorSprite("map_crown", 128, CrownPixel);
            Material numberMat = PinNumberMaterial();

            RectTransform node = Rect(parent, "Node");
            node.anchorMin = node.anchorMax = Vector2.zero;
            node.pivot = new Vector2(0.5f, (NodeH - NodeAbove) / NodeH);   // the node position is the pin's tip
            node.sizeDelta = new Vector2(NodeW, NodeH);
            Image hit = node.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            Button button = node.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            Wire(node.gameObject.AddComponent<UIButton>(), ("pressedScale", 0.92f), ("lightHaptic", true));

            // Box centred at (cx, heightAboveTip) in the tap box.
            RectTransform At(RectTransform rt, float cx, float heightAboveTip, float w, float h) =>
                Box(rt, NodeW * 0.5f + cx - w * 0.5f, (NodeAbove - heightAboveTip) - h * 0.5f, w, h);

            // Soft ground shadow under the point, two ripples (current).
            Image shadow = Img(node, "Shadow", ellipse, new Color(0.05f, 0.1f, 0.25f, 0.38f));
            shadow.raycastTarget = false;
            At(shadow.rectTransform, 0f, -1f, 54f, 17f);
            var ripples = new Image[2];
            for (int i = 0; i < 2; i++)
            {
                ripples[i] = Img(node, "Ripple" + i, ring, new Color(1f, 1f, 1f, 0f));
                ripples[i].raycastTarget = false;
                At(ripples[i].rectTransform, 0f, 0f, 64f, 64f);
            }

            // Pin body: its pivot is the tip, so lifting / squashing / shaking works around the point.
            float tipBelowCentre = PinHalf * 1.41421f;
            RectTransform body = Rect(node, "Body");
            body.anchorMin = body.anchorMax = new Vector2(0f, 1f);
            body.pivot = new Vector2(0.5f, (48f - tipBelowCentre) / 96f);
            body.sizeDelta = new Vector2(96f, 96f);
            body.anchoredPosition = new Vector2(NodeW * 0.5f, -NodeAbove);
            Image pin = Img(body, "Pin", pinCurrent, Color.white);
            pin.raycastTarget = false;
            Stretch(pin.rectTransform);
            TextMeshProUGUI number = V2Text(body, "Number", "1", 20f, true, MapNodeView.NumberCurrent, numberMat);
            Place(number.rectTransform, 0.5f, 0.5f, 0f, -1f, 40f, 28f);
            Image lockIcon = Img(body, "Lock", UiV2Sprite("Map/lock.png"), new Color32(0x6B, 0x78, 0x94, 0xFF));
            lockIcon.raycastTarget = false;
            lockIcon.preserveAspect = true;
            Place(lockIcon.rectTransform, 0.5f, 0.5f, 0f, 0f, 20f, 24f);
            Image crown = Img(body, "Crown", crownSprite, Color.white);
            crown.raycastTarget = false;
            Place(crown.rectTransform, 0.5f, 0.5f, 0f, 36f, 32f, 24f);

            // Done: three gold stars under the pin.
            RectTransform starsRow = Rect(node, "Stars");
            At(starsRow, 0f, -15f, 60f, 22f);
            var stars = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                stars[i] = Img(starsRow, "Star" + i, star, Color.white);
                stars[i].raycastTarget = false;
                Place(stars[i].rectTransform, 0.5f, 0.5f, (i - 1) * 19f, i == 1 ? -2f : 1f, i == 1 ? 21f : 18f, i == 1 ? 21f : 18f);
            }

            // Current: white PLAY pill above the pin (bobs).
            RectTransform play = Rect(node, "Play");
            At(play, 0f, 92f, 64f, 32f);
            Image pill = RoundImg(play, "Pill", 13f, Color.white);
            pill.raycastTarget = false;
            Place(pill.rectTransform, 0.5f, 0.5f, 0f, 3f, 62f, 26f);
            Image pointer = Img(play, "Pointer", White, Color.white);
            pointer.raycastTarget = false;
            Place(pointer.rectTransform, 0.5f, 0f, 0f, 6f, 9f, 9f);
            pointer.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            TextMeshProUGUI playText = V2Text(play, "Text", "PLAY", 15f, true, MapNodeView.NumberCurrent, numberMat);
            Place(playText.rectTransform, 0.5f, 0.5f, 0f, 3f, 62f, 24f);

            // Unlock fx: ring flash and 6 padlock pieces at the pin's circle centre.
            float centre = tipBelowCentre;
            Image flash = Img(node, "Flash", ring, new Color(1f, 1f, 1f, 0f));
            flash.raycastTarget = false;
            At(flash.rectTransform, 0f, centre, 88f, 88f);
            var pieces = new Image[6];
            for (int i = 0; i < 6; i++)
            {
                pieces[i] = Img(node, "Piece" + i, soft, new Color(0.62f, 0.68f, 0.8f, 1f));
                pieces[i].raycastTarget = false;
                At(pieces[i].rectTransform, 0f, centre, 9f, 9f);
            }

            MapNodeView view = node.gameObject.AddComponent<MapNodeView>();
            Wire(view, ("button", button), ("body", body), ("pin", pin), ("pinDone", pinDone), ("pinCurrent", pinCurrent),
                ("pinLocked", pinLocked), ("pinBoss", pinBoss), ("number", number), ("lockIcon", lockIcon), ("crown", crown),
                ("shadow", shadow), ("starsRow", starsRow), ("stars", stars), ("playMarker", play), ("playText", playText),
                ("ripples", ripples), ("flashRing", flash), ("pieces", pieces));
            node.gameObject.SetActive(false);
            return view;
        }

        // ------------------------------------------------------------------ seam (mist + banner, shown by two instances)

        private static MapSeamView BuildMapSeam(RectTransform parent, Sprite soft, Material blueText)
        {
            // Seam rect 390x140 centred on the tile seam; design y = seam - 70 + top.
            RectTransform seam = Rect(parent, "Seam");
            seam.anchorMin = seam.anchorMax = Vector2.zero;
            seam.pivot = new Vector2(0f, 0.5f);
            seam.sizeDelta = new Vector2(RoadMapGeometry.FrameWidth, 140f);
            const float top0 = 1691.5f - 70f;
            // Mist: the same six clouds, about 45 % as tall and a bit lighter, centred on the seam so it never reaches a node.
            (float l, float t, float w, float h)[] clouds =
            {
                (-85f, 1656f, 150f, 80f), (-15f, 1643f, 170f, 86f), (70f, 1658f, 180f, 84f),
                (165f, 1644f, 170f, 88f), (250f, 1658f, 160f, 80f), (325f, 1653f, 150f, 78f)
            };
            var cloudObjects = new GameObject[clouds.Length];
            for (int i = 0; i < clouds.Length; i++)
            {
                float h = clouds[i].h * 0.45f;
                float cy = clouds[i].t + clouds[i].h * 0.5f; // original vertical centre
                Image c = Img(seam, "Cloud" + i, soft, new Color(1f, 1f, 1f, 0.85f));
                c.raycastTarget = false;
                Box(c.rectTransform, clouds[i].l, cy - h * 0.5f - top0, clouds[i].w, h);
                cloudObjects[i] = c.gameObject;
            }

            RectTransform sign = V2Box(seam, "WorldSign", 20f, H(0x7FD3FF), H(0x2F86E8), null, 4f, H(0x103E86), 5f);
            Box(sign, 85f, 1662f - top0, MapSeamView.BannerWidth, MapSeamView.BannerHeight);
            TextMeshProUGUI num = V2Text(sign, "World", "WORLD 2", 11f, true, H(0xD6F0FF));
            num.characterSpacing = 8f;
            Place(num.rectTransform, 0.5f, 0.5f, 0f, 11f, 200f, 14f);
            TextMeshProUGUI name = V2Text(sign, "Name", "Snow", 22f, true, Color.white, blueText);
            Place(name.rectTransform, 0.5f, 0.5f, 0f, -7f, 200f, 26f);
            Button signButton = V2Button(sign);

            // Old lock badge: kept (inactive) so serialized references stay valid; locked nodes carry their own lock now.
            RectTransform badge = V2Box(sign, "Lock", 12f, H(0x24345E), H(0x24345E), null, 2f, Color.white, 0f);
            Place(badge, 1f, 1f, 16f, 16f, 80f, 28f, 1f, 1f);
            TextMeshProUGUI lockText = V2Text(badge, "Text", "Level 11", 12f, true, Color.white, null, TextAlignmentOptions.Left);
            Stretch(lockText.rectTransform, 24f, 0f, 4f, 0f);
            badge.gameObject.SetActive(false);

            MapSeamView view = seam.gameObject.AddComponent<MapSeamView>();
            Wire(view, ("signButton", signButton), ("worldNumber", num), ("worldName", name), ("lockBadge", badge.gameObject),
                ("lockText", lockText), ("clouds", cloudObjects), ("sign", sign));
            seam.gameObject.SetActive(false);
            return view;
        }

        // ------------------------------------------------------------------ level card

        private static LevelStartCard BuildLevelCard(RectTransform root, Sprite star, Sprite coin)
        {
            Color navy = H(0x1B2A5C), navyEdge = H(0x1B4FA0);
            RectTransform layer = Stretch(Rect(root, "LevelCard"));

            Image dim = Img(layer, "Dim", White, new Color(0.04f, 0.08f, 0.22f, 0f));
            Stretch(dim.rectTransform);
            dim.raycastTarget = true;
            Button dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.targetGraphic = dim;

            RectTransform card = Place(Rect(layer, "Card"), 0.5f, 0.5f, 0f, 0f, 300f, 380f);
            CanvasGroup cardGroup = card.gameObject.AddComponent<CanvasGroup>();
            RectTransform body = V2Box(card, "Body", 28f, Color.white, H(0xE3EFFF), null, 4f, navyEdge, 8f, true);
            Stretch(body);

            // World header in the world's colour (tinted at runtime).
            Image header = RoundImg(card, "Header", 18f, LevelStartCard.WorldColors[0]);
            Box(header.rectTransform, 14f, 14f, 272f, 56f);
            TextMeshProUGUI headerText = V2Text(header.transform, "Name", "City", 30f, true, Color.white, LilitaOutline("Blue", H(0x103E86)));
            Place(headerText.rectTransform, 0.5f, 0.5f, 0f, 0f, 260f, 44f);

            // World intro variant: art strip + "Welcome to ... World" (the content below moves down when it shows).
            RectTransform intro = Box(Rect(card, "IntroStrip"), 14f, 80f, 272f, 108f);
            RectTransform strip = Box(Rect(intro, "Mask"), 0f, 0f, 272f, 74f);
            strip.gameObject.AddComponent<RectMask2D>();
            Image artFrame = Img(strip, "Frame", White, navyEdge);
            Stretch(artFrame.rectTransform);
            Image art = Img(strip, "Art", null, Color.white);
            art.preserveAspect = true;
            art.raycastTarget = false;
            RectTransform artRt = art.rectTransform;
            artRt.anchorMin = artRt.anchorMax = new Vector2(0.5f, 0.5f);
            artRt.sizeDelta = new Vector2(340f, 604f);
            artRt.anchoredPosition = new Vector2(0f, -70f);
            Stretch(artFrame.rectTransform, 0f, 0f, 0f, 0f);
            artFrame.transform.SetAsFirstSibling();
            TextMeshProUGUI introText = V2Text(intro, "Text", "Welcome to Snow World", 21f, true, navy);
            Box(introText.rectTransform, 0f, 76f, 272f, 32f);
            intro.gameObject.SetActive(false);

            RectTransform content = Stretch(Rect(card, "Content"));

            TextMeshProUGUI levelText = V2Text(content, "Level", "Level 1", 58f, true, navy);
            Box(levelText.rectTransform, 0f, 80f, 300f, 70f);

            RectTransform boss = V2Box(content, "Boss", 12f, H(0xFF8A8A), H(0xD12A33), null, 2f, H(0x7A1016), 3f);
            Box(boss, 104f, 148f, 92f, 26f);
            TextMeshProUGUI bossText = V2Text(boss, "Text", "BOSS", 15f, true, Color.white);
            Stretch(bossText.rectTransform);

            var stars = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                stars[i] = Img(content, "Star" + i, star, Color.white);
                stars[i].raycastTarget = false;
                Box(stars[i].rectTransform, 69f + i * 58f, 182f, 46f, 46f);
            }

            Image coinImg = Img(content, "Coin", coin, Color.white);
            coinImg.raycastTarget = false;
            Box(coinImg.rectTransform, 98f, 236f, 32f, 34f);
            TextMeshProUGUI rewardText = V2Text(content, "Reward", "+60", 28f, true, H(0x5A3410), null, TextAlignmentOptions.Left);
            Box(rewardText.rectTransform, 136f, 236f, 100f, 36f);

            // Big green Play button: the Home hero button (new button style, shine sweep, sparkles), scaled to fit the card.
            RectTransform playPulse = Box(Rect(content, "Play"), 5f, 276f, LevelW, LevelH);
            playPulse.localScale = Vector3.one * 0.92f;
            var built = BuildLevelButton(playPulse);
            CanvasGroup playGroup = playPulse.gameObject.AddComponent<CanvasGroup>();

            RectTransform close = V2Box(card, "Close", 22f, H(0xFF8A8A), H(0xD12A33), null, 3f, H(0x7A1016), 5f, true);
            Box(close, 264f, -16f, 46f, 50f);
            V2Text(close, "X", "X", 26f, true, Color.white);
            Button closeButton = V2Button(close);

            var view = layer.gameObject.AddComponent<LevelStartCard>();
            Material Mat(string n) => AssetDatabase.LoadAssetAtPath<Material>($"Assets/BusRush/Art/Fonts/LilitaOne UIv2 SDF {n}.mat");
            Wire(view, ("dim", dim), ("dimButton", dimButton), ("card", card), ("cardGroup", cardGroup), ("content", content),
                ("headerImage", header), ("headerText", headerText), ("headerMaterials", new object[] { Mat("Title Blue"), Mat("Title Blue"), Mat("Title Orange") }),
                ("levelText", levelText), ("bossTag", boss.gameObject), ("bossText", bossText), ("stars", stars), ("rewardText", rewardText),
                ("introStrip", intro.gameObject), ("introArt", art), ("introText", introText), ("playButton", built.button),
                ("playLabel", built.text), ("playGroup", playGroup), ("closeButton", closeButton));
            layer.gameObject.SetActive(false);
            return view;
        }
    }
}
