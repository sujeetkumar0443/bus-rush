using UnityEngine;

namespace BusRush.Gameplay
{
    /// <summary>
    /// Keeps a world-space area (lot + road + bays, standing on the ground) fully visible on any aspect ratio.
    /// Perspective cameras get their vertical FOV adjusted, orthographic ones their size.
    /// Camera position and rotation are left as authored.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFitter : MonoBehaviour
    {
        [Tooltip("Centre of the area on the ground that must stay visible (world space).")]
        [SerializeField] private Vector3 areaCenter = new Vector3(0f, 0f, 6.5f);

        [Tooltip("Size of the area (world space): X and Z on the ground, Y = height of the tallest object.")]
        [SerializeField] private Vector3 areaSize = new Vector3(12f, 1.2f, 24f);

        [Tooltip("Extra margin as a fraction of the screen (per side).")]
        [SerializeField, Range(0f, 0.3f)] private float padding = 0.02f;

        [SerializeField, Range(10f, 100f)] private float minFov = 20f;
        [SerializeField, Range(10f, 100f)] private float maxFov = 75f;

        private Camera cam;
        private int lastWidth;
        private int lastHeight;

        public Vector3 AreaCenter
        {
            get => areaCenter;
            set => areaCenter = value;
        }

        public Vector3 AreaSize
        {
            get => areaSize;
            set => areaSize = value;
        }

        private Camera Cam => cam != null ? cam : cam = GetComponent<Camera>();

        private void Start()
        {
            Fit();
        }

        private void LateUpdate()
        {
            if (Screen.width != lastWidth || Screen.height != lastHeight)
            {
                Fit();
            }
        }

        /// <summary>Fits for the current screen.</summary>
        public void Fit()
        {
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            Fit(Cam.aspect);
        }

        /// <summary>Fits for an explicit aspect ratio (width / height). Usable from editor tools.</summary>
        public void Fit(float aspect)
        {
            if (Cam.orthographic)
            {
                Cam.orthographicSize = GetRequiredOrthographicSize(aspect);
            }
            else
            {
                Cam.fieldOfView = Mathf.Clamp(GetRequiredFieldOfView(aspect), minFov, maxFov);
            }
        }

        /// <summary>Unclamped vertical FOV (degrees) needed to see the whole area at this aspect.</summary>
        public float GetRequiredFieldOfView(float aspect)
        {
            float maxTanX = 0f;
            float maxTanY = 0f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = transform.InverseTransformPoint(GetCorner(i));
                if (p.z > 0.01f)
                {
                    maxTanX = Mathf.Max(maxTanX, Mathf.Abs(p.x) / p.z);
                    maxTanY = Mathf.Max(maxTanY, Mathf.Abs(p.y) / p.z);
                }
            }

            float tanHalf = Mathf.Max(maxTanY, maxTanX / aspect) * (1f + padding * 2f);
            return 2f * Mathf.Atan(tanHalf) * Mathf.Rad2Deg;
        }

        public float GetRequiredOrthographicSize(float aspect)
        {
            float maxX = 0f;
            float maxY = 0f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = transform.InverseTransformPoint(GetCorner(i));
                maxX = Mathf.Max(maxX, Mathf.Abs(p.x));
                maxY = Mathf.Max(maxY, Mathf.Abs(p.y));
            }
            return Mathf.Max(maxY, maxX / aspect) * (1f + padding * 2f);
        }

        /// <summary>One of the 8 corners of the area. It stands on the ground (areaCenter.y) and extends up by areaSize.y.</summary>
        public Vector3 GetCorner(int index)
        {
            return areaCenter + new Vector3(
                (index & 1) == 0 ? -areaSize.x * 0.5f : areaSize.x * 0.5f,
                (index & 2) == 0 ? 0f : areaSize.y,
                (index & 4) == 0 ? -areaSize.z * 0.5f : areaSize.z * 0.5f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.9f);
            Gizmos.DrawWireCube(areaCenter + 0.5f * areaSize.y * Vector3.up, areaSize);
        }
    }
}
