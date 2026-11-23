using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>Coins arcing from a point to a coin pill; the amount is added to the wallet when the first one lands.
    /// Sound: a one-time reward plays the cash ding as the coins land; with <c>countUp</c> the coin pill's count-up plays the ticks and the final ding.</summary>
    public static class CoinFly
    {
        /// <param name="host">Runs the coroutine (must stay alive for ~1 s).</param>
        /// <param name="layer">Canvas transform the flying coins are drawn on.</param>
        public static void Play(MonoBehaviour host, Transform layer, Sprite sprite, Vector3 from, RectTransform target, int amount,
                                int count = 6, Action done = null, bool countUp = false)
        {
            if (host == null || target == null)
            {
                Pay(amount, countUp);
                done?.Invoke();
                return;
            }
            host.StartCoroutine(Fly(layer, sprite, from, target, amount, count, done, countUp));
        }

        private static void Pay(int amount, bool countUp)
        {
            if (countUp)
            {
                CoinText.CountUpSound = true;                      // every CoinText that sees this change ticks, then dings
                try { CoinWallet.Add(amount); }
                finally { CoinText.CountUpSound = false; }
            }
            else
            {
                CoinWallet.Add(amount);
                AudioManager.Play(SfxId.CoinDing, 1f, 0.9f);       // one-time reward: a single ding, no tick
            }
        }

        private static IEnumerator Fly(Transform layer, Sprite sprite, Vector3 from, RectTransform target, int amount, int count, Action done, bool countUp)
        {
            bool paid = false;
            float paidAt = 0f;
            var coins = new RectTransform[count];
            var scatter = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("FlyingCoin", typeof(RectTransform));
                go.layer = layer.gameObject.layer;
                var rt = (RectTransform)go.transform;
                rt.SetParent(layer, false);
                rt.SetAsLastSibling();
                rt.sizeDelta = new Vector2(44f, 47f);
                var img = go.AddComponent<Image>();
                img.sprite = sprite;
                img.raycastTarget = false;
                rt.position = from;
                rt.localScale = Vector3.zero;
                coins[i] = rt;
                scatter[i] = (Vector3)(UnityEngine.Random.insideUnitCircle * 60f) * layer.lossyScale.x;
            }

            const float duration = 0.55f, stagger = 0.06f;
            float t = 0f;
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
                        UnityEngine.Object.Destroy(coins[i].gameObject);
                        coins[i] = null;
                        if (!paid)
                        {
                            paid = true;
                            paidAt = Time.unscaledTime;
                            Pay(amount, countUp);
                            Haptics.Light();
                        }
                        continue;
                    }
                    any = true;
                    Vector3 to = target.position;
                    Vector3 mid = Vector3.Lerp(from, to, 0.35f) + scatter[i];
                    float e = Tween.Evaluate(Ease.InOutSine, k);
                    coins[i].position = Vector3.Lerp(Vector3.Lerp(from, mid, e), Vector3.Lerp(mid, to, e), e);
                    coins[i].localScale = Vector3.one * Mathf.Lerp(1.4f, 0.8f, k) * Mathf.Clamp01(k * 6f);
                }
                if (!any) break;
                yield return null;
            }
            if (!paid) Pay(amount, countUp);
            else if (countUp) yield return new WaitForSecondsRealtime(Mathf.Max(0f, paidAt + 0.5f - Time.unscaledTime)); // let the count-up finish and ding before the screen changes
            done?.Invoke();
        }
    }
}
