using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BusRush.Gameplay;
using BusRush.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Step 1 project set-up. Idempotent: re-running it rebuilds materials, prefabs, config, font and scenes.
    /// Menu: Tools/Bus Rush/... or batch mode: -executeMethod BusRush.EditorTools.BusRushSetup.RunAllBatch
    /// </summary>
    public static class BusRushSetup
    {
        private const string Root = "Assets/BusRush";
        private const string ModelsDir = Root + "/Art/Models";
        private const string MaterialsDir = Root + "/Art/Materials";
        private const string WorldsDir = Root + "/Art/Worlds";
        private const string FontsDir = Root + "/Art/Fonts";
        private const string VehiclePrefabsDir = Root + "/Prefabs/Vehicles";
        private const string CharacterPrefabsDir = Root + "/Prefabs/Characters";
        private const string ScenesDir = Root + "/Scenes";
        private const string ConfigPath = Root + "/Resources/GameConfig.asset";
        private const string FontPath = FontsDir + "/Fredoka-SemiBold.ttf";
        private const string FontAssetPath = FontsDir + "/Fredoka-SemiBold SDF.asset";
        private const string TmpEssentialsPackage = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        private static readonly string[] Folders =
        {
            "Art/Models", "Art/Materials", "Art/Textures", "Art/Worlds", "Art/Sprites", "Art/Fonts",
            "Audio/Music", "Audio/SFX",
            "Prefabs/Vehicles", "Prefabs/Characters", "Prefabs/UI",
            "Scenes",
            "Scripts/Core", "Scripts/Gameplay", "Scripts/UI", "Scripts/Data", "Scripts/Utils", "Scripts/Editor",
            "Resources/Levels"
        };

        // Vehicle footprint targets in cells (width x length). Forward is +Z.
        private static readonly (string name, float width, float length)[] VehicleTargets =
        {
            ("Car", 1f, 2f),
            ("Van", 1f, 2f),
            ("Bus", 1f, 3f)
        };

        /// <summary>Max rendered width so neighbouring vehicles in adjacent columns do not touch.</summary>
        private const float MaxVehicleWidth = 0.94f;

        /// <summary>Imported models face -Z (despite the README); this turns them to the game's +Z forward.</summary>
        private static readonly Quaternion ModelToForward = Quaternion.Euler(0f, 180f, 0f);

        // Reference resolution (portrait).
        private const float RefWidth = 390f;
        private const float RefHeight = 844f;

        private static readonly StringBuilder Report = new StringBuilder();

        // ------------------------------------------------------------------ entry points

        [MenuItem("Tools/Bus Rush/Import TMP Essentials")]
        public static void ImportTmpEssentials()
        {
            if (File.Exists(TmpSettingsPath))
            {
                Debug.Log("[BusRushSetup] TMP Essentials already imported.");
                return;
            }
            // Package import is asynchronous; it finishes on a later editor update.
            UnityEditor.AssetPackage.Package.Import(Path.GetFullPath(TmpEssentialsPackage), false);
        }

        /// <summary>Batch mode: run WITHOUT -quit; exits by itself once the import has finished.</summary>
        public static void ImportTmpEssentialsBatch()
        {
            ImportTmpEssentials();
            double deadline = EditorApplication.timeSinceStartup + 300.0;

            void Poll()
            {
                bool done = File.Exists(TmpSettingsPath) && !EditorApplication.isUpdating && !EditorApplication.isCompiling;
                if (!done && EditorApplication.timeSinceStartup < deadline)
                {
                    return;
                }
                EditorApplication.update -= Poll;
                AssetDatabase.Refresh();
                Debug.Log("[BusRushSetup] TMP Essentials imported: " + done);
                EditorApplication.Exit(done ? 0 : 1);
            }

            EditorApplication.update += Poll;
        }

        [MenuItem("Tools/Bus Rush/Run Step 1 Setup")]
        public static void RunAll()
        {
            Report.Clear();
            CreateFolders();
            ConfigurePlayerSettings();
            ConfigureQualityAndUrp();
            ConfigureWorldTextures();
            Dictionary<string, Material> mats = CreateMaterials();
            ConfigureModels(mats, out Dictionary<string, Bounds> scaledBounds);
            CreateVehiclePrefabs(scaledBounds);
            CreatePassengerPrefab(mats);
            CreateGameConfig(mats);
            TMP_FontAsset font = CreateFontAsset();
            CreateScenes(font);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[BusRushSetup] REPORT\n" + Report);
            Debug.Log("[BusRushSetup] Done.");
        }

        public static void RunAllBatch() => RunBatch(RunAll);

        private static void RunBatch(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Debug.LogError("[BusRushSetup] FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }

        // ------------------------------------------------------------------ folders & settings

        private static void CreateFolders()
        {
            foreach (string f in Folders)
            {
                string path = Root;
                foreach (string part in f.Split('/'))
                {
                    string next = path + "/" + part;
                    if (!AssetDatabase.IsValidFolder(next))
                    {
                        AssetDatabase.CreateFolder(path, part);
                    }
                    path = next;
                }
            }
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.productName = "Bus Rush";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "busrush.parkingjam.cartraffic");

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // API 24 was requested, but Unity 6000.6 rejects anything below API 26 (Android 8.0).
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;

            PlayerSettings.colorSpace = ColorSpace.Linear;

            Report.AppendLine($"Player: product='{PlayerSettings.productName}', androidId='{PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android)}', " +
                              $"orientation={PlayerSettings.defaultInterfaceOrientation}, backend={PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android)}, " +
                              $"arch={PlayerSettings.Android.targetArchitectures}, minSdk={PlayerSettings.Android.minSdkVersion}, colorSpace={PlayerSettings.colorSpace}");
        }

        private static void ConfigureQualityAndUrp()
        {
            const string mobileRp = "Assets/Settings/Mobile_RPAsset.asset";
            const string pcRp = "Assets/Settings/PC_RPAsset.asset";
            const string androidRp = "Assets/Settings/Android_RPAsset.asset";

            if (!File.Exists(androidRp))
            {
                AssetDatabase.CopyAsset(mobileRp, androidRp);
            }

            // URP assets: MSAA 4x everywhere; Android = soft shadows (low quality), 1 cascade.
            SetUrp(mobileRp, msaa: 4);
            SetUrp(pcRp, msaa: 4);
            SetUrp(androidRp, msaa: 4, softShadows: true, softQuality: 1 /* Low */, cascades: 1);

            Object androidAsset = AssetDatabase.LoadMainAssetAtPath(androidRp);

            Object qualityObj = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0];
            var so = new SerializedObject(qualityObj);
            SerializedProperty levels = so.FindProperty("m_QualitySettings");

            int mobileIndex = -1;
            int androidIndex = -1;
            for (int i = 0; i < levels.arraySize; i++)
            {
                string n = levels.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue;
                if (n == "Mobile") mobileIndex = i;
                if (n == "Android") androidIndex = i;
            }

            if (androidIndex < 0)
            {
                if (mobileIndex < 0)
                {
                    throw new InvalidOperationException("Quality level 'Mobile' not found.");
                }
                // Duplicate Mobile, then move the copy to the end so existing indices stay valid.
                levels.InsertArrayElementAtIndex(mobileIndex);
                levels.MoveArrayElement(mobileIndex + 1, levels.arraySize - 1);
                androidIndex = levels.arraySize - 1;
            }

            SerializedProperty android = levels.GetArrayElementAtIndex(androidIndex);
            android.FindPropertyRelative("name").stringValue = "Android";
            android.FindPropertyRelative("shadows").intValue = 2;          // Hard and Soft
            android.FindPropertyRelative("shadowResolution").intValue = 0; // Low
            android.FindPropertyRelative("shadowCascades").intValue = 1;
            android.FindPropertyRelative("antiAliasing").intValue = 4;
            android.FindPropertyRelative("customRenderPipeline").objectReferenceValue = androidAsset;
            SetExcluded(android.FindPropertyRelative("excludedTargetPlatforms"), "Standalone", "iPhone");

            for (int i = 0; i < levels.arraySize; i++)
            {
                SerializedProperty level = levels.GetArrayElementAtIndex(i);
                level.FindPropertyRelative("vSyncCount").intValue = 0;
                if (i == mobileIndex)
                {
                    AddExcluded(level.FindPropertyRelative("excludedTargetPlatforms"), "Android");
                }
            }

            SerializedProperty perPlatform = so.FindProperty("m_PerPlatformDefaultQuality");
            for (int i = 0; i < perPlatform.arraySize; i++)
            {
                SerializedProperty pair = perPlatform.GetArrayElementAtIndex(i);
                if (pair.FindPropertyRelative("first").stringValue == "Android")
                {
                    pair.FindPropertyRelative("second").intValue = androidIndex;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            QualitySettings.vSyncCount = 0;

            Report.AppendLine($"Quality: levels=[{string.Join(", ", QualitySettings.names)}], Android level index={androidIndex}, vsync off on all levels, MSAA 4x on Mobile/PC/Android URP assets.");
        }

        private static void SetUrp(string path, int msaa, bool? softShadows = null, int? softQuality = null, int? cascades = null)
        {
            var so = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(path));
            so.FindProperty("m_MSAA").intValue = msaa;
            so.FindProperty("m_MainLightShadowsSupported").boolValue = true;
            if (softShadows.HasValue) so.FindProperty("m_SoftShadowsSupported").boolValue = softShadows.Value;
            if (softQuality.HasValue) so.FindProperty("m_SoftShadowQuality").intValue = softQuality.Value;
            if (cascades.HasValue) so.FindProperty("m_ShadowCascadeCount").intValue = cascades.Value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetUrpShadowDistance(float distance)
        {
            foreach (string path in new[] { "Assets/Settings/Mobile_RPAsset.asset", "Assets/Settings/PC_RPAsset.asset", "Assets/Settings/Android_RPAsset.asset" })
            {
                var so = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(path));
                so.FindProperty("m_ShadowDistance").floatValue = distance;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetExcluded(SerializedProperty list, params string[] platforms)
        {
            list.ClearArray();
            foreach (string p in platforms)
            {
                AddExcluded(list, p);
            }
        }

        private static void AddExcluded(SerializedProperty list, string platform)
        {
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).stringValue == platform) return;
            }
            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).stringValue = platform;
        }

        // ------------------------------------------------------------------ textures

        private static void ConfigureWorldTextures()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { WorldsDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.mipmapEnabled = false;
                ti.maxTextureSize = 2048;
                ti.textureCompression = TextureImporterCompression.Compressed;
                // Mirror once on both axes: the background extends the art beyond its edges on taller / wider screens.
                ti.wrapModeU = TextureWrapMode.MirrorOnce;
                ti.wrapModeV = TextureWrapMode.MirrorOnce;
                ti.SaveAndReimport();
            }
        }

        // ------------------------------------------------------------------ materials

        private static Dictionary<string, Material> CreateMaterials()
        {
            var mats = new Dictionary<string, Material>();
            string[] names = Enum.GetNames(typeof(GameColor));
            uint[] hex = { 0xE8433F, 0x3B7BE8, 0xF5C518, 0x3DBE5A, 0x8E5AD8, 0xF26BAE, 0x2FC4D8 };
            for (int i = 0; i < names.Length; i++)
            {
                // Emission enabled at black so gameplay can flash vehicles via MaterialPropertyBlock.
                mats[names[i]] = Lit(names[i], GameColors.Hex(hex[i]), 0.45f, emission: Color.black);
            }

            mats["Glass"] = Lit("Glass", GameColors.Hex(0x2B3646), 0.85f);
            mats["Tyre"] = Lit("Tyre", GameColors.Hex(0x1C1D22), 0.15f);
            mats["Chrome"] = Lit("Chrome", GameColors.Hex(0xCFD3DA), 0.75f, metallic: 0.35f);
            mats["Light"] = Lit("Light", GameColors.Hex(0xFFF2D6), 0.5f, emission: GameColors.Hex(0xFFE9B8) * 1.4f);
            // Extra slots present on the models that the brief's list does not cover:
            mats["TailLight"] = Lit("TailLight", GameColors.Hex(0xFF3B30), 0.5f, emission: GameColors.Hex(0xFF3B30) * 0.8f);
            mats["Arrow"] = Lit("Arrow", Color.white, 0.3f);

            AssetDatabase.SaveAssets();
            return mats;
        }

        private static Material Lit(string name, Color color, float smoothness, float metallic = 0f, Color? emission = null)
        {
            Material m = LoadOrCreateMaterial(name, "Universal Render Pipeline/Lit");
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                m.DisableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material LoadOrCreateMaterial(string name, string shaderName)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                throw new InvalidOperationException("Shader not found: " + shaderName);
            }
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader)
            {
                m.shader = shader;
            }
            return m;
        }

        // ------------------------------------------------------------------ models

        private static string MapSlot(string sourceName)
        {
            string n = sourceName.Split('.')[0];
            return n switch
            {
                "Body" => "Red",
                "Cap" => "Red",
                "Glass" => "Glass",
                "Tire" => "Tyre",
                "Tyre" => "Tyre",
                "Trim" => "Tyre",
                "ArrowOutline" => "Tyre",
                "Hub" => "Chrome",
                "HeadLight" => "Light",
                "TailLight" => "TailLight",
                "Arrow" => "Arrow",
                _ => null
            };
        }

        private static void ConfigureModels(Dictionary<string, Material> mats, out Dictionary<string, Bounds> scaledBounds)
        {
            scaledBounds = new Dictionary<string, Bounds>();
            Report.AppendLine("Models (bounds measured at Scale Factor 1, Bake Axis Conversion on; X = width, Y = height, Z = length):");

            foreach (string name in new[] { "Car", "Van", "Bus", "Passenger" })
            {
                string path = $"{ModelsDir}/{name}.fbx";
                var mi = (ModelImporter)AssetImporter.GetAtPath(path);
                if (mi == null)
                {
                    throw new FileNotFoundException("Model not imported: " + path);
                }

                // Pass 1: neutral import, measure.
                mi.globalScale = 1f;
                mi.useFileScale = true;
                mi.bakeAxisConversion = true;
                mi.importAnimation = false;
                mi.animationType = ModelImporterAnimationType.None;
                mi.importCameras = false;
                mi.importLights = false;
                mi.importBlendShapes = false;
                mi.importVisibility = false;
                mi.isReadable = false;
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                foreach (KeyValuePair<AssetImporter.SourceAssetIdentifier, Object> kv in mi.GetExternalObjectMap())
                {
                    mi.RemoveRemap(kv.Key);
                }
                mi.SaveAndReimport();

                Bounds raw = MeasureModel(path);
                Report.AppendLine($"  {name}: raw size {Fmt(raw.size)}, centre {Fmt(raw.center)}");

                // Source material slot names = embedded materials of the neutral import.
                string[] slotNames = AssetDatabase.LoadAllAssetsAtPath(path)
                    .OfType<Material>()
                    .Select(m => m.name)
                    .Distinct()
                    .ToArray();

                if (name == "Passenger")
                {
                    // Split Body / Cap submeshes into separate meshes while source material names are still visible.
                    SplitPassengerMeshes(path);
                }

                // Pass 2: scale + material remaps.
                float scale = 1f;
                foreach ((string vName, float _, float length) in VehicleTargets)
                {
                    if (vName == name)
                    {
                        scale = (float)Math.Round(length / raw.size.z, 4);
                    }
                }
                mi.globalScale = scale;

                var mapped = new List<string>();
                foreach (string slot in slotNames)
                {
                    string target = MapSlot(slot);
                    if (target != null && mats.TryGetValue(target, out Material mat))
                    {
                        mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), slot), mat);
                        mapped.Add($"{slot}->{target}");
                    }
                    else
                    {
                        Debug.LogWarning($"[BusRushSetup] {name}: no material mapping for slot '{slot}'.");
                    }
                }
                mi.SaveAndReimport();
                Report.AppendLine($"  {name}: material remaps {string.Join(", ", mapped)}");

                Bounds b = MeasureModel(path);
                scaledBounds[name] = b;
                Report.AppendLine($"  {name}: Scale Factor {scale} -> {Fmt(b.size)}");
            }
        }

        private static Bounds MeasureModel(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            GameObject go = Object.Instantiate(asset);
            try
            {
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                return RendererBounds(go);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static Bounds RendererBounds(GameObject go)
        {
            Renderer[] rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0)
            {
                return new Bounds(go.transform.position, Vector3.zero);
            }
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++)
            {
                b.Encapsulate(rs[i].bounds);
            }
            return b;
        }

        private static void SplitPassengerMeshes(string fbxPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            GameObject go = Object.Instantiate(asset);
            try
            {
                var found = new Dictionary<string, Mesh>();
                foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>())
                {
                    Mesh src = mf.sharedMesh;
                    Material[] slots = mf.GetComponent<Renderer>().sharedMaterials;
                    Matrix4x4 toRoot = Matrix4x4.Rotate(ModelToForward) * go.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                    for (int sub = 0; sub < src.subMeshCount && sub < slots.Length; sub++)
                    {
                        string slot = slots[sub] != null ? slots[sub].name.Split('.')[0] : "";
                        string part = slot == "Cap" ? "Cap" : "Body";
                        found[part] = ExtractSubmesh(src, sub, toRoot, "Passenger_" + part);
                    }
                }

                foreach (string part in new[] { "Body", "Cap" })
                {
                    if (!found.TryGetValue(part, out Mesh mesh))
                    {
                        throw new InvalidOperationException("Passenger submesh not found: " + part);
                    }
                    SaveMesh(mesh, $"{ModelsDir}/Passenger_{part}.asset");
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static Mesh ExtractSubmesh(Mesh src, int subMesh, Matrix4x4 toRoot, string name)
        {
            int[] tris = src.GetTriangles(subMesh);
            Vector3[] v = src.vertices;
            Vector3[] n = src.normals;
            Vector2[] uv = src.uv;
            var remap = new Dictionary<int, int>();
            var nv = new List<Vector3>();
            var nn = new List<Vector3>();
            var nuv = new List<Vector2>();
            var nt = new int[tris.Length];

            for (int i = 0; i < tris.Length; i++)
            {
                int old = tris[i];
                if (!remap.TryGetValue(old, out int idx))
                {
                    idx = nv.Count;
                    remap[old] = idx;
                    nv.Add(toRoot.MultiplyPoint3x4(v[old]));
                    if (n.Length > 0) nn.Add(toRoot.MultiplyVector(n[old]).normalized);
                    if (uv.Length > 0) nuv.Add(uv[old]);
                }
                nt[i] = idx;
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(nv);
            if (nn.Count > 0) mesh.SetNormals(nn);
            if (nuv.Count > 0) mesh.SetUVs(0, nuv);
            mesh.SetTriangles(nt, 0);
            if (nn.Count == 0) mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            return mesh;
        }

        private static void SaveMesh(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                EditorUtility.SetDirty(existing);
            }
            else
            {
                AssetDatabase.CreateAsset(mesh, path);
            }
        }

        // ------------------------------------------------------------------ prefabs

        private static void CreateVehiclePrefabs(Dictionary<string, Bounds> scaledBounds)
        {
            Report.AppendLine("Vehicle prefabs (final footprint, X = width, Z = length):");
            foreach ((string name, float _, float _) in VehicleTargets)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelsDir}/{name}.fbx");
                var root = new GameObject(name);
                try
                {
                    var modelGo = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                    modelGo.name = "Model";
                    // The FBX fronts (headlights, arrow tip) face -Z after import; turn them to face +Z.
                    modelGo.transform.SetLocalPositionAndRotation(Vector3.zero, ModelToForward);

                    Bounds b = scaledBounds[name];
                    float widthScale = 1f;
                    if (b.size.x > MaxVehicleWidth)
                    {
                        // A uniform Scale Factor that gives the right length makes this model too wide for one cell.
                        widthScale = (float)Math.Round(MaxVehicleWidth / b.size.x, 3);
                        modelGo.transform.localScale = new Vector3(widthScale, 1f, 1f);
                    }

                    Bounds fb = RendererBounds(root);

                    var pivot = new GameObject("Pivot").transform;
                    pivot.SetParent(root.transform, false);
                    pivot.localPosition = new Vector3(fb.center.x, 0f, fb.center.z);

                    var exhaust = new GameObject("ExhaustPoint").transform;
                    exhaust.SetParent(root.transform, false);
                    exhaust.localPosition = new Vector3(fb.extents.x * 0.5f, fb.size.y * 0.2f, fb.min.z - 0.03f);
                    exhaust.localRotation = Quaternion.Euler(0f, 180f, 0f); // points backwards

                    foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
                    {
                        r.shadowCastingMode = ShadowCastingMode.On;
                        r.receiveShadows = true;
                    }

                    PrefabUtility.SaveAsPrefabAsset(root, $"{VehiclePrefabsDir}/{name}.prefab");
                    Report.AppendLine($"  {name}: {Fmt(fb.size)} (Model X scale {widthScale})");
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        private static void CreatePassengerPrefab(Dictionary<string, Material> mats)
        {
            var root = new GameObject("Passenger");
            try
            {
                foreach (string part in new[] { "Body", "Cap" })
                {
                    var child = new GameObject(part);
                    child.transform.SetParent(root.transform, false);
                    child.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{ModelsDir}/Passenger_{part}.asset");
                    var mr = child.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = mats["Red"];
                    mr.shadowCastingMode = ShadowCastingMode.On;
                    mr.receiveShadows = true;
                }

                Bounds b = RendererBounds(root);
                PrefabUtility.SaveAsPrefabAsset(root, $"{CharacterPrefabsDir}/Passenger.prefab");
                Report.AppendLine($"  Passenger prefab: {Fmt(b.size)} (children Body + Cap)");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ------------------------------------------------------------------ config

        private static GameConfig CreateGameConfig(Dictionary<string, Material> mats)
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<GameConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            var palette = new GameConfig.PaletteEntry[GameColors.Count];
            for (int i = 0; i < GameColors.Count; i++)
            {
                var c = (GameColor)i;
                Material m = mats[c.ToString()];
                palette[i] = new GameConfig.PaletteEntry { id = c, color = m.GetColor("_BaseColor"), material = m };
            }
            config.palette = palette;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            return config;
        }

        // ------------------------------------------------------------------ font

        private static TMP_FontAsset CreateFontAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null)
            {
                return existing;
            }

            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (font == null)
            {
                Report.AppendLine("Font: Fredoka not found, using TMP default font.");
                return null;
            }

            TMP_FontAsset fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            fa.name = "Fredoka-SemiBold SDF";
            fa.TryAddCharacters(" !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~", out string missing);

            AssetDatabase.CreateAsset(fa, FontAssetPath);
            fa.atlasTextures[0].name = "Fredoka-SemiBold Atlas";
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            fa.material.name = "Fredoka-SemiBold Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);

            var liberation = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (liberation != null)
            {
                fa.fallbackFontAssetTable = new List<TMP_FontAsset> { liberation };
            }
            EditorUtility.SetDirty(fa);

            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
            if (settings != null)
            {
                var sso = new SerializedObject(settings);
                sso.FindProperty("m_defaultFontAsset").objectReferenceValue = fa;
                sso.ApplyModifiedPropertiesWithoutUndo();
            }

            AssetDatabase.SaveAssets();
            Report.AppendLine($"Font: Fredoka SemiBold TMP asset created (dynamic, missing ASCII glyphs: '{missing}'), set as TMP default font.");
            return fa;
        }

        // ------------------------------------------------------------------ scenes

        private static void CreateScenes(TMP_FontAsset font)
        {
            string boot = $"{ScenesDir}/Boot.unity";
            string home = $"{ScenesDir}/Home.unity";
            string game = $"{ScenesDir}/Game.unity";

            // Boot
            Scene s = NewScene();
            CreateUiCamera(GameColors.Hex(0x000000));
            new GameObject("Boot").AddComponent<BootLoader>();
            EditorSceneManager.SaveScene(s, boot);

            // Home
            s = NewScene();
            CreateUiCamera(GameColors.Hex(0x8FD3F4));
            Transform canvas = CreateCanvas(font, SceneLoader.HomeScene);
            CreateButton(canvas, font, "PLAY", SceneLoader.GameScene, GameColors.Hex(0x3DBE5A));
            CreateEventSystem();
            EditorSceneManager.SaveScene(s, home);

            // Game
            s = NewScene();
            BuildGameWorld();
            canvas = CreateCanvas(font, SceneLoader.GameScene);
            CreateButton(canvas, font, "HOME", SceneLoader.HomeScene, GameColors.Hex(0x3B7BE8));
            CreateEventSystem();
            EditorSceneManager.SaveScene(s, game);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(boot, true),
                new EditorBuildSettingsScene(home, true),
                new EditorBuildSettingsScene(game, true)
            };

            EditorSceneManager.OpenScene(boot);
            Report.AppendLine("Build Settings: 0 Boot, 1 Home, 2 Game.");
        }

        private static Scene NewScene()
        {
            Scene s = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = GameColors.Hex(0x9AA4B8);
            RenderSettings.ambientEquatorColor = GameColors.Hex(0x7E8698);
            RenderSettings.ambientGroundColor = GameColors.Hex(0x565B67);
            RenderSettings.ambientIntensity = 1f;
            return s;
        }

        private static Camera CreateUiCamera(Color background)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            cam.orthographic = true;
            go.transform.position = new Vector3(0f, 0f, -10f);
            return cam;
        }

        /// <summary>
        /// Minimal Game scene world: light + orthographic board camera. The background, lot grid and board are added by
        /// the Step 3 builder (Tools/Bus Rush/Build Gameplay (Step 3)).
        /// </summary>
        private static void BuildGameWorld()
        {
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = GameColors.Hex(0xFFF5E6);
            light.intensity = 1.0f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.55f;
            lightGo.transform.rotation = Quaternion.Euler(55f, 115f, 0f);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = GameColors.Hex(0xD5DAE6);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 200f;
            var fitter = camGo.AddComponent<CameraFitter>();
            fitter.Fit(RefWidth / RefHeight);
            SetUrpShadowDistance(60f);

            Report.AppendLine($"Game camera: orthographic, pos {Fmt(camGo.transform.position)}, rot (58,0,0), size {cam.orthographicSize:F2} at 390x844.");
        }

        private static Transform CreateCanvas(TMP_FontAsset font, string label)
        {
            var go = new GameObject("Canvas", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            var labelGo = new GameObject("SceneLabel", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var rt = (RectTransform)labelGo.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(360f, 120f);
            rt.anchoredPosition = Vector2.zero;
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = label;
            tmp.fontSize = 64f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.outlineWidth = 0.2f;
            tmp.outlineColor = new Color32(30, 40, 60, 255);
            tmp.raycastTarget = false;

            return go.transform;
        }

        private static void CreateButton(Transform canvas, TMP_FontAsset font, string text, string targetScene, Color color)
        {
            var go = new GameObject("TempSceneButton", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(220f, 72f);
            rt.anchoredPosition = new Vector2(0f, 90f);

            var image = go.AddComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.color = color;
            go.AddComponent<Button>().targetGraphic = image;

            var slb = go.AddComponent<SceneLoadButton>();
            var so = new SerializedObject(slb);
            so.FindProperty("sceneName").stringValue = targetScene;
            so.ApplyModifiedPropertiesWithoutUndo();

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var lrt = (RectTransform)labelGo.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = 36f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<UnityEngine.EventSystems.EventSystem>();
            go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        private static string Fmt(Vector3 v) => $"({v.x:F3}, {v.y:F3}, {v.z:F3})";
    }
}
