using System.Collections.Generic;
using UnityEngine;

namespace BusRush.Gameplay
{
    /// <summary>
    /// Waypoints (reference px, for the vehicle centre) from the board to a bay and from a bay off screen.
    /// Up: straight up to TOPY. Down: down to BOTY, to the nearer side lane, up to TOPY. Left/Right: to LX/RX, up to TOPY.
    /// Then along TOPY to the bay x and up into the bay (front stops at BAY_Y).
    /// </summary>
    public static class RouteBuilder
    {
        /// <summary>How far past the right screen edge departing vehicles drive.</summary>
        public const float OffscreenRightPx = 160f;

        public static Vector2 VehicleCenterRef(VehicleData v)
        {
            Vector2Int h = v.Head, t = v.Tail;
            return ScreenRef.CellToRef((h.x + t.x) * 0.5f, (h.y + t.y) * 0.5f);
        }

        public static Vector2 ParkPointRef(VehicleType type, int bayIndex)
        {
            return new Vector2(ScreenRef.BayX(bayIndex), ScreenRef.ParkCenterY(type));
        }

        /// <summary>
        /// True if the arrival route to this bay has room for the turn in: straight up into it, or a straight run along
        /// the arrival lane of at least the vehicle's length before turning up (so its tail is on the lane, not over the
        /// lot, while it turns).
        /// </summary>
        public static bool TurnInRoomOk(VehicleData v, int bayIndex)
        {
            List<Vector2> pts = Build(v, bayIndex);
            if (pts.Count < 3) return true;
            Vector2 turn = pts[pts.Count - 2];           // (bayX, TOPY)
            Vector2 join = pts[pts.Count - 3];           // where the route joined the arrival lane
            if (Mathf.Abs(turn.y - ScreenRef.TopY) > 0.5f || Mathf.Abs(join.y - ScreenRef.TopY) > 0.5f) return true;
            float run = Mathf.Abs(turn.x - join.x);
            float length = VehicleSpecs.Length(v.type) * BoardGeometry.PixelsPerCell * ScreenRef.VehicleVisualScale;
            return run < 1f || run >= length;
        }

        public static List<Vector2> Build(VehicleData v, int bayIndex)
        {
            Vector2 start = VehicleCenterRef(v);
            var pts = new List<Vector2> { start };

            switch (v.direction)
            {
                case Direction.Up:
                    pts.Add(new Vector2(start.x, ScreenRef.TopY));
                    break;
                case Direction.Down:
                {
                    float lane = start.x < ScreenRef.Width * 0.5f ? ScreenRef.LeftX : ScreenRef.RightX;
                    pts.Add(new Vector2(start.x, ScreenRef.BotY));
                    pts.Add(new Vector2(lane, ScreenRef.BotY));
                    pts.Add(new Vector2(lane, ScreenRef.TopY));
                    break;
                }
                case Direction.Left:
                    pts.Add(new Vector2(ScreenRef.LeftX, start.y));
                    pts.Add(new Vector2(ScreenRef.LeftX, ScreenRef.TopY));
                    break;
                default:
                    pts.Add(new Vector2(ScreenRef.RightX, start.y));
                    pts.Add(new Vector2(ScreenRef.RightX, ScreenRef.TopY));
                    break;
            }

            float bayX = ScreenRef.BayX(bayIndex);
            pts.Add(new Vector2(bayX, ScreenRef.TopY));
            pts.Add(ParkPointRef(v.type, bayIndex));
            return Clean(pts);
        }

        /// <summary>
        /// Departure: drive forward out of the top of the slot onto the strip above the bays (DEPART_Y) and off to the
        /// right. Departures never share the arrival lane, so the two flows cannot meet head-on.
        /// </summary>
        public static List<Vector2> BuildDeparture(VehicleType type, int bayIndex)
        {
            float x = ScreenRef.BayX(bayIndex);
            return Clean(new List<Vector2>
            {
                ParkPointRef(type, bayIndex),
                new Vector2(x, ScreenRef.DepartY),
                new Vector2(ScreenRef.Width + OffscreenRightPx, ScreenRef.DepartY)
            });
        }

        /// <summary>Removes duplicate points and points lying on a straight line between their neighbours.</summary>
        public static List<Vector2> Clean(List<Vector2> pts)
        {
            var dedup = new List<Vector2>();
            foreach (Vector2 p in pts)
            {
                if (dedup.Count == 0 || (dedup[dedup.Count - 1] - p).sqrMagnitude > 0.25f) dedup.Add(p);
            }

            var result = new List<Vector2>();
            for (int i = 0; i < dedup.Count; i++)
            {
                if (i > 0 && i < dedup.Count - 1)
                {
                    Vector2 a = (dedup[i] - dedup[i - 1]).normalized;
                    Vector2 b = (dedup[i + 1] - dedup[i]).normalized;
                    if (Vector2.Dot(a, b) > 0.9999f) continue; // collinear, same direction
                }
                result.Add(dedup[i]);
            }
            return result;
        }

        public static List<Vector3> ToWorld(List<Vector2> refPoints)
        {
            var w = new List<Vector3>(refPoints.Count);
            foreach (Vector2 p in refPoints) w.Add(ScreenRef.RefToWorld(p));
            return w;
        }
    }
}
