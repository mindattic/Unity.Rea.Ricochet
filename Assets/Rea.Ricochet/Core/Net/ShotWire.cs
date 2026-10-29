// The shot on the wire (UR-T12, UR-LAW-1): the server solves a volley once and sends what it solved, so every peer draws
// the server's own bounce, not one it worked out again from the aim in a level (hazards, players, crates) that may not
// match the server's that frame. ShotWire packs a Volley into a flat list of numbers and a string of the target ids it
// names (what a [ToClients] call can carry) and unpacks it on the other side, exactly: doubles go over as doubles, so the
// client's points are the server's to the last bit. ShotOutcome is the other half: what the volley did once it landed,
// the damage each seat it struck took, summed per seat (a shotgun's pellets, a blast), for every peer to show.
// U.RICO-T79: an item's flight (EncodeFlight / DecodeFlight) goes over the same way, one path laid out as a volley's are.
using System;
using System.Collections.Generic;

namespace Ricochet.Core.Net
{
    using Ricochet.Core.Aim;
    using Ricochet.Core.Arsenal;
    public static class ShotWire
    {
        /// <summary>The format's version, the first number; Decode refuses any other.</summary>
        public const int Version = 3;

        /// <summary>The most projectiles, points per path, contacts per path and strikes Decode accepts.</summary>
        public const int MaxPaths = 64;
        public const int MaxPoints = 1024;
        public const int MaxStrikes = 256;
        public const int MaxStormShapes = 1024;
        public const int MaxStormWalls = 8192;

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
                WritePath(path, n, names);
            }

            n.Add(volley.Strikes.Count);
            foreach (Strike strike in volley.Strikes)
            {
                n.Add(IdIndex(strike.TargetId, names));
                n.Add(strike.Bounces);
                n.Add(strike.Scale);
            }

            // U.RICO-T134: a vortex's storm, so every peer walks the same shapes the same way and keeps where they land.
            WriteStorm(volley.Storm, n);

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

            if (!AllFinite(numbers))
            {
                return null;
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
                ShotPath path = ReadPath(reader, names);
                if (path == null)
                {
                    return null;
                }

                paths.Add(path);
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

            if (!ReadStorm(reader, out StormResult storm))
            {
                return null;
            }

            if (reader.Has(1))
            {
                return null; // trailing numbers: not something Encode wrote
            }

            return new Volley((Weapon)weapon, paths, speed, strikes, blast, blastCenter, blastRadius, storm);
        }

        /// <summary>
        /// Packs an item's flight (U.RICO-T79: a teleporter, shrink ray or redirector, which ItemUsed carries): the
        /// version, then the path laid out as a volley's paths are; <paramref name="ids"/> the target id it names.
        /// </summary>
        public static void EncodeFlight(ShotPath path, out double[] numbers, out string ids)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            var names = new List<string>();
            var n = new List<double> { Version };
            WritePath(path, n, names);
            numbers = n.ToArray();
            ids = string.Join(Separator.ToString(), names);
        }

        /// <summary>Unpacks what <see cref="EncodeFlight"/> packed; null when it is not a flight this version packed.</summary>
        public static ShotPath DecodeFlight(double[] numbers, string ids)
        {
            if (numbers == null || numbers.Length < 1 + PathHeaderLength || !AllFinite(numbers))
            {
                return null;
            }

            string[] names = string.IsNullOrEmpty(ids) ? Array.Empty<string>() : ids.Split(Separator);
            var reader = new Reader(numbers);
            if (reader.Int() != Version)
            {
                return null;
            }

            ShotPath path = ReadPath(reader, names);
            return path == null || reader.Has(1) ? null : path;
        }

        /// <summary>The most storms DecodeStorms accepts (a round's vortexes).</summary>
        public const int MaxStorms = 256;

        /// <summary>
        /// Packs the storms a level has kept, in the order it kept them (U.RICO-T140), so a client that joins after a storm replays
        /// them with Level.Restore and lays its walls where the host's are.
        /// </summary>
        public static double[] EncodeStorms(IReadOnlyList<StormResult> storms)
        {
            var n = new List<double> { Version, storms == null ? 0 : storms.Count };
            if (storms != null)
            {
                foreach (StormResult storm in storms)
                {
                    WriteStorm(storm, n);
                }
            }

            return n.ToArray();
        }

        /// <summary>Unpacks what <see cref="EncodeStorms"/> packed; null when it is not something this version packed.</summary>
        public static List<StormResult> DecodeStorms(double[] numbers)
        {
            if (numbers == null || numbers.Length < 2 || !AllFinite(numbers))
            {
                return null;
            }

            var reader = new Reader(numbers);
            if (reader.Int() != Version)
            {
                return null;
            }

            int count = reader.Int();
            if (count < 0 || count > MaxStorms)
            {
                return null;
            }

            var storms = new List<StormResult>(count);
            for (int i = 0; i < count; i++)
            {
                if (!ReadStorm(reader, out StormResult storm))
                {
                    return null;
                }

                storms.Add(storm);
            }

            return reader.Has(1) ? null : storms;
        }

        private static void WriteStorm(StormResult storm, List<double> n)
        {
            n.Add(storm.FlingSteps);
            n.Add(storm.Shapes.Count);
            foreach (StormShape shape in storm.Shapes)
            {
                n.Add(shape.Walls.Count);
                foreach (int wall in shape.Walls)
                {
                    n.Add(wall);
                }

                n.Add(shape.Pull.X);
                n.Add(shape.Pull.Y);
                n.Add(shape.Fling.X);
                n.Add(shape.Fling.Y);
                n.Add(shape.Pivot.X);
                n.Add(shape.Pivot.Y);
            }
        }

        // One storm as WriteStorm laid it out; false when it is not one.
        private static bool ReadStorm(Reader reader, out StormResult storm)
        {
            storm = null;
            if (!reader.Has(2))
            {
                return false;
            }

            int flingSteps = reader.Int();
            int shapeCount = reader.Int();
            if (flingSteps < 0 || flingSteps > VortexStorm.FlingSteps || shapeCount < 0 || shapeCount > MaxStormShapes)
            {
                return false;
            }

            var shapes = new List<StormShape>(shapeCount);
            int totalWalls = 0;
            for (int i = 0; i < shapeCount; i++)
            {
                if (!reader.Has(1))
                {
                    return false;
                }

                int wallCount = reader.Int();
                if (wallCount < 1 || wallCount > MaxStormWalls - totalWalls || !reader.Has(wallCount + 6))
                {
                    return false;
                }

                totalWalls += wallCount;

                var walls = new int[wallCount];
                for (int w = 0; w < wallCount; w++)
                {
                    walls[w] = reader.Int();
                    if (walls[w] < 0)
                    {
                        return false;
                    }
                }

                var pull = new Vec2(reader.Next(), reader.Next());
                var fling = new Vec2(reader.Next(), reader.Next());
                var pivot = new Vec2(reader.Next(), reader.Next());
                shapes.Add(new StormShape(walls, pull, fling, pivot));
            }

            storm = shapeCount == 0 && flingSteps == 0 ? StormResult.None : new StormResult(null, shapes, flingSteps);
            return true;
        }

        private static bool AllFinite(double[] numbers)
        {
            foreach (double number in numbers)
            {
                if (double.IsNaN(number) || double.IsInfinity(number))
                {
                    return false;
                }
            }

            return true;
        }

        private static void WritePath(ShotPath path, List<double> n, List<string> names)
        {
            n.Add(path.Origin.X);
            n.Add(path.Origin.Y);
            n.Add(path.Heading);
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

        // One path as WritePath laid it out; null when it is not one.
        private static ShotPath ReadPath(Reader reader, string[] names)
        {
            if (!reader.Has(PathHeaderLength))
            {
                return null;
            }

            var origin = new Vec2(reader.Next(), reader.Next());
            double heading = reader.Next();
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

            return new ShotPath(origin, heading, points, contacts, (ShotEndKind)end, targetId, hazardId);
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
