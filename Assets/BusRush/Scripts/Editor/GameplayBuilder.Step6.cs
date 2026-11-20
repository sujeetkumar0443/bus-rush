using System;
using BusRush.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Step 6: shared per-colour passenger materials (SRP Batcher instead of property blocks), dim/full glow materials,
    /// world crossfade overlay and the tutorial controller.
    /// </summary>
    public static partial class GameplayBuilder
    {
        private static void BuildStep6Assets()
        {
            CreatePassengerColorMaterials();

            Texture2D soft = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/soft_circle.png");
            Material headOn = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/HeadGlow.mat");
            Material tailOn = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/TailGlow.mat");
            Material headDim = CreateParticleMaterial("HeadGlowDim", soft, new Color(1f, 0.93f, 0.7f, 0.55f * 0.3f), 2);
            Material tailDim = CreateParticleMaterial("TailGlowDim", soft, new Color(1f, 0.15f, 0.1f, 0.35f), 2);

            foreach (VehicleType type in (VehicleType[])Enum.GetValues(typeof(VehicleType)))
            {
                string path = $"{VehiclesDir}/{type}.prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    VehicleMotion motion = root.GetComponent<VehicleMotion>();
                    UIKit.Wire(motion, ("headGlowOn", headOn), ("headGlowDim", headDim), ("tailGlowOn", tailOn), ("tailGlowDim", tailDim));
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            Material fade = CreateTransparentUnlit("World_BackgroundFade", null, Color.white);
            fade.renderQueue = (int)RenderQueue.Geometry + 400; // just after the opaque background, before transparents
            EditorUtility.SetDirty(fade);
            AssetDatabase.SaveAssets();
        }

        /// <summary>One Body and one Cap material per colour, referenced from the GameConfig palette.</summary>
        private static void CreatePassengerColorMaterials()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/BusRush/Resources/GameConfig.asset");
            var palette = cfg.palette;
            for (int i = 0; i < palette.Length; i++)
            {
                GameColor c = palette[i].id;
                Color body = BodyColor(c, palette[i].color);
                Color.RGBToHSV(body, out float h, out float s, out float v);
                Color cap = Color.HSVToRGB(h, Mathf.Clamp01(s * 1.05f), v * 0.72f);

                palette[i].passengerBody = ColorMaterial($"PassengerBody_{c}", body);
                palette[i].passengerCap = ColorMaterial($"PassengerCap_{c}", cap);
            }
            cfg.palette = palette;
            EditorUtility.SetDirty(cfg);
        }

        /// <summary>Same rule as GameColors.GetPassengerBodyColor (pink and blue lifted a little).</summary>
        private static Color BodyColor(GameColor c, Color baseColor)
        {
            if (c != GameColor.Pink && c != GameColor.Blue) return baseColor;
            Color.RGBToHSV(baseColor, out float h, out float s, out float v);
            return Color.HSVToRGB(h, s * 0.9f, Mathf.Min(1f, v * 1.12f));
        }

        private static Material ColorMaterial(string name, Color color)
        {
            Material m = LoadOrCreate(name, "Universal Render Pipeline/Lit");
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", 0.45f);
            m.SetFloat("_Metallic", 0f);
            m.enableInstancing = true; // used when the SRP Batcher is off (see perf notes)
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Scene parts for Step 6 (called from WireGameScene).</summary>
        private static void WireStep6Scene(GameObject world, GameObject board, Camera cam, GameSession session, InputController input)
        {
            var overlayGo = new GameObject("CrossfadeOverlay");
            overlayGo.transform.SetParent(world.transform, false);
            overlayGo.transform.localPosition = new Vector3(0f, 0.002f, 0f);
            overlayGo.AddComponent<MeshFilter>();
            var overlay = overlayGo.AddComponent<MeshRenderer>();
            overlay.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/World_BackgroundFade.mat");
            overlay.shadowCastingMode = ShadowCastingMode.Off;
            overlay.receiveShadows = false;
            overlay.enabled = false;
            UIKit.Wire(world.GetComponent<WorldBackground>(), ("overlay", overlay));

            TutorialController tutorial = board.AddComponent<TutorialController>();
            UIKit.Wire(tutorial, ("session", session), ("input", input), ("viewCamera", cam));
        }
    }
}
