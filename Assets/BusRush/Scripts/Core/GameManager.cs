using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BusRush
{
    public enum GameState
    {
        Boot,
        Home,
        Playing,
        Paused,
        Won,
        Lost
    }

    /// <summary>Persistent game-wide state holder.</summary>
    public sealed class GameManager : MonoBehaviour
    {
        public const int TargetFrameRate = 60;

        public static GameManager Instance { get; private set; }

        public GameState State { get; private set; } = GameState.Boot;

        /// <summary>(previous, current)</summary>
        public event Action<GameState, GameState> StateChanged;

        public static GameManager EnsureExists()
        {
            if (Instance == null)
            {
                new GameObject("[GameManager]").AddComponent<GameManager>();
            }
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            ApplyRuntimeSettings();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                Instance = null;
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                SaveSystem.Save();
            }
        }

        private void OnApplicationQuit()
        {
            SaveSystem.Save();
        }

        public void SetState(GameState next)
        {
            if (next == State)
            {
                return;
            }

            GameState previous = State;
            State = next;
            Time.timeScale = next == GameState.Paused ? 0f : 1f;
            StateChanged?.Invoke(previous, next);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            switch (scene.name)
            {
                case SceneLoader.HomeScene:
                    SetState(GameState.Home);
                    break;
                case SceneLoader.GameScene:
                    SetState(GameState.Playing);
                    break;
            }
        }

        private static void ApplyRuntimeSettings()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFrameRate;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.orientation = ScreenOrientation.Portrait;
        }
    }
}
