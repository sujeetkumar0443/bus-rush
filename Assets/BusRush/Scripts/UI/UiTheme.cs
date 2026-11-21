using System;
using TMPro;
using UnityEngine;

namespace BusRush.UI
{
    /// <summary>
    /// UI v2 look (BusRush_UI_v2 README and Design/*.dc.html): colours, button gradients and the two fonts.
    /// One asset: Assets/BusRush/Art/UI_v2/UiTheme.asset (created by Tools > Bus Rush > Import UI v2 Art).
    /// </summary>
    [CreateAssetMenu(menuName = "Bus Rush/UI Theme", fileName = "UiTheme")]
    public sealed class UiTheme : ScriptableObject
    {
        /// <summary>A vertical gradient (top, middle, bottom) with its dark edge / bottom lip colour.</summary>
        [Serializable]
        public struct GradientColors
        {
            public Color top;
            public Color middle;
            public Color bottom;
            public Color edge;

            public GradientColors(Color top, Color middle, Color bottom, Color edge)
            {
                this.top = top;
                this.middle = middle;
                this.bottom = bottom;
                this.edge = edge;
            }
        }

        [Header("Navy")]
        public Color navy = Hex(0x1B2A6B);
        public Color navyDeep = Hex(0x103E86);

        [Header("Ribbon / title bar")]
        public GradientColors ribbon = new GradientColors(Hex(0x7FD3FF), Hex(0x57ACF3), Hex(0x2F86E8), Hex(0x103E86));

        [Header("Buttons")]
        public GradientColors greenButton = new GradientColors(Hex(0xA4F560), Hex(0x56D134), Hex(0x36A920), Hex(0x1D5F12));
        public GradientColors orangeButton = new GradientColors(Hex(0xFFD36B), Hex(0xFFA22E), Hex(0xF07F12), Hex(0x8A3E05));
        public GradientColors blueButton = new GradientColors(Hex(0x7FD3FF), Hex(0x3A9BF5), Hex(0x1F74D6), Hex(0x103E86));
        public GradientColors redButton = new GradientColors(Hex(0xFF8A8A), Hex(0xE85A5E), Hex(0xD12A33), Hex(0x7A1016));

        [Header("Panels")]
        public Color panelTop = Hex(0xFFFFFF);
        public Color panelBottom = Hex(0xE3EFFF);
        public Color panelBorder = Hex(0x1B4FA0);

        [Header("Text")]
        public Color gold = Hex(0xFFC531);
        public Color white = Color.white;

        [Header("Fonts")]
        [Tooltip("Lilita One: titles, numbers, buttons.")]
        public TMP_FontAsset headingFont;
        [Tooltip("Lilita One with a dark outline and drop shadow.")]
        public Material headingOutline;
        [Tooltip("Fredoka: body text.")]
        public TMP_FontAsset bodyFont;

        public static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
