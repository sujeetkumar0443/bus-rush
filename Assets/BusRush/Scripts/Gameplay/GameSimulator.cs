using System.Collections.Generic;

namespace BusRush.Gameplay
{
    /// <summary>The boarding window rule, shared by the game, the simulator, the generator check and the bot.</summary>
    public static class BoardingRule
    {
        /// <summary>
        /// Index of the earliest passenger of this colour among the first <paramref name="window"/> in queue order, or
        /// -1 if none of them has that colour.
        /// </summary>
        public static int EligibleIndex(IReadOnlyList<GameColor> queue, GameColor color, int window = GameTuning.FrontWindow)
        {
            int n = System.Math.Min(window, queue.Count);
            for (int i = 0; i < n; i++)
            {
                if (queue[i] == color) return i;
            }
            return -1;
        }
    }

    /// <summary>
    /// Pure-logic simulation of a whole level (no VIP, no boosters) with a simple greedy player. Used by the edit-mode
    /// tests and the generator's solvability check, and mirrored by the play-test bot.
    /// <para>Boarding (window rule): parked vehicles are served in bay order; each takes the earliest passenger of its
    /// colour among the first <see cref="GameTuning.FrontWindow"/> in the queue, until nobody more can board. Full
    /// vehicles leave and free their bay.</para>
    /// <para>Parking: if a bay is free, park the free vehicle whose colour is needed soonest in the queue; if that colour
    /// is not yet in the window, only while that still leaves a bay spare. Bays are taken lowest index first.</para>
    /// </summary>
    public static class GameSimulator
    {
        private sealed class Parked
        {
            public int id;
            public int bay;
            public GameColor color;
            public int capacity;
            public int filled;
        }

        public struct Result
        {
            public bool won;
            public int boarded;
            public int parkedVehicles;
            public string failReason;
        }

        /// <summary>A vehicle already parked in a bay (bay order), with the passengers already on board.</summary>
        public struct ParkedState
        {
            public GameColor color;
            public int capacity;
            public int filled;
        }

        /// <summary>Whole level from the start (every vehicle on the board, empty bays, the full queue).</summary>
        public static Result Run(LevelData level, int openBays = 4, int window = GameTuning.FrontWindow)
        {
            var all = new List<int>(level.vehicles.Count);
            for (int i = 0; i < level.vehicles.Count; i++) all.Add(i);
            return RunFrom(level.columns, level.rows, level.vehicles, all, null, level.queue, openBays, window);
        }

        /// <summary>
        /// From a state in the middle of a level (used by the boosters to keep the level winnable): the vehicles still on
        /// the board (indices into <paramref name="vehicles"/>, whose colours may have been changed), the vehicles parked
        /// in the bays, the remaining queue in order, and the number of usable bays.
        /// </summary>
        public static Result RunFrom(int columns, int rows, IList<VehicleData> vehicles, IEnumerable<int> onBoardIds,
                                     IEnumerable<ParkedState> parkedNow, IList<GameColor> queue, int openBays,
                                     int window = GameTuning.FrontWindow)
        {
            var board = new BoardState(columns, rows);
            var onBoard = new HashSet<int>();
            foreach (int i in onBoardIds)
            {
                board.Occupy(i, vehicles[i]);
                onBoard.Add(i);
            }

            var remaining = new List<GameColor>(queue);
            var parked = new List<Parked>(); // kept sorted by bay
            var result = new Result();
            if (parkedNow != null)
            {
                foreach (ParkedState p in parkedNow)
                {
                    if (p.filled >= p.capacity) continue; // full: leaving anyway
                    parked.Add(new Parked { id = -1 - parked.Count, bay = parked.Count, color = p.color, capacity = p.capacity, filled = p.filled });
                }
            }
            int total = remaining.Count;

            while (true)
            {
                result.boarded += BoardAll(parked, remaining, window);

                if (onBoard.Count == 0 && parked.Count == 0 && remaining.Count == 0)
                {
                    result.won = true;
                    return result;
                }

                int freeBays = openBays - parked.Count;
                int pick = -1;
                if (freeBays > 0 && remaining.Count > 0)
                {
                    pick = PickFree(vehicles, board, onBoard, v => NeededIn(remaining, 0, v.color));
                    if (pick >= 0 && NeededIn(remaining, 0, vehicles[pick].color) >= window && freeBays < 2) pick = -1;
                }

                if (pick < 0)
                {
                    result.failReason = remaining.Count == 0
                        ? $"queue empty but {onBoard.Count} vehicles left"
                        : $"stuck: window=[{string.Join(",", remaining.GetRange(0, System.Math.Min(window, remaining.Count)))}] " +
                          $"boarded={result.boarded}/{total} parked=[{Describe(parked)}] board={onBoard.Count}";
                    return result;
                }

                VehicleData pv = vehicles[pick];
                board.Free(pick, pv);
                onBoard.Remove(pick);
                int bay = 0;
                while (parked.Exists(p => p.bay == bay)) bay++;
                parked.Add(new Parked { id = pick, bay = bay, color = pv.color, capacity = pv.Capacity });
                parked.Sort((a, b) => a.bay.CompareTo(b.bay));
                result.parkedVehicles++;
            }
        }

        /// <summary>
        /// Boarding passes (bay order, earliest eligible passenger of the vehicle's colour) until nobody can board; full
        /// vehicles leave. Returns the number boarded.
        /// </summary>
        private static int BoardAll(List<Parked> parked, List<GameColor> remaining, int window)
        {
            int boarded = 0;
            bool progress = true;
            while (progress)
            {
                progress = false;
                foreach (Parked p in parked)
                {
                    if (p.filled >= p.capacity) continue;
                    if (BoardOne(remaining, p.color, window) < 0) continue;
                    p.filled++;
                    boarded++;
                    progress = true;
                }
                parked.RemoveAll(p => p.filled >= p.capacity);
            }
            return boarded;
        }

        /// <summary>
        /// One passenger boards a parked vehicle of this colour with space: the earliest of that colour in the window is
        /// removed from the queue (the people behind close the gap). Returns their queue index, or -1 if nobody can board.
        /// </summary>
        public static int BoardOne(List<GameColor> queue, GameColor vehicleColor, int window = GameTuning.FrontWindow)
        {
            int idx = BoardingRule.EligibleIndex(queue, vehicleColor, window);
            if (idx >= 0) queue.RemoveAt(idx);
            return idx;
        }

        /// <summary>Free vehicle with the lowest non-negative score (ties: lowest index).</summary>
        private static int PickFree(IList<VehicleData> vehicles, BoardState board, HashSet<int> onBoard, System.Func<VehicleData, int> score)
        {
            int best = -1, bestScore = int.MaxValue;
            foreach (int id in onBoard)
            {
                VehicleData v = vehicles[id];
                if (!board.IsPathClear(v)) continue;
                int s = score(v);
                if (s < 0) continue;
                if (s < bestScore || (s == bestScore && id < best))
                {
                    best = id;
                    bestScore = s;
                }
            }
            return best;
        }

        /// <summary>Queue distance to the next passenger of this colour (int.MaxValue - 1 if none left).</summary>
        public static int NeededIn(List<GameColor> queue, int from, GameColor color)
        {
            for (int i = from; i < queue.Count; i++)
            {
                if (queue[i] == color) return i - from;
            }
            return int.MaxValue - 1;
        }

        private static string Describe(List<Parked> parked)
        {
            var parts = new List<string>();
            foreach (Parked p in parked) parts.Add($"bay{p.bay} {p.color} {p.filled}/{p.capacity}");
            return string.Join(", ", parts);
        }
    }
}
