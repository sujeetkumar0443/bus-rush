using BusRush.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static BusRush.EditorTools.UIKit;
using Object = UnityEngine.Object;

namespace BusRush.EditorTools
{
    /// <summary>Step 5 additions: passenger prefab rig, crowd, confetti / sparkle effects.</summary>
    public static partial class GameplayBuilder
    {
        private const string PassengerPrefabPath = "Assets/BusRush/Prefabs/Characters/Passenger.prefab";

        private static void BuildStep5()
        {
            Material confetti = CreateParticleMaterial("Confetti", null, Color.white, 0);
            Material sparkle = CreateParticleMaterial("Sparkle", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/soft_circle.png"), Color.white, 2);
            UpgradePassengerPrefab();
            BuildFxPrefab(confetti, sparkle);
            AssetDatabase.SaveAssets();
        }

        private static void UpgradePassengerPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PassengerPrefabPath);
            try
            {
                Transform visual = root.transform.Find("Visual");
                if (visual == null)
                {
                    visual = new GameObject("Visual").transform;
                    visual.SetParent(root.transform, false);
                    root.transform.Find("Body").SetParent(visual, false);
                    root.transform.Find("Cap").SetParent(visual, false);
                }
                Renderer body = visual.Find("Body").GetComponent<Renderer>();
                Renderer cap = visual.Find("Cap").GetComponent<Renderer>();
                foreach (Renderer r in new[] { body, cap })
                {
                    r.shadowCastingMode = ShadowCastingMode.Off; // crowds use the blob shadow (mobile)
                    r.receiveShadows = false;
                }

                Transform oldShadow = root.transform.Find("Shadow");
                if (oldShadow != null) Object.DestroyImmediate(oldShadow.gameObject);
                var shadow = new GameObject("Shadow");
                shadow.transform.SetParent(root.transform, false);
                shadow.transform.SetLocalPositionAndRotation(new Vector3(0f, 0.015f, 0f), Quaternion.Euler(90f, 0f, 0f));
                shadow.transform.localScale = new Vector3(0.62f, 0.62f, 1f);
                shadow.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                var sr = shadow.AddComponent<MeshRenderer>();
                sr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/ContactShadow.mat");
                sr.shadowCastingMode = ShadowCastingMode.Off;
                sr.receiveShadows = false;

                if (!root.TryGetComponent(out PassengerView view)) view = root.AddComponent<PassengerView>();
                Wire(view, ("visual", visual), ("body", body), ("cap", cap));
                PrefabUtility.SaveAsPrefabAsset(root, PassengerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void BuildFxPrefab(Material confettiMat, Material sparkleMat)
        {
            var root = new GameObject("GameFx");
            try
            {
                ParticleSystem confetti = NewFxSystem(root.transform, "Confetti", confettiMat);
                ParticleSystem.MainModule cm = confetti.main;
                cm.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 2.6f);
                cm.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
                cm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                cm.gravityModifier = 0.9f;
                cm.maxParticles = 220;
                ParticleSystem.RotationOverLifetimeModule rot = confetti.rotationOverLifetime;
                rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
                ParticleSystem.LimitVelocityOverLifetimeModule drag = confetti.limitVelocityOverLifetime;
                drag.enabled = true;
                drag.drag = 1.2f;
                ParticleSystem.ColorOverLifetimeModule cc = confetti.colorOverLifetime;
                cc.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                          new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
                cc.color = g;

                ParticleSystem sparkle = NewFxSystem(root.transform, "Sparkle", sparkleMat);
                ParticleSystem.MainModule sm = sparkle.main;
                sm.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
                sm.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
                sm.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
                sm.maxParticles = 60;
                ParticleSystem.ShapeModule shape = sparkle.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.15f;
                ParticleSystem.SizeOverLifetimeModule size = sparkle.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));

                GameFx fx = root.AddComponent<GameFx>();
                Wire(fx, ("confetti", confetti), ("sparkle", sparkle));
                PrefabUtility.SaveAsPrefabAsset(root, $"{GameplayPrefabsDir}/GameFx.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static ParticleSystem NewFxSystem(Transform parent, string name, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false; // emitted from code
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = material;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        /// <summary>Crowd + effects in the Game scene; called from WireGameScene with the Board object.</summary>
        private static (CrowdView crowd, GameFx fx) BuildCrowdAndFx(GameObject board)
        {
            var crowdGo = new GameObject("Crowd");
            crowdGo.transform.SetParent(board.transform, false);
            CrowdView crowd = crowdGo.AddComponent<CrowdView>();
            Wire(crowd, ("passengerPrefab", AssetDatabase.LoadAssetAtPath<PassengerView>(PassengerPrefabPath)));

            var fxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{GameplayPrefabsDir}/GameFx.prefab");
            var fxGo = (GameObject)PrefabUtility.InstantiatePrefab(fxPrefab);
            fxGo.transform.SetParent(board.transform, false);
            return (crowd, fxGo.GetComponent<GameFx>());
        }
    }
}
