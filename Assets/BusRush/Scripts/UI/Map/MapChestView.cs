using System.Collections;
using BusRush.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>Road Map gift chest: closed + gentle bob until collected, then chest_open (still); bounces when due.</summary>
    public sealed class MapChestView : MonoBehaviour
    {
        [SerializeField] private Image image;
        [SerializeField] private Image shadow;
        [SerializeField] private UiLoopBob bob;
        [SerializeField] private Sprite closedSprite;
        [SerializeField] private Sprite openSprite;
        [Tooltip("Soft glow behind a READY chest (pulses). Optional.")]
        [SerializeField] private Image glow;

        private MapRewards.ChestState state;
        private bool ready;

        /// <summary>LOCKED: closed and still, dimmed. READY: closed, glowing and wobbling. CLAIMED: open and still.</summary>
        public void SetState(MapRewards.ChestState s)
        {
            state = s;
            ready = s == MapRewards.ChestState.Ready;
            bool claimed = s == MapRewards.ChestState.Claimed;
            image.sprite = shadow.sprite = claimed ? openSprite : closedSprite;
            image.color = s == MapRewards.ChestState.Locked ? new Color(0.72f, 0.74f, 0.8f, 1f) : Color.white;
            bob.enabled = ready;
            Transform t = bob.transform;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            if (glow != null) glow.gameObject.SetActive(ready);
        }

        public MapRewards.ChestState State => state;

        private void Update()
        {
            if (!ready) return;
            // Wobble: a quick shake every 1.6 s, glow breathes 1.2 s. Transform / colour only, no allocations.
            float t = Time.unscaledTime;
            float phase = Mathf.Repeat(t, 1.6f);
            float shake = phase < 0.5f ? Mathf.Sin(phase / 0.5f * Mathf.PI * 4f) * 7f * (1f - phase / 0.5f) : 0f;
            bob.transform.localRotation = Quaternion.Euler(0f, 0f, shake);
            if (glow != null)
            {
                float k = 0.5f - 0.5f * Mathf.Cos(t / 1.2f * Mathf.PI * 2f);
                Color c = glow.color;
                c.a = Mathf.Lerp(0.35f, 0.9f, k);
                glow.color = c;
                glow.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.95f, 1.15f, k);
            }
        }

        /// <summary>Three quick bounces (the chest just earned), about 1.2 s.</summary>
        public IEnumerator Bounce3()
        {
            Transform t = bob.transform;
            bob.enabled = false;
            Vector3 home = t.localPosition;
            for (int i = 0; i < 3; i++)
            {
                for (float k = 0f; k < 1f; k += Time.unscaledDeltaTime / 0.4f)
                {
                    float hop = Mathf.Sin(k * Mathf.PI);
                    t.localPosition = home + new Vector3(0f, 22f * hop, 0f);
                    t.localScale = new Vector3(1f + 0.12f * hop, 1f + 0.18f * hop, 1f);
                    yield return null;
                }
                Haptics.Light();
            }
            t.localPosition = home;
            t.localScale = Vector3.one;
            bob.enabled = true;
        }
    }
}
