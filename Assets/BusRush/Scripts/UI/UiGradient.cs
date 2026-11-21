using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>
    /// CSS linear-gradient(180deg, top, [middle 50%,] bottom) for a UI graphic: tints every vertex by its height in the
    /// rect (multiplied with the graphic colour). Sliced images have few vertex rows, so the middle stop is approximate.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class UiGradient : BaseMeshEffect
    {
        [SerializeField] private Color top = Color.white;
        [SerializeField] private Color middle = Color.white;
        [SerializeField] private Color bottom = Color.white;
        [SerializeField] private bool useMiddle;

        public void Set(Color topColor, Color bottomColor, Color? middleColor = null)
        {
            top = topColor;
            bottom = bottomColor;
            useMiddle = middleColor.HasValue;
            middle = middleColor ?? Color.Lerp(topColor, bottomColor, 0.5f);
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            Rect r = ((RectTransform)transform).rect;
            if (r.height <= 0f) return;
            var v = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                float t = Mathf.Clamp01((r.yMax - v.position.y) / r.height); // 0 = top, 1 = bottom
                Color c = useMiddle
                    ? (t < 0.5f ? Color.Lerp(top, middle, t * 2f) : Color.Lerp(middle, bottom, t * 2f - 1f))
                    : Color.Lerp(top, bottom, t);
                v.color = (Color)v.color * c;
                vh.SetUIVertex(v, i);
            }
        }
    }
}
