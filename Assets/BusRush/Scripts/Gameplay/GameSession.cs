using System;
using System.Collections;
using BusRush.UI;
using UnityEngine;

namespace BusRush.Gameplay
{
    /// <summary>
    /// One play of the current level: board (seed = level * 7919), road to the bays, the passenger queue boarding
    /// matching vehicles, full vehicles leaving, and win / lose.
    /// </summary>
    public sealed class GameSession : MonoBehaviour
    {
        public static GameSession Current { get; private set; }

        [SerializeField] private BoardView board;
        [SerializeField] private BayManager bays;
        [SerializeField] private CrowdView crowd;
        [SerializeField] private GameFx fx;
        [SerializeField] private WorldManager world;
        [SerializeField] private float boardingInterval = 0.12f;
        [SerializeField] private float fullDepartDelay = 0.35f;
        [SerializeField] private float confettiToPopupDelay = 0.8f;
        [SerializeField] private float loseDelay = 0.6f;
        [Tooltip("Debug: parked vehicles leave on their own after a delay, full or not.")]
        [SerializeField] private bool debugAutoDepart;
        [SerializeField] private float autoDepartDelay = 3f;

        private float boardingTimer;
        private float loseTimer;
        private bool outOfSpaceShown;

        public int Level { get; private set; }
        public LevelData LevelData { get; private set; }
        public int Moves { get; private set; }
        public int ExitedCount { get; private set; }
        public int DepartedCount { get; private set; }
        public int BoardedCount { get; private set; }
        public int WrongTaps { get; private set; }
        public int NoBayTaps { get; private set; }
        public bool IsCleared { get; private set; }
        public bool IsLost { get; private set; }
        public BoardView Board => board;
        public BayManager Bays => bays;
        public CrowdView Crowd => crowd;

        /// <summary>Passengers still to board (in the queue or walking to a seat).</summary>
        public int PassengersLeft => crowd != null ? crowd.PassengersLeft : 0;

        public bool DebugAutoDepart
        {
            get => debugAutoDepart;
            set
            {
                if (debugAutoDepart == value) return;
                debugAutoDepart = value;
                if (!value) return;
                for (int i = 0; i < bays.Bays.Count; i++)
                {
                    if (bays.Bays[i].IsParked) StartCoroutine(AutoDepart(bays.Bays[i]));
                }
            }
        }

        public event Action<VehicleView> OnVehicleExited;
        public event Action<BayState> OnVehicleParked;
        public event Action<BayState> OnVehicleDeparted;
        public event Action OnLevelCleared;
        public event Action OnLevelLost;

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
            crowd.Build(LevelData.queue);
            OutOfSpacePopup.BayPurchased += HandleBayPurchased;

            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing)
            {
                GameManager.Instance.SetState(GameState.Playing);
            }
        }

        private void Update()
        {
            if (IsCleared) return;

            boardingTimer += Time.deltaTime;
            if (boardingTimer >= boardingInterval)
            {
                boardingTimer = 0f;
                TryBoardFront();
            }
            UpdateLose(Time.deltaTime);
        }

        /// <summary>Every resolved vehicle tap (tutorial, analytics).</summary>
        public event Action<VehicleView, TapResult> OnTap;

        public void OnVehicleTapped(VehicleView vehicle)
        {
            if (IsCleared) return;
            TapResult result = board.TryMove(vehicle);
            OnTap?.Invoke(vehicle, result);
            switch (result)
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

        // ------------------------------------------------------------------ boarding

        /// <summary>The bay a passenger of this colour would board right now (closest to full first), or null.</summary>
        public BayState FindSeat(GameColor color)
        {
            BayState best = null;
            for (int i = 0; i < bays.Bays.Count; i++) // indexed: no enumerator allocation (runs every frame)
            {
                BayState b = bays.Bays[i];
                if (!b.CanBoard(color)) continue;
                if (best == null || b.Filled + b.SeatsReserved > best.Filled + best.SeatsReserved) best = b;
            }
            return best;
        }

        private void TryBoardFront()
        {
            if (!crowd.FrontAtGate) return;
            BayState bay = FindSeat(crowd.FrontColor);
            if (bay == null) return; // the queue waits; nobody skips ahead

            GameColor color = crowd.FrontColor;
            VehicleView vehicle = bay.Vehicle;
            bay.SeatsReserved++;
            var seat = new Vector2(ScreenRef.BayX(bay.Index), ScreenRef.BayY - 6f);
            crowd.BoardFront(seat, () => OnPassengerArrived(bay, vehicle, color));
        }

        private void OnPassengerArrived(BayState bay, VehicleView vehicle, GameColor color)
        {
            if (bay.Vehicle != vehicle) return; // safety: vehicle left (debug auto-depart)
            bay.SeatsReserved = Mathf.Max(0, bay.SeatsReserved - 1);
            bay.SetFilled(bay.Filled + 1);
            BoardedCount++;

            Tween.Scale(vehicle, vehicle.transform, Vector3.one * 1.06f, Vector3.one, 0.1f, Ease.OutQuad);
            if (fx != null) fx.Sparkle(ScreenRef.RefToWorld(ScreenRef.BayX(bay.Index), ScreenRef.BayY) + Vector3.up * 0.6f, GameColors.GetColor(color));

            if (bay.Filled >= bay.Capacity && !bay.Full)
            {
                bay.Full = true;
                StartCoroutine(DepartWhenFull(bay));
            }
        }

        private IEnumerator DepartWhenFull(BayState bay)
        {
            yield return new WaitForSeconds(fullDepartDelay);
            bay.DepartVehicle();
        }

        private void HandleParked(BayState bay)
        {
            OnVehicleParked?.Invoke(bay);
            if (debugAutoDepart) StartCoroutine(AutoDepart(bay));
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
            if (bay.IsParked && bay.Vehicle == vehicle && !bay.Full && bay.SeatsReserved == 0) bay.DepartVehicle();
        }

        // ------------------------------------------------------------------ win / lose

        private void CheckWin()
        {
            if (IsCleared || board.RemainingCount > 0 || !bays.AllEmpty) return;
            if (crowd.QueueCount > 0 || crowd.WalkerCount > 0) return;
            IsCleared = true;

            // Progress is saved as soon as the level is won; the popup only grants the coins.
            SaveSystem.Data.level = Mathf.Max(SaveSystem.Data.level, Level + 1);
            SaveSystem.Commit();

            OnLevelCleared?.Invoke();
            if (fx != null) fx.Confetti();
            StartCoroutine(ShowLevelComplete());
        }

        /// <summary>
        /// Stuck: vehicles left on the board, every open bay holds a parked vehicle, nothing is moving or walking, and no
        /// parked vehicle can take the queue front. Must hold for <see cref="loseDelay"/> seconds.
        /// </summary>
        public bool IsStuck()
        {
            if (board.RemainingCount == 0 || !bays.AllOpenOccupied || bays.HasFreeBay) return false;
            if (bays.Movers > 0 || crowd.WalkerCount > 0) return false;
            for (int i = 0; i < bays.Bays.Count; i++) if (bays.Bays[i].Full || bays.Bays[i].Departing) return false;
            if (!crowd.HasFront) return false;
            return FindSeat(crowd.FrontColor) == null;
        }

        private void UpdateLose(float dt)
        {
            if (outOfSpaceShown || !IsStuck())
            {
                loseTimer = 0f;
                return;
            }
            loseTimer += dt;
            if (loseTimer < loseDelay) return;

            outOfSpaceShown = true;
            IsLost = true;
            OnLevelLost?.Invoke();
            Toast.Show("All bays are full and nobody in line matches.");
            if (UIManager.Instance != null) UIManager.Instance.Open<OutOfSpacePopup>();
        }

        private void HandleBayPurchased()
        {
            if (!bays.UnlockNextBay()) return;
            outOfSpaceShown = false;
            IsLost = false;
        }

        /// <summary>3 stars with no wrong taps, 2 with up to 2, otherwise 1.</summary>
        public static int StarsFor(int wrongTaps) => wrongTaps == 0 ? 3 : wrongTaps <= 2 ? 2 : 1;

        /// <summary>Coins for a win: base + 10 per star.</summary>
        public static int CoinsFor(int stars)
        {
            int baseCoins = GameConfig.Instance != null ? GameConfig.Instance.levelCompleteCoins : 50;
            return baseCoins + 10 * stars;
        }

        public const string FinishedAllNote = "You finished all levels! More are coming";

        public int Stars { get; private set; }

        private IEnumerator ShowLevelComplete()
        {
            Stars = StarsFor(WrongTaps);
            SaveSystem.Data.SetBestStars(Level, Stars);
            bool finishedAll = Level == LevelPlan.PlannedLevels && !SaveSystem.Data.finishedAllShown;
            if (finishedAll) SaveSystem.Data.finishedAllShown = true;
            SaveSystem.Commit();

            yield return new WaitForSecondsRealtime(confettiToPopupDelay);
            if (UIManager.Instance == null) yield break;
            LevelCompletePopup popup = UIManager.Instance.Get<LevelCompletePopup>();
            popup.Setup(Level, Stars, CoinsFor(Stars));
            popup.SetNote(finishedAll ? FinishedAllNote : null);
            UIManager.Instance.Open<LevelCompletePopup>();
        }

        // ------------------------------------------------------------------ debug

        public void DebugNextLevel()
        {
            SaveSystem.Data.level = Level + 1;
            SaveSystem.Commit();
            SceneLoader.LoadGame();
        }

        /// <summary>Debug: empties the board and the queue and sends parked vehicles away, which wins the level.</summary>
        public void DebugClearBoard()
        {
            crowd.Build(Array.Empty<GameColor>());
            for (int i = 0; i < bays.Bays.Count; i++)
            {
                if (bays.Bays[i].IsParked && !bays.Bays[i].Full) bays.Bays[i].DepartVehicle();
            }
            board.DebugClear();
            CheckWin();
        }
    }
}
