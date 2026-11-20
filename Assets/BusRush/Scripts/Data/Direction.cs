using UnityEngine;

namespace BusRush
{
    /// <summary>Board directions. Up = toward the road (top of the screen); rows count downward from the top.</summary>
    public enum Direction
    {
        Up,
        Down,
        Left,
        Right
    }

    public static class DirectionExtensions
    {
        public static readonly Direction[] All = { Direction.Up, Direction.Down, Direction.Left, Direction.Right };

        /// <summary>Step in board coordinates (col, row). Row 0 is the top row, so Up is row - 1.</summary>
        public static Vector2Int ToOffset(this Direction d)
        {
            return d switch
            {
                Direction.Up => new Vector2Int(0, -1),
                Direction.Down => new Vector2Int(0, 1),
                Direction.Left => new Vector2Int(-1, 0),
                _ => new Vector2Int(1, 0)
            };
        }

        /// <summary>Unit vector in world space (board lies on XZ, Up = +Z).</summary>
        public static Vector3 ToWorld(this Direction d)
        {
            return d switch
            {
                Direction.Up => Vector3.forward,
                Direction.Down => Vector3.back,
                Direction.Left => Vector3.left,
                _ => Vector3.right
            };
        }

        /// <summary>Yaw that turns a +Z-facing model to face this direction.</summary>
        public static float ToYaw(this Direction d)
        {
            return d switch
            {
                Direction.Up => 0f,
                Direction.Right => 90f,
                Direction.Down => 180f,
                _ => 270f
            };
        }

        public static Direction Opposite(this Direction d)
        {
            return d switch
            {
                Direction.Up => Direction.Down,
                Direction.Down => Direction.Up,
                Direction.Left => Direction.Right,
                _ => Direction.Left
            };
        }
    }
}
