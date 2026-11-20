using UnityEngine;

namespace BusRush
{
    /// <summary>A world (background art) that levels rotate through.</summary>
    [CreateAssetMenu(fileName = "WorldTheme", menuName = "Bus Rush/World Theme")]
    public sealed class WorldTheme : ScriptableObject
    {
        public string themeName = "City";
        [Tooltip("Background rendered for the 390x844 reference camera.")]
        public Sprite background;
    }
}
