namespace BusRush.Gameplay
{
    public enum BayKind
    {
        Vip,
        Open,
        Locked
    }

    /// <summary>
    /// One parking bay. A bay is reserved the moment a vehicle starts driving to it, so two vehicles never target the
    /// same bay; it is parked once the vehicle arrives, and departing while it backs out.
    /// </summary>
    public sealed class BayState
    {
        internal BayManager Owner;

        public int Index { get; internal set; }
        public BayKind Kind { get; internal set; }
        public BayView View { get; internal set; }

        /// <summary>Vehicle driving to, parked in, or backing out of this bay.</summary>
        public VehicleView Vehicle { get; internal set; }
        public int Filled { get; private set; }
        public bool Reserved { get; internal set; }
        public bool Departing { get; internal set; }

        /// <summary>Passengers walking to this vehicle (seat taken, not yet aboard).</summary>
        public int SeatsReserved { get; internal set; }

        /// <summary>Every seat filled; the vehicle is about to leave.</summary>
        public bool Full { get; internal set; }

        public bool IsFree => Kind == BayKind.Open && Vehicle == null && !Reserved && !Departing;
        public bool IsParked => Vehicle != null && !Reserved && !Departing;
        public int Capacity => Vehicle != null && Vehicle.Data != null ? Vehicle.Data.Capacity : 0;

        /// <summary>Parked, not full, and a seat is still free for a passenger of <paramref name="color"/>.</summary>
        public bool CanBoard(GameColor color) =>
            IsParked && !Full && Vehicle.Data.color == color && Filled + SeatsReserved < Capacity;

        /// <summary>Passengers on board; updates the "n/cap" pill.</summary>
        public void SetFilled(int count)
        {
            Filled = count < 0 ? 0 : (count > Capacity ? Capacity : count);
            if (View != null) View.SetPill(Filled, Capacity);
        }

        internal void ResetFilled()
        {
            Filled = 0;
            SeatsReserved = 0;
            Full = false;
        }

        /// <summary>Backs the parked vehicle out and drives it off screen; pays out its capacity in coins.</summary>
        public void DepartVehicle()
        {
            if (Owner != null) Owner.Depart(this);
        }
    }
}
