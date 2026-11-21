using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>CSS skewX for a UI graphic: shears its mesh horizontally around the rect centre.</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class UiSkew : BaseMeshEffect
    {
        [Tooltip("CSS skewX angle in degrees (negative leans the top to the right).")]
        [SerializeField] private float skewXDegrees = -20f;

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            // CSS y points down: x' = x + tan(a) * yCss = x - tan(a) * yUnity.
            float k = -Mathf.Tan(skewXDegrees * Mathf.Deg2Rad);
            float cy = ((RectTransform)transform).rect.center.y;
            var v = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                v.position.x += k * (v.position.y - cy);
                vh.SetUIVertex(v, i);
            }
        }
    }
}
