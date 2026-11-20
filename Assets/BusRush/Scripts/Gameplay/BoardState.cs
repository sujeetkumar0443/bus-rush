using System;
using System.Collections.Generic;
using UnityEngine;

namespace BusRush.Gameplay
{
    /// <summary>Occupancy grid of the lot. Cells hold the id of the vehicle on them, or -1.</summary>
    public sealed class BoardState
    {
        public const int Empty = -1;

        private readonly int[] cells;

        public int Columns { get; }
        public int Rows { get; }

        public BoardState(int columns, int rows)
        {
            Columns = columns;
            Rows = rows;
            cells = new int[columns * rows];
            Array.Fill(cells, Empty);
        }

        public bool InBounds(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < Columns && c.y < Rows;

        public int OccupantAt(Vector2Int c) => InBounds(c) ? cells[c.y * Columns + c.x] : Empty;

        public bool CanPlace(VehicleData v)
        {
            foreach (Vector2Int c in v.Cells())
            {
                if (!InBounds(c) || cells[c.y * Columns + c.x] != Empty) return false;
            }
            return true;
        }

        public void Occupy(int id, VehicleData v)
        {
            if (!CanPlace(v)) throw new InvalidOperationException($"Cannot place vehicle {id}: {v}");
            foreach (Vector2Int c in v.Cells()) cells[c.y * Columns + c.x] = id;
        }

        public void Free(int id, VehicleData v)
        {
            foreach (Vector2Int c in v.Cells())
            {
                if (InBounds(c) && cells[c.y * Columns + c.x] == id) cells[c.y * Columns + c.x] = Empty;
            }
        }

        /// <summary>Cells from head + direction up to the board edge.</summary>
        public IEnumerable<Vector2Int> PathCells(VehicleData v)
        {
            Vector2Int step = v.direction.ToOffset();
            for (Vector2Int c = v.Head + step; InBounds(c); c += step) yield return c;
        }

        public bool IsPathClear(VehicleData v) => IsPathClear(v, out _);

        /// <summary>True if nothing stands between the head and the board edge; otherwise returns the first blocker.</summary>
        public bool IsPathClear(VehicleData v, out int blockerId)
        {
            foreach (Vector2Int c in PathCells(v))
            {
                int occ = cells[c.y * Columns + c.x];
                if (occ != Empty)
                {
                    blockerId = occ;
                    return false;
                }
            }
            blockerId = Empty;
            return true;
        }

        public int OccupiedCount()
        {
            int n = 0;
            foreach (int c in cells) if (c != Empty) n++;
            return n;
        }
    }
}
