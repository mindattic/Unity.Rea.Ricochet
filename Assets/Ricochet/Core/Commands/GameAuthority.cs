// The single authority (UR-T34): the one place that changes the game. The runtime used to reach into Core's Match, Loadout,
// Volley, DropField and HazardField itself (Player and Arena called methods that changed state and read it back); now a
// player's input is a Command carrying their id, GameAuthority checks it against the rules, applies it, and answers with
// the ordered list of events it caused (GameEvents.cs). Presentation (the shot view, the HUD, the announcer, the audio, the
// crate and hazard objects) subscribes to those events and never changes the game. In an online game the server runs the
// one authority, the [ToServer] calls on Player become commands, and nothing else is trusted; local play goes through the
// same path. There is no global current player: every command says who gave it.
// It holds the rules' state: the players (PlayerState: aim, loadout, tank, shot in the air), the Match, the crates, the
// hazards and the level in play. Time comes in as Tick (the match clock, crates, hazards) and AdvanceFlight / AdvanceDrive
// (one player's shot or tank, a frame at a time); a shot that lands is struck, scored and collected here (Land).
using System;
using System.Collections.Generic;

namespace Ricochet.Core.Commands
{
    using Ricochet.Core.Ai;
    using Ricochet.Core.Aim;
    using Ricochet.Core.Arsenal;
    using Ricochet.Core.Level;
    using Ricochet.Core.Match;
    using Ricochet.Core.Net;

    /// <summary>What the authority cannot know on its own: where the practice dummies stand.</summary>
    public interface IGameWorld
    {
        /// <summary>A target for every practice dummy still standing.</summary>
        IReadOnlyList<Target> DummyTargets();
    }

    public sealed class GameAuthority
    {
        /// <summary>How long (seconds) past the last drive command the tank keeps moving.</summary>
        public const double DriveHold = 0.25;

        private readonly IGameWorld _world;
        private readonly Dictionary<int, PlayerState> _players = new Dictionary<int, PlayerState>();
        private readonly List<PlayerState> _order = new List<PlayerState>();
        private readonly Random _dropDice;
        private readonly Random _layoutDice;
        private readonly Random _matchDice;
        private readonly Dictionary<string, VoteTally> _votes = new Dictionary<string, VoteTally>();
        private List<GameEvent> _current;
        private int _matchMap;

        public GameAuthority(IGameWorld world = null, ShotRules rules = null, MatchRules matchRules = null, ArsenalRules arms = null,
            DropRules drops = null, HazardRules hazards = null, int seed = 0)
        {
            _world = world;
            Rules = rules ?? ShotRules.Default;
            MatchRules = matchRules ?? MatchRules.Default;
            Arms = arms ?? ArsenalRules.Default;
            DropRules = drops ?? DropRules.Default;
            _dropDice = seed == 0 ? new Random() : new Random(seed);
            _layoutDice = seed == 0 ? new Random() : new Random(seed + 1);
            _matchDice = seed == 0 ? new Random() : new Random(seed + 2);
            Layout = Maps.At(0).Layout(0);
            Hazards = new HazardField(Layout.Level, hazards ?? HazardRules.Default, seed == 0 ? new Random() : new Random(seed + 3));
        }

        /// <summary>
        /// Raised for every event the moment it happens (presentation subscribes here); a call's own events are also
        /// returned from it, in the same order.
        /// </summary>
        public event Action<GameEvent> Published;

        public ShotRules Rules { get; }

        public MatchRules MatchRules { get; }

        /// <summary>The weapons and items in play.</summary>
        public ArsenalRules Arms { get; }

        public DropRules DropRules { get; }

        /// <summary>The match being played (null before the first one starts: practice).</summary>
        public Match Match { get; private set; }

        /// <summary>The crates on the level (null outside a match).</summary>
        public DropField Crates { get; private set; }

        /// <summary>The hazards on the level now.</summary>
        public HazardField Hazards { get; }

        /// <summary>The round being played: the map's level and where each seat stands.</summary>
        public RoundLayout Layout { get; private set; }

        /// <summary>The map <see cref="Layout"/> was built from (an index into Maps.All).</summary>
        public int MapIndex { get; private set; }

        /// <summary>The seed a random round was built from (0 on a hand-laid map).</summary>
        public int LayoutSeed { get; private set; }

        /// <summary>Counts the level rolls, so a peer adopts the newest and never an older one.</summary>
        public int LayoutVersion { get; private set; }

        /// <summary>True when each round (and practice) is laid with the rules' electric arcs, laser gates and foliage.</summary>
        public bool LaysHazards { get; set; }

        /// <summary>True while a match is being played.</summary>
        public bool MatchRunning => Match != null && Match.IsRunning;

        /// <summary>Every player, in the order they were added.</summary>
        public IReadOnlyList<PlayerState> Players => _order;

        public bool TryGetPlayer(int id, out PlayerState state) => _players.TryGetValue(id, out state);

        public PlayerState Player(int id) => _players.TryGetValue(id, out PlayerState state) ? state : null;

        /// <summary>The first player sitting in <paramref name="seat"/>, or null.</summary>
        public PlayerState PlayerAtSeat(int seat)
        {
            if (seat < 0)
            {
                return null;
            }

            foreach (PlayerState state in _order)
            {
                if (state.Seat == seat)
                {
                    return state;
                }
            }

            return null;
        }

        // ------------------------------------------------------------------ players

        /// <summary>
        /// Adds a player the authority will take commands from, standing as <paramref name="from"/> says (a fresh player when
        /// null). A player already added is returned as it is.
        /// </summary>
        public PlayerState AddPlayer(int id, bool isAi = false, PlayerSnapshot from = null)
        {
            if (_players.TryGetValue(id, out PlayerState existing))
            {
                return existing;
            }

            var state = new PlayerState(id, isAi, Arms);
            if (from != null)
            {
                state.Restore(from);
            }

            _players.Add(id, state);
            _order.Add(state);
            return state;
        }

        /// <summary>A player left (their shot goes with them, and they are no longer a target).</summary>
        public void RemovePlayer(int id)
        {
            if (_players.TryGetValue(id, out PlayerState state))
            {
                _players.Remove(id);
                _order.Remove(state);
            }
        }

        /// <summary>Seats a player (-1 for none). The match seat's own bookkeeping is Match's; this is where the player sits.</summary>
        public void SetSeat(int id, int seat)
        {
            if (_players.TryGetValue(id, out PlayerState state))
            {
                state.Seat = seat;
            }
        }

        /// <summary>Stands a player's tank at <paramref name="spot"/> (a spawn, a new round).</summary>
        public IReadOnlyList<GameEvent> Place(int id, Vec2 spot)
        {
            return Run(() =>
            {
                if (_players.TryGetValue(id, out PlayerState state))
                {
                    MoveTo(state, spot, MoveCause.Placed);
                }
            });
        }

        /// <summary>The hit radius a seat's player has now (the full radius for an empty seat).</summary>
        public double RadiusOf(int seat)
        {
            PlayerState state = PlayerAtSeat(seat);
            return state != null ? state.Radius : Seating.PlayerRadius;
        }

        // ------------------------------------------------------------------ the world a shot flies in

        /// <summary>
        /// What a shot fired now can hit: every standing dummy and, during a match, every player still in the round (the
        /// shooter too: a shot that banks back hurts, as hit.gml's self-hit did).
        /// </summary>
        public List<Target> Targets()
        {
            var targets = new List<Target>();
            if (_world != null)
            {
                IReadOnlyList<Target> dummies = _world.DummyTargets();
                if (dummies != null)
                {
                    targets.AddRange(dummies);
                }
            }

            if (Match != null)
            {
                foreach (PlayerState state in _order)
                {
                    if (state.Seat >= 0 && Match.TryGetSeat(state.Seat, out MatchSeat seat) && seat.Alive)
                    {
                        targets.Add(new Target(TargetIds.Seat(state.Seat), state.Position, state.Radius));
                    }
                }
            }

            return targets;
        }

        /// <summary>The level <paramref name="seat"/>'s shot sees now: the hazards that stop it (not their own shield walls) on the level.</summary>
        public Level LevelFor(int seat) => Hazards.LevelFor(seat);

        /// <summary>Solves what <paramref name="weapon"/> would do from the player's spot, as things stand (a preview; nothing changes).</summary>
        public Volley Solve(PlayerState state, Weapon weapon, double heading, double power, double fuse, int extraBounces)
        {
            return Arsenal.Fire(weapon, state.Position, heading, power, fuse, extraBounces, LevelFor(state.Seat), Targets(), Rules, Arms);
        }

        // ------------------------------------------------------------------ the match

        /// <summary>
        /// Opens a match on map <paramref name="mapIndex"/> to <paramref name="goal"/> points (its seats are joined to
        /// <see cref="Match"/> before <see cref="StartMatch"/>): the crates and hazards start afresh on the map's level.
        /// </summary>
        public Match BeginMatch(int mapIndex, int goal)
        {
            Run(() =>
            {
                _matchMap = Maps.Clamp(mapIndex);
                UseMap(_matchMap);
                Hazards.UseLevel(Layout.Level);
                NewField();
            });

            var match = new Match(MatchRules, _matchDice.Next(1, int.MaxValue)) { Goal = goal };
            match.RoundStarted += _ => NewRound();
            match.GameEnded += champion =>
            {
                Crates?.Clear();
                Emit(new GameOver(champion));
            };
            match.TurnStarted += seat =>
            {
                ClearExtraBounces();
                Hazards.NextTurn(); // the laser gates switch
                Emit(new TurnStarted(seat));
            };
            match.Countdown += secondsLeft => Emit(new Countdown(secondsLeft));
            match.TimedOut += seat => Emit(new TurnPassed(seat, TurnPassReason.TimedOut));
            match.Damaged += (seat, damage, by) => Emit(new Damaged(seat, damage, by));
            match.Scored += (seat, points) => Emit(new Scored(seat, points));
            match.Eliminated += seat => Emit(new Eliminated(seat));
            match.RoundEnded += winner => Emit(new RoundOver(winner));
            Match = match;
            return match;
        }

        /// <summary>Starts round one: the lowest seat goes first.</summary>
        public IReadOnlyList<GameEvent> StartMatch()
        {
            return Run(() => Match.Start());
        }

        /// <summary>Runs the match clock, the falling crates and the hazards' decay.</summary>
        public IReadOnlyList<GameEvent> Tick(double seconds)
        {
            return Run(() =>
            {
                if (Match == null)
                {
                    return;
                }

                Match.Tick(seconds);

                // obj_drop_emitter: crates fall while a round is being played; none outlive the match.
                if (Crates != null)
                {
                    if (Match.IsRunning && Match.Phase != MatchPhase.RoundOver && Match.Phase != MatchPhase.GameOver)
                    {
                        Crates.Tick(seconds);
                        Hazards.Tick(seconds); // shield walls decay
                    }
                    else if ((!Match.IsRunning || Match.Phase == MatchPhase.GameOver) && Crates.Drops.Count > 0)
                    {
                        Crates.Clear();
                    }
                }
            });
        }

        /// <summary>A crate of <paramref name="kind"/> at <paramref name="spot"/> now (a test, or a debug key; null outside a match).</summary>
        public Drop DropCrate(DropKind kind, Vec2 spot)
        {
            Drop drop = null;
            Run(() =>
            {
                if (MatchRunning && Crates != null)
                {
                    drop = Crates.Spawn(kind, spot);
                }
            });
            return drop;
        }

        /// <summary>Every hazard goes, and the level is laid again when <see cref="LaysHazards"/> (arcs, gates and foliage kept clear of every seat, player and dummy).</summary>
        public void ResetHazards()
        {
            Hazards.Clear();
            if (!LaysHazards)
            {
                return;
            }

            var keepClear = new List<Vec2>();
            for (int i = 0; i < MatchRules.MaxSeats; i++)
            {
                keepClear.Add(Layout.SpawnPoint(i));
            }

            foreach (Vec2 start in Seating.DummyStarts)
            {
                keepClear.Add(start);
            }

            foreach (PlayerState state in _order)
            {
                keepClear.Add(state.Position);
            }

            Hazards.LayOut(keepClear);
        }

        /// <summary>
        /// Practice only (R, PageUp and PageDown in the original): rolls another random level and stands every player at a
        /// fresh spot. Empty during a match, whose rounds roll their own.
        /// </summary>
        public IReadOnlyList<GameEvent> Reroll(int playerId = -1)
        {
            return Run(() =>
            {
                if (Match != null && Match.Phase != MatchPhase.Practice)
                {
                    return; // a match, or its recap, keeps its level
                }

                RollLayout(Maps.RandomIndex);
                PlaceEveryone();
                ResetHazards();
                Emit(new LevelRerolled(playerId, MapIndex, LayoutSeed, LayoutVersion));
            });
        }

        // ------------------------------------------------------------------ votes

        /// <summary>
        /// Opens a vote on <paramref name="kind"/> among <paramref name="electorate"/> voters: it passes once
        /// <paramref name="fraction"/> of them say yes (two thirds by default, as the kick vote does) and fails once that can
        /// no longer happen. Opening it again starts it over.
        /// </summary>
        public void OpenVote(string kind, int electorate, double fraction = 2.0 / 3.0)
        {
            _votes[kind ?? string.Empty] = new VoteTally(Math.Max(1, electorate), fraction);
        }

        /// <summary>Calls off a vote of this kind that was not decided (a no-op when none is open).</summary>
        public void CloseVote(string kind) => _votes.Remove(kind ?? string.Empty);

        /// <summary>True while a vote of this kind is open.</summary>
        public bool VoteOpen(string kind) => _votes.ContainsKey(kind ?? string.Empty);

        // ------------------------------------------------------------------ commands

        /// <summary>
        /// Applies a player's command and returns the events it caused, in order. A command the rules do not allow now (an
        /// unknown player, out of turn, a shot already in the air) changes nothing and returns no events.
        /// </summary>
        public IReadOnlyList<GameEvent> Apply(Command command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            return Run(() =>
            {
                if (!_players.TryGetValue(command.PlayerId, out PlayerState state))
                {
                    if (command is LeaveSeatCommand gone)
                    {
                        DoLeaveSeat(null, gone); // the player was removed with their connection: the command still names the seat
                    }

                    return;
                }

                switch (command)
                {
                    case AimCommand aim:
                        state.Heading = Quantize(Angles.Normalize(Finite(aim.Heading) ? aim.Heading : state.Heading));
                        Emit(new Aimed(state.Id, state.Heading, state.Power));
                        break;
                    case SetPowerCommand power:
                        state.Power = Quantize(ShotRules.ClampPower(power.Power));
                        Emit(new Aimed(state.Id, state.Heading, state.Power));
                        break;
                    case FireCommand fire:
                        DoFire(state, fire);
                        break;
                    case CycleWeaponCommand cycle:
                        if (MayAct(state) && state.Loadout.CycleWeapon(Math.Sign(cycle.Direction)))
                        {
                            Emit(new LoadoutChanged(state.Id));
                        }

                        break;
                    case CycleItemCommand cycle:
                        if (MayAct(state) && state.Loadout.CycleItem(Math.Sign(cycle.Direction)))
                        {
                            Emit(new LoadoutChanged(state.Id));
                        }

                        break;
                    case FuseCommand fuse:
                        DoFuse(state, fuse.Change);
                        break;
                    case UseItemCommand use:
                        DoUseItem(state, use);
                        break;
                    case DetonateCommand _:
                        DoDetonate(state);
                        break;
                    case DriveCommand drive:
                        DoDrive(state, drive.Direction);
                        break;
                    case EndTurnCommand _:
                        DoEndTurn(state);
                        break;
                    case RerollCommand _:
                        Reroll(state.Id);
                        break;
                    case VoteCommand vote:
                        DoVote(state, vote);
                        break;
                    case JoinSeatCommand join:
                        DoJoinSeat(state, join);
                        break;
                    case DropSeatCommand drop:
                        DoDropSeat(state, drop);
                        break;
                    case RejoinSeatCommand _:
                        DoRejoinSeat(state);
                        break;
                    case LeaveSeatCommand leave:
                        DoLeaveSeat(state, leave);
                        break;
                }
            });
        }

        /// <summary>
        /// Moves one player's shot on by <paramref name="seconds"/>: a bounce is an event, and the frame it lands it is
        /// struck, scored and collected (<see cref="Land"/>).
        /// </summary>
        public IReadOnlyList<GameEvent> AdvanceFlight(int playerId, double seconds)
        {
            return Run(() =>
            {
                if (!_players.TryGetValue(playerId, out PlayerState state) || state.Flight == null || state.Flight.Landed)
                {
                    return;
                }

                ProjectileFlight main = state.Flight.Flights[0];
                int before = main.Contacts;
                state.Flight.Advance(seconds);
                for (int i = before; i < main.Contacts && i < main.Path.Contacts.Count; i++)
                {
                    Emit(new Bounced(state.Id, state.Shots, i + 1, main.Path.Contacts[i].Point));
                }

                if (state.Flight.Landed)
                {
                    Land(state, state.Flight.Volley);
                }
            });
        }

        /// <summary>
        /// Drives one player's tank a frame along its barrel while a drive command is held (UR-T24), telling the match once
        /// (the first drive of a turn forfeits its shot).
        /// </summary>
        public IReadOnlyList<GameEvent> AdvanceDrive(int playerId, double seconds)
        {
            return Run(() =>
            {
                if (!_players.TryGetValue(playerId, out PlayerState state) || state.DriveDirection == 0)
                {
                    return;
                }

                if (state.DriveLeft <= 0 || !MayAct(state))
                {
                    state.DriveDirection = 0;
                    return;
                }

                if (MatchRunning)
                {
                    bool first = !Match.HasMoved;
                    if (!Match.Move(state.Seat))
                    {
                        state.DriveDirection = 0;
                        return;
                    }

                    if (first)
                    {
                        Emit(new DriveStarted(state.Id, state.Seat));
                    }
                }

                state.DriveLeft -= seconds;
                Vec2 to = Driving.Step(state.Position, state.Heading, state.DriveDirection, seconds, LevelFor(state.Seat), state.Radius, Rules);
                if (to != state.Position)
                {
                    MoveTo(state, to, MoveCause.Drove);
                }
            });
        }

        /// <summary>
        /// A volley has landed (<see cref="AdvanceFlight"/> calls this itself): it scars the level, knocks down the dummies
        /// it struck, and in a match hurts and scores for every player it struck, gives its shooter every crate it passed
        /// over and ends the turn.
        /// </summary>
        public IReadOnlyList<GameEvent> Land(int playerId, Volley volley)
        {
            return Run(() =>
            {
                if (_players.TryGetValue(playerId, out PlayerState state))
                {
                    Land(state, volley);
                }
            });
        }

        // ------------------------------------------------------------------ the rules of each command

        private void DoFire(PlayerState state, FireCommand fire)
        {
            if (!MayAct(state) || !MayShoot(state))
            {
                return;
            }

            Loadout loadout = state.Loadout;
            Weapon weapon;
            if (!MatchRunning)
            {
                if (!loadout.Has(loadout.Weapon))
                {
                    return;
                }

                weapon = loadout.Weapon;
            }
            else
            {
                Weapon? fired = loadout.Fire();
                if (fired == null)
                {
                    return;
                }

                weapon = fired.Value;
            }

            SetAim(state, fire.Heading, fire.Power);
            state.ShotHeading = state.Heading;
            state.ShotPower = state.Power;
            state.ShotWeapon = weapon;
            state.ShotFuse = Quantize(loadout.Fuse);
            state.ShotExtraBounces = state.ExtraBounces;
            state.ShotDetonation = 0;
            state.ExtraBounces = 0;
            state.Shots++;
            Volley volley = Solve(state, weapon, state.ShotHeading, state.ShotPower, state.ShotFuse, state.ShotExtraBounces);
            state.Flight = new VolleyFlight(volley);
            state.LastVolley = volley;
            Emit(new LoadoutChanged(state.Id));
            Emit(new ShotFired(state.Id, state.Seat, state.Shots, weapon, state.ShotHeading, state.ShotPower, state.ShotFuse, state.ShotExtraBounces, volley));
            if (MatchRunning)
            {
                Match.Fire(state.Seat); // the turn clock stops until it lands
            }
        }

        private void DoFuse(PlayerState state, int change)
        {
            if (!MayAct(state))
            {
                return;
            }

            Loadout loadout = state.Loadout;
            if (change < 0)
            {
                loadout.ShortenFuse();
            }
            else if (change == 1)
            {
                loadout.LengthenFuse();
            }
            else if (change >= 2)
            {
                loadout.MaxOutFuse();
            }

            Emit(new LoadoutChanged(state.Id));
        }

        // use_item.gml: the selected item, if carried and in play, before this turn's shot.
        private void DoUseItem(PlayerState state, UseItemCommand use)
        {
            if (!MayAct(state))
            {
                return;
            }

            Loadout loadout = state.Loadout;
            Item item = loadout.Item;
            if (!Arms.InPlay(item) || !loadout.Has(item))
            {
                return;
            }

            if (MatchRunning)
            {
                loadout.Use();
            }

            SetAim(state, use.Heading, use.Power);
            Emit(new ItemUsed(state.Id, item));
            switch (item)
            {
                case Item.ExtraBounce:
                    state.ExtraBounces++;
                    break;
                case Item.Teleporter:
                    Vec2 spot = Arsenal.Teleport(state.Position, state.Heading, state.Power, LevelFor(state.Seat), Rules, state.Radius, out _);
                    MoveTo(state, spot, MoveCause.Teleported);
                    break;
                case Item.ShieldWall:
                    Hazards.AddShieldWall(state.Seat, state.Position, state.Heading); // spawn_shieldwall.gml
                    break;
                case Item.Redirector:
                    Vec2 placed = Arsenal.PlaceRedirector(state.Position, state.Heading, state.Power, LevelFor(state.Seat), Rules, out double redirectorHeading, out _);
                    Hazards.AddRedirector(state.Seat, placed, redirectorHeading);
                    break;
                case Item.ShrinkRay:
                    string hit = Arsenal.ShrinkRay(state.Position, state.Heading, TargetIds.Seat(state.Seat), LevelFor(state.Seat), Targets(), Rules, out _);
                    PlayerState shrunk = PlayerAtSeat(TargetIds.SeatOf(hit));
                    if (shrunk != null)
                    {
                        shrunk.Scale = Quantize(Math.Min(shrunk.Scale, Arms.ShrinkScale));
                        Emit(new Shrunk(shrunk.Id, state.Seat, shrunk.Scale));
                    }

                    break;
                case Item.AimAssist:
                    // obj_aimassist, with the AI's targeting: onto the shot that reaches an opponent, if any does.
                    AiPlan assist = Ai.Assist(state.Position, TargetIds.Seat(state.Seat), LevelFor(state.Seat), Targets(), state.Power, Rules);
                    if (assist != null)
                    {
                        SetAim(state, assist.Heading, assist.Power);
                    }

                    state.AssistHeading = state.Heading;
                    state.AssistPower = state.Power;
                    state.Assists++;
                    Emit(new AssistApplied(state.Id, state.Heading, state.Power, assist != null));
                    break;
            }

            Emit(new LoadoutChanged(state.Id));
        }

        // vortex_detonate.gml. Only this player's own vortex, only while it is still in the air, and only once: the volley is
        // cut short where it is now and lands at once (the implosion strikes, swallows and breaches there).
        private void DoDetonate(PlayerState state)
        {
            VolleyFlight flight = state.Flight;
            if (flight == null || flight.Landed || flight.Volley.Weapon != Weapon.Vortex || state.ShotDetonation > 0)
            {
                return;
            }

            double at = Quantize(Math.Max(0.001, flight.Flights[0].Travelled));
            Volley cut = Arsenal.Detonate(flight.Volley, at, Targets(), Arms);
            if (ReferenceEquals(cut, flight.Volley))
            {
                return;
            }

            state.ShotDetonation = at;
            state.LastVolley = cut;
            Emit(new ShotCut(state.Id, state.Shots, at, cut));
            state.Flight = new VolleyFlight(cut);
            state.Flight.Advance(cut.Duration + 1.0);
            Land(state, cut);
        }

        // The player holds a drive key. Only on their own turn with nothing of theirs in the air; the tank then moves in
        // AdvanceDrive until the command stops arriving.
        private void DoDrive(PlayerState state, int direction)
        {
            direction = Math.Sign(direction);
            if (direction == 0 || !MayAct(state))
            {
                state.DriveDirection = 0;
                return;
            }

            state.DriveDirection = direction;
            state.DriveLeft = DriveHold;
        }

        // F / End Turn: only on the seat's own turn, before firing (Match.EndTurn).
        private void DoEndTurn(PlayerState state)
        {
            state.DriveDirection = 0;
            if (!MatchRunning || !MayAct(state))
            {
                return;
            }

            if (Match.MayEndTurn(state.Seat))
            {
                Emit(new TurnPassed(state.Seat, TurnPassReason.EndedEarly));
                Match.EndTurn(state.Seat);
            }
        }

        // ------------------------------------------------------------------ seat lifecycle (UR-T65)

        private void DoJoinSeat(PlayerState state, JoinSeatCommand join)
        {
            if (Match == null || join.Seat < 0 || join.Seat >= MatchRules.MaxSeats || Match.TryGetSeat(join.Seat, out _))
            {
                return;
            }

            Match.Join(join.Seat, join.IsAi ? SeatKind.Ai : SeatKind.Human, join.Name);
            state.Seat = join.Seat;
            Emit(new SeatJoined(state.Id, join.Seat, join.IsAi));
        }

        private void DoDropSeat(PlayerState state, DropSeatCommand drop)
        {
            if (Match != null && state.Seat >= 0 && Match.Drop(state.Seat, drop.HoldSeconds))
            {
                Emit(new SeatDropped(state.Id, state.Seat));
            }
        }

        private void DoRejoinSeat(PlayerState state)
        {
            if (Match != null && state.Seat >= 0 && Match.Rejoin(state.Seat))
            {
                Emit(new SeatRejoined(state.Id, state.Seat));
            }
        }

        private void DoLeaveSeat(PlayerState state, LeaveSeatCommand leave)
        {
            // The command names the seat; a player still here must be sitting in it (a stale or wrong id leaves nobody out).
            int seat = leave.Seat;
            if (Match == null || !Match.TryGetSeat(seat, out _) || (state != null && state.Seat != seat))
            {
                return;
            }

            Match.Leave(seat);
            Emit(new SeatLeft(leave.PlayerId, seat));
        }

        private void DoVote(PlayerState state, VoteCommand vote)
        {
            if (!_votes.TryGetValue(vote.Kind, out VoteTally tally))
            {
                return;
            }

            tally.Cast(state.Id, vote.Yes);
            Emit(new VoteCast(state.Id, vote.Kind, vote.Yes));
            if (tally.Passed)
            {
                _votes.Remove(vote.Kind);
                Emit(new VotePassed(vote.Kind, tally.Yes, tally.No));
            }
            else if (tally.Failed)
            {
                _votes.Remove(vote.Kind);
                Emit(new VoteFailed(vote.Kind, tally.Yes, tally.No));
            }
        }

        // ------------------------------------------------------------------ landing

        private void Land(PlayerState state, Volley volley)
        {
            if (volley == null)
            {
                return;
            }

            // A landed volley's mark on the level: every shield wall or tuft it ran into goes (obj_shieldwall's collision
            // events), and a vortex that went off swallows what it reaches and breaches the border near it.
            foreach (int id in volley.HazardsHit)
            {
                Hazards.Struck(id, state.Seat);
            }

            if (volley.Weapon == Weapon.Vortex && volley.HasBlast)
            {
                Hazards.Implode(volley.BlastCenter, volley.BlastRadius);
            }

            foreach (Strike strike in volley.Strikes)
            {
                if (TargetIds.IsDummy(strike.TargetId))
                {
                    Emit(new Hit(state.Id, strike.TargetId, -1, strike.Bounces));
                }
            }

            if (!MatchRunning)
            {
                Emit(new ShotLanded(state.Id, state.Seat, state.Shots, volley, null, false, -1));
                return;
            }

            // hit.gml per strike: damage, and points for a hit on an opponent.
            bool struckAPlayer = false;
            int knockedOut = -1;
            double flight = volley.Duration;
            var outcome = new ShotOutcome(); // UR-T12: what it did, for every peer
            foreach (Strike strike in volley.Strikes)
            {
                int hit = TargetIds.SeatOf(strike.TargetId);
                if (hit < 0)
                {
                    continue;
                }

                struckAPlayer = true;
                Emit(new Hit(state.Id, strike.TargetId, hit, strike.Bounces));
                bool wasIn = Match.TryGetSeat(hit, out MatchSeat before) && before.Alive;
                outcome.Add(hit, Match.Strike(state.Seat, hit, strike.Bounces, strike.Scale, flight));
                if (wasIn && !Match.Seat(hit).Alive)
                {
                    outcome.KnockOut(hit);
                    if (hit != state.Seat)
                    {
                        knockedOut = hit;
                    }
                }
            }

            Emit(new ShotLanded(state.Id, state.Seat, state.Shots, volley, outcome, struckAPlayer, knockedOut));

            if (Crates != null)
            {
                foreach (Drop drop in Crates.Collect(volley))
                {
                    Give(state, drop.Kind);
                }
            }

            bool inFlight = Match.Phase == MatchPhase.ShotInFlight && Match.Current == state.Seat;
            Match.ShotOver(state.Seat);
            if (inFlight && Match.Phase == MatchPhase.TurnOver)
            {
                Emit(new TurnPassed(state.Seat, TurnPassReason.ShotLanded));
            }
        }

        // add_random_weapon / add_random_item / add_random_health.gml: what a collected crate gives its collector.
        private void Give(PlayerState state, DropKind kind)
        {
            int value = 0;
            switch (kind)
            {
                case DropKind.Weapon:
                case DropKind.Powerup:
                    Weapon? weapon = Pickups.RandomWeapon(_dropDice, Arms);
                    if (weapon == null)
                    {
                        return;
                    }

                    state.Loadout.Add(weapon.Value);
                    value = (int)weapon.Value;
                    break;
                case DropKind.Item:
                    Item? item = Pickups.RandomItem(_dropDice, Arms);
                    if (item == null)
                    {
                        return;
                    }

                    state.Loadout.Add(item.Value);
                    value = (int)item.Value;
                    break;
                case DropKind.Health:
                    value = Match.Heal(state.Seat, Pickups.HealAmount(_dropDice, MatchRules.WeaponDamage));
                    break;
            }

            state.LastPickup = (int)kind * 1000 + Math.Max(0, Math.Min(999, value));
            state.Pickups++;
            Emit(new LoadoutChanged(state.Id));
            Emit(new CratePickedUp(state.Id, kind, value));
        }

        // ------------------------------------------------------------------ rounds and levels

        // A new round: everyone back to the starting loadout and full size, and the level cleared of crates. A random map
        // is laid afresh (randomize_level.gml) and everyone stands at a random spot of it (randomize_player_position.gml).
        private void NewRound()
        {
            RollLayout(_matchMap);
            NewField();
            PlaceEveryone();
            ResetHazards();
            foreach (PlayerState state in _order)
            {
                state.Loadout.Reset();
                state.Scale = 1;
                state.ExtraBounces = 0;
                Emit(new LoadoutChanged(state.Id));
            }

            Emit(new RoundStarted(Match.Round));
        }

        private void PlaceEveryone()
        {
            foreach (PlayerState state in _order)
            {
                if (state.Seat >= 0)
                {
                    MoveTo(state, Layout.SpawnPoint(state.Seat), MoveCause.Placed);
                }
            }
        }

        // Plays map `index` from now on (a hand-laid map is always itself).
        private void UseMap(int index)
        {
            index = Maps.Clamp(index);
            if (index != MapIndex || LayoutSeed != 0)
            {
                MapIndex = index;
                LayoutSeed = 0;
                Layout = Maps.At(index).Layout(0);
            }

            Emit(new LayoutChanged(MapIndex, LayoutSeed, LayoutVersion, false));
        }

        // Lays map `index`'s next round (a fresh seed for the random map) and tells the hazards which level it is.
        private void RollLayout(int index)
        {
            index = Maps.Clamp(index);
            int seed = Maps.At(index).IsRandom ? _layoutDice.Next(1, int.MaxValue) : 0;
            MapIndex = index;
            LayoutSeed = seed;
            Layout = Maps.At(index).Layout(seed);
            LayoutVersion++;
            Hazards.UseLevel(Layout.Level);
            Emit(new LayoutChanged(MapIndex, LayoutSeed, LayoutVersion, true));
        }

        /// <summary>
        /// Plays the level a peer already has (a new authority joining an arena whose map, seed and version are not the
        /// first map's): the version never goes backwards, so peers still adopt what comes next.
        /// </summary>
        public void AdoptLayout(int map, int seed, int version)
        {
            map = Maps.Clamp(map);
            MapIndex = map;
            LayoutSeed = seed;
            LayoutVersion = version;
            Layout = Maps.At(map).Layout(seed);
            Hazards.UseLevel(Layout.Level);
        }

        // A new crate field on the current level, the old one's crates gone.
        private void NewField()
        {
            Crates?.Clear();
            Crates = new DropField(Layout.Level, DropRules, _dropDice);
            Crates.Spawned += drop => Emit(new CrateDropped(drop));
            Crates.Landed += drop => Emit(new CrateLanded(drop));
            Crates.Removed += drop => Emit(new CrateRemoved(drop));
        }

        private void ClearExtraBounces()
        {
            foreach (PlayerState state in _order)
            {
                state.ExtraBounces = 0;
            }
        }

        // ------------------------------------------------------------------ small rules

        // May this player act now (cycle, set the fuse, use an item, fire, drive)? Their turn (anyone's in practice), and
        // nothing of theirs in the air (cycle_weapon_left's "turnStarted && !hasFired").
        private bool MayAct(PlayerState state)
        {
            if (state.InFlight)
            {
                return false;
            }

            return !MatchRunning || (state.Seat >= 0 && Match.MayFire(state.Seat));
        }

        // fire_weapon.gml's "!hasMoved": in a match, not after driving this turn.
        private bool MayShoot(PlayerState state) => !(MatchRunning && Match.HasMoved);

        // Never trusted as-is: NaN or an out-of-range power would be garbage on every peer.
        private static void SetAim(PlayerState state, double heading, double power)
        {
            state.Heading = Quantize(Angles.Normalize(Finite(heading) ? heading : state.Heading));
            state.Power = Quantize(ShotRules.ClampPower(power));
        }

        // NaN and infinity are garbage a client could send: the last good value stays.
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private void MoveTo(PlayerState state, Vec2 to, MoveCause cause)
        {
            Vec2 from = state.Position;
            state.Position = new Vec2(Quantize(to.X), Quantize(to.Y));
            Emit(new Moved(state.Id, from, state.Position, cause));
        }

        // What a client sees as a float is held as one here too.
        private static double Quantize(double value) => (float)value;

        // ------------------------------------------------------------------ events

        private IReadOnlyList<GameEvent> Run(Action action)
        {
            bool top = _current == null;
            if (top)
            {
                _current = new List<GameEvent>();
            }

            int start = _current.Count;
            try
            {
                action();
                return _current.GetRange(start, _current.Count - start);
            }
            finally
            {
                if (top)
                {
                    _current = null;
                }
            }
        }

        private void Emit(GameEvent e)
        {
            if (e == null)
            {
                return;
            }

            _current?.Add(e);
            Published?.Invoke(e);
        }

        private sealed class VoteTally
        {
            private readonly Dictionary<int, bool> _cast = new Dictionary<int, bool>();
            private readonly int _needed;
            private readonly int _electorate;

            public VoteTally(int electorate, double fraction)
            {
                _electorate = electorate;
                _needed = Math.Max(1, Math.Min(electorate, (int)Math.Ceiling(electorate * Math.Max(0, Math.Min(1, fraction)) - 1e-9)));
            }

            public int Yes
            {
                get
                {
                    int count = 0;
                    foreach (bool yes in _cast.Values)
                    {
                        count += yes ? 1 : 0;
                    }

                    return count;
                }
            }

            public int No => _cast.Count - Yes;

            public bool Passed => Yes >= _needed;

            public bool Failed => No > _electorate - _needed;

            public void Cast(int playerId, bool yes) => _cast[playerId] = yes;
        }
    }
}
