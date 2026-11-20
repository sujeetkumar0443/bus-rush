using System;
using System.IO;
using BusRush.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Steps 3-4: gameplay board, road, bays and smoke (see GameplayBuilder.Step4.cs). Creates textures/materials/world themes, upgrades the vehicle prefabs
    /// (VehicleView, enlarged tap box, contact shadow) and wires the Game scene (world background, lot grid, board,
    /// session, input). Idempotent. Order for a full rebuild: Step 1 setup -> Build UI -> Build Gameplay
    /// (Tools/Bus Rush/Rebuild Everything does exactly that).
    /// </summary>
    public static partial class GameplayBuilder
    {
        private const string TexturesDir = "Assets/BusRush/Art/Textures";
        private const string MaterialsDir = "Assets/BusRush/Art/Materials";
        private const string WorldsDir = "Assets/BusRush/Art/Worlds";
        private const string ThemesDir = WorldsDir + "/Themes";
        private const string VehiclesDir = "Assets/BusRush/Prefabs/Vehicles";
        private const string GameScene = "Assets/BusRush/Scenes/Game.unity";

        /// <summary>Minimum tap target: 48 dp on the 390 dp reference width = 48 / 34 cells across.</summary>
        private const float MinTapWidth = 48f / BoardGeometry.PixelsPerCell;

        [MenuItem("Tools/Bus Rush/Build Gameplay (Steps 3-5)")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(TexturesDir);
            Directory.CreateDirectory(ThemesDir);
            AssetDatabase.Refresh();

            ConfigureWorldTextures();
            WorldTheme[] themes = CreateThemes();
            Material background = CreateBackgroundMaterial(themes[0]);
            Material grid = CreateTransparentUnlit("LotGrid", SaveTexture("lot_cell", LotCellTexture(128), true), new Color(1f, 1f, 1f, 0.6f));
            Material shadow = CreateTransparentUnlit("ContactShadow", SaveTexture("contact_shadow", ShadowTexture(128), false), new Color(0.08f, 0.1f, 0.18f, 0.42f));
            EnablePaletteEmission();
            UpgradeVehiclePrefabs(shadow);
            BuildStep4();
            BuildStep5();
            SetShadowDistance(60f);
            AssetDatabase.SaveAssets();

            WireGameScene();
            Debug.Log("[GameplayBuilder] Gameplay built.");
        }

        [MenuItem("Tools/Bus Rush/Rebuild Everything (Steps 1-3)")]
        public static void RebuildEverything()
        {
            BusRushSetup.RunAll();
            UIBuilder.BuildAll();
            BuildAll();
        }

        /// <summary>Batch: rebuilds the UI (HUD changes) and the gameplay.</summary>
        public static void BuildAllBatch()
        {
            try
            {
                UIBuilder.BuildAll();
                BuildAll();
            }
            catch (Exception e)
            {
                Debug.LogError("[GameplayBuilder] FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Tools/Bus Rush/Validate 200 Levels")]
        public static void ValidateLevels()
        {
            int failed = 0;
            for (int level = 1; level <= 200; level++)
            {
                LevelData data = LevelGenerator.Generate(level);
                if (!LevelSolver.IsSolvable(data, out _))
                {
                    failed++;
                    Debug.LogError($"[GameplayBuilder] Level {level} is NOT solvable");
                }
            }
            Debug.Log($"[GameplayBuilder] Level validation: {200 - failed}/200 solvable.");
        }

        // ------------------------------------------------------------------ textures & materials

        private static void ConfigureWorldTextures()
        {
            foreach (string name in new[] { "world_city", "world_snow", "world_beach" })
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath($"{WorldsDir}/{name}.jpg");
                if (ti.wrapModeU == TextureWrapMode.MirrorOnce && ti.wrapModeV == TextureWrapMode.MirrorOnce) continue;
                ti.wrapModeU = TextureWrapMode.MirrorOnce;
                ti.wrapModeV = TextureWrapMode.MirrorOnce;
                ti.SaveAndReimport();
            }
        }

        private static WorldTheme[] CreateThemes()
        {
            (string name, string file)[] defs = { ("City", "world_city"), ("Snow", "world_snow"), ("Beach", "world_beach") };
            var themes = new WorldTheme[defs.Length];
            for (int i = 0; i < defs.Length; i++)
            {
                string path = $"{ThemesDir}/{defs[i].name}.asset";
                var theme = AssetDatabase.LoadAssetAtPath<WorldTheme>(path);
                if (theme == null)
                {
                    theme = ScriptableObject.CreateInstance<WorldTheme>();
                    AssetDatabase.CreateAsset(theme, path);
                }
                theme.themeName = defs[i].name;
                theme.background = AssetDatabase.LoadAssetAtPath<Sprite>($"{WorldsDir}/{defs[i].file}.jpg");
                if (theme.background == null) throw new InvalidOperationException("World sprite missing: " + defs[i].file);
                EditorUtility.SetDirty(theme);
                themes[i] = theme;
            }
            return themes;
        }

        private static Material CreateBackgroundMaterial(WorldTheme first)
        {
            Material m = LoadOrCreate("World_Background", "Universal Render Pipeline/Unlit");
            m.SetTexture("_BaseMap", first.background.texture);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 0f);
            UnityEditor.BaseShaderGUI.SetMaterialKeywords(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material CreateTransparentUnlit(string name, Texture2D texture, Color color)
        {
            Material m = LoadOrCreate(name, "Universal Render Pipeline/Unlit");
            m.SetTexture("_BaseMap", texture);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Surface", 1f); // transparent
            m.SetFloat("_Blend", 0f);   // alpha
            UnityEditor.BaseShaderGUI.SetMaterialKeywords(m);
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material LoadOrCreate(string name, string shaderName)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            Shader shader = Shader.Find(shaderName);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            return m;
        }

        /// <summary>Palette materials keep _EMISSION on with black emission so vehicles can flash via MaterialPropertyBlock.</summary>
        private static void EnablePaletteEmission()
        {
            foreach (string name in Enum.GetNames(typeof(GameColor)))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/{name}.mat");
                if (m == null) continue;
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                EditorUtility.SetDirty(m);
            }
        }

        private static Texture2D SaveTexture(string name, Texture2D tex, bool mipmaps)
        {
            string path = $"{TexturesDir}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Default;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = mipmaps;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static float RoundedBox(Vector2 p, Vector2 half, float r)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - half + new Vector2(r, r);
            return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        /// <summary>One lot cell: faint white fill with a rounded white outline.</summary>
        private static Texture2D LotCellTexture(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            float half = size * 0.5f - 4f;
            const float radius = 22f, line = 5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f);
                    float d = RoundedBox(p, new Vector2(half, half), radius);
                    float inside = Mathf.Clamp01(0.5f - d);
                    float outline = Mathf.Clamp01(0.5f - (Mathf.Abs(d + line * 0.5f) - line * 0.5f));
                    float a = Mathf.Max(outline, inside * 0.18f);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        /// <summary>Soft rounded blob used as a contact shadow.</summary>
        private static Texture2D ShadowTexture(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            const float blur = 26f;
            float half = size * 0.5f - blur;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f);
                    float d = RoundedBox(p, new Vector2(half, half), 18f);
                    float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-blur * 0.6f, blur, d));
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        private static void SetShadowDistance(float distance)
        {
            foreach (string path in new[] { "Assets/Settings/Mobile_RPAsset.asset", "Assets/Settings/PC_RPAsset.asset", "Assets/Settings/Android_RPAsset.asset" })
            {
                Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null) continue;
                var so = new SerializedObject(asset);
                so.FindProperty("m_ShadowDistance").floatValue = distance;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // ------------------------------------------------------------------ vehicle prefabs

        private static void UpgradeVehiclePrefabs(Material shadowMaterial)
        {
            Mesh quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            foreach (VehicleType type in (VehicleType[])Enum.GetValues(typeof(VehicleType)))
            {
                string path = $"{VehiclesDir}/{type}.prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Transform model = root.transform.Find("Model") ?? throw new InvalidOperationException($"{type}: no Model child");
                    Transform arrow = model.Find("Arrow") ?? throw new InvalidOperationException($"{type}: no Model/Arrow");

                    int length = VehicleSpecs.Length(type);
                    float cell = BoardGeometry.CellSize;

                    // Contact shadow blob under the footprint.
                    Transform old = root.transform.Find("ContactShadow");
                    if (old != null) Object.DestroyImmediate(old.gameObject);
                    var shadow = new GameObject("ContactShadow");
                    shadow.transform.SetParent(root.transform, false);
                    shadow.transform.SetLocalPositionAndRotation(new Vector3(0f, 0.012f, 0f), Quaternion.Euler(90f, 0f, 0f));
                    shadow.transform.localScale = new Vector3(cell * 0.92f + 0.4f, length * cell * 0.92f + 0.4f, 1f);
                    shadow.AddComponent<MeshFilter>().sharedMesh = quad;
                    var mr = shadow.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = shadowMaterial;
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    mr.receiveShadows = false;

                    // Tap box: a bit larger than the model, at least 48 dp across on screen.
                    if (!root.TryGetComponent(out BoxCollider box)) box = root.AddComponent<BoxCollider>();
                    float width = Mathf.Max(cell * 0.94f + 0.15f, MinTapWidth);
                    float depth = Mathf.Max(length * cell + 0.1f, MinTapWidth / BoardGeometry.SinPitch);
                    box.size = new Vector3(width, 1.4f, depth);
                    box.center = new Vector3(0f, 0.6f, 0f);

                    if (!root.TryGetComponent(out VehicleView view)) view = root.AddComponent<VehicleView>();
                    UIKit.Wire(view, ("model", model), ("arrow", arrow), ("contactShadow", shadow.transform), ("hitBox", box));

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        // ------------------------------------------------------------------ scene

        private static void WireGameScene()
        {
            Scene scene = EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);

            // Opening a scene unloads unreferenced assets, so load everything from disk only now.
            var themes = new[]
            {
                AssetDatabase.LoadAssetAtPath<WorldTheme>($"{ThemesDir}/City.asset"),
                AssetDatabase.LoadAssetAtPath<WorldTheme>($"{ThemesDir}/Snow.asset"),
                AssetDatabase.LoadAssetAtPath<WorldTheme>($"{ThemesDir}/Beach.asset")
            };
            var background = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/World_Background.mat");
            var grid = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/LotGrid.mat");
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.name is "Lot" or "Background (placeholder)" or "World" or "Board") Object.DestroyImmediate(go);
            }

            GameObject camGo = Array.Find(scene.GetRootGameObjects(), g => g.name == "Main Camera")
                               ?? throw new InvalidOperationException("Main Camera missing in Game scene");
            Camera cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.84f, 0.86f, 0.9f);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 120f;
            if (!camGo.TryGetComponent(out CameraFitter fitter)) fitter = camGo.AddComponent<CameraFitter>();
            fitter.Fit(BoardGeometry.ReferenceWidth / BoardGeometry.ReferenceHeight);

            // World background
            var world = new GameObject("World");
            world.AddComponent<MeshFilter>();
            var bgRenderer = world.AddComponent<MeshRenderer>();
            bgRenderer.sharedMaterial = background;
            bgRenderer.shadowCastingMode = ShadowCastingMode.Off;
            bgRenderer.receiveShadows = false;
            WorldBackground bg = world.AddComponent<WorldBackground>();
            UIKit.Wire(bg, ("fitter", fitter));
            WorldManager manager = world.AddComponent<WorldManager>();
            UIKit.Wire(manager, ("themes", themes), ("background", bg));

            // Board + grid overlay + session + input
            var board = new GameObject("Board");
            BoardView boardView = board.AddComponent<BoardView>();
            UIKit.Wire(boardView,
                ("carPrefab", AssetDatabase.LoadAssetAtPath<VehicleView>($"{VehiclesDir}/Car.prefab")),
                ("vanPrefab", AssetDatabase.LoadAssetAtPath<VehicleView>($"{VehiclesDir}/Van.prefab")),
                ("busPrefab", AssetDatabase.LoadAssetAtPath<VehicleView>($"{VehiclesDir}/Bus.prefab")));

            var gridGo = new GameObject("LotGrid");
            gridGo.transform.SetParent(board.transform, false);
            gridGo.AddComponent<MeshFilter>();
            var gridRenderer = gridGo.AddComponent<MeshRenderer>();
            gridRenderer.sharedMaterial = grid;
            gridRenderer.shadowCastingMode = ShadowCastingMode.Off;
            gridRenderer.receiveShadows = false;
            gridGo.AddComponent<LotGridView>();

            GameSession session = board.AddComponent<GameSession>();
            (CrowdView crowd, GameFx fx) = BuildCrowdAndFx(board);
            UIKit.Wire(session, ("board", boardView), ("world", manager), ("bays", BuildBayManager(board)),
                ("crowd", crowd), ("fx", fx), ("debugAutoDepart", false));
            InputController input = board.AddComponent<InputController>();
            UIKit.Wire(input, ("viewCamera", cam), ("session", session));

            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.OpenScene("Assets/BusRush/Scenes/Boot.unity", OpenSceneMode.Single);
        }
    }
}
