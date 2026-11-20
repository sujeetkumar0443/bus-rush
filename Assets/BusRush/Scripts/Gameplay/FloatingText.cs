using TMPro;
using UnityEngine;

namespace BusRush.Gameplay
{
    /// <summary>World-space "+N" that rises and fades, facing the camera.</summary>
    public sealed class FloatingText : MonoBehaviour
    {
        private TextMeshPro text;
        private Vector3 start;
        private float age;
        private const float Lifetime = 1.1f;
        private const float Rise = 1.2f;

        public static FloatingText Spawn(Vector3 position, string message, TMP_FontAsset font, Material material, Color color, float size = 5f)
        {
            var go = new GameObject("FloatingText");
            go.transform.position = position;
            if (Camera.main != null) go.transform.rotation = Camera.main.transform.rotation;
            var t = go.AddComponent<TextMeshPro>();
            if (font != null) t.font = font;
            if (material != null) t.fontSharedMaterial = material;
            t.text = message;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.sizeDelta = new Vector2(4f, 1.5f);
            var ft = go.AddComponent<FloatingText>();
            ft.text = t;
            ft.start = position;
            return ft;
        }

        private void Update()
        {
            age += Time.deltaTime;
            float k = age / Lifetime;
            transform.position = start + Vector3.up * (Rise * (1f - (1f - k) * (1f - k)));
            float scale = k < 0.15f ? Mathf.Lerp(0.6f, 1.15f, k / 0.15f) : Mathf.Lerp(1.15f, 1f, (k - 0.15f) / 0.85f);
            transform.localScale = Vector3.one * scale;
            Color c = text.color;
            c.a = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
            text.color = c;
            if (age >= Lifetime) Destroy(gameObject);
        }
    }
}
