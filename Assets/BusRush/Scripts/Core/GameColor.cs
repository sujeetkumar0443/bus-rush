using UnityEngine;

namespace BusRush
{
    /// <summary>The colours a vehicle / passenger can have. Order matches GameConfig.palette.</summary>
    public enum GameColor
    {
        Red,
        Blue,
        Yellow,
        Green,
        Purple,
        Pink,
        Cyan
    }

    /// <summary>Colour / material lookups for <see cref="GameColor"/>.</summary>
    public static class GameColors
    {
        public const int Count = 7;

        // Fallback values, used only if GameConfig is missing an entry.
        private static readonly Color[] Fallback =
        {
            Hex(0xE8433F), // Red
            Hex(0x3B7BE8), // Blue
            Hex(0xF5C518), // Yellow
            Hex(0x3DBE5A), // Green
            Hex(0x8E5AD8), // Purple
            Hex(0xF26BAE), // Pink
            Hex(0x2FC4D8)  // Cyan
        };

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static MaterialPropertyBlock propertyBlock;

        public static Color GetColor(GameColor color)
        {
            GameConfig config = GameConfig.Instance;
            if (config != null && config.TryGetPaletteEntry(color, out GameConfig.PaletteEntry entry))
            {
                return entry.color;
            }
            return Fallback[(int)color];
        }

        /// <summary>Darker shade of the colour, used for passenger caps.</summary>
        public static Color GetCapColor(GameColor color)
        {
            Color.RGBToHSV(GetColor(color), out float h, out float s, out float v);
            return Color.HSVToRGB(h, Mathf.Clamp01(s * 1.05f), v * 0.72f);
        }

        public static Material GetMaterial(GameColor color)
        {
            GameConfig config = GameConfig.Instance;
            if (config != null && config.TryGetPaletteEntry(color, out GameConfig.PaletteEntry entry))
            {
                return entry.material;
            }
            return null;
        }

        /// <summary>
        /// Replaces every palette material on the given renderers with the material of <paramref name="color"/>.
        /// Non-palette materials (glass, tyres, ...) are left untouched.
        /// </summary>
        public static void Recolor(Renderer[] renderers, GameColor color)
        {
            GameConfig config = GameConfig.Instance;
            Material target = GetMaterial(color);
            if (config == null || target == null)
            {
                return;
            }

            foreach (Renderer r in renderers)
            {
                Material[] mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != target && config.IsPaletteMaterial(mats[i]))
                    {
                        mats[i] = target;
                        changed = true;
                    }
                }
                if (changed)
                {
                    r.sharedMaterials = mats;
                }
            }
        }

        /// <summary>
        /// Passenger body colour: the palette colour, with pink and blue lifted a little so small figures read as bright
        /// as the vehicles of the same colour.
        /// </summary>
        public static Color GetPassengerBodyColor(GameColor color)
        {
            Color c = GetColor(color);
            if (color != GameColor.Pink && color != GameColor.Blue) return c;
            Color.RGBToHSV(c, out float h, out float s, out float v);
            return Color.HSVToRGB(h, s * 0.9f, Mathf.Min(1f, v * 1.12f));
        }

        public static bool TryGetPassengerMaterials(GameColor color, out Material body, out Material cap)
        {
            GameConfig config = GameConfig.Instance;
            if (config != null && config.TryGetPaletteEntry(color, out GameConfig.PaletteEntry e) && e.passengerBody != null)
            {
                body = e.passengerBody;
                cap = e.passengerCap != null ? e.passengerCap : e.passengerBody;
                return true;
            }
            body = cap = null;
            return false;
        }

        /// <summary>Tints a renderer's base colour without creating a material instance.</summary>
        public static void SetTint(Renderer renderer, Color color)
        {
            propertyBlock ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(propertyBlock);
        }

        public static Color Hex(uint rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }
    }
}
