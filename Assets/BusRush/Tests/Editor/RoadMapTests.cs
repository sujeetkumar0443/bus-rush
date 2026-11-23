using System.IO;
using BusRush.UI;
using NUnit.Framework;
using UnityEngine;

namespace BusRush.Tests
{
    /// <summary>Edit-mode tests for the Road Map geometry: node placement and path continuity.</summary>
    public sealed class RoadMapTests
    {
        private const string JsonPath = "Assets/BusRush/Art/UI_v2/Map/road_centerline.json";

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
        public void Path_IsContinuousAcrossTileSeams()
        {
            // The path 10 -> 11 (and 20 -> 21 ...) crosses a seam: consecutive 1 px arc samples never jump > 4 px.
            var geo = new RoadMapGeometry(File.ReadAllText(JsonPath));
            for (int seam = 1; seam <= 3; seam++)
            {
                float from = seam * geo.TileArc - 120f, to = seam * geo.TileArc + 120f;
                Vector2 prev = geo.ContentAt(from, out _);
                float maxJump = 0f;
                for (float a = from + 1f; a <= to; a += 1f)
                {
                    Vector2 p = geo.ContentAt(a, out Vector2 dir);
                    maxJump = Mathf.Max(maxJump, Vector2.Distance(prev, p));
                    Assert.Greater(dir.y, 0f, "the road keeps going up across the seam");
                    prev = p;
                }
                Assert.LessOrEqual(maxJump, 4f, $"seam {seam}: biggest step {maxJump:0.00} px");
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
