using UnityEngine;

namespace BusRush.Gameplay
{
    /// <summary>
    /// Ground quad showing the world art. The art is mapped to exactly the reference camera view
    /// (<see cref="BoardGeometry.ArtGroundRect"/>), so the painted layout lines up with the lot. The quad itself is
    /// grown to cover whatever the camera sees; beyond the art the texture is mirrored (texture wrap = MirrorOnce),
    /// so tall or wide screens never show empty edges.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class WorldBackground : MonoBehaviour
    {
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private CameraFitter fitter;
        [SerializeField] private float margin = 1f;
        [SerializeField] private float height = -0.02f;
        [Tooltip("Transparent copy on top, used to fade the previous world out when the world changes.")]
        [SerializeField] private MeshRenderer overlay;

        private Mesh mesh;
        private MaterialPropertyBlock block;
        private MaterialPropertyBlock overlayBlock;
        private Coroutine fade;

        private void Awake()
        {
            mesh = new Mesh { name = "WorldBackground" };
            mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            if (overlay != null)
            {
                overlay.GetComponent<MeshFilter>().sharedMesh = mesh;
                overlay.enabled = false;
            }
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            transform.localScale = Vector3.one;
            Rebuild();
        }

        private void OnEnable()
        {
            if (fitter != null) fitter.Fitted += OnFitted;
        }

        private void OnDisable()
        {
            if (fitter != null) fitter.Fitted -= OnFitted;
        }

        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }

        public void SetTexture(Texture texture)
        {
            block ??= new MaterialPropertyBlock();
            MeshRenderer r = GetComponent<MeshRenderer>();
            r.GetPropertyBlock(block);
            block.SetTexture(BaseMapId, texture);
            r.SetPropertyBlock(block);
        }

        /// <summary>Shows <paramref name="to"/> and fades <paramref name="from"/> out on top of it.</summary>
        public void Crossfade(Texture from, Texture to, float seconds)
        {
            SetTexture(to);
            if (overlay == null || seconds <= 0f) return;
            if (fade != null) StopCoroutine(fade);
            fade = StartCoroutine(FadeOut(from, seconds));
        }

        private System.Collections.IEnumerator FadeOut(Texture from, float seconds)
        {
            overlayBlock ??= new MaterialPropertyBlock();
            overlay.enabled = true;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                overlay.GetPropertyBlock(overlayBlock);
                overlayBlock.SetTexture(BaseMapId, from);
                overlayBlock.SetColor(BaseColorId, new Color(1f, 1f, 1f, 1f - t / seconds));
                overlay.SetPropertyBlock(overlayBlock);
                yield return null;
            }
            overlay.enabled = false;
            fade = null;
        }

        private void OnFitted(CameraFitter _) => Rebuild();

        /// <summary>Reference-px rectangle the current art covers (default: exactly the 390x844 reference view).</summary>
        private Rect refCoverage = new Rect(0f, 0f, ScreenRef.Width, ScreenRef.Height);

        /// <summary>
        /// Sets which reference-px rectangle the art covers, so art pixel (x, y) lands on ScreenRef.RefToWorld(x, y).
        /// </summary>
        public void SetCoverage(Rect refRect)
        {
            if (refRect.width <= 0f || refRect.height <= 0f) return;
            refCoverage = refRect;
            Rebuild();
        }

        /// <summary>Ground rectangle (world X/Z) of the art: corners mapped through ScreenRef.</summary>
        public Rect ArtGroundRect()
        {
            Vector3 topLeft = ScreenRef.RefToWorld(refCoverage.xMin, refCoverage.yMin);
            Vector3 bottomRight = ScreenRef.RefToWorld(refCoverage.xMax, refCoverage.yMax);
            return Rect.MinMaxRect(topLeft.x, bottomRight.z, bottomRight.x, topLeft.z);
        }

        public void Rebuild()
        {
            if (mesh == null) return;

            Rect art = ArtGroundRect();
            Rect quad = art;
            if (fitter != null)
            {
                Rect view = fitter.VisibleGroundRect();
                quad = Rect.MinMaxRect(
                    Mathf.Min(art.xMin, view.xMin - margin), Mathf.Min(art.yMin, view.yMin - margin),
                    Mathf.Max(art.xMax, view.xMax + margin), Mathf.Max(art.yMax, view.yMax + margin));
            }

            Vector2 Uv(float x, float z) => new Vector2((x - art.xMin) / art.width, (z - art.yMin) / art.height);

            mesh.Clear();
            mesh.vertices = new[]
            {
                new Vector3(quad.xMin, height, quad.yMin), new Vector3(quad.xMin, height, quad.yMax),
                new Vector3(quad.xMax, height, quad.yMax), new Vector3(quad.xMax, height, quad.yMin)
            };
            mesh.uv = new[] { Uv(quad.xMin, quad.yMin), Uv(quad.xMin, quad.yMax), Uv(quad.xMax, quad.yMax), Uv(quad.xMax, quad.yMin) };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
        }
    }
}
