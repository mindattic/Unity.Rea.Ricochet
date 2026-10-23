// A freshly randomised level for every round (UR-T23). The 2013 original's randomize_level.gml rebuilt the arena each
// round from the setup's polygon count (20), shapes (abstract), rotation (360) and overlap (no) settings via
// setup_shapes.gml, and randomize_player_position.gml dropped every player at a random spot. Here the same inputs make
// a Level of polygon-shaped mirror walls and sixteen seat spots from one seed, so every peer rebuilds the identical
// round from the seed alone and only the seed goes over the wire (UR-LAW-1).
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ricochet.Core
{
    /// <summary>The setup screen's level settings, with the original's defaults.</summary>
    public sealed class LevelSettings
    {
        /// <summary>How many polygons to lay (the original's 20).</summary>
        public int PolygonCount { get; set; } = 20;

        /// <summary>The widest a polygon may be turned, in degrees (the original's 360: any way round).</summary>
        public double Rotation { get; set; } = 360;

        /// <summary>True lets polygons overlap each other (the original's "no").</summary>
        public bool Overlap { get; set; }

        /// <summary>Fewest sides of an abstract shape.</summary>
        public int MinSides { get; set; } = 3;

        /// <summary>Most sides of an abstract shape.</summary>
        public int MaxSides { get; set; } = 6;

        /// <summary>Smallest distance from a polygon's centre to its corners.</summary>
        public double MinRadius { get; set; } = 0.6;

        /// <summary>Largest distance from a polygon's centre to its corners.</summary>
        public double MaxRadius { get; set; } = 1.3;

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

        /// <summary>The room a seat's spot keeps from every polygon's corners circle.</summary>
        private const double SeatClearance = Seating.PlayerRadius + 0.35;

        /// <summary>The room a seat's spot keeps from a practice dummy (never relaxed).</summary>
        private const double DummyClearance = Seating.PlayerRadius * 2 + 0.1;

        /// <summary>Seats stand at least this far apart.</summary>
        private const double SeatGap = 1.6;

        private const int Attempts = 60;

        /// <summary>
        /// The round for <paramref name="seed"/>: polygons laid per <paramref name="settings"/> clear of
        /// <paramref name="keepClear"/> (the practice dummies), then sixteen seat spots clear of every polygon, the border,
        /// the dummies and each other. The same seed and settings always give the same round.
        /// </summary>
        public static RoundLayout Generate(int seed, LevelSettings settings = null, IEnumerable<Vec2> keepClear = null)
        {
            settings = settings ?? LevelSettings.Default;
            var dice = new Random(seed);
            var avoid = new List<Vec2>(keepClear ?? DefaultKeepClear());
            var shapes = new List<(Vec2 Centre, double Radius)>();
            var walls = new List<Wall>();
            int minSides = Math.Max(3, settings.MinSides);
            int maxSides = Math.Max(minSides, settings.MaxSides);
            double minRadius = Math.Max(0.1, settings.MinRadius);
            double maxRadius = Math.Max(minRadius, settings.MaxRadius);
            double rotation = Math.Max(0, Math.Min(360, settings.Rotation));

            for (int i = 0; i < settings.PolygonCount; i++)
            {
                for (int attempt = 0; attempt < Attempts; attempt++)
                {
                    double radius = Between(dice, minRadius, maxRadius);
                    double gap = radius + EdgeGap;
                    var centre = new Vec2(Between(dice, MinX + gap, MaxX - gap), Between(dice, MinY + gap, MaxY - gap));
                    if (!ClearOfAll(centre, radius + SeatClearance, avoid) || (!settings.Overlap && Overlaps(centre, radius, shapes)))
                    {
                        continue;
                    }

                    int sides = dice.Next(minSides, maxSides + 1);
                    double turn = Between(dice, 0, rotation);
                    var corners = new Vec2[sides];
                    for (int k = 0; k < sides; k++)
                    {
                        double angle = (turn + 360.0 * k / sides) * Math.PI / 180.0;
                        corners[k] = new Vec2(centre.X + radius * Math.Cos(angle), centre.Y + radius * Math.Sin(angle));
                    }

                    for (int k = 0; k < sides; k++)
                    {
                        walls.Add(new Wall(corners[k], corners[(k + 1) % sides]));
                    }

                    shapes.Add((centre, radius));
                    break;
                }
            }

            var level = new Level(MinX, MinY, MaxX, MaxY, walls);
            return new RoundLayout(level, PickSpots(dice, shapes, avoid), shapes.Count);
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

        private static List<Vec2> PickSpots(Random dice, List<(Vec2 Centre, double Radius)> shapes, List<Vec2> avoid)
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

        private static bool ClearOfShapes(Vec2 point, double clearance, List<(Vec2 Centre, double Radius)> shapes)
        {
            foreach ((Vec2 centre, double radius) in shapes)
            {
                if (Vec2.Distance(point, centre) < radius + clearance)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Overlaps(Vec2 centre, double radius, List<(Vec2 Centre, double Radius)> shapes)
        {
            foreach ((Vec2 other, double otherRadius) in shapes)
            {
                if (Vec2.Distance(centre, other) < radius + otherRadius + 0.1)
                {
                    return true;
                }
            }

            return false;
        }

        private static double Between(Random dice, double low, double high) => low + dice.NextDouble() * (high - low);
    }
}
