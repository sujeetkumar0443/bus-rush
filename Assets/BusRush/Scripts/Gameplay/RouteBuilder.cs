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

        /// <summary>Sideways offset while reversing out of a bay before driving off to the right.</summary>
        public const float ReverseOutPx = 34f;

        public static Vector2 VehicleCenterRef(VehicleData v)
        {
            Vector2Int h = v.Head, t = v.Tail;
            return ScreenRef.CellToRef((h.x + t.x) * 0.5f, (h.y + t.y) * 0.5f);
        }

        public static Vector2 ParkPointRef(VehicleType type, int bayIndex)
        {
            return new Vector2(ScreenRef.BayX(bayIndex), ScreenRef.ParkCenterY(type));
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

        /// <summary>Leg 1 (driven in reverse): back out of the bay onto TOPY, swinging left so the nose points right.</summary>
        public static List<Vector2> BuildReverseOut(VehicleType type, int bayIndex)
        {
            float x = ScreenRef.BayX(bayIndex);
            return Clean(new List<Vector2>
            {
                ParkPointRef(type, bayIndex),
                new Vector2(x, ScreenRef.TopY),
                new Vector2(x - ReverseOutPx, ScreenRef.TopY)
            });
        }

        /// <summary>Leg 2: drive right along TOPY and off screen.</summary>
        public static List<Vector2> BuildDriveOff(int bayIndex)
        {
            float x = ScreenRef.BayX(bayIndex) - ReverseOutPx;
            return new List<Vector2>
            {
                new Vector2(x, ScreenRef.TopY),
                new Vector2(ScreenRef.Width + OffscreenRightPx, ScreenRef.TopY)
            };
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
