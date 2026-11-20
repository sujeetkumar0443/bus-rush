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

        [SerializeField] private CameraFitter fitter;
        [SerializeField] private float margin = 1f;
        [SerializeField] private float height = -0.02f;

        private Mesh mesh;
        private MaterialPropertyBlock block;

        private void Awake()
        {
            mesh = new Mesh { name = "WorldBackground" };
            mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = mesh;
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

        private void OnFitted(CameraFitter _) => Rebuild();

        public void Rebuild()
        {
            if (mesh == null) return;

            Rect art = BoardGeometry.ArtGroundRect;
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
