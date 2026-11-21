using System.Collections.Generic;
using BusRush.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>
    /// HUD tags over the locked bays (lock icon + coin price) that follow each locked bay on screen; a tap buys a bay
    /// through <see cref="GameSession.RequestBayUnlock"/>. The bays' own world labels are hidden while this runs.
    /// </summary>
    public sealed class BayLockOverlay : MonoBehaviour
    {
        [SerializeField] private Button tagTemplate;
        [Tooltip("Screen offset from the bay centre (UI units, y up).")]
        [SerializeField] private Vector2 offset = Vector2.zero;

        private readonly Dictionary<BayState, Button> tags = new Dictionary<BayState, Button>();
        private readonly List<BayState> drop = new List<BayState>();
        private Camera cam;

        private void LateUpdate()
        {
            GameSession session = GameSession.Current;
            if (session == null || session.Bays == null) return;
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            var area = (RectTransform)transform;
            int cost = GameConfig.Instance != null ? GameConfig.Instance.extraBayCost : 200;

            foreach (BayState b in session.Bays.Bays)
            {
                if (b.Kind != BayKind.Locked || b.View == null) continue;
                if (!tags.TryGetValue(b, out Button tag))
                {
                    b.View.HideLockLabel();
                    tag = Instantiate(tagTemplate, area);
                    tag.gameObject.SetActive(true);
                    tag.onClick.AddListener(() =>
                    {
                        if (GameSession.Current != null) GameSession.Current.RequestBayUnlock();
                    });
                    TMP_Text price = tag.GetComponentInChildren<TMP_Text>(true);
                    if (price != null) price.text = cost.ToString();
                    tags[b] = tag;
                }
                Vector3 screen = cam.WorldToScreenPoint(b.View.transform.position);
                Canvas canvas = area.GetComponentInParent<Canvas>().rootCanvas;
                Camera uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, uiCam, out Vector2 local))
                {
                    ((RectTransform)tag.transform).anchoredPosition = local + offset;
                }
            }

            drop.Clear();
            foreach (KeyValuePair<BayState, Button> kv in tags)
            {
                if (kv.Key.Kind != BayKind.Locked) drop.Add(kv.Key);
            }
            foreach (BayState b in drop)
            {
                Destroy(tags[b].gameObject);
                tags.Remove(b);
            }
        }
    }
}
