using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BusRush
{
    /// <summary>Async scene loading with a fade-through-black transition.</summary>
    public static class SceneLoader
    {
        public const string BootScene = "Boot";
        public const string HomeScene = "Home";
        public const string GameScene = "Game";

        public const float DefaultFadeDuration = 0.3f;

        private static ScreenFader fader;

        public static bool IsLoading { get; private set; }

        /// <summary>Raised after the new scene is active and before fading back in.</summary>
        public static event Action<string> SceneReady;

        /// <summary>Creates the persistent transition canvas if it does not exist yet.</summary>
        public static void EnsureTransitionCanvas()
        {
            if (fader == null)
            {
                fader = ScreenFader.Create();
            }
        }

        public static void LoadHome() => Load(HomeScene);

        public static void LoadGame() => Load(GameScene);

        public static void Load(string sceneName, float fadeDuration = DefaultFadeDuration)
        {
            if (IsLoading)
            {
                return;
            }
            EnsureTransitionCanvas();
            fader.StartCoroutine(LoadRoutine(sceneName, fadeDuration));
        }

        /// <summary>
        /// Finishes a scene preloaded with allowSceneActivation = false (the splash): fade out, activate, fade in.
        /// Runs on the persistent fader, so the caller's scene may unload meanwhile.
        /// </summary>
        public static void ActivatePreloaded(AsyncOperation op, string sceneName, float fadeDuration = DefaultFadeDuration)
        {
            if (IsLoading || op == null) return;
            EnsureTransitionCanvas();
            fader.StartCoroutine(ActivateRoutine(op, sceneName, fadeDuration));
        }

        private static IEnumerator ActivateRoutine(AsyncOperation op, string sceneName, float fadeDuration)
        {
            IsLoading = true;
            yield return fader.FadeTo(1f, fadeDuration);
            op.allowSceneActivation = true;
            while (!op.isDone)
            {
                yield return null;
            }
            SceneReady?.Invoke(sceneName);
            yield return fader.FadeTo(0f, fadeDuration);
            IsLoading = false;
        }

        private static IEnumerator LoadRoutine(string sceneName, float fadeDuration)
        {
            IsLoading = true;

            yield return fader.FadeTo(1f, fadeDuration);

            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (op == null)
            {
                Debug.LogError($"[BusRush] Scene '{sceneName}' could not be loaded. Is it in Build Settings?");
                yield return fader.FadeTo(0f, fadeDuration);
                IsLoading = false;
                yield break;
            }

            while (!op.isDone)
            {
                yield return null;
            }

            SceneReady?.Invoke(sceneName);

            yield return fader.FadeTo(0f, fadeDuration);
            IsLoading = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Supports "Enter Play Mode" without domain reload.
            fader = null;
            IsLoading = false;
            SceneReady = null;
        }
    }
}
