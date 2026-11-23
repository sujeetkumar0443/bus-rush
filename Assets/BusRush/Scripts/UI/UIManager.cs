using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BusRush.UI
{
    /// <summary>
    /// Per-scene popup stack. Popups are instantiated from the catalog on first use and reused afterwards.
    /// The Android back button (Escape) closes the top popup.
    /// </summary>
    public sealed class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [SerializeField] private PopupCatalog catalog;
        [SerializeField] private RectTransform popupRoot;

        private readonly List<Popup> stack = new List<Popup>();
        private readonly Dictionary<Type, Popup> instances = new Dictionary<Type, Popup>();

        public event Action<Popup> PopupOpened;
        public event Action<Popup> PopupClosed;

        /// <summary>Back pressed while no popup is open (e.g. Game opens the pause menu).</summary>
        public event Action BackPressedWithNoPopup;

        public int OpenCount => stack.Count;
        public Popup Top => stack.Count > 0 ? stack[stack.Count - 1] : null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[BusRush] Second UIManager in scene; ignoring it.");
                return;
            }
            Instance = this;
            if (popupRoot == null) popupRoot = (RectTransform)transform;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                HandleBack();
            }
        }

        public T Open<T>() where T : Popup
        {
            T popup = Get<T>();
            if (popup == null) return null;

            stack.Remove(popup);
            stack.Add(popup);
            popup.Show();
            PopupOpened?.Invoke(popup);
            return popup;
        }

        /// <summary>Returns the popup instance (creating it, inactive, if needed) without opening it.</summary>
        public T Get<T>() where T : Popup
        {
            if (instances.TryGetValue(typeof(T), out Popup existing) && existing != null)
            {
                return (T)existing;
            }

            T prefab = catalog != null ? catalog.Find<T>() : null;
            if (prefab == null)
            {
                Debug.LogError($"[BusRush] No popup prefab of type {typeof(T).Name} in the catalog.");
                return null;
            }

            T created = Instantiate(prefab, popupRoot, false);
            created.name = prefab.name;
            created.gameObject.SetActive(false);
            instances[typeof(T)] = created;
            return created;
        }

        public bool IsOpen<T>() where T : Popup
        {
            return instances.TryGetValue(typeof(T), out Popup p) && p != null && p.IsOpen;
        }

        public void Close(Popup popup, bool force = false)
        {
            if (popup == null || !stack.Contains(popup)) return;
            if (!force && !popup.CanClose) return;
            stack.Remove(popup);
            popup.Hide();
            PopupClosed?.Invoke(popup);
        }

        public void CloseTop()
        {
            if (Top != null) Close(Top);
        }

        public void CloseAll()
        {
            for (int i = stack.Count - 1; i >= 0; i--) Close(stack[i], true); // scene changes etc.: always
        }

        public void HandleBack()
        {
            Popup top = Top;
            if (top != null)
            {
                if (top.CloseOnBack) Close(top);
            }
            else
            {
                BackPressedWithNoPopup?.Invoke();
            }
        }
    }
}
