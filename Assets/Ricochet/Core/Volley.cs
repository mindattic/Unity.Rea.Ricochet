// What one press of fire sends out with each weapon, solved up front like a single shot so the server and every peer
// fly the same thing from the same synced inputs (the weapon, the aim, the fuse and any extra bounce). The original's
// shoot_bullet/shoot_laser/shoot_shotgun/shoot_grenade/shoot_railgun/shoot_vortex.gml spawned objects that stepped a
// pixel at a time (design reference only, UR-LAW-2); here each is one or more solved ShotPaths plus what they strike:
//   Cannon   one shell along the aim at the aim's power; it stops in the first player it reaches (hit.gml's damage).
//   Laser    a beam: always full range, at three times a shell's speed; otherwise a cannon shot.
//   Shotgun  a fan of pellets (pelletCount, +/- shotgunSpread degrees), each its own bouncing shot doing a share of a hit.
//   Grenade  bounces like a shell but passes through players, and goes off when its fuse runs out (grenade_countdown.gml)
//            or on its bounce limit (grenade_step.gml), hurting everyone within its blast (check_grenade_intersection.gml).
//   Railgun  a slug in a straight line through walls and redirectors to the border (shoot_railgun.gml's 2000 px line),
//            hitting every player on the way.
//   Vortex   flies like a shell and implodes where it stops, or where its shooter sets it off in mid-flight
//            (vortex_detonate.gml, Detonate), hurting everyone within its pull and swallowing hazards (UR-T6, Hazards.cs).
// A shot that runs into a barrier (a hazard, UR-T6) stops there and strikes nothing: a grenade or vortex stopped by one
// is lost without going off, as obj_shieldwall destroyed them.
using System;
using System.Collections.Generic;

namespace Ricochet.Core
{
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
        /// Solves what <paramref name="weapon"/> fired from <paramref name="origin"/> at <paramref name="heading"/> and
        /// <paramref name="power"/> does. <paramref name="targets"/> may include the shooter (a shot that banks back hurts
        /// them, as hit.gml's self-hit did); the muzzle starts clear of them.
        /// </summary>
        /// <param name="fuse">A grenade's fuse in seconds (ignored by the others).</param>
        /// <param name="extraBounces">Extra Bounce items used this turn: each lets the shot survive one more contact.</param>
        public static Volley Fire(Weapon weapon, Vec2 origin, double heading, double power, double fuse, int extraBounces,
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
                    ShotPath path = BounceSolver.Solve(origin, heading, 1, level, targets, shot);
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
                        ShotPath path = BounceSolver.Solve(origin, heading + offset, power, level, targets, shot);
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
                    shot.MinRange = range;
                    shot.MaxRange = range;
                    ShotPath path = BounceSolver.Solve(origin, heading, 1, level, null, shot);
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
                    shot.MinRange = shot.MaxRange = 2 * ((level.MaxX - level.MinX) + (level.MaxY - level.MinY));
                    ShotPath path = BounceSolver.Solve(origin, heading, 1, open, null, shot);
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
                    ShotPath path = BounceSolver.Solve(origin, heading, power, level, targets, shot);
                    return Implode(path, shot.Speed, targets, arsenal);
                }

                default:
                {
                    ShotPath path = BounceSolver.Solve(origin, heading, power, level, targets, shot);
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
        public static Volley Detonate(Volley volley, double distance, IReadOnlyList<Target> targets, ArsenalRules arsenal = null)
        {
            if (volley == null || volley.Weapon != Weapon.Vortex || !volley.HasBlast)
            {
                return volley;
            }

            ShotPath cut = volley.Main.Truncate(distance);
            return ReferenceEquals(cut, volley.Main) ? volley : Implode(cut, volley.Speed, targets, arsenal ?? ArsenalRules.Default);
        }

        /// <summary>
        /// shoot_redirector.gml: a redirector flies along the aim like a shell (through players, as far as the power sends
        /// it) and becomes a rail redirector where it stops, sending shots on along the heading it was last flying in.
        /// Returns that spot; <paramref name="heading"/> is the redirector's, <paramref name="path"/> its flight.
        /// </summary>
        public static Vec2 PlaceRedirector(Vec2 origin, double aim, double power, Level level, ShotRules rules, out double heading, out ShotPath path)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            rules = rules ?? ShotRules.Default;
            path = BounceSolver.Solve(origin, aim, power, level, null, rules);
            path.PositionAt(path.Length, out heading);

            // Stopped on a surface: set it down a little short, so it does not sit on what it hit.
            Vec2 spot = path.End == ShotEndKind.Spent ? path.Impact : path.Impact - Vec2.FromAngle(heading) * rules.MuzzleOffset;
            double x = Math.Max(level.MinX + rules.MuzzleOffset, Math.Min(level.MaxX - rules.MuzzleOffset, spot.X));
            double y = Math.Max(level.MinY + rules.MuzzleOffset, Math.Min(level.MaxY - rules.MuzzleOffset, spot.Y));
            return new Vec2(x, y);
        }

        /// <summary>
        /// shoot_teleporter.gml + teleport_player.gml: a teleporter flies along the aim (through players, bouncing like a
        /// shell, as far as the power sends it) and the shooter lands where it stops, stepped back along its last heading
        /// (the original's Len = 40 px) and kept inside the border. Returns that spot; <paramref name="path"/> is its flight.
        /// </summary>
        public static Vec2 Teleport(Vec2 origin, double heading, double power, Level level, ShotRules rules, double clearance, out ShotPath path)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            rules = rules ?? ShotRules.Default;
            path = BounceSolver.Solve(origin, heading, power, level, null, rules);
            path.PositionAt(path.Length, out double lastHeading);
            Vec2 spot = path.Impact - Vec2.FromAngle(lastHeading) * rules.MuzzleOffset;
            double x = Math.Max(level.MinX + clearance, Math.Min(level.MaxX - clearance, spot.X));
            double y = Math.Max(level.MinY + clearance, Math.Min(level.MaxY - clearance, spot.Y));
            return new Vec2(x, y);
        }

        /// <summary>
        /// shoot_shrinkray.gml: a ray along the aim at full range; returns the player it reaches first (never the shooter,
        /// <paramref name="selfId"/>), or null. <paramref name="path"/> is its flight.
        /// </summary>
        public static string ShrinkRay(Vec2 origin, double heading, string selfId, Level level, IReadOnlyList<Target> targets, ShotRules rules, out ShotPath path)
        {
            var others = new List<Target>();
            if (targets != null)
            {
                foreach (Target target in targets)
                {
                    if (target.Id != selfId)
                    {
                        others.Add(target);
                    }
                }
            }

            path = BounceSolver.Solve(origin, heading, 1, level, others, rules);
            return path.End == ShotEndKind.Target ? path.TargetId : null;
        }

        // A vortex's end: lost without going off when a barrier stopped it, else an implosion that strikes everyone in reach.
        private static Volley Implode(ShotPath path, double speed, IReadOnlyList<Target> targets, ArsenalRules arsenal)
        {
            var paths = new List<ShotPath> { path };
            var strikes = new List<Strike>();
            if (path.End == ShotEndKind.Absorbed)
            {
                return new Volley(Weapon.Vortex, paths, speed, strikes, false, default, 0);
            }

            AddBlastStrikes(path.Impact, arsenal.VortexRadius, path.Bounces, targets, strikes);
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
