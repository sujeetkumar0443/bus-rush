using BusRush.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace BusRush.Gameplay
{
    /// <summary>
    /// Tap-to-move input (new Input System: mouse and touch). Raycasts the vehicles' enlarged hit boxes and picks the
    /// vehicle whose footprint is under the finger, falling back to the nearest one. Ignored while a popup is open,
    /// the game is paused, a scene is loading or the pointer is over UI.
    /// </summary>
    public sealed class InputController : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private GameSession session;
        [Tooltip("Height of the plane used to map a tap onto footprints (about mid vehicle height).")]
        [SerializeField] private float pickHeight = 0.5f;
        [SerializeField] private float maxRayDistance = 200f;

        private readonly RaycastHit[] hits = new RaycastHit[16];

        public bool InputEnabled { get; set; } = true;

        private void Update()
        {
            if (!CanAcceptInput()) return;
            if (TryGetTap(out Vector2 position, out int pointerId) && !IsOverUI(pointerId))
            {
                HandleTap(position);
            }
        }

        public bool CanAcceptInput()
        {
            if (!InputEnabled || SceneLoader.IsLoading) return false;
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return false;
            if (UIManager.Instance != null && UIManager.Instance.OpenCount > 0) return false;
            return session == null || !session.IsCleared;
        }

        /// <summary>Resolves a tap at a screen position. Public so automated tests drive the same path.</summary>
        public VehicleView HandleTap(Vector2 screenPosition)
        {
            VehicleView v = PickAt(screenPosition);
            if (v != null && session != null) session.OnVehicleTapped(v);
            return v;
        }

        public VehicleView PickAt(Vector2 screenPosition)
        {
            Ray ray = viewCamera.ScreenPointToRay(screenPosition);
            int count = Physics.RaycastNonAlloc(ray, hits, maxRayDistance, ~0, QueryTriggerInteraction.Collide);

            var plane = new Plane(Vector3.up, new Vector3(0f, pickHeight, 0f));
            Vector3 pickPoint = plane.Raycast(ray, out float enter) ? ray.GetPoint(enter) : ray.origin;

            VehicleView best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                VehicleView v = hits[i].collider.GetComponentInParent<VehicleView>();
                if (v == null || v.IsExiting || v.Data == null) continue;

                if (v.FootprintContains(pickPoint)) return v;

                Vector3 d = v.transform.position - pickPoint;
                d.y = 0f;
                if (d.sqrMagnitude < bestDistance)
                {
                    bestDistance = d.sqrMagnitude;
                    best = v;
                }
            }
            return best;
        }

        private static bool TryGetTap(out Vector2 position, out int pointerId)
        {
            Touchscreen touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
            {
                position = touch.primaryTouch.position.ReadValue();
                pointerId = touch.primaryTouch.touchId.ReadValue();
                return true;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                position = mouse.position.ReadValue();
                pointerId = -1;
                return true;
            }

            position = default;
            pointerId = -1;
            return false;
        }

        private static bool IsOverUI(int pointerId)
        {
            EventSystem es = EventSystem.current;
            if (es == null) return false;
            return pointerId >= 0 ? es.IsPointerOverGameObject(pointerId) : es.IsPointerOverGameObject();
        }
    }
}
