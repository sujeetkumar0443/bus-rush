using UnityEngine;

namespace BusRush.UI
{
    /// <summary>
    /// Marks temporary debug UI. Destroyed automatically in non-development builds.
    /// Remove these objects once the real flow can reach the screens they preview.
    /// </summary>
    public sealed class DebugOnly : MonoBehaviour
    {
        private void Awake()
        {
            if (!Debug.isDebugBuild)
            {
                Destroy(gameObject);
            }
        }
    }
}
