using System;
using System.Collections;
using BusRush.UI;
using UnityEngine;

namespace BusRush.Gameplay
{
    /// <summary>
    /// One play of the current level: generates the board (seed = level * 7919), counts moves / exits / wrong taps
    /// and raises <see cref="OnLevelCleared"/> when every vehicle has left.
    /// </summary>
    public sealed class GameSession : MonoBehaviour
    {
        public static GameSession Current { get; private set; }

        [SerializeField] private BoardView board;
        [SerializeField] private WorldManager world;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private float levelCompleteDelay = 0.5f;

        public int Level { get; private set; }
        public LevelData LevelData { get; private set; }
        public int Moves { get; private set; }
        public int ExitedCount { get; private set; }
        public int WrongTaps { get; private set; }
        public bool IsCleared { get; private set; }
        public BoardView Board => board;

        public event Action<VehicleView> OnVehicleExited;
        public event Action OnLevelCleared;

        private void Awake()
        {
            Current = this;
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        private void Start()
        {
            Level = Mathf.Max(1, SaveSystem.Data.level);
            LevelData = LevelGenerator.Generate(Level, LevelGenerator.SeedFor(Level));
            if (world != null) world.Apply(Level);

            board.VehicleExited += HandleVehicleExited;
            board.BoardCleared += HandleBoardCleared;
            board.Build(LevelData, new StraightExitBehaviour(viewCamera));

            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing)
            {
                GameManager.Instance.SetState(GameState.Playing);
            }
        }

        public void OnVehicleTapped(VehicleView vehicle)
        {
            if (IsCleared) return;
            switch (board.TryMove(vehicle))
            {
                case TapResult.Moved:
                    Moves++;
                    break;
                case TapResult.Blocked:
                    WrongTaps++;
                    Haptics.Short();
                    ScreenFlash.Play();
                    break;
            }
        }

        private void HandleVehicleExited(VehicleView v)
        {
            ExitedCount++;
            OnVehicleExited?.Invoke(v);
        }

        private void HandleBoardCleared()
        {
            if (IsCleared) return;
            IsCleared = true;

            // Progress is saved as soon as the board is cleared; the popup only grants the coins.
            SaveSystem.Data.level = Mathf.Max(SaveSystem.Data.level, Level + 1);
            SaveSystem.Commit();

            OnLevelCleared?.Invoke();
            StartCoroutine(ShowLevelComplete());
        }

        private IEnumerator ShowLevelComplete()
        {
            yield return new WaitForSecondsRealtime(levelCompleteDelay);
            if (UIManager.Instance == null) yield break;
            int coins = GameConfig.Instance != null ? GameConfig.Instance.levelCompleteCoins : 50;
            UIManager.Instance.Get<LevelCompletePopup>().Setup(Level, 3, coins);
            UIManager.Instance.Open<LevelCompletePopup>();
        }

        // ------------------------------------------------------------------ debug

        public void DebugNextLevel()
        {
            SaveSystem.Data.level = Level + 1;
            SaveSystem.Commit();
            SceneLoader.LoadGame();
        }

        public void DebugClearBoard()
        {
            board.DebugClear();
        }
    }
}
