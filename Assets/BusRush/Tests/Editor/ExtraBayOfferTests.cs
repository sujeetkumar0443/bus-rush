using BusRush.Gameplay;
using NUnit.Framework;

namespace BusRush.Tests
{
    /// <summary>Level Failed "Extra bay": once per level, and only while a locked bay exists.</summary>
    public sealed class ExtraBayOfferTests
    {
        [Test]
        public void OfferedOnlyOncePerLevel()
        {
            var level = new ExtraBayOffer();
            Assert.IsTrue(level.CanOffer(3), "fresh level with locked bays: offered");
            Assert.IsTrue(level.TryUse(3), "first use opens a bay");
            Assert.IsFalse(level.CanOffer(2), "already used this level: hidden");
            Assert.IsFalse(level.TryUse(2), "a second use does nothing");
            Assert.IsTrue(new ExtraBayOffer().CanOffer(3), "the next level offers it again");
        }

        [Test]
        public void OfferedOnlyWhileALockedBayExists()
        {
            var level = new ExtraBayOffer();
            Assert.IsFalse(level.CanOffer(0), "no locked bay left: hidden");
            Assert.IsFalse(level.TryUse(0), "nothing to open: not spent");
            Assert.IsFalse(level.Used);
            Assert.IsTrue(level.CanOffer(1));
        }
    }
}
