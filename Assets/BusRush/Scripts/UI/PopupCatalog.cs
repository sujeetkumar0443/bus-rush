using UnityEngine;

namespace BusRush.UI
{
    /// <summary>List of popup prefabs that a <see cref="UIManager"/> can open.</summary>
    [CreateAssetMenu(fileName = "PopupCatalog", menuName = "Bus Rush/Popup Catalog")]
    public sealed class PopupCatalog : ScriptableObject
    {
        [SerializeField] private Popup[] popups = new Popup[0];

        public T Find<T>() where T : Popup
        {
            foreach (Popup p in popups)
            {
                if (p is T typed) return typed;
            }
            return null;
        }
    }
}
