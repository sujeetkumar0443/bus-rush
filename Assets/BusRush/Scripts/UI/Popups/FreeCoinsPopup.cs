using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>
    /// "FREE COINS" (Home.dc.html): watch a rewarded video (<see cref="Ads.Service"/>) for <see cref="reward"/> coins.
    /// On success the popup closes and coins fly to the Home coin pill; the balance is added when the first one lands.
    /// </summary>
    public sealed class FreeCoinsPopup : Popup
    {
        [SerializeField] private Button watchButton;
        [SerializeField] private Image coinImage;
        [SerializeField, Min(1)] private int reward = 50;
        [SerializeField, Range(1, 12)] private int flyingCoins = 6;

        private bool waiting;

        protected override void Awake()
        {
            base.Awake();
            watchButton.onClick.AddListener(Watch);
        }

        protected override void OnOpening() => waiting = false;

        private void Watch()
        {
            if (waiting) return;
            waiting = true;
            Ads.Service.ShowRewarded("home_free_coins", granted =>
            {
                waiting = false;
                if (this == null || !granted)
                {
                    if (this != null) Toast.Show("Video not available");
                    return;
                }
                Vector3 from = coinImage.rectTransform.position;
                Sprite sprite = coinImage.sprite;
                Close();
                if (UIManager.Instance != null && HomeScreen.CoinTarget != null)
                {
                    UIManager.Instance.StartCoroutine(Fly(UIManager.Instance.transform, sprite, from, HomeScreen.CoinTarget, reward, flyingCoins));
                }
                else
                {
                    CoinWallet.Add(reward);
                }
            });
        }

        /// <summary>Coins arc from the popup coin to the pill; the reward is added when the first coin arrives.</summary>
        private static IEnumerator Fly(Transform layer, Sprite sprite, Vector3 from, RectTransform target, int amount, int count)
        {
            bool paid = false;
            var coins = new RectTransform[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("FlyingCoin", typeof(RectTransform));
                go.layer = layer.gameObject.layer;
                var rt = (RectTransform)go.transform;
                rt.SetParent(layer, false);
                rt.sizeDelta = new Vector2(44f, 47f);
                var img = go.AddComponent<Image>();
                img.sprite = sprite;
                img.raycastTarget = false;
                rt.position = from;
                rt.localScale = Vector3.zero;
                coins[i] = rt;
            }

            const float duration = 0.55f, stagger = 0.06f;
            float t = 0f;
            var scatter = new Vector3[count];
            for (int i = 0; i < count; i++) scatter[i] = (Vector3)(Random.insideUnitCircle * 60f) * layer.lossyScale.x;
            while (true)
            {
                t += Time.unscaledDeltaTime;
                bool any = false;
                for (int i = 0; i < count; i++)
                {
                    if (coins[i] == null) continue;
                    float k = (t - i * stagger) / duration;
                    if (k < 0f)
                    {
                        any = true;
                        continue;
                    }
                    if (k >= 1f || target == null)
                    {
                        Object.Destroy(coins[i].gameObject);
                        coins[i] = null;
                        if (!paid)
                        {
                            paid = true;
                            CoinWallet.Add(amount);
                            Haptics.Light();
                            Sfx.Play("coins");
                        }
                        continue;
                    }
                    any = true;
                    Vector3 to = target.position;
                    Vector3 mid = Vector3.Lerp(from, to, 0.35f) + scatter[i];
                    float e = Tween.Evaluate(Ease.InOutSine, k);
                    Vector3 a = Vector3.Lerp(from, mid, e), b = Vector3.Lerp(mid, to, e);
                    coins[i].position = Vector3.Lerp(a, b, e);
                    coins[i].localScale = Vector3.one * Mathf.Lerp(1.4f, 0.8f, k) * Mathf.Clamp01(k * 6f);
                }
                if (!any) break;
                yield return null;
            }
            if (!paid) CoinWallet.Add(amount);
        }
    }
}
