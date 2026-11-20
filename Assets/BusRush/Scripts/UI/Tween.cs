using System;
using System.Collections;
using UnityEngine;

namespace BusRush.UI
{
    public enum Ease
    {
        Linear,
        OutQuad,
        InQuad,
        OutCubic,
        InOutSine,
        OutBack,
        InBack
    }

    /// <summary>
    /// Minimal coroutine tweens. Always unscaled time (UI keeps animating while the game is paused).
    /// Tweens run on <c>host</c> and stop automatically when it is disabled or destroyed.
    /// </summary>
    public static class Tween
    {
        public static float Evaluate(Ease ease, float t)
        {
            t = Mathf.Clamp01(t);
            const float back = 1.70158f;
            switch (ease)
            {
                case Ease.OutQuad: return 1f - (1f - t) * (1f - t);
                case Ease.InQuad: return t * t;
                case Ease.OutCubic: return 1f - Mathf.Pow(1f - t, 3f);
                case Ease.InOutSine: return -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;
                case Ease.OutBack:
                {
                    float u = t - 1f;
                    return 1f + (back + 1f) * u * u * u + back * u * u;
                }
                case Ease.InBack: return (back + 1f) * t * t * t - back * t * t;
                default: return t;
            }
        }

        public static Coroutine Run(MonoBehaviour host, float duration, Action<float> step, Ease ease = Ease.Linear,
                                    float delay = 0f, Action done = null)
        {
            if (host == null || !host.isActiveAndEnabled)
            {
                step?.Invoke(Evaluate(ease, 1f));
                done?.Invoke();
                return null;
            }
            return host.StartCoroutine(Routine(duration, step, ease, delay, done));
        }

        public static Coroutine Scale(MonoBehaviour host, Transform target, Vector3 from, Vector3 to, float duration,
                                      Ease ease = Ease.OutBack, float delay = 0f, Action done = null)
        {
            if (delay <= 0f && target != null) target.localScale = from;
            return Run(host, duration, k =>
            {
                if (target != null) target.localScale = Vector3.LerpUnclamped(from, to, k);
            }, ease, delay, done);
        }

        public static Coroutine Fade(MonoBehaviour host, CanvasGroup group, float from, float to, float duration,
                                     Ease ease = Ease.Linear, float delay = 0f, Action done = null)
        {
            return Run(host, duration, k =>
            {
                if (group != null) group.alpha = Mathf.LerpUnclamped(from, to, k);
            }, ease, delay, done);
        }

        public static Coroutine Move(MonoBehaviour host, RectTransform target, Vector2 from, Vector2 to, float duration,
                                     Ease ease = Ease.OutCubic, float delay = 0f, Action done = null)
        {
            return Run(host, duration, k =>
            {
                if (target != null) target.anchoredPosition = Vector2.LerpUnclamped(from, to, k);
            }, ease, delay, done);
        }

        public static Coroutine Value(MonoBehaviour host, float from, float to, float duration, Action<float> setter,
                                      Ease ease = Ease.Linear, float delay = 0f, Action done = null)
        {
            return Run(host, duration, k => setter(Mathf.LerpUnclamped(from, to, k)), ease, delay, done);
        }

        /// <summary>Short horizontal shake, e.g. for "not enough coins".</summary>
        public static Coroutine Shake(MonoBehaviour host, RectTransform target, float strength = 12f, float duration = 0.35f)
        {
            Vector2 origin = target.anchoredPosition;
            return Run(host, duration, k =>
            {
                if (target == null) return;
                float damp = 1f - k;
                target.anchoredPosition = origin + new Vector2(Mathf.Sin(k * Mathf.PI * 8f) * strength * damp, 0f);
            }, Ease.Linear, 0f, () =>
            {
                if (target != null) target.anchoredPosition = origin;
            });
        }

        /// <summary>Endless gentle scale pulse.</summary>
        public static Coroutine Pulse(MonoBehaviour host, Transform target, float amount = 0.05f, float period = 1.2f)
        {
            return host != null && host.isActiveAndEnabled ? host.StartCoroutine(PulseRoutine(target, amount, period)) : null;
        }

        public static void Stop(MonoBehaviour host, ref Coroutine routine)
        {
            if (routine != null && host != null)
            {
                host.StopCoroutine(routine);
            }
            routine = null;
        }

        private static IEnumerator Routine(float duration, Action<float> step, Ease ease, float delay, Action done)
        {
            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }

            if (duration > 0f)
            {
                for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
                {
                    step?.Invoke(Evaluate(ease, t / duration));
                    yield return null;
                }
            }

            step?.Invoke(Evaluate(ease, 1f));
            done?.Invoke();
        }

        private static IEnumerator PulseRoutine(Transform target, float amount, float period)
        {
            Vector3 baseScale = target.localScale;
            for (float t = 0f; target != null; t += Time.unscaledDeltaTime)
            {
                float s = 1f + amount * (0.5f - 0.5f * Mathf.Cos(t / period * Mathf.PI * 2f));
                target.localScale = baseScale * s;
                yield return null;
            }
        }
    }
}
