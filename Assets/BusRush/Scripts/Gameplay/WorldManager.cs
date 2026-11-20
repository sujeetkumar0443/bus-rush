using UnityEngine;

namespace BusRush.Gameplay
{
    /// <summary>Picks the world theme for a level ((level - 1) % themeCount) and shows it on the background.</summary>
    public sealed class WorldManager : MonoBehaviour
    {
        [SerializeField] private WorldTheme[] themes = new WorldTheme[0];
        [SerializeField] private WorldBackground background;

        public WorldTheme Current { get; private set; }

        public static int IndexForLevel(int level, int themeCount)
        {
            if (themeCount <= 0) return 0;
            int i = (Mathf.Max(1, level) - 1) % themeCount;
            return i < 0 ? i + themeCount : i;
        }

        public void Apply(int level)
        {
            if (themes == null || themes.Length == 0) return;
            Current = themes[IndexForLevel(level, themes.Length)];
            if (Current != null && Current.background != null && background != null)
            {
                background.SetTexture(Current.background.texture);
            }
        }
    }
}
