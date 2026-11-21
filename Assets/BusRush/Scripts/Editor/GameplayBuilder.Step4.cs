using System;
using System.IO;
using BusRush.Gameplay;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using static BusRush.EditorTools.UIKit;
using Object = UnityEngine.Object;

namespace BusRush.EditorTools
{
    /// <summary>Step 4 additions: exhaust smoke, light glows, vehicle motion, bay prefab and bay manager.</summary>
    public static partial class GameplayBuilder
    {
        private const string GameplayPrefabsDir = "Assets/BusRush/Prefabs/Gameplay";
        private const string BayPrefabPath = GameplayPrefabsDir + "/Bay.prefab";

        private static void BuildStep4()
        {
            Directory.CreateDirectory(GameplayPrefabsDir);
            AssetDatabase.Refresh();

            Texture2D soft = SaveTexture("soft_circle", SoftCircle(64), false);
            Material smoke = CreateParticleMaterial("Smoke", soft, new Color(1f, 1f, 1f, 1f), 0);
            Material head = CreateParticleMaterial("HeadGlow", soft, new Color(1f, 0.93f, 0.7f, 0.55f), 2);
            Material tail = CreateParticleMaterial("TailGlow", soft, new Color(1f, 0.15f, 0.1f, 1f), 2);

            var shadow = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/ContactShadow.mat");
            shadow.renderQueue = (int)RenderQueue.Transparent + 10; // above lot grid and bay decals
            EditorUtility.SetDirty(shadow);

            var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/BusRush/Resources/GameConfig.asset");
            cfg.extraBayCost = 200; // design: locked bays show a 200-coin tag
            EditorUtility.SetDirty(cfg);

            UpgradeVehiclesStep4(smoke, head, tail);
            BuildBayPrefab(cfg.extraBayCost);
            AssetDatabase.SaveAssets();
        }

        private static Texture2D SoftCircle(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float r = new Vector2((x + 0.5f) / size - 0.5f, (y + 0.5f) / size - 0.5f).magnitude * 2f;
                    float a = Mathf.Clamp01(1f - r);
                    px[y * size + x] = new Color(1f, 1f, 1f, a * a * (3f - 2f * a));
                }
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        /// <summary>URP Particles/Unlit, transparent. blend: 0 alpha, 2 additive.</summary>
        private static Material CreateParticleMaterial(string name, Texture2D texture, Color color, int blend)
        {
            Material m = LoadOrCreate(name, "Universal Render Pipeline/Particles/Unlit");
            m.SetTexture("_BaseMap", texture);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", blend);
            UnityEditor.BaseShaderGUI.SetMaterialKeywords(m);
            m.enableInstancing = false;
            m.renderQueue = (int)RenderQueue.Transparent + 20;
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ vehicles

        private static void UpgradeVehiclesStep4(Material smoke, Material head, Material tail)
        {
            Mesh quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            foreach (VehicleType type in (VehicleType[])Enum.GetValues(typeof(VehicleType)))
            {
                string path = $"{VehiclesDir}/{type}.prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Transform model = root.transform.Find("Model");
                    Transform exhaustPoint = root.transform.Find("ExhaustPoint") ?? throw new InvalidOperationException($"{type}: no ExhaustPoint");
                    float halfLen = VehicleSpecs.Length(type) * BoardGeometry.CellSize * ScreenRef.VehicleVisualScale * 0.5f;
                    bool bus = type == VehicleType.Bus;

                    foreach (string n in new[] { "Exhaust" })
                    {
                        Transform old = exhaustPoint.Find(n);
                        if (old != null) Object.DestroyImmediate(old.gameObject);
                    }
                    foreach (string n in new[] { "HeadGlow", "TailGlow" })
                    {
                        Transform old = root.transform.Find(n);
                        if (old != null) Object.DestroyImmediate(old.gameObject);
                    }

                    // Exhaust smoke
                    var ex = new GameObject("Exhaust");
                    ex.transform.SetParent(exhaustPoint, false);
                    ConfigureSmoke(ex.AddComponent<ParticleSystem>(), smoke, bus ? 1.4f : type == VehicleType.Van ? 1.2f : 1f);
                    VehicleExhaust exhaust = ex.AddComponent<VehicleExhaust>();

                    // Headlight pool in front, brake glow behind (flat on the ground, additive).
                    MeshRenderer headGlow = GlowQuad(root.transform, "HeadGlow", quad, head,
                        new Vector3(0f, 0.03f, halfLen + 0.45f), new Vector2(1.05f, 0.9f));
                    MeshRenderer tailGlow = GlowQuad(root.transform, "TailGlow", quad, tail,
                        new Vector3(0f, 0.03f, -halfLen - 0.12f), new Vector2(0.95f, 0.45f));

                    if (!root.TryGetComponent(out VehicleMotion motion)) motion = root.AddComponent<VehicleMotion>();
                    Wire(motion, ("model", model), ("exhaust", exhaust), ("headGlow", headGlow), ("tailGlow", tailGlow));

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static MeshRenderer GlowQuad(Transform parent, string name, Mesh quad, Material material, Vector3 pos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(pos, Quaternion.Euler(90f, 0f, 0f));
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.enabled = false;
            return r;
        }

        private static void ConfigureSmoke(ParticleSystem ps, Material material, float scale)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.30f * scale, 0.55f * scale);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            // Warm diesel grey (#6E6A66 .. #8C8782): reads on the light lot and on the dark road.
            main.startColor = new ParticleSystem.MinMaxGradient(UIArtGenerator.Hex(0x6E6A66), UIArtGenerator.Hex(0x8C8782));
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 20;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false; // VehicleExhaust emits by speed

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 14f;
            shape.radius = 0.03f;

            ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            // All three axes must use the same curve mode (two constants), or every Emit logs an error.
            vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.y = new ParticleSystem.MinMaxCurve(0.25f, 0.45f); // rises slowly
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 2.2f))); // grows 2.2x

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0.4f, 0.55f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.12f;
            noise.frequency = 1.2f;
            noise.scrollSpeed = 0.3f;
            noise.quality = ParticleSystemNoiseQuality.Low;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enableGPUInstancing = false;
        }

        // ------------------------------------------------------------------ bay prefab

        private static void BuildBayPrefab(int unlockCost)
        {
            Load(); // UIKit sprites + fonts
            var root = new GameObject("Bay");
            try
            {
                BayView view = root.AddComponent<BayView>();
                BoxCollider box = root.AddComponent<BoxCollider>();
                box.size = new Vector3(1.3f, 0.4f, 2.4f);
                box.center = new Vector3(0f, 0.2f, 0f);

                // Flat decal on the ground. Canvas units = reference px across; along the ground the art is
                // foreshortened by sin(pitch), so a slot 62 px tall on screen is 62 / sin canvas units long.
                const float slotW = ScreenRef.BaySpacing, slotScreenH = 62f;
                float slotH = slotScreenH / BoardGeometry.SinPitch;
                RectTransform decal = WorldCanvas(root.transform, "Decal", new Vector2(slotW + 8f, slotH + 8f), 20);
                decal.localPosition = new Vector3(0f, 0.008f, 0f);
                decal.localRotation = Quaternion.Euler(90f, 0f, 0f);

                RectTransform openGroup = Stretch(Rect(decal, "Open"));
                // (City v2: slot lines and pads are painted in the world art.)

                RectTransform vipGroup = Stretch(Rect(decal, "Vip"));
                Image crown = IconImg(vipGroup, "Crown", "crown", 26f);
                Place(crown.rectTransform, 0.5f, 0.5f, 0f, -8f, 26f, 26f);
                TextMeshProUGUI vip = Label(vipGroup, "Label", "VIP", 14f, TextStyle.Heading, Gold);
                Place(vip.rectTransform, 0.5f, 0.5f, 0f, -30f, 40f, 18f);

                RectTransform lockedGroup = Stretch(Rect(decal, "Locked"));
                Image lockIcon = IconImg(lockedGroup, "Lock", "lock", 22f);
                Place(lockIcon.rectTransform, 0.5f, 0.5f, 0f, 16f, 22f, 22f);
                Image plus = Badge(lockedGroup, "Plus", Green, 20f, out RectTransform plusContent);
                Place(plus.rectTransform, 0.5f, 0.5f, 0f, -8f, 20f, 20f);
                Image plusIcon = IconImg(plusContent, "Icon", "plus", 13f);
                Place(plusIcon.rectTransform, 0.5f, 0.5f, 0f, 0f, 13f, 13f);
                Image coin = IconImg(lockedGroup, "Coin", "coin", 13f);
                Place(coin.rectTransform, 0.5f, 0.5f, -11f, -30f, 13f, 13f);
                TextMeshProUGUI price = Label(lockedGroup, "Price", unlockCost.ToString(), 13f, TextStyle.Heading, null, TextAlignmentOptions.Left);
                Place(price.rectTransform, 0.5f, 0.5f, 8f, -30f, 28f, 16f);

                // Capacity pill, facing the camera, above the parked vehicle.
                RectTransform pillCanvas = WorldCanvas(root.transform, "Pill", new Vector2(44f, 24f), 40);
                pillCanvas.localPosition = new Vector3(0f, 1.55f, 0.15f);
                pillCanvas.localRotation = Quaternion.Euler(BoardGeometry.CameraPitch, 0f, 0f);
                Image outline = Img(pillCanvas, "Outline", Circle, DarkNavy);
                MakePill(outline, 21f);
                Place(outline.rectTransform, 0.5f, 0.5f, 0f, 0f, 42f, 21f);
                Image fill = Img(outline.transform, "Fill", Circle, Color.white);
                MakePill(fill, 17f);
                Stretch(fill.rectTransform, 2f, 2f, 2f, 2f);
                TextMeshProUGUI pillText = Label(fill.transform, "Text", "0/4", 14f, TextStyle.BodyPlain, DarkNavy);
                Stretch(pillText.rectTransform, 2f, 0f, 2f, 0f);

                Wire(view, ("openGroup", openGroup.gameObject), ("vipGroup", vipGroup.gameObject), ("lockedGroup", lockedGroup.gameObject),
                    ("lockedPriceText", price), ("pill", pillCanvas.gameObject), ("pillText", pillText), ("tapCollider", box));

                PrefabUtility.SaveAsPrefabAsset(root, BayPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static RectTransform WorldCanvas(Transform parent, string name, Vector2 size, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 4f;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one * ScreenRef.UnitsPerPx;
            return rt;
        }

        // ------------------------------------------------------------------ scene

        private static BayManager BuildBayManager(GameObject board)
        {
            BayManager manager = board.AddComponent<BayManager>();
            Wire(manager,
                ("bayPrefab", AssetDatabase.LoadAssetAtPath<BayView>(BayPrefabPath)),
                ("floatFont", AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/BusRush/Art/Fonts/LilitaOne-Regular SDF.asset")),
                ("floatMaterial", AssetDatabase.LoadAssetAtPath<Material>("Assets/BusRush/Art/Fonts/LilitaOne-Regular SDF Outline.mat")));
            return manager;
        }
    }
}
