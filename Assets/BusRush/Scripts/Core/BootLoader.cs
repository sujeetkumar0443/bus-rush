using System.Collections;
using BusRush.UI;
using UnityEngine;

namespace BusRush
{
    /// <summary>Boot scene entry point: creates persistent systems, then the splash preloads and opens Home.</summary>
    public sealed class BootLoader : MonoBehaviour
    {
        [SerializeField] private SplashController splash;
        [Tooltip("Used only when there is no splash screen.")]
        [SerializeField, Min(0f)] private float delay = 0.5f;

        private void Awake()
        {
            GameManager.EnsureExists();
            SaveSystem.Load();
            AudioManager.EnsureCreated();
            SceneLoader.EnsureTransitionCanvas();
        }

        private IEnumerator Start()
        {
            if (splash != null)
            {
                yield return splash.Play(); // loads Home itself
                yield break;
            }
            yield return new WaitForSecondsRealtime(delay);
            SceneLoader.LoadHome();
        }
    }
}
