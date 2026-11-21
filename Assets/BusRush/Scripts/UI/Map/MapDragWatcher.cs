using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BusRush.UI
{
    /// <summary>Sits next to a ScrollRect and reports when the player starts dragging it (stops auto-follow).</summary>
    public sealed class MapDragWatcher : MonoBehaviour, IBeginDragHandler
    {
        public event Action BeganDrag;

        public void OnBeginDrag(PointerEventData eventData) => BeganDrag?.Invoke();
    }
}
