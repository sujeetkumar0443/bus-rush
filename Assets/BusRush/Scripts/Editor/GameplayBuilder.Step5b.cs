using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BusRush.EditorTools
{
    /// <summary>Step 5b: rigged passenger (separate limbs) imported from Passenger_Rig.fbx.</summary>
    public static partial class GameplayBuilder
    {
        private const string RigSource = "C:/UnityBuilds/BusRush_Source/Passenger_Rig.fbx";
        private const string RigPath = "Assets/BusRush/Art/Models/Passenger_Rig.fbx";
        public static readonly string[] RigParts = { "Torso", "Head", "Cap", "ArmL", "ArmR", "LegL", "LegR" };

        [MenuItem("Tools/Bus Rush/Inspect Passenger Rig")]
        public static void ImportAndInspectRig()
        {
            if (!File.Exists(RigPath) || new FileInfo(RigSource).LastWriteTimeUtc > new FileInfo(RigPath).LastWriteTimeUtc)
            {
                File.Copy(RigSource, RigPath, true);
                AssetDatabase.ImportAsset(RigPath, ImportAssetOptions.ForceUpdate);
            }

            Material body = PassengerMaterial("PassengerBody");
            Material cap = PassengerMaterial("PassengerCap");

            var mi = (ModelImporter)AssetImporter.GetAtPath(RigPath);
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.bakeAxisConversion = true;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.importCameras = false;
            mi.importLights = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            foreach (var kv in mi.GetExternalObjectMap()) mi.RemoveRemap(kv.Key);
            mi.SaveAndReimport();

            string[] slots = AssetDatabase.LoadAllAssetsAtPath(RigPath).OfType<Material>().Select(m => m.name).Distinct().ToArray();
            foreach (string slot in slots)
            {
                mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), slot), slot.Contains("Cap") ? cap : body);
            }
            mi.SaveAndReimport();

            var report = new StringBuilder();
            report.AppendLine($"[GameplayBuilder] Rig material slots: {string.Join(", ", slots)}");
            GameObject inst = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RigPath));
            try
            {
                inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                foreach (Transform t in inst.GetComponentsInChildren<Transform>())
                {
                    Renderer r = t.GetComponent<Renderer>();
                    string path = t == inst.transform ? "<root>" : AnimationUtility.CalculateTransformPath(t, inst.transform);
                    report.AppendLine($"  {path}: localPos {t.localPosition:F3} localRot {t.localEulerAngles:F1} scale {t.localScale:F2}" +
                                      (r != null ? $" bounds c{r.bounds.center:F3} s{r.bounds.size:F3} mats[{string.Join(",", r.sharedMaterials.Select(m => m != null ? m.name : "null"))}]" : ""));
                }
                Bounds all = inst.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                report.AppendLine($"  total bounds centre {all.center:F3} size {all.size:F3}");
            }
            finally
            {
                Object.DestroyImmediate(inst);
            }
            Debug.Log(report.ToString());
        }

        public static void ImportAndInspectRigBatch() => RunGuarded(ImportAndInspectRig);

        private static void RunGuarded(System.Action action)
        {
            try
            {
                action();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[GameplayBuilder] FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Rebuilds Prefabs/Characters/Passenger (same path and GUID) from the rig: Model child turned to face +Z,
        /// Head (with Cap) and both arms parented under Torso so they follow its lean, blob shadow, PassengerView wired.
        /// </summary>
        private static void BuildRiggedPassengerPrefab()
        {
            var root = new GameObject("Passenger");
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RigPath), root.transform);
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                model.name = "Model";
                // The rig faces -Z (cap brim at -Z), like the vehicle models: turn it to the game's +Z forward.
                model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 180f, 0f));

                Transform Part(string n) => model.transform.Find(n) ?? throw new System.InvalidOperationException("Rig part missing: " + n);
                Transform torso = Part("Torso"), head = Part("Head"), cap = Part("Cap");
                Transform armL = Part("ArmL"), armR = Part("ArmR"), legL = Part("LegL"), legR = Part("LegR");
                head.SetParent(torso, true);
                cap.SetParent(head, true);
                armL.SetParent(torso, true);
                armR.SetParent(torso, true);

                foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // blob shadow instead (mobile)
                    r.receiveShadows = false;
                }

                var shadow = new GameObject("Shadow");
                shadow.transform.SetParent(root.transform, false);
                shadow.transform.SetLocalPositionAndRotation(new Vector3(0f, 0.015f, 0f), Quaternion.Euler(90f, 0f, 0f));
                shadow.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
                shadow.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                var sr = shadow.AddComponent<MeshRenderer>();
                sr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/ContactShadow.mat");
                sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                sr.receiveShadows = false;

                var view = root.AddComponent<BusRush.Gameplay.PassengerView>();
                Renderer R(Transform t) => t.GetComponent<Renderer>();
                UIKit.Wire(view, ("torso", torso), ("head", head), ("armL", armL), ("armR", armR), ("legL", legL), ("legR", legR),
                    ("bodyRenderers", new Object[] { R(torso), R(head), R(armL), R(armR), R(legL), R(legR) }),
                    ("capRenderers", new Object[] { R(cap) }));

                PrefabUtility.SaveAsPrefabAsset(root, PassengerPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Material PassengerMaterial(string name)
        {
            Material m = LoadOrCreate(name, "Universal Render Pipeline/Lit");
            m.SetColor("_BaseColor", Color.white); // tinted per person with a MaterialPropertyBlock
            m.SetFloat("_Smoothness", 0.45f);
            m.SetFloat("_Metallic", 0f);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
