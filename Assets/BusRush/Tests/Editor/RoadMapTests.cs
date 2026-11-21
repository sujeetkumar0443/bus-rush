using System.IO;
using BusRush.UI;
using NUnit.Framework;
using UnityEngine;

namespace BusRush.Tests
{
    /// <summary>Edit-mode tests for the Road Map geometry: bus frame picker and node placement.</summary>
    public sealed class RoadMapTests
    {
        private const string JsonPath = "Assets/BusRush/Art/UI_v2/Map/road_centerline.json";

        [Test]
        public void FramePicker_CardinalDirections()
        {
            Assert.AreEqual(0, RoadMapGeometry.FrameFor(1f, 0f), "right");
            Assert.AreEqual(8, RoadMapGeometry.FrameFor(0f, -1f), "up (screen y down)");
            Assert.AreEqual(16, RoadMapGeometry.FrameFor(-1f, 0f), "left");
            Assert.AreEqual(24, RoadMapGeometry.FrameFor(0f, 1f), "down");
            // Up-right on screen at 45 deg is steeper on the ground (divided by sin 50): 52.5 deg = frame 5, not 4.
            Assert.AreEqual(5, RoadMapGeometry.FrameFor(1f, -1f), "up-right");
        }

        [Test]
        public void Nodes_InsideTheirTile_InOrder()
        {
            var geo = new RoadMapGeometry(File.ReadAllText(JsonPath));
            float lastArc = -1f;
            for (int level = 1; level <= LevelPlan.PlannedLevels; level++)
            {
                Vector2 local = geo.NodeLocal(level);
                Assert.That(local.y, Is.InRange(0f, RoadMapGeometry.TileHeight), $"level {level}: y inside its tile");
                Assert.That(local.x, Is.InRange(33f, RoadMapGeometry.FrameWidth - 33f), $"level {level}: disc fully on screen");

                Vector2 content = geo.NodeContent(level);
                int tile = RoadMapGeometry.TileOf(level);
                Assert.That(content.y, Is.InRange(tile * RoadMapGeometry.TileHeight, (tile + 1) * RoadMapGeometry.TileHeight),
                    $"level {level}: content y inside tile {tile}");

                float arc = geo.LevelArc(level);
                Assert.Greater(arc, lastArc, $"level {level}: further along the road than the previous one");
                lastArc = arc;
            }
        }

        [Test]
        public void Design_Level5_MatchesMockPosition()
        {
            // RoadMap.dc.html: level 5 box 66x54 at (217.1, 2101.0) in a 2537 px column whose bottom is tile 0's bottom.
            var geo = new RoadMapGeometry(File.ReadAllText(JsonPath));
            Vector2 p = geo.NodeContent(5);
            float designCentreY = 2537.3f - (2101.0f + 27f);
            Assert.AreEqual(217.1f + 33f, p.x, 1.5f);
            Assert.AreEqual(designCentreY, p.y + 3f, 2f);
        }
    }
}
