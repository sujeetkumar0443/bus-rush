using System;
using BusRush.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>
    /// New World (UI v2, WorldUnlocked.dc.html), Home-scene overlay over the Road Map: a soft, dark copy of the new
    /// world's map, rays, "NEW WORLD!", the world card spinning in, its padlock shaking off, "Levels A - B are open" and
    /// "Let's go!". Falling particles by theme: snow (Snow), sparkles (City), rising bubbles (Beach).
    /// </summary>
    public sealed class NewWorldScreen : MonoBehaviour
    {
        [Serializable]
        private struct Particle
        {
            public Image image;
            public float left;
            public float duration;
            public float delay; // CSS negative delay = already running
        }

        [SerializeField] private RawImage background;
        [SerializeField] private Image dark;
        [SerializeField] private RectTransform rays;
        [SerializeField] private RectTransform title;
        [SerializeField] private CanvasGroup titleGroup;
        [SerializeField] private RectTransform card;
        [SerializeField] private CanvasGroup cardGroup;
        [SerializeField] private Image cardMap;
        [SerializeField] private TMP_Text worldNumberText;
        [SerializeField] private TMP_Text worldNameText;
        [SerializeField] private RectTransform padlock;
        [SerializeField] private CanvasGroup padlockGroup;
        [SerializeField] private RectTransform levels;
        [SerializeField] private CanvasGroup levelsGroup;
        [SerializeField] private TMP_Text levelsText;
        [SerializeField] private RectTransform go;
        [SerializeField] private CanvasGroup goGroup;
        [SerializeField] private Button goButton;
        [SerializeField] private Particle[] particles = new Particle[0];
        [SerializeField] private Sprite[] worldMaps = new Sprite[3];
        [SerializeField] private Sprite flakeSprite;
        [SerializeField] private Sprite sparkleSprite;
        [SerializeField] private Sprite bubbleSprite;
        [SerializeField, Range(0f, 1f)] private float darkAlpha = 0.72f;

        public float Elapsed { get; private set; }
        public int World { get; private set; }

        private enum Theme
        {
            City,
            Snow,
            Beach
        }

        private Theme theme;
        private Action closed;
        private RenderTexture soft;
        private Vector2 titleHome, padHome, levelsHome, goHome;
        private static readonly Vector4 Pop = new Vector4(0.3f, 1.5f, 0.5f, 1f), EaseOut = new Vector4(0f, 0f, 0.58f, 1f),
                                        EaseIn = new Vector4(0.42f, 0f, 1f, 1f);
        private static AnimationCurve cardS, cardR, cardA, dropY, dropS, lockY, lockS, lockR, rise;

        private void Awake()
        {
            titleHome = title.anchoredPosition;
            padHome = padlock.anchoredPosition;
            levelsHome = levels.anchoredPosition;
            goHome = go.anchoredPosition;
            goButton.onClick.AddListener(Close);
            BuildCurves();
        }

        private void OnDestroy() => ReleaseSoft();

        /// <summary>Opens for a 1-based world; <paramref name="onClosed"/> runs after "Let's go!".</summary>
        public void Show(int world, Action onClosed)
        {
            World = world;
            closed = onClosed;
            Elapsed = 0f;
            int index = (world - 1) % RoadMapScreen.WorldNames.Length;
            theme = (Theme)index;
            worldNumberText.text = Localizer.F("WORLD {0}", world);
            worldNameText.text = Localizer.T(RoadMapScreen.WorldNames[index]);
            int first = (world - 1) * MapRewards.LevelsPerWorld + 1;
            int last = Mathf.Min(first + MapRewards.LevelsPerWorld - 1, LevelPlan.PlannedLevels);
            levelsText.text = Localizer.F("Levels {0}-{1} are open", first, last);
            Sprite map = worldMaps[index];
            cardMap.sprite = map;
            ReleaseSoft();
            soft = ScreenGrab.BlurTexture(map.texture);
            background.texture = soft;

            Sprite particle = theme == Theme.Snow ? flakeSprite : theme == Theme.City ? sparkleSprite : bubbleSprite;
            Color tint = theme == Theme.Snow ? new Color(1f, 1f, 1f, 0.85f)
                : theme == Theme.City ? new Color(1f, 0.93f, 0.6f, 0.9f) : new Color(0.85f, 0.97f, 1f, 0.7f);
            foreach (Particle p in particles)
            {
                p.image.sprite = particle;
                p.image.color = tint;
            }
            gameObject.SetActive(true);
            Apply(0f);
            Sfx.Play("new_world");
            Haptics.Light();
        }

        private void Close()
        {
            ReleaseSoft();
            gameObject.SetActive(false);
            closed?.Invoke();
        }

        private void ReleaseSoft()
        {
            if (soft == null) return;
            if (background != null) background.texture = null;
            soft.Release();
            Destroy(soft);
            soft = null;
        }

        private void Update()
        {
            Elapsed += Time.deltaTime;
            Apply(Elapsed);
        }

        private void Apply(float t)
        {
            SetAlpha(dark, darkAlpha * Mathf.Clamp01(t / 0.25f));
            rays.localRotation = Quaternion.Euler(0f, 0f, -360f * (t / 14f % 1f));

            float d = Mathf.Clamp01((t - 0.2f) / 0.45f);
            title.anchoredPosition = titleHome - new Vector2(0f, dropY.Evaluate(d));
            title.localScale = Vector3.one * dropS.Evaluate(d);
            titleGroup.alpha = Mathf.Clamp01(d * 3f);

            float c = Mathf.Clamp01((t - 0.2f) / 0.7f); // cardIn .7s from .2s
            card.localScale = Vector3.one * cardS.Evaluate(c);
            card.localRotation = Quaternion.Euler(0f, 0f, cardR.Evaluate(c));
            cardGroup.alpha = cardA.Evaluate(c);

            float l = Mathf.Clamp01((t - 1.1f) / 0.7f); // lockOff .7s ease-in from 1.1s
            float le = CssCurve.Bezier(EaseIn, l);
            padlock.anchoredPosition = padHome + new Vector2(0f, lockY.Evaluate(le));
            padlock.localScale = Vector3.one * lockS.Evaluate(le);
            padlock.localRotation = Quaternion.Euler(0f, 0f, lockR.Evaluate(le));
            padlockGroup.alpha = cardA.Evaluate(c) * (le < 0.4f ? 1f : 1f - (le - 0.4f) / 0.6f);

            Rise(levels, levelsGroup, levelsHome, (t - 1.3f) / 0.45f);
            Rise(go, goGroup, goHome, (t - 1.5f) / 0.45f);

            // Particles: snow / sparkles fall (translate 0,-20 -> 30,880), bubbles rise; linear, already running.
            foreach (Particle p in particles)
            {
                float k = Mathf.Repeat((t - p.delay) / p.duration, 1f);
                RectTransform r = p.image.rectTransform;
                if (theme == Theme.Beach) r.anchoredPosition = new Vector2(p.left + 30f * k, -(880f - 900f * k));
                else r.anchoredPosition = new Vector2(p.left + 30f * k, -(-20f + 900f * k));
                r.localRotation = theme == Theme.City ? Quaternion.Euler(0f, 0f, -360f * k) : Quaternion.identity;
            }
        }

        private static void Rise(RectTransform rt, CanvasGroup g, Vector2 home, float k)
        {
            k = Mathf.Clamp01(k);
            float e = rise.Evaluate(k);
            rt.anchoredPosition = home - new Vector2(0f, 24f * (1f - e));
            g.alpha = e;
            g.blocksRaycasts = k >= 1f;
        }

        private static void SetAlpha(Graphic g, float a)
        {
            Color c = g.color;
            c.a = a;
            g.color = c;
        }

        private static void BuildCurves()
        {
            if (cardS != null) return;
            float[] cardAt = { 0f, 0.65f, 1f };
            cardS = CssCurve.Build(cardAt, new[] { 0.3f, 1.06f, 1f }, Pop);
            cardR = CssCurve.Build(cardAt, new[] { 14f, -3f, 2f }, Pop); // CSS rotate(-14deg) -> 3deg -> -2deg, negated
            cardA = CssCurve.Build(cardAt, new[] { 0f, 1f, 1f }, Pop);
            dropY = CssCurve.Build(new[] { 0f, 1f }, new[] { 40f, 0f }, Pop);
            dropS = CssCurve.Build(new[] { 0f, 1f }, new[] { 0.8f, 1f }, Pop);
            float[] lockAt = { 0f, 0.4f, 1f };
            lockY = CssCurve.Build(lockAt, new[] { 0f, 20f, 80f }, CssCurve.Linear, 8);
            lockS = CssCurve.Build(lockAt, new[] { 1f, 1.2f, 0.5f }, CssCurve.Linear, 8);
            lockR = CssCurve.Build(lockAt, new[] { 0f, 10f, -30f }, CssCurve.Linear, 8);
            rise = CssCurve.Build(new[] { 0f, 1f }, new[] { 0f, 1f }, EaseOut);
        }
    }
}
