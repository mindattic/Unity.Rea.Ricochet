// A freshly randomised level for every round (UR-T23). The 2013 original's randomize_level.gml rebuilt the arena each
// round from the setup's polygon count (20), shapes (abstract), rotation (360) and overlap (no) settings via
// setup_shapes.gml, and randomize_player_position.gml dropped every player at a random spot. Here the same inputs make
// a Level of mirror walls shaped like the original's sixteen-a-kind sprites and sixteen seat spots from one seed, so every peer rebuilds the identical
// round from the seed alone and only the seed goes over the wire (UR-LAW-1).
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ricochet.Core.Level
{
    using Ricochet.Core.Aim;
    using Ricochet.Core.Match;
    /// <summary>Which of the original's five shape sets the walls are drawn from (its shapetype setting), or a mix.</summary>
    public enum ShapeKind
    {
        /// <summary>obj_polygon1..16: the hand-made irregular outlines (the original's default).</summary>
        Abstract,
        Triangles,
        Bars,
        Circles,
        Squares,

        /// <summary>Each wall picks one of the five sets at random, then one of its sixteen shapes.</summary>
        Random,
    }

    /// <summary>The setup screen's level settings, with the original's defaults.</summary>
    public sealed class LevelSettings
    {
        /// <summary>How many shapes to lay (the original's 20).</summary>
        public int PolygonCount { get; set; } = 20;

        /// <summary>
        /// The original's rotation setting in degrees: 0 leaves every shape as drawn, 45 turns it to one of eight
        /// multiples of 45, 90 to one of four multiples of 90, and anything else (the original's 360) any whole angle up
        /// to that many degrees.
        /// </summary>
        public double Rotation { get; set; } = 360;

        /// <summary>True lets shapes overlap each other (the original's "no").</summary>
        public bool Overlap { get; set; }

        /// <summary>Which shape set the walls come from (the original's "abstract").</summary>
        public ShapeKind Shapes { get; set; } = ShapeKind.Abstract;

        public static LevelSettings Default => new LevelSettings();
    }

    /// <summary>One round's arena: the level and where each of the sixteen seats stands in it.</summary>
    public sealed class RoundLayout
    {
        public RoundLayout(Level level, IReadOnlyList<Vec2> spots, int polygons = 0)
        {
            Polygons = polygons;
            Level = level ?? throw new ArgumentNullException(nameof(level));
            Spots = spots ?? throw new ArgumentNullException(nameof(spots));
        }

        public Level Level { get; }

        /// <summary>How many polygons were laid (0 for a hand-laid map).</summary>
        public int Polygons { get; }

        /// <summary>Where seat i stands (any seat index wraps onto the spots).</summary>
        public IReadOnlyList<Vec2> Spots { get; }

        public Vec2 SpawnPoint(int seat)
        {
            int n = Spots.Count;
            return Spots[((seat % n) + n) % n];
        }
    }

    public static class LevelGenerator
    {
        private const double MinX = -11, MinY = -6, MaxX = 11, MaxY = 6;

        /// <summary>The border's inset a polygon's centre keeps (so its walls stay inside).</summary>
        private const double EdgeGap = 0.3;

        /// <summary>The room a seat's spot (and a hazard's end) keeps from every shape's outline.</summary>
        private const double SeatClearance = Seating.PlayerRadius + 0.35;

        /// <summary>The room a seat's spot keeps from a practice dummy (never relaxed).</summary>
        private const double DummyClearance = Seating.PlayerRadius * 2 + 0.1;

        /// <summary>Seats stand at least this far apart.</summary>
        private const double SeatGap = 1.6;

        /// <summary>Shapes keep this far from each other when overlap is off.</summary>
        private const double ShapeGap = 0.1;

        private const int Attempts = 150;

        /// <summary>How many shapes are tried for a wall before it is given up.</summary>
        private const int Picks = 12;

        /// <summary>
        /// The round for <paramref name="seed"/>: shapes picked and turned the way setup_shapes.gml does and laid per
        /// <paramref name="settings"/> clear of <paramref name="keepClear"/> (the practice dummies), then sixteen seat spots
        /// clear of every shape, the border, the dummies and each other. The same seed and settings always give the same round.
        /// </summary>
        public static RoundLayout Generate(int seed, LevelSettings settings = null, IEnumerable<Vec2> keepClear = null)
        {
            settings = settings ?? LevelSettings.Default;
            var dice = new Random(seed);
            var avoid = new List<Vec2>(keepClear ?? DefaultKeepClear());
            var shapes = new List<PlacedShape>();
            var walls = new List<Wall>();

            for (int i = 0; i < settings.PolygonCount; i++)
            {
                for (int pick = 0; pick < Picks; pick++)
                {
                    // A shape that will not fit anywhere is swapped for another pick (the original would retry forever).
                    Vec2[] outline = Turn(ShapeLibrary.Outline(ShapeLibrary.Pick(settings.Shapes, dice)), RotationDegrees(settings.Rotation, dice));
                    PlacedShape placed = default;
                    bool found = false;
                    for (int attempt = 0; attempt < Attempts && !found; attempt++)
                    {
                        placed = PlacedShape.At(outline, RandomOrigin(outline, dice));
                        found = placed.InsideBorder(MinX, MinY, MaxX, MaxY) && ClearOfPoints(placed, SeatClearance, avoid)
                            && (settings.Overlap || !Overlaps(placed, shapes));
                    }

                    if (!found)
                    {
                        continue;
                    }

                    Vec2[] c = placed.Corners;
                    for (int k = 0; k < c.Length; k++)
                    {
                        walls.Add(new Wall(c[k], c[(k + 1) % c.Length]));
                    }

                    shapes.Add(placed);
                    break;
                }
            }

            var level = new Level(MinX, MinY, MaxX, MaxY, walls);
            return new RoundLayout(level, PickSpots(dice, shapes, avoid), shapes.Count);
        }

        /// <summary>setup_shapes.gml's rotation switch: none, one of eight, one of four or any whole angle.</summary>
        private static double RotationDegrees(double rotation, Random dice)
        {
            if (rotation <= 0)
            {
                return 0;
            }

            if (rotation == 45)
            {
                return 45 * dice.Next(0, 8);
            }

            if (rotation == 90)
            {
                return 90 * dice.Next(0, 4);
            }

            return dice.Next(0, (int)Math.Min(360, Math.Ceiling(rotation)));
        }

        private static Vec2[] Turn(Vec2[] outline, double degrees)
        {
            if (degrees == 0)
            {
                return outline;
            }

            double r = degrees * Math.PI / 180.0, cos = Math.Cos(r), sin = Math.Sin(r);
            var turned = new Vec2[outline.Length];
            for (int i = 0; i < outline.Length; i++)
            {
                turned[i] = new Vec2(outline[i].X * cos - outline[i].Y * sin, outline[i].X * sin + outline[i].Y * cos);
            }

            return turned;
        }

        /// <summary>A random spot for the shape's origin (its middle) that keeps the whole outline inside the border.</summary>
        private static Vec2 RandomOrigin(Vec2[] outline, Random dice)
        {
            double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
            foreach (Vec2 v in outline)
            {
                minX = Math.Min(minX, v.X);
                maxX = Math.Max(maxX, v.X);
                minY = Math.Min(minY, v.Y);
                maxY = Math.Max(maxY, v.Y);
            }

            double lowX = MinX + EdgeGap - minX, highX = MaxX - EdgeGap - maxX;
            double lowY = MinY + EdgeGap - minY, highY = MaxY - EdgeGap - maxY;
            // A shape wider than the border cannot fit: aim for the middle and let the border check refuse it.
            return new Vec2(highX > lowX ? Between(dice, lowX, highX) : 0, highY > lowY ? Between(dice, lowY, highY) : 0);
        }

        /// <summary>The practice dummies and, sampled along them, the electric arcs and laser gates every round lays.</summary>
        private static List<Vec2> DefaultKeepClear()
        {
            var keep = new List<Vec2>(Seating.DummyStarts);
            HazardRules hazards = HazardRules.Default;
            foreach (Vec2[] segment in hazards.Arcs.Concat(hazards.Gates))
            {
                int steps = Math.Max(1, (int)Math.Ceiling(Vec2.Distance(segment[0], segment[1]) / 0.5));
                for (int i = 0; i <= steps; i++)
                {
                    double t = (double)i / steps;
                    keep.Add(new Vec2(segment[0].X + (segment[1].X - segment[0].X) * t, segment[0].Y + (segment[1].Y - segment[0].Y) * t));
                }
            }

            return keep;
        }

        private static List<Vec2> PickSpots(Random dice, List<PlacedShape> shapes, List<Vec2> avoid)
        {
            var spots = new List<Vec2>(MatchRules.MaxSeats);
            double edge = Seating.PlayerRadius + 0.3;
            for (int seat = 0; seat < MatchRules.MaxSeats; seat++)
            {
                Vec2 spot = default;
                bool found = false;
                // Relax the clearances only if a crowded level leaves no room, so a seat is never left without a spot.
                for (int relax = 0; relax < 4 && !found; relax++)
                {
                    double scale = 1.0 - 0.1 * relax;
                    for (int attempt = 0; attempt < 200 && !found; attempt++)
                    {
                        spot = new Vec2(Between(dice, MinX + edge, MaxX - edge), Between(dice, MinY + edge, MaxY - edge));
                        found = ClearOfAll(spot, SeatGap * scale, spots) && ClearOfAll(spot, DummyClearance, avoid)
                            && ClearOfShapes(spot, SeatClearance * scale, shapes);
                    }
                }

                // Last resort on a crowded level: only the bare minimum room, so a seat never lands inside a polygon.
                for (int attempt = 0; attempt < 3000 && !found; attempt++)
                {
                    spot = new Vec2(Between(dice, MinX + edge, MaxX - edge), Between(dice, MinY + edge, MaxY - edge));
                    found = ClearOfAll(spot, Seating.PlayerRadius * 2 + 0.05, spots) && ClearOfAll(spot, DummyClearance, avoid)
                        && ClearOfShapes(spot, Seating.PlayerRadius + 0.05, shapes);
                }

                spots.Add(found ? spot : Seating.SpawnPoint(seat));
            }

            return spots;
        }

        private static bool ClearOfAll(Vec2 point, double distance, List<Vec2> others)
        {
            foreach (Vec2 other in others)
            {
                if (Vec2.Distance(point, other) < distance)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ClearOfShapes(Vec2 point, double clearance, List<PlacedShape> shapes)
        {
            foreach (PlacedShape shape in shapes)
            {
                if (!shape.ClearOfPoint(point, clearance))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ClearOfPoints(PlacedShape shape, double clearance, List<Vec2> points)
        {
            foreach (Vec2 point in points)
            {
                if (!shape.ClearOfPoint(point, clearance))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>True when the shape crosses another, lies inside it or comes within <see cref="ShapeGap"/> of it.</summary>
        private static bool Overlaps(PlacedShape shape, List<PlacedShape> others)
        {
            foreach (PlacedShape other in others)
            {
                if (Vec2.Distance(shape.Centre, other.Centre) < shape.Radius + other.Radius + ShapeGap && shape.Touches(other, ShapeGap))
                {
                    return true;
                }
            }

            return false;
        }

        private static double Between(Random dice, double low, double high) => low + dice.NextDouble() * (high - low);
    }
}
