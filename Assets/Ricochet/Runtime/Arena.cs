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
// and breaches the border.
using System;
using System.Collections.Generic;
using Ricochet.Core;         // Level, ShotRules, Vec2, Target, Match, Seating, Ai, AiTurn, MatchSetup, HazardField
using Unity.Multiplayer;      // NetSession, NetKey
using Unity.Multiplayer.Core; // VoteKind
using UnityEngine;

namespace Ricochet
{
    public sealed class Arena
    {
        public const int DummyCount = 3;
        public const string DummyKeyPrefix = "dummy:";
        public const string SeatKeyPrefix = "seat:";
        public const string AiKeyPrefix = "ai:";
        public const string RestartVote = "restart";
        public const string MatchVote = "match";

        /// <summary>A dummy's hit radius (its square is drawn 0.8 wide).</summary>
        public const float DummyRadius = 0.45f;

        /// <summary>The level every peer plays in.</summary>
        public static readonly Level Level = Level.Default();

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

        private static readonly Vector2[] DummyStarts = { new Vector2(-7f, 1f), new Vector2(7f, -3f), new Vector2(0f, 3.5f) };

        private readonly NetSession _session;
        private readonly GameObject _dummyPrefab;
        private readonly System.Random _aiDice = new System.Random();
        private readonly System.Random _dropDice = new System.Random();
        private readonly HazardField _hazards;
        private Match _match;
        private DropField _field;
        private Turns _turns;
        private readonly Dictionary<int, int> _humanSeats = new Dictionary<int, int>(); // kit seat -> match seat
        private MatchSetup _setup = new MatchSetup();
        private AiTurn _aiTurn;
        private bool _dummiesUp;

        public Arena(NetSession session, GameObject dummyPrefab)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _dummyPrefab = dummyPrefab ?? throw new ArgumentNullException(nameof(dummyPrefab));
            _hazards = new HazardField(Level, HazardRules, new System.Random());
            _hazards.Added += SpawnHazard;
            _hazards.Changed += MirrorHazard;
            _hazards.Removed += hazard => _session.Objects.Despawn(LevelHazard.KeyFor(hazard.Id));
        }

        /// <summary>
        /// True when each round (and practice) is laid with the rules' electric arcs, laser gates and foliage. The game's
        /// scene turns it on; off, the only hazards are the ones players make (shield walls, redirectors, breaches).
        /// </summary>
        public bool LaysHazards { get; set; }

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
                if (session.IsServer)
                {
                    Current = arena;
                    arena.SpawnDummies();
                    arena.ResetHazards();
                }
            };
            session.SeatReleased += seat => arena.SeatReleased(seat.Index);
            session.Ended += _ =>
            {
                if (Current == arena)
                {
                    Current = null;
                }
            };
            if (session.IsServer && session.IsLive)
            {
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

        /// <summary>Where a seat's player stands (Core's sixteen spots; the first five are along the bottom).</summary>
        public static Vector2 SpawnPoint(int seat) => ToUnity(Seating.SpawnPoint(Math.Max(0, seat)));

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
                return current._hazards.LevelFor(shooter);
            }

            return session != null ? HazardField.Build(Level, LevelHazard.Collect(session), shooter) : Level;
        }

        /// <summary>
        /// Server: every hazard goes, and the level is laid again when <see cref="LaysHazards"/> (the arcs and gates, and
        /// create_random_foliage's tufts kept clear of every seat's spot, every player and the practice dummies).
        /// </summary>
        public void ResetHazards()
        {
            _hazards.Clear();
            if (!LaysHazards)
            {
                return;
            }

            var keepClear = new List<Vec2>();
            for (int i = 0; i < MatchRules.MaxSeats; i++)
            {
                keepClear.Add(Seating.SpawnPoint(i));
            }

            for (int i = 0; i < DummyCount; i++)
            {
                keepClear.Add(ToCore(DummyStart(i)));
            }

            foreach (Player player in Player.In(_session))
            {
                keepClear.Add(ToCore(player.Position));
            }

            _hazards.LayOut(keepClear);
        }

        /// <summary>Server: spawn_shieldwall.gml: <paramref name="player"/>'s shield wall, across their aim, in front of them.</summary>
        public Hazard PlaceShieldWall(Player player)
        {
            return player == null ? null : _hazards.AddShieldWall(player.Seat, ToCore(player.Position), player.Heading);
        }

        /// <summary>Server: the redirector item set down at <paramref name="spot"/>, sending shots along <paramref name="heading"/>.</summary>
        public Hazard PlaceRedirector(Player player, Vec2 spot, double heading)
        {
            return player == null ? null : _hazards.AddRedirector(player.Seat, spot, heading);
        }

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

        // A landed volley's mark on the level: every shield wall or tuft it ran into goes (obj_shieldwall's collision
        // events), and a vortex that went off swallows what it reaches and breaches the border near it (vortex_step's pull).
        private void Scar(Player player, Volley volley)
        {
            int shooter = player != null ? player.Seat : -1;
            foreach (int id in volley.HazardsHit)
            {
                _hazards.Struck(id, shooter);
            }

            if (volley.Weapon == Weapon.Vortex && volley.HasBlast)
            {
                _hazards.Implode(volley.BlastCenter, volley.BlastRadius);
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
            int seated = Math.Min(humans.Count, MatchRules.MaxSeats);
            aiCount = Math.Max(0, Math.Min(aiCount, MatchRules.MaxSeats - seated));
            if (seated + aiCount < 2)
            {
                return false;
            }

            RemoveAi();
            if (_dummiesUp)
            {
                RemoveDummies();
            }

            setup.AiCount = aiCount;
            _setup = setup;
            _setup.ApplyTo(ArsenalRules);
            _aiTurn = null;
            _field?.Clear();
            _field = new DropField(Level, DropRules, _dropDice);
            _field.Spawned += SpawnCrate;
            _field.Landed += LandCrate;
            _field.Removed += drop => _session.Objects.Despawn(Crate.KeyFor(drop.Id));
            _match = new Match(MatchRules);
            _match.RoundStarted += _ => NewRound();
            _match.TurnStarted += seat =>
            {
                ClearExtraBounces();
                _hazards.NextTurn(); // the laser gates switch
                BeginAiTurn(seat);
            };
            _humanSeats.Clear();
            for (int i = 0; i < humans.Count; i++)
            {
                Player human = humans[i];
                int seat = i < seated ? i : -1; // a seventeenth human watches
                human.Seat = seat;
                if (seat >= 0)
                {
                    _match.Join(seat, SeatKind.Human, human.DisplayName);
                    _humanSeats[human.KitSeat] = seat;
                    human.Position = SpawnPoint(seat);
                }
            }

            GameObject opponentPrefab = OpponentPrefab();
            for (int i = 0; i < aiCount; i++)
            {
                int seat = seated + i;
                GameObject spawned = _session.Objects.Spawn(opponentPrefab, AiKeyPrefix + (i + 1), SpawnPoint(seat));
                Player ai = spawned.GetComponent<Player>();
                ai.Seat = seat;
                ai.Position = SpawnPoint(seat);
                _match.Join(seat, SeatKind.Ai, ai.DisplayName);
            }

            _turns = Turns.For(_session);
            if (_turns == null)
            {
                _turns = _session.Objects.Spawn(TurnsPrefab(), Turns.Key).GetComponent<Turns>();
            }

            _turns.Arena = this;
            _match.Start();
            Mirror();
            return true;
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

        /// <summary>Server: <paramref name="player"/> fired (the turn clock stops until it lands).</summary>
        public void Fired(Player player)
        {
            if (_match != null && _match.IsRunning && player != null)
            {
                _match.Fire(player.Seat);
                Mirror();
            }
        }

        /// <summary>
        /// Server: <paramref name="player"/>'s volley landed: knock down every dummy it struck; in a match, hurt every
        /// player it struck (hit.gml per strike), give the shooter every crate it passed over, and end the turn.
        /// </summary>
        public void Landed(Player player, Volley volley)
        {
            if (volley == null)
            {
                return;
            }

            Scar(player, volley);
            foreach (Strike strike in volley.Strikes)
            {
                if (strike.TargetId != null && strike.TargetId.StartsWith(DummyKeyPrefix, StringComparison.Ordinal))
                {
                    Knock(strike.TargetId);
                }
            }

            if (_match != null && _match.IsRunning && player != null)
            {
                foreach (Strike strike in volley.Strikes)
                {
                    int hit = SeatOf(strike.TargetId);
                    if (hit >= 0)
                    {
                        _match.Strike(player.Seat, hit, strike.Bounces, strike.Scale);
                    }
                }

                if (_field != null)
                {
                    foreach (Ricochet.Core.Drop drop in _field.Collect(volley))
                    {
                        Give(player, drop.Kind);
                    }
                }

                _match.ShotOver(player.Seat);
                Mirror();
            }
        }

        /// <summary>The crates on the level now (server only; empty outside a match).</summary>
        public IReadOnlyList<Ricochet.Core.Drop> CratesOnField => _field != null ? _field.Drops : (IReadOnlyList<Ricochet.Core.Drop>)Array.Empty<Ricochet.Core.Drop>();

        /// <summary>Server: drops a crate of <paramref name="kind"/> at <paramref name="spot"/> now (a test, or a debug key; null outside a match).</summary>
        public Ricochet.Core.Drop DropCrate(DropKind kind, Vector2 spot)
        {
            return MatchRunning && _field != null ? _field.Spawn(kind, ToCore(spot)) : null;
        }

        // add_random_weapon / add_random_item / add_random_health.gml: what a collected crate gives its collector.
        private void Give(Player player, DropKind kind)
        {
            int value = 0;
            switch (kind)
            {
                case DropKind.Weapon:
                case DropKind.Powerup:
                    Weapon? weapon = Pickups.RandomWeapon(_dropDice, ArsenalRules);
                    if (weapon == null)
                    {
                        return;
                    }

                    player.Loadout.Add(weapon.Value);
                    value = (int)weapon.Value;
                    break;
                case DropKind.Item:
                    Item? item = Pickups.RandomItem(_dropDice, ArsenalRules);
                    if (item == null)
                    {
                        return;
                    }

                    player.Loadout.Add(item.Value);
                    value = (int)item.Value;
                    break;
                case DropKind.Health:
                    value = _match.Heal(player.Seat, Pickups.HealAmount(_dropDice, MatchRules.WeaponDamage));
                    break;
            }

            player.MirrorLoadout();
            player.Collected(kind, value);
        }

        private void SpawnCrate(Ricochet.Core.Drop drop)
        {
            GameObject spawned = _session.Objects.Spawn(CratePrefab(), Crate.KeyFor(drop.Id), ToUnity(drop.Position));
            Crate crate = spawned.GetComponent<Crate>();
            crate.Kind = (int)drop.Kind;
            crate.Spot = ToUnity(drop.Spot);
            crate.Landed = drop.Landed;
        }

        private void LandCrate(Ricochet.Core.Drop drop)
        {
            if (_session.Objects.TryGet(Crate.KeyFor(drop.Id), out Crate crate) && crate != null)
            {
                crate.Landed = true;
            }
        }

        // A new round: everyone back to the starting loadout and full size, and the level cleared of crates.
        private void NewRound()
        {
            _field?.Clear();
            ResetHazards();
            foreach (Player player in Player.In(_session))
            {
                player.ResetForRound();
            }
        }

        private void ClearExtraBounces()
        {
            foreach (Player player in Player.In(_session))
            {
                if (player.ExtraBounces != 0)
                {
                    player.ExtraBounces = 0;
                }
            }
        }

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
            player.Seat = seat;
            if (seat >= 0)
            {
                _match.Join(seat, SeatKind.Human, player.DisplayName);
                _humanSeats[player.KitSeat] = seat;
                player.Position = SpawnPoint(seat);
            }

            Mirror();
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
                _match.Leave(seat);
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

            _match.Tick(seconds);

            // obj_drop_emitter: crates fall while a round is being played; none outlive the match.
            if (_field != null)
            {
                if (_match.IsRunning && _match.Phase != MatchPhase.RoundOver)
                {
                    _field.Tick(seconds);
                    _hazards.Tick(seconds); // shield walls decay
                }
                else if (!_match.IsRunning && _field.Drops.Count > 0)
                {
                    _field.Clear();
                }
            }

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
        public VoteKind CreateRestartVote() => new VoteKind(RestartVote, "Restart the round?")
        {
            Description = _ => _match != null && _match.IsRunning ? "Everyone back to full health; the match starts over." : "Every practice dummy stands back up and the level is laid again.",
            Passed = _ =>
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
            },
        };

        /// <summary>
        /// The vote that starts a match; its number is how many AI opponents to add and its text the rest of the setup
        /// (<see cref="MatchSetup.Format"/>: the AI skill and the aim assists each).
        /// </summary>
        public VoteKind CreateMatchVote() => new VoteKind(MatchVote, r => "Start a match with " + r.Number + " AI opponent" + (r.Number == 1 ? "?" : "s?"))
        {
            Description = r => MatchSetup.Parse(r.Number, r.Text).Describe() + ". Everyone takes a seat and plays in turn; the last one standing wins the round.",
            Check = r => HumanCount() + Math.Max(0, r.Number) < 2 ? "A match needs two players: add an AI opponent." : null,
            Passed = v => StartMatch(MatchSetup.Parse(v.Number, v.Text)),
        };

        private int HumanCount()
        {
            int count = 0;
            foreach (Player player in Player.In(_session))
            {
                count += player.IsAi ? 0 : 1;
            }

            return count;
        }

        private void Mirror()
        {
            if (_match == null)
            {
                return;
            }

            if (_turns != null && _turns.IsSpawned)
            {
                _turns.Mirror(_match);
                _turns.MirrorAi(_aiTurn != null && _aiTurn.Seat == _match.Current && _match.Phase == MatchPhase.Aiming ? _aiTurn.Message : -1);
            }

            foreach (Player player in Player.In(_session))
            {
                if (player.Seat >= 0 && _match.TryGetSeat(player.Seat, out MatchSeat seat))
                {
                    player.Health = seat.Health;
                    player.InRound = seat.Alive;
                }
                else
                {
                    player.InRound = false;
                }
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
