// The shot on the wire (UR-T12, UR-LAW-1): the server solves a volley once and sends what it solved, so every peer draws
// the server's own bounce, not one it worked out again from the aim in a level (hazards, players, crates) that may not
// match the server's that frame. ShotWire packs a Volley into a flat list of numbers and a string of the target ids it
// names (what a [ToClients] call can carry) and unpacks it on the other side, exactly: doubles go over as doubles, so the
// client's points are the server's to the last bit. ShotOutcome is the other half: what the volley did once it landed,
// the damage each seat it struck took, summed per seat (a shotgun's pellets, a blast), for every peer to show.
using System;
using System.Collections.Generic;

namespace Ricochet.Core.Net
{
    using Ricochet.Core.Aim;
    using Ricochet.Core.Arsenal;
    public static class ShotWire
    {
        /// <summary>The format's version, the first number; Decode refuses any other.</summary>
        public const int Version = 1;

        /// <summary>The most projectiles, points per path, contacts per path and strikes Decode accepts.</summary>
        public const int MaxPaths = 64;
        public const int MaxPoints = 1024;
        public const int MaxStrikes = 256;

        private const int HeaderLength = 8;
        private const int PathHeaderLength = 8;
        private const int ContactLength = 8;
        private const int StrikeLength = 3;
        private const char Separator = '\n';

        /// <summary>
        /// Packs <paramref name="volley"/>: <paramref name="numbers"/> holds its weapon, speed, blast, every path (origin,
        /// aim, end, points, contacts) and every strike; <paramref name="ids"/> the target ids they name, one per line.
        /// </summary>
        public static void Encode(Volley volley, out double[] numbers, out string ids)
        {
            if (volley == null)
            {
                throw new ArgumentNullException(nameof(volley));
            }

            var names = new List<string>();
            var n = new List<double>
            {
                Version,
                (int)volley.Weapon,
                volley.Speed,
                volley.HasBlast ? 1 : 0,
                volley.BlastCenter.X,
                volley.BlastCenter.Y,
                volley.BlastRadius,
                volley.Paths.Count,
            };

            foreach (ShotPath path in volley.Paths)
            {
                n.Add(path.Origin.X);
                n.Add(path.Origin.Y);
                n.Add(path.Heading);
                n.Add(path.Power);
                n.Add((int)path.End);
                n.Add(path.HazardId);
                n.Add(IdIndex(path.TargetId, names));
                n.Add(path.Points.Count);
                foreach (Vec2 point in path.Points)
                {
                    n.Add(point.X);
                    n.Add(point.Y);
                }

                n.Add(path.Contacts.Count);
                foreach (Contact contact in path.Contacts)
                {
                    n.Add((int)contact.Kind);
                    n.Add(contact.Point.X);
                    n.Add(contact.Point.Y);
                    n.Add(contact.Distance);
                    n.Add(contact.HeadingIn);
                    n.Add(contact.HeadingOut);
                    n.Add(contact.Deflected ? 1 : 0);
                    n.Add(contact.Index);
                }
            }

            n.Add(volley.Strikes.Count);
            foreach (Strike strike in volley.Strikes)
            {
                n.Add(IdIndex(strike.TargetId, names));
                n.Add(strike.Bounces);
                n.Add(strike.Scale);
            }

            numbers = n.ToArray();
            ids = string.Join(Separator.ToString(), names);
        }

        /// <summary>
        /// Unpacks what <see cref="Encode"/> packed. Null when it is not a volley this version packed (too short, a count
        /// out of range, a number that is not finite, an id index past the ids): a peer then has nothing to draw, never
        /// a garbled shot.
        /// </summary>
        public static Volley Decode(double[] numbers, string ids)
        {
            if (numbers == null || numbers.Length < HeaderLength)
            {
                return null;
            }

            foreach (double number in numbers)
            {
                if (double.IsNaN(number) || double.IsInfinity(number))
                {
                    return null;
                }
            }

            string[] names = string.IsNullOrEmpty(ids) ? Array.Empty<string>() : ids.Split(Separator);
            var reader = new Reader(numbers);
            if (reader.Int() != Version)
            {
                return null;
            }

            int weapon = reader.Int();
            double speed = reader.Next();
            bool blast = reader.Int() != 0;
            var blastCenter = new Vec2(reader.Next(), reader.Next());
            double blastRadius = reader.Next();
            int pathCount = reader.Int();
            if (!InRange(weapon, ArsenalRules.Kinds) || speed <= 0 || pathCount < 1 || pathCount > MaxPaths)
            {
                return null;
            }

            var paths = new List<ShotPath>();
            for (int p = 0; p < pathCount; p++)
            {
                if (!reader.Has(PathHeaderLength))
                {
                    return null;
                }

                var origin = new Vec2(reader.Next(), reader.Next());
                double heading = reader.Next();
                double power = reader.Next();
                int end = reader.Int();
                int hazardId = reader.Int();
                int target = reader.Int();
                int pointCount = reader.Int();
                if (!Enum.IsDefined(typeof(ShotEndKind), end) || !Name(target, names, out string targetId)
                    || pointCount < 1 || pointCount > MaxPoints || !reader.Has(pointCount * 2 + 1))
                {
                    return null;
                }

                var points = new List<Vec2>(pointCount);
                for (int i = 0; i < pointCount; i++)
                {
                    points.Add(new Vec2(reader.Next(), reader.Next()));
                }

                int contactCount = reader.Int();
                if (contactCount < 0 || contactCount > MaxPoints || !reader.Has(contactCount * ContactLength))
                {
                    return null;
                }

                var contacts = new List<Contact>(contactCount);
                for (int i = 0; i < contactCount; i++)
                {
                    int kind = reader.Int();
                    var point = new Vec2(reader.Next(), reader.Next());
                    double distance = reader.Next();
                    double headingIn = reader.Next();
                    double headingOut = reader.Next();
                    bool deflected = reader.Int() != 0;
                    int index = reader.Int();
                    if (!Enum.IsDefined(typeof(ContactKind), kind))
                    {
                        return null;
                    }

                    contacts.Add(new Contact((ContactKind)kind, point, distance, headingIn, headingOut, deflected, index));
                }

                paths.Add(new ShotPath(origin, heading, power, points, contacts, (ShotEndKind)end, targetId, hazardId));
            }

            if (!reader.Has(1))
            {
                return null;
            }

            int strikeCount = reader.Int();
            if (strikeCount < 0 || strikeCount > MaxStrikes || !reader.Has(strikeCount * StrikeLength))
            {
                return null;
            }

            var strikes = new List<Strike>(strikeCount);
            for (int i = 0; i < strikeCount; i++)
            {
                int target = reader.Int();
                int bounces = reader.Int();
                double scale = reader.Next();
                if (!Name(target, names, out string targetId) || targetId == null)
                {
                    return null;
                }

                strikes.Add(new Strike(targetId, bounces, scale));
            }

            if (reader.Has(1))
            {
                return null; // trailing numbers: not something Encode wrote
            }

            return new Volley((Weapon)weapon, paths, speed, strikes, blast, blastCenter, blastRadius);
        }

        private static int IdIndex(string id, List<string> names)
        {
            if (id == null)
            {
                return -1;
            }

            int index = names.IndexOf(id);
            if (index < 0)
            {
                names.Add(id);
                index = names.Count - 1;
            }

            return index;
        }

        private static bool Name(int index, string[] names, out string id)
        {
            id = null;
            if (index == -1)
            {
                return true;
            }

            if (index < 0 || index >= names.Length)
            {
                return false;
            }

            id = names[index];
            return true;
        }

        private static bool InRange(int value, int count) => value >= 0 && value < count;

        private sealed class Reader
        {
            private readonly double[] _numbers;
            private int _at;

            public Reader(double[] numbers)
            {
                _numbers = numbers;
            }

            public bool Has(int count) => count >= 0 && _numbers.Length - _at >= count;

            public double Next() => _at < _numbers.Length ? _numbers[_at++] : 0;

            public int Int()
            {
                double value = Next();
                return value >= int.MinValue && value <= int.MaxValue ? (int)Math.Round(value) : int.MinValue;
            }
        }
    }

    /// <summary>
    /// What a landed volley did in a match: the damage each seat it struck took, summed per seat in the order they were
    /// first struck (hit.gml per strike; a shotgun's pellets and a blast's victims add up), and the seats it put out.
    /// </summary>
    public sealed class ShotOutcome
    {
        private readonly List<int> _seats = new List<int>();
        private readonly List<int> _damage = new List<int>();
        private readonly List<int> _knockedOut = new List<int>();

        /// <summary>The seats struck, each once, in the order they were first struck.</summary>
        public IReadOnlyList<int> Seats => _seats;

        /// <summary>The damage each of <see cref="Seats"/> took, at the same index.</summary>
        public IReadOnlyList<int> Damage => _damage;

        /// <summary>The seats this volley put out of the round.</summary>
        public IReadOnlyList<int> KnockedOut => _knockedOut;

        /// <summary>All the damage done.</summary>
        public int Total
        {
            get
            {
                int total = 0;
                foreach (int damage in _damage)
                {
                    total += damage;
                }

                return total;
            }
        }

        /// <summary>True when the volley struck nobody.</summary>
        public bool Missed => _seats.Count == 0;

        /// <summary>The points the shooter scored with it (hit.gml's point_combo for each opponent struck; U.RICO-T48 floats them).</summary>
        public int Points { get; private set; }

        /// <summary>Adds <paramref name="points"/> scored (negative ones are ignored).</summary>
        public void AddPoints(int points)
        {
            if (points > 0)
            {
                Points += points;
            }
        }

        /// <summary>One strike's damage on <paramref name="seat"/> (added to what it already took from this volley).</summary>
        public void Add(int seat, int damage)
        {
            if (seat < 0)
            {
                return;
            }

            damage = Math.Max(0, damage);
            int at = _seats.IndexOf(seat);
            if (at < 0)
            {
                _seats.Add(seat);
                _damage.Add(damage);
            }
            else
            {
                _damage[at] += damage;
            }
        }

        /// <summary><paramref name="seat"/> was put out of the round by this volley.</summary>
        public void KnockOut(int seat)
        {
            if (seat >= 0 && !_knockedOut.Contains(seat))
            {
                _knockedOut.Add(seat);
            }
        }

        /// <summary>The damage <paramref name="seat"/> took from this volley (0 when it was not struck).</summary>
        public int DamageTo(int seat)
        {
            int at = _seats.IndexOf(seat);
            return at < 0 ? 0 : _damage[at];
        }

        /// <summary>The struck seats and their damage, for the wire.</summary>
        public void Encode(out int[] seats, out int[] damage, out int[] knockedOut)
        {
            seats = _seats.ToArray();
            damage = _damage.ToArray();
            knockedOut = _knockedOut.ToArray();
        }

        /// <summary>
        /// The outcome the wire carried; null when the lists do not belong together (different lengths, a seat out of
        /// range or twice, negative damage, more seats than a match has).
        /// </summary>
        public static ShotOutcome Decode(int[] seats, int[] damage, int[] knockedOut, int maxSeats) => Decode(seats, damage, knockedOut, 0, maxSeats);

        /// <summary>As <see cref="Decode(int[], int[], int[], int)"/>, with the <paramref name="points"/> the shooter scored.</summary>
        public static ShotOutcome Decode(int[] seats, int[] damage, int[] knockedOut, int points, int maxSeats)
        {
            seats = seats ?? Array.Empty<int>();
            damage = damage ?? Array.Empty<int>();
            knockedOut = knockedOut ?? Array.Empty<int>();
            if (seats.Length != damage.Length || seats.Length > maxSeats || knockedOut.Length > maxSeats)
            {
                return null;
            }

            var outcome = new ShotOutcome();
            for (int i = 0; i < seats.Length; i++)
            {
                if (seats[i] < 0 || seats[i] >= maxSeats || damage[i] < 0 || outcome._seats.Contains(seats[i]))
                {
                    return null;
                }

                outcome.Add(seats[i], damage[i]);
            }

            foreach (int seat in knockedOut)
            {
                if (seat < 0 || seat >= maxSeats || outcome._knockedOut.Contains(seat))
                {
                    return null;
                }

                outcome.KnockOut(seat);
            }

            if (points < 0)
            {
                return null;
            }

            outcome.AddPoints(points);
            return outcome;
        }
    }
}
