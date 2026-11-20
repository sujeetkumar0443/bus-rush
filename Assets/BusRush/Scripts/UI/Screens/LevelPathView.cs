using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>
    /// Zig-zag path of level circles on Home: the last completed level (with its best stars), the current level
    /// (highlighted, pulsing) and the next 4 levels (grey, locked).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class LevelPathView : MonoBehaviour
    {
        [SerializeField] private RectTransform nodeTemplate;
        [SerializeField] private RectTransform dotTemplate;
        [SerializeField, Min(1)] private int upcomingLevels = 4;
        [SerializeField] private float[] zigzag = { 0f, -0.2f, 0.16f, -0.18f, 0.14f, -0.12f };
        [SerializeField] private Color starOn = Color.white;
        [SerializeField] private Color starOff = new Color(0.12f, 0.16f, 0.27f, 0.3f);
        [SerializeField] private float currentScale = 1.3f;

        [SerializeField] private Color doneFace = new Color32(0x3C, 0xC3, 0x5B, 0xFF);
        [SerializeField] private Color doneLip = new Color32(0x23, 0x86, 0x3D, 0xFF);
        [SerializeField] private Color currentFace = new Color32(0xFF, 0xC9, 0x3C, 0xFF);
        [SerializeField] private Color currentLip = new Color32(0xD6, 0x9A, 0x12, 0xFF);
        [SerializeField] private Color futureFace = new Color32(0xDD, 0xE3, 0xEE, 0xFF);
        [SerializeField] private Color futureLip = new Color32(0xB7, 0xC0, 0xD3, 0xFF);

        private readonly List<GameObject> spawned = new List<GameObject>();
        private bool dirty = true;
        private Coroutine pulse;

        private void OnEnable()
        {
            dirty = true;
        }

        private void OnRectTransformDimensionsChange()
        {
            dirty = true;
        }

        private void LateUpdate()
        {
            if (dirty) Build();
        }

        public void Build()
        {
            dirty = false;
            foreach (GameObject go in spawned) Destroy(go);
            spawned.Clear();
            Tween.Stop(this, ref pulse);

            var area = (RectTransform)transform;
            Rect r = area.rect;
            if (r.height <= 1f) return;

            int current = Mathf.Max(1, SaveSystem.Data.level);
            int first = Mathf.Max(1, current - 1);
            int visibleNodes = current - first + 1 + upcomingLevels;
            float nodeSize = nodeTemplate.rect.height;
            float bottom = r.yMin + nodeSize * currentScale * 0.6f;
            float top = r.yMax - nodeSize * 0.6f;

            var positions = new Vector2[visibleNodes];
            for (int i = 0; i < visibleNodes; i++)
            {
                float y = Mathf.Lerp(bottom, top, i / (float)(visibleNodes - 1));
                float x = zigzag[i % zigzag.Length] * r.width;
                positions[i] = new Vector2(x, y);
            }

            for (int i = 0; i < visibleNodes - 1; i++)
            {
                for (int d = 1; d <= 3; d++)
                {
                    RectTransform dot = Instantiate(dotTemplate, area);
                    dot.gameObject.SetActive(true);
                    dot.anchoredPosition = Vector2.Lerp(positions[i], positions[i + 1], d / 4f);
                    spawned.Add(dot.gameObject);
                }
            }

            for (int i = visibleNodes - 1; i >= 0; i--)
            {
                int level = first + i;
                RectTransform node = Instantiate(nodeTemplate, area);
                node.gameObject.SetActive(true);
                node.name = "Level_" + level;
                node.anchoredPosition = positions[i];
                spawned.Add(node.gameObject);

                bool isCurrent = level == current;
                bool done = level < current;
                node.Find("Lip").GetComponent<Image>().color = isCurrent ? currentLip : done ? doneLip : futureLip;
                node.Find("Face").GetComponent<Image>().color = isCurrent ? currentFace : done ? doneFace : futureFace;
                node.Find("Number").GetComponent<TMP_Text>().text = level.ToString();
                node.Find("Ring").gameObject.SetActive(isCurrent);
                node.Find("Lock").gameObject.SetActive(!done && !isCurrent);
                Transform starRow = node.Find("Stars");
                starRow.gameObject.SetActive(done);
                if (done)
                {
                    int best = SaveSystem.Data.GetStars(level);
                    for (int k = 0; k < starRow.childCount; k++)
                    {
                        starRow.GetChild(k).GetComponent<Image>().color = k < best ? starOn : starOff;
                    }
                }

                if (isCurrent)
                {
                    node.localScale = Vector3.one * currentScale;
                    node.SetAsLastSibling();
                    pulse = Tween.Pulse(this, node, 0.06f, 1.1f);
                }
            }
        }
    }
}
