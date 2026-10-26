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
    public sealed partial class Arena
    {
        public const int DummyCount = 3;
        public const string DummyKeyPrefix = Cmd.TargetIds.DummyPrefix;
        public const string SeatKeyPrefix = Cmd.TargetIds.SeatPrefix;
        public const string AiKeyPrefix = "ai:";
        public const string RestartVote = "restart";
        public const string MatchVote = "match";

        /// <summary>A dummy's hit radius (its square is drawn 0.8 wide).</summary>
        public const float DummyRadius = 0.45f;

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
