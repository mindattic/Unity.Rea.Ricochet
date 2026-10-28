// Level hazards (UR-T6): what a level holds besides its border, mirror walls and rail redirectors, and how each one
// comes and goes. Design reference only (UR-LAW-2); the 2013 original's pieces, and what became of each:
//   Shield wall   use_item.gml + spawn_shieldwall.gml: a hologram wall 60 px (about a unit) in front of the player, across
//                 their aim, 128 px (about 2.2 units) long. The owner's own shots pass through it; anyone else's that
//                 touches it shatters it and is stopped there (obj_shieldwall's collision events: the bullet destroyed, the
//                 turn over). Left alone it decays after a random 20-120 s (decayDelay). destroy_shieldwall.gml was an empty
//                 stub; its job is Struck (a shot) and Implode (a vortex) here.
//   Vortex        shoot_vortex.gml / vortex_detonate.gml (detonate_vortex.gml is an older copy): fire again while it is in
//                 the air to set it off there (Arsenal.Detonate). The original's implosion sucked every obj_polygon_parent
//                 within vortexRadius (200 px) in and blew it back out at random, the border pieces (obj_border_top etc.
//                 are polygon_parent children) included; here it swallows every destructible hazard within its radius and
//                 tears a breach in any border side it reaches, which shots then fly out through for the rest of the round.
//   Electricity   draw_electricity.gml: a jittering bolt between two points, drawn with random offsets every step. The
//                 original never called it; here it is an arc between two pylons that stops any shot touching it.
//   Laser         check_source_laser.gml: a beam whose source walks toward its end (dX, dY a step) until within 5 px. Also
//                 never called in the original; here a laser gate between an emitter and a receiver that switches on and
//                 off every turn (its beam sweeping out from the emitter as it comes on) and stops a shot while it is on.
//   Foliage       create_random_foliage.gml: tufts of grass at random spots inside the border (randomize_level.gml had the
//                 32-tuft loop commented out, so they were decoration); here a few tufts each round that stop a shot and
//                 are cut down by it.
//   Redirector    the redirector item (shoot_redirector.gml): flies like a shell and becomes a rail redirector where it
//                 stops, sending whatever touches it on along the heading it flew in (the original span until paused).
// Plain C#: the server runs one HazardField; the network layer mirrors each hazard and every peer rebuilds the level a
// shot sees from them (Build), so the preview, the AI and the server's solve all bounce off the same thing.
using System;
using System.Collections.Generic;

namespace Ricochet.Core.Level
{
    using Ricochet.Core.Aim;
    using Ricochet.Core.Arsenal;
    public enum HazardKind
    {
        /// <summary>A player's hologram wall (the shield wall item).</summary>
        ShieldWall = 0,

        /// <summary>An electric arc between two pylons; never goes.</summary>
        Electricity = 1,

        /// <summary>A laser gate that is on every other turn.</summary>
        Laser = 2,

        /// <summary>A tuft of foliage; a shot cuts it down.</summary>
        Foliage = 3,

        /// <summary>A gap a vortex tore in the border.</summary>
        Breach = 4,

        /// <summary>A rail redirector a player placed (the redirector item).</summary>
        Redirector = 5,

        /// <summary>
        /// A level shape a shrink ray struck (obj_polygon_parent's shrinking, U.RICO-T90): it shrinks away and is gone from the
        /// level for every later shot. <see cref="Hazard.Owner"/> is the shape (RoundLayout.Shapes), <see cref="Hazard.A"/>
        /// the range of the level's walls it made (start, count).
        /// </summary>
        Shrunk = 6,
    }

    /// <summary>How a hazard left the level (U.RICO-T48: a shield wall's glass flies on every peer unless it was cleared).</summary>
    public enum HazardEnd
    {
        /// <summary>Still on the level.</summary>
        None = 0,

        /// <summary>A shot shattered or cut it down (obj_shieldwall's collision events: sfx_shatter, ef_ring, ef_firework, spawn_glass).</summary>
        Struck = 1,

        /// <summary>A vortex's implosion swallowed it.</summary>
        Swallowed = 2,

        /// <summary>Its time ran out (obj_shieldwall's decayDelay: ef_ring, ef_firework, spawn_glass, no sound).</summary>
        Decayed = 3,

        /// <summary>Swept away with the round or the level (clear_objects): nothing is drawn.</summary>
        Cleared = 4,
    }

    public sealed class HazardRules
    {
        public const int KindCount = 7;

        /// <summary>How far in front of the player a shield wall stands (spawn_shieldwall's Len = 60 px).</summary>
        public double ShieldWallOffset { get; set; }

        /// <summary>A shield wall's length (spr_shieldwall is 128 px tall).</summary>
        public double ShieldWallLength { get; set; }

        /// <summary>The shortest a shield wall lasts before it decays, in seconds (decayDelay's 20 * normalFPS).</summary>
        public double ShieldWallMinLife { get; set; }

        /// <summary>The longest a shield wall lasts (120 * normalFPS).</summary>
        public double ShieldWallMaxLife { get; set; }

        /// <summary>A placed redirector's length.</summary>
        public double RedirectorLength { get; set; }

        /// <summary>Foliage tufts laid each round.</summary>
        public int FoliageCount { get; set; }

        /// <summary>A tuft's radius (spr_sprite_grass is 24 px across).</summary>
        public double FoliageRadius { get; set; }

        /// <summary>How far a tuft keeps from a seat's spot, a wall, a redirector and the border.</summary>
        public double FoliageClearance { get; set; }

        /// <summary>Whether laser gates switch on and off every turn (off: always on).</summary>
        public bool LasersAlternate { get; set; }

        /// <summary>pause_redirector off (U.RICO-T104): a placed redirector keeps turning; on, it turns until its owner fires or teleports (redirector_stop).</summary>
        public bool RedirectorsSpin { get; set; }

        /// <summary>Degrees a standing redirector turns every step (obj_redirector's spin = 1, through tick).</summary>
        public double RedirectorSpinDegrees { get; set; } = 1;

        /// <summary>The electric arcs every round starts with (pairs of pylons), for <see cref="Level.Default"/>.</summary>
        public List<Vec2[]> Arcs { get; } = new List<Vec2[]>();

        /// <summary>The laser gates every round starts with (emitter, receiver), for <see cref="Level.Default"/>.</summary>
        public List<Vec2[]> Gates { get; } = new List<Vec2[]>();

        /// <summary>The stock rules, from hazards.json (UR-T35): a new set starts as a copy of them.</summary>
        public HazardRules()
        {
            CopyFrom(Ricochet.Core.Data.GameData.Current.Hazards);
        }

        internal HazardRules(bool blank)
        {
        }

        internal void CopyFrom(HazardRules other)
        {
            ShieldWallOffset = other.ShieldWallOffset;
            ShieldWallLength = other.ShieldWallLength;
            ShieldWallMinLife = other.ShieldWallMinLife;
            ShieldWallMaxLife = other.ShieldWallMaxLife;
            RedirectorLength = other.RedirectorLength;
            FoliageCount = other.FoliageCount;
            FoliageRadius = other.FoliageRadius;
            FoliageClearance = other.FoliageClearance;
            LasersAlternate = other.LasersAlternate;
            RedirectorsSpin = other.RedirectorsSpin;
            RedirectorSpinDegrees = other.RedirectorSpinDegrees;
            Arcs.Clear();
            Gates.Clear();
            foreach (Vec2[] arc in other.Arcs)
            {
                Arcs.Add((Vec2[])arc.Clone());
            }

            foreach (Vec2[] gate in other.Gates)
            {
                Gates.Add((Vec2[])gate.Clone());
            }
        }

        public static HazardRules Default => new HazardRules();
    }

    /// <summary>One hazard.</summary>
    public sealed class Hazard
    {
        /// <summary>A hazard as the server made it, or as a peer read it back off the wire.</summary>
        public Hazard(int id, HazardKind kind, Vec2 a, Vec2 b, double radius = 0, int owner = -1, double heading = 0, bool active = true, double alpha = 1)
        {
            Id = id;
            Kind = kind;
            A = a;
            B = b;
            Radius = radius;
            Owner = owner;
            Heading = heading;
            Active = active;
            Alpha = alpha;
            Fading = alpha < 1; // a redirector's fade shows in its alpha, which is what peers are sent
            LifeLeft = double.PositiveInfinity;
        }

        /// <summary>1, 2, 3 ... within a field: the key it goes on the wire under.</summary>
        public int Id { get; }

        public HazardKind Kind { get; }

        /// <summary>One end: a pylon, a laser's emitter, a shield wall's or breach's end; a tuft's centre.</summary>
        public Vec2 A { get; internal set; }

        /// <summary>The other end (a tuft's centre again).</summary>
        public Vec2 B { get; internal set; }

        /// <summary>A tuft's radius; 0 for the rest.</summary>
        public double Radius { get; }

        /// <summary>The seat that placed it (a shield wall, a redirector), or -1.</summary>
        public int Owner { get; }

        /// <summary>A placed redirector's heading, in degrees.</summary>
        public double Heading { get; internal set; }

        /// <summary>A standing redirector's turn, in degrees a step (signed; 0 once stopped).</summary>
        public double Spin { get; internal set; }

        /// <summary>A standing redirector that has sent a shot on: it fades (image_alpha -= 0.01 a step) and is gone.</summary>
        public bool Fading { get; internal set; }

        /// <summary>image_alpha: 1, falling while <see cref="Fading"/>.</summary>
        public double Alpha { get; internal set; } = 1;

        /// <summary>A laser gate's beam is on (the rest are always active).</summary>
        public bool Active { get; internal set; }

        /// <summary>Seconds until a shield wall decays; infinite for the rest.</summary>
        public double LifeLeft { get; internal set; }

        /// <summary>How it left the level (<see cref="HazardEnd.None"/> while it is still there).</summary>
        public HazardEnd End { get; internal set; }

        public Vec2 Center => (A + B) * 0.5;

        /// <summary>A <see cref="HazardKind.Shrunk"/> shape's first wall in the level.</summary>
        public int WallStart => (int)Math.Round(A.X);

        /// <summary>How many walls a <see cref="HazardKind.Shrunk"/> shape made.</summary>
        public int WallCount => (int)Math.Round(A.Y);

        /// <summary>True for what a shot or a vortex can destroy: a shield wall, a tuft, a placed redirector.</summary>
        public bool Destructible => Kind == HazardKind.ShieldWall || Kind == HazardKind.Foliage || Kind == HazardKind.Redirector;

        /// <summary>
        /// True when this hazard stops <paramref name="shooter"/>'s shot: a shield wall unless it is theirs, an arc, a
        /// laser that is on, a tuft.
        /// </summary>
        public bool Stops(int shooter)
        {
            switch (Kind)
            {
                case HazardKind.ShieldWall:
                    return Owner < 0 || Owner != shooter;
                case HazardKind.Electricity:
                case HazardKind.Foliage:
                    return true;
                case HazardKind.Laser:
                    return Active;
                default:
                    return false;
            }
        }
    }

    public sealed class HazardField
    {
        private readonly List<Hazard> _hazards = new List<Hazard>();
        private readonly Random _dice;
        private int _nextId;

        public HazardField(Level level, HazardRules rules = null, Random dice = null)
        {
            Level = level ?? throw new ArgumentNullException(nameof(level));
            Rules = rules ?? HazardRules.Default;
            _dice = dice ?? new Random();
        }

        /// <summary>The level without its hazards.</summary>
        public Level Level { get; private set; }

        /// <summary>
        /// Lays future hazards out on <paramref name="level"/> instead (a match on another map, UR-T9); the hazards already
        /// down stay until <see cref="Clear"/>.
        /// </summary>
        public void UseLevel(Level level)
        {
            Level = level ?? throw new ArgumentNullException(nameof(level));
        }

        public HazardRules Rules { get; }

        /// <summary>Every hazard on the level, oldest first.</summary>
        public IReadOnlyList<Hazard> Hazards => _hazards;

        /// <summary>A hazard appeared.</summary>
        public event Action<Hazard> Added;

        /// <summary>A laser gate switched on or off.</summary>
        public event Action<Hazard> Changed;

        /// <summary>A hazard went: shattered, cut down, swallowed by a vortex, decayed or cleared away.</summary>
        public event Action<Hazard> Removed;

        public Hazard Find(int id)
        {
            foreach (Hazard hazard in _hazards)
            {
                if (hazard.Id == id)
                {
                    return hazard;
                }
            }

            return null;
        }

        /// <summary>
        /// The level <paramref name="shooter"/>'s shot sees with <paramref name="hazards"/> on it: every hazard that stops
        /// their shot as a barrier, every breach, every placed redirector. A peer builds it from the hazards it was sent.
        /// </summary>
        public static Level Build(Level level, IEnumerable<Hazard> hazards, int shooter)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            if (hazards == null)
            {
                return level;
            }

            var redirectors = new List<Redirector>();
            var barriers = new List<Barrier>();
            var breaches = new List<Breach>();
            var gone = new HashSet<int>();
            foreach (Hazard hazard in hazards)
            {
                if (hazard == null)
                {
                    continue;
                }

                switch (hazard.Kind)
                {
                    case HazardKind.Shrunk:
                        for (int i = 0; i < hazard.WallCount; i++)
                        {
                            gone.Add(hazard.WallStart + i);
                        }

                        break;
                    case HazardKind.Breach:
                        breaches.Add(new Breach(hazard.A, hazard.B));
                        break;
                    case HazardKind.Redirector:
                        // One shot only: a redirector that has sent one on no longer bends another (collision stays true).
                        if (!hazard.Fading && Vec2.Distance(hazard.A, hazard.B) >= Geometry.Epsilon)
                        {
                            redirectors.Add(new Redirector(hazard.A, hazard.B, hazard.Heading, hazard.Id));
                        }

                        break;
                    default:
                        if (!hazard.Stops(shooter))
                        {
                            break;
                        }

                        if (hazard.Radius > 0)
                        {
                            barriers.Add(new Barrier(hazard.Id, hazard.Kind, hazard.A, hazard.Radius));
                        }
                        else if (Vec2.Distance(hazard.A, hazard.B) >= Geometry.Epsilon)
                        {
                            barriers.Add(new Barrier(hazard.Id, hazard.Kind, hazard.A, hazard.B));
                        }

                        break;
                }
            }

            level = level.WithoutWalls(gone);
            return redirectors.Count + barriers.Count + breaches.Count == 0 ? level : level.With(redirectors, barriers, breaches);
        }

        /// <summary>The level <paramref name="shooter"/>'s shot sees now.</summary>
        public Level LevelFor(int shooter) => Build(Level, _hazards, shooter);

        /// <summary>
        /// spawn_shieldwall.gml: a wall <see cref="HazardRules.ShieldWallOffset"/> in front of <paramref name="shooter"/>
        /// along <paramref name="heading"/>, across it, owned by <paramref name="owner"/>, lasting a random 20-120 s.
        /// </summary>
        public Hazard AddShieldWall(int owner, Vec2 shooter, double heading)
        {
            Vec2 along = Vec2.FromAngle(heading);
            Vec2 center = shooter + along * Rules.ShieldWallOffset;
            Vec2 across = new Vec2(-along.Y, along.X) * (Rules.ShieldWallLength / 2);
            var wall = new Hazard(++_nextId, HazardKind.ShieldWall, center - across, center + across, owner: owner)
            {
                LifeLeft = Between(Rules.ShieldWallMinLife, Rules.ShieldWallMaxLife),
            };
            return Add(wall);
        }

        /// <summary>The redirector item: a redirector at <paramref name="spot"/>, across <paramref name="heading"/>, sending shots along it.</summary>
        public Hazard AddRedirector(int owner, Vec2 spot, double heading, bool reversed = false)
        {
            Vec2 along = Vec2.FromAngle(heading);
            Vec2 across = new Vec2(-along.Y, along.X) * (Rules.RedirectorLength / 2);
            var placed = new Hazard(++_nextId, HazardKind.Redirector, spot - across, spot + across, owner: owner, heading: Angles.Normalize(heading))
            {
                // spin = 1, negated by each bounce in flight (redirector_step); an odd number of bounces leaves it reversed.
                Spin = reversed ? -Rules.RedirectorSpinDegrees : Rules.RedirectorSpinDegrees,
            };
            return Add(placed);
        }

        /// <summary>
        /// redirector_stop (fire_weapon, use_item's teleporter): with pause_redirector on, <paramref name="owner"/>'s standing
        /// redirector stops turning; with it off, it keeps turning.
        /// </summary>
        public void PauseRedirectors(int owner)
        {
            if (Rules.RedirectorsSpin)
            {
                return;
            }

            foreach (Hazard hazard in _hazards)
            {
                if (hazard.Kind == HazardKind.Redirector && hazard.Owner == owner)
                {
                    hazard.Spin = 0;
                }
            }
        }

        /// <summary>A redirector sent a shot on (obj_redirector's collision events set fade = true): it fades away.</summary>
        public void RedirectorSpent(int id)
        {
            Hazard hazard = Find(id);
            if (hazard != null && hazard.Kind == HazardKind.Redirector)
            {
                hazard.Fading = true;
            }
        }

        /// <summary>
        /// obj_shrinkray striking shape <paramref name="shape"/> (U.RICO-T90), whose walls are the level's
        /// <paramref name="wallCount"/> from <paramref name="wallStart"/>: it shrinks away and is gone from the level. Null when
        /// it is already going.
        /// </summary>
        public Hazard AddShrunkShape(int shape, int wallStart, int wallCount)
        {
            foreach (Hazard hazard in _hazards)
            {
                if (hazard.Kind == HazardKind.Shrunk && hazard.Owner == shape)
                {
                    return null;
                }
            }

            return Add(new Hazard(++_nextId, HazardKind.Shrunk, new Vec2(wallStart, wallCount), new Vec2(wallStart, wallCount), owner: shape));
        }

        /// <summary>An electric arc between pylons at <paramref name="a"/> and <paramref name="b"/>.</summary>
        public Hazard AddArc(Vec2 a, Vec2 b) => Add(new Hazard(++_nextId, HazardKind.Electricity, a, b));

        /// <summary>A laser gate from an emitter at <paramref name="a"/> to a receiver at <paramref name="b"/>, on to begin with.</summary>
        public Hazard AddGate(Vec2 a, Vec2 b) => Add(new Hazard(++_nextId, HazardKind.Laser, a, b));

        /// <summary>A tuft of foliage at <paramref name="center"/>.</summary>
        public Hazard AddFoliage(Vec2 center) => Add(new Hazard(++_nextId, HazardKind.Foliage, center, center, Rules.FoliageRadius));

        /// <summary>A fixed hazard end pulled inside the level's (inset) playfield (U.RICO-T126).</summary>
        private Vec2 InPlay(Vec2 end) => new Playfield(Level.MinX, Level.MinY, Level.MaxX, Level.MaxY).Clamp(end, 0.3);

        /// <summary>A breach in the border from <paramref name="a"/> to <paramref name="b"/> (both on one side).</summary>
        public Hazard AddBreach(Vec2 a, Vec2 b) => Add(new Hazard(++_nextId, HazardKind.Breach, a, b));

        /// <summary>
        /// randomize_level.gml's hazards for a new round: the rules' electric arcs and laser gates, and
        /// <see cref="HazardRules.FoliageCount"/> tufts at random spots (create_random_foliage.gml) clear of
        /// <paramref name="keepClear"/> (where the players stand), the walls, the redirectors and the border. A spot that
        /// cannot be found in a few tries is skipped.
        /// </summary>
        public void LayOut(IEnumerable<Vec2> keepClear)
        {
            foreach (Vec2[] arc in Rules.Arcs)
            {
                AddArc(InPlay(arc[0]), InPlay(arc[1]));
            }

            foreach (Vec2[] gate in Rules.Gates)
            {
                AddGate(InPlay(gate[0]), InPlay(gate[1]));
            }

            var avoid = new List<Vec2>(keepClear ?? Array.Empty<Vec2>());
            double margin = Rules.FoliageClearance;
            for (int i = 0; i < Rules.FoliageCount; i++)
            {
                for (int attempt = 0; attempt < 30; attempt++)
                {
                    var spot = new Vec2(Between(Level.MinX + margin, Level.MaxX - margin), Between(Level.MinY + margin, Level.MaxY - margin));
                    if (Clear(spot, avoid, margin))
                    {
                        AddFoliage(spot);
                        avoid.Add(spot);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// A shot from <paramref name="shooter"/> ran into hazard <paramref name="id"/>: a shield wall (not theirs) shatters
        /// and a tuft is cut down. Returns the hazard destroyed, or null (an arc or a laser stands).
        /// </summary>
        public Hazard Struck(int id, int shooter)
        {
            Hazard hazard = Find(id);
            if (hazard == null || !hazard.Stops(shooter) || (hazard.Kind != HazardKind.ShieldWall && hazard.Kind != HazardKind.Foliage))
            {
                return null;
            }

            Remove(hazard, HazardEnd.Struck);
            return hazard;
        }

        /// <summary>
        /// vortex_detonate.gml's implosion at <paramref name="center"/>: every destructible hazard within
        /// <paramref name="radius"/> is swallowed, and every border side the circle reaches is breached along the chord
        /// it cuts. Returns the hazards swallowed and the breaches torn, in that order.
        /// </summary>
        public List<Hazard> Implode(Vec2 center, double radius)
        {
            var changed = new List<Hazard>();
            if (!(radius > 0))
            {
                return changed;
            }

            for (int i = _hazards.Count - 1; i >= 0; i--)
            {
                Hazard hazard = _hazards[i];
                if (hazard.Destructible && Reaches(hazard, center, radius))
                {
                    changed.Insert(0, hazard);
                    Remove(hazard, HazardEnd.Swallowed);
                }
            }

            BreachSide(center, radius, center.X - Level.MinX, true, Level.MinX, Level.MinY, Level.MaxY, changed);
            BreachSide(center, radius, Level.MaxX - center.X, true, Level.MaxX, Level.MinY, Level.MaxY, changed);
            BreachSide(center, radius, center.Y - Level.MinY, false, Level.MinY, Level.MinX, Level.MaxX, changed);
            BreachSide(center, radius, Level.MaxY - center.Y, false, Level.MaxY, Level.MinX, Level.MaxX, changed);
            return changed;
        }

        /// <summary>A new turn: every laser gate switches (when <see cref="HazardRules.LasersAlternate"/>) and every standing redirector is destroyed.</summary>
        public void NextTurn()
        {
            foreach (Hazard hazard in _hazards)
            {
                if (hazard.Kind == HazardKind.Laser && Rules.LasersAlternate)
                {
                    hazard.Active = !hazard.Active;
                    Changed?.Invoke(hazard);
                }
            }

            // next_player: with(obj_redirector) instance_destroy().
            for (int i = _hazards.Count - 1; i >= 0; i--)
            {
                if (_hazards[i].Kind == HazardKind.Redirector)
                {
                    Remove(_hazards[i], HazardEnd.Cleared);
                }
            }
        }

        /// <summary>Runs every shield wall's decay (obj_shieldwall's decayTime); a wall whose time is up goes.</summary>
        public void Tick(double seconds)
        {
            if (seconds <= 0 || double.IsNaN(seconds))
            {
                return;
            }

            double steps = seconds * StepsPerSecond;
            for (int i = _hazards.Count - 1; i >= 0; i--)
            {
                Hazard hazard = _hazards[i];
                if (hazard.Kind == HazardKind.Redirector)
                {
                    TickRedirector(hazard, steps);
                    continue;
                }

                if (double.IsInfinity(hazard.LifeLeft))
                {
                    continue;
                }

                hazard.LifeLeft -= seconds;
                if (hazard.LifeLeft <= 0)
                {
                    Remove(hazard, HazardEnd.Decayed);
                }
            }
        }

        /// <summary>The original's room speed.</summary>
        public const double StepsPerSecond = 60;

        // redirector_step, spinning: image_angle += tick(spin) a step; once it has sent a shot on, image_alpha -= 0.01 a step
        // and it is destroyed when none is left.
        private void TickRedirector(Hazard hazard, double steps)
        {
            if (hazard.Spin != 0)
            {
                double degrees = hazard.Spin * steps;
                double radians = degrees * Math.PI / 180;
                double cos = Math.Cos(radians);
                double sin = Math.Sin(radians);
                Vec2 center = hazard.Center;
                Vec2 half = (hazard.B - hazard.A) * 0.5;
                var turned = new Vec2(half.X * cos - half.Y * sin, half.X * sin + half.Y * cos);
                hazard.A = center - turned;
                hazard.B = center + turned;
                hazard.Heading = Angles.Normalize(hazard.Heading + degrees);
                Changed?.Invoke(hazard);
            }

            if (hazard.Fading)
            {
                hazard.Alpha -= 0.01 * steps;
                Changed?.Invoke(hazard);
                if (hazard.Alpha <= 0)
                {
                    Remove(hazard, HazardEnd.Decayed);
                }
            }
        }

        /// <summary>Every hazard goes (a new round, or the level is laid again).</summary>
        public void Clear()
        {
            for (int i = _hazards.Count - 1; i >= 0; i--)
            {
                Remove(_hazards[i], HazardEnd.Cleared);
            }
        }

        private Hazard Add(Hazard hazard)
        {
            _hazards.Add(hazard);
            Added?.Invoke(hazard);
            return hazard;
        }

        private void Remove(Hazard hazard, HazardEnd end)
        {
            if (_hazards.Remove(hazard))
            {
                hazard.End = end;
                Removed?.Invoke(hazard);
            }
        }

        // The circle reaches a side gap = distance from the centre to that side is less than the radius: the chord it cuts.
        private void BreachSide(Vec2 center, double radius, double gap, bool vertical, double at, double min, double max, List<Hazard> changed)
        {
            if (gap < 0 || gap >= radius)
            {
                return;
            }

            double half = Math.Sqrt(radius * radius - gap * gap);
            double mid = vertical ? center.Y : center.X;
            double from = Math.Max(min, mid - half);
            double to = Math.Min(max, mid + half);
            if (to - from < Geometry.Epsilon)
            {
                return;
            }

            changed.Add(vertical ? AddBreach(new Vec2(at, from), new Vec2(at, to)) : AddBreach(new Vec2(from, at), new Vec2(to, at)));
        }

        private static bool Reaches(Hazard hazard, Vec2 center, double radius)
        {
            double gap = hazard.Radius > 0 || Vec2.Distance(hazard.A, hazard.B) < Geometry.Epsilon
                ? Vec2.Distance(hazard.A, center)
                : Geometry.SegmentDistance(hazard.A, hazard.B, center, out _);
            return gap <= radius + hazard.Radius;
        }

        private bool Clear(Vec2 spot, List<Vec2> avoid, double margin)
        {
            foreach (Vec2 point in avoid)
            {
                if (Vec2.Distance(point, spot) < margin)
                {
                    return false;
                }
            }

            foreach (Wall wall in Level.Walls)
            {
                if (Geometry.SegmentDistance(wall.A, wall.B, spot, out _) < margin)
                {
                    return false;
                }
            }

            foreach (Redirector redirector in Level.Redirectors)
            {
                if (Geometry.SegmentDistance(redirector.A, redirector.B, spot, out _) < margin)
                {
                    return false;
                }
            }

            foreach (Hazard hazard in _hazards)
            {
                double gap = hazard.Radius > 0 ? Vec2.Distance(hazard.A, spot) - hazard.Radius : Geometry.SegmentDistance(hazard.A, hazard.B, spot, out _);
                if (gap < margin)
                {
                    return false;
                }
            }

            return true;
        }

        private double Between(double min, double max) => max <= min ? min : min + _dice.NextDouble() * (max - min);
    }
}
