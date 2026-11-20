using UnityEngine;

namespace BusRush.Gameplay
{
    /// <summary>
    /// Picks the world for a level (City, Snow, Beach, repeating every 5 levels; see <see cref="LevelPlan.WorldIndex"/>)
    /// and shows it. When the world differs from the previous level's, the old art crossfades to the new one in 0.4 s.
    /// </summary>
    public sealed class WorldManager : MonoBehaviour
    {
        [SerializeField] private WorldTheme[] themes = new WorldTheme[0];
        [SerializeField] private WorldBackground background;
        [SerializeField] private float crossfadeSeconds = 0.4f;

        /// <summary>World shown by the previous Game scene (survives scene loads).</summary>
        private static int lastIndex = -1;

        public WorldTheme Current { get; private set; }

        public static int IndexForLevel(int level, int themeCount) => LevelPlan.WorldIndex(level, themeCount);

        public void Apply(int level)
        {
            if (themes == null || themes.Length == 0 || background == null) return;
            int index = IndexForLevel(level, themes.Length);
            Current = themes[index];

            WorldTheme previous = lastIndex >= 0 && lastIndex < themes.Length && lastIndex != index ? themes[lastIndex] : null;
            lastIndex = index;
            if (Current == null || Current.background == null) return;

            if (previous != null && previous.background != null)
            {
                background.Crossfade(previous.background.texture, Current.background.texture, crossfadeSeconds);
            }
            else
            {
                background.SetTexture(Current.background.texture);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => lastIndex = -1;
    }
}
