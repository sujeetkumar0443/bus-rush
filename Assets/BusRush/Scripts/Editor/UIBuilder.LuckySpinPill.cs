using System;
using BusRush.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static BusRush.EditorTools.UIKit;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Lucky Spin popup: a small light rounded pill (white, 85 % alpha) behind "N spins left today" so the navy text is
    /// readable on the dark blurred background. <see cref="AddSpinsLeftPill"/> is used by the full Daily/Lucky Spin build
    /// and by the in-place patch (Tools > Bus Rush > Patch Lucky Spin Pill), which only touches LuckySpinPopup.prefab.
    /// </summary>
    public static partial class UIBuilder
    {
        private const float SpinsPillW = 220f, SpinsPillH = 30f;
        private static readonly Color SpinsPillColor = new Color(1f, 1f, 1f, 0.85f);
        private static readonly Color SpinsTextColor = new Color(0x1B / 255f, 0x2A / 255f, 0x5C / 255f, 1f);

        /// <summary>Creates (or reuses) the pill as the sibling just below <paramref name="text"/> in draw order.</summary>
        private static Image AddSpinsLeftPill(RectTransform text)
        {
            Transform parent = text.parent;
            Transform existing = parent.Find("SpinsLeftPill");
            Image pill = existing != null ? existing.GetComponent<Image>() : Img(parent, "SpinsLeftPill", Round, SpinsPillColor);
            pill.color = SpinsPillColor;
            pill.raycastTarget = false;
            MakePill(pill, SpinsPillH);
            RectTransform rt = pill.rectTransform;
            rt.anchorMin = text.anchorMin;
            rt.anchorMax = text.anchorMax;
            rt.pivot = text.pivot;
            rt.sizeDelta = new Vector2(SpinsPillW, SpinsPillH);
            rt.anchoredPosition = text.anchoredPosition;
            rt.SetSiblingIndex(text.GetSiblingIndex()); // the text moves one place up, so it draws over the pill
            return pill;
        }

        [MenuItem("Tools/Bus Rush/Patch Lucky Spin Pill")]
        public static void PatchLuckySpinPill()
        {
            Load();
            string path = $"{PrefabDir}/LuckySpinPopup.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                TMP_Text spins = null;
                foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true))
                    if (t.name == "SpinsLeft") spins = t;
                if (spins == null) throw new InvalidOperationException("SpinsLeft not found in LuckySpinPopup.prefab");
                AddSpinsLeftPill((RectTransform)spins.transform);
                spins.color = SpinsTextColor;
                spins.fontSize = 18f; // Apply UI Text Style then sets the font (Nunito-ExtraBold) and auto-size
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[UIBuilder] Lucky Spin pill added behind SpinsLeft.");
        }

        public static void PatchLuckySpinPillBatch()
        {
            try
            {
                PatchLuckySpinPill();
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
