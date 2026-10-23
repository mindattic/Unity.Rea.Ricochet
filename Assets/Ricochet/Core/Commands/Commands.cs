// The commands a player (or the AI, or a test) can give the game (UR-T34). The 2013 original's objects reached into each
// other's globals (obj_player's keyboard events called fire_weapon.gml, cycle_weapon_*.gml, use_item.gml, next_player.gml
// and randomize_level.gml straight on the match; design reference only, UR-LAW-2). Here every input is a plain, engine-free
// command carrying the id of the player who gave it, and one authority (GameAuthority) checks it against the rules,
// applies it and answers with the ordered list of events it caused (GameEvents.cs). Nothing in the runtime changes the
// match, the loadouts, the crates or the hazards any other way; there is no "current player", the command says who.
// A command is a request, never a fact: the authority clamps garbage (a NaN heading, a power of 5) and ignores what the
// player may not do now (out of turn, a shot already in the air), answering with no events at all.
namespace Ricochet.Core.Commands
{
    /// <summary>Something a player asks the game to do.</summary>
    public abstract class Command
    {
        protected Command(int playerId)
        {
            PlayerId = playerId;
        }

        /// <summary>The player who gave it (<see cref="PlayerState.Id"/>).</summary>
        public int PlayerId { get; }
    }

    /// <summary>Turn the barrel (at any time, so everyone sees where it points). A NaN heading keeps the last good one.</summary>
    public sealed class AimCommand : Command
    {
        public AimCommand(int playerId, double heading) : base(playerId)
        {
            Heading = heading;
        }

        public double Heading { get; }
    }

    /// <summary>Set the shot's power (clamped to 0..1; NaN is 0).</summary>
    public sealed class SetPowerCommand : Command
    {
        public SetPowerCommand(int playerId, double power) : base(playerId)
        {
            Power = power;
        }

        public double Power { get; }
    }

    /// <summary>Fire the selected weapon at this aim (fire_weapon.gml): only on your turn, before driving, with a weapon you have.</summary>
    public sealed class FireCommand : Command
    {
        public FireCommand(int playerId, double heading, double power) : base(playerId)
        {
            Heading = heading;
            Power = power;
        }

        public double Heading { get; }

        public double Power { get; }
    }

    /// <summary>Select the next weapon (+1, cycle_weapon_right) or the one before (-1, cycle_weapon_left).</summary>
    public sealed class CycleWeaponCommand : Command
    {
        public CycleWeaponCommand(int playerId, int direction) : base(playerId)
        {
            Direction = direction;
        }

        public int Direction { get; }
    }

    /// <summary>Select the next item (+1) or the one before (-1).</summary>
    public sealed class CycleItemCommand : Command
    {
        public CycleItemCommand(int playerId, int direction) : base(playerId)
        {
            Direction = direction;
        }

        public int Direction { get; }
    }

    /// <summary>The grenade fuse keys: negative shortens, +1 lengthens, +2 or more sets the longest.</summary>
    public sealed class FuseCommand : Command
    {
        public FuseCommand(int playerId, int change) : base(playerId)
        {
            Change = change;
        }

        public int Change { get; }
    }

    /// <summary>Use the selected item at this aim (use_item.gml): a teleporter and a shrink ray fly along it.</summary>
    public sealed class UseItemCommand : Command
    {
        public UseItemCommand(int playerId, double heading, double power) : base(playerId)
        {
            Heading = heading;
            Power = power;
        }

        public double Heading { get; }

        public double Power { get; }
    }

    /// <summary>Set off this player's own vortex while it is still in the air (vortex_detonate.gml).</summary>
    public sealed class DetonateCommand : Command
    {
        public DetonateCommand(int playerId) : base(playerId)
        {
        }
    }

    /// <summary>
    /// The player holds a drive key (UR-T24): +1 forward (move_forward.gml), -1 back, 0 stop. The tank keeps moving only
    /// <see cref="GameAuthority.DriveHold"/> seconds past the last one, so a dropped stop cannot leave it running.
    /// Driving forfeits the turn's shot.
    /// </summary>
    public sealed class DriveCommand : Command
    {
        public DriveCommand(int playerId, int direction) : base(playerId)
        {
            Direction = direction;
        }

        public int Direction { get; }
    }

    /// <summary>Give up the rest of this turn, before firing (next_player.gml on F or the End Turn button).</summary>
    public sealed class EndTurnCommand : Command
    {
        public EndTurnCommand(int playerId) : base(playerId)
        {
        }
    }

    /// <summary>Practice only (UR-T23): roll another random level (randomize_level.gml on R, PageUp or PageDown).</summary>
    public sealed class RerollCommand : Command
    {
        public RerollCommand(int playerId) : base(playerId)
        {
        }
    }

    /// <summary>A vote on an open question (see <see cref="GameAuthority.OpenVote"/>): yes or no, once a player, the last one counts.</summary>
    public sealed class VoteCommand : Command
    {
        public VoteCommand(int playerId, string kind, bool yes) : base(playerId)
        {
            Kind = kind ?? string.Empty;
            Yes = yes;
        }

        public string Kind { get; }

        public bool Yes { get; }
    }
}
