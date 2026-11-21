using System;
using System.Collections.Generic;
using UnityEngine;

namespace BusRush.Gameplay
{
    /// <summary>
    /// Builds solvable boards by reverse placement. Vehicles are placed one at a time and a placement is only
    /// accepted if its exit path is clear of every vehicle placed earlier. The exit order is therefore the reverse
    /// of the placement order: each vehicle's path only crosses vehicles that leave before it.
    /// Deterministic for a given (level, seed).
    /// </summary>
    public static class LevelGenerator
    {
        public struct Settings
        {
            public int vehicleCount;
            public float vanShare;
            public float busShare;
            public int colorCount;
            /// <summary>Maximum share of board cells that may be occupied.</summary>
            public float maxFill;
            /// <summary>How strongly placements that block earlier vehicles are preferred (puzzle depth).</summary>
            public float blockBias;
        }

        public static int SeedFor(int level) => level * 7919;

        /// <summary>Generator settings from the difficulty curve in <see cref="LevelPlan"/>.</summary>
        public static Settings SettingsFor(int level)
        {
            LevelSpec spec = LevelPlan.Get(level);
            return new Settings
            {
                vehicleCount = spec.vehicleCount,
                vanShare = spec.vanShare,
                busShare = spec.busShare,
                colorCount = spec.colorCount,
                maxFill = spec.density,
                blockBias = spec.blockBias
            };
        }

        public static LevelData Generate(int level) => Generate(level, SeedFor(level));

        /// <summary>Extra generation attempts (seed + k * SolvableRetryStride) if a board fails the window-rule check.</summary>
        public const int SolvableAttempts = 24;
        public const int SolvableRetryStride = 104729;

        /// <summary>
        /// Generates the level and checks it with <see cref="GameSimulator"/> under the boarding-window rule (4 open bays).
        /// Attempt 0 uses the given seed, so boards that already pass are unchanged; otherwise the next seeds are tried.
        /// </summary>
        public static LevelData Generate(int level, int seed, int columns = 9, int rows = 10)
        {
            LevelData data = null;
            for (int attempt = 0; attempt < SolvableAttempts; attempt++)
            {
                data = GenerateOnce(level, seed + attempt * SolvableRetryStride, columns, rows);
                if (!GameSimulator.Run(data).won) continue;
                if (attempt > 0) Debug.Log($"[LevelGenerator] level {level}: seed {seed} failed the window-rule check; using attempt {attempt}.");
                return data;
            }
            Debug.LogError($"[LevelGenerator] level {level}: no board passed the window-rule check in {SolvableAttempts} attempts.");
            return data;
        }

        private static LevelData GenerateOnce(int level, int seed, int columns, int rows)
        {
            Settings s = SettingsFor(level);
            var rng = new System.Random(seed);
            var board = new BoardState(columns, rows);
            var placed = new List<VehicleData>();
            int maxCells = Mathf.FloorToInt(s.maxFill * columns * rows);
            int usedCells = 0;

            for (int i = 0; i < s.vehicleCount; i++)
            {
                VehicleType type = PickType(rng, s);
                VehicleData v = Place(rng, board, placed, type, s.blockBias, maxCells - usedCells);
                if (v == null && type != VehicleType.Car)
                {
                    v = Place(rng, board, placed, VehicleType.Car, s.blockBias, maxCells - usedCells);
                }
                if (v == null) break; // board is as full as it can get

                board.Occupy(placed.Count, v);
                placed.Add(v);
                usedCells += v.Length;
            }

            AssignColors(rng, placed, s.colorCount);

            // Generator exit order = reverse placement (each path only crosses vehicles that leave earlier).
            var generatorExitOrder = new List<int>();
            for (int i = placed.Count - 1; i >= 0; i--) generatorExitOrder.Add(i);

            List<GameColor> queue = BuildQueue(rng, placed, generatorExitOrder, columns, rows, out List<int> parkOrder);

            return new LevelData
            {
                level = level,
                seed = seed,
                worldIndex = LevelPlan.WorldIndex(level),
                columns = columns,
                rows = rows,
                vehicles = placed,
                exitOrder = parkOrder,
                queue = queue
            };
        }

        public const int QueueChunkVehicles = 3;
        public const int QueueGroupSize = 4;

        /// <summary>
        /// Passenger queue built together with a play-through of the greedy player (see <see cref="GameSimulator"/>), in
        /// chunks of up to 3 parked vehicles, earliest generator exit order first:
        /// <list type="bullet">
        /// <item>Each chunk starts with one "trigger" passenger per vehicle, in a colour no parked vehicle can take, so the
        /// player has to park exactly that vehicle: the lowest-index free vehicle of that colour, which is the one the
        /// greedy player picks.</item>
        /// <item>The chunk's remaining passengers follow in groups of 4, shuffled (seeded) across the chunk's vehicles.</item>
        /// </list>
        /// Per colour the queue equals the total vehicle capacity. Winnability under the boarding-window rule is then
        /// checked with <see cref="GameSimulator"/> in <see cref="Generate(int,int,int,int)"/>.
        /// </summary>
        private static List<GameColor> BuildQueue(System.Random rng, List<VehicleData> vehicles, List<int> exitOrder,
                                                  int columns, int rows, out List<int> parkOrder)
        {
            var queue = new List<GameColor>();
            parkOrder = new List<int>();
            var board = new BoardState(columns, rows);
            var onBoard = new HashSet<int>();
            for (int i = 0; i < vehicles.Count; i++)
            {
                board.Occupy(i, vehicles[i]);
                onBoard.Add(i);
            }

            while (onBoard.Count > 0)
            {
                // Park up to 3 vehicles, each with a colour no other vehicle in the chunk has.
                var chunk = new List<int>();
                var chunkColors = new HashSet<GameColor>();
                while (chunk.Count < QueueChunkVehicles)
                {
                    int pick = -1;
                    foreach (int id in exitOrder) // earliest exit first
                    {
                        if (!onBoard.Contains(id)) continue;
                        VehicleData v = vehicles[id];
                        if (chunkColors.Contains(v.color) || !board.IsPathClear(v)) continue;
                        pick = LowestFreeOfColor(vehicles, board, onBoard, v.color); // what the greedy player parks
                        break;
                    }
                    if (pick < 0) break;

                    VehicleData pv = vehicles[pick];
                    board.Free(pick, pv);
                    onBoard.Remove(pick);
                    chunk.Add(pick);
                    chunkColors.Add(pv.color);
                    parkOrder.Add(pick);
                    queue.Add(pv.color); // trigger passenger: makes the player park this vehicle
                }

                if (chunk.Count == 0)
                {
                    throw new System.InvalidOperationException("Queue builder found no free vehicle; board is not solvable.");
                }

                // Remaining seats of the chunk in shuffled groups of 4.
                var groups = new List<List<GameColor>>();
                foreach (int id in chunk)
                {
                    int remaining = vehicles[id].Capacity - 1;
                    for (int g = 0; g < remaining; g += QueueGroupSize)
                    {
                        int n = Mathf.Min(QueueGroupSize, remaining - g);
                        var group = new List<GameColor>(n);
                        for (int k = 0; k < n; k++) group.Add(vehicles[id].color);
                        groups.Add(group);
                    }
                }
                for (int i = groups.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (groups[i], groups[j]) = (groups[j], groups[i]);
                }
                foreach (List<GameColor> g in groups) queue.AddRange(g);
            }
            return queue;
        }

        private static int LowestFreeOfColor(List<VehicleData> vehicles, BoardState board, HashSet<int> onBoard, GameColor color)
        {
            int best = -1;
            foreach (int id in onBoard)
            {
                if (vehicles[id].color == color && board.IsPathClear(vehicles[id]) && (best < 0 || id < best)) best = id;
            }
            return best;
        }

        private static VehicleType PickType(System.Random rng, Settings s)
        {
            double r = rng.NextDouble();
            if (r < s.busShare) return VehicleType.Bus;
            if (r < s.busShare + s.vanShare) return VehicleType.Van;
            return VehicleType.Car;
        }

        /// <summary>Evaluates every head cell and direction; picks a valid one weighted toward blocking earlier vehicles.</summary>
        private static VehicleData Place(System.Random rng, BoardState board, List<VehicleData> placed, VehicleType type,
                                         float blockBias, int cellsLeft)
        {
            if (VehicleSpecs.Length(type) > cellsLeft) return null;

            // Cells each earlier vehicle needs to drive through.
            var pathOwners = new Dictionary<Vector2Int, int>();
            for (int i = 0; i < placed.Count; i++)
            {
                foreach (Vector2Int c in board.PathCells(placed[i]))
                {
                    pathOwners.TryGetValue(c, out int n);
                    pathOwners[c] = n + 1;
                }
            }

            var candidates = new List<(VehicleData v, double w)>();
            double total = 0;
            for (int row = 0; row < board.Rows; row++)
            {
                for (int col = 0; col < board.Columns; col++)
                {
                    foreach (Direction d in DirectionExtensions.All)
                    {
                        var v = new VehicleData(type, GameColor.Red, col, row, d);
                        if (!board.CanPlace(v) || !board.IsPathClear(v)) continue;

                        int blocks = 0;
                        foreach (Vector2Int c in v.Cells())
                        {
                            if (pathOwners.TryGetValue(c, out int n)) blocks += n;
                        }
                        double w = 1.0 + blockBias * blocks;
                        candidates.Add((v, w));
                        total += w;
                    }
                }
            }

            if (candidates.Count == 0) return null;
            double pick = rng.NextDouble() * total;
            foreach ((VehicleData v, double w) in candidates)
            {
                pick -= w;
                if (pick <= 0) return v;
            }
            return candidates[candidates.Count - 1].v;
        }

        /// <summary>Picks <paramref name="colorCount"/> colours and balances total capacity across them.</summary>
        private static void AssignColors(System.Random rng, List<VehicleData> vehicles, int colorCount)
        {
            var palette = new List<GameColor>((GameColor[])Enum.GetValues(typeof(GameColor)));
            for (int i = palette.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (palette[i], palette[j]) = (palette[j], palette[i]);
            }
            int k = Mathf.Clamp(colorCount, 1, Mathf.Min(palette.Count, Mathf.Max(1, vehicles.Count)));
            var load = new int[k];

            // Largest vehicles first, each to the colour with the least capacity so far (random tie-break).
            var order = new List<int>();
            for (int i = 0; i < vehicles.Count; i++) order.Add(i);
            order.Sort((a, b) =>
            {
                int c = vehicles[b].Capacity.CompareTo(vehicles[a].Capacity);
                return c != 0 ? c : a.CompareTo(b);
            });

            foreach (int i in order)
            {
                int best = 0;
                int ties = 1;
                for (int c = 1; c < k; c++)
                {
                    if (load[c] < load[best])
                    {
                        best = c;
                        ties = 1;
                    }
                    else if (load[c] == load[best] && rng.Next(++ties) == 0)
                    {
                        best = c;
                    }
                }
                vehicles[i].color = palette[best];
                load[best] += vehicles[i].Capacity;
            }
        }
    }

    /// <summary>Greedy solver: repeatedly removes any vehicle whose path is clear. Removal never blocks anything,
    /// so if this gets stuck the board is unsolvable.</summary>
    public static class LevelSolver
    {
        public static bool IsSolvable(LevelData level, out List<int> exitOrder)
        {
            exitOrder = new List<int>();
            var board = new BoardState(level.columns, level.rows);
            for (int i = 0; i < level.vehicles.Count; i++)
            {
                if (!board.CanPlace(level.vehicles[i])) return false;
                board.Occupy(i, level.vehicles[i]);
            }

            var remaining = new List<int>();
            for (int i = 0; i < level.vehicles.Count; i++) remaining.Add(i);

            bool progress = true;
            while (remaining.Count > 0 && progress)
            {
                progress = false;
                for (int k = remaining.Count - 1; k >= 0; k--)
                {
                    int id = remaining[k];
                    if (!board.IsPathClear(level.vehicles[id])) continue;
                    board.Free(id, level.vehicles[id]);
                    exitOrder.Add(id);
                    remaining.RemoveAt(k);
                    progress = true;
                }
            }
            return remaining.Count == 0;
        }

        /// <summary>Vehicles that can leave right now on a fresh board.</summary>
        public static int InitiallyFree(LevelData level)
        {
            var board = new BoardState(level.columns, level.rows);
            for (int i = 0; i < level.vehicles.Count; i++) board.Occupy(i, level.vehicles[i]);
            int n = 0;
            foreach (VehicleData v in level.vehicles) if (board.IsPathClear(v)) n++;
            return n;
        }
    }
}
