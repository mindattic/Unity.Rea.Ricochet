// What one press of fire sends out with each weapon, solved up front like a single shot so the server and every peer
// fly the same thing from the same synced inputs (the weapon, the aim, the fuse and any extra bounce). The original's
// shoot_bullet/shoot_laser/shoot_shotgun/shoot_grenade/shoot_railgun/shoot_vortex.gml spawned objects that stepped a
// pixel at a time (design reference only, UR-LAW-2); here each is one or more solved ShotPaths plus what they strike:
//   Cannon   one shell along the aim until its bounces run out; it stops in the first player it reaches (hit.gml's damage).
//   Laser    a beam at a shell's speed (shoot_laser.gml); otherwise a cannon shot.
//   Shotgun  a fan of pellets (pelletCount, +/- shotgunSpread degrees), each its own bouncing shot doing a share of a hit.
//   Grenade  bounces like a shell but passes through players, and goes off when its fuse runs out (grenade_countdown.gml)
//            or on its bounce limit (grenade_step.gml), hurting everyone within its blast (check_grenade_intersection.gml).
//   Railgun  a slug in a straight line through walls and redirectors to the border (shoot_railgun.gml's 2000 px line),
//            hitting every player on the way.
//   Vortex   flies like a shell and implodes where it stops, or where its shooter sets it off in mid-flight
//            (vortex_detonate.gml, Detonate), its pull flings the level's shapes out and one that strikes a tank hurts it (VortexStorm, U.RICO-T96).
// A shot that runs into a barrier (a hazard, UR-T6) stops there and strikes nothing: a grenade or vortex stopped by one
// is lost without going off, as obj_shieldwall destroyed them.
using System;
using System.Collections.Generic;

namespace Ricochet.Core.Arsenal
{
    using Ricochet.Core.Aim;
    using Ricochet.Core.Art;
    using Ricochet.Core.Level;
    /// <summary>One player struck by a volley.</summary>
    public readonly struct Strike
    {
        public Strike(string targetId, int bounces, double scale)
        {
            TargetId = targetId;
            Bounces = bounces;
            Scale = scale;
        }

        public string TargetId { get; }

        /// <summary>The bounces the striking path made first (hit.gml's damage grows with them).</summary>
        public int Bounces { get; }

        /// <summary>The share of a full hit's damage (a pellet's is less than 1).</summary>
        public double Scale { get; }
    }

    public sealed class Volley
    {
        private readonly List<ShotPath> _paths;
        private readonly List<Strike> _strikes;

        internal Volley(Weapon weapon, List<ShotPath> paths, double speed, List<Strike> strikes, bool blast, Vec2 blastCenter, double blastRadius)
        {
            Weapon = weapon;
            _paths = paths;
            Speed = speed;
            _strikes = strikes;
            HasBlast = blast;
            BlastCenter = blastCenter;
            BlastRadius = blastRadius;
        }

        public Weapon Weapon { get; }

        /// <summary>Every projectile's path (one, except a shotgun's pellets).</summary>
        public IReadOnlyList<ShotPath> Paths => _paths;

        /// <summary>The first (the only one, except for a shotgun): what the preview draws and the AI aims with.</summary>
        public ShotPath Main => _paths[0];

        /// <summary>Units a second the projectiles fly at.</summary>
        public double Speed { get; }

        /// <summary>Everyone it strikes when it lands.</summary>
        public IReadOnlyList<Strike> Strikes => _strikes;

        /// <summary>True for a grenade (or vortex): it goes off at <see cref="BlastCenter"/>.</summary>
        public bool HasBlast { get; }

        public Vec2 BlastCenter { get; }

        public double BlastRadius { get; }

        /// <summary>The hazards (barrier ids) the volley's projectiles ran into, each once, in path order.</summary>
        public IReadOnlyList<int> HazardsHit
        {
            get
            {
                var ids = new List<int>();
                foreach (ShotPath path in _paths)
                {
                    if (path.End == ShotEndKind.Absorbed && path.HazardId >= 0 && !ids.Contains(path.HazardId))
                    {
                        ids.Add(path.HazardId);
                    }
                }

                return ids;
            }
        }

        /// <summary>Seconds until the last projectile lands.</summary>
        public double Duration
        {
            get
            {
                double longest = 0;
                foreach (ShotPath path in _paths)
                {
                    longest = Math.Max(longest, path.Length);
                }

                return longest / Speed;
            }
        }

        /// <summary>
        /// The earliest distance along any path at which the volley comes within <paramref name="radius"/> of
        /// <paramref name="center"/> (a pickup lying there), or null when none does.
        /// </summary>
        public double? PassDistance(Vec2 center, double radius)
        {
            double? best = null;
            foreach (ShotPath path in _paths)
            {
                double? distance = path.PassDistance(center, radius);
                if (distance != null && (best == null || distance.Value < best.Value))
                {
                    best = distance;
                }
            }

            return best;
        }
    }

    /// <summary>A volley in the air: one <see cref="ProjectileFlight"/> per path, all at the volley's speed.</summary>
    public sealed class VolleyFlight
    {
        private readonly List<ProjectileFlight> _flights = new List<ProjectileFlight>();

        public VolleyFlight(Volley volley)
        {
            Volley = volley ?? throw new ArgumentNullException(nameof(volley));
            foreach (ShotPath path in volley.Paths)
            {
                _flights.Add(new ProjectileFlight(path, volley.Speed));
            }
        }

        public Volley Volley { get; }

        public IReadOnlyList<ProjectileFlight> Flights => _flights;

        /// <summary>True once every projectile has landed.</summary>
        public bool Landed
        {
            get
            {
                foreach (ProjectileFlight flight in _flights)
                {
                    if (!flight.Landed)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>Moves every projectile on; returns the contacts passed in this step, all told.</summary>
        public int Advance(double seconds)
        {
            int passed = 0;
            foreach (ProjectileFlight flight in _flights)
            {
                passed += flight.Advance(seconds);
            }

            return passed;
        }
    }

    public static class Arsenal
    {
        /// <summary>
        /// Solves what <paramref name="weapon"/> fired from <paramref name="origin"/> at <paramref name="heading"/> does. <paramref name="targets"/> may include the shooter (a shot that banks back hurts
        /// them, as hit.gml's self-hit did); the muzzle starts clear of them.
        /// </summary>
        /// <param name="fuse">A grenade's fuse in seconds (ignored by the others).</param>
        /// <param name="extraBounces">Extra Bounce items used this turn: each lets the shot survive one more contact.</param>
        public static Volley Fire(Weapon weapon, Vec2 origin, double heading, double fuse, int extraBounces,
            Level level, IReadOnlyList<Target> targets, ShotRules rules = null, ArsenalRules arsenal = null)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            arsenal = arsenal ?? ArsenalRules.Default;
            ShotRules shot = (rules ?? ShotRules.Default).Clone();
            shot.MaxBounces = Math.Max(1, shot.MaxBounces + Math.Max(0, extraBounces));
            var paths = new List<ShotPath>();
            var strikes = new List<Strike>();

            switch (weapon)
            {
                case Weapon.Laser:
                {
                    ShotPath path = BounceSolver.Solve(origin, heading, level, targets, shot);
                    paths.Add(path);
                    AddTargetStrike(path, 1, strikes);
                    return new Volley(weapon, paths, shot.Speed * arsenal.LaserSpeed, strikes, false, default, 0);
                }

                case Weapon.Shotgun:
                {
                    int pellets = Math.Max(1, arsenal.Pellets);
                    for (int i = 0; i < pellets; i++)
                    {
                        double offset = pellets == 1 ? 0 : arsenal.PelletSpread * (2.0 * i / (pellets - 1) - 1);
                        ShotPath path = BounceSolver.Solve(origin, heading + offset, level, targets, shot);
                        paths.Add(path);
                        AddTargetStrike(path, arsenal.PelletDamage, strikes);
                    }

                    // The middle pellet first, so Main is the one along the aim.
                    int middle = pellets / 2;
                    ShotPath centre = paths[middle];
                    paths.RemoveAt(middle);
                    paths.Insert(0, centre);
                    return new Volley(weapon, paths, shot.Speed, strikes, false, default, 0);
                }

                case Weapon.Grenade:
                {
                    // The fuse is the range: it flies at a shell's speed until the fuse runs out.
                    double range = arsenal.ClampFuse(fuse) * shot.Speed;
                    ShotPath path = BounceSolver.Solve(origin, heading, level, null, shot, range);
                    paths.Add(path);
                    if (path.End == ShotEndKind.Absorbed)
                    {
                        return new Volley(weapon, paths, shot.Speed, strikes, false, default, 0);
                    }

                    AddBlastStrikes(path.Impact, arsenal.GrenadeRadius, path.Bounces, targets, strikes);
                    return new Volley(weapon, paths, shot.Speed, strikes, true, path.Impact, arsenal.GrenadeRadius);
                }

                case Weapon.Railgun:
                {
                    var open = new Level(level.MinX, level.MinY, level.MaxX, level.MaxY, null, null, null, level.Breaches);
                    shot.MaxBounces = 1;
                    ShotPath path = BounceSolver.Solve(origin, heading, open, null, shot);
                    paths.Add(path);
                    if (targets != null)
                    {
                        foreach (Target target in targets)
                        {
                            if (Geometry.SegmentDistance(path.Start, path.Impact, target.Center, out _) <= target.Radius
                                && Vec2.Distance(origin, target.Center) > target.Radius)
                            {
                                strikes.Add(new Strike(target.Id, 0, 1));
                            }
                        }
                    }

                    return new Volley(weapon, paths, shot.Speed * arsenal.RailgunSpeed, strikes, false, default, 0);
                }

                case Weapon.Vortex:
                {
                    ShotPath path = BounceSolver.Solve(origin, heading, level, targets, shot);
                    return Implode(path, shot.Speed, targets, arsenal, level);
                }

                default:
                {
                    ShotPath path = BounceSolver.Solve(origin, heading, level, targets, shot);
                    paths.Add(path);
                    AddTargetStrike(path, 1, strikes);
                    return new Volley(Weapon.Cannon, paths, shot.Speed, strikes, false, default, 0);
                }
            }
        }

        /// <summary>
        /// vortex_detonate.gml: a vortex in the air set off <paramref name="distance"/> from the muzzle. Returns the volley
        /// cut short there, imploding at that point (and striking whoever is within its pull then); the volley unchanged
        /// when it is not a vortex, was stopped by a barrier, or the distance is not short of where it lands anyway.
        /// </summary>
        public static Volley Detonate(Volley volley, double distance, IReadOnlyList<Target> targets, ArsenalRules arsenal = null, Level level = null)
        {
            if (volley == null || volley.Weapon != Weapon.Vortex || !volley.HasBlast)
            {
                return volley;
            }

            ShotPath cut = volley.Main.Truncate(distance);
            return ReferenceEquals(cut, volley.Main) ? volley : Implode(cut, volley.Speed, targets, arsenal ?? ArsenalRules.Default, level);
        }

        /// <summary>
        /// shoot_redirector.gml (U.RICO-T80): a redirector flies along the aim like a shell (through players, bouncing off
        /// the walls and the border) until its owner presses the item key again (redirector_detonate). The path is solved
        /// out to <see cref="ItemLooks.RedirectorMaxSeconds"/> of flight; <see cref="PlaceRedirector"/> says where it stops.
        /// </summary>
        public static ShotPath FlyRedirector(Vec2 origin, double aim, Level level, ShotRules rules)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            // redirector_step bounces off the border and the shapes (obj_polygon_parent) only: hazards, standing redirectors
            // and breaches in the border are nothing to it.
            return FlyItem(origin, aim, Bare(level, false), rules, ItemLooks.SpeedFactor(Item.Redirector) * ItemLooks.RedirectorMaxSeconds);
        }

        /// <summary>The most contacts an item's flight is solved through.</summary>
        public const int RedirectorMaxBounces = 1000;

        // An item flying `seconds` of shot-speed time (seconds already scaled by its speed factor), bouncing as it goes.
        private static ShotPath FlyItem(Vec2 origin, double aim, Level level, ShotRules rules, double seconds)
        {
            ShotRules flying = (rules ?? ShotRules.Default).Clone();
            double range = flying.Speed * seconds;
            flying.MaxBounces = RedirectorMaxBounces;
            return BounceSolver.Solve(origin, aim, level, null, flying, range);
        }

        // The level as a flying item sees it: the border and the walls (the shapes), and the standing redirectors only when
        // `redirectors` (obj_redirector turns a teleporter); no hazards, no breaches (the items test the border by position).
        private static Level Bare(Level level, bool redirectors)
        {
            return new Level(level.MinX, level.MinY, level.MaxX, level.MaxY, level.Walls, redirectors ? level.Redirectors : null);
        }

        /// <summary>
        /// redirector_detonate.gml: the redirector <paramref name="flight"/> (see <see cref="FlyRedirector"/>) stops
        /// <paramref name="distance"/> along its path and becomes a rail redirector there, sending shots on along the heading
        /// it was last flying in. Returns that spot; <paramref name="heading"/> is the redirector's.
        /// </summary>
        public static Vec2 PlaceRedirector(ShotPath flight, double distance, Level level, ShotRules rules, out double heading)
        {
            if (flight == null)
            {
                throw new ArgumentNullException(nameof(flight));
            }

            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            rules = rules ?? ShotRules.Default;
            ShotPath stopped = flight.Truncate(distance);
            stopped.PositionAt(stopped.Length, out heading);

            // Stopped on a surface: set it down a little short, so it does not sit on what it hit.
            bool inTheAir = stopped.End == ShotEndKind.Spent || stopped.End == ShotEndKind.Detonated;
            Vec2 spot = inTheAir ? stopped.Impact : stopped.Impact - Vec2.FromAngle(heading) * rules.MuzzleOffset;
            double x = Math.Max(level.MinX + rules.MuzzleOffset, Math.Min(level.MaxX - rules.MuzzleOffset, spot.X));
            double y = Math.Max(level.MinY + rules.MuzzleOffset, Math.Min(level.MaxY - rules.MuzzleOffset, spot.Y));
            return new Vec2(x, y);
        }

        /// <summary>
        /// shoot_teleporter.gml + obj_teleporter (U.RICO-T80): a teleporter flies along the aim (through players and hazards,
        /// turned by a standing redirector) and teleports its shooter the moment it first strikes a shape or the border
        /// (teleport_player), instead of bouncing on. Returns where the shooter lands (<see cref="TeleportSpot"/>);
        /// <paramref name="path"/> is its flight, ending there.
        /// </summary>
        public static Vec2 Teleport(Vec2 origin, double heading, Level level, ShotRules rules, double clearance, out ShotPath path)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            rules = rules ?? ShotRules.Default;
            path = FlyItem(origin, heading, Bare(level, true), rules, ItemLooks.SpeedFactor(Item.Teleporter) * ItemLooks.RedirectorMaxSeconds);
            foreach (Contact contact in path.Contacts)
            {
                if (contact.Kind != ContactKind.Redirector)
                {
                    path = path.Truncate(contact.Distance); // the first shape or border: it teleports there
                    break;
                }
            }

            return TeleportSpot(path, level, rules, clearance);
        }

        /// <summary>
        /// teleport_player.gml: the shooter lands Len = 40 px (the muzzle offset) back along the teleporter's last heading from
        /// where its <paramref name="flight"/> stopped, kept inside the border by <paramref name="clearance"/>.
        /// </summary>
        public static Vec2 TeleportSpot(ShotPath flight, Level level, ShotRules rules, double clearance)
        {
            if (flight == null)
            {
                throw new ArgumentNullException(nameof(flight));
            }

            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            rules = rules ?? ShotRules.Default;
            flight.PositionAt(flight.Length, out double lastHeading);
            Vec2 spot = flight.Impact - Vec2.FromAngle(lastHeading) * rules.MuzzleOffset;
            double x = Math.Max(level.MinX + clearance, Math.Min(level.MaxX - clearance, spot.X));
            double y = Math.Max(level.MinY + clearance, Math.Min(level.MaxY - clearance, spot.Y));
            return new Vec2(x, y);
        }

        /// <summary>
        /// shoot_shrinkray.gml + obj_shrinkray (U.RICO-T90): a ray along the aim at bulletSpeed * 4, through tanks and hazards,
        /// that stops on the first shape or the border it touches (its only collision is obj_polygon_parent, the shapes and
        /// the border pieces). Returns the index in <paramref name="level"/>'s walls of the shape's wall it struck, or -1 for
        /// the border. <paramref name="path"/> is its flight, ending there.
        /// </summary>
        public static int ShrinkRay(Vec2 origin, double heading, Level level, ShotRules rules, out ShotPath path)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            rules = rules ?? ShotRules.Default;
            path = FlyItem(origin, heading, Bare(level, false), rules, ItemLooks.SpeedFactor(Item.ShrinkRay) * ItemLooks.RedirectorMaxSeconds);
            if (path.Contacts.Count == 0)
            {
                return -1;
            }

            Contact first = path.Contacts[0];
            path = path.Truncate(first.Distance);
            return first.Kind == ContactKind.Wall ? first.Index : -1;
        }

        // A vortex's end: lost without going off when a barrier stopped it, else an implosion that strikes everyone in reach.
        private static Volley Implode(ShotPath path, double speed, IReadOnlyList<Target> targets, ArsenalRules arsenal, Level level)
        {
            var paths = new List<ShotPath> { path };
            var strikes = new List<Strike>();
            if (path.End == ShotEndKind.Absorbed)
            {
                return new Volley(Weapon.Vortex, paths, speed, strikes, false, default, 0);
            }

            // vortex_step.gml: the pull and blow move the level's shapes; a tank is hurt only when a flung shape strikes it.
            int seed = unchecked((int)Math.Round(path.Impact.X * 1000) * 31 + (int)Math.Round(path.Impact.Y * 1000));
            Target? struck = VortexStorm.Strike(path.Impact, arsenal.VortexRadius, level, targets, seed);
            if (struck.HasValue)
            {
                strikes.Add(new Strike(struck.Value.Id, path.Bounces, 1));
            }

            return new Volley(Weapon.Vortex, paths, speed, strikes, true, path.Impact, arsenal.VortexRadius);
        }

        private static void AddTargetStrike(ShotPath path, double scale, List<Strike> strikes)
        {
            if (path.End == ShotEndKind.Target && path.TargetId != null)
            {
                strikes.Add(new Strike(path.TargetId, path.Bounces, scale));
            }
        }

        private static void AddBlastStrikes(Vec2 center, double radius, int bounces, IReadOnlyList<Target> targets, List<Strike> strikes)
        {
            if (targets == null)
            {
                return;
            }

            foreach (Target target in targets)
            {
                if (Vec2.Distance(center, target.Center) <= radius + target.Radius)
                {
                    strikes.Add(new Strike(target.Id, bounces, 1));
                }
            }
        }
    }
}
