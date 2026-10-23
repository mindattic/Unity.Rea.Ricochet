// What a shot can hit: the level's borders (the original's obj_border_top/bottom/left/right, which flipped the
// bullet's hspeed or vspeed), walls it bounces off like a mirror (obj_polygon_parent, normal_detect + the
// "2 x normal - 180 - incoming" rule), and rail redirectors (obj_redirector: whatever touches one leaves along the
// redirector's own angle). Targets are circles the shot stops in. Level hazards (UR-T6, Hazards.cs) add two more:
// barriers, which stop a shot dead where it touches them (a shield wall, an electric arc, a laser beam, a tuft of
// foliage), and breaches, gaps a vortex tore in the border that a shot flies out through instead of bouncing.
using System;
using System.Collections.Generic;

namespace Ricochet.Core
{
    /// <summary>A mirror wall: a segment the shot bounces off.</summary>
    public sealed class Wall
    {
        public Wall(Vec2 a, Vec2 b)
        {
            if (Vec2.Distance(a, b) < Geometry.Epsilon)
            {
                throw new ArgumentException("A wall needs two different end points.");
            }

            A = a;
            B = b;
        }

        public Vec2 A { get; }

        public Vec2 B { get; }

        /// <summary>A unit normal (one of the two; reflection does not care which).</summary>
        public Vec2 Normal
        {
            get
            {
                Vec2 along = (B - A).Normalized;
                return new Vec2(-along.Y, along.X);
            }
        }
    }

    /// <summary>A rail redirector: a segment that sends whatever touches it off along <see cref="Heading"/>.</summary>
    public sealed class Redirector
    {
        public Redirector(Vec2 a, Vec2 b, double heading)
        {
            if (Vec2.Distance(a, b) < Geometry.Epsilon)
            {
                throw new ArgumentException("A redirector needs two different end points.");
            }

            A = a;
            B = b;
            Heading = Angles.Normalize(heading);
        }

        public Vec2 A { get; }

        public Vec2 B { get; }

        /// <summary>The direction, in degrees, a redirected shot leaves in.</summary>
        public double Heading { get; }

        public Vec2 Center => (A + B) * 0.5;
    }

    /// <summary>
    /// A level hazard a shot stops in: a segment (a shield wall, an electric arc, a laser beam) or a circle (a tuft of
    /// foliage). <see cref="Id"/> is the hazard's, so the server can tell which one a shot ran into.
    /// </summary>
    public sealed class Barrier
    {
        /// <summary>A segment barrier from <paramref name="a"/> to <paramref name="b"/>.</summary>
        public Barrier(int id, HazardKind kind, Vec2 a, Vec2 b)
        {
            if (Vec2.Distance(a, b) < Geometry.Epsilon)
            {
                throw new ArgumentException("A barrier segment needs two different end points.");
            }

            Id = id;
            Kind = kind;
            A = a;
            B = b;
        }

        /// <summary>A round barrier of <paramref name="radius"/> about <paramref name="center"/>.</summary>
        public Barrier(int id, HazardKind kind, Vec2 center, double radius)
        {
            if (!(radius > 0))
            {
                throw new ArgumentException("A round barrier needs a positive radius.");
            }

            Id = id;
            Kind = kind;
            A = center;
            B = center;
            Radius = radius;
        }

        public int Id { get; }

        public HazardKind Kind { get; }

        /// <summary>One end (a round barrier's centre).</summary>
        public Vec2 A { get; }

        /// <summary>The other end (a round barrier's centre).</summary>
        public Vec2 B { get; }

        /// <summary>A round barrier's radius; 0 for a segment.</summary>
        public double Radius { get; }

        public bool IsRound => Radius > 0;
    }

    /// <summary>
    /// A gap in the level's border from <see cref="A"/> to <see cref="B"/> (both on the same side): a shot reaching the
    /// border there leaves the level instead of bouncing.
    /// </summary>
    public sealed class Breach
    {
        public Breach(Vec2 a, Vec2 b)
        {
            A = a;
            B = b;
        }

        public Vec2 A { get; }

        public Vec2 B { get; }
    }

    /// <summary>Something a shot can hit and stop in: a player, a practice target.</summary>
    public readonly struct Target
    {
        public Target(string id, Vec2 center, double radius)
        {
            Id = id;
            Center = center;
            Radius = radius;
        }

        public string Id { get; }

        public Vec2 Center { get; }

        public double Radius { get; }
    }

    /// <summary>The playfield: an axis-aligned border rectangle plus the walls and redirectors inside it.</summary>
    public sealed class Level
    {
        public Level(double minX, double minY, double maxX, double maxY, IEnumerable<Wall> walls = null, IEnumerable<Redirector> redirectors = null,
            IEnumerable<Barrier> barriers = null, IEnumerable<Breach> breaches = null)
        {
            if (maxX <= minX || maxY <= minY)
            {
                throw new ArgumentException("A level's border must have a positive width and height.");
            }

            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
            Walls = new List<Wall>(walls ?? Array.Empty<Wall>());
            Redirectors = new List<Redirector>(redirectors ?? Array.Empty<Redirector>());
            Barriers = new List<Barrier>(barriers ?? Array.Empty<Barrier>());
            Breaches = new List<Breach>(breaches ?? Array.Empty<Breach>());
        }

        public double MinX { get; }

        public double MinY { get; }

        public double MaxX { get; }

        public double MaxY { get; }

        public IReadOnlyList<Wall> Walls { get; }

        public IReadOnlyList<Redirector> Redirectors { get; }

        /// <summary>Hazards a shot stops in (UR-T6).</summary>
        public IReadOnlyList<Barrier> Barriers { get; }

        /// <summary>Gaps in the border a shot flies out through (UR-T6).</summary>
        public IReadOnlyList<Breach> Breaches { get; }

        /// <summary>This level with more walls, redirectors, barriers and breaches added (the same border).</summary>
        public Level With(IEnumerable<Redirector> redirectors, IEnumerable<Barrier> barriers, IEnumerable<Breach> breaches)
        {
            var allRedirectors = new List<Redirector>(Redirectors);
            allRedirectors.AddRange(redirectors ?? Array.Empty<Redirector>());
            var allBarriers = new List<Barrier>(Barriers);
            allBarriers.AddRange(barriers ?? Array.Empty<Barrier>());
            var allBreaches = new List<Breach>(Breaches);
            allBreaches.AddRange(breaches ?? Array.Empty<Breach>());
            return new Level(MinX, MinY, MaxX, MaxY, Walls, allRedirectors, allBarriers, allBreaches);
        }

        /// <summary>
        /// True when <paramref name="point"/>, on the border, lies in a breach: on the left or right side
        /// (<paramref name="vertical"/>) or the bottom or top.
        /// </summary>
        public bool IsBreached(Vec2 point, bool vertical)
        {
            const double OnSide = 1e-6;
            foreach (Breach breach in Breaches)
            {
                if (vertical)
                {
                    double x = point.X;
                    if (Math.Abs(breach.A.X - x) <= OnSide && Math.Abs(breach.B.X - x) <= OnSide
                        && point.Y >= Math.Min(breach.A.Y, breach.B.Y) - OnSide && point.Y <= Math.Max(breach.A.Y, breach.B.Y) + OnSide)
                    {
                        return true;
                    }
                }
                else
                {
                    double y = point.Y;
                    if (Math.Abs(breach.A.Y - y) <= OnSide && Math.Abs(breach.B.Y - y) <= OnSide
                        && point.X >= Math.Min(breach.A.X, breach.B.X) - OnSide && point.X <= Math.Max(breach.A.X, breach.B.X) + OnSide)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool Contains(Vec2 p) => p.X >= MinX && p.X <= MaxX && p.Y >= MinY && p.Y <= MaxY;

        /// <summary>
        /// The practice level the game opens on: a 22 x 12 border (fits a 16:9 orthographic camera of size 7), a mirror
        /// wall angled across each upper corner, a flat wall in the middle, and two redirectors, one sending shots
        /// straight up and one sending them down-left, so a single shot can show every kind of contact.
        /// </summary>
        public static Level Default()
        {
            return new Level(-11, -6, 11, 6,
                new[]
                {
                    new Wall(new Vec2(-11, 3), new Vec2(-8, 6)),
                    new Wall(new Vec2(8, 6), new Vec2(11, 3)),
                    new Wall(new Vec2(-2, 0.5), new Vec2(2, 0.5)),
                },
                new[]
                {
                    new Redirector(new Vec2(-6.5, -1.5), new Vec2(-5.5, -1.5), 90),
                    new Redirector(new Vec2(6, 2.5), new Vec2(6, 3.5), 225),
                });
        }
    }
}
