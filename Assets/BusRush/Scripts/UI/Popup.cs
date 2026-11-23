using System;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>
    /// Base popup: full-screen dim + a panel inside the safe area. Opens with a scale bounce, closes with a fade.
    /// Open through <see cref="UIManager.Open{T}"/>; close with <see cref="Close"/>.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class Popup : MonoBehaviour
    {
        public const float OpenDuration = 0.25f;
        public const float CloseDuration = 0.15f;

        [SerializeField] private Image dim;
        [SerializeField] private RectTransform panel;
        [SerializeField] private Button closeButton;
        [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.65f;
        [SerializeField] private bool closeOnDimClick = true;
        [SerializeField] private bool closeOnBack = true;

        private CanvasGroup group;
        private Coroutine panelAnim;
        private Coroutine fadeAnim;
        private float fitScale = 1f;

        public bool IsOpen { get; private set; }
        public bool CloseOnBack => closeOnBack;

        /// <summary>False while the popup must not be closed (a Lucky Spin in progress): dim taps, X and Back are ignored.</summary>
        public virtual bool CanClose => true;
        public RectTransform Panel => panel;

        public event Action<Popup> Closed;

        protected virtual void Awake()
        {
            group = GetComponent<CanvasGroup>();
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (dim != null && dim.TryGetComponent(out Button dimButton))
            {
                dimButton.onClick.AddListener(() =>
                {
                    if (closeOnDimClick && IsOpen && CanClose) AudioManager.Play(SfxId.UiBack);
                    if (closeOnDimClick) Close();
                });
            }
        }

        public void Close()
        {
            if (!IsOpen || !CanClose) return;
            if (UIManager.Instance != null) UIManager.Instance.Close(this);
            else Hide();
        }

        protected void SetCloseInteractable(bool interactable)
        {
            if (closeButton != null) closeButton.interactable = interactable;
        }

        internal void Show()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            IsOpen = true;
            OnOpening();

            Tween.Stop(this, ref fadeAnim);
            Tween.Stop(this, ref panelAnim);
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;

            FitPanel();
            if (dim != null)
            {
                Color c = dim.color;
                fadeAnim = Tween.Value(this, 0f, dimAlpha, OpenDuration, a => dim.color = new Color(c.r, c.g, c.b, a));
            }
            if (panel != null)
            {
                panelAnim = Tween.Scale(this, panel, Vector3.one * (fitScale * 0.6f), Vector3.one * fitScale, OpenDuration, Ease.OutBack);
            }

            OnOpened();
        }

        internal void Hide()
        {
            if (!gameObject.activeInHierarchy)
            {
                IsOpen = false;
                return;
            }

            IsOpen = false;
            group.interactable = false;
            group.blocksRaycasts = false;
            Tween.Stop(this, ref fadeAnim);
            fadeAnim = Tween.Fade(this, group, group.alpha, 0f, CloseDuration, Ease.OutQuad, 0f, () =>
            {
                gameObject.SetActive(false);
            });

            OnClosed();
            Closed?.Invoke(this);
        }

        /// <summary>Scales the panel down if it does not fit the safe area (small or very wide screens).</summary>
        private void FitPanel()
        {
            fitScale = 1f;
            if (panel == null || !(panel.parent is RectTransform area)) return;

            Canvas.ForceUpdateCanvases();
            Vector2 avail = area.rect.size - new Vector2(16f, 24f);
            Vector2 size = panel.rect.size;
            if (size.x > 0f && size.y > 0f)
            {
                fitScale = Mathf.Min(1f, avail.x / size.x, avail.y / size.y);
            }
        }

        /// <summary>Called before layout fitting and the open animation (change panel size/content here).</summary>
        protected virtual void OnOpening() { }

        /// <summary>Called after the open animation has started.</summary>
        protected virtual void OnOpened() { }

        protected virtual void OnClosed() { }
    }
}
