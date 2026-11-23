using System;
using BusRush.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static BusRush.EditorTools.UIKit;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Road Map chest: the soft gold glow behind a READY chest (MapChestView.glow). <see cref="AddChestGlow"/> is used by the
    /// full Road Map build and by the in-place patch (Tools > Bus Rush > Patch Road Map Chest Glow) on RoadMap.prefab.
    /// </summary>
    public static partial class UIBuilder
    {
        private static Image AddChestGlow(RectTransform chest)
        {
            Transform existing = chest.Find("ReadyGlow");
            Image glow = existing != null ? existing.GetComponent<Image>() : Img(chest, "ReadyGlow", SoftCircleSprite(), new Color(1f, 0.89f, 0.48f, 0.6f));
            glow.raycastTarget = false;
            // Centred on the 56x48 chest image (its centre sits at 30, 24 below the column's top-left), 96 px wide.
            Box(glow.rectTransform, 30f - 48f, 24f - 48f, 96f, 96f, 0.5f, 0.5f);
            glow.rectTransform.SetAsFirstSibling();
            glow.gameObject.SetActive(false);
            return glow;
        }

        [MenuItem("Tools/Bus Rush/Patch Road Map Chest Glow")]
        public static void PatchRoadMapChestGlow()
        {
            Load();
            string path = $"{PrefabDir}/RoadMap.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var views = root.GetComponentsInChildren<MapChestView>(true);
                if (views.Length == 0) throw new InvalidOperationException("no MapChestView in RoadMap.prefab");
                foreach (MapChestView v in views)
                {
                    Image glow = AddChestGlow((RectTransform)v.transform);
                    Wire(v, ("glow", glow));
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[UIBuilder] Road Map chest glow added.");
        }

        public static void PatchRoadMapChestGlowBatch()
        {
            try
            {
                PatchRoadMapChestGlow();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[UIBuilder] FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }
    }
}
