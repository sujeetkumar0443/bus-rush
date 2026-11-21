using UnityEngine;

namespace BusRush
{
    /// <summary>A world (background art) that levels rotate through.</summary>
    [CreateAssetMenu(fileName = "WorldTheme", menuName = "Bus Rush/World Theme")]
    public sealed class WorldTheme : ScriptableObject
    {
        public string themeName = "City";

        [Tooltip("Background rendered for the gameplay camera (ortho, pitch 58).")]
        public Sprite background;

        [Tooltip("Reference-px rectangle (390x844 space, origin top-left, y down) the art covers: x, y, width, height. " +
                 "City v2: (-60, -150, 510, 1144). The original worlds: (0, 0, 390, 844).")]
        public Rect refCoverage = new Rect(0f, 0f, 390f, 844f);

        [Tooltip("Mirror the art beyond its edges when the camera sees more than it covers (old worlds). " +
                 "Off for art with built-in margins (City v2).")]
        public bool mirrorEdges = true;
    }
}
