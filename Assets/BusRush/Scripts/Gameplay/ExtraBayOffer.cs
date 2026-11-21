namespace BusRush.Gameplay
{
    /// <summary>
    /// The Level Failed "Extra bay" offer: one free bay (rewarded video) per level, and only while a locked bay is
    /// left. A new level builds its bays again, so the opened bay is locked again there. Pure logic (edit-mode tested).
    /// </summary>
    public sealed class ExtraBayOffer
    {
        public bool Used { get; private set; }

        /// <summary>Whether the screen shows the offer.</summary>
        public bool CanOffer(int lockedBays) => !Used && lockedBays > 0;

        /// <summary>Spends the offer; false (nothing changes) if it cannot be offered.</summary>
        public bool TryUse(int lockedBays)
        {
            if (!CanOffer(lockedBays)) return false;
            Used = true;
            return true;
        }
    }
}
