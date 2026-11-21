using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BusRush.UI
{
    /// <summary>
    /// Road Map layout in design px (RoadMap.dc.html: 390 wide frame, map image 941x1672 shown 476 wide at x -43).
    /// Content space: x from the frame's left edge, y UP from the bottom of tile 0 (level 1 side). Tiles stack bottom
    /// to top, 10 levels each; the road centre line (Art/UI_v2/Map/road_centerline.json) is the same on every tile.
    /// Pure math, no scene: used by <see cref="RoadMapScreen"/> and the edit-mode tests.
    /// </summary>
    public sealed class RoadMapGeometry
    {
        public const float FrameWidth = 390f;
        public const float TileWidth = 476f;
        public const float TileLeft = -43f;
        public const float ImageWidth = 941f;
        public const float ImageHeight = 1672f;
        public const float Scale = TileWidth / ImageWidth;
        public const float TileHeight = ImageHeight * Scale; // 845.8
        public const int LevelsPerTile = 10;

        /// <summary>Road points of one tile, tile-local design px (x from frame left, y up from tile bottom), bottom to top.</summary>
        public readonly Vector2[] Points;
        /// <summary>Cumulative arc length at each point (design px).</summary>
        public readonly float[] Arc;
        public readonly float[] NodeFractions;
        public float TileArc => Arc[Arc.Length - 1];

        public RoadMapGeometry(string json)
        {
            List<float> pts = Numbers(Section(json, "points"));
            NodeFractions = Numbers(Section(json, "node_arc_fractions")).ToArray();
            if (pts.Count < 4 || pts.Count % 2 != 0) throw new FormatException("road_centerline.json: bad points");
            if (NodeFractions.Length != LevelsPerTile) throw new FormatException("road_centerline.json: need 10 node fractions");

            Points = new Vector2[pts.Count / 2];
            Arc = new float[Points.Length];
            for (int i = 0; i < Points.Length; i++) Points[i] = ImageToTile(pts[2 * i], pts[2 * i + 1]);
            BlendSeam(Points);
            for (int i = 1; i < Points.Length; i++) Arc[i] = Arc[i - 1] + Vector2.Distance(Points[i - 1], Points[i]);
        }

        /// <summary>Arc length over which the line is eased at each tile end so it meets the next tile's start.</summary>
        public const float SeamBlend = 60f;

        /// <summary>
        /// The drawn road leaves the top of a tile ~5 px left of where it enters the next tile's bottom. Ease both ends
        /// towards their midpoint (smoothstep over SeamBlend px of arc) so the bus path and dashes continue across seams.
        /// </summary>
        private static void BlendSeam(Vector2[] p)
        {
            float dx = p[0].x - p[p.Length - 1].x; // next tile's start minus this tile's end
            float fromStart = 0f;
            for (int i = 0; i < p.Length; i++)
            {
                if (i > 0) fromStart += Vector2.Distance(p[i - 1], p[i]);
                if (fromStart >= SeamBlend) break;
                float k = 1f - Mathf.SmoothStep(0f, 1f, fromStart / SeamBlend);
                p[i].x -= dx * 0.5f * k;
            }
            float fromEnd = 0f;
            for (int i = p.Length - 1; i >= 0; i--)
            {
                if (i < p.Length - 1) fromEnd += Vector2.Distance(p[i + 1], p[i]);
                if (fromEnd >= SeamBlend) break;
                float k = 1f - Mathf.SmoothStep(0f, 1f, fromEnd / SeamBlend);
                p[i].x += dx * 0.5f * k;
            }
        }

        public static Vector2 ImageToTile(float px, float py) => new Vector2(px * Scale + TileLeft, TileHeight - py * Scale);

        public static int TileOf(int level) => (level - 1) / LevelsPerTile;

        public static int TileCount(int levels) => Mathf.Max(1, (levels + LevelsPerTile - 1) / LevelsPerTile);

        // ------------------------------------------------------------------ arc

        /// <summary>Arc position of a level's node from the start of tile 0 (tiles chain end to end).</summary>
        public float LevelArc(int level) => TileOf(level) * TileArc + NodeFractions[(level - 1) % LevelsPerTile] * TileArc;

        /// <summary>Tile-local point of a level's node on the road.</summary>
        public Vector2 NodeLocal(int level) => LocalAt(NodeFractions[(level - 1) % LevelsPerTile] * TileArc, out _);

        /// <summary>Content-space point of a level's node on the road.</summary>
        public Vector2 NodeContent(int level) => NodeLocal(level) + new Vector2(0f, TileOf(level) * TileHeight);

        /// <summary>Content-space point and road direction (unit, y up) at a global arc position.</summary>
        public Vector2 ContentAt(float globalArc, out Vector2 direction)
        {
            int tile = Mathf.Max(0, Mathf.FloorToInt(globalArc / TileArc));
            Vector2 p = LocalAt(globalArc - tile * TileArc, out direction);
            return p + new Vector2(0f, tile * TileHeight);
        }

        /// <summary>Tile-local point and direction at an arc position inside one tile (clamped).</summary>
        public Vector2 LocalAt(float arc, out Vector2 direction)
        {
            arc = Mathf.Clamp(arc, 0f, TileArc);
            int lo = 0, hi = Arc.Length - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (Arc[mid] <= arc) lo = mid;
                else hi = mid;
            }
            float seg = Arc[hi] - Arc[lo];
            float u = seg > 1e-5f ? (arc - Arc[lo]) / seg : 0f;
            direction = (Points[hi] - Points[lo]).normalized;
            return Vector2.Lerp(Points[lo], Points[hi], u);
        }

        /// <summary>Content-space points of the road between two global arc positions (for the unlock trail).</summary>
        public List<Vector2> ContentPath(float fromArc, float toArc, float step = 4f)
        {
            var list = new List<Vector2>();
            for (float a = fromArc; a < toArc; a += step) list.Add(ContentAt(a, out _));
            list.Add(ContentAt(toArc, out _));
            return list;
        }

        // ------------------------------------------------------------------ bus frame

        public const int BusFrames = 32;

        /// <summary>
        /// Bus sprite frame for a screen direction (dx right, dyDown down): frame i = ground heading i*11.25 deg
        /// (0 = right, 90 = up/away); the ground is seen at 50 deg, so screen-vertical is divided by sin 50.
        /// </summary>
        public static int FrameFor(float dx, float dyDown)
        {
            float ground = Mathf.Atan2(-dyDown / Mathf.Sin(50f * Mathf.Deg2Rad), dx) * Mathf.Rad2Deg;
            int frame = Mathf.RoundToInt(ground / (360f / BusFrames));
            return ((frame % BusFrames) + BusFrames) % BusFrames;
        }

        // ------------------------------------------------------------------ json (nested arrays: no JsonUtility)

        private static string Section(string json, string key)
        {
            int k = json.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            if (k < 0) throw new FormatException("road_centerline.json: missing " + key);
            int start = json.IndexOf('[', k);
            int depth = 0;
            for (int i = start; i < json.Length; i++)
            {
                if (json[i] == '[') depth++;
                else if (json[i] == ']' && --depth == 0) return json.Substring(start, i - start + 1);
            }
            throw new FormatException("road_centerline.json: unterminated " + key);
        }

        private static List<float> Numbers(string s)
        {
            var list = new List<float>();
            foreach (Match m in Regex.Matches(s, @"-?\d+(\.\d+)?([eE][-+]?\d+)?"))
            {
                list.Add(float.Parse(m.Value, CultureInfo.InvariantCulture));
            }
            return list;
        }
    }
}
