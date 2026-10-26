// Player, the network side: the [ToServer]/[ToClients] calls, every [Synced] value, the authority copy (SyncFromAuthority) and the
// events the authority publishes, played here as the volley and result sent to every peer. (U.RICO-T39: split out of Player.cs, no behaviour change.)
using System.Collections.Generic;
using Ricochet.Core.Ai;
using Ricochet.Core.Aim;
using Ricochet.Core.Arsenal;
using Ricochet.Core.Level;
using Ricochet.Core.Match;
using Ricochet.Core.Menu;
using Ricochet.Core.Net;
using Unity.Multiplayer;      // NetEntity, NetPlayer, NetRoster, NetBots
using Unity.Multiplayer.Core; // [ToServer], [Synced]
using UnityEngine;
using Cmd = Ricochet.Core.Commands;

namespace Ricochet
{
    public partial class Player : NetEntity
    {
        // client -> server: the owner's aim, so everyone sees which way the barrel points (at most 20 a second).
        [ToServer(RateLimit = 20)]
        public partial void Aim(float heading, float power);

        // client -> server: fire the selected weapon at this aim. The shot carries its own aim, so a dropped Aim never
        // changes what flies.
        [ToServer(RateLimit = 4)]
        public partial void Fire(float heading, float power);

        // client -> server: select the next weapon (+1, cycle_weapon_right) or the one before (-1, cycle_weapon_left).
        [ToServer(RateLimit = 10)]
        public partial void CycleWeapon(int direction);

        // client -> server: select the next item (+1) or the one before (-1).
        [ToServer(RateLimit = 10)]
        public partial void CycleItem(int direction);

        // client -> server (U.RICO-T49): a click on a weapon's HUD icon (obj_icon_weapon_*'s Left Pressed) selects it.
        [ToServer(RateLimit = 10)]
        public partial void SelectWeapon(int weapon);

        // client -> server (U.RICO-T49): a click on an item's HUD icon (obj_item_*'s Left Pressed) selects it.
        [ToServer(RateLimit = 10)]
        public partial void SelectItem(int item);

        // client -> server: the grenade fuse keys: -1 shorter, +1 longer, +2 the longest.
        [ToServer(RateLimit = 10)]
        public partial void Fuse(int change);

        // client -> server: use the selected item at this aim (a teleporter and a shrink ray fly along it).
        [ToServer(RateLimit = 4)]
        public partial void UseItem(float heading, float power);

        // client -> server: set off this player's vortex where it is now (vortex_detonate.gml: fire again after firing it).
        [ToServer(RateLimit = 4)]
        public partial void Detonate();

        // client -> server (UR-T23): R, PageUp or PageDown in practice: roll another random level (randomize_level.gml).
        [ToServer(RateLimit = 2)]
        public partial void Reroll();

        // client -> server: the owner is holding W (+1, move_forward.gml) or S (-1, move_backward.gml); sent every tenth of a
        // second while held, and the server keeps driving only a moment past the last one, so a dropped stop cannot leave the
        // tank running. Driving forfeits the turn's shot (Match.Move).
        [ToServer(RateLimit = 20)]
        public partial void Drive(int direction);

        // client -> server: F or the End Turn button (next_player.gml): give up the rest of this turn.
        [ToServer(RateLimit = 4)]
        public partial void EndTurn();

        // server -> every client (UR-T12): the volley the server solved for shot number `shot` (Core's ShotWire), so every
        // peer flies the server's own bounce. Sent again, for the same shot, with the volley cut short when a vortex is set
        // off in the air.
        [ToClients]
        public partial void ShotResolved(int shot, double[] numbers, string ids);

        // server -> every client (UR-T12): what shot number `shot` did in a match once it landed (Core's ShotOutcome): the
        // damage each seat it struck took, and the seats it put out of the round.
        [ToClients]
        public partial void ShotResult(int shot, int[] seats, int[] damage, int[] knockedOut, int points);

        // server -> every client (U.RICO-T48): something on the level broke that every peer draws, sent once by one player
        // object for the whole session: kind 0 a shield wall (id, a = 1 when a shot broke it), kind 1 a crate gone (id, a its
        // DropKind, b its DropEnd), at (x, y); kind 2 a sound every peer plays (U.RICO-T50: id the SoundCue).
        [ToClients]
        public partial void Flourish(int kind, int id, int a, int b, float x, float y);

        // server -> every client (U.RICO-T79): this player used an item that flies (a teleporter, a shrink ray, a
        // redirector) along the path the server flew it (Core's ShotWire.EncodeFlight), so every peer draws it in flight.
        [ToClients]
        public partial void ItemFlown(int item, double[] numbers, string ids);

        // The level in play (UR-T23): the map, the seed its random round was built from, and a count that goes up by one per
        // change, so a peer adopts the newest and every one lays the identical round (Arena.AdoptLayout). Every Player
        // carries the same three, so a late joiner learns the level whichever Player it sees first, practice included.
        [Synced] private int _levelMap;
        [Synced] private int _levelSeed;
        [Synced] private int _levelVersion;

        // Only the server writes these; every peer, late joiners included, reads the same values.
        [Synced] private Vector2 _position;
        [Synced] private float _heading = StartHeading;
        [Synced] private float _power = StartPower;

        // Whether the authority is driving this tank (a drive key held: the original's isMoving), so every peer turns the
        // hull after the turret and lays tracks only while it drives (U.RICO-T45).
        [Synced] private bool _tankMoving;

        // The last shot: its aim, weapon, fuse and extra bounces, and a count that goes up by one per shot so every peer
        // knows a new one was fired.
        [Synced] private int _shots;
        [Synced] private float _shotHeading;
        [Synced] private float _shotPower;
        [Synced] private int _shotWeapon;
        [Synced] private float _shotFuse = 2f;
        [Synced] private int _shotExtraBounces;

        // How far from the muzzle the last shot (a vortex) was set off; 0 when it was not.
        [Synced] private float _shotDetonation;

        // The match: the seat (turn order and spot; -1 for none), health, and whether still in the round.
        [Synced] private int _seat = -1;
        [Synced] private int _health = StartHealth;
        [Synced] private bool _inRound;

        // Reconnects (UR-T13): the kit seat a human's connection sits in (kept while they are away, when the server owns
        // this object), and whether their connection has dropped and the seat is held for them.
        [Synced] private int _connectionSeat = -1;
        [Synced] private bool _away;

        // Points scored this match (UR-T8: hit.gml's pScore), carried from round to round.
        [Synced] private int _score;

        // The loadout (Loadout.PackWeapons / PackItems; 0 until the server first writes it), the extra bounces used this
        // turn, and the size a shrink ray left this player at (1 = full size).
        [Synced] private int _weapons;
        [Synced] private int _items;
        [Synced] private int _extraBounces;
        [Synced] private float _scale = 1f;

        // The last crate collected (kind * 1000 + what it gave: a weapon or item index, or the health added), and a count
        // that goes up by one per crate so every peer can float its "Laser x 1 collected" over the player.
        [Synced] private int _pickupCount;
        [Synced] private int _lastPickup;

        // The last aim assist: the aim the server turned this player onto, and a count that goes up by one per use so the
        // owner's aim follows it and every peer draws its beam.
        [Synced] private int _assists;
        [Synced] private float _assistHeading;
        [Synced] private float _assistPower;

        private int _seenShots;             // every peer: the last shot it has drawn

        // ---- the authority's copy of this player (server only)

        private static int s_nextPlayerId = 1;
        private int _playerId;
        private Cmd.PlayerState _state;
        private Cmd.GameAuthority _registered; // the authority _state belongs to
        private Cmd.GameAuthority _standalone;  // one of its own, for a player in a session that serves no Arena (a bare test fixture)

        /// <summary>What commands name this player by (server only; 0 before it has spawned).</summary>
        public int PlayerId => _playerId;

        // The authority this player's commands go to: the arena's, or (no Arena serving this session) a plain practice one.
        private Cmd.GameAuthority Authority
        {
            get
            {
                Arena arena = Arena.Current;
                if (arena != null)
                {
                    return arena.Authority;
                }

                if (_standalone == null)
                {
                    NetSession session = Session;
                    _standalone = new Cmd.GameAuthority(new StandaloneWorld(session), Arena.Rules, Arena.MatchRules, Arena.ArsenalRules,
                        Arena.DropRules, Arena.HazardRules);
                    _standalone.Published += e => Present(session, e);
                }

                return _standalone;
            }
        }

        private sealed class StandaloneWorld : Cmd.IGameWorld
        {
            private readonly NetSession _session;

            public StandaloneWorld(NetSession session)
            {
                _session = session;
            }

            public IReadOnlyList<Target> DummyTargets() => Arena.Dummies(_session);
        }

        /// <summary>Server: the authority's state for this player, added (or moved to the arena's authority) the first time it is asked for.</summary>
        private Cmd.PlayerState State
        {
            get
            {
                Cmd.GameAuthority wanted = Authority;
                if (_state == null || _registered != wanted)
                {
                    if (_playerId == 0)
                    {
                        _playerId = s_nextPlayerId++;
                    }

                    Cmd.PlayerSnapshot from = _state != null ? Cmd.PlayerSnapshot.Of(_state) : SnapshotFromSynced();
                    if (_registered != null)
                    {
                        _registered.RemovePlayer(_playerId);
                    }

                    _state = wanted.AddPlayer(_playerId, IsAi, from);
                    _registered = wanted;
                }

                return _state;
            }
        }

        // A player object that spawned before an authority knew it (or came back after a reconnect): what it has synced.
        private Cmd.PlayerSnapshot SnapshotFromSynced()
        {
            return new Cmd.PlayerSnapshot
            {
                Seat = Seat,
                Position = Arena.ToCore(Position),
                Heading = Heading,
                Power = Power,
                Weapons = Weapons,
                Items = Items,
                ExtraBounces = ExtraBounces,
                Scale = Scale,
                Shots = Shots,
                ShotHeading = ShotHeading,
                ShotPower = ShotPower,
                ShotWeapon = ShotWeapon,
                ShotFuse = ShotFuse,
                ShotExtraBounces = ShotExtraBounces,
                ShotDetonation = ShotDetonation,
                Assists = Assists,
                AssistHeading = AssistHeading,
                AssistPower = AssistPower,
                Pickups = PickupCount,
                LastPickup = LastPickup,
            };
        }

        /// <summary>Server: copies the authority's state for this player out to the values every peer reads.</summary>
        internal void SyncFromAuthority()
        {
            if (!IsServer || !IsSpawned)
            {
                return;
            }

            Cmd.PlayerState state = State;
            Vector2 at = Arena.ToUnity(state.Position);
            if (Position != at)
            {
                Position = at;
            }

            if (Heading != (float)state.Heading)
            {
                Heading = (float)state.Heading;
            }

            if (Power != (float)state.Power)
            {
                Power = (float)state.Power;
            }

            if (ExtraBounces != state.ExtraBounces)
            {
                ExtraBounces = state.ExtraBounces;
            }

            if (Scale != (float)state.Scale)
            {
                Scale = (float)state.Scale;
            }

            if (Shots != state.Shots)
            {
                ShotHeading = (float)state.ShotHeading;
                ShotPower = (float)state.ShotPower;
                ShotWeapon = (int)state.ShotWeapon;
                ShotFuse = (float)state.ShotFuse;
                ShotExtraBounces = state.ShotExtraBounces;
                Shots = state.Shots;
            }

            if (ShotDetonation != (float)state.ShotDetonation)
            {
                ShotDetonation = (float)state.ShotDetonation;
            }

            if (Assists != state.Assists)
            {
                AssistHeading = (float)state.AssistHeading;
                AssistPower = (float)state.AssistPower;
                Assists = state.Assists;
            }

            if (PickupCount != state.Pickups)
            {
                LastPickup = state.LastPickup;
                PickupCount = state.Pickups;
            }

            MirrorLoadout();
        }

        /// <summary>Server: sits this player in <paramref name="seat"/> (-1 for none) with the match, and on every peer.</summary>
        internal void AssignSeat(int seat)
        {
            Seat = seat;
            if (IsServer)
            {
                Authority.SetSeat(State.Id, seat);
            }
        }

        /// <summary>Server: stands this player's tank at <paramref name="spot"/> (a spawn, a seat taken).</summary>
        internal void Place(Vector2 spot)
        {
            if (IsServer)
            {
                Authority.Place(State.Id, Arena.ToCore(spot));
                SyncFromAuthority();
            }
        }

        // Server: gives the authority this player's command, copies what changed out to the peers, and has the match
        // mirrored when the command can change it.
        private void Run(Cmd.Command command, bool mirror = true)
        {
            Authority.Apply(command);
            SyncFromAuthority();
            if (mirror && Arena.Current != null)
            {
                Arena.Current.Mirror();
            }
        }

        /// <summary>Server: writes the level in play for the clients to adopt (see <see cref="Arena.AdoptLayout"/>).</summary>
        internal void MirrorLevel(int map, int seed, int version)
        {
            if (!IsServer)
            {
                return;
            }

            LevelMap = map;
            LevelSeed = seed;
            LevelVersion = version;
        }

        // The authority's events about shots (UR-T34), played by the player they name: a shot fired is drawn here and sent to
        // every client as the volley the authority solved (UR-T12; its own solve is the one that lands), a vortex set off in
        // the air cuts that volley short, and a landed shot's result goes to everyone. Called for every event of a session.
        internal static void Present(NetSession session, Cmd.GameEvent e)
        {
            switch (e)
            {
                case Cmd.ShotFired fired:
                    ById(session, fired.PlayerId)?.OnShotFired(fired);
                    break;
                case Cmd.ShotCut cut:
                    ById(session, cut.PlayerId)?.OnShotCut(cut);
                    break;
                case Cmd.ShotLanded landed:
                    ById(session, landed.PlayerId)?.OnShotLanded(landed);
                    break;
                case Cmd.ShieldWallShattered wall:
                    Sender(session)?.Flourish(FlourishWall, wall.HazardId, wall.Struck ? 1 : 0, 0, (float)wall.Center.X, (float)wall.Center.Y);
                    break;
                case Cmd.CrateGone crate:
                    Sender(session)?.Flourish(FlourishCrate, crate.CrateId, (int)crate.Kind, (int)crate.End, (float)crate.Spot.X, (float)crate.Spot.Y);
                    break;
                case Cmd.ItemUsed used:
                    ById(session, used.PlayerId)?.OnItemUsed(used); // U.RICO-T79: its flight, drawn on every peer
                    goto default; // and its launch sound, heard on every peer
                default:
                    // U.RICO-T50: the sounds of the events no view draws (an item used, a shrink, a teleport, a crate
                    // collected, the countdown, a turn timed out, a knock-out, the round's applause), heard on every peer.
                    SoundCue? cue = SoundBook.ForEvent(e);
                    if (cue.HasValue)
                    {
                        Sender(session)?.Flourish(FlourishSound, (int)cue.Value, 0, 0, 0f, 0f);
                    }

                    break;
            }
        }

        private const int FlourishWall = 0;
        private const int FlourishCrate = 1;
        private const int FlourishSound = 2;

        // The one player object a session-wide flourish goes out on (any will do; every peer has them all).
        private static Player Sender(NetSession session)
        {
            foreach (Player player in In(session))
            {
                if (player.IsServer)
                {
                    return player;
                }
            }

            return null;
        }

        // Every peer (the host's included): the server's flourish, raised on the feed as the event it came from.
        private void OnFlourish(int kind, int id, int a, int b, float x, float y)
        {
            var at = new Vec2(x, y);
            switch (kind)
            {
                case FlourishWall:
                    Presentation.Raise(this, new Cmd.ShieldWallShattered(id, at, a != 0));
                    break;
                case FlourishCrate:
                    Presentation.Raise(this, new Cmd.CrateGone(id, (DropKind)Mathf.Clamp(a, 0, DropRules.KindCount - 1), at, (DropEnd)b));
                    break;
                case FlourishSound when id >= 0 && id < SoundBook.Count:
                    GameAudio.PlaySettled((SoundCue)id);
                    break;
            }
        }

        private static Player ById(NetSession session, int playerId)
        {
            foreach (Player player in In(session))
            {
                if (player._playerId == playerId)
                {
                    return player;
                }
            }

            return null;
        }

        // Server: an item used. One that flies is drawn here at once and sent to every client along the server's flight.
        private void OnItemUsed(Cmd.ItemUsed used)
        {
            if (used.Flight == null)
            {
                return;
            }

            Presentation.Raise(this, used);
            ShotWire.EncodeFlight(used.Flight, out double[] numbers, out string ids);
            ItemFlown((int)used.Item, numbers, ids ?? string.Empty);
        }

        // A client: an item this player used flies along the server's flight (the host's own peer drew it already).
        private void OnItemFlown(int item, double[] numbers, string ids)
        {
            if (IsServer)
            {
                return;
            }

            ShotPath flight = ShotWire.DecodeFlight(numbers, ids);
            if (flight == null || !System.Enum.IsDefined(typeof(Item), item))
            {
                Debug.LogWarning("[Ricochet] " + name + ": an item's flight came over unreadable; nothing drawn.");
                return;
            }

            Presentation.Raise(this, new Cmd.ItemUsed(PlayerId, (Item)item, flight));
        }

        private void OnShotFired(Cmd.ShotFired fired)
        {
            SyncFromAuthority();
            _seenShots = fired.Shot; // a second Fire in the same frame finds this one in the air
            Draw(fired.Shot, fired.Volley);
            SendResolved(fired.Shot, fired.Volley);
        }

        private void OnShotCut(Cmd.ShotCut cut)
        {
            SyncFromAuthority();
            LastVolley = cut.Volley;
            Presentation.Raise(this, cut);

            SendResolved(cut.Shot, cut.Volley); // every client cuts its flight at the server's own cut, striking whom it struck
        }

        private void OnShotLanded(Cmd.ShotLanded landed)
        {
            SyncFromAuthority();
            if (landed.Outcome != null)
            {
                ServerResult(landed.Outcome);
            }
        }

        // Server: ShotResolved for shot number `shot`.
        private void SendResolved(int shot, Volley volley)
        {
            ShotWire.Encode(volley, out double[] numbers, out string ids);
            ShotResolved(shot, numbers, ids ?? string.Empty);
        }

        // A client: the server solved shot number `shot`. A new shot is drawn as the server solved it; the same shot again is
        // its vortex set off in the air, cut where the server cut it. (A host's own peer is the server: it drew it already.)
        private void OnShotResolved(int shot, double[] numbers, string ids)
        {
            if (IsServer)
            {
                return;
            }

            Volley volley = ShotWire.Decode(numbers, ids);
            if (volley == null)
            {
                Debug.LogWarning("[Ricochet] " + name + ": shot " + shot + " came over unreadable; nothing drawn.");
                return;
            }

            if (shot > _seenShots)
            {
                _seenShots = shot;
                Draw(shot, volley);
            }
            else if (shot == LastVolleyShot && LastVolley != null && volley.Main.End == ShotEndKind.Detonated)
            {
                LastVolley = volley;
                Presentation.Raise(this, new Cmd.ShotCut(PlayerId, shot, 0, volley));
            }
        }

        /// <summary>
        /// Server: what this player's volley did in a match once it landed (Arena.Landed). Every peer gets it: the struck
        /// players float the damage they took over themselves once that peer's own flight lands.
        /// </summary>
        public void ServerResult(ShotOutcome outcome)
        {
            if (!IsServer || outcome == null)
            {
                return;
            }

            LastOutcome = outcome;
            LastOutcomeShot = Shots;
            outcome.Encode(out int[] seats, out int[] damage, out int[] knockedOut);
            ShotResult(Shots, seats, damage, knockedOut, outcome.Points);
        }

        // Every peer (the host's included): the server's result for shot number `shot`.
        private void OnShotResult(int shot, int[] seats, int[] damage, int[] knockedOut, int points)
        {
            ShotOutcome outcome = ShotOutcome.Decode(seats, damage, knockedOut, points, global::Ricochet.Core.Match.MatchRules.MaxSeats);
            if (outcome == null)
            {
                Debug.LogWarning("[Ricochet] " + name + ": the result of shot " + shot + " came over unreadable.");
                return;
            }

            LastOutcome = outcome;
            LastOutcomeShot = shot;
            if (_shotView != null && _shotView.Flying && shot == LastVolleyShot)
            {
                _pendingOutcome = outcome; // shown when this peer's flight lands, not before the shot gets there
            }
            else
            {
                ShowOutcome(outcome);
            }
        }
    }
}
