// Driving a tank: move_forward.gml / move_backward.gml set the current player's hspeed and vspeed along the barrel's
// heading (cos, -sin of image_angle times moveSpeed) while W or S is held, stop_moving.gml zeroes them on release, and
// driving at all sets hasMoved, which forfeits that turn's shot (Match.Move). Here the step is a pure function of the
// position, the heading and the level so it is testable and the server can run it for any seat: the tank stays inside
// the level and out of its walls, sliding along one axis when the other is blocked.
using System;

namespace Ricochet.Core.Aim
{
    using Ricochet.Core.Level;
    using Ricochet.Core.Match;
    public static class Driving
    {
        /// <summary>
        /// Where a tank at <paramref name="from"/> with its barrel at <paramref name="heading"/> ends up after driving
        /// <paramref name="direction"/> (+1 forward, -1 backward, 0 stopped) for <paramref name="seconds"/>.
        /// </summary>
        public static Vec2 Step(Vec2 from, double heading, double direction, double seconds, Level level, double radius, ShotRules rules = null)
        {
            rules = rules ?? ShotRules.Default;
            direction = double.IsNaN(direction) ? 0 : Math.Max(-1, Math.Min(1, direction));
            if (direction == 0 || seconds <= 0 || double.IsNaN(seconds) || double.IsNaN(heading))
            {
                return from;
            }

            Vec2 delta = Vec2.FromAngle(heading) * (direction * rules.DriveSpeed * seconds);
            Vec2 full = from + delta;
            if (Free(full, level, radius))
            {
                return full;
            }

            Vec2 alongX = new Vec2(from.X + delta.X, from.Y);
            if (Free(alongX, level, radius))
            {
                return alongX;
            }

            Vec2 alongY = new Vec2(from.X, from.Y + delta.Y);
            return Free(alongY, level, radius) ? alongY : from;
        }

        /// <summary>A tank of <paramref name="radius"/> may stand at <paramref name="at"/>: inside the border and clear of every wall.</summary>
        public static bool Free(Vec2 at, Level level, double radius)
        {
            if (level == null)
            {
                return true;
            }

            if (at.X - radius < level.MinX || at.X + radius > level.MaxX || at.Y - radius < level.MinY || at.Y + radius > level.MaxY)
            {
                return false;
            }

            foreach (Wall wall in level.Walls)
            {
                if (DistanceToSegment(at, wall.A, wall.B) < radius)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>The distance from <paramref name="p"/> to the segment <paramref name="a"/>-<paramref name="b"/>.</summary>
        public static double DistanceToSegment(Vec2 p, Vec2 a, Vec2 b)
        {
            Vec2 ab = b - a;
            double lengthSquared = ab.LengthSquared;
            double t = lengthSquared < Geometry.Epsilon ? 0 : Math.Max(0, Math.Min(1, Vec2.Dot(p - a, ab) / lengthSquared));
            return Vec2.Distance(p, a + ab * t);
        }
    }
}
