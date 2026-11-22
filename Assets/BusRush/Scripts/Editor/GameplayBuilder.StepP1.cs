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
    /// Step P1: the pilot-helmet passenger v2 (Passenger2_Crowd.fbx) replaces the old rig in Prefabs/Characters/Passenger
    /// (same path + GUID, so the crowd keeps its reference). Shared materials for Skin / Brown / GoggleFrame / Lens / Eye /
    /// EyeShine / Cheek, and one Team + TeamDark material per game colour (stored in the GameConfig palette as
    /// passengerBody / passengerCap), so all passengers of a colour share materials (SRP Batcher friendly).
    /// </summary>
    public static partial class GameplayBuilder
    {
        private const string P2SourceDir = "C:/UnityBuilds/BusRush_Source/Passenger2/Passenger2";
        private const string P2Dir = "Assets/BusRush/Art/Models/Passenger2";
        private const string P2CrowdPath = P2Dir + "/Passenger2_Crowd.fbx";
        private const string P2MaterialsDir = "Assets/BusRush/Art/Materials/Passenger2";

        /// <summary>README colours per game colour (Team, TeamDark); orange has no game colour.</summary>
        private static readonly Dictionary<GameColor, (uint team, uint dark)> P2Colours = new Dictionary<GameColor, (uint, uint)>
        {
            { GameColor.Red, (0xE8262B, 0xB8151B) },
            { GameColor.Blue, (0x2F6BFF, 0x1D45B8) },
            { GameColor.Yellow, (0xFFC81E, 0xD49A0C) },
            { GameColor.Green, (0x3DBE3A, 0x24862A) },
            { GameColor.Purple, (0x9A4DFF, 0x6A2CC2) },
            { GameColor.Pink, (0xFF4FA8, 0xC92F7C) },
            { GameColor.Cyan, (0x1EC3D6, 0x128E9C) }
        };

        [MenuItem("Tools/Bus Rush/Build Passenger v2 (P1)")]
        public static void BuildPassengerV2()
        {
            ImportPassengerV2();
            Dictionary<string, Material> shared = PassengerV2SharedMaterials();
            CreatePassengerV2TeamMaterials();
            BuildPassengerV2Prefab(shared);
            AssetDatabase.SaveAssets();
            Debug.Log("[GameplayBuilder] Passenger v2 built.");
        }

        public static void BuildPassengerV2Batch() => RunGuarded(BuildPassengerV2);

        /// <summary>For the full gameplay build (Step 5): import the FBX and make every material the prefab needs.</summary>
        private static Dictionary<string, Material> PassengerV2SharedMaterialsAfterImport()
        {
            ImportPassengerV2();
            CreatePassengerV2TeamMaterials();
            return PassengerV2SharedMaterials();
        }

        private static void ImportPassengerV2()
        {
            Directory.CreateDirectory(P2Dir);
            foreach (string f in new[] { "Passenger2_Crowd.fbx", "Passenger2_Rig.fbx" })
            {
                string dst = $"{P2Dir}/{f}";
                File.Copy($"{P2SourceDir}/{f}", dst, true);
                AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceUpdate);
                var mi = (ModelImporter)AssetImporter.GetAtPath(dst);
                mi.globalScale = 1f;
                mi.useFileScale = true;
                mi.bakeAxisConversion = true;
                mi.importAnimation = false;
                mi.animationType = ModelImporterAnimationType.None;
                mi.importBlendShapes = false;
                mi.importCameras = false;
                mi.importLights = false;
                mi.isReadable = false;
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                mi.SaveAndReimport();
            }
        }

        private static Material P2Material(string name, Color color, float smoothness, float metallic = 0f, bool emissive = false)
        {
            Directory.CreateDirectory(P2MaterialsDir);
            string path = $"{P2MaterialsDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (emissive)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * 0.6f);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Dictionary<string, Material> PassengerV2SharedMaterials()
        {
            return new Dictionary<string, Material>
            {
                { "Skin", P2Material("P2_Skin", GameColors.Hex(0xFFD2B0), 0.35f) },
                { "Brown", P2Material("P2_Brown", GameColors.Hex(0x7A4526), 0.3f) },
                { "GoggleFrame", P2Material("P2_GoggleFrame", GameColors.Hex(0xC9D0DA), 0.7f, 0.3f) },
                { "Lens", P2Material("P2_Lens", GameColors.Hex(0x8FD6FF), 0.9f, 0f, true) },
                { "Eye", P2Material("P2_Eye", GameColors.Hex(0x1E1A2E), 0.6f) },
                { "EyeShine", P2Material("P2_EyeShine", Color.white, 0.2f, 0f, true) },
                { "Cheek", P2Material("P2_Cheek", GameColors.Hex(0xFF9AAE), 0.3f) }
            };
        }

        /// <summary>Team / TeamDark per game colour, referenced from the GameConfig palette (passengerBody / passengerCap).</summary>
        private static void CreatePassengerV2TeamMaterials()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/BusRush/Resources/GameConfig.asset");
            var palette = cfg.palette;
            for (int i = 0; i < palette.Length; i++)
            {
                GameColor c = palette[i].id;
                if (!P2Colours.TryGetValue(c, out var col)) continue;
                palette[i].passengerBody = P2Material($"P2_Team_{c}", GameColors.Hex(col.team), 0.55f);
                palette[i].passengerCap = P2Material($"P2_TeamDark_{c}", GameColors.Hex(col.dark), 0.45f);
            }
            cfg.palette = palette;
            EditorUtility.SetDirty(cfg);
        }

        private static void BuildPassengerV2Prefab(Dictionary<string, Material> shared)
        {
            var report = new StringBuilder("[GameplayBuilder] Passenger v2:");
            var root = new GameObject("Passenger");
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(P2CrowdPath), root.transform);
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                model.name = "Model";
                // The model's face (goggles, eyes) is at -Z after import: turn it to the game's +Z forward.
                model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 180f, 0f));
                Transform rig = model.transform.Find("Passenger") ?? model.transform;

                Transform Part(string n) => rig.Find(n) ?? model.transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n)
                                            ?? throw new System.InvalidOperationException("Passenger2 part missing: " + n);
                Transform torso = Part("Torso"), head = Part("Head"), cap = Part("Cap");
                Transform armL = Part("ArmL"), armR = Part("ArmR"), legL = Part("LegL"), legR = Part("LegR");
                // Helmet follows the head; head and arms follow the torso (lean, breathing).
                cap.SetParent(head, true);
                head.SetParent(torso, true);
                armL.SetParent(torso, true);
                armR.SetParent(torso, true);

                // Shared materials by slot name; Team / TeamDark slots are recorded for per-colour swaps.
                var colourRenderers = new List<Object>();
                var colourSlots = new List<int>();
                var colourDark = new List<bool>();
                Material placeholderTeam = AssetDatabase.LoadAssetAtPath<Material>($"{P2MaterialsDir}/P2_Team_Red.mat");
                Material placeholderDark = AssetDatabase.LoadAssetAtPath<Material>($"{P2MaterialsDir}/P2_TeamDark_Red.mat");
                foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        string slot = mats[i] != null ? mats[i].name.Replace(" (Instance)", "") : "";
                        string key = shared.Keys.FirstOrDefault(k => slot == k || slot.StartsWith(k + "."));
                        if (slot == "TeamDark" || slot.StartsWith("TeamDark."))
                        {
                            mats[i] = placeholderDark;
                            colourRenderers.Add(r);
                            colourSlots.Add(i);
                            colourDark.Add(true);
                        }
                        else if (slot == "Team" || slot.StartsWith("Team."))
                        {
                            mats[i] = placeholderTeam;
                            colourRenderers.Add(r);
                            colourSlots.Add(i);
                            colourDark.Add(false);
                        }
                        else if (key != null) mats[i] = shared[key];
                        else report.Append($"\n  unknown material slot '{slot}' on {r.name}");
                    }
                    r.sharedMaterials = mats;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // blob shadow (mobile)
                    r.receiveShadows = false;
                    r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                    r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                    var mf = r.GetComponent<MeshFilter>();
                    report.Append($"\n  {r.name}: {mats.Length} mats [{string.Join(",", mats.Select(m => m != null ? m.name : "null"))}]" +
                                  (mf != null && mf.sharedMesh != null ? $" {mf.sharedMesh.triangles.Length / 3} tris" : ""));
                }

                Bounds b = model.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((x, y) => { x.Encapsulate(y); return x; });
                report.Append($"\n  model bounds centre {b.center:F3} size {b.size:F3}; hip (LegL) y {legL.position.y:F3}; head y {head.position.y:F3}");
                Vector3 goggle = cap.GetComponent<Renderer>() != null ? cap.GetComponent<Renderer>().bounds.center : cap.position;
                report.Append($"\n  cap centre {goggle:F3} (face side = {(goggle.z > 0f ? "+Z" : "-Z")})");

                var shadow = new GameObject("Shadow");
                shadow.transform.SetParent(root.transform, false);
                shadow.transform.SetLocalPositionAndRotation(new Vector3(0f, 0.015f, 0f), Quaternion.Euler(90f, 0f, 0f));
                shadow.transform.localScale = new Vector3(b.size.x * 0.95f, b.size.z * 0.95f, 1f);
                shadow.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                var sr = shadow.AddComponent<MeshRenderer>();
                sr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/ContactShadow.mat");
                sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                sr.receiveShadows = false;

                var view = root.AddComponent<PassengerView>();
                UIKit.Wire(view, ("torso", torso), ("head", head), ("armL", armL), ("armR", armR), ("legL", legL), ("legR", legR),
                    ("colourRenderers", colourRenderers.ToArray()), ("colourSlots", colourSlots.Cast<object>().ToArray()),
                    ("colourDark", colourDark.Cast<object>().ToArray()), ("modelHeight", b.size.y), ("modelWidth", b.size.x));
                PrefabUtility.SaveAsPrefabAsset(root, PassengerPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            Debug.Log(report.ToString());
        }
    }
}
