// The game's rules on the server, in a plain class so a test (or a bot test) can serve them on its own session.
// It holds the level every shot bounces in (Core's Level.Default, the same on every peer, so nothing about it goes
// over the wire), spawns the keyed practice dummies shots are fired at before a match, and runs the match (UR-T4): up
// to sixteen seats, the connected humans plus AI opponents it spawns, taking turns on Core's Match with its turn
// clock and health, mirrored to every peer through the Turns object and each Player. It offers two votes: start a
// match (with a number of AI opponents) and restart (the round's health, or the practice dummies). Weapons and items
// (UR-T5): a landed volley strikes everyone it reached (Core's Arsenal), every player's loadout is reset at the start of
// each round, and during a round Core's DropField drops crates (spawned as keyed Crate objects) that a shot passing
// over collects for its shooter: a random weapon, a random item or some health. AI opponents (UR-T7): the match vote
// carries a MatchSetup (AI count, AI skill, aim assists each); on an AI seat's turn Core's AiTurn thinks (its "is
// thinking..." line mirrored through Turns), plans the shot with Ai.Plan (a straight shot, a bank shot or a railgun
// shot, nudged by its skill), turns the barrel onto it and fires through the same checks as a human. Level hazards
// (UR-T6): Core's HazardField holds the electric arcs, laser gates and foliage each round is laid with (when the scene
// asks for them), the shield walls and redirectors players place, and the breaches a vortex tears in the border; each is
// mirrored as a keyed LevelHazard object, and every peer builds the level a shot sees from them (LevelFor). A landed
// volley shatters the shield wall or cuts down the tuft it ran into, and a vortex's implosion swallows what it reaches
// and breaches the border. The announcer and scores (UR-T8): the match vote's setup also names the personality on the air
// and how often it speaks; Core's Announcer welcomes everyone when the match starts, cheers a shot that knocks an
// opponent out and heckles one that hits nobody, each line mirrored through Turns; a hit on an opponent scores the shooter
// (Match.Strike, for the bounces and the volley's time in the air), mirrored to each Player's score. The menu flow (UR-T9):
// the match vote's setup also carries the map (Core's Maps: the level every peer builds from its index, mirrored through
// Turns) and the game's goal; once a round ends with the goal reached the match is over (MatchPhase.GameOver), nobody fires
// and every peer's HUD shows the endgame recap until a new match (or a rematch vote) starts. Reconnects (UR-T13): when the
// kit holds a dropped player's seat (NetSession.SeatHeld) their Player waits, marked Away, and their match seat keeps its
// health and score (Match.Drop); on their turn the clock waits for them, and every peer's HUD says so through Turns. Back
// in time (SeatReclaimed) they resume the same turn (Match.Rejoin); once the kit gives the seat up (SeatReleased) they leave.
// Votes (UR-T14): a third vote, the kit's kick, removes a player; the kit frees their seat at once, so they leave the same way.
// Commands and events (UR-T34): the rules above are applied by Core's GameAuthority, the one place that changes the match, the
// loadouts, the crates and the hazards; Arena builds it, feeds it the clock and the match's seats, and listens to the events
// it publishes (OnEvent): it spawns and lands the crate objects, knocks down the dummy a shot struck, adopts the level it
// laid, starts an AI seat's turn, and has the announcer speak. Nothing here changes the game another way.
using System;
using System.Collections.Generic;
using Ricochet.Core.Ai;
using Ricochet.Core.Aim;
using Ricochet.Core.Arsenal;
using Ricochet.Core.Level;
using Ricochet.Core.Match;
using Ricochet.Core.Menu;
using Ricochet.Core.Net;
using Unity.Multiplayer;      // NetSession, NetKey
using Unity.Multiplayer.Core; // VoteKind
using UnityEngine;
using Cmd = Ricochet.Core.Commands;

namespace Ricochet
{
    public sealed class Arena
    {
        public const int DummyCount = 3;
        public const string DummyKeyPrefix = Cmd.TargetIds.DummyPrefix;
        public const string SeatKeyPrefix = Cmd.TargetIds.SeatPrefix;
        public const string AiKeyPrefix = "ai:";
        public const string RestartVote = "restart";
        public const string MatchVote = "match";

        /// <summary>A dummy's hit radius (its square is drawn 0.8 wide).</summary>
        public const float DummyRadius = 0.45f;

        /// <summary>
        /// The level every peer plays in: the current map's (UR-T9). The first map (the practice range) until a match is
        /// started on another; each peer follows the map Turns carries.
        /// </summary>
        public static Level Level => s_layout.Level;

        // The round being played: the map's level and where each seat stands (a random map's come from LevelSeed, UR-T23).
        private static RoundLayout s_layout = Maps.At(0).Layout(0);

        /// <summary>The map <see cref="Level"/> was built from (an index into Maps.All).</summary>
        public static int MapIndex { get; private set; }

        /// <summary>The seed the current round's random level was built from (0 on a hand-laid map).</summary>
        public static int LevelSeed { get; private set; }

        /// <summary>Counts the server's level changes, so a client adopts the newest and never an older one (0 before any).</summary>
        public static int LevelVersion { get; private set; }

        /// <summary>Raised on this peer when <see cref="Level"/> changes to another map's (the level view redraws).</summary>
        public static event Action<Level> LevelChanged;

        /// <summary>Plays on map <paramref name="index"/> from now on, on this peer (the server's match start, a client's Turns).</summary>
        public static void UseMap(int index) => UseLayout(index, 0);

        /// <summary>
        /// Plays map <paramref name="index"/>'s round for <paramref name="seed"/> from now on, on this peer: a random map's
        /// level and seat spots are built from the seed, so every peer lays the same round (UR-T23).
        /// </summary>
        public static void UseLayout(int index, int seed)
        {
            index = Maps.Clamp(index);
            if (index == MapIndex && seed == LevelSeed)
            {
                return;
            }

            MapIndex = index;
            LevelSeed = seed;
            s_layout = Maps.At(index).Layout(seed);
            LevelChanged?.Invoke(Level);
        }

        /// <summary>Back to the practice range and no level changes seen yet (a session starting or ending).</summary>
        private static void ResetLayout()
        {
            LevelVersion = 0;
            UseLayout(0, 0);
        }

        /// <summary>Client: the server's level (map, seed and version, as Player carries them): adopted when newer than this peer's.</summary>
        internal static void AdoptLayout(int index, int seed, int version)
        {
            if (version > LevelVersion)
            {
                LevelVersion = version;
                UseLayout(index, seed);
            }
        }

        /// <summary>The shot rules every peer plays by.</summary>
        public static readonly ShotRules Rules = ShotRules.Default;

        /// <summary>The match rules every peer plays by.</summary>
        public static readonly MatchRules MatchRules = MatchRules.Default;

        /// <summary>The weapons and items every peer plays with (UR-T5).</summary>
        public static readonly ArsenalRules ArsenalRules = ArsenalRules.Default;

        /// <summary>How crates drop during a match (UR-T5).</summary>
        public static readonly DropRules DropRules = DropRules.Default;

        /// <summary>The level hazards every peer plays with (UR-T6).</summary>
        public static readonly HazardRules HazardRules = HazardRules.Default;

        // One source with the level generator, which keeps its polygons clear of these.
        private static readonly Vector2[] DummyStarts = System.Array.ConvertAll(Seating.DummyStarts, ToUnity);

        private readonly NetSession _session;
        private readonly GameObject _dummyPrefab;
        private readonly System.Random _aiDice = new System.Random();
        private readonly Cmd.GameAuthority _authority;
        private Turns _turns;
        private readonly Dictionary<int, int> _humanSeats = new Dictionary<int, int>(); // kit seat -> match seat
        private MatchSetup _setup = new MatchSetup();
        private AiTurn _aiTurn;
        private Announcer _announcer;
        private readonly System.Random _announcerDice = new System.Random();
        private bool _dummiesUp;

        public Arena(NetSession session, GameObject dummyPrefab)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _dummyPrefab = dummyPrefab ?? throw new ArgumentNullException(nameof(dummyPrefab));
            _authority = new Cmd.GameAuthority(new World(session), Rules, MatchRules, ArsenalRules, DropRules, HazardRules);
            _authority.AdoptLayout(MapIndex, LevelSeed, LevelVersion); // the level this peer is already on
            _authority.Published += OnEvent;
            _hazards.Added += SpawnHazard;
            _hazards.Changed += MirrorHazard;
            _hazards.Removed += hazard => _session.Objects.Despawn(LevelHazard.KeyFor(hazard.Id));
        }

        // The authority's state, by the names this class has always used.
        private Match _match => _authority.Match;

        private HazardField _hazards => _authority.Hazards;

        private DropField _field => _authority.Crates;

        /// <summary>The single authority that applies every player's commands (server only; Player gives it theirs).</summary>
        public Cmd.GameAuthority Authority => _authority;

        /// <summary>
        /// True when each round (and practice) is laid with the rules' electric arcs, laser gates and foliage. The game's
        /// scene turns it on; off, the only hazards are the ones players make (shield walls, redirectors, breaches).
        /// </summary>
        public bool LaysHazards
        {
            get => _authority.LaysHazards;
            set => _authority.LaysHazards = value;
        }

        // Where the practice dummies stand, for the authority's targets.
        private sealed class World : Cmd.IGameWorld
        {
            private readonly NetSession _session;

            public World(NetSession session)
            {
                _session = session;
            }

            public IReadOnlyList<Target> DummyTargets() => Dummies(_session);
        }

        /// <summary>The hazards on the level now (server only).</summary>
        public IReadOnlyList<Hazard> HazardsOnField => _hazards.Hazards;

        /// <summary>The arena the server is running, for Player to land shots on (null on a client).</summary>
        public static Arena Current { get; private set; }

        /// <summary>The match being played (null before the first one starts). Server only.</summary>
        public Match Match => _match;

        /// <summary>The AI opponents the current match was started with.</summary>
        public int AiCount => _setup.AiCount;

        /// <summary>What the current (or last) match was started with: AI count, AI skill, aim assists.</summary>
        public MatchSetup Setup => _setup.Clone();

        /// <summary>The AI seat's turn being played (server only; null on a human's turn).</summary>
        public AiTurn AiTurn => _aiTurn;

        /// <summary>
        /// Serves the rules on <paramref name="session"/>: dummies spawn as soon as it runs as a server, and with
        /// <paramref name="layHazards"/> the level's arcs, gates and foliage too (UR-T6).
        /// </summary>
        public static Arena Serve(NetSession session, GameObject dummyPrefab, bool layHazards = false)
        {
            var arena = new Arena(session, dummyPrefab) { LaysHazards = layHazards };
            session.Started += () =>
            {
                ResetLayout(); // every session opens on the practice range
                arena._authority.AdoptLayout(0, 0, 0);
                if (session.IsServer)
                {
                    Current = arena;
                    arena.SpawnDummies();
                    arena.ResetHazards();
                }
            };
            session.SeatHeld += seat => arena.SeatHeld(seat.Index);
            session.SeatReclaimed += seat => arena.SeatReclaimed(seat.Index);
            session.SeatReleased += seat => arena.SeatReleased(seat.Index);
            session.Ended += _ =>
            {
                if (Current == arena)
                {
                    Current = null;
                }

                ResetLayout();
            };
            if (session.IsServer && session.IsLive)
            {
                ResetLayout();
                arena._authority.AdoptLayout(0, 0, 0);
                Current = arena;
                arena.SpawnDummies();
                arena.ResetHazards();
            }

            return arena;
        }

        public static string DummyKey(int index) => DummyKeyPrefix + index;

        public static int DummyIndex(string key)
        {
            int index;
            return key != null && key.StartsWith(DummyKeyPrefix, StringComparison.Ordinal)
                && int.TryParse(key.Substring(DummyKeyPrefix.Length), out index) ? index : 0;
        }

        public static Vector2 DummyStart(int index) => DummyStarts[((index % DummyStarts.Length) + DummyStarts.Length) % DummyStarts.Length];

        /// <summary>Where a seat's player stands this round (a random map's spots, else Core's fixed sixteen).</summary>
        public static Vector2 SpawnPoint(int seat) => ToUnity(s_layout.SpawnPoint(Math.Max(0, seat)));

        /// <summary>The target id a seated player is hit as ("seat:3").</summary>
        public static string SeatTarget(int seat) => SeatKeyPrefix + seat;

        /// <summary>The seat a target id names, or -1 when it is not a player.</summary>
        public static int SeatOf(string targetId)
        {
            int seat;
            return targetId != null && targetId.StartsWith(SeatKeyPrefix, StringComparison.Ordinal)
                && int.TryParse(targetId.Substring(SeatKeyPrefix.Length), out seat) ? seat : -1;
        }

        public static Vec2 ToCore(Vector2 v) => new Vec2(v.x, v.y);

        public static Vector2 ToUnity(Vec2 v) => new Vector2((float)v.X, (float)v.Y);

        /// <summary>
        /// What a shot fired now can hit: every standing dummy <paramref name="session"/> knows about and, during a match,
        /// every player still in the round (the shooter too: a shot that banks back hurts, as hit.gml's self-hit did).
        /// </summary>
        public static List<Target> Targets(NetSession session)
        {
            List<Target> targets = Dummies(session);
            if (session == null || session.Objects == null)
            {
                return targets;
            }

            if (Turns.For(session) != null)
            {
                foreach (Player player in Player.In(session))
                {
                    if (player.InRound && player.Seat >= 0)
                    {
                        targets.Add(new Target(SeatTarget(player.Seat), ToCore(player.Position), player.Radius));
                    }
                }
            }

            return targets;
        }

        /// <summary>A target for every standing practice dummy <paramref name="session"/> knows about.</summary>
        public static List<Target> Dummies(NetSession session)
        {
            var targets = new List<Target>();
            if (session == null || session.Objects == null)
            {
                return targets;
            }

            foreach (NetKey key in session.Objects.All)
            {
                Dummy dummy = key != null ? key.GetComponent<Dummy>() : null;
                if (dummy != null && dummy.IsSpawned && !dummy.Down)
                {
                    targets.Add(new Target(dummy.Key, ToCore(dummy.Position), DummyRadius));
                }
            }

            return targets;
        }

        /// <summary>
        /// The level <paramref name="shooter"/>'s shot sees in <paramref name="session"/> now: the level with every hazard
        /// on it that stops their shot (not their own shield walls), the breaches and the placed redirectors. The server
        /// reads its own HazardField; every other peer builds it from the LevelHazard objects it has (the same hazards).
        /// </summary>
        public static Level LevelFor(NetSession session, int shooter)
        {
            Arena current = Current;
            if (current != null && current._session == session)
            {
                return current._authority.LevelFor(shooter);
            }

            return session != null ? HazardField.Build(Level, LevelHazard.Collect(session), shooter) : Level;
        }

        /// <summary>
        /// Server: every hazard goes, and the level is laid again when <see cref="LaysHazards"/> (the arcs and gates, and
        /// create_random_foliage's tufts kept clear of every seat's spot, every player and the practice dummies).
        /// </summary>
        public void ResetHazards() => _authority.ResetHazards();

        private void SpawnHazard(Hazard hazard)
        {
            GameObject spawned = _session.Objects.Spawn(HazardPrefab(), LevelHazard.KeyFor(hazard.Id), Vector2.zero);
            spawned.GetComponent<LevelHazard>().Mirror(hazard);
        }

        private void MirrorHazard(Hazard hazard)
        {
            if (_session.Objects.TryGet(LevelHazard.KeyFor(hazard.Id), out LevelHazard entity) && entity != null)
            {
                entity.Mirror(hazard);
            }
        }

        /// <summary>Spawns every dummy at its start, keyed dummy:0, dummy:1, ... (server only).</summary>
        public void SpawnDummies()
        {
            for (int i = 0; i < DummyCount; i++)
            {
                GameObject spawned = _session.Objects.Spawn(_dummyPrefab, DummyKey(i), DummyStart(i));
                spawned.GetComponent<Dummy>().Position = DummyStart(i);
            }

            _dummiesUp = true;
        }

        /// <summary>Despawns every dummy and spawns it again, standing, under the same key, in the same frame.</summary>
        public void ResetDummies()
        {
            RemoveDummies();
            SpawnDummies();
        }

        /// <summary>A shot landed on <paramref name="key"/>: knock it down (server only). False when there is no such dummy.</summary>
        public bool Knock(string key)
        {
            if (key == null || !_session.Objects.TryGet(key, out Dummy dummy) || dummy == null)
            {
                return false;
            }

            dummy.Down = true;
            return true;
        }

        /// <summary>Starts a match with <paramref name="aiCount"/> AI opponents and the default skill and aim assists.</summary>
        public bool StartMatch(int aiCount) => StartMatch(new MatchSetup { AiCount = aiCount });

        /// <summary>
        /// Starts a match (server only): every connected player takes a seat in join order, then the setup's AI opponents
        /// fill the next seats, up to sixteen in all; everyone starts each round with the setup's aim assists; the
        /// practice dummies are cleared away and seat 0 takes the first turn. A match already running starts over.
        /// </summary>
        /// <returns>False (and nothing changes) when fewer than two seats would play.</returns>
        public bool StartMatch(MatchSetup setup)
        {
            setup = setup != null ? setup.Clone() : new MatchSetup();
            int aiCount = setup.AiCount;
            var humans = new List<Player>();
            foreach (Player player in Player.In(_session))
            {
                if (!player.IsAi)
                {
                    humans.Add(player);
                }
            }

            humans.Sort((a, b) => a.KitSeat.CompareTo(b.KitSeat));
            // The lobby's rule (UR-T11): the people first, then as many of the AI as still fit; two seats at least.
            if (!Lobby.CanStart(humans.Count, aiCount))
            {
                return false;
            }

            int seated = Lobby.HumanSeats(humans.Count);
            aiCount = Lobby.AiSeats(humans.Count, aiCount);

            RemoveAi();
            if (_dummiesUp)
            {
                RemoveDummies();
            }

            setup.AiCount = aiCount;
            _setup = setup;
            _setup.ApplyTo(ArsenalRules);
            _aiTurn = null;
            _authority.BeginMatch(_setup.Map, _setup.Goal); // the map room's choice; a random map's level is rolled as round one starts
            _humanSeats.Clear();
            for (int i = 0; i < humans.Count; i++)
            {
                Player human = humans[i];
                int seat = i < seated ? i : -1; // a seventeenth human watches
                human.AssignSeat(seat);
                if (seat >= 0)
                {
                    JoinSeat(human, seat, false);
                    _humanSeats[human.KitSeat] = seat;
                    human.Place(SpawnPoint(seat));
                    if (human.Away)
                    {
                        _authority.Apply(new Cmd.DropSeatCommand(human.PlayerId, HoldLeft(human.KitSeat))); // dropped before the match: their seat waits for them too
                    }
                }
            }

            GameObject opponentPrefab = OpponentPrefab();
            for (int i = 0; i < aiCount; i++)
            {
                int seat = seated + i;
                GameObject spawned = _session.Objects.Spawn(opponentPrefab, AiKeyPrefix + (i + 1), SpawnPoint(seat));
                Player ai = spawned.GetComponent<Player>();
                ai.AssignSeat(seat);
                ai.Place(SpawnPoint(seat));
                JoinSeat(ai, seat, true);
            }

            _turns = Turns.For(_session);
            if (_turns == null)
            {
                _turns = _session.Objects.Spawn(TurnsPrefab(), Turns.Key).GetComponent<Turns>();
            }

            _turns.Arena = this;
            _turns.MirrorMap(MapIndex);
            _announcer = new Announcer(_setup.AnnouncerVoice, _setup.Announcements, _announcerDice);
            _turns.MirrorVoice(_announcer.Voice);
            _authority.StartMatch();
            Mirror();
            Say(_announcer.Welcome()); // obj_game's create event: random_announcement_welcome
            return true;
        }

        // Seats a player through the authority (AssignSeat has registered them already); a join it refuses is said, not hidden.
        private void JoinSeat(Player player, int seat, bool isAi)
        {
            if (_authority.Apply(new Cmd.JoinSeatCommand(player.PlayerId, seat, isAi, player.DisplayName)).Count == 0)
            {
                Debug.LogWarning("[Ricochet] " + player.DisplayName + " was not seated in " + seat + ": the authority refused the join.");
            }
        }

        /// <summary>Server: may <paramref name="player"/> fire now? Always in practice; in a match, only on their turn.</summary>
        public bool MayFire(Player player)
        {
            if (_match == null || !_match.IsRunning)
            {
                return true;
            }

            return player != null && player.Seat >= 0 && _match.MayFire(player.Seat);
        }

        // What the authority published (UR-T34), in the order it happened. Core changed nothing here: this is Runtime
        // reacting to it, with objects, the announcer and the players' copies of what every peer reads.
        private void OnEvent(Cmd.GameEvent e)
        {
            Player.Present(_session, e); // the shot drawn and sent, and the result sent, by the player who fired it
            switch (e)
            {
                case Cmd.Hit hit when hit.IsDummy:
                    Knock(hit.TargetId);
                    break;
                case Cmd.ShotLanded landed:
                    Announce(landed);
                    break;
                case Cmd.CrateDropped dropped:
                    SpawnCrate(dropped.Crate);
                    break;
                case Cmd.CrateLanded landed:
                    LandCrate(landed.Crate);
                    break;
                case Cmd.CrateRemoved removed:
                    _session.Objects.Despawn(Crate.KeyFor(removed.Crate.Id));
                    break;
                case Cmd.LayoutChanged layout:
                    AdoptServerLayout(layout);
                    break;
                case Cmd.LevelRerolled _:
                    ResetDummies();
                    break;
                case Cmd.TurnStarted turn:
                    BeginAiTurn(turn.Seat);
                    break;
                case Cmd.GameOver _:
                    _aiTurn = null;
                    break;
                case Cmd.DriveStarted _:
                    Mirror(); // only the first drive of a turn changes what peers see
                    break;
                case Cmd.VotePassed passed when passed.Kind == _votingKind:
                    _votePassedInCore = true;
                    break;
            }
        }

        // The announcer cheers a knock-out (random_announcement_win) or heckles a shot that hit nobody
        // (random_announcement_lose, obj_explosion).
        private void Announce(Cmd.ShotLanded landed)
        {
            if (landed.Outcome == null || _announcer == null)
            {
                return;
            }

            if (landed.KnockedOut >= 0)
            {
                Say(_announcer.Win(landed.Seat, landed.KnockedOut));
            }
            else if (!landed.StruckAPlayer)
            {
                Say(_announcer.Lose(landed.Seat));
            }
        }

        // The server laid a level (a match's map, a new round, a reroll): this peer plays on it and every player's clients learn it.
        private void AdoptServerLayout(Cmd.LayoutChanged layout)
        {
            UseLayout(layout.Map, layout.Seed);
            if (!layout.Rolled)
            {
                return;
            }

            LevelVersion = layout.Version;
            foreach (Player player in Player.In(_session))
            {
                player.MirrorLevel(MapIndex, LevelSeed, LevelVersion);
            }
        }

        /// <summary>The crates on the level now (server only; empty outside a match).</summary>
        public IReadOnlyList<Drop> CratesOnField => _field != null ? _field.Drops : (IReadOnlyList<Drop>)Array.Empty<Drop>();

        /// <summary>Server: drops a crate of <paramref name="kind"/> at <paramref name="spot"/> now (a test, or a debug key; null outside a match).</summary>
        public Drop DropCrate(DropKind kind, Vector2 spot) => _authority.DropCrate(kind, ToCore(spot));

        private void SpawnCrate(Drop drop)
        {
            GameObject spawned = _session.Objects.Spawn(CratePrefab(), Crate.KeyFor(drop.Id), ToUnity(drop.Position));
            Crate crate = spawned.GetComponent<Crate>();
            crate.Kind = (int)drop.Kind;
            crate.Spot = ToUnity(drop.Spot);
            crate.Landed = drop.Landed;
        }

        private void LandCrate(Drop drop)
        {
            if (_session.Objects.TryGet(Crate.KeyFor(drop.Id), out Crate crate) && crate != null)
            {
                crate.Landed = true;
            }
        }

        /// <summary>
        /// Server, practice only (R, PageUp and PageDown in the original): rolls another random level, stands every player at
        /// a fresh spot and lays the practice dummies and hazards again. False during a match, whose rounds roll their own.
        /// Players ask for it with a command; this is for a test or a debug key.
        /// </summary>
        public bool RerollLevel() => _authority.Reroll().Count > 0;

        /// <summary>True while a match is being played (server only).</summary>
        public bool MatchRunning => _match != null && _match.IsRunning;

        /// <summary>
        /// Server: a player arrived while a match is running: they take the lowest free seat and play from the next
        /// round (a seventeenth watches). Before a match, Player seats itself by its connection's seat.
        /// </summary>
        public void Arrived(Player player)
        {
            if (!MatchRunning || player == null || player.IsAi)
            {
                return;
            }

            int seat = _match.FreeSeat();
            player.AssignSeat(seat);
            if (seat >= 0)
            {
                JoinSeat(player, seat, false);
                _humanSeats[player.KitSeat] = seat;
                player.Place(SpawnPoint(seat));
            }

            Mirror();
        }

        /// <summary>
        /// Server: a player's connection dropped and the kit holds their seat for the reconnect grace (UR-T13): their
        /// Player waits, Away (nobody plays it, not even the host it now belongs to), and in a match their seat keeps its
        /// health and score while the turn clock waits on their turn.
        /// </summary>
        public void SeatHeld(int kitSeat)
        {
            Player player = HumanAt(kitSeat);
            if (player != null)
            {
                player.Away = true;
            }

            if (player != null && _humanSeats.ContainsKey(kitSeat))
            {
                _authority.Apply(new Cmd.DropSeatCommand(player.PlayerId, HoldLeft(kitSeat)));
            }

            Mirror();
        }

        /// <summary>
        /// Server: a dropped player reconnected within the grace and the kit gave them their seat and Player back
        /// (UR-T13): in a match they resume where they were, on the same turn if it was theirs.
        /// </summary>
        public void SeatReclaimed(int kitSeat)
        {
            Player player = HumanAt(kitSeat);
            if (player != null)
            {
                player.Away = false;
            }

            if (player != null && _humanSeats.ContainsKey(kitSeat))
            {
                _authority.Apply(new Cmd.RejoinSeatCommand(player.PlayerId));
            }

            Mirror();
        }

        // The human Player of a kit seat (held ones included), or null.
        private Player HumanAt(int kitSeat)
        {
            foreach (Player player in Player.In(_session))
            {
                if (!player.IsAi && player.KitSeat == kitSeat)
                {
                    return player;
                }
            }

            return null;
        }

        // Seconds left before the kit gives up a held seat (the whole grace when it is not held yet).
        private double HoldLeft(int kitSeat)
        {
            foreach (Unity.Multiplayer.Core.Seat seat in _session.Seats)
            {
                if (seat.Index == kitSeat && seat.Status == SeatStatus.Held)
                {
                    return Math.Max(0, seat.HeldUntil - Time.realtimeSinceStartupAsDouble);
                }
            }

            NetConfig config = _session.Config;
            return config != null ? config.ReconnectGraceSeconds : NetConfig.DefaultReconnectGraceSeconds;
        }

        /// <summary>
        /// Server: the kit released a connection's seat (the player left, or did not come back in time): their match seat
        /// drops out, passing the turn if it was theirs. A dropped player whose seat is only held keeps it.
        /// </summary>
        public void SeatReleased(int kitSeat)
        {
            if (_match != null && _humanSeats.TryGetValue(kitSeat, out int seat))
            {
                _humanSeats.Remove(kitSeat);
                Player player = HumanAt(kitSeat); // already gone with the connection, perhaps: the command names the seat too
                _authority.Apply(new Cmd.LeaveSeatCommand(player != null ? player.PlayerId : -1, seat));
                Mirror();
            }
        }

        /// <summary>Server: runs the match clock (called by Turns each frame) and plays the AI seats' turns.</summary>
        public void Tick(float seconds)
        {
            if (_match == null)
            {
                return;
            }

            _authority.Tick(seconds);

            PlayAi(seconds);
            Mirror();
        }

        // obj_CheckAI: a new turn; an AI seat starts thinking, with one of check_ai's thirty lines.
        private void BeginAiTurn(int seat)
        {
            _aiTurn = _match.TryGetSeat(seat, out MatchSeat sitting) && sitting.Kind == SeatKind.Ai
                ? new AiTurn(seat, _aiDice.Next(Ai.ThinkingLineCount), MatchRules.AiThinkSeconds, MatchRules.AiDegreesPerSecond)
                : null;
        }

        // An AI seat's turn, a frame at a time: think, plan (and pick the weapon), turn the barrel, fire.
        private void PlayAi(float seconds)
        {
            if (_aiTurn == null || _match.Phase != MatchPhase.Aiming || _match.Current != _aiTurn.Seat)
            {
                return;
            }

            Player ai = PlayerAt(_aiTurn.Seat);
            if (ai == null || ai.ShotInFlight)
            {
                return;
            }

            switch (_aiTurn.Step(seconds, ai.Heading, _match.TimeLeft, out double next))
            {
                case AiStep.Plan:
                    AiPlan plan = Ai.Plan(ToCore(ai.Position), SeatTarget(_aiTurn.Seat), LevelFor(_session, _aiTurn.Seat), Targets(_session), _setup.AiSkill, _aiDice,
                        Rules, ArsenalRules, ai.Loadout);
                    _aiTurn.SetPlan(plan);
                    ai.ServerSelectWeapon(plan.Weapon);
                    break;
                case AiStep.Turn:
                    ai.ServerAim((float)next, (float)_aiTurn.Plan.Power);
                    break;
                case AiStep.Fire:
                    ai.ServerFire((float)_aiTurn.Plan.Heading, (float)_aiTurn.Plan.Power);
                    break;
            }
        }

        /// <summary>The player sitting in <paramref name="seat"/>, or null.</summary>
        public Player PlayerAt(int seat)
        {
            foreach (Player player in Player.In(_session))
            {
                if (player.Seat == seat)
                {
                    return player;
                }
            }

            return null;
        }

        /// <summary>The vote this game offers: restart the round (in a match) or stand every dummy back up.</summary>
        public VoteKind CreateRestartVote() => new VoteKind(RestartVote, VoteMenu.RestartTitle)
        {
            Description = _ => VoteMenu.RestartDescription(_match != null ? _match.Phase : MatchPhase.Practice),
            Passed = vote => RouteVote(vote, _ =>
            {
                if (_match != null && _match.IsRunning)
                {
                    StartMatch(_setup);
                }
                else
                {
                    ResetDummies();
                    ResetHazards();
                }
            }),
        };

        /// <summary>
        /// The kick vote (UR-T14): the kit's own (<see cref="VoteBoard.CreateKickKind"/>: "Kick Bob?", voted on by everyone
        /// but Bob, never the host or the starter's self), with a line saying what passing does here. Passing disconnects
        /// them; the kit frees their seat at once (no reconnect grace), so in a match <see cref="SeatReleased"/> drops their
        /// seat out of the round and passes the turn if it was theirs.
        /// </summary>
        public VoteKind CreateKickVote()
        {
            VoteKind kick = VoteBoard.CreateKickKind(() => _session, VoteMenu.KickedLine);
            kick.Description = _ => VoteMenu.KickDescription(MatchRunning);
            Action<Vote> disconnect = kick.Passed;
            kick.Passed = vote => RouteVote(vote, disconnect);
            return kick;
        }

        /// <summary>
        /// The vote that starts a match; its number is how many AI opponents to add and its text the rest of the setup
        /// (<see cref="MatchSetup.Format"/>: the AI skill and the aim assists each).
        /// </summary>
        public VoteKind CreateMatchVote() => new VoteKind(MatchVote, r => "Start a match with " + r.Number + " AI opponent" + (r.Number == 1 ? "?" : "s?"))
        {
            Description = r => MatchSetup.Parse(r.Number, r.Text).DescribeGame() + ". " + MatchSetup.Parse(r.Number, r.Text).Describe() + ". "
                + MatchSetup.Parse(r.Number, r.Text).DescribeAnnouncer() + ". Everyone takes a seat and plays in turn; the last one standing wins the round.",
            Check = r => HumanCount() + Math.Max(0, r.Number) < 2 ? "A match needs two players: add an AI opponent." : null,
            Passed = vote => RouteVote(vote, v => StartMatch(MatchSetup.Parse(v.Number, v.Text))),
        };

        // The kit counts the ballots (its overlay and quorum are its own); what they decided goes through Core (UR-T65): the
        // vote opens on the authority, every ballot is a VoteCommand from the player who cast it, and the authority's
        // VotePassed is what carries the effect out, and only it: a vote Core does not pass is not carried out. A voter with
        // no Player on this peer cannot cast in Core, so they are left out of its electorate and the threshold falls by their yes; with no voter Core knows at all, the kit's count stands.
        private string _votingKind;
        private bool _votePassedInCore;

        private void RouteVote(Vote vote, Action<Vote> effect)
        {
            string kind = vote.Kind.Id;

            // Only voters with a player here can cast in Core: they are its electorate, and the kit's quorum less the yes
            // votes it cannot mirror is its threshold (the kit has already counted those).
            int electorate = 0;
            foreach (ulong voter in vote.Voters)
            {
                electorate += PlayerOf(voter) != null ? 1 : 0;
            }

            int unmirroredYes = 0;
            foreach (ulong voter in vote.Yes)
            {
                unmirroredYes += PlayerOf(voter) == null ? 1 : 0;
            }

            if (electorate == 0)
            {
                effect?.Invoke(vote); // nobody here is a player Core knows (a bare fixture): the kit's count is all there is
                return;
            }

            int needed = Math.Max(1, Math.Min(electorate, vote.Quorum - unmirroredYes));
            _authority.OpenVote(kind, electorate, (double)needed / electorate);
            _votingKind = kind;
            _votePassedInCore = false;
            CastBallots(vote.Yes, kind, true);
            CastBallots(vote.No, kind, false);
            _votingKind = null;
            _authority.CloseVote(kind);
            if (!_votePassedInCore)
            {
                Debug.LogWarning("[Ricochet] The " + kind + " vote passed on the kit's board but Core did not pass it; nothing carried out.");
                return;
            }

            effect?.Invoke(vote);
        }

        private Player PlayerOf(ulong clientId)
        {
            foreach (Player player in Player.In(_session))
            {
                if (!player.IsAi && player.OwnerClientId == clientId)
                {
                    return player;
                }
            }

            return null;
        }

        private void CastBallots(IReadOnlyList<ulong> voters, string kind, bool yes)
        {
            foreach (ulong voter in voters)
            {
                Player player = PlayerOf(voter);
                if (player != null)
                {
                    _authority.Apply(new Cmd.VoteCommand(player.PlayerId, kind, yes));
                }
            }
        }

        private int HumanCount()
        {
            int count = 0;
            foreach (Player player in Player.In(_session))
            {
                count += player.IsAi ? 0 : 1;
            }

            return count;
        }

        // Copies the authority's state out to every peer: each player's copy of what the authority holds (aim, tank,
        // loadout, shot) first, then the match's turn state and each seat's health.
        internal void Mirror()
        {
            foreach (Player each in Player.In(_session))
            {
                each.SyncFromAuthority();
            }

            if (_match == null)
            {
                return;
            }

            if (_turns != null && _turns.IsSpawned)
            {
                _turns.Mirror(_match);
                _turns.MirrorAi(_aiTurn != null && _aiTurn.Seat == _match.Current && _match.Phase == MatchPhase.Aiming ? _aiTurn.Message : -1);
                _turns.MirrorWait(_match.WaitingForCurrent ? (int)Math.Ceiling(_match.Seat(_match.Current).AwayLeft) : -1);
            }

            foreach (Player player in Player.In(_session))
            {
                if (player.Seat >= 0 && _match.TryGetSeat(player.Seat, out MatchSeat seat))
                {
                    player.Health = seat.Health;
                    player.InRound = seat.Alive;
                    player.Score = seat.Score;
                }
                else
                {
                    player.InRound = false;
                }
            }
        }

        // The announcer says a line (when it spoke at all): every peer's HUD shows it through Turns.
        private void Say(Announcement? announcement)
        {
            if (announcement != null && _turns != null && _turns.IsSpawned)
            {
                _turns.Announce(announcement.Value);
            }
        }

        private void RemoveDummies()
        {
            for (int i = 0; i < DummyCount; i++)
            {
                _session.Objects.Despawn(DummyKey(i));
            }

            _dummiesUp = false;
        }

        private void RemoveAi()
        {
            for (int i = 1; i <= MatchRules.MaxSeats; i++)
            {
                _session.Objects.Despawn(AiKeyPrefix + i);
            }
        }

        private static GameObject OpponentPrefab() => LoadPrefab("Game/Opponent");

        private static GameObject TurnsPrefab() => LoadPrefab("Game/Turns");

        private static GameObject CratePrefab() => LoadPrefab("Game/Crate");

        private static GameObject HazardPrefab() => LoadPrefab("Game/Hazard");

        private static GameObject LoadPrefab(string path)
        {
            GameObject prefab = Resources.Load<GameObject>(path);
            if (prefab == null)
            {
                throw new InvalidOperationException("Resources/" + path + ".prefab is missing.");
            }

            return prefab;
        }
    }
}
