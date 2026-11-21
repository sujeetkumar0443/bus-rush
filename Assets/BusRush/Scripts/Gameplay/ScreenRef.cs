using UnityEngine;

namespace BusRush.Gameplay
{
    /// <summary>
    /// Reference-screen layout (390x844 px, origin top-left, y down) of the road loop and the parking bays, and the
    /// conversion between reference pixels and the ground plane.
    /// <para>
    /// <see cref="RefToWorld(float,float)"/> returns the ground point that the fitted gameplay camera shows at the
    /// reference pixel: the art and the board are fixed in world space and <see cref="CameraFitter"/> only changes how
    /// much of it is visible, so for the orthographic camera this is the same point a ray through the matching viewport
    /// position would hit, on every aspect ratio. It is computed analytically so it also works without a camera
    /// (edit-mode tests, gizmos).
    /// </para>
    /// </summary>
    public static class ScreenRef
    {
        public const float Width = BoardGeometry.ReferenceWidth;
        public const float Height = BoardGeometry.ReferenceHeight;

        /// <summary>
        /// Road loop lanes (City v2 art): arrivals drive on the dashed lane between the bay slots (y 268..374) and the
        /// lot (y 392+), so they never cut through parked vehicles; the bottom lane follows the dashed line below the lot.
        /// </summary>
        public const float TopY = 385f;
        public const float BotY = 719f;

        /// <summary>Road strip above the bays: full vehicles leave forward onto it and drive off to the right.</summary>
        public const float DepartY = 254f;
        public const float LeftX = 19f;
        public const float RightX = 371f;

        /// <summary>Parking bays: 8 slots, vehicles park facing up, centred in the slot (BayY: boarding / label reference).</summary>
        public const float BayY = 292f;
        public const float BayX0 = 37f;
        public const float BaySpacing = 45f;
        public const int BayCount = 8;

        /// <summary>Visual scale of vehicles (see VehicleView); used so the visible front stops at BayY.</summary>
        public const float VehicleVisualScale = 0.92f;

        public static float BayX(int index) => BayX0 + index * BaySpacing;

        public static float UnitsPerPx => BoardGeometry.CellSize / BoardGeometry.PixelsPerCell;

        /// <summary>Vertical reference px per world unit along Z (foreshortened by the camera pitch).</summary>
        public static float PxPerUnitZ => BoardGeometry.PixelsPerCell * BoardGeometry.SinPitch / BoardGeometry.CellSize;

        public static Vector3 RefToWorld(Vector2 p) => RefToWorld(p.x, p.y);

        public static Vector3 RefToWorld(float x, float y)
        {
            Vector2 c = BoardGeometry.LotCenterPx;
            float u = UnitsPerPx;
            return new Vector3((x - c.x) * u, 0f, (c.y - y) * u / BoardGeometry.SinPitch);
        }

        public static Vector2 WorldToRef(Vector3 w)
        {
            Vector2 c = BoardGeometry.LotCenterPx;
            float u = UnitsPerPx;
            return new Vector2(c.x + w.x / u, c.y - w.z * BoardGeometry.SinPitch / u);
        }

        /// <summary>Reference px of a (possibly fractional) cell centre.</summary>
        public static Vector2 CellToRef(float col, float row)
        {
            return new Vector2(
                BoardGeometry.LotLeftPx + BoardGeometry.PixelsPerCell * (col + 0.5f),
                BoardGeometry.LotTopPx + BoardGeometry.PixelsPerCell * BoardGeometry.SinPitch * (row + 0.5f));
        }

        /// <summary>Visible on-screen length (reference px, vertical) of a vehicle of the given type when facing up.</summary>
        public static float VehicleLengthPx(VehicleType type)
        {
            return VehicleSpecs.Length(type) * BoardGeometry.PixelsPerCell * BoardGeometry.SinPitch * VehicleVisualScale;
        }

        /// <summary>Painted bay slot (City v2 art): y 268..374.</summary>
        public const float SlotTopY = 268f;
        public const float SlotBottomY = 374f;

        /// <summary>
        /// Reference y of a parked vehicle's centre: centred in the painted slot, so even a bus (80 px) keeps ~12 px
        /// clear of the arrival lane behind it and of the departure strip in front.
        /// </summary>
        public static float ParkCenterY(VehicleType type) => (SlotTopY + SlotBottomY) * 0.5f;
    }
}
