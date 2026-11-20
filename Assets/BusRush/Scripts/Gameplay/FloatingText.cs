using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace BusRush.Gameplay
{
    /// <summary>World-space "+N" that rises and fades, facing the camera. Pooled.</summary>
    public sealed class FloatingText : MonoBehaviour
    {
        private static readonly Stack<FloatingText> Pool = new Stack<FloatingText>();

        private TextMeshPro text;
        private Vector3 start;
        private float age;
        private const float Lifetime = 1.1f;
        private const float Rise = 1.2f;

        public static FloatingText Spawn(Vector3 position, string message, TMP_FontAsset font, Material material, Color color, float size = 5f)
        {
            FloatingText ft = null;
            while (Pool.Count > 0 && ft == null) ft = Pool.Pop(); // entries die with their scene
            if (ft == null)
            {
                var go = new GameObject("FloatingText");
                TextMeshPro created = go.AddComponent<TextMeshPro>();
                created.alignment = TextAlignmentOptions.Center;
                created.textWrappingMode = TextWrappingModes.NoWrap;
                created.rectTransform.sizeDelta = new Vector2(4f, 1.5f);
                ft = go.AddComponent<FloatingText>();
                ft.text = created;
            }

            TextMeshPro t = ft.text;
            if (font != null) t.font = font;
            if (material != null) t.fontSharedMaterial = material;
            t.text = message;
            t.fontSize = size;
            t.color = color;
            ft.transform.position = position;
            ft.transform.localScale = Vector3.one;
            if (Camera.main != null) ft.transform.rotation = Camera.main.transform.rotation;
            ft.start = position;
            ft.age = 0f;
            ft.gameObject.SetActive(true);
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
            if (age >= Lifetime)
            {
                gameObject.SetActive(false);
                Pool.Push(this);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Pool.Clear();
    }
}
