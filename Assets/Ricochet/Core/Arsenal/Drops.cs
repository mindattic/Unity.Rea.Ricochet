// Random drops: crates that fall onto the level during a match and give whoever's shot passes over one a random
// weapon, a random item or some health. Ports obj_drop_emitter (one timer per kind, each firing after a random
// 20-120 seconds and starting over), add_weapondrop/itemdrop/healthdrop/powerup.gml (a crate at a random x falls from
// above the top to a random height between two border widths of the edges), the crates' own step (fall, land, sit for
// a random 20-120 seconds, then burst), their collision events (a bullet, pellet, grenade or laser that touches one
// collects it for its owner) and add_random_weapon/item/health.gml (what a crate gives). The powerup crate
// (add_powerup.gml, the "random drops" option) gave a random weapon, as obj_powerup did. Two of the original's slips
// are not carried over: add_random_weapon's roll for the cannon gave a laser, and add_random_item's roll for aim assist
// gave a teleporter; here a roll lands on the kind it names, moving right past any not in play (as hasX did).
// Design reference only (UR-LAW-2). Plain C#: the server runs one DropField per match; the network layer mirrors it.
using System;
using System.Collections.Generic;

namespace Ricochet.Core.Arsenal
{
    using Ricochet.Core.Aim;
    using Ricochet.Core.Level;
    public enum DropKind
    {
        /// <summary>obj_weapondrop: a random weapon.</summary>
        Weapon = 0,

        /// <summary>obj_itemdrop: a random item.</summary>
        Item = 1,

        /// <summary>obj_healthdrop: some health back.</summary>
        Health = 2,

        /// <summary>obj_powerup: a random weapon (the "random drops" option).</summary>
        Powerup = 3,
    }

    /// <summary>How a crate left the level (U.RICO-T48: what every peer then draws there).</summary>
    public enum DropEnd
    {
        /// <summary>Still on the level.</summary>
        None = 0,

        /// <summary>A shot passed over it: its collision event's ef_ring where it stood.</summary>
        Collected = 1,

        /// <summary>It lay too long and burst (decayDelay): obj_powerup_explosion.</summary>
        Burst = 2,

        /// <summary>Swept away with the round or the match (clear_objects): nothing is drawn.</summary>
        Cleared = 3,
    }

    public sealed class DropRules
    {
        public const int KindCount = 4;

        private readonly bool[] _enabled = { true, true, true, true };

        /// <summary>The shortest wait between two drops of a kind, in seconds (irandom_range(20 * normalFPS, ...)).</summary>
        public double MinDelay { get; set; } = 20;

        /// <summary>The longest wait between two drops of a kind (... 120 * normalFPS).</summary>
        public double MaxDelay { get; set; } = 120;

        /// <summary>The shortest time a landed crate stays before it bursts (decayDelay).</summary>
        public double MinLife { get; set; } = 20;

        /// <summary>The longest time a landed crate stays.</summary>
        public double MaxLife { get; set; } = 120;

        /// <summary>How fast a crate falls, units a second (tick(10) px a frame at 60 fps).</summary>
        public double FallSpeed { get; set; } = 10;

        /// <summary>How close a shot must pass to collect a crate (its sprite was about half a unit across).</summary>
        public double PickupRadius { get; set; } = 0.5;

        /// <summary>How far in from the border a crate lands (border * 2).</summary>
        public double Margin { get; set; } = 1.5;

        /// <summary>The most crates on the level at once (the original had no cap).</summary>
        public int MaxOnField { get; set; } = 8;

        public static DropRules Default => new DropRules();

        /// <summary>weapondrops / itemdrops / healthdrops / randomdrops: whether this kind falls at all.</summary>
        public bool Enabled(DropKind kind) => _enabled[(int)kind];

        public void SetEnabled(DropKind kind, bool enabled) => _enabled[(int)kind] = enabled;
    }

    /// <summary>One crate.</summary>
    public sealed class Drop
    {
        internal Drop(int id, DropKind kind, Vec2 spot, double startY, double life)
        {
            Id = id;
            Kind = kind;
            Spot = spot;
            StartY = startY;
            Y = startY;
            LifeLeft = life;
        }

        /// <summary>1, 2, 3 ... within a field: the key it goes on the wire under.</summary>
        public int Id { get; }

        public DropKind Kind { get; }

        /// <summary>Where it lands.</summary>
        public Vec2 Spot { get; }

        /// <summary>The height it falls from (above the level's top).</summary>
        public double StartY { get; }

        /// <summary>Its height now.</summary>
        public double Y { get; internal set; }

        public Vec2 Position => new Vec2(Spot.X, Y);

        /// <summary>True once it has reached its spot; only a landed crate can be collected.</summary>
        public bool Landed { get; internal set; }

        /// <summary>Seconds until a landed crate bursts.</summary>
        public double LifeLeft { get; internal set; }

        /// <summary>How it left the level (<see cref="DropEnd.None"/> while it is still there).</summary>
        public DropEnd End { get; internal set; }
    }

    public sealed class DropField
    {
        private readonly List<Drop> _drops = new List<Drop>();
        private readonly double[] _untilNext = new double[DropRules.KindCount];
        private readonly Random _dice;
        private int _nextId;

        public DropField(Level level, DropRules rules = null, Random dice = null)
        {
            Level = level ?? throw new ArgumentNullException(nameof(level));
            Rules = rules ?? DropRules.Default;
            _dice = dice ?? new Random();
            for (int i = 0; i < DropRules.KindCount; i++)
            {
                _untilNext[i] = NextDelay();
            }
        }

        public Level Level { get; }

        public DropRules Rules { get; }

        /// <summary>The crates falling or lying on the level, oldest first.</summary>
        public IReadOnlyList<Drop> Drops => _drops;

        /// <summary>A crate appeared above the level.</summary>
        public event Action<Drop> Spawned;

        /// <summary>A crate reached its spot.</summary>
        public event Action<Drop> Landed;

        /// <summary>A crate went: burst after lying too long, collected, or cleared away.</summary>
        public event Action<Drop> Removed;

        /// <summary>Seconds until the next crate of <paramref name="kind"/> (whether or not it is enabled).</summary>
        public double UntilNext(DropKind kind) => _untilNext[(int)kind];

        /// <summary>Runs obj_drop_emitter's timers and every crate's fall and decay.</summary>
        public void Tick(double seconds)
        {
            if (seconds <= 0 || double.IsNaN(seconds))
            {
                return;
            }

            // The crates already out move first, so a new one starts its fall from the top on the next tick.
            for (int i = _drops.Count - 1; i >= 0; i--)
            {
                Drop drop = _drops[i];
                if (!drop.Landed)
                {
                    drop.Y = Math.Max(drop.Spot.Y, drop.Y - Rules.FallSpeed * seconds);
                    if (drop.Y <= drop.Spot.Y)
                    {
                        drop.Landed = true;
                        Landed?.Invoke(drop);
                    }
                }
                else
                {
                    drop.LifeLeft -= seconds;
                    if (drop.LifeLeft <= 0)
                    {
                        RemoveAt(i, DropEnd.Burst);
                    }
                }
            }

            for (int i = 0; i < DropRules.KindCount; i++)
            {
                _untilNext[i] -= seconds;
                if (_untilNext[i] <= 0)
                {
                    _untilNext[i] = NextDelay();
                    if (Rules.Enabled((DropKind)i))
                    {
                        Spawn((DropKind)i);
                    }
                }
            }
        }

        /// <summary>
        /// add_weapondrop (and its siblings): a crate of <paramref name="kind"/> at a random spot, falling from above the
        /// top. Null when the level already holds <see cref="DropRules.MaxOnField"/>.
        /// </summary>
        public Drop Spawn(DropKind kind)
        {
            if (_drops.Count >= Rules.MaxOnField)
            {
                return null;
            }

            double margin = Math.Min(Rules.Margin, Math.Min(Level.MaxX - Level.MinX, Level.MaxY - Level.MinY) / 2);
            var spot = new Vec2(Between(Level.MinX + margin, Level.MaxX - margin), Between(Level.MinY + margin, Level.MaxY - margin));
            return Spawn(kind, spot);
        }

        /// <summary>A crate of <paramref name="kind"/> landing at <paramref name="spot"/>.</summary>
        public Drop Spawn(DropKind kind, Vec2 spot)
        {
            if (_drops.Count >= Rules.MaxOnField)
            {
                return null;
            }

            var drop = new Drop(++_nextId, kind, spot, Level.MaxY + 1, Between(Rules.MinLife, Rules.MaxLife));
            _drops.Add(drop);
            Spawned?.Invoke(drop);
            return drop;
        }

        /// <summary>
        /// The landed crates <paramref name="volley"/> passes over, nearest the muzzle first; each is taken off the
        /// level. The caller gives their contents to the shooter.
        /// </summary>
        public List<Drop> Collect(Volley volley)
        {
            var collected = new List<Drop>();
            if (volley == null)
            {
                return collected;
            }

            var distances = new List<double>();
            for (int i = _drops.Count - 1; i >= 0; i--)
            {
                Drop drop = _drops[i];
                double? distance = drop.Landed ? volley.PassDistance(drop.Spot, Rules.PickupRadius) : null;
                if (distance != null)
                {
                    int at = 0;
                    while (at < distances.Count && distances[at] <= distance.Value)
                    {
                        at++;
                    }

                    collected.Insert(at, drop);
                    distances.Insert(at, distance.Value);
                    RemoveAt(i, DropEnd.Collected);
                }
            }

            return collected;
        }

        /// <summary>Every crate goes (a new round, or the match ends).</summary>
        public void Clear()
        {
            for (int i = _drops.Count - 1; i >= 0; i--)
            {
                RemoveAt(i, DropEnd.Cleared);
            }
        }

        private void RemoveAt(int index, DropEnd end)
        {
            Drop drop = _drops[index];
            _drops.RemoveAt(index);
            drop.End = end;
            Removed?.Invoke(drop);
        }

        private double NextDelay() => Between(Rules.MinDelay, Rules.MaxDelay);

        private double Between(double min, double max) => max <= min ? min : min + _dice.NextDouble() * (max - min);
    }

    /// <summary>What a crate gives: add_random_weapon/item/health.gml.</summary>
    public static class Pickups
    {
        /// <summary>
        /// A random weapon a drop can give: a roll of the six, moved right past any not in play or carried unlimited from
        /// the start (the cannon, here); null when none can drop.
        /// </summary>
        public static Weapon? RandomWeapon(Random dice, ArsenalRules rules)
        {
            rules = rules ?? ArsenalRules.Default;
            int roll = (dice ?? new Random()).Next(ArsenalRules.Kinds);
            for (int i = 0; i < ArsenalRules.Kinds; i++)
            {
                var weapon = (Weapon)((roll + i) % ArsenalRules.Kinds);
                if (rules.InPlay(weapon) && rules.StartCount(weapon) < ArsenalRules.Unlimited)
                {
                    return weapon;
                }
            }

            return null;
        }

        /// <summary>A random item a drop can give, as <see cref="RandomWeapon"/>.</summary>
        public static Item? RandomItem(Random dice, ArsenalRules rules)
        {
            rules = rules ?? ArsenalRules.Default;
            int roll = (dice ?? new Random()).Next(ArsenalRules.Kinds);
            for (int i = 0; i < ArsenalRules.Kinds; i++)
            {
                var item = (Item)((roll + i) % ArsenalRules.Kinds);
                if (rules.InPlay(item) && rules.StartCount(item) < ArsenalRules.Unlimited)
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>
        /// add_random_health.gml: between half and all of a hit's base damage, plus a roll of a tenth of it either way;
        /// at least 1. (The caller caps health at the maximum.)
        /// </summary>
        public static int HealAmount(Random dice, int weaponDamage)
        {
            dice = dice ?? new Random();
            int damage = Math.Max(1, weaponDamage);
            int amount = dice.Next(damage / 2, damage + 1);
            int spread = damage / 10;
            amount += dice.Next(-spread, spread + 1);
            return Math.Max(1, amount);
        }
    }
}
