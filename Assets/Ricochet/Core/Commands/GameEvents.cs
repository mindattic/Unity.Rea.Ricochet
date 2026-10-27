// What happened (UR-T34): the facts GameAuthority answers a command with, in the order they happened. The runtime's
// presentation (the shot view, the HUD, the announcer, the audio, the crate and hazard objects) reads these and plays
// them; it never changes the game itself. Events are immutable and carry everything a listener needs, so none has to
// ask the authority "what was that?" afterwards.
namespace Ricochet.Core.Commands
{
    using Ricochet.Core.Aim;
    using Ricochet.Core.Arsenal;
    using Ricochet.Core.Net;

    /// <summary>Something that happened in the game.</summary>
    public abstract class GameEvent
    {
    }

    /// <summary>A player's barrel now points this way, at this power.</summary>
    public sealed class Aimed : GameEvent
    {
        public Aimed(int playerId, double heading, double power)
        {
            PlayerId = playerId;
            Heading = heading;
            Power = power;
        }

        public int PlayerId { get; }

        public double Heading { get; }

        public double Power { get; }
    }

    /// <summary>A player's weapon, item, fuse or counts changed (a cycle, a shot or item spent, a crate, a new round).</summary>
    public sealed class LoadoutChanged : GameEvent
    {
        public LoadoutChanged(int playerId)
        {
            PlayerId = playerId;
        }

        public int PlayerId { get; }
    }

    /// <summary>A player fired: the volley the authority solved (every peer draws this one) and the aim it was fired at.</summary>
    public sealed class ShotFired : GameEvent
    {
        public ShotFired(int playerId, int seat, int shot, Weapon weapon, double heading, double power, double fuse, int extraBounces, Volley volley)
        {
            PlayerId = playerId;
            Seat = seat;
            Shot = shot;
            Weapon = weapon;
            Heading = heading;
            Power = power;
            Fuse = fuse;
            ExtraBounces = extraBounces;
            Volley = volley;
        }

        public int PlayerId { get; }

        /// <summary>The shooter's seat, or -1.</summary>
        public int Seat { get; }

        /// <summary>The player's shot count with this one in it (1 for their first).</summary>
        public int Shot { get; }

        public Weapon Weapon { get; }

        public double Heading { get; }

        public double Power { get; }

        public double Fuse { get; }

        public int ExtraBounces { get; }

        public Volley Volley { get; }
    }

    /// <summary>A shot in the air passed another contact (a wall, a redirector, a hazard): <see cref="Bounces"/> so far.</summary>
    public sealed class Bounced : GameEvent
    {
        public Bounced(int playerId, int shot, int bounces, Vec2 point)
        {
            PlayerId = playerId;
            Shot = shot;
            Bounces = bounces;
            Point = point;
        }

        public int PlayerId { get; }

        public int Shot { get; }

        public int Bounces { get; }

        public Vec2 Point { get; }
    }

    /// <summary>A vortex was set off in the air <see cref="At"/> from the muzzle: the volley now cut short (it lands at once).</summary>
    public sealed class ShotCut : GameEvent
    {
        public ShotCut(int playerId, int shot, double at, Volley volley)
        {
            PlayerId = playerId;
            Shot = shot;
            At = at;
            Volley = volley;
        }

        public int PlayerId { get; }

        public int Shot { get; }

        public double At { get; }

        public Volley Volley { get; }
    }

    /// <summary>A shot struck a target: a player's seat, or -1 for a practice dummy (<see cref="TargetId"/> names it).</summary>
    public sealed class Hit : GameEvent
    {
        public Hit(int playerId, string targetId, int seat, int bounces)
        {
            PlayerId = playerId;
            TargetId = targetId;
            Seat = seat;
            Bounces = bounces;
        }

        /// <summary>The shooter.</summary>
        public int PlayerId { get; }

        public string TargetId { get; }

        /// <summary>The struck player's seat, or -1 for a dummy.</summary>
        public int Seat { get; }

        public int Bounces { get; }

        public bool IsDummy => Seat < 0;
    }

    /// <summary>A seat lost health.</summary>
    public sealed class Damaged : GameEvent
    {
        public Damaged(int seat, int damage, int bySeat)
        {
            Seat = seat;
            Damage = damage;
            BySeat = bySeat;
        }

        public int Seat { get; }

        public int Damage { get; }

        public int BySeat { get; }
    }

    /// <summary>A seat scored points for a hit on an opponent.</summary>
    public sealed class Scored : GameEvent
    {
        public Scored(int seat, int points)
        {
            Seat = seat;
            Points = points;
        }

        public int Seat { get; }

        public int Points { get; }
    }

    /// <summary>A seat's health reached zero: it is out of the round.</summary>
    public sealed class Eliminated : GameEvent
    {
        public Eliminated(int seat)
        {
            Seat = seat;
        }

        public int Seat { get; }
    }

    /// <summary>
    /// A player's volley has landed, with everything it did: in a match <see cref="Outcome"/> is the damage each seat took
    /// and who it knocked out (null outside a match); <see cref="KnockedOut"/> is the opponent it put out for the
    /// announcer's cheer (-1 for none), <see cref="StruckAPlayer"/> whether it hit any player at all.
    /// </summary>
    public sealed class ShotLanded : GameEvent
    {
        public ShotLanded(int playerId, int seat, int shot, Volley volley, ShotOutcome outcome, bool struckAPlayer, int knockedOut)
        {
            PlayerId = playerId;
            Seat = seat;
            Shot = shot;
            Volley = volley;
            Outcome = outcome;
            StruckAPlayer = struckAPlayer;
            KnockedOut = knockedOut;
        }

        public int PlayerId { get; }

        public int Seat { get; }

        public int Shot { get; }

        public Volley Volley { get; }

        public ShotOutcome Outcome { get; }

        public bool StruckAPlayer { get; }

        public int KnockedOut { get; }
    }

    /// <summary>
    /// A player used an item (before the shot). A teleporter, a shrink ray and a redirector fly (shoot_teleporter,
    /// shoot_shrinkray, shoot_redirector): <see cref="Flight"/> is the path the authority flew it along, which every peer
    /// draws it on (U.RICO-T79); null for an item that does not fly.
    /// </summary>
    public sealed class ItemUsed : GameEvent
    {
        public ItemUsed(int playerId, Item item, ShotPath flight = null)
        {
            PlayerId = playerId;
            Item = item;
            Flight = flight;
        }

        public int PlayerId { get; }

        public Item Item { get; }

        /// <summary>The item's flight, from the muzzle to where it stopped; null when it does not fly.</summary>
        public ShotPath Flight { get; }
    }

    /// <summary>
    /// A redirector in flight stopped (redirector_detonate, U.RICO-T80): its owner pressed the item key again. It stood
    /// <see cref="Distance"/> along the <see cref="ItemUsed.Flight"/> it set off on; a standing redirector opens out there.
    /// </summary>
    public sealed class RedirectorStopped : GameEvent
    {
        public RedirectorStopped(int playerId, double distance)
        {
            PlayerId = playerId;
            Distance = distance;
        }

        public int PlayerId { get; }

        public double Distance { get; }
    }

    /// <summary>
    /// Every item still in the air is gone without taking effect (U.RICO-T80): a new turn (next_player destroys every
    /// obj_teleporter, obj_shrinkray and obj_redirector) or a new level.
    /// </summary>
    public sealed class ItemFlightsCleared : GameEvent
    {
    }

    /// <summary>The aim assist turned a player's aim onto a shot (or left it, when none reaches an opponent).</summary>
    public sealed class AssistApplied : GameEvent
    {
        public AssistApplied(int playerId, double heading, double power, bool found)
        {
            PlayerId = playerId;
            Heading = heading;
            Power = power;
            Found = found;
        }

        public int PlayerId { get; }

        public double Heading { get; }

        public double Power { get; }

        public bool Found { get; }
    }

    /// <summary>
    /// A shrink ray struck a level shape (U.RICO-T90: obj_shrinkray's collision): it shrinks away and is gone from the level.
    /// <see cref="Shape"/> is its index in RoundLayout.Shapes (-1 for the border, which nothing changes), <see cref="At"/>
    /// where the ray struck.
    /// </summary>
    public sealed class Shrunk : GameEvent
    {
        public Shrunk(int bySeat, int shape, Vec2 at)
        {
            BySeat = bySeat;
            Shape = shape;
            At = at;
        }

        public int BySeat { get; }

        public int Shape { get; }

        public Vec2 At { get; }
    }

    /// <summary>Why a player's tank is somewhere new.</summary>
    public enum MoveCause
    {
        /// <summary>Stood at a spot (a new round, a new level, a seat taken).</summary>
        Placed,

        /// <summary>Drove there (UR-T24).</summary>
        Drove,

        /// <summary>A teleporter put it there.</summary>
        Teleported,
    }

    /// <summary>A player's tank moved.</summary>
    public sealed class Moved : GameEvent
    {
        public Moved(int playerId, Vec2 from, Vec2 to, MoveCause cause)
        {
            PlayerId = playerId;
            From = from;
            To = to;
            Cause = cause;
        }

        public int PlayerId { get; }

        public Vec2 From { get; }

        public Vec2 To { get; }

        public MoveCause Cause { get; }
    }

    /// <summary>The seat on turn drove, so its shot is forfeit this turn (the first drive of a turn only).</summary>
    public sealed class DriveStarted : GameEvent
    {
        public DriveStarted(int playerId, int seat)
        {
            PlayerId = playerId;
            Seat = seat;
        }

        public int PlayerId { get; }

        public int Seat { get; }
    }

    /// <summary>Why a turn passed.</summary>
    public enum TurnPassReason
    {
        /// <summary>The player gave it up (EndTurn).</summary>
        EndedEarly,

        /// <summary>The turn clock ran out before they fired.</summary>
        TimedOut,

        /// <summary>Their shot landed; the next turn follows after the pause.</summary>
        ShotLanded,
    }

    /// <summary>A seat's turn is over.</summary>
    public sealed class TurnPassed : GameEvent
    {
        public TurnPassed(int seat, TurnPassReason reason)
        {
            Seat = seat;
            Reason = reason;
        }

        public int Seat { get; }

        public TurnPassReason Reason { get; }
    }

    /// <summary>A new turn began for this seat.</summary>
    public sealed class TurnStarted : GameEvent
    {
        public TurnStarted(int seat)
        {
            Seat = seat;
        }

        public int Seat { get; }
    }

    /// <summary>The turn clock passed into one of its last whole seconds (a beep).</summary>
    public sealed class Countdown : GameEvent
    {
        public Countdown(int secondsLeft)
        {
            SecondsLeft = secondsLeft;
        }

        public int SecondsLeft { get; }
    }

    /// <summary>A round began (everyone at full health, loadouts and levels fresh).</summary>
    public sealed class RoundStarted : GameEvent
    {
        public RoundStarted(int round)
        {
            Round = round;
        }

        public int Round { get; }
    }

    /// <summary>The round ended: the winner's seat, or -1 when nobody survived.</summary>
    public sealed class RoundOver : GameEvent
    {
        public RoundOver(int winner)
        {
            Winner = winner;
        }

        public int Winner { get; }
    }

    /// <summary>The goal was reached and the match is over: the champion's seat.</summary>
    public sealed class GameOver : GameEvent
    {
        public GameOver(int champion)
        {
            Champion = champion;
        }

        public int Champion { get; }
    }

    /// <summary>
    /// The match was paused or resumed (UR-T43, obj_pausemenu's isPaused): while paused the turn clock, the Ready card's
    /// hold, the AI, every shot and tank in motion, the crates and the hazards all wait, and nobody acts but to vote.
    /// <see cref="PlayerId"/> is who asked (-1 for the host or a vote).
    /// </summary>
    public sealed class PauseChanged : GameEvent
    {
        public PauseChanged(int playerId, bool paused)
        {
            PlayerId = playerId;
            Paused = paused;
        }

        public int PlayerId { get; }

        public bool Paused { get; }
    }

    /// <summary>The round was quit from the pause menu (UR-T43, obj_btn_quit_round): the match is over and play goes back to practice.</summary>
    public sealed class MatchQuit : GameEvent
    {
        public MatchQuit(int playerId)
        {
            PlayerId = playerId;
        }

        public int PlayerId { get; }
    }

    /// <summary>A crate began to fall.</summary>
    public sealed class CrateDropped : GameEvent
    {
        public CrateDropped(Drop crate)
        {
            Crate = crate;
        }

        public Drop Crate { get; }
    }

    /// <summary>A crate reached its spot and can now be collected.</summary>
    public sealed class CrateLanded : GameEvent
    {
        public CrateLanded(Drop crate)
        {
            Crate = crate;
        }

        public Drop Crate { get; }
    }

    /// <summary>A crate left the level (collected, expired or cleared).</summary>
    public sealed class CrateRemoved : GameEvent
    {
        public CrateRemoved(Drop crate)
        {
            Crate = crate;
        }

        public Drop Crate { get; }
    }

    /// <summary>
    /// U.RICO-T48: a crate left the level other than by being swept away with the round: collected (its collision event's
    /// ef_ring) or burst after lying too long (obj_powerup_explosion). Every peer draws it where the crate lay.
    /// </summary>
    public sealed class CrateGone : GameEvent
    {
        public CrateGone(int crateId, DropKind kind, Vec2 spot, DropEnd end)
        {
            CrateId = crateId;
            Kind = kind;
            Spot = spot;
            End = end;
        }

        public int CrateId { get; }

        public DropKind Kind { get; }

        public Vec2 Spot { get; }

        public DropEnd End { get; }
    }

    /// <summary>
    /// U.RICO-T48: a shield wall broke (obj_shieldwall: ef_ring, ef_firework and spawn_glass's shards at <see cref="Center"/>),
    /// <see cref="Struck"/> by a shot or a vortex (with sfx_shatter) or decayed (silently). A wall swept away with the round
    /// raises nothing.
    /// </summary>
    public sealed class ShieldWallShattered : GameEvent
    {
        public ShieldWallShattered(int hazardId, Vec2 center, bool struck)
        {
            HazardId = hazardId;
            Center = center;
            Struck = struck;
        }

        public int HazardId { get; }

        public Vec2 Center { get; }

        public bool Struck { get; }
    }

    /// <summary>A shot passed over a crate: its collector got a weapon, an item or health (<see cref="Value"/>).</summary>
    public sealed class CratePickedUp : GameEvent
    {
        public CratePickedUp(int playerId, DropKind kind, int value)
        {
            PlayerId = playerId;
            Kind = kind;
            Value = value;
        }

        public int PlayerId { get; }

        public DropKind Kind { get; }

        /// <summary>The weapon or item index it gave, or the health added.</summary>
        public int Value { get; }
    }

    /// <summary>The level in play changed (a match's map, a new round of the random arena, a practice reroll).</summary>
    public sealed class LayoutChanged : GameEvent
    {
        public LayoutChanged(int map, int seed, int version, bool rolled)
        {
            Map = map;
            Seed = seed;
            Version = version;
            Rolled = rolled;
        }

        public int Map { get; }

        public int Seed { get; }

        /// <summary>Goes up by one per roll, so a peer adopts the newest and never an older one.</summary>
        public int Version { get; }

        /// <summary>True when a round or a reroll laid it afresh (the version went up), false for a map picked at match start.</summary>
        public bool Rolled { get; }
    }

    /// <summary>
    /// A player rerolled the level: in practice (the practice dummies stand back up on it), or in a match from the pause
    /// menu before the round's first shot (UR-T43, randomize_level: the turn starts over on the new level).
    /// </summary>
    public sealed class LevelRerolled : GameEvent
    {
        public LevelRerolled(int playerId, int map, int seed, int version)
        {
            PlayerId = playerId;
            Map = map;
            Seed = seed;
            Version = version;
        }

        public int PlayerId { get; }

        public int Map { get; }

        public int Seed { get; }

        public int Version { get; }
    }

    /// <summary>A player voted.</summary>
    public sealed class VoteCast : GameEvent
    {
        public VoteCast(int playerId, string kind, bool yes)
        {
            PlayerId = playerId;
            Kind = kind;
            Yes = yes;
        }

        public int PlayerId { get; }

        public string Kind { get; }

        public bool Yes { get; }
    }

    /// <summary>A vote reached its threshold: it passed.</summary>
    public sealed class VotePassed : GameEvent
    {
        public VotePassed(string kind, int yes, int no)
        {
            Kind = kind;
            Yes = yes;
            No = no;
        }

        public string Kind { get; }

        public int Yes { get; }

        public int No { get; }
    }

    /// <summary>A vote can no longer reach its threshold: it failed.</summary>
    public sealed class VoteFailed : GameEvent
    {
        public VoteFailed(string kind, int yes, int no)
        {
            Kind = kind;
            Yes = yes;
            No = no;
        }

        public string Kind { get; }

        public int Yes { get; }

        public int No { get; }
    }

    /// <summary>A player took a match seat (UR-T65).</summary>
    public sealed class SeatJoined : GameEvent
    {
        public SeatJoined(int playerId, int seat, bool isAi)
        {
            PlayerId = playerId;
            Seat = seat;
            IsAi = isAi;
        }

        public int PlayerId { get; }

        public int Seat { get; }

        public bool IsAi { get; }
    }

    /// <summary>A human's connection dropped; their seat is held for them (UR-T13).</summary>
    public sealed class SeatDropped : GameEvent
    {
        public SeatDropped(int playerId, int seat)
        {
            PlayerId = playerId;
            Seat = seat;
        }

        public int PlayerId { get; }

        public int Seat { get; }
    }

    /// <summary>A dropped human is back in their seat.</summary>
    public sealed class SeatRejoined : GameEvent
    {
        public SeatRejoined(int playerId, int seat)
        {
            PlayerId = playerId;
            Seat = seat;
        }

        public int PlayerId { get; }

        public int Seat { get; }
    }

    /// <summary>A seat left the match (the player left, timed out or was kicked).</summary>
    public sealed class SeatLeft : GameEvent
    {
        public SeatLeft(int playerId, int seat)
        {
            PlayerId = playerId;
            Seat = seat;
        }

        public int PlayerId { get; }

        public int Seat { get; }
    }
}
