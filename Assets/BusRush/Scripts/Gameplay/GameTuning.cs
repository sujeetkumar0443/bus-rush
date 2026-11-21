namespace BusRush.Gameplay
{
    /// <summary>Game-feel tuning in one place (reference px = the 390 x 844 layout; 34 px = one world unit).</summary>
    public static class GameTuning
    {
        // ---------------------------------------------------------------- traffic (see TrafficSchedule)

        /// <summary>
        /// Constant road speed of every vehicle (reference px per second, measured along x; one world unit = 34 px).
        /// Longest route (lot corner cell to the farthest open bay) 2.0 s; the median route is about 1.0 s (layout ratio).
        /// </summary>
        public const float RoadSpeedPx = 675f;

        /// <summary>From rest to road speed when a vehicle starts (lot or bay), and road speed to rest into a bay.</summary>
        public const float LotAccelSeconds = 0.15f;

        /// <summary>One fixed arc radius for every corner, driven at full speed.</summary>
        public const float CornerRadiusPx = 4f;

        /// <summary>Total clearance kept between two vehicles along the road (the reservation gap), reference px.</summary>
        public const float GapMarginPx = 8f;

        /// <summary>Delay between a vehicle becoming full and its departure becoming ready to schedule.</summary>
        public const float FullDepartDelaySeconds = 0.25f;

        // ---------------------------------------------------------------- passengers

        /// <summary>
        /// One passenger per vehicle per boarding tick; with the run speed below a bus of 8 boards in about 1.5 s
        /// (7 ticks + the last ~250 px run).
        /// </summary>
        public const float BoardingIntervalSeconds = 0.14f;

        /// <summary>Boarding run (queue to gate to vehicle), reference px per second.</summary>
        public const float PassengerRunSpeedPx = 520f;

        /// <summary>Queue shuffle-forward walk (also the level intro walk-in and newcomers down the alleys), px/s.</summary>
        public const float QueueShuffleSpeedPx = 128f;

        // ---------------------------------------------------------------- boosters (see BoosterInventory, BoosterController)

        /// <summary>The booster row is hidden before this level (tutorial levels 1-3).</summary>
        public const int BoosterRowFromLevel = 4;
        public const int VipUnlockLevel = 4;
        public const int ArrangeUnlockLevel = 7;
        public const int ShuffleUnlockLevel = 13;

        /// <summary>Starting count of each booster, and starting coins (new saves).</summary>
        public const int StartingBoosters = 2;
        public const int StartingCoins = 300;

        /// <summary>Price of one booster in coins (any type).</summary>
        public const int BoosterPriceCoins = 100;

        /// <summary>A booster pressed while vehicles move or passengers board waits up to this long for a calm board.</summary>
        public const float BoosterCalmWaitSeconds = 1f;

        /// <summary>Arrange: passengers move to their new slots in this time.</summary>
        public const float ArrangeAnimSeconds = 0.4f;

        /// <summary>Shuffle: random colour permutations tried before giving up (and refunding).</summary>
        public const int ShuffleRetries = 20;
        public const float ShuffleAnimSeconds = 0.35f;

        /// <summary>
        /// Boarding window: the first FrontWindow passengers in queue order may board. A parked vehicle with space takes
        /// the earliest eligible passenger of its colour; vehicles are served in bay order.
        /// </summary>
        public const int FrontWindow = 8;
    }
}
