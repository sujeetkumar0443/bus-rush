using UnityEngine;

namespace BusRush.UI
{
    /// <summary>Spins a UI element (e.g. sun rays) in unscaled time.</summary>
    public sealed class Rotator : MonoBehaviour
    {
        [SerializeField] private float degreesPerSecond = 20f;

        private void Update()
        {
            transform.Rotate(0f, 0f, -degreesPerSecond * Time.unscaledDeltaTime);
        }
    }
}
