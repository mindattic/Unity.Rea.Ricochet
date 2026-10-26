// The solved flight of one shot: where it starts, every contact it makes, and how it ends. The preview draws it before
// the shot is fired and ProjectileFlight walks along it after, so what the player is shown is exactly what flies.
using System;
using System.Collections.Generic;

namespace Ricochet.Core.Aim
{
    using Ricochet.Core.Level;
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

        /// <summary>The shot ran into a barrier (a hazard, UR-T6) and stopped there; see <see cref="ShotPath.HazardId"/>.</summary>
        Absorbed,

        /// <summary>The shot reached the border where a breach is and flew out of the level.</summary>
        Escaped,

        /// <summary>The shot was set off in mid-flight (a vortex detonated by its shooter).</summary>
        Detonated,
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

        internal ShotPath(Vec2 origin, double heading, double power, List<Vec2> points, List<Contact> contacts, ShotEndKind end, string targetId,
            int hazardId = -1)
        {
            HazardId = hazardId;
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

        /// <summary>The barrier's hazard id the shot ran into (<see cref="ShotEndKind.Absorbed"/> only), else -1.</summary>
        public int HazardId { get; }

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

        /// <summary>
        /// How far along the path the shot comes within <paramref name="radius"/> of <paramref name="center"/> (the
        /// nearest approach on the first leg that does), or null when it never does: what a pickup lying there is
        /// collected at (the original's drop objects collided with every bullet, pellet, grenade and laser).
        /// </summary>
        public double? PassDistance(Vec2 center, double radius)
        {
            for (int i = 1; i < _points.Count; i++)
            {
                double gap = Geometry.SegmentDistance(_points[i - 1], _points[i], center, out double along);
                if (gap <= radius)
                {
                    return _cumulative[i - 1] + along;
                }
            }

            return _points.Count == 1 && Vec2.Distance(_points[0], center) <= radius ? 0 : (double?)null;
        }

        /// <summary>
        /// The path cut short <paramref name="distance"/> from the muzzle, ending <see cref="ShotEndKind.Detonated"/> there
        /// (a vortex set off in mid-flight, detonate_vortex.gml); this path itself when the distance is not short of its end.
        /// </summary>
        public ShotPath Truncate(double distance)
        {
            if (double.IsNaN(distance) || distance >= Length - Geometry.Epsilon)
            {
                return this;
            }

            distance = Math.Max(0, distance);
            var points = new List<Vec2> { _points[0] };
            for (int i = 1; i < _points.Count && _cumulative[i] < distance; i++)
            {
                points.Add(_points[i]);
            }

            points.Add(PositionAt(distance, out _));
            var contacts = new List<Contact>();
            foreach (Contact contact in _contacts)
            {
                if (contact.Distance < distance)
                {
                    contacts.Add(contact);
                }
            }

            return new ShotPath(Origin, Heading, Power, points, contacts, ShotEndKind.Detonated, null);
        }

        /// <summary>
        /// The stretch of the path between <paramref name="from"/> and <paramref name="to"/> along it (each clamped to the
        /// path), as points into <paramref name="into"/> (cleared first): the two ends and every corner between. Empty when
        /// the stretch is empty. What a beam drawn along the path shows (laser_draw's line from the origin to the head).
        /// </summary>
        public void Slice(double from, double to, List<Vec2> into)
        {
            into.Clear();
            from = Math.Max(0, Math.Min(Length, from));
            to = Math.Max(0, Math.Min(Length, to));
            if (double.IsNaN(from) || double.IsNaN(to) || to <= from)
            {
                return;
            }

            into.Add(PositionAt(from, out _));
            for (int i = 1; i < _points.Count - 1; i++)
            {
                if (_cumulative[i] > from && _cumulative[i] < to)
                {
                    into.Add(_points[i]);
                }
            }

            into.Add(PositionAt(to, out _));
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
