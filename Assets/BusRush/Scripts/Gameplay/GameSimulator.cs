using System.Collections.Generic;

namespace BusRush.Gameplay
{
    /// <summary>
    /// Pure-logic simulation of a whole level (no VIP, no boosters) with a simple greedy player. Used by the edit-mode
    /// tests and mirrored by the play-test bot.
    /// <para>Policy: board while the queue front matches a parked vehicle with space. Otherwise, if a bay is free, park a
    /// free vehicle of the front's colour; failing that, park the free vehicle whose colour is needed soonest in the
    /// queue, but only while that still leaves a bay spare. Full vehicles leave and free their bay.</para>
    /// </summary>
    public static class GameSimulator
    {
        private sealed class Parked
        {
            public int id;
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

        public static Result Run(LevelData level, int openBays = 4)
        {
            var board = new BoardState(level.columns, level.rows);
            var onBoard = new HashSet<int>();
            for (int i = 0; i < level.vehicles.Count; i++)
            {
                board.Occupy(i, level.vehicles[i]);
                onBoard.Add(i);
            }

            var parked = new List<Parked>();
            int qi = 0;
            var result = new Result();

            while (true)
            {
                // Board as many as possible.
                while (qi < level.queue.Count)
                {
                    Parked target = BestParked(parked, level.queue[qi]);
                    if (target == null) break;
                    target.filled++;
                    qi++;
                    result.boarded++;
                    if (target.filled >= target.capacity) parked.Remove(target);
                }

                if (onBoard.Count == 0 && parked.Count == 0 && qi >= level.queue.Count)
                {
                    result.won = true;
                    return result;
                }

                int freeBays = openBays - parked.Count;
                int pick = -1;
                if (freeBays > 0 && qi < level.queue.Count)
                {
                    GameColor front = level.queue[qi];
                    pick = PickFree(level, board, onBoard, v => v.color == front ? 0 : -1);
                    if (pick < 0 && freeBays > 1)
                    {
                        pick = PickFree(level, board, onBoard, v => NeededIn(level.queue, qi, v.color));
                    }
                }

                if (pick < 0)
                {
                    result.failReason = qi >= level.queue.Count
                        ? $"queue empty but {onBoard.Count} vehicles left"
                        : $"stuck: front={level.queue[qi]} boarded={qi}/{level.queue.Count} parked=[{Describe(parked)}] board={onBoard.Count}";
                    return result;
                }

                VehicleData pv = level.vehicles[pick];
                board.Free(pick, pv);
                onBoard.Remove(pick);
                parked.Add(new Parked { id = pick, color = pv.color, capacity = pv.Capacity });
                result.parkedVehicles++;
            }
        }

        /// <summary>The parked vehicle of this colour with space that is closest to full (fill one at a time).</summary>
        private static Parked BestParked(List<Parked> parked, GameColor color)
        {
            Parked best = null;
            foreach (Parked p in parked)
            {
                if (p.color != color || p.filled >= p.capacity) continue;
                if (best == null || p.filled > best.filled) best = p;
            }
            return best;
        }

        /// <summary>Free vehicle with the lowest non-negative score (ties: lowest index).</summary>
        private static int PickFree(LevelData level, BoardState board, HashSet<int> onBoard, System.Func<VehicleData, int> score)
        {
            int best = -1, bestScore = int.MaxValue;
            foreach (int id in onBoard)
            {
                VehicleData v = level.vehicles[id];
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
            foreach (Parked p in parked) parts.Add($"{p.color} {p.filled}/{p.capacity}");
            return string.Join(", ", parts);
        }
    }
}
