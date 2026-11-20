using System;
using System.Collections.Generic;
using UnityEngine;

namespace BusRush
{
    /// <summary>
    /// One vehicle on the board. The head is the cell nearest the exit; the body occupies
    /// <see cref="Length"/> cells behind it (opposite to <see cref="direction"/>).
    /// </summary>
    [Serializable]
    public sealed class VehicleData
    {
        public VehicleType type;
        public GameColor color;
        public int headCol;
        public int headRow;
        public Direction direction;

        public VehicleData(VehicleType type, GameColor color, int headCol, int headRow, Direction direction)
        {
            this.type = type;
            this.color = color;
            this.headCol = headCol;
            this.headRow = headRow;
            this.direction = direction;
        }

        public int Length => VehicleSpecs.Length(type);

        public int Capacity => VehicleSpecs.Capacity(type);

        public Vector2Int Head => new Vector2Int(headCol, headRow);

        /// <summary>Occupied cells, head first.</summary>
        public IEnumerable<Vector2Int> Cells()
        {
            Vector2Int back = -direction.ToOffset();
            Vector2Int c = Head;
            for (int i = 0; i < Length; i++)
            {
                yield return c;
                c += back;
            }
        }

        /// <summary>Cell at the far end of the body (the tail).</summary>
        public Vector2Int Tail => Head - direction.ToOffset() * (Length - 1);

        public override string ToString() => $"{color} {type} head=({headCol},{headRow}) {direction}";
    }

    /// <summary>Length and capacity per vehicle type, from GameConfig when available.</summary>
    public static class VehicleSpecs
    {
        public static int Length(VehicleType type)
        {
            GameConfig cfg = GameConfig.TryGet();
            if (cfg != null) return cfg.GetLength(type);
            return type == VehicleType.Bus ? 3 : 2;
        }

        public static int Capacity(VehicleType type)
        {
            GameConfig cfg = GameConfig.TryGet();
            if (cfg != null) return cfg.GetCapacity(type);
            return type switch
            {
                VehicleType.Bus => 8,
                VehicleType.Van => 6,
                _ => 4
            };
        }
    }
}
