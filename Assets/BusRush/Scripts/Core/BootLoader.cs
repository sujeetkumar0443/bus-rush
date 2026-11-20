using System.Collections;
using BusRush.UI;
using UnityEngine;

namespace BusRush
{
    /// <summary>Boot scene entry point: creates persistent systems, plays the splash, then loads Home.</summary>
    public sealed class BootLoader : MonoBehaviour
    {
        [SerializeField] private SplashScreen splash;
        [Tooltip("Used only when there is no splash screen.")]
        [SerializeField, Min(0f)] private float delay = 0.5f;

        private void Awake()
        {
            GameManager.EnsureExists();
            SaveSystem.Load();
            SceneLoader.EnsureTransitionCanvas();
        }

        private IEnumerator Start()
        {
            if (splash != null)
            {
                yield return splash.Play();
            }
            else
            {
                yield return new WaitForSecondsRealtime(delay);
            }
            SceneLoader.LoadHome();
        }
    }
}
