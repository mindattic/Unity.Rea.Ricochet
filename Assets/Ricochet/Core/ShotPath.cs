// The solved flight of one shot: where it starts, every contact it makes, and how it ends. The preview draws it before
// the shot is fired and ProjectileFlight walks along it after, so what the player is shown is exactly what flies.
using System;
using System.Collections.Generic;

namespace Ricochet.Core
{
    public enum ContactKind
    {
        /// <summary>The level's border: the shot's x or y speed flips (both, in a corner).</summary>
        Border,

        /// <summary>A mirror wall: the shot reflects off its normal.</summary>
        Wall,

        /// <summary>A rail redirector: the shot leaves along the redirector's heading.</summary>
        Redirector,
    }

    public enum ShotEndKind
    {
        /// <summary>The shot hit a target.</summary>
        Target,

        /// <summary>The shot detonated on the contact that reached its bounce limit.</summary>
        BounceLimit,

        /// <summary>The shot ran out of range (power) in mid-air.</summary>
        Spent,
    }

    /// <summary>One contact on the path.</summary>
    public readonly struct Contact
    {
        public Contact(ContactKind kind, Vec2 point, double distance, double headingIn, double headingOut, bool deflected, int index)
        {
            Kind = kind;
            Point = point;
            Distance = distance;
            HeadingIn = headingIn;
            HeadingOut = headingOut;
            Deflected = deflected;
            Index = index;
        }

        public ContactKind Kind { get; }

        public Vec2 Point { get; }

        /// <summary>How far along the path (from the muzzle) the contact is.</summary>
        public double Distance { get; }

        public double HeadingIn { get; }

        /// <summary>The heading after the contact (the same as <see cref="HeadingIn"/> when the shot detonated on it).</summary>
        public double HeadingOut { get; }

        /// <summary>False for the contact the shot detonated on.</summary>
        public bool Deflected { get; }

        /// <summary>Which wall or redirector (index into the level's list); -1 for the border.</summary>
        public int Index { get; }
    }

    public sealed class ShotPath
    {
        private readonly List<Vec2> _points;
        private readonly List<Contact> _contacts;
        private readonly double[] _cumulative;

        internal ShotPath(Vec2 origin, double heading, double power, List<Vec2> points, List<Contact> contacts, ShotEndKind end, string targetId)
        {
            Origin = origin;
            Heading = heading;
            Power = power;
            _points = points;
            _contacts = contacts;
            End = end;
            TargetId = targetId;
            _cumulative = new double[points.Count];
            for (int i = 1; i < points.Count; i++)
            {
                _cumulative[i] = _cumulative[i - 1] + Vec2.Distance(points[i - 1], points[i]);
            }
        }

        /// <summary>The shooter's centre.</summary>
        public Vec2 Origin { get; }

        /// <summary>The aim, in degrees.</summary>
        public double Heading { get; }

        public double Power { get; }

        /// <summary>The muzzle, every contact point, and the end point, in order.</summary>
        public IReadOnlyList<Vec2> Points => _points;

        public IReadOnlyList<Contact> Contacts => _contacts;

        public ShotEndKind End { get; }

        /// <summary>The target hit (<see cref="ShotEndKind.Target"/> only), else null.</summary>
        public string TargetId { get; }

        public Vec2 Start => _points[0];

        /// <summary>Where the shot stops: the impact point.</summary>
        public Vec2 Impact => _points[_points.Count - 1];

        /// <summary>The path's total length from the muzzle.</summary>
        public double Length => _cumulative[_cumulative.Length - 1];

        /// <summary>The contacts the shot bounced (or was redirected) off and kept flying; draw_bounceinfo's "Bounces: n".</summary>
        public int Bounces
        {
            get
            {
                int count = 0;
                foreach (Contact contact in _contacts)
                {
                    if (contact.Deflected)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>The point <paramref name="distance"/> along the path (clamped to it) and the heading there.</summary>
        public Vec2 PositionAt(double distance, out double heading)
        {
            distance = Math.Max(0, Math.Min(Length, distance));
            for (int i = 1; i < _points.Count; i++)
            {
                if (distance <= _cumulative[i] || i == _points.Count - 1)
                {
                    double segment = _cumulative[i] - _cumulative[i - 1];
                    Vec2 from = _points[i - 1];
                    Vec2 to = _points[i];
                    heading = segment > Geometry.Epsilon ? (to - from).Angle : Heading;
                    double along = segment > Geometry.Epsilon ? (distance - _cumulative[i - 1]) / segment : 0;
                    return from + (to - from) * along;
                }
            }

            heading = Heading;
            return _points[0];
        }

        /// <summary>Contacts at or before <paramref name="distance"/> along the path.</summary>
        public int ContactsBefore(double distance)
        {
            int count = 0;
            foreach (Contact contact in _contacts)
            {
                if (contact.Distance <= distance + Geometry.Epsilon)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
