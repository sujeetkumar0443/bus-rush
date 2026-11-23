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
        public GradientColors purpleButton = new GradientColors(Hex(0xD7A6FF), Hex(0xB266FF), Hex(0x8A3FE0), Hex(0x4A1690));
        public GradientColors whiteButton = new GradientColors(Hex(0xFFFFFF), Hex(0xEEF6FF), Hex(0xDDF1FF), Hex(0x1B4FA0));

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

        // ------------------------------------------------------------------ text style (one place for every screen)

        [Header("Text style: BUTTON text (white face, no outline, soft drop shadow in a darker shade of the button)")]
        public float buttonUnderlayOffsetX = 0f;
        public float buttonUnderlayOffsetY = -0.6f;
        [Range(0f, 1f)] public float buttonUnderlaySoftness = 0.1f;
        [Range(-1f, 1f)] public float buttonUnderlayDilate = 0f;
        [Range(0f, 1f)] public float buttonUnderlayAlpha = 0.65f;

        [Header("Text style: TITLE text (white face, thin outline + small shadow in a darker shade of the header)")]
        [Range(0f, 1f)] public float titleOutlineWidth = 0.12f;
        public float titleUnderlayOffsetY = -0.5f;
        [Range(0f, 1f)] public float titleUnderlaySoftness = 0.1f;
        [Range(-1f, 1f)] public float titleUnderlayDilate = 0f;
        [Range(0f, 1f)] public float titleUnderlayAlpha = 0.5f;

        [Header("Text style: size (button + title text)")]
        [Tooltip("Button and title text is this much larger than before (auto-size still shrinks it to fit its rect).")]
        public float textSizeScale = 1.08f;
        [Tooltip("The scale already baked into the prefabs/scenes by Tools > Bus Rush > Apply UI Text Style. Do not edit.")]
        public float textSizeScaleApplied = 1f;

        /// <summary>The darker shade used for the text shadow/outline of a button of this colour: its bottom lip.</summary>
        public static Color ShadeOf(GradientColors button) => new Color(button.edge.r, button.edge.g, button.edge.b, 1f);

        /// <summary>Button label: white face, outline off, underlay (drop shadow) at buttonUnderlay* in <paramref name="shade"/>.</summary>
        public void ApplyButtonText(Material m, Color shade)
        {
            SetCommon(m);
            SetOutline(m, 0f, shade);
            SetUnderlay(m, true, buttonUnderlayOffsetX, buttonUnderlayOffsetY, buttonUnderlaySoftness, buttonUnderlayDilate,
                        new Color(shade.r, shade.g, shade.b, buttonUnderlayAlpha));
        }

        /// <summary>Popup/ribbon title: white face, thin outline in <paramref name="shade"/> (never black) plus a small shadow.</summary>
        public void ApplyTitleText(Material m, Color shade)
        {
            SetCommon(m);
            SetOutline(m, titleOutlineWidth, shade);
            SetUnderlay(m, true, 0f, titleUnderlayOffsetY, titleUnderlaySoftness, titleUnderlayDilate,
                        new Color(shade.r, shade.g, shade.b, titleUnderlayAlpha));
        }

        /// <summary>Dark text on light panels: no outline, no underlay. The (unused) outline/underlay colours are navy, never black.</summary>
        public void ApplyPlainText(Material m)
        {
            SetCommon(m);
            SetOutline(m, 0f, navy);
            SetUnderlay(m, false, 0f, 0f, 0f, 0f, new Color(navy.r, navy.g, navy.b, 0f));
        }

        private void SetCommon(Material m)
        {
            if (m.HasProperty(ShaderUtilities.ID_FaceColor)) m.SetColor(ShaderUtilities.ID_FaceColor, white);
            if (m.HasProperty(ShaderUtilities.ID_OutlineSoftness)) m.SetFloat(ShaderUtilities.ID_OutlineSoftness, 0f);
        }

        private static void SetOutline(Material m, float width, Color color)
        {
            if (width > 0f) m.EnableKeyword(ShaderUtilities.Keyword_Outline);
            else m.DisableKeyword(ShaderUtilities.Keyword_Outline);
            m.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
            m.SetColor(ShaderUtilities.ID_OutlineColor, new Color(color.r, color.g, color.b, 1f));
        }

        private static void SetUnderlay(Material m, bool on, float x, float y, float softness, float dilate, Color color)
        {
            if (on) m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            else m.DisableKeyword(ShaderUtilities.Keyword_Underlay);
            m.SetColor(ShaderUtilities.ID_UnderlayColor, color);
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, x);
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, y);
            m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, softness);
            m.SetFloat(ShaderUtilities.ID_UnderlayDilate, dilate);
        }

        public static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
