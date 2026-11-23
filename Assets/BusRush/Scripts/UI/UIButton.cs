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
        [Tooltip("Chunky buttons: while pressed everything but the bottom edge (Lip) and shadow sinks by this many units.")]
        [SerializeField] private float pushDepth;
        [SerializeField] private bool haptic = true;
        [Tooltip("Light tick instead of the short buzz (UI v2 buttons).")]
        [SerializeField] private bool lightHaptic;
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
            Push(false);
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
            Push(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            ScaleTo(1f, 0.2f, Ease.OutBack);
            Push(false);
        }

        private RectTransform[] pushed;
        private Vector2[] pushedHome;
        private bool isPushed;

        /// <summary>The face (everything except the bottom edge and shadow) sinks onto the bottom edge while held.</summary>
        private void Push(bool down)
        {
            if (pushDepth <= 0f || target == null || isPushed == down) return;
            if (pushed == null)
            {
                var list = new System.Collections.Generic.List<RectTransform>();
                foreach (Transform child in target)
                {
                    if (child.name == "Lip" || child.name == "DropShadow" || !(child is RectTransform rt)) continue;
                    list.Add(rt);
                }
                pushed = list.ToArray();
                pushedHome = new Vector2[pushed.Length];
            }
            isPushed = down;
            for (int i = 0; i < pushed.Length; i++)
            {
                if (pushed[i] == null) continue;
                if (down) pushedHome[i] = pushed[i].anchoredPosition;
                pushed[i].anchoredPosition = down ? pushedHome[i] - new Vector2(0f, pushDepth) : pushedHome[i];
            }
        }

        private void OnClick()
        {
            Clicked?.Invoke();
            Sfx.Play(name.Contains("Close") || name.Contains("Back") ? "back" : "click");   // soft click, softer for back / close
            if (!haptic) return;
            if (lightHaptic) Haptics.Light();
            else Haptics.Short();
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
