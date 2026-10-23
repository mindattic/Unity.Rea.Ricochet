// The line math the 2013 original aimed with, ported from its scripts (design reference only, UR-LAW-2):
//   LineLineIntersection_X/_Y.gml  -> LineLineIntersection: where two infinite lines cross.
//   lines_intersect.gml            -> SegmentIntersection: the parametric t (and u) where two segments cross.
//   bullet_step.gml                -> ReflectAngle: "outgoing = 2 x normal - 180 - incoming" off a surface, and the
//                                     per-axis speed flip off a border (Reflect with an axis normal is the same thing).
// Plus the ray/circle test the solver uses to find a target on the path.
using System;

namespace Ricochet.Core
{
    public static class Geometry
    {
        /// <summary>Below this, a denominator counts as zero (parallel lines) and a distance as "touching".</summary>
        public const double Epsilon = 1e-9;

        /// <summary>
        /// Where the infinite line through <paramref name="a1"/>-<paramref name="a2"/> crosses the one through
        /// <paramref name="b1"/>-<paramref name="b2"/>; false when they are parallel or coincide. The GML scripts assumed
        /// an intersection and divided by zero otherwise; this reports it instead. The two returns are exactly
        /// LineLineIntersection_X and LineLineIntersection_Y.
        /// </summary>
        public static bool LineLineIntersection(Vec2 a1, Vec2 a2, Vec2 b1, Vec2 b2, out Vec2 point)
        {
            double x1 = a1.X, y1 = a1.Y, x2 = a2.X, y2 = a2.Y, x3 = b1.X, y3 = b1.Y, x4 = b2.X, y4 = b2.Y;
            double denominator = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
            if (Math.Abs(denominator) < Epsilon)
            {
                point = Vec2.Zero;
                return false;
            }

            double a = x1 * y2 - y1 * x2;
            double b = x3 * y4 - y3 * x4;
            point = new Vec2((a * (x3 - x4) - (x1 - x2) * b) / denominator, (a * (y3 - y4) - (y1 - y2) * b) / denominator);
            return true;
        }

        /// <summary>
        /// lines_intersect.gml: the multipliers along the first segment (<paramref name="t"/>, so the point is
        /// p1 + t * (p2 - p1)) and the second (<paramref name="u"/>) where the two lines cross. Returns false when they are
        /// parallel; with <paramref name="segmentsOnly"/>, also when the crossing is off either segment (0..1). GML returned
        /// 0 for "no intersection", which could not be told from a hit at p1; this keeps the two apart.
        /// </summary>
        public static bool SegmentIntersection(Vec2 p1, Vec2 p2, Vec2 p3, Vec2 p4, bool segmentsOnly, out double t, out double u)
        {
            t = 0;
            u = 0;
            double ud = (p4.Y - p3.Y) * (p2.X - p1.X) - (p4.X - p3.X) * (p2.Y - p1.Y);
            if (Math.Abs(ud) < Epsilon)
            {
                return false;
            }

            t = ((p4.X - p3.X) * (p1.Y - p3.Y) - (p4.Y - p3.Y) * (p1.X - p3.X)) / ud;
            u = ((p2.X - p1.X) * (p1.Y - p3.Y) - (p2.Y - p1.Y) * (p1.X - p3.X)) / ud;
            return !segmentsOnly || (t >= 0 && t <= 1 && u >= 0 && u <= 1);
        }

        /// <summary><paramref name="direction"/> mirrored off a surface with unit normal <paramref name="normal"/>.</summary>
        public static Vec2 Reflect(Vec2 direction, Vec2 normal)
        {
            return direction - 2.0 * Vec2.Dot(direction, normal) * normal;
        }

        /// <summary>bullet_step.gml's bounce in degrees: outgoing = 2 * normal - 180 - incoming, wrapped to [0, 360).</summary>
        public static double ReflectAngle(double incomingDegrees, double normalDegrees)
        {
            return Angles.Normalize(2.0 * normalDegrees - 180.0 - incomingDegrees);
        }

        /// <summary>
        /// The distance along the ray from <paramref name="origin"/> (unit <paramref name="direction"/>) to where it first
        /// enters the circle, or false when it misses or the circle is behind. A ray that starts inside hits at 0.
        /// </summary>
        public static bool RayCircle(Vec2 origin, Vec2 direction, Vec2 center, double radius, out double distance)
        {
            distance = 0;
            Vec2 toOrigin = origin - center;
            double c = toOrigin.LengthSquared - radius * radius;
            if (c <= 0)
            {
                return true;
            }

            double b = Vec2.Dot(toOrigin, direction);
            if (b > 0)
            {
                return false; // outside and moving away
            }

            double discriminant = b * b - c;
            if (discriminant < 0)
            {
                return false;
            }

            distance = -b - Math.Sqrt(discriminant);
            return distance >= 0;
        }
    }
}
