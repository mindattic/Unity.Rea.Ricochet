// What the authority knows about one player (UR-T34): where their tank stands, the aim, the loadout they carry, the shot
// they last fired and the volley still in the air. Only GameAuthority changes it; the runtime's Player copies it out to the
// values every peer reads and never writes it back. Numbers a client sees as floats are held at float precision here too, so
// what the server solves from is exactly what it sent.
using System;

namespace Ricochet.Core.Commands
{
    using Ricochet.Core.Aim;
    using Ricochet.Core.Arsenal;
    using Ricochet.Core.Match;

    public sealed class PlayerState
    {
        internal PlayerState(int id, bool isAi, ArsenalRules arsenal)
        {
            Id = id;
            IsAi = isAi;
            Loadout = new Loadout(arsenal);
        }

        /// <summary>What commands name this player by: unique for the life of the authority, never reused.</summary>
        public int Id { get; }

        public bool IsAi { get; }

        /// <summary>The seat the player sits in (the match's turn order and spot; a practice seat before a match), or -1 for none.</summary>
        public int Seat { get; internal set; } = -1;

        public Vec2 Position { get; internal set; }

        /// <summary>The barrel's heading in degrees, [0, 360).</summary>
        public double Heading { get; internal set; } = 90;


        public Loadout Loadout { get; }

        /// <summary>Extra Bounce items used this turn (the next shot carries them).</summary>
        public int ExtraBounces { get; internal set; }

        /// <summary>1 at full size, less after a shrink ray (for the rest of the round).</summary>
        public double Scale { get; internal set; } = 1;

        /// <summary>The hit radius: a seat's full radius, less after a shrink ray.</summary>
        public double Radius => Seating.PlayerRadius * Math.Max(0.05, Math.Min(1, Scale));

        /// <summary>Shots fired so far (the last one's number).</summary>
        public int Shots { get; internal set; }

        public double ShotHeading { get; internal set; }


        public Weapon ShotWeapon { get; internal set; }

        public double ShotFuse { get; internal set; } = 2;

        public int ShotExtraBounces { get; internal set; }

        /// <summary>How far from the muzzle the last shot (a vortex) was set off; 0 when it was not.</summary>
        public double ShotDetonation { get; internal set; }

        /// <summary>The volley the last shot is flying (null before the first).</summary>
        public VolleyFlight Flight { get; internal set; }

        /// <summary>The last volley fired, or cut short by a detonation.</summary>
        public Volley LastVolley { get; internal set; }

        /// <summary>True while a volley of this player's is in the air.</summary>
        public bool InFlight => Flight != null && !Flight.Landed;

        /// <summary>A redirector fired this turn (hasFiredRedirector): the item key stops it rather than firing another (U.RICO-T80).</summary>
        public bool RedirectorFired { get; internal set; }

        /// <summary>Aim assists used so far, and the aim the last one turned this player onto.</summary>
        public int Assists { get; internal set; }

        public double AssistHeading { get; internal set; }


        /// <summary>Crates collected so far, and the last one (kind * 1000 + what it gave).</summary>
        public int Pickups { get; internal set; }

        public int LastPickup { get; internal set; }

        /// <summary>+1 or -1 while a drive key is held (UR-T24), else 0.</summary>
        public int DriveDirection { get; internal set; }

        /// <summary>Seconds of driving left before the tank stops on its own.</summary>
        public double DriveLeft { get; internal set; }

        internal void Restore(PlayerSnapshot from)
        {
            Seat = from.Seat;
            Position = from.Position;
            Heading = from.Heading;
            if (from.Weapons != 0)
            {
                Loadout.UnpackFrom(from.Weapons, from.Items);
            }

            ExtraBounces = from.ExtraBounces;
            Scale = from.Scale;
            Shots = from.Shots;
            ShotHeading = from.ShotHeading;
            ShotWeapon = (Weapon)Math.Max(0, Math.Min(ArsenalRules.Kinds - 1, from.ShotWeapon));
            ShotFuse = from.ShotFuse;
            ShotExtraBounces = from.ShotExtraBounces;
            ShotDetonation = from.ShotDetonation;
            Assists = from.Assists;
            AssistHeading = from.AssistHeading;
            Pickups = from.Pickups;
            LastPickup = from.LastPickup;
        }

        public override string ToString() => "player " + Id + " (seat " + Seat + (IsAi ? ", AI" : string.Empty) + ")";
    }

    /// <summary>
    /// A player's state as plain values, to hand a player to an authority (a player object that spawned before the game's
    /// authority existed, or came back after a reconnect): <see cref="Of"/> reads a state, and the runtime can fill one in
    /// from what it has synced. A shot in the air is not carried over.
    /// </summary>
    public sealed class PlayerSnapshot
    {
        public int Seat = -1;
        public Vec2 Position;
        public double Heading = 90;

        /// <summary>The loadout packed by <see cref="Loadout.PackWeapons"/> and <see cref="Loadout.PackItems"/>; 0 weapons is a fresh one.</summary>
        public int Weapons;
        public int Items;
        public int ExtraBounces;
        public double Scale = 1;
        public int Shots;
        public double ShotHeading;
        public int ShotWeapon;
        public double ShotFuse = 2;
        public int ShotExtraBounces;
        public double ShotDetonation;
        public int Assists;
        public double AssistHeading;
        public int Pickups;
        public int LastPickup;

        public static PlayerSnapshot Of(PlayerState state)
        {
            return new PlayerSnapshot
            {
                Seat = state.Seat,
                Position = state.Position,
                Heading = state.Heading,
                Weapons = state.Loadout.PackWeapons(),
                Items = state.Loadout.PackItems(),
                ExtraBounces = state.ExtraBounces,
                Scale = state.Scale,
                Shots = state.Shots,
                ShotHeading = state.ShotHeading,
                ShotWeapon = (int)state.ShotWeapon,
                ShotFuse = state.ShotFuse,
                ShotExtraBounces = state.ShotExtraBounces,
                ShotDetonation = state.ShotDetonation,
                Assists = state.Assists,
                AssistHeading = state.AssistHeading,
                Pickups = state.Pickups,
                LastPickup = state.LastPickup,
            };
        }
    }

    /// <summary>The ids a shot's targets carry: players are "seat:3", the practice dummies "dummy:0".</summary>
    public static class TargetIds
    {
        public const string SeatPrefix = "seat:";
        public const string DummyPrefix = "dummy:";

        /// <summary>The target id a seated player is hit as ("seat:3").</summary>
        public static string Seat(int seat) => SeatPrefix + seat;

        /// <summary>The seat a target id names, or -1 when it is not a player.</summary>
        public static int SeatOf(string targetId)
        {
            int seat;
            return targetId != null && targetId.StartsWith(SeatPrefix, StringComparison.Ordinal)
                && int.TryParse(targetId.Substring(SeatPrefix.Length), out seat) ? seat : -1;
        }

        public static bool IsDummy(string targetId) => targetId != null && targetId.StartsWith(DummyPrefix, StringComparison.Ordinal);
    }
}
