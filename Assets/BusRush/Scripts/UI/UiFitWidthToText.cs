using TMPro;
using UnityEngine;

namespace BusRush.UI
{
    /// <summary>CSS "width: auto" for a pill around a label: width = text width + padding (e.g. the coin pill).</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class UiFitWidthToText : MonoBehaviour
    {
        [SerializeField] private TMP_Text text;
        [SerializeField] private float padding = 80f;
        [SerializeField] private float minWidth = 60f;

        private void LateUpdate()
        {
            if (text == null) return;
            var rt = (RectTransform)transform;
            float w = Mathf.Max(minWidth, text.preferredWidth + padding);
            if (!Mathf.Approximately(rt.sizeDelta.x, w)) rt.sizeDelta = new Vector2(w, rt.sizeDelta.y);
        }
    }
}
