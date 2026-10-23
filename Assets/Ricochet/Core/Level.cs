// What a shot can hit: the level's borders (the original's obj_border_top/bottom/left/right, which flipped the
// bullet's hspeed or vspeed), walls it bounces off like a mirror (obj_polygon_parent, normal_detect + the
// "2 x normal - 180 - incoming" rule), and rail redirectors (obj_redirector: whatever touches one leaves along the
// redirector's own angle). Targets are circles the shot stops in.
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
        public Level(double minX, double minY, double maxX, double maxY, IEnumerable<Wall> walls = null, IEnumerable<Redirector> redirectors = null)
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
        }

        public double MinX { get; }

        public double MinY { get; }

        public double MaxX { get; }

        public double MaxY { get; }

        public IReadOnlyList<Wall> Walls { get; }

        public IReadOnlyList<Redirector> Redirectors { get; }

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
