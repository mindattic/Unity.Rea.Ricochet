// The bounce prediction: from a shooter's centre, an aim and a power, trace the shot off the level's borders, walls
// and redirectors until it hits a target, reaches its bounce limit or runs out of range. The 2013 original stepped a
// point one pixel at a time (find_target_angle_from_bounce.gml) and asked collision_point/collision_line what it was
// touching; this casts exact rays instead, with the original's intersection math (Geometry), so the preview and the
// flight are the same path to the last decimal and the server and every client agree on it. A level's hazards (UR-T6)
// take part too: a barrier stops the shot where it touches it (Absorbed), and a border breach lets it fly out (Escaped).
using System;
using System.Collections.Generic;

namespace Ricochet.Core.Aim
{
    using Ricochet.Core.Level;
    public static class BounceSolver
    {
        // A ray starting on a surface it just left would hit it again at distance ~0; anything closer than this is that.
        private const double SelfHit = 1e-7;

        // Two border distances closer than this are a corner: both speeds flip.
        private const double Corner = 1e-7;

        /// <summary>Solves the shot. <paramref name="targets"/> must not include the shooter.</summary>
        /// <param name="origin">The shooter's centre.</param>
        /// <param name="heading">The aim in degrees (0 = right, 90 = up).</param>
        /// <param name="power">0..1 (clamped): the shot's range, see <see cref="ShotRules.RangeFor"/>.</param>
        /// <param name="level">What it bounces off.</param>
        /// <param name="targets">What it can hit; may be null.</param>
        /// <param name="rules">Null for the defaults.</param>
        public static ShotPath Solve(Vec2 origin, double heading, double power, Level level, IReadOnlyList<Target> targets = null, ShotRules rules = null)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            rules = rules ?? ShotRules.Default;
            heading = Angles.Normalize(double.IsNaN(heading) ? 0 : heading);
            power = ShotRules.ClampPower(power);
            Vec2 direction = Vec2.FromAngle(heading);
            Vec2 position = origin + direction * rules.MuzzleOffset;
            double remaining = rules.RangeFor(power);
            double travelled = 0;
            int maxContacts = Math.Max(1, rules.MaxBounces);

            var points = new List<Vec2> { position };
            var contacts = new List<Contact>();

            // A muzzle already inside a target hits it point blank.
            string pointBlank = TargetAt(position, targets);
            if (pointBlank != null)
            {
                points.Add(position);
                return new ShotPath(origin, heading, power, points, contacts, ShotEndKind.Target, pointBlank);
            }

            for (int guard = 0; guard < maxContacts + 1; guard++)
            {
                Hit surface = NearestSurface(position, direction, level);
                Hit target = NearestTarget(position, direction, targets);

                // A target in front of the next surface (or level with it) and within range: impact.
                if (target.Found && target.Distance <= remaining && (!surface.Found || target.Distance <= surface.Distance))
                {
                    points.Add(position + direction * target.Distance);
                    return new ShotPath(origin, heading, power, points, contacts, ShotEndKind.Target, target.TargetId);
                }

                // Nothing to hit before the range runs out: the shot dies in mid-air.
                if (!surface.Found || surface.Distance > remaining)
                {
                    points.Add(position + direction * remaining);
                    return new ShotPath(origin, heading, power, points, contacts, ShotEndKind.Spent, null);
                }

                Vec2 point = position + direction * surface.Distance;
                if (surface.Barrier)
                {
                    points.Add(point);
                    return new ShotPath(origin, heading, power, points, contacts, ShotEndKind.Absorbed, null, surface.Index);
                }

                if (surface.Escape)
                {
                    points.Add(point);
                    return new ShotPath(origin, heading, power, points, contacts, ShotEndKind.Escaped, null);
                }

                travelled += surface.Distance;
                remaining -= surface.Distance;
                double headingIn = direction.Angle;
                bool last = contacts.Count + 1 >= maxContacts;
                if (last)
                {
                    contacts.Add(new Contact(surface.Kind, point, travelled, headingIn, headingIn, false, surface.Index));
                    points.Add(point);
                    return new ShotPath(origin, heading, power, points, contacts, ShotEndKind.BounceLimit, null);
                }

                direction = Deflect(direction, surface, level);
                contacts.Add(new Contact(surface.Kind, point, travelled, headingIn, direction.Angle, true, surface.Index));
                points.Add(point);
                position = point;
            }

            // Unreachable: every pass either ends the shot or adds a contact, and the last contact ends it.
            return new ShotPath(origin, heading, power, points, contacts, ShotEndKind.BounceLimit, null);
        }

        /// <summary>
        /// find_target_angle(_from_bounce).gml: sweeps the aim from 0 degrees in <paramref name="step"/>-degree steps for
        /// headings whose shot hits <paramref name="targetId"/>, or null when none does. The original took the first such
        /// degree, which is often a graze that a hair of rounding turns into a miss; this returns the middle of the first
        /// unbroken run of hitting headings instead. The AI and bots aim with it; it is the same solver the preview uses.
        /// </summary>
        public static double? FindHeading(Vec2 origin, double power, string targetId, Level level, IReadOnlyList<Target> targets,
            ShotRules rules = null, double step = 0.5)
        {
            if (step <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(step));
            }

            double? first = null;
            double last = 0;
            for (double heading = 0; heading < 360; heading += step)
            {
                ShotPath path = Solve(origin, heading, power, level, targets, rules);
                bool hits = path.End == ShotEndKind.Target && path.TargetId == targetId;
                if (hits)
                {
                    first = first ?? heading;
                    last = heading;
                }
                else if (first != null)
                {
                    break;
                }
            }

            return first == null ? (double?)null : (first.Value + last) / 2;
        }

        private static Vec2 Deflect(Vec2 direction, Hit surface, Level level)
        {
            switch (surface.Kind)
            {
                case ContactKind.Border:
                    // bullet_step.gml: hspeed = -hspeed off a side, vspeed = -vspeed off the top or bottom.
                    return new Vec2(surface.FlipX ? -direction.X : direction.X, surface.FlipY ? -direction.Y : direction.Y);
                case ContactKind.Wall:
                    return Geometry.Reflect(direction, level.Walls[surface.Index].Normal);
                default:
                    // check_rail_redirector_intersection.gml: the shot leaves along the redirector's image_angle.
                    return Vec2.FromAngle(level.Redirectors[surface.Index].Heading);
            }
        }

        private static Hit NearestSurface(Vec2 position, Vec2 direction, Level level)
        {
            Hit best = Border(position, direction, level);
            Vec2 far = position + direction; // a unit-length probe: t along it is the distance

            for (int i = 0; i < level.Walls.Count; i++)
            {
                Wall wall = level.Walls[i];
                if (Geometry.SegmentIntersection(position, far, wall.A, wall.B, false, out double t, out double u)
                    && u >= 0 && u <= 1 && t > SelfHit && (!best.Found || t < best.Distance))
                {
                    best = new Hit { Found = true, Distance = t, Kind = ContactKind.Wall, Index = i };
                }
            }

            for (int i = 0; i < level.Redirectors.Count; i++)
            {
                Redirector redirector = level.Redirectors[i];
                if (Geometry.SegmentIntersection(position, far, redirector.A, redirector.B, false, out double t, out double u)
                    && u >= 0 && u <= 1 && t > SelfHit && (!best.Found || t < best.Distance))
                {
                    best = new Hit { Found = true, Distance = t, Kind = ContactKind.Redirector, Index = i };
                }
            }

            for (int i = 0; i < level.Barriers.Count; i++)
            {
                Barrier barrier = level.Barriers[i];
                double t;
                bool touches;
                if (barrier.IsRound)
                {
                    touches = Geometry.RayCircle(position, direction, barrier.A, barrier.Radius, out t);
                }
                else
                {
                    touches = Geometry.SegmentIntersection(position, far, barrier.A, barrier.B, false, out t, out double u)
                        && u >= 0 && u <= 1 && t >= 0;
                }

                // A barrier level with another surface wins: the shot stops on it rather than bouncing past.
                if (touches && (!best.Found || t <= best.Distance))
                {
                    best = new Hit { Found = true, Distance = t, Barrier = true, Index = barrier.Id };
                }
            }

            return best;
        }

        private static Hit Border(Vec2 position, Vec2 direction, Level level)
        {
            double tx = double.PositiveInfinity;
            double ty = double.PositiveInfinity;
            if (direction.X > Geometry.Epsilon)
            {
                tx = (level.MaxX - position.X) / direction.X;
            }
            else if (direction.X < -Geometry.Epsilon)
            {
                tx = (level.MinX - position.X) / direction.X;
            }

            if (direction.Y > Geometry.Epsilon)
            {
                ty = (level.MaxY - position.Y) / direction.Y;
            }
            else if (direction.Y < -Geometry.Epsilon)
            {
                ty = (level.MinY - position.Y) / direction.Y;
            }

            // Already on (or past) a side and heading out of it: that side is hit now. Clamp so the shot never leaves.
            tx = Math.Max(0, tx);
            ty = Math.Max(0, ty);
            double t = Math.Min(tx, ty);
            if (double.IsInfinity(t))
            {
                return default;
            }

            bool flipX = tx - t <= Corner;
            bool flipY = ty - t <= Corner;
            bool escape = false;
            if (level.Breaches.Count > 0)
            {
                Vec2 point = position + direction * t;
                escape = (flipX && level.IsBreached(new Vec2(direction.X > 0 ? level.MaxX : level.MinX, point.Y), true))
                    || (flipY && level.IsBreached(new Vec2(point.X, direction.Y > 0 ? level.MaxY : level.MinY), false));
            }

            return new Hit
            {
                Found = true,
                Distance = t,
                Kind = ContactKind.Border,
                Index = -1,
                FlipX = flipX,
                FlipY = flipY,
                Escape = escape,
            };
        }

        private static Hit NearestTarget(Vec2 position, Vec2 direction, IReadOnlyList<Target> targets)
        {
            Hit best = default;
            if (targets == null)
            {
                return best;
            }

            foreach (Target target in targets)
            {
                if (Geometry.RayCircle(position, direction, target.Center, target.Radius, out double distance)
                    && (!best.Found || distance < best.Distance))
                {
                    best = new Hit { Found = true, Distance = distance, TargetId = target.Id };
                }
            }

            return best;
        }

        private static string TargetAt(Vec2 point, IReadOnlyList<Target> targets)
        {
            if (targets != null)
            {
                foreach (Target target in targets)
                {
                    if (Vec2.Distance(point, target.Center) <= target.Radius)
                    {
                        return target.Id;
                    }
                }
            }

            return null;
        }

        private struct Hit
        {
            public bool Found;
            public double Distance;
            public ContactKind Kind;
            public int Index;
            public bool FlipX;
            public bool FlipY;
            public string TargetId;
            public bool Barrier;
            public bool Escape;
        }
    }
}
