namespace BusRush.Gameplay
{
    /// <summary>
    /// Game-feel tuning in one place. Speeds are multipliers on the base values serialized on the components
    /// (VehicleMotion max speed / acceleration, BayManager departure multiplier, CrowdView walk / board speeds).
    /// </summary>
    public static class GameTuning
    {
        /// <summary>Vehicle drive speed and acceleration: lot exit, road loop and arrival into a bay.</summary>
        public const float VehicleDriveSpeed = 1.8f;

        /// <summary>Full vehicles driving out of their bay and off screen (on top of BayManager's departure multiplier).</summary>
        public const float VehicleDepartSpeed = 1.8f;

        /// <summary>Passenger boarding run (queue to gate to vehicle).</summary>
        public const float PassengerBoardSpeed = 1.5f;

        /// <summary>Queue shuffle-forward walk (also the level intro walk-in and newcomers coming down the alleys).</summary>
        public const float QueueShuffleSpeed = 1.5f;

        /// <summary>
        /// Boarding window: the first FrontWindow passengers in queue order may board. A parked vehicle with space takes
        /// the earliest eligible passenger of its colour; vehicles are served in bay order.
        /// </summary>
        public const int FrontWindow = 8;

        /// <summary>Largest distance (reference px) a vehicle may move in one traffic-check substep (no tunnelling).</summary>
        public const float MaxTrafficStepPx = 4f;
    }
}
