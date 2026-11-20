using System;
using System.Collections;
using BusRush.UI;
using UnityEngine;

namespace BusRush.Gameplay
{
    /// <summary>
    /// One play of the current level: generates the board (seed = level * 7919), routes tapped vehicles to the bays,
    /// counts moves / wrong taps, and decides win (board and bays empty) or lose (open bays full, nothing can leave).
    /// </summary>
    public sealed class GameSession : MonoBehaviour
    {
        public static GameSession Current { get; private set; }

        [SerializeField] private BoardView board;
        [SerializeField] private BayManager bays;
        [SerializeField] private WorldManager world;
        [SerializeField] private float levelCompleteDelay = 0.5f;
        [Tooltip("TEMPORARY until the crowd exists (Step 5): parked vehicles leave on their own after this delay.")]
        [SerializeField] private bool debugAutoDepart = true;
        [SerializeField] private float autoDepartDelay = 3f;

        private bool outOfSpaceShown;

        public int Level { get; private set; }
        public LevelData LevelData { get; private set; }
        public int Moves { get; private set; }
        public int ExitedCount { get; private set; }
        public int DepartedCount { get; private set; }
        public int WrongTaps { get; private set; }
        public int NoBayTaps { get; private set; }
        public bool IsCleared { get; private set; }
        public BoardView Board => board;
        public BayManager Bays => bays;

        /// <summary>Parked vehicles depart automatically after <see cref="autoDepartDelay"/> (test loop until Step 5).</summary>
        public bool DebugAutoDepart
        {
            get => debugAutoDepart;
            set
            {
                if (debugAutoDepart == value) return;
                debugAutoDepart = value;
                if (value)
                {
                    foreach (BayState b in bays.Bays)
                    {
                        if (b.IsParked) StartCoroutine(AutoDepart(b));
                    }
                }
                CheckLose();
            }
        }

        public event Action<VehicleView> OnVehicleExited;
        public event Action<BayState> OnVehicleParked;
        public event Action<BayState> OnVehicleDeparted;
        public event Action OnLevelCleared;

        private void Awake()
        {
            Current = this;
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
            OutOfSpacePopup.BayPurchased -= HandleBayPurchased;
        }

        private void Start()
        {
            Level = Mathf.Max(1, SaveSystem.Data.level);
            LevelData = LevelGenerator.Generate(Level, LevelGenerator.SeedFor(Level));
            if (world != null) world.Apply(Level);

            bays.Build();
            bays.OnVehicleParked += HandleParked;
            bays.OnVehicleDeparted += HandleDeparted;
            board.VehicleExited += v => OnVehicleExited?.Invoke(v);
            board.VehicleLeftBoard += _ => ExitedCount++;
            board.BoardCleared += CheckWin;
            board.Build(LevelData, new RoadExitBehaviour(bays));
            OutOfSpacePopup.BayPurchased += HandleBayPurchased;

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
                case TapResult.NoBay:
                    NoBayTaps++;
                    Haptics.Short();
                    Toast.Show("No free bay!");
                    break;
            }
        }

        private void HandleParked(BayState bay)
        {
            OnVehicleParked?.Invoke(bay);
            if (debugAutoDepart) StartCoroutine(AutoDepart(bay));
            CheckLose();
        }

        private void HandleDeparted(BayState bay)
        {
            DepartedCount++;
            outOfSpaceShown = false;
            OnVehicleDeparted?.Invoke(bay);
            CheckWin();
        }

        private IEnumerator AutoDepart(BayState bay)
        {
            VehicleView vehicle = bay.Vehicle;
            yield return new WaitForSeconds(autoDepartDelay);
            if (bay.IsParked && bay.Vehicle == vehicle) bay.DepartVehicle();
        }

        /// <summary>Placeholder until boarding exists: a parked vehicle can only leave via the debug auto-depart.</summary>
        private bool CanAnyVehicleDepart => debugAutoDepart;

        private void CheckWin()
        {
            if (IsCleared || board.RemainingCount > 0 || !bays.AllEmpty) return;
            IsCleared = true;

            // Progress is saved as soon as the level is won; the popup only grants the coins.
            SaveSystem.Data.level = Mathf.Max(SaveSystem.Data.level, Level + 1);
            SaveSystem.Commit();

            OnLevelCleared?.Invoke();
            StartCoroutine(ShowLevelComplete());
        }

        private void CheckLose()
        {
            if (IsCleared || outOfSpaceShown || board.RemainingCount == 0) return;
            if (!bays.AllOpenOccupied || bays.HasFreeBay || CanAnyVehicleDepart) return;

            outOfSpaceShown = true;
            if (UIManager.Instance != null) UIManager.Instance.Open<OutOfSpacePopup>();
        }

        private void HandleBayPurchased()
        {
            if (bays.UnlockNextBay()) outOfSpaceShown = false;
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
