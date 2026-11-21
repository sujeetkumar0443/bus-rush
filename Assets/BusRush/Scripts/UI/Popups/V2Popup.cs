using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>
    /// UI v2 popup shell: blurred + dimmed screen behind (ScreenGrab), white panel (base Popup pop-in) and a ribbon
    /// that drops in (CSS drop: translateY(-40px) scale(.8) opacity 0 -> 1, cubic-bezier(.3,1.5,.5,1)).
    /// </summary>
    public abstract class V2Popup : Popup
    {
        [SerializeField] private RawImage blur;
        [SerializeField] private RectTransform ribbon;
        [SerializeField] private CanvasGroup ribbonGroup;
        [SerializeField] private float ribbonDelay = 0.15f;
        [SerializeField] private float ribbonDuration = 0.4f;

        private float clock;
        private float ribbonHomeY;
        private static AnimationCurve dropY, dropS, dropA;

        protected override void Awake()
        {
            base.Awake();
            ribbonHomeY = ribbon.anchoredPosition.y;
            if (dropY != null) return;
            Vector4 pop = new Vector4(0.3f, 1.5f, 0.5f, 1f);
            float[] ends = { 0f, 1f };
            dropY = CssCurve.Build(ends, new[] { 40f, 0f }, pop);
            dropS = CssCurve.Build(ends, new[] { 0.8f, 1f }, pop);
            dropA = CssCurve.Build(ends, new[] { 0f, 1f }, pop);
        }

        protected override void OnOpening()
        {
            clock = 0f;
            Canvas canvas = GetComponentInParent<Canvas>();
            if (blur != null && canvas != null) StartCoroutine(ScreenGrab.Blurred(blur, canvas.rootCanvas));
            DropRibbon();
        }

        protected virtual void OnDestroy() => ScreenGrab.Release(blur);

        protected virtual void Update()
        {
            if (!IsOpen) return;
            clock += Time.unscaledDeltaTime; // works while the game is paused
            DropRibbon();
        }

        private void DropRibbon()
        {
            float k = Mathf.Clamp01((clock - ribbonDelay) / ribbonDuration);
            ribbon.anchoredPosition = new Vector2(ribbon.anchoredPosition.x, ribbonHomeY - dropY.Evaluate(k));
            ribbon.localScale = Vector3.one * dropS.Evaluate(k);
            ribbonGroup.alpha = dropA.Evaluate(k);
        }
    }
}
