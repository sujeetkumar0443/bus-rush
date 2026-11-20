using UnityEngine;

namespace BusRush.Gameplay
{
    /// <summary>
    /// The view contract the world art was rendered for. Reference screen 390x844 (origin top-left, y down):
    /// orthographic camera pitched 58 degrees, one cell = 34 px wide and 34 * sin(58) = 28.8 px tall,
    /// lot (9x10) at x 42..348, y 400..688. World space: lot centre at the origin, 1 cell = cellSize units,
    /// row 0 is the top row (+Z), column 0 the left one (-X).
    /// </summary>
    public static class BoardGeometry
    {
        public const float CameraPitch = 58f;
        public const float ReferenceWidth = 390f;
        public const float ReferenceHeight = 844f;
        public const float PixelsPerCell = 34f;
        public const float LotLeftPx = 42f;
        public const float LotTopPx = 400f;

        public static float SinPitch => Mathf.Sin(CameraPitch * Mathf.Deg2Rad);

        public static int Columns => GameConfig.TryGet() != null ? GameConfig.TryGet().gridWidth : 9;
        public static int Rows => GameConfig.TryGet() != null ? GameConfig.TryGet().gridHeight : 10;
        public static float CellSize => GameConfig.TryGet() != null ? GameConfig.TryGet().cellSize : 1f;

        /// <summary>World width visible on screen (the camera fits this width on every aspect).</summary>
        public static float ViewWidth => ReferenceWidth / PixelsPerCell * CellSize;

        /// <summary>Lot centre on the reference screen, in px from the top-left.</summary>
        public static Vector2 LotCenterPx => new Vector2(
            LotLeftPx + Columns * PixelsPerCell * 0.5f,
            LotTopPx + Rows * PixelsPerCell * SinPitch * 0.5f);

        /// <summary>Lot centre as viewport coordinates (0..1, origin bottom-left).</summary>
        public static Vector2 LotCenterViewport => new Vector2(
            LotCenterPx.x / ReferenceWidth,
            1f - LotCenterPx.y / ReferenceHeight);

        /// <summary>
        /// Ground rectangle (XZ) covered by the 780x1688 world art: exactly the reference camera view.
        /// x/y of the returned rect are world X/Z.
        /// </summary>
        public static Rect ArtGroundRect
        {
            get
            {
                float unitsPerPx = CellSize / PixelsPerCell;
                float width = ReferenceWidth * unitsPerPx;
                float length = ReferenceHeight * unitsPerPx / SinPitch;
                // Reference screen centre relative to the lot centre, projected onto the ground.
                Vector2 c = LotCenterPx;
                float cx = (ReferenceWidth * 0.5f - c.x) * unitsPerPx;
                float cz = (c.y - ReferenceHeight * 0.5f) * unitsPerPx / SinPitch;
                return new Rect(cx - width * 0.5f, cz - length * 0.5f, width, length);
            }
        }

        /// <summary>World position of a cell centre on the ground.</summary>
        public static Vector3 CellToWorld(float col, float row)
        {
            float cell = CellSize;
            return new Vector3((col - (Columns - 1) * 0.5f) * cell, 0f, ((Rows - 1) * 0.5f - row) * cell);
        }

        /// <summary>Centre of a vehicle's footprint (midpoint between head and tail cells).</summary>
        public static Vector3 VehicleCenter(VehicleData v)
        {
            Vector2Int h = v.Head, t = v.Tail;
            return CellToWorld((h.x + t.x) * 0.5f, (h.y + t.y) * 0.5f);
        }

        public static Vector2 LotSize => new Vector2(Columns * CellSize, Rows * CellSize);
    }
}
