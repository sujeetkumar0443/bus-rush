using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BusRush.Gameplay;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Steps P1 / P1b: the pilot-helmet passenger v2 as ONE skinned mesh with 6 bones (Torso, Head [+ Cap], ArmL, ArmR,
    /// LegL, LegR; 1 bone per vertex) and vertex colours (rgb = part colour in sRGB, a = team mask), drawn with one
    /// "BusRush/PassengerVC" material per game colour, plus an instanced blob shadow. The mesh is baked here from the
    /// 7 parts of Passenger2_Crowd.fbx (facing +Z) and saved as Art/Models/Passenger2/Passenger2_Skinned.asset;
    /// Prefabs/Characters/Passenger keeps its path + GUID, so the crowd keeps its reference.
    /// </summary>
    public static partial class GameplayBuilder
    {
        private const string P2SourceDir = "C:/UnityBuilds/BusRush_Source/Passenger2/Passenger2";
        private const string P2Dir = "Assets/BusRush/Art/Models/Passenger2";
        private const string P2CrowdPath = P2Dir + "/Passenger2_Crowd.fbx";
        private const string P2SkinnedMeshPath = P2Dir + "/Passenger2_Skinned.asset";
        private const string P2MaterialsDir = "Assets/BusRush/Art/Materials/Passenger2";
        private const string BlobMaterialPath = P2MaterialsDir + "/PassengerBlob.mat";

        /// <summary>README Team colours per game colour (orange has no game colour).</summary>
        private static readonly Dictionary<GameColor, uint> P2TeamColours = new Dictionary<GameColor, uint>
        {
            { GameColor.Red, 0xE8262B }, { GameColor.Blue, 0x2F6BFF }, { GameColor.Yellow, 0xFFC81E }, { GameColor.Green, 0x3DBE3A },
            { GameColor.Purple, 0x9A4DFF }, { GameColor.Pink, 0xFF4FA8 }, { GameColor.Cyan, 0x1EC3D6 }
        };

        /// <summary>Part colours (sRGB) for the shared slots; Team = white x team colour, TeamDark = darker x team colour.</summary>
        private static readonly Dictionary<string, Color32> P2SlotColours = new Dictionary<string, Color32>
        {
            { "Skin", new Color32(0xFF, 0xD2, 0xB0, 0) },
            { "Brown", new Color32(0x7A, 0x45, 0x26, 0) },
            { "GoggleFrame", new Color32(0xC9, 0xD0, 0xDA, 0) },
            { "Lens", new Color32(0x9C, 0xDC, 0xFF, 0) },
            { "Eye", new Color32(0x1E, 0x1A, 0x2E, 0) },
            { "EyeShine", new Color32(0xFF, 0xFF, 0xFF, 0) },
            { "Cheek", new Color32(0xFF, 0x9A, 0xAE, 0) },
            { "Team", new Color32(0xFF, 0xFF, 0xFF, 0xFF) },
            // README TeamDark is ~0.6 of Team in linear terms; 0xCC (sRGB) ~= 0.6 linear.
            { "TeamDark", new Color32(0xCC, 0xCC, 0xCC, 0xFF) }
        };

        private static readonly string[] P2Bones = { "Torso", "Head", "ArmL", "ArmR", "LegL", "LegR" };

        [MenuItem("Tools/Bus Rush/Build Passenger v2 (P1b skinned)")]
        public static void BuildPassengerV2()
        {
            BuildSkinnedPassenger();
            AssetDatabase.SaveAssets();
            Debug.Log("[GameplayBuilder] Passenger v2 (skinned) built.");
        }

        public static void BuildPassengerV2Batch() => RunGuarded(BuildPassengerV2);

        /// <summary>Import, bake the skinned mesh, materials, quality (1 bone), prefab. Also used by the full gameplay build.</summary>
        private static void BuildSkinnedPassenger()
        {
            ImportPassengerV2();
            CreatePassengerVCMaterials();
            Material blob = PassengerBlobMaterial();
            Mesh mesh = BakeSkinnedMesh(out Dictionary<string, Matrix4x4> bonePoses);
            SetOneBoneSkinning();
            BuildSkinnedPassengerPrefab(mesh, bonePoses, blob);
            DeleteP1PartMaterials();
        }

        private static void ImportPassengerV2()
        {
            Directory.CreateDirectory(P2Dir);
            foreach (string f in new[] { "Passenger2_Crowd.fbx", "Passenger2_Rig.fbx" })
            {
                string dst = $"{P2Dir}/{f}";
                if (!File.Exists(dst)) File.Copy($"{P2SourceDir}/{f}", dst, true);
                AssetDatabase.ImportAsset(dst);
                var mi = (ModelImporter)AssetImporter.GetAtPath(dst);
                mi.globalScale = 1f;
                mi.useFileScale = true;
                mi.bakeAxisConversion = true;
                mi.importAnimation = false;
                mi.animationType = ModelImporterAnimationType.None;
                mi.importBlendShapes = false;
                mi.importCameras = false;
                mi.importLights = false;
                mi.isReadable = f.Contains("Crowd"); // the bake reads its vertices
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                mi.SaveAndReimport();
            }
        }

        // ------------------------------------------------------------------ materials

        /// <summary>One PassengerVC material per game colour (only _TeamColor differs), in the GameConfig palette.</summary>
        private static void CreatePassengerVCMaterials()
        {
            Directory.CreateDirectory(P2MaterialsDir);
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/BusRush/Art/Shaders/PassengerVC.shader");
            var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/BusRush/Resources/GameConfig.asset");
            var palette = cfg.palette;
            for (int i = 0; i < palette.Length; i++)
            {
                GameColor c = palette[i].id;
                if (!P2TeamColours.TryGetValue(c, out uint rgb)) continue;
                string path = $"{P2MaterialsDir}/PassengerVC_{c}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(shader) { name = $"PassengerVC_{c}" };
                    AssetDatabase.CreateAsset(m, path);
                }
                m.shader = shader;
                m.SetColor("_TeamColor", GameColors.Hex(rgb));
                m.enableInstancing = false; // SRP Batcher handles these
                EditorUtility.SetDirty(m);
                palette[i].passengerBody = m;
                palette[i].passengerCap = m;
            }
            cfg.palette = palette;
            EditorUtility.SetDirty(cfg);
        }

        private static Material PassengerBlobMaterial()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/BusRush/Art/Shaders/BlobShadow.shader");
            var m = AssetDatabase.LoadAssetAtPath<Material>(BlobMaterialPath);
            if (m == null)
            {
                m = new Material(shader) { name = "PassengerBlob" };
                AssetDatabase.CreateAsset(m, BlobMaterialPath);
            }
            m.shader = shader;
            m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/contact_shadow.png"));
            m.SetColor("_Color", new Color(0.08f, 0.1f, 0.18f, 0.45f));
            m.enableInstancing = true;
            m.renderQueue = 2950;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static void DeleteP1PartMaterials()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Material P2_", new[] { P2MaterialsDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path).StartsWith("P2_")) AssetDatabase.DeleteAsset(path);
            }
        }

        private static void SetOneBoneSkinning()
        {
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.skinWeights = SkinWeights.OneBone;
            }
            QualitySettings.SetQualityLevel(current, false);
        }

        // ------------------------------------------------------------------ bake

        /// <summary>
        /// One mesh from the 7 parts: vertices in model space turned 180 degrees about Y (the parts face -Z after import;
        /// the baked mesh faces the game's +Z), vertex colours from the material slot, each vertex weighted 1.0 to its
        /// part's bone (Cap -> Head). Bone rest poses = the part pivots (also turned).
        /// </summary>
        private static Mesh BakeSkinnedMesh(out Dictionary<string, Matrix4x4> bonePoses)
        {
            var report = new StringBuilder("[GameplayBuilder] Passenger v2 skinned bake:");
            GameObject inst = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(P2CrowdPath));
            bonePoses = new Dictionary<string, Matrix4x4>();
            try
            {
                inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Matrix4x4 turn = Matrix4x4.Rotate(Quaternion.Euler(0f, 180f, 0f));
                Transform Find(string n) => inst.GetComponentsInChildren<Transform>(true).First(t => t.name == n);

                foreach (string bone in P2Bones)
                {
                    Transform t = Find(bone);
                    bonePoses[bone] = Matrix4x4.TRS(turn.MultiplyPoint3x4(t.position), Quaternion.identity, Vector3.one);
                }

                var verts = new List<Vector3>();
                var normals = new List<Vector3>();
                var colours = new List<Color32>();
                var weights = new List<BoneWeight>();
                var tris = new List<int>();
                foreach (string part in new[] { "Torso", "Head", "Cap", "ArmL", "ArmR", "LegL", "LegR" })
                {
                    Transform t = Find(part);
                    var mf = t.GetComponent<MeshFilter>();
                    var mr = t.GetComponent<MeshRenderer>();
                    Mesh src = mf.sharedMesh;
                    int bone = System.Array.IndexOf(P2Bones, part == "Cap" ? "Head" : part);
                    Matrix4x4 toModel = turn * t.localToWorldMatrix;
                    Vector3[] sv = src.vertices;
                    Vector3[] sn = src.normals;
                    int baseIndex = verts.Count;
                    for (int v = 0; v < sv.Length; v++)
                    {
                        verts.Add(toModel.MultiplyPoint3x4(sv[v]));
                        normals.Add(toModel.MultiplyVector(sn[v]).normalized);
                        colours.Add(new Color32(255, 0, 255, 0)); // overwritten per submesh below
                        weights.Add(new BoneWeight { boneIndex0 = bone, weight0 = 1f });
                    }
                    for (int sub = 0; sub < src.subMeshCount; sub++)
                    {
                        string slot = mr.sharedMaterials[sub] != null ? mr.sharedMaterials[sub].name.Split('.')[0] : "";
                        if (!P2SlotColours.TryGetValue(slot, out Color32 col))
                        {
                            report.Append($"\n  unknown slot '{slot}' on {part} (used Skin)");
                            col = P2SlotColours["Skin"];
                        }
                        int[] st = src.GetTriangles(sub);
                        foreach (int idx in st) colours[baseIndex + idx] = col;
                        foreach (int idx in st) tris.Add(baseIndex + idx);
                    }
                    report.Append($"\n  {part}: {sv.Length} verts, {src.subMeshCount} slots -> bone {P2Bones[bone]}");
                }

                var mesh = new Mesh { name = "Passenger2_Skinned" };
                if (verts.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(verts);
                mesh.SetNormals(normals);
                mesh.SetColors(colours);
                mesh.SetTriangles(tris, 0);
                mesh.boneWeights = weights.ToArray();
                // Bind poses: bone rest pose relative to the mesh root (no rotation in the rest pose).
                var bind = new Matrix4x4[P2Bones.Length];
                for (int i = 0; i < P2Bones.Length; i++) bind[i] = bonePoses[P2Bones[i]].inverse;
                mesh.bindposes = bind;
                mesh.RecalculateBounds();
                mesh.UploadMeshData(false);

                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(P2SkinnedMeshPath);
                if (existing != null)
                {
                    existing.Clear();
                    EditorUtility.CopySerialized(mesh, existing);
                    Object.DestroyImmediate(mesh);
                    mesh = existing;
                }
                else AssetDatabase.CreateAsset(mesh, P2SkinnedMeshPath);
                EditorUtility.SetDirty(mesh);
                report.Append($"\n  total {verts.Count} verts, {tris.Count / 3} tris, 1 submesh, bounds {mesh.bounds.size:F3}");
                Debug.Log(report.ToString());
                return mesh;
            }
            finally
            {
                Object.DestroyImmediate(inst);
            }
        }

        // ------------------------------------------------------------------ prefab

        private static void BuildSkinnedPassengerPrefab(Mesh mesh, Dictionary<string, Matrix4x4> poses, Material blob)
        {
            var root = new GameObject("Passenger");
            try
            {
                Transform Bone(string name, Transform parent)
                {
                    var t = new GameObject(name).transform;
                    t.SetParent(parent, false);
                    Vector3 world = poses[name].GetColumn(3);
                    t.position = world; // root is at the origin with no rotation while building
                    return t;
                }

                var rig = new GameObject("Rig").transform;
                rig.SetParent(root.transform, false);
                Transform torso = Bone("Torso", rig);
                Transform head = Bone("Head", torso);
                Transform armL = Bone("ArmL", torso);
                Transform armR = Bone("ArmR", torso);
                Transform legL = Bone("LegL", rig);
                Transform legR = Bone("LegR", rig);
                var bones = new[] { torso, head, armL, armR, legL, legR };

                var body = new GameObject("Body");
                body.transform.SetParent(root.transform, false);
                var smr = body.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = mesh;
                smr.bones = bones;
                smr.rootBone = rig;
                smr.quality = SkinQuality.Bone1;
                smr.updateWhenOffscreen = false;
                smr.skinnedMotionVectors = false;
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                smr.receiveShadows = false;
                smr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                smr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                smr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{P2MaterialsDir}/PassengerVC_Red.mat");
                Bounds b = mesh.bounds;
                // Room for the swings, bob and hop (bounds are relative to the root bone).
                smr.localBounds = new Bounds(b.center, b.size * 1.6f);

                var shadow = new GameObject("Shadow");
                shadow.transform.SetParent(root.transform, false);
                shadow.transform.SetLocalPositionAndRotation(new Vector3(0f, 0.015f, 0f), Quaternion.Euler(90f, 0f, 0f));
                shadow.transform.localScale = new Vector3(b.size.x * 1.05f, b.size.z * 1.05f, 1f);
                shadow.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                var sr = shadow.AddComponent<MeshRenderer>();
                sr.sharedMaterial = blob;
                sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                sr.receiveShadows = false;
                sr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                sr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

                var view = root.AddComponent<PassengerView>();
                UIKit.Wire(view, ("torso", torso), ("head", head), ("armL", armL), ("armR", armR), ("legL", legL), ("legR", legR),
                    ("body", smr), ("modelHeight", b.size.y), ("modelWidth", b.size.x));
                PrefabUtility.SaveAsPrefabAsset(root, PassengerPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
