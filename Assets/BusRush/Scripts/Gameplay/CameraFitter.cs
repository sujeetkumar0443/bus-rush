using UnityEngine;

namespace BusRush.Gameplay
{
    /// <summary>
    /// Orthographic board camera. Fits the reference view width (so the lot keeps the same horizontal screen
    /// fractions on every aspect) and keeps the lot centre at the same vertical screen fraction. Taller screens
    /// see more ground above/below; <see cref="WorldBackground"/> extends the art to cover it.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFitter : MonoBehaviour
    {
        [SerializeField] private float distance = 40f;

        private Camera cam;
        private int lastWidth;
        private int lastHeight;
        private float lastAspect = -1f;

        /// <summary>Raised after the camera was refitted (background listens to cover the new view).</summary>
        public event System.Action<CameraFitter> Fitted;

        public Camera Camera => cam != null ? cam : cam = GetComponent<Camera>();

        private void Start()
        {
            Fit();
        }

        private void LateUpdate()
        {
            if (Screen.width != lastWidth || Screen.height != lastHeight || !Mathf.Approximately(Camera.aspect, lastAspect))
            {
                Fit();
            }
        }

        public void Fit()
        {
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            Fit(Camera.aspect);
        }

        /// <summary>Fits for an explicit aspect ratio (width / height). Usable from editor tools.</summary>
        public void Fit(float aspect)
        {
            lastAspect = aspect;
            Camera c = Camera;
            c.orthographic = true;
            transform.rotation = Quaternion.Euler(BoardGeometry.CameraPitch, 0f, 0f);

            // Fit by width. On screens wider than the reference (e.g. 9:16) keep the reference height instead, so the
            // lot does not grow under the HUD; the background mirrors its edges to cover the extra width.
            float referenceHeight = BoardGeometry.ViewWidth * BoardGeometry.ReferenceHeight / BoardGeometry.ReferenceWidth;
            float viewHeight = Mathf.Max(BoardGeometry.ViewWidth / Mathf.Max(0.01f, aspect), referenceHeight);
            float viewWidth = viewHeight * aspect;
            c.orthographicSize = viewHeight * 0.5f;

            // Ground point that must sit at the screen centre so the lot centre lands on its viewport fraction.
            Vector2 lotVp = BoardGeometry.LotCenterViewport;
            float screenOffsetX = (lotVp.x - 0.5f) * viewWidth;   // lot centre relative to screen centre, screen units
            float screenOffsetY = (lotVp.y - 0.5f) * viewHeight;
            var target = new Vector3(-screenOffsetX, 0f, -screenOffsetY / BoardGeometry.SinPitch);
            transform.position = target - transform.forward * distance;

            Fitted?.Invoke(this);
        }

        /// <summary>Ground rectangle (world XZ) currently visible, assuming the ground plane y = 0.</summary>
        public Rect VisibleGroundRect()
        {
            Camera c = Camera;
            float halfH = c.orthographicSize;
            float halfW = halfH * c.aspect;
            Vector3 center = GroundPointAtScreenCenter();
            float halfDepth = halfH / BoardGeometry.SinPitch;
            return new Rect(center.x - halfW, center.z - halfDepth, halfW * 2f, halfDepth * 2f);
        }

        private Vector3 GroundPointAtScreenCenter()
        {
            Vector3 p = transform.position;
            Vector3 f = transform.forward;
            float t = -p.y / f.y;
            return p + f * t;
        }
    }
}
