using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>
    /// Chunky-button feel: press squash, click SFX hook and a short haptic. Add next to a <see cref="Button"/>.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class UIButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        /// <summary>Raised on every UIButton click. Hook the click sound here once audio exists.</summary>
        public static event Action Clicked;

        [Tooltip("Visual that is squashed on press. Defaults to this transform.")]
        [SerializeField] private Transform target;
        [SerializeField, Range(0.8f, 1f)] private float pressedScale = 0.94f;
        [SerializeField] private bool haptic = true;
        [Tooltip("Optional: dimmed when the button is not interactable.")]
        [SerializeField] private CanvasGroup disabledGroup;

        private Button button;
        private Coroutine anim;

        public Button Button => button != null ? button : button = GetComponent<Button>();

        private void Awake()
        {
            if (target == null) target = transform;
            Button.onClick.AddListener(OnClick);
        }

        private void OnDisable()
        {
            anim = null;
            if (target != null) target.localScale = Vector3.one;
        }

        public void SetInteractable(bool interactable)
        {
            Button.interactable = interactable;
            if (disabledGroup != null) disabledGroup.alpha = interactable ? 1f : 0.55f;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!Button.IsInteractable()) return;
            ScaleTo(pressedScale, 0.07f, Ease.OutQuad);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            ScaleTo(1f, 0.2f, Ease.OutBack);
        }

        private void OnClick()
        {
            Clicked?.Invoke();
            if (haptic) Haptics.Short();
        }

        private void ScaleTo(float scale, float duration, Ease ease)
        {
            Tween.Stop(this, ref anim);
            anim = Tween.Scale(this, target, target.localScale, Vector3.one * scale, duration, ease);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Clicked = null;
    }
}
