using UnityEngine;

namespace BusRush.Gameplay
{
    /// <summary>
    /// Translucent rounded-cell overlay on the lot, generated as one mesh (one quad per cell, each mapping the
    /// rounded-outline cell texture). Also draws the grid as a gizmo in the Scene view.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class LotGridView : MonoBehaviour
    {
        [SerializeField] private float height = 0.004f;
        [Tooltip("Gap between neighbouring cells, as a fraction of the cell size.")]
        [SerializeField, Range(0f, 0.2f)] private float inset = 0.03f;

        private Mesh mesh;

        private void Awake()
        {
            Build();
        }

        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }

        public void Build()
        {
            int cols = BoardGeometry.Columns, rows = BoardGeometry.Rows;
            float half = BoardGeometry.CellSize * (0.5f - inset * 0.5f);
            var vertices = new Vector3[cols * rows * 4];
            var uv = new Vector2[vertices.Length];
            var normals = new Vector3[vertices.Length];
            var tris = new int[cols * rows * 6];

            int v = 0, t = 0;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    Vector3 p = BoardGeometry.CellToWorld(c, r);
                    p.y = height;
                    vertices[v] = p + new Vector3(-half, 0f, -half);
                    vertices[v + 1] = p + new Vector3(-half, 0f, half);
                    vertices[v + 2] = p + new Vector3(half, 0f, half);
                    vertices[v + 3] = p + new Vector3(half, 0f, -half);
                    uv[v] = new Vector2(0f, 0f);
                    uv[v + 1] = new Vector2(0f, 1f);
                    uv[v + 2] = new Vector2(1f, 1f);
                    uv[v + 3] = new Vector2(1f, 0f);
                    for (int k = 0; k < 4; k++) normals[v + k] = Vector3.up;
                    tris[t++] = v; tris[t++] = v + 1; tris[t++] = v + 2;
                    tris[t++] = v; tris[t++] = v + 2; tris[t++] = v + 3;
                    v += 4;
                }
            }

            if (mesh == null) mesh = new Mesh { name = "LotGrid" };
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.normals = normals;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        private void OnDrawGizmos()
        {
            int cols = BoardGeometry.Columns, rows = BoardGeometry.Rows;
            float cell = BoardGeometry.CellSize;
            Vector3 origin = BoardGeometry.CellToWorld(-0.5f, -0.5f); // top-left corner of the lot
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
            for (int c = 0; c <= cols; c++)
            {
                Vector3 a = origin + new Vector3(c * cell, 0.01f, 0f);
                Gizmos.DrawLine(a, a + new Vector3(0f, 0f, -rows * cell));
            }
            for (int r = 0; r <= rows; r++)
            {
                Vector3 a = origin + new Vector3(0f, 0.01f, -r * cell);
                Gizmos.DrawLine(a, a + new Vector3(cols * cell, 0f, 0f));
            }
        }
    }
}
