using System.Collections;
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

        public void SetClaimed(bool claimed)
        {
            image.sprite = shadow.sprite = claimed ? openSprite : closedSprite;
            bob.enabled = !claimed;
            if (claimed) bob.transform.localRotation = Quaternion.identity;
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
