using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Ricochet.Core.Tests
{
    using Ricochet.Core.Aim;
    using Ricochet.Core.Commands;
    using Ricochet.Core.Level;
    using Ricochet.Core.Match;

    // UR-T43: the pause menu (obj_pausemenu, isPaused, obj_btn_randomize, obj_btn_quit_round) on the one authority. Paused,
    // the turn clock, the Ready card's hold, every shot and tank in motion wait, and nobody acts but to vote; Randomize
    // (before the round's first shot) unpauses and lays the round again on a fresh random level with the turn starting
    // over; Quit round stops the match. The Runtime (host-or-vote, every peer's freeze) is PlayMode's PauseMenuTests.
    public class PauseTests
    {
        private sealed class World : IGameWorld
        {
            public IReadOnlyList<Target> DummyTargets() => new List<Target>();
        }

        private static List<T> Of<T>(IEnumerable<GameEvent> events) where T : GameEvent => events.OfType<T>().ToList();

        private static GameAuthority TwoSeats(out PlayerState a, out PlayerState b, bool readyCard = false)
        {
            var authority = new GameAuthority(new World(), matchRules: readyCard ? MatchRules.Default : NoReadyCard.Rules(), seed: 7);
            a = authority.AddPlayer(1);
            b = authority.AddPlayer(2);
            authority.SetSeat(1, 0);
            authority.SetSeat(2, 1);
            Match match = authority.BeginMatch(0, MatchSetup.OneRound);
            match.Join(0, SeatKind.Human, "A");
            match.Join(1, SeatKind.Human, "B");
            authority.StartMatch();
            return authority;
        }

        [Test]
        public void APause_HoldsTheReadyCardAndTheClock_AndRefusesEveryCommandButAVote()
        {
            GameAuthority authority = TwoSeats(out PlayerState a, out _, readyCard: true);
            Assert.That(authority.Match.Phase, Is.EqualTo(MatchPhase.Ready));

            PauseChanged paused = Of<PauseChanged>(authority.SetPaused(true, 1)).Single();
            Assert.That(paused.Paused, Is.True);
            Assert.That(paused.PlayerId, Is.EqualTo(1));
            Assert.That(authority.Paused, Is.True);
            Assert.That(authority.SetPaused(true), Is.Empty, "already paused");

            authority.Tick(MatchRules.Default.ReadySeconds * 3);
            Assert.That(authority.Match.Phase, Is.EqualTo(MatchPhase.Ready), "the Ready card's hold waits");
            Assert.That(authority.Match.TimeLeft, Is.EqualTo(MatchRules.Default.TurnSeconds));

            Assert.That(Of<PauseChanged>(authority.SetPaused(false)).Single().Paused, Is.False);
            authority.Tick(MatchRules.Default.ReadySeconds + 0.01);
            Assert.That(authority.Match.Phase, Is.EqualTo(MatchPhase.Aiming), "resumed, the hold runs out and the clock starts");

            authority.Tick(2);
            double left = authority.Match.TimeLeft;
            authority.SetPaused(true);
            authority.Tick(100);
            Assert.That(authority.Match.TimeLeft, Is.EqualTo(left), "the turn clock waits");
            Assert.That(authority.Match.Current, Is.EqualTo(0), "the turn never times out while paused");

            double heading = a.Heading;
            Assert.That(authority.Apply(new AimCommand(1, heading + 45)), Is.Empty, "no turning");
            Assert.That(authority.Apply(new FireCommand(1, 90, 1)), Is.Empty, "no firing");
            Assert.That(authority.Apply(new EndTurnCommand(1)), Is.Empty, "no ending the turn");
            authority.Apply(new DriveCommand(1, 1));
            authority.AdvanceDrive(1, 0.2);
            Assert.That(authority.Match.HasMoved, Is.False, "no driving");
            Assert.That(a.Heading, Is.EqualTo(heading));
            Assert.That(a.Shots, Is.EqualTo(0));

            authority.OpenVote("resume", 2);
            Assert.That(Of<VoteCast>(authority.Apply(new VoteCommand(1, "resume", true))), Has.Count.EqualTo(1), "a vote goes on");
        }

        [Test]
        public void APause_HangsAShotInTheAir_AndStopsATankBeingDriven()
        {
            GameAuthority authority = TwoSeats(out PlayerState a, out PlayerState b);
            authority.Apply(new DriveCommand(2, 1)); // not B's turn: nothing
            authority.Apply(new FireCommand(1, 180, 1));
            Assert.That(a.InFlight, Is.True);
            authority.AdvanceFlight(1, 0.05);
            Vec2 at = a.Flight.Flights[0].Position;

            authority.SetPaused(true);
            for (int i = 0; i < 100; i++)
            {
                Assert.That(authority.AdvanceFlight(1, 0.05), Is.Empty);
            }

            Assert.That(a.InFlight, Is.True, "the shot hangs");
            Assert.That(a.Flight.Flights[0].Position, Is.EqualTo(at));

            authority.SetPaused(false);
            for (int i = 0; i < 400 && a.InFlight; i++)
            {
                authority.AdvanceFlight(1, 0.05);
            }

            Assert.That(a.InFlight, Is.False, "resumed, it flies on and lands");

            // B's turn: a held drive key stops at the pause.
            authority.Tick(MatchRules.Default.EndTurnSeconds + 0.01);
            Assert.That(authority.Match.Current, Is.EqualTo(1));
            authority.Apply(new DriveCommand(2, 1));
            Assert.That(b.DriveDirection, Is.Not.EqualTo(0));
            authority.SetPaused(true);
            Assert.That(b.DriveDirection, Is.EqualTo(0));
        }

        [Test]
        public void OnlyARunningMatchPauses()
        {
            var practice = new GameAuthority(new World(), seed: 7);
            practice.AddPlayer(1);
            Assert.That(practice.SetPaused(true), Is.Empty);
            Assert.That(practice.Paused, Is.False);
            Assert.That(practice.RerollRound(), Is.Empty);
            Assert.That(practice.QuitMatch(), Is.Empty);
        }

        [Test]
        public void Randomize_BeforeTheFirstShot_UnpausesAndLaysTheRoundAgain_OnANewRandomLevel_WithTheTurnStartedOver()
        {
            GameAuthority authority = TwoSeats(out PlayerState a, out _, readyCard: true);
            authority.Tick(MatchRules.Default.ReadySeconds + 0.01);
            authority.Tick(5);
            Assert.That(authority.Match.TimeLeft, Is.LessThan(MatchRules.Default.TurnSeconds));
            Assert.That(authority.MapIndex, Is.EqualTo(0), "the practice range");
            int version = authority.LayoutVersion;
            authority.SetPaused(true);
            Assert.That(authority.MayRerollRound, Is.True);

            IReadOnlyList<GameEvent> events = authority.RerollRound(-1);
            Assert.That(authority.Paused, Is.False, "Randomize unpauses first");
            Assert.That(Of<PauseChanged>(events).Single().Paused, Is.False);
            LevelRerolled rolled = Of<LevelRerolled>(events).Single();
            Assert.That(rolled.Map, Is.EqualTo(Maps.RandomIndex), "a new random level from the level generator");
            Assert.That(authority.MapIndex, Is.EqualTo(Maps.RandomIndex));
            Assert.That(authority.LayoutVersion, Is.GreaterThan(version), "every peer adopts the new level");
            Vec2 spot = authority.Layout.SpawnPoint(0);
            Assert.That(a.Position.X, Is.EqualTo(spot.X).Within(1e-4), "everyone at the new level's spots");
            Assert.That(a.Position.Y, Is.EqualTo(spot.Y).Within(1e-4));
            Assert.That(Of<TurnStarted>(events).Single().Seat, Is.EqualTo(0), "the same seat's turn starts over");
            Assert.That(authority.Match.Phase, Is.EqualTo(MatchPhase.Ready), "with its Ready card");
            Assert.That(authority.Match.TimeLeft, Is.EqualTo(MatchRules.Default.TurnSeconds), "and a full clock (reset_timer)");
            Assert.That(authority.Match.Round, Is.EqualTo(1), "the same round");
        }

        [Test]
        public void Randomize_IsGoneOnceAnyoneHasFiredThisRound_AndBackNextRound()
        {
            GameAuthority authority = TwoSeats(out PlayerState a, out PlayerState b);
            authority.Apply(new FireCommand(1, 90, 0.1));
            Assert.That(authority.MayRerollRound, Is.False, "a shot is in the air");
            for (int i = 0; i < 400 && a.InFlight; i++)
            {
                authority.AdvanceFlight(1, 0.05);
            }

            authority.Tick(MatchRules.Default.EndTurnSeconds + 0.01);
            Assert.That(authority.Match.Current, Is.EqualTo(1));
            Assert.That(authority.MayRerollRound, Is.False, "hasFired: someone shot this round already");
            int version = authority.LayoutVersion;
            Assert.That(authority.RerollRound(), Is.Empty);
            Assert.That(authority.LayoutVersion, Is.EqualTo(version));
        }

        [Test]
        public void QuitRound_StopsTheMatch_LiftsThePause_AndGoesBackToPractice()
        {
            GameAuthority authority = TwoSeats(out _, out _);
            authority.SetPaused(true);
            IReadOnlyList<GameEvent> events = authority.QuitMatch(2);
            Assert.That(Of<MatchQuit>(events).Single().PlayerId, Is.EqualTo(2));
            Assert.That(Of<PauseChanged>(events).Single().Paused, Is.False);
            Assert.That(authority.Paused, Is.False);
            Assert.That(authority.MatchRunning, Is.False);
            Assert.That(authority.Match.Phase, Is.EqualTo(MatchPhase.Practice));
            Assert.That(Of<ShotFired>(authority.Apply(new FireCommand(1, 90, 0.5))), Has.Count.EqualTo(1), "practice: anyone fires");
        }
    }
}
