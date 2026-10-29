// The original's level shapes (UR-T60): sixteen hand-made outlines per kind (obj_polygon1..16, obj_triangle1..16,
// obj_bar1..16, obj_circle1..16, obj_square1..16), exported from their sprites' collision masks into shapes.json (UR-T35), and the
// pick setup_shapes.gml makes: the chosen kind's sixteen with choose(), or for "random" one pick from each kind and then
// choose() among the five.
using System;

namespace Ricochet.Core.Level
{
    using Ricochet.Core.Aim;

    /// <summary>One shape picked from the library: which kind and which of its sixteen.</summary>
    public readonly struct ShapeId
    {
        public ShapeId(ShapeKind kind, int index)
        {
            Kind = kind;
            Index = index;
        }

        /// <summary>One of the five concrete kinds (never <see cref="ShapeKind.Random"/>).</summary>
        public ShapeKind Kind { get; }

        /// <summary>0 to 15 (obj_xxx1 to obj_xxx16).</summary>
        public int Index { get; }
    }

    public static class ShapeLibrary
    {
        /// <summary>Shapes per kind.</summary>
        public const int PerKind = 16;

        /// <summary>Arena units per original room pixel: the original's 1920 pixel room is the arena's 22 units wide.</summary>
        public const double UnitsPerPixel = 22.0 / 1920.0;

        /// <summary>setup_shapes.gml's pick for <paramref name="kind"/>.</summary>
        public static ShapeId Pick(ShapeKind kind, Random dice)
        {
            if (kind != ShapeKind.Random)
            {
                return new ShapeId(kind, dice.Next(PerKind));
            }

            // The original rolls one shape of every kind, then chooses among the five.
            var rolls = new int[5];
            for (int i = 0; i < rolls.Length; i++)
            {
                rolls[i] = dice.Next(PerKind);
            }

            int chosen = dice.Next(rolls.Length);
            return new ShapeId((ShapeKind)chosen, rolls[chosen]);
        }

        /// <summary>The shape's outline in arena units around its origin (y up), counter-clockwise.</summary>
        public static Vec2[] Outline(ShapeId id)
        {
            int[] flat = Table(id.Kind)[id.Index];
            var outline = new Vec2[flat.Length / 2];
            for (int i = 0; i < outline.Length; i++)
            {
                outline[i] = new Vec2(flat[2 * i] * UnitsPerPixel, flat[2 * i + 1] * UnitsPerPixel);
            }

            return outline;
        }

        private static int[][] Table(ShapeKind kind) => Ricochet.Core.Data.GameData.Current.Shapes.Table(kind);
    }

    /// <summary>A shape's outline laid at a spot in the arena.</summary>
    public readonly struct PlacedShape
    {
        private PlacedShape(Vec2[] corners, Vec2 centre, double radius)
        {
            Corners = corners;
            Centre = centre;
            Radius = radius;
        }

        public Vec2[] Corners { get; }

        /// <summary>The shape's origin (where the original placed the instance).</summary>
        public Vec2 Centre { get; }

        /// <summary>The farthest any corner is from the origin.</summary>
        public double Radius { get; }

        public static PlacedShape At(Vec2[] outline, Vec2 origin)
        {
            var corners = new Vec2[outline.Length];
            double radius = 0;
            for (int i = 0; i < corners.Length; i++)
            {
                radius = Math.Max(radius, outline[i].Length);
                corners[i] = new Vec2(outline[i].X + origin.X, outline[i].Y + origin.Y);
            }

            return new PlacedShape(corners, origin, radius);
        }

        public bool InsideBorder(double minX, double minY, double maxX, double maxY)
        {
            foreach (Vec2 c in Corners)
            {
                if (c.X < minX || c.X > maxX || c.Y < minY || c.Y > maxY)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>True when the point is outside the shape and at least <paramref name="clearance"/> from its outline.</summary>
        public bool ClearOfPoint(Vec2 point, double clearance)
        {
            if (Vec2.Distance(point, Centre) > Radius + clearance)
            {
                return true;
            }

            return !Contains(point) && DistanceTo(point) >= clearance;
        }

        /// <summary>True when the point is inside the shape.</summary>
        public bool Contains(Vec2 point)
        {
            bool inside = false;
            for (int i = 0, j = Corners.Length - 1; i < Corners.Length; j = i++)
            {
                Vec2 a = Corners[i], b = Corners[j];
                if ((a.Y > point.Y) != (b.Y > point.Y) && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        /// <summary>How far the point is from the nearest part of the outline.</summary>
        public double DistanceTo(Vec2 point)
        {
            double best = double.MaxValue;
            for (int i = 0; i < Corners.Length; i++)
            {
                best = Math.Min(best, Geometry.SegmentDistance(Corners[i], Corners[(i + 1) % Corners.Length], point, out _));
            }

            return best;
        }

        /// <summary>True when the shapes cross, one lies inside the other, or they come within <paramref name="gap"/>.</summary>
        public bool Touches(PlacedShape other, double gap)
        {
            if (Contains(other.Corners[0]) || other.Contains(Corners[0]))
            {
                return true;
            }

            foreach (Vec2 c in Corners)
            {
                if (other.DistanceTo(c) < gap)
                {
                    return true;
                }
            }

            foreach (Vec2 c in other.Corners)
            {
                if (DistanceTo(c) < gap)
                {
                    return true;
                }
            }

            for (int i = 0; i < Corners.Length; i++)
            {
                Vec2 a = Corners[i], b = Corners[(i + 1) % Corners.Length];
                for (int j = 0; j < other.Corners.Length; j++)
                {
                    if (Geometry.SegmentIntersection(a, b, other.Corners[j], other.Corners[(j + 1) % other.Corners.Length], true, out _, out _))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
