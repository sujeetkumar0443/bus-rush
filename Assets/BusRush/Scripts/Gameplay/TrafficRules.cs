using UnityEngine;

namespace BusRush.Gameplay
{
    /// <summary>
    /// Pure traffic rules (no scene state), shared by <see cref="VehicleMotion"/> and the edit-mode tests.
    /// <para><b>Priority.</b> One global total order, as a key (lower = higher priority). A vehicle in the road network
    /// (it left the lot, or pulled out of a bay) outranks every vehicle still inside the lot or parked; inside a tier,
    /// whoever entered first outranks the rest. Soft waits (merge gate, arrival lane, bay departure) only ever target a
    /// higher-priority vehicle, so the wait graph has no cycle.</para>
    /// <para><b>Hard guard.</b> Independently, a vehicle never makes its overlap with another vehicle deeper (real
    /// footprints, no padding: being close is not overlapping).</para>
    /// </summary>
    public static class TrafficRules
    {
        public const long LotTier = 1_000_000_000L;
        public const long ParkedTier = 2_000_000_000L;

        /// <summary>Priority of the n-th vehicle to enter the road network (left the lot or pulled out of a bay).</summary>
        public static long NetworkKey(long sequence) => sequence;

        /// <summary>Priority of the n-th vehicle to start moving inside the lot (always below the road network).</summary>
        public static long LotKey(long sequence) => LotTier + sequence;

        /// <summary>A parked vehicle is out of the network; it re-enters with a new NetworkKey when it departs.</summary>
        public static long ParkedKey(long sequence) => ParkedTier + sequence;

        /// <summary>The only soft-wait rule: wait for strictly higher priority (lower key), never for lower.</summary>
        public static bool MayWaitFor(long myKey, long otherKey) => otherKey < myKey;

        /// <summary>Two runs along the arrival lane conflict when they go opposite ways over overlapping stretches.</summary>
        public static bool LaneRunsConflict(int dirA, float minA, float maxA, int dirB, float minB, float maxB) =>
            dirA != dirB && maxA > minB && minA < maxB;

        /// <summary>
        /// Penetration depth of two oriented rectangles (minimum overlap over the separating axes); 0 or less when they
        /// do not overlap.
        /// </summary>
        public static float Penetration(Vector2 c1, Vector2 f1, float hl1, float hw1, Vector2 c2, Vector2 f2, float hl2, float hw2)
        {
            var r1 = new Vector2(f1.y, -f1.x);
            var r2 = new Vector2(f2.y, -f2.x);
            Vector2 d = c2 - c1;
            float min = float.MaxValue;
            Axis(f1); Axis(r1); Axis(f2); Axis(r2);
            return min;

            void Axis(Vector2 axis)
            {
                float p1 = hl1 * Mathf.Abs(Vector2.Dot(f1, axis)) + hw1 * Mathf.Abs(Vector2.Dot(r1, axis));
                float p2 = hl2 * Mathf.Abs(Vector2.Dot(f2, axis)) + hw2 * Mathf.Abs(Vector2.Dot(r2, axis));
                min = Mathf.Min(min, p1 + p2 - Mathf.Abs(Vector2.Dot(d, axis)));
            }
        }

        /// <summary>
        /// Hard guard: moving a footprint from "now" to "next" is blocked by another footprint only if the move makes
        /// their overlap deeper (a vehicle may always move away from, or slide along, one it already touches).
        /// </summary>
        public static bool HardBlocks(Vector2 cNow, Vector2 fNow, Vector2 cNext, Vector2 fNext, float hl, float hw,
                                      Vector2 oc, Vector2 of, float ohl, float ohw)
        {
            float next = Penetration(cNext, fNext, hl, hw, oc, of, ohl, ohw);
            if (next <= 0f) return false;
            float now = Penetration(cNow, fNow, hl, hw, oc, of, ohl, ohw);
            return next > Mathf.Max(0f, now) + 1e-5f;
        }
    }
}
