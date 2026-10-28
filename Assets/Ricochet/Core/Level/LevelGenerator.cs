// A freshly randomised level for every round (UR-T23). The 2013 original's randomize_level.gml rebuilt the arena each
// round from the setup's polygon count (20), shapes (abstract), rotation (360) and overlap (no) settings via
// setup_shapes.gml, and randomize_player_position.gml dropped every player at a random spot. Here the same inputs make
// a Level of mirror walls shaped like the original's sixteen-a-kind sprites and sixteen seat spots from one seed, so every peer rebuilds the identical
// round from the seed alone (UR-LAW-1). Peers share the LevelSettings through the seed too (U.RICO-T52, LevelSeeds): the
// Start screen's shapes, type, rotation and overlap are packed into a negative seed, and a positive seed (every seed before
// U.RICO-T52, and every round played on the stock settings) is the stock settings, so old seeds lay the same rounds.
// The border (U.RICO-T106: obj_game's PageUp and PageDown, 32 to 256 in steps of 32) rides in the same packed seed.
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
        public int PolygonCount { get; set; }

        /// <summary>
        /// The original's rotation setting in degrees: 0 leaves every shape as drawn, 45 turns it to one of eight
        /// multiples of 45, 90 to one of four multiples of 90, and anything else (the original's 360) any whole angle up
        /// to that many degrees.
        /// </summary>
        public double Rotation { get; set; }

        /// <summary>True lets shapes overlap each other (the original's "no").</summary>
        public bool Overlap { get; set; }

        /// <summary>Which shape set the walls come from (the original's "abstract").</summary>
        public ShapeKind Shapes { get; set; }

        /// <summary>
        /// The original's border (U.RICO-T106): 32 to 256 pixels in steps of 32, the playfield's inset that PageUp and PageDown
        /// move. 32 is the stock arena; every step above it pulls the walls in (see <see cref="LevelGenerator.Inset"/>).
        /// </summary>
        public int Border { get; set; } = LevelSeeds.MinBorder;

        /// <summary>The stock setup, from level.json (UR-T35): a new set starts as a copy of it.</summary>
        public LevelSettings()
        {
            CopyFrom(Ricochet.Core.Data.GameData.Current.Level);
        }

        internal LevelSettings(bool blank)
        {
        }

        public static LevelSettings Default => new LevelSettings();

        internal void CopyFrom(LevelSettings other)
        {
            PolygonCount = other.PolygonCount;
            Rotation = other.Rotation;
            Overlap = other.Overlap;
            Shapes = other.Shapes;
            Border = other.Border;
        }

        /// <summary>True when <paramref name="other"/> lays the same kind of level.</summary>
        public bool SameAs(LevelSettings other)
        {
            return other != null && PolygonCount == other.PolygonCount && Rotation.Equals(other.Rotation) && Overlap == other.Overlap
                && Shapes == other.Shapes && Border == other.Border;
        }
    }

    /// <summary>
    /// U.RICO-T52: a random level's seed carries the setup's level settings, so every peer lays the same round from the seed
    /// alone (and the border, U.RICO-T106). A seed of 0 or above is the stock settings (level.json) with that seed as the dice: every seed before this
    /// existed, and any round on the stock settings. A seed below zero holds the settings in bits 18 to 29 (the shape count's
    /// place in <see cref="ShapeCounts"/>, the <see cref="ShapeKind"/>, the rotation's place in <see cref="Rotations"/>, overlap
    /// and the border's step) and the dice in bits 0 to 17.
    /// </summary>
    public static class LevelSeeds
    {
        private const int DiceBits = 18;
        private const int DiceMask = (1 << DiceBits) - 1;

        /// <summary>The smallest border (draw_borders' border, 32) and the biggest PageDown reaches (256), in steps of 32.</summary>
        public const int MinBorder = 32, MaxBorder = 256, BorderStep = 32;

        /// <summary>The border one PageUp (-1) or PageDown (+1) from <paramref name="border"/> gives: obj_game's key 33 and 34, which stop at either end.</summary>
        public static int StepBorder(int border, int step)
        {
            border = ClampBorder(border);
            if (step < 0 && border > MinBorder)
            {
                return border - BorderStep;
            }

            return step > 0 && border < MaxBorder ? border + BorderStep : border;
        }

        private static int ClampBorder(int border) => Math.Max(MinBorder, Math.Min(MaxBorder, (border + BorderStep / 2) / BorderStep * BorderStep));

        /// <summary>The Start screen's shape counts (obj_btn_polygons_less / more: 5 to 40 in fives, wrapping).</summary>
        public static readonly int[] ShapeCounts = { 5, 10, 15, 20, 25, 30, 35, 40 };

        /// <summary>The Start screen's rotations (obj_btn_rotation_last / next: none, 45, 90, 360 degrees).</summary>
        public static readonly int[] Rotations = { 0, 45, 90, 360 };

        /// <summary>
        /// The seed for a round laid by <paramref name="settings"/> with <paramref name="dice"/>: <paramref name="dice"/> itself
        /// for the stock settings (or none, or settings the Start screen cannot set), the packed seed otherwise.
        /// </summary>
        public static int Compose(int dice, LevelSettings settings)
        {
            int count = Array.IndexOf(ShapeCounts, settings != null ? settings.PolygonCount : -1);
            int rotation = settings != null ? Array.IndexOf(Rotations, (int)settings.Rotation) : -1;
            if (settings == null || count < 0 || rotation < 0 || settings.Rotation != (int)settings.Rotation || settings.SameAs(LevelSettings.Default))
            {
                return dice;
            }

            int step = (ClampBorder(settings.Border) - MinBorder) / BorderStep;
            int code = count | (int)settings.Shapes << 3 | rotation << 6 | (settings.Overlap ? 1 : 0) << 8 | step << 9;
            return int.MinValue | code << DiceBits | (dice & DiceMask);
        }

        /// <summary>The dice a seed lays its round with.</summary>
        public static int Dice(int seed) => seed < 0 ? seed & DiceMask : seed;

        /// <summary>The level settings a seed lays its round with (a fresh copy).</summary>
        public static LevelSettings Settings(int seed)
        {
            var settings = LevelSettings.Default;
            if (seed >= 0)
            {
                return settings;
            }

            int code = seed >> DiceBits & 4095;
            settings.PolygonCount = ShapeCounts[code & 7];
            int kind = code >> 3 & 7;
            settings.Shapes = kind <= (int)ShapeKind.Random ? (ShapeKind)kind : ShapeKind.Random;
            settings.Rotation = Rotations[code >> 6 & 3];
            settings.Overlap = (code >> 8 & 1) != 0;
            settings.Border = MinBorder + BorderStep * (code >> 9 & 7);
            return settings;
        }
    }

    /// <summary>
    /// One shape a random level laid, as the original's obj_polygon/triangle/bar/circle/square instance stood: which of the
    /// eighty, where its origin is and how far it is turned (U.RICO-T46 draws its sprite there).
    /// </summary>
    public readonly struct LaidShape
    {
        public LaidShape(ShapeId id, Vec2 origin, double degrees, Vec2[] corners)
        {
            Id = id;
            Origin = origin;
            Degrees = degrees;
            Corners = corners;
        }

        public ShapeId Id { get; }

        /// <summary>The sprite's origin in the arena (instance_create's x, y).</summary>
        public Vec2 Origin { get; }

        /// <summary>image_angle: degrees anticlockwise.</summary>
        public double Degrees { get; }

        /// <summary>The outline the walls were made from, laid and turned.</summary>
        public Vec2[] Corners { get; }
    }

    /// <summary>One round's arena: the level and where each of the sixteen seats stands in it.</summary>
    public sealed class RoundLayout
    {
        private static readonly LaidShape[] s_noShapes = new LaidShape[0];

        public RoundLayout(Level level, IReadOnlyList<Vec2> spots, int polygons = 0, IReadOnlyList<LaidShape> shapes = null)
        {
            Polygons = polygons;
            Level = level ?? throw new ArgumentNullException(nameof(level));
            Spots = spots ?? throw new ArgumentNullException(nameof(spots));
            Shapes = shapes ?? s_noShapes;
        }

        public Level Level { get; }

        /// <summary>How many polygons were laid (0 for a hand-laid map).</summary>
        public int Polygons { get; }

        /// <summary>The shapes a random level laid, in laying order (none on a hand-laid map); their walls are the level's.</summary>
        public IReadOnlyList<LaidShape> Shapes { get; }

        /// <summary>Where seat i stands (any seat index wraps onto the spots).</summary>
        public IReadOnlyList<Vec2> Spots { get; }

        public Vec2 SpawnPoint(int seat)
        {
            int n = Spots.Count;
            return Spots[((seat % n) + n) % n];
        }
    }

    /// <summary>A playfield's edges.</summary>
    public readonly struct Playfield
    {
        public Playfield(double minX, double minY, double maxX, double maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public double MinX { get; }

        public double MinY { get; }

        public double MaxX { get; }

        public double MaxY { get; }

        /// <summary>
        /// <paramref name="point"/> pulled inside this playfield, <paramref name="margin"/> from each edge (U.RICO-T126): a spot
        /// that is fixed in the stock arena (a dummy, an arc's pylon) stays in play however thick the border. A field narrower
        /// than twice the margin gives its middle.
        /// </summary>
        public Vec2 Clamp(Vec2 point, double margin = 0)
        {
            return new Vec2(Pull(point.X, MinX + margin, MaxX - margin), Pull(point.Y, MinY + margin, MaxY - margin));
        }

        private static double Pull(double v, double low, double high)
        {
            return high < low ? (low + high) / 2 : Math.Max(low, Math.Min(high, v));
        }
    }

    public static class LevelGenerator
    {
        private const double MinX = -11, MinY = -6, MaxX = 11, MaxY = 6;

        /// <summary>The least height the playfield keeps however thick the border: room to seat every player (U.RICO-T106).</summary>
        private const double MinPlayHeight = 6;

        /// <summary>The full arena's bounds (the border at its stock 32).</summary>
        public static Playfield Arena => new Playfield(MinX, MinY, MaxX, MaxY);

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

        /// <summary>How many spots are tried for a seat that must be able to hit seat 0.</summary>
        private const int ReachAttempts = 60;

        /// <summary>The seats (from 0) that are guaranteed a shot to and from seat 0: enough for any small game.</summary>
        private const int ReachSeats = 4;

        /// <summary>How many shapes are tried for a wall before it is given up.</summary>
        private const int Picks = 12;

        /// <summary>
        /// The playfield for a border of <paramref name="border"/> pixels (U.RICO-T106). The 32 px stock border is the whole arena
        /// (its frame is drawn outside it); every 32 px more pulls the sides in by that many pixels and the top and bottom by twice
        /// that, as the original's border did x and y. Deviation: the vertical inset is clamped so the playfield always keeps
        /// <see cref="MinPlayHeight"/> of height to seat the most players (the arena is only 12 units tall).
        /// </summary>
        public static Playfield Inset(int border)
        {
            double extra = Math.Max(0, border - LevelSeeds.MinBorder) * ShapeLibrary.UnitsPerPixel;
            double x = extra;
            double y = Math.Min(2 * extra, (MaxY - MinY - MinPlayHeight) / 2);
            return new Playfield(MinX + x, MinY + y, MaxX - x, MaxY - y);
        }

        /// <summary>
        /// The round for <paramref name="seed"/>: shapes picked and turned the way setup_shapes.gml does and laid per
        /// <paramref name="settings"/> clear of <paramref name="keepClear"/> (the practice dummies), then sixteen seat spots
        /// clear of every shape, the border, the dummies and each other. The same seed and settings always give the same round.
        /// </summary>
        public static RoundLayout Generate(int seed, LevelSettings settings = null, IEnumerable<Vec2> keepClear = null)
        {
            settings = settings ?? LevelSettings.Default;
            var dice = new Random(seed);
            Playfield field = Inset(settings.Border);
            var avoid = new List<Vec2>(keepClear ?? DefaultKeepClear(field));
            var shapes = new List<PlacedShape>();
            var laid = new List<LaidShape>();
            var walls = new List<Wall>();

            for (int i = 0; i < settings.PolygonCount; i++)
            {
                for (int pick = 0; pick < Picks; pick++)
                {
                    // A shape that will not fit anywhere is swapped for another pick (the original would retry forever).
                    ShapeId id = ShapeLibrary.Pick(settings.Shapes, dice);
                    double degrees = RotationDegrees(settings.Rotation, dice);
                    Vec2[] outline = Turn(ShapeLibrary.Outline(id), degrees);
                    PlacedShape placed = default;
                    bool found = false;
                    for (int attempt = 0; attempt < Attempts && !found; attempt++)
                    {
                        placed = PlacedShape.At(outline, RandomOrigin(outline, dice, field));
                        found = placed.InsideBorder(field.MinX, field.MinY, field.MaxX, field.MaxY) && ClearOfPoints(placed, SeatClearance, avoid)
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
                    laid.Add(new LaidShape(id, placed.Centre, degrees, c));
                    break;
                }
            }

            var level = new Level(field.MinX, field.MinY, field.MaxX, field.MaxY, walls);
            return new RoundLayout(level, PickSpots(dice, shapes, avoid, level, field), shapes.Count, laid);
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
            if (degrees % 90 == 0)
            {
                // Quarter turns are exact, so turned bars and squares stay square to the axes.
                int quarter = (int)(degrees / 90) % 4;
                cos = new[] { 1.0, 0.0, -1.0, 0.0 }[quarter];
                sin = new[] { 0.0, 1.0, 0.0, -1.0 }[quarter];
            }

            var turned = new Vec2[outline.Length];
            for (int i = 0; i < outline.Length; i++)
            {
                turned[i] = new Vec2(outline[i].X * cos - outline[i].Y * sin, outline[i].X * sin + outline[i].Y * cos);
            }

            return turned;
        }

        /// <summary>A random spot for the shape's origin (its middle) that keeps the whole outline inside the border.</summary>
        private static Vec2 RandomOrigin(Vec2[] outline, Random dice, Playfield field)
        {
            double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
            foreach (Vec2 v in outline)
            {
                minX = Math.Min(minX, v.X);
                maxX = Math.Max(maxX, v.X);
                minY = Math.Min(minY, v.Y);
                maxY = Math.Max(maxY, v.Y);
            }

            double lowX = field.MinX + EdgeGap - minX, highX = field.MaxX - EdgeGap - maxX;
            double lowY = field.MinY + EdgeGap - minY, highY = field.MaxY - EdgeGap - maxY;
            // A shape wider than the border cannot fit: aim for the middle and let the border check refuse it.
            return new Vec2(highX > lowX ? Between(dice, lowX, highX) : 0, highY > lowY ? Between(dice, lowY, highY) : 0);
        }

        /// <summary>The practice dummies and, sampled along them, the electric arcs and laser gates every round lays.</summary>
        private static List<Vec2> DefaultKeepClear(Playfield field)
        {
            var keep = new List<Vec2>();
            foreach (Vec2 start in Seating.DummyStarts)
            {
                keep.Add(field.Clamp(start, EdgeGap));
            }

            HazardRules hazards = HazardRules.Default;
            foreach (Vec2[] segment in hazards.Arcs.Concat(hazards.Gates))
            {
                Vec2 from = field.Clamp(segment[0], EdgeGap), to = field.Clamp(segment[1], EdgeGap);
                int steps = Math.Max(1, (int)Math.Ceiling(Vec2.Distance(from, to) / 0.5));
                for (int i = 0; i <= steps; i++)
                {
                    double t = (double)i / steps;
                    keep.Add(new Vec2(from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t));
                }
            }

            return keep;
        }

        private static List<Vec2> PickSpots(Random dice, List<PlacedShape> shapes, List<Vec2> avoid, Level level, Playfield field)
        {
            var spots = new List<Vec2>(MatchRules.MaxSeats);
            double edge = Seating.PlayerRadius + 0.3;
            for (int seat = 0; seat < MatchRules.MaxSeats; seat++)
            {
                Vec2 spot = default;
                bool found = false;
                // First look for a spot that can trade shots with seat 0 (for the first few seats), so a small game never
                // starts in a level where nobody can hit anybody (U.RICO-T72); a crowded level falls through to the rest.
                for (int attempt = 0; attempt < ReachAttempts && !found && seat > 0 && seat < ReachSeats; attempt++)
                {
                    spot = new Vec2(Between(dice, field.MinX + edge, field.MaxX - edge), Between(dice, field.MinY + edge, field.MaxY - edge));
                    found = ClearOfAll(spot, SeatGap, spots) && ClearOfAll(spot, DummyClearance, avoid)
                        && ClearOfShapes(spot, SeatClearance, shapes)
                        && CanTradeShots(level, spots[0], spot);
                }

                // Relax the clearances only if a crowded level leaves no room, so a seat is never left without a spot.
                for (int relax = 0; relax < 4 && !found; relax++)
                {
                    double scale = 1.0 - 0.1 * relax;
                    for (int attempt = 0; attempt < 200 && !found; attempt++)
                    {
                        spot = new Vec2(Between(dice, field.MinX + edge, field.MaxX - edge), Between(dice, field.MinY + edge, field.MaxY - edge));
                        found = ClearOfAll(spot, SeatGap * scale, spots) && ClearOfAll(spot, DummyClearance, avoid)
                            && ClearOfShapes(spot, SeatClearance * scale, shapes);
                    }
                }

                // Last resort on a crowded level: only the bare minimum room, so a seat never lands inside a polygon.
                for (int attempt = 0; attempt < 3000 && !found; attempt++)
                {
                    spot = new Vec2(Between(dice, field.MinX + edge, field.MaxX - edge), Between(dice, field.MinY + edge, field.MaxY - edge));
                    found = ClearOfAll(spot, Seating.PlayerRadius * 2 + 0.05, spots) && ClearOfAll(spot, DummyClearance, avoid)
                        && ClearOfShapes(spot, Seating.PlayerRadius + 0.05, shapes);
                }

                spots.Add(found ? spot : Seating.SpawnPoint(seat));
            }

            return spots;
        }

        /// <summary>True when each of the two standing points has an aim that hits the other (with nobody else in the way).</summary>
        private static bool CanTradeShots(Level level, Vec2 a, Vec2 b)
        {
            var targets = new[] { new Target("a", a, Seating.PlayerRadius), new Target("b", b, Seating.PlayerRadius) };
            return BounceSolver.CanHit(a, "b", level, targets) && BounceSolver.CanHit(b, "a", level, targets);
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
