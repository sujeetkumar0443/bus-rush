using System;
using System.Collections.Generic;
using System.Linq;

namespace BusRush.Gameplay
{
    /// <summary>A snapshot of a level in progress, enough to check (with <see cref="GameSimulator"/>) that it is winnable.</summary>
    public sealed class LevelSnapshot
    {
        public int columns;
        public int rows;
        /// <summary>All vehicles of the level by id (colours as they are now).</summary>
        public List<VehicleData> vehicles = new List<VehicleData>();
        /// <summary>Ids of the vehicles still standing in the lot.</summary>
        public List<int> onBoard = new List<int>();
        /// <summary>Generator park order (ids), used as "the order the lot vehicles will likely be needed".</summary>
        public List<int> exitOrder = new List<int>();
        /// <summary>Parked vehicles in bay order, with passengers already on board.</summary>
        public List<GameSimulator.ParkedState> parked = new List<GameSimulator.ParkedState>();
        /// <summary>Waiting passengers, front first (not those already walking to a vehicle).</summary>
        public List<GameColor> queue = new List<GameColor>();
        /// <summary>Bays the player can use right now.</summary>
        public int usableBays = 4;

        public bool IsWinnable(IList<GameColor> queueOverride = null, IList<VehicleData> vehiclesOverride = null) =>
            GameSimulator.RunFrom(columns, rows, vehiclesOverride ?? vehicles, onBoard, parked, queueOverride ?? queue,
                                  usableBays).won;
    }

    /// <summary>
    /// Booster rules as pure functions (no scene): what VIP, Arrange and Shuffle change, and that the level stays winnable.
    /// <see cref="BoosterController"/> applies the results in the game.
    /// </summary>
    public static class BoosterLogic
    {
        // ---------------------------------------------------------------- VIP

        /// <summary>The VIP bay of one level: locked until the booster opens it, once.</summary>
        public sealed class VipLatch
        {
            public bool Open { get; private set; }

            /// <summary>Opens the VIP bay; false (nothing to spend) if it is already open this level.</summary>
            public bool TryOpen()
            {
                if (Open) return false;
                Open = true;
                return true;
            }
        }

        // ---------------------------------------------------------------- ARRANGE

        /// <summary>
        /// Arrange: the front window (first <paramref name="window"/>) gets passengers for the parked vehicles with free
        /// seats (bay order, up to their free seats), then everyone else is grouped by colour in the order the lot
        /// vehicles will likely be needed (generator park order, a vehicle's capacity at a time), leftovers in their old
        /// order. Same passengers (multiset), different order. Candidates are tried best first and the first winnable one
        /// is returned: the full arrangement, then only the front window rearranged, then the unchanged queue.
        /// </summary>
        public static List<GameColor> Arrange(LevelSnapshot s, int window, out bool changed)
        {
            List<GameColor> original = s.queue;
            List<GameColor> front = FrontFor(s, window, out Dictionary<GameColor, int> pool);

            var full = new List<GameColor>(front);
            var restPool = new Dictionary<GameColor, int>(pool);
            foreach (int id in s.exitOrder)
            {
                if (!s.onBoard.Contains(id)) continue;
                VehicleData v = s.vehicles[id];
                Take(restPool, v.color, v.Capacity, full);
            }
            AppendLeftovers(original, full, restPool);

            var frontOnly = new List<GameColor>(front);
            AppendLeftovers(original, frontOnly, new Dictionary<GameColor, int>(pool));

            foreach (List<GameColor> candidate in new[] { full, frontOnly })
            {
                if (candidate.SequenceEqual(original)) continue;
                if (!s.IsWinnable(candidate)) continue;
                changed = true;
                return candidate;
            }
            changed = false;
            return new List<GameColor>(original);
        }

        /// <summary>Front window for the parked vehicles with free seats; pool = passengers not yet placed, by colour.</summary>
        private static List<GameColor> FrontFor(LevelSnapshot s, int window, out Dictionary<GameColor, int> pool)
        {
            pool = Counts(s.queue);
            var front = new List<GameColor>();
            foreach (GameSimulator.ParkedState p in s.parked)
            {
                int free = p.capacity - p.filled;
                if (free <= 0) continue;
                Take(pool, p.color, Math.Min(free, window - front.Count), front);
                if (front.Count >= window) break;
            }
            return front;
        }

        /// <summary>After the placed prefix, the remaining passengers in their original order (skipping those placed).</summary>
        private static void AppendLeftovers(List<GameColor> original, List<GameColor> into, Dictionary<GameColor, int> pool)
        {
            // 'pool' = what is still unplaced, by colour; take them in their original queue order.
            foreach (GameColor c in original)
            {
                if (!pool.TryGetValue(c, out int n) || n <= 0) continue;
                pool[c] = n - 1;
                into.Add(c);
            }
        }

        private static void Take(Dictionary<GameColor, int> pool, GameColor c, int count, List<GameColor> into)
        {
            if (count <= 0 || !pool.TryGetValue(c, out int n) || n <= 0) return;
            int k = Math.Min(n, count);
            for (int i = 0; i < k; i++) into.Add(c);
            pool[c] = n - k;
        }

        public static Dictionary<GameColor, int> Counts(IEnumerable<GameColor> colours)
        {
            var d = new Dictionary<GameColor, int>();
            foreach (GameColor c in colours) d[c] = d.TryGetValue(c, out int n) ? n + 1 : 1;
            return d;
        }

        // ---------------------------------------------------------------- SHUFFLE

        /// <summary>
        /// Shuffle: new colours for the vehicles still in the lot, permuted among vehicles of the same type (same
        /// capacity), so every colour keeps exactly the seats its passengers need. Up to <paramref name="retries"/>
        /// random permutations that actually change something; the first winnable one is returned as colours by vehicle
        /// id (only lot vehicles differ). Null if none works (the caller keeps the colours and refunds).
        /// </summary>
        public static Dictionary<int, GameColor> Shuffle(LevelSnapshot s, Random rng, int retries)
        {
            var groups = s.onBoard.GroupBy(id => s.vehicles[id].type).Select(g => g.ToList()).Where(g => g.Count > 1).ToList();
            if (groups.Count == 0) return null;

            for (int attempt = 0; attempt < retries; attempt++)
            {
                var colours = new Dictionary<int, GameColor>();
                bool any = false;
                foreach (List<int> ids in groups)
                {
                    var palette = ids.Select(id => s.vehicles[id].color).ToList();
                    for (int i = palette.Count - 1; i > 0; i--)
                    {
                        int j = rng.Next(i + 1);
                        (palette[i], palette[j]) = (palette[j], palette[i]);
                    }
                    for (int k = 0; k < ids.Count; k++)
                    {
                        colours[ids[k]] = palette[k];
                        any |= palette[k] != s.vehicles[ids[k]].color;
                    }
                }
                if (!any) continue;

                var trial = s.vehicles.Select((v, id) => colours.TryGetValue(id, out GameColor c) && c != v.color
                    ? new VehicleData(v.type, c, v.headCol, v.headRow, v.direction)
                    : v).ToList();
                if (s.IsWinnable(null, trial)) return colours;
            }
            return null;
        }

        /// <summary>Seats per colour of the given vehicles (what the passengers per colour must match).</summary>
        public static Dictionary<GameColor, int> SeatsPerColour(IEnumerable<VehicleData> vehicles)
        {
            var d = new Dictionary<GameColor, int>();
            foreach (VehicleData v in vehicles) d[v.color] = d.TryGetValue(v.color, out int n) ? n + v.Capacity : v.Capacity;
            return d;
        }
    }
}
