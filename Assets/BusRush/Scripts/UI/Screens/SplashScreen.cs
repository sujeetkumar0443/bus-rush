using System.Collections;
using TMPro;
using UnityEngine;

namespace BusRush.UI
{
    /// <summary>
    /// Boot splash: logo pop, 3D hero vehicles sliding in, loading bar, version text.
    /// <see cref="BootLoader"/> waits for <see cref="Play"/> before loading Home.
    /// </summary>
    public sealed class SplashScreen : MonoBehaviour
    {
        [System.Serializable]
        private struct Hero
        {
            public GameObject prefab;
            public GameColor color;
            public Vector3 position;
            public float yaw;
            [Tooltip("Slide in from this X offset.")]
            public float enterFromX;
            public float delay;
        }

        [SerializeField] private RectTransform logo;
        [SerializeField] private RectTransform loadingFill;
        [SerializeField] private TMP_Text loadingText;
        [SerializeField] private TMP_Text versionText;
        [SerializeField] private Transform stage;
        [SerializeField] private Hero[] heroes = new Hero[0];
        [SerializeField, Min(0.5f)] private float duration = 2.2f;

        private void Awake()
        {
            versionText.text = "v" + Application.version;
            SetProgress(0f);
            logo.localScale = Vector3.zero;
        }

        public IEnumerator Play()
        {
            Tween.Scale(this, logo, Vector3.zero, Vector3.one, 0.5f, Ease.OutBack, 0.1f);

            foreach (Hero hero in heroes)
            {
                if (hero.prefab == null) continue;
                GameObject go = Instantiate(hero.prefab, stage);
                go.transform.localRotation = Quaternion.Euler(0f, hero.yaw, 0f);
                GameColors.Recolor(go.GetComponentsInChildren<Renderer>(), hero.color);

                Transform t = go.transform;
                Vector3 to = hero.position;
                Vector3 from = to + new Vector3(hero.enterFromX, 0f, 0f);
                t.localPosition = from;
                Tween.Run(this, 0.7f, k =>
                {
                    if (t != null) t.localPosition = Vector3.LerpUnclamped(from, to, k);
                }, Ease.OutBack, hero.delay);
            }

            int dots = 0;
            float nextDot = 0f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                SetProgress(Tween.Evaluate(Ease.InOutSine, t / duration));
                if (t >= nextDot)
                {
                    nextDot = t + 0.3f;
                    dots = (dots + 1) % 4;
                    loadingText.text = "Loading" + new string('.', dots);
                }
                yield return null;
            }
            SetProgress(1f);
        }

        private void SetProgress(float k)
        {
            loadingFill.anchorMax = new Vector2(Mathf.Lerp(0.06f, 1f, k), loadingFill.anchorMax.y);
        }
    }
}
