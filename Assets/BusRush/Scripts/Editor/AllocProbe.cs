using UnityEngine;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Dev tool: measures managed allocations made by game scripts between the earliest Update and the latest
    /// LateUpdate of each frame (excludes rendering and the editor itself), to spot per-frame GC in gameplay loops.
    /// </summary>
    public static class AllocProbe
    {
        private static long frameStart;
        private static long total;
        private static int frames;

        public static double AverageBytes => frames > 0 ? (double)total / frames : 0;

        public static void Reset()
        {
            total = 0;
            frames = 0;
        }

        public static void Install(GameObject host)
        {
            if (host.GetComponent<Begin>() == null) host.AddComponent<Begin>();
            if (host.GetComponent<End>() == null) host.AddComponent<End>();
        }

        [DefaultExecutionOrder(-32000)]
        private sealed class Begin : MonoBehaviour
        {
            private void Update() => frameStart = System.GC.GetAllocatedBytesForCurrentThread();
        }

        [DefaultExecutionOrder(32000)]
        private sealed class End : MonoBehaviour
        {
            private void LateUpdate()
            {
                total += System.GC.GetAllocatedBytesForCurrentThread() - frameStart;
                frames++;
            }
        }
    }
}
