using System.Collections.Generic;
using BusRush.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace BusRush.Tests
{
    /// <summary>Edit-mode tests for road routes (reference px).</summary>
    public sealed class RouteBuilderTests
    {
        private static readonly Rect Allowed = new Rect(0f, 0f, ScreenRef.Width + 480f, ScreenRef.Height);

        private static IEnumerable<VehicleData> AllPlacements()
        {
            foreach (VehicleType type in new[] { VehicleType.Car, VehicleType.Van, VehicleType.Bus })
            {
                for (int row = 0; row < 10; row++)
                {
                    for (int col = 0; col < 9; col++)
                    {
                        foreach (Direction d in DirectionExtensions.All)
                        {
                            var v = new VehicleData(type, GameColor.Red, col, row, d);
                            var board = new BoardState(9, 10);
                            if (board.CanPlace(v)) yield return v;
                        }
                    }
                }
            }
        }

        [Test]
        public void Routes_StayOnScreen_StartAtVehicle_EndInBay()
        {
            int checkedRoutes = 0;
            foreach (VehicleData v in AllPlacements())
            {
                for (int bay = 0; bay < ScreenRef.BayCount; bay++)
                {
                    List<Vector2> route = RouteBuilder.Build(v, bay);
                    string ctx = $"{v} -> bay {bay}";
                    Assert.GreaterOrEqual(route.Count, 2, ctx);

                    foreach (Vector2 p in route)
                    {
                        Assert.IsTrue(Allowed.Contains(p), $"{ctx}: point {p} outside the allowed rect");
                    }

                    Assert.AreEqual(RouteBuilder.VehicleCenterRef(v), route[0], ctx + ": start");
                    Vector2 end = route[route.Count - 1];
                    Assert.AreEqual(ScreenRef.BayX(bay), end.x, 0.001f, ctx + ": end x");
                    Assert.AreEqual(ScreenRef.ParkCenterY(v.type), end.y, 0.001f, ctx + ": end y");
                    // Front of the parked vehicle is exactly at BAY_Y.
                    float half = ScreenRef.VehicleLengthPx(v.type) * 0.5f;
                    Assert.GreaterOrEqual(end.y - half, ScreenRef.SlotTopY, ctx + ": front inside the slot");
                    Assert.LessOrEqual(end.y + half, ScreenRef.SlotBottomY, ctx + ": tail inside the slot");

                    Vector2 first = (route[1] - route[0]).normalized;
                    Vector2 expected = v.direction switch
                    {
                        Direction.Up => Vector2.down,     // screen y grows downward
                        Direction.Down => Vector2.up,
                        Direction.Left => Vector2.left,
                        _ => Vector2.right
                    };
                    Assert.Greater(Vector2.Dot(first, expected), 0.999f, ctx + ": first leg direction");

                    for (int i = 1; i < route.Count; i++)
                    {
                        Vector2 seg = route[i] - route[i - 1];
                        Assert.IsTrue(Mathf.Abs(seg.x) < 0.001f || Mathf.Abs(seg.y) < 0.001f, $"{ctx}: diagonal segment {i}");
                        Assert.Greater(seg.sqrMagnitude, 0.2f, $"{ctx}: duplicate point {i}");
                    }

                    // The last leg drives up into the bay.
                    Vector2 last = route[route.Count - 1] - route[route.Count - 2];
                    Assert.Less(last.y, 0f, ctx + ": enters the bay upward");
                    checkedRoutes++;
                }
            }
            Debug.Log($"[RouteBuilderTests] {checkedRoutes} routes checked.");
        }

        [Test]
        public void SideLanes_DoNotCrossTheLot()
        {
            float lotLeft = BoardGeometry.LotLeftPx;
            float lotRight = lotLeft + 9 * BoardGeometry.PixelsPerCell;
            float lotBottom = BoardGeometry.LotTopPx + 10 * BoardGeometry.PixelsPerCell * BoardGeometry.SinPitch;
            Assert.Less(ScreenRef.LeftX, lotLeft);
            Assert.Greater(ScreenRef.RightX, lotRight);
            Assert.Greater(ScreenRef.BotY, lotBottom);
            Assert.Less(ScreenRef.TopY, BoardGeometry.LotTopPx);
        }

        [Test]
        public void Departure_LeavesForwardOverTheBays_ThenRight()
        {
            foreach (VehicleType type in new[] { VehicleType.Car, VehicleType.Van, VehicleType.Bus })
            {
                for (int bay = 0; bay < ScreenRef.BayCount; bay++)
                {
                    List<Vector2> route = RouteBuilder.BuildDeparture(type, bay);
                    Assert.AreEqual(RouteBuilder.ParkPointRef(type, bay), route[0]);
                    Assert.Less(route[1].y, route[0].y, "first leg drives forward (up) out of the slot");
                    Assert.AreEqual(ScreenRef.DepartY, route[route.Count - 1].y, 0.001f);
                    Assert.Greater(route[route.Count - 1].x, ScreenRef.Width);
                    Assert.Less(ScreenRef.DepartY, ScreenRef.SlotTopY, "departure strip is above the painted slots");
                    foreach (Vector2 p in route) Assert.IsTrue(Allowed.Contains(p), $"{type} bay {bay}: {p}");
                }
            }
        }

        [Test]
        public void ArrivalLane_ClearsParkedBuses()
        {
            // Bus tail (front at BAY_Y) must stay above the top edge of a vehicle driving on the arrival lane.
            float busTail = ScreenRef.ParkCenterY(VehicleType.Bus) + ScreenRef.VehicleLengthPx(VehicleType.Bus) * 0.5f;
            float laneTopEdge = ScreenRef.TopY - 0.40f * BoardGeometry.PixelsPerCell * BoardGeometry.SinPitch;
            Assert.Less(busTail, laneTopEdge);
        }

        [Test]
        public void RefToWorld_RoundTrips_AndMatchesTheLot()
        {
            Vector3 lotCenter = ScreenRef.RefToWorld(BoardGeometry.LotCenterPx);
            Assert.AreEqual(0f, lotCenter.x, 0.0001f);
            Assert.AreEqual(0f, lotCenter.z, 0.0001f);

            Vector3 cell = ScreenRef.RefToWorld(ScreenRef.CellToRef(2, 7));
            Vector3 expected = BoardGeometry.CellToWorld(2, 7);
            Assert.AreEqual(expected.x, cell.x, 0.0001f);
            Assert.AreEqual(expected.z, cell.z, 0.0001f);

            var p = new Vector2(123f, 456f);
            Vector2 back = ScreenRef.WorldToRef(ScreenRef.RefToWorld(p));
            Assert.AreEqual(p.x, back.x, 0.001f);
            Assert.AreEqual(p.y, back.y, 0.001f);
        }
    }
}
