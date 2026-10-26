// The turn loop: up to sixteen seats, humans and AI, taking turns in seat order with a turn timer, losing health to
// shots, and dropping out at zero until one is left standing. The 2013 original kept this in globals and scripts
// (design reference only, UR-LAW-2): next_player.gml (the next living seat, wrapping), turn_countdown.gml and
// reset_timer.gml (a per-turn timer that passes the turn when it runs out, beeping over the last four seconds),
// hit.gml (damage, self-hits, a kill when health reaches zero) and check_endgame.gml (the round is over when all but
// one are dead). Plain C# so every rule is unit-tested; the server runs one Match and the network layer mirrors it.
// The game's goal (UR-T9): check_endgame.gml went to the endgame room instead of the next round once anyone's score had
// reached the goal (globals.gml's goal of 500 points; 1 meant a single round). Here the round's result stays up for its
// pause, then the match is over (GameOver) and its champion is the top score, as the endgame room's leaderboard ranked it.
// Reconnects (UR-T13): the original was hotseat and never lost a player; here a human whose connection drops is Away
// (Drop) for the kit's reconnect grace: their seat, health and score are kept, and while it is their turn the turn
// clock waits for them, so a player back in time (Rejoin) resumes the same turn with the time it had left. One who does
// not come back in time leaves (Leave), which passes the turn.
// The Ready card (UR-T42): every turn opens in MatchPhase.Ready, obj_ready's hold and fade, with the clock full and
// standing still and nobody able to act (the original's turnStarted false); after MatchRules.ReadySeconds the clock
// starts (ClockStarted) and the turn is Aiming. The round itself is never started before it is laid: the authority rolls
// the round's level and every seat's spot, clear of overlaps (LevelGenerator), in full before the first turn begins, where
// the original polled check_isready each step until its shapes and players had shuffled themselves apart.
using System;
using System.Collections.Generic;

namespace Ricochet.Core.Match
{
    using Ricochet.Core.Ai;
    using Ricochet.Core.Arsenal;
    public enum SeatKind
    {
        Human,
        Ai,
    }

    public enum MatchPhase
    {
        /// <summary>No match yet: free practice, anyone may fire.</summary>
        Practice,

        /// <summary>The current seat is aiming and the turn timer runs.</summary>
        Aiming,

        /// <summary>The current seat's shot is in the air; the timer is stopped.</summary>
        ShotInFlight,

        /// <summary>The shot has landed; the next turn starts after <see cref="MatchRules.EndTurnSeconds"/>.</summary>
        TurnOver,

        /// <summary>One seat (or none) is left standing; the next round starts after <see cref="MatchRules.RoundOverSeconds"/>.</summary>
        RoundOver,

        /// <summary>The goal was reached (UR-T9): the match is over and <see cref="Match.Champion"/> won it; nobody fires.</summary>
        GameOver,

        /// <summary>
        /// A turn has begun and its Ready card is up (UR-T42, obj_ready): <see cref="Match.Current"/> is set and the clock is
        /// full, but it does not run and nobody may act (turnStarted is still false) until
        /// <see cref="MatchRules.ReadySeconds"/> have passed; then the turn goes to <see cref="Aiming"/>.
        /// </summary>
        Ready,
    }

    /// <summary>One seat at the table.</summary>
    public sealed class MatchSeat
    {
        internal MatchSeat(int index, SeatKind kind, string name)
        {
            Index = index;
            Kind = kind;
            Name = name ?? string.Empty;
        }

        /// <summary>0 to <see cref="MatchRules.MaxSeats"/> - 1: the turn order, and where the seat stands.</summary>
        public int Index { get; }

        public SeatKind Kind { get; }

        public string Name { get; internal set; }

        public int Health { get; internal set; }

        /// <summary>Points scored this match (pScore): hits on opponents, carried from round to round (UR-T8).</summary>
        public int Score { get; internal set; }

        /// <summary>Still in this round: has health left, and has not left the game or joined after it started.</summary>
        public bool Alive { get; internal set; }

        /// <summary>False once a human has left (their seat sits out until the next match).</summary>
        public bool Present { get; internal set; } = true;

        /// <summary>
        /// True while a human's connection has dropped and their seat is held for them to reconnect (UR-T13): still in
        /// the round, health and score kept, and the turn clock waits on their turn.
        /// </summary>
        public bool Away { get; internal set; }

        /// <summary>While <see cref="Away"/>, about how many seconds are left for them to come back (counted down by <see cref="Match.Tick"/>).</summary>
        public double AwayLeft { get; internal set; }

        public override string ToString() => Name + " (seat " + Index + ", " + Kind + ", " + Health + " hp" + (Alive ? string.Empty : ", out") + (Away ? ", away" : string.Empty) + ")";
    }

    public sealed class Match
    {
        private readonly SortedDictionary<int, MatchSeat> _seats = new SortedDictionary<int, MatchSeat>();
        private readonly Random _dice;
        private double _phaseTime;

        public Match(MatchRules rules = null, int seed = 0)
        {
            Rules = rules ?? MatchRules.Default;
            _dice = seed == 0 ? new Random() : new Random(seed);
            Current = -1;
            Winner = -1;
            Champion = -1;
        }

        public MatchRules Rules { get; }

        public MatchPhase Phase { get; private set; }

        /// <summary>The seat whose turn it is, or -1 (practice, or nobody left).</summary>
        public int Current { get; private set; }

        /// <summary>The current seat has driven this turn, so it can no longer shoot (the original's hasMoved).</summary>
        public bool HasMoved { get; private set; }

        /// <summary>Seconds left in the current turn (stopped while a shot is in the air).</summary>
        public double TimeLeft { get; private set; }

        /// <summary>1 for the first round, counting up.</summary>
        public int Round { get; private set; }

        /// <summary>The last round's winner, or -1 (no round over yet, or nobody survived).</summary>
        public int Winner { get; private set; }

        /// <summary>
        /// The points that win the game (goal): once a round ends with anyone's score at or above it, the match is over.
        /// <see cref="MatchSetup.OneRound"/> (1) ends it after the first round whatever the scores; 0 or less never ends it.
        /// </summary>
        public int Goal { get; set; }

        /// <summary>The game's champion (the top score, the lowest seat on a tie) once it is over, else -1.</summary>
        public int Champion { get; private set; }

        /// <summary>Every seat, in turn (seat) order.</summary>
        public IEnumerable<MatchSeat> Seats => _seats.Values;

        public int SeatCount => _seats.Count;

        public bool IsRunning => Phase != MatchPhase.Practice;

        /// <summary>A round began (its number), every seat back at full health, just before its first turn.</summary>
        public event Action<int> RoundStarted;

        /// <summary>A new turn began, for this seat (its Ready card goes up; the clock waits for <see cref="ClockStarted"/>).</summary>
        public event Action<int> TurnStarted;

        /// <summary>
        /// The Ready card's hold is over and the seat's clock starts (obj_ready's turnStarted = true and ping_player): from
        /// here it may aim, drive and fire, and an AI starts thinking.
        /// </summary>
        public event Action<int> ClockStarted;

        /// <summary>The turn timer passed into one of the last whole seconds (4, 3, 2, 1): a beep.</summary>
        public event Action<int> Countdown;

        /// <summary>The seat ran out of time without firing.</summary>
        public event Action<int> TimedOut;

        /// <summary>A seat lost health: (seat, damage, by seat).</summary>
        public event Action<int, int, int> Damaged;

        /// <summary>A seat scored for a hit on an opponent: (seat, points).</summary>
        public event Action<int, int> Scored;

        /// <summary>A seat's health reached zero.</summary>
        public event Action<int> Eliminated;

        /// <summary>The round ended; the winner's seat, or -1 when nobody survived.</summary>
        public event Action<int> RoundEnded;

        /// <summary>The goal was reached and the match is over: the champion's seat (UR-T9).</summary>
        public event Action<int> GameEnded;

        /// <summary>A human's connection dropped and their seat is held (UR-T13).</summary>
        public event Action<int> Dropped;

        /// <summary>A dropped human reconnected in time and is back in their seat (UR-T13).</summary>
        public event Action<int> Rejoined;

        /// <summary>
        /// True while the turn is waiting for its seat's player to reconnect: it is an <see cref="MatchSeat.Away"/> seat's
        /// turn and the clock is stopped.
        /// </summary>
        public bool WaitingForCurrent => Phase == MatchPhase.Aiming && _seats.TryGetValue(Current, out MatchSeat seat) && seat.Away;

        /// <summary>True when the goal has been reached: a single-round game, or anyone's score at or above it.</summary>
        public bool GoalReached
        {
            get
            {
                if (Goal <= 0)
                {
                    return false;
                }

                if (Goal == MatchSetup.OneRound)
                {
                    return Round >= 1;
                }

                foreach (MatchSeat seat in _seats.Values)
                {
                    if (seat.Score >= Goal)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool TryGetSeat(int index, out MatchSeat seat) => _seats.TryGetValue(index, out seat);

        public MatchSeat Seat(int index) => _seats.TryGetValue(index, out MatchSeat seat) ? seat : null;

        /// <summary>The lowest seat index nobody holds, or -1 when all sixteen are taken.</summary>
        public int FreeSeat()
        {
            for (int i = 0; i < MatchRules.MaxSeats; i++)
            {
                if (!_seats.ContainsKey(i))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Seats a player. Before the match starts they play from the first round; once it is running they sit the
        /// current round out (alive next round), the way a late arrival waits for the next rack.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The index is outside 0..15.</exception>
        /// <exception cref="InvalidOperationException">The seat is taken.</exception>
        public MatchSeat Join(int index, SeatKind kind, string name)
        {
            if (index < 0 || index >= MatchRules.MaxSeats)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "A seat is 0 to " + (MatchRules.MaxSeats - 1) + ".");
            }

            if (_seats.ContainsKey(index))
            {
                throw new InvalidOperationException("Seat " + index + " is taken.");
            }

            var seat = new MatchSeat(index, kind, name) { Health = Rules.MaxHealth, Alive = !IsRunning };
            _seats.Add(index, seat);
            return seat;
        }

        /// <summary>
        /// A player left: their seat drops out of the round (and is removed outright before the match starts). When it
        /// was their turn the turn passes; when only one is left standing the round is over.
        /// </summary>
        public void Leave(int index)
        {
            if (!_seats.TryGetValue(index, out MatchSeat seat))
            {
                return;
            }

            if (!IsRunning)
            {
                _seats.Remove(index);
                return;
            }

            seat.Present = false;
            seat.Alive = false;
            seat.Away = false;
            seat.AwayLeft = 0;
            if (CheckRoundOver())
            {
                return;
            }

            if (index == Current && (Phase == MatchPhase.Aiming || Phase == MatchPhase.Ready))
            {
                NextTurn();
            }
        }

        /// <summary>
        /// A human's connection dropped and the kit holds their seat for <paramref name="holdSeconds"/> (UR-T13): the seat
        /// stays in the match with its health and score, and on its turn the clock waits for them. False (and nothing
        /// changes) for an AI, a seat that has left, or one already away.
        /// </summary>
        public bool Drop(int index, double holdSeconds)
        {
            if (!_seats.TryGetValue(index, out MatchSeat seat) || seat.Kind != SeatKind.Human || !seat.Present || seat.Away)
            {
                return false;
            }

            seat.Away = true;
            seat.AwayLeft = double.IsNaN(holdSeconds) ? 0 : Math.Max(0, holdSeconds);
            Dropped?.Invoke(index);
            return true;
        }

        /// <summary>
        /// A dropped human reconnected in time (UR-T13): back in their seat as they left it, and on their turn the clock
        /// runs on from where it stopped. False (and nothing changes) when the seat was not away.
        /// </summary>
        public bool Rejoin(int index)
        {
            if (!_seats.TryGetValue(index, out MatchSeat seat) || !seat.Away)
            {
                return false;
            }

            seat.Away = false;
            seat.AwayLeft = 0;
            Rejoined?.Invoke(index);
            return true;
        }

        /// <summary>Starts round one with every seat at full health; the lowest seat goes first.</summary>
        /// <exception cref="InvalidOperationException">Fewer than two seats.</exception>
        public void Start()
        {
            if (_seats.Count < 2)
            {
                throw new InvalidOperationException("A match needs at least two seats; it has " + _seats.Count + ".");
            }

            Round = 0;
            Winner = -1;
            Champion = -1;
            foreach (MatchSeat seat in _seats.Values)
            {
                seat.Score = 0; // reset_scores.gml: a new match starts everyone at nothing
            }

            StartRound(-1);
        }

        /// <summary>Back to practice: the turn loop stops (the seats stay).</summary>
        public void Stop()
        {
            Phase = MatchPhase.Practice;
            HasMoved = false;
            Current = -1;
            TimeLeft = 0;
        }

        /// <summary>
        /// Only the seat whose turn it is may fire, and only while aiming (anyone may in practice; nobody once the game is
        /// over, and nobody for a seat that is away).
        /// </summary>
        public bool MayFire(int seat)
        {
            return Phase == MatchPhase.Practice || (Phase == MatchPhase.Aiming && seat == Current && !WaitingForCurrent);
        }

        /// <summary>
        /// The current seat drove its tank this turn (move_forward.gml's hasMoved): it may not shoot again until the turn
        /// passes. False (and nothing changes) when it may not drive now.
        /// </summary>
        public bool Move(int seat)
        {
            if (Phase != MatchPhase.Aiming || seat != Current || WaitingForCurrent)
            {
                return false;
            }

            HasMoved = true;
            return true;
        }

        /// <summary>
        /// next_player.gml on F or the End Turn button: the current seat gives up the rest of its turn (before firing). False
        /// when it may not (not its turn, a shot already in the air, or no match).
        /// </summary>
        public bool EndTurn(int seat)
        {
            if (!MayEndTurn(seat))
            {
                return false;
            }

            NextTurn();
            return true;
        }

        /// <summary>True when <see cref="EndTurn"/> would pass the turn: it is this seat's turn, aiming, and the seat is not away.</summary>
        public bool MayEndTurn(int seat) => Phase == MatchPhase.Aiming && seat == Current && !WaitingForCurrent;

        /// <summary>The current seat fired: the timer stops until the shot lands. False (and nothing changes) when it may not.</summary>
        public bool Fire(int seat)
        {
            if (!MayFire(seat) || (Phase == MatchPhase.Aiming && HasMoved))
            {
                return false; // fire_weapon.gml: "You cannot move and shoot within the same turn"
            }

            if (Phase == MatchPhase.Aiming)
            {
                Phase = MatchPhase.ShotInFlight;
            }

            return true;
        }

        /// <summary>
        /// The shot from <paramref name="shooter"/> landed, on <paramref name="hitSeat"/> (-1 for a miss) after
        /// <paramref name="bounces"/> deflections: hit.gml's damage and kill, then the turn ends. Returns the damage done.
        /// </summary>
        public int ShotLanded(int shooter, int hitSeat, int bounces)
        {
            int damage = Strike(shooter, hitSeat, bounces);
            ShotOver(shooter);
            return damage;
        }

        /// <summary>
        /// One hit from <paramref name="shooter"/>'s shot on <paramref name="hitSeat"/> (a volley can make several: a
        /// shotgun's pellets, a grenade's blast, a railgun's slug): hit.gml's damage times <paramref name="scale"/> (at
        /// least 1), and a kill at zero. A hit on an opponent also scores the shooter <see cref="MatchRules.Points"/> for
        /// the bounces and the <paramref name="flightSeconds"/> the shot was in the air, times the same share (hit.gml's
        /// pScore += point_combo). The turn does not end here; see <see cref="ShotOver"/>. Returns the damage done.
        /// </summary>
        public int Strike(int shooter, int hitSeat, int bounces, double scale = 1, double flightSeconds = 0)
        {
            if (!IsRunning || Phase == MatchPhase.RoundOver || Phase == MatchPhase.GameOver || !_seats.TryGetValue(hitSeat, out MatchSeat hit) || !hit.Alive)
            {
                return 0;
            }

            bool whole = double.IsNaN(scale) || scale >= 1;
            int full = Rules.Damage(hitSeat == shooter, bounces, _dice.NextDouble() * 2 - 1);
            int damage = whole ? full : Math.Max(1, (int)Math.Round(full * Math.Max(0, scale), MidpointRounding.AwayFromZero));
            if (hitSeat != shooter && _seats.TryGetValue(shooter, out MatchSeat scorer))
            {
                int combo = Rules.Points(bounces, flightSeconds);
                int points = whole ? combo : Math.Max(1, (int)Math.Round(combo * Math.Max(0, scale), MidpointRounding.AwayFromZero));
                scorer.Score += points;
                Scored?.Invoke(shooter, points);
            }

            hit.Health = Math.Max(0, hit.Health - damage);
            Damaged?.Invoke(hitSeat, damage, shooter);
            if (hit.Health == 0)
            {
                hit.Alive = false;
                Eliminated?.Invoke(hitSeat);
            }

            return damage;
        }

        /// <summary>
        /// add_random_health.gml: <paramref name="amount"/> health back for a seat still in the round, up to the maximum.
        /// Returns what was actually added.
        /// </summary>
        public int Heal(int seat, int amount)
        {
            if (!IsRunning || Phase == MatchPhase.GameOver || amount <= 0 || !_seats.TryGetValue(seat, out MatchSeat healed) || !healed.Alive)
            {
                return 0;
            }

            int before = healed.Health;
            healed.Health = Math.Min(Rules.MaxHealth, healed.Health + amount);
            return healed.Health - before;
        }

        /// <summary>The current seat's shot (every part of it) has landed: the round may be over, else the turn ends.</summary>
        public void ShotOver(int shooter)
        {
            if (Phase == MatchPhase.ShotInFlight && shooter == Current && !CheckRoundOver())
            {
                Phase = MatchPhase.TurnOver;
                _phaseTime = Rules.EndTurnSeconds;
            }
            else
            {
                CheckRoundOver();
            }
        }

        /// <summary>Runs the clock: the turn timer while aiming, and the pauses after a shot and after a round.</summary>
        public void Tick(double seconds)
        {
            if (seconds <= 0 || double.IsNaN(seconds))
            {
                return;
            }

            foreach (MatchSeat seat in _seats.Values)
            {
                if (seat.Away)
                {
                    seat.AwayLeft = Math.Max(0, seat.AwayLeft - seconds);
                }
            }

            switch (Phase)
            {
                case MatchPhase.Ready:
                    // obj_ready: the card holds, fades, and only then does the clock run (reset_timer filled it already).
                    _phaseTime -= seconds;
                    if (_phaseTime <= 0)
                    {
                        StartClock();
                    }

                    break;
                case MatchPhase.Aiming:
                    if (WaitingForCurrent)
                    {
                        break; // the turn waits for its player to reconnect (or for the kit to give their seat up)
                    }

                    double before = TimeLeft;
                    TimeLeft = Math.Max(0, TimeLeft - seconds);
                    for (int beep = Math.Min(Rules.CountdownBeeps, (int)Math.Ceiling(before) - 1); beep >= 1 && TimeLeft <= beep; beep--)
                    {
                        Countdown?.Invoke(beep);
                    }

                    if (TimeLeft <= 0)
                    {
                        TimedOut?.Invoke(Current);
                        NextTurn();
                    }

                    break;
                case MatchPhase.TurnOver:
                    _phaseTime -= seconds;
                    if (_phaseTime <= 0)
                    {
                        NextTurn();
                    }

                    break;
                case MatchPhase.RoundOver:
                    _phaseTime -= seconds;
                    if (_phaseTime <= 0)
                    {
                        if (GoalReached)
                        {
                            EndGame();
                        }
                        else
                        {
                            StartRound(Winner);
                        }
                    }

                    break;
            }
        }

        /// <summary>The living seats in the order they play from now: the current seat first.</summary>
        public List<int> TurnOrder()
        {
            var order = new List<int>();
            var living = new List<int>();
            foreach (MatchSeat seat in _seats.Values)
            {
                if (seat.Alive)
                {
                    living.Add(seat.Index);
                }
            }

            int start = living.FindIndex(i => i >= Current);
            if (start < 0)
            {
                start = 0;
            }

            for (int i = 0; i < living.Count; i++)
            {
                order.Add(living[(start + i) % living.Count]);
            }

            return order;
        }

        /// <summary>next_player.gml: the turn passes to the next living seat, wrapping past the last.</summary>
        public void NextTurn()
        {
            if (!IsRunning || Phase == MatchPhase.GameOver || CheckRoundOver())
            {
                return;
            }

            int next = NextLiving(Current);
            BeginTurn(next);
        }

        /// <summary>
        /// The current seat's turn starts over (UR-T43: the pause menu's Randomize ends in reset_timer and a fresh obj_ready):
        /// its Ready card and a full clock, as if the turn had just begun. False (and nothing changes) unless a turn is
        /// open and nothing has been fired in it (Ready or Aiming).
        /// </summary>
        public bool RestartTurn()
        {
            if ((Phase != MatchPhase.Ready && Phase != MatchPhase.Aiming) || !_seats.ContainsKey(Current))
            {
                return false;
            }

            BeginTurn(Current);
            return true;
        }

        /// <summary>
        /// The seats ranked for the endgame recap: the highest score first, the lower seat first on a tie (the endgame
        /// room's ds_grid_sort of pScore).
        /// </summary>
        public List<MatchSeat> Standings()
        {
            var ranked = new List<MatchSeat>(_seats.Values);
            ranked.Sort((a, b) => a.Score != b.Score ? b.Score.CompareTo(a.Score) : a.Index.CompareTo(b.Index));
            return ranked;
        }

        // check_endgame.gml's room_goto(endgame): the match is over, and the top score is its champion.
        private void EndGame()
        {
            List<MatchSeat> ranked = Standings();
            Champion = ranked.Count > 0 ? ranked[0].Index : -1;
            Phase = MatchPhase.GameOver;
            Current = -1;
            TimeLeft = 0;
            GameEnded?.Invoke(Champion);
        }

        private void StartRound(int firstSeat)
        {
            Round++;
            Winner = -1;
            int present = 0;
            foreach (MatchSeat seat in _seats.Values)
            {
                seat.Health = Rules.MaxHealth;
                seat.Alive = seat.Present;
                present += seat.Present ? 1 : 0;
            }

            if (present < 2)
            {
                // Everyone else left: nothing to play; back to practice until a new match starts.
                Stop();
                return;
            }

            int first = firstSeat >= 0 && _seats.TryGetValue(firstSeat, out MatchSeat winner) && winner.Alive ? firstSeat : NextLiving(-1);
            RoundStarted?.Invoke(Round);
            BeginTurn(first);
        }

        // next_player.gml / randomize_level.gml: the turn is the seat's, its clock full (reset_timer), and obj_ready's card
        // goes up; the clock starts once the card has held and faded (a hold of 0 starts it at once).
        private void BeginTurn(int seat)
        {
            Current = seat;
            HasMoved = false;
            TimeLeft = Rules.TurnSeconds;
            double hold = Rules.ReadySeconds;
            if (hold > 0 && !double.IsNaN(hold))
            {
                Phase = MatchPhase.Ready;
                _phaseTime = hold;
                TurnStarted?.Invoke(seat);
                return;
            }

            Phase = MatchPhase.Aiming;
            TurnStarted?.Invoke(seat);
            if (Phase == MatchPhase.Aiming && Current == seat)
            {
                ClockStarted?.Invoke(seat);
            }
        }

        private void StartClock()
        {
            Phase = MatchPhase.Aiming;
            ClockStarted?.Invoke(Current);
        }

        private int NextLiving(int after)
        {
            foreach (MatchSeat seat in _seats.Values)
            {
                if (seat.Index > after && seat.Alive)
                {
                    return seat.Index;
                }
            }

            foreach (MatchSeat seat in _seats.Values)
            {
                if (seat.Alive)
                {
                    return seat.Index;
                }
            }

            return -1;
        }

        // check_endgame.gml: all but one (or all) are out: the round is over.
        private bool CheckRoundOver()
        {
            if (Phase == MatchPhase.RoundOver || Phase == MatchPhase.GameOver || !IsRunning)
            {
                return Phase == MatchPhase.RoundOver || Phase == MatchPhase.GameOver;
            }

            int living = 0;
            int last = -1;
            foreach (MatchSeat seat in _seats.Values)
            {
                if (seat.Alive)
                {
                    living++;
                    last = seat.Index;
                }
            }

            if (living > 1)
            {
                return false;
            }

            Winner = last;
            Current = -1;
            TimeLeft = 0;
            Phase = MatchPhase.RoundOver;
            _phaseTime = Rules.RoundOverSeconds;
            RoundEnded?.Invoke(last);
            return true;
        }
    }
}
