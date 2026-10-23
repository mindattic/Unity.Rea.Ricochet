using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Ricochet.Core.Tests
{
    public class MatchTests
    {
        private static Match Table(int seats, MatchRules rules = null)
        {
            var match = new Match(rules, seed: 7);
            for (int i = 0; i < seats; i++)
            {
                match.Join(i, i % 2 == 0 ? SeatKind.Human : SeatKind.Ai, "P" + i);
            }

            return match;
        }

        [Test]
        public void SixteenSeatsHumanAndAiMixed_NoSeventeenth()
        {
            Match match = Table(16);
            Assert.That(match.SeatCount, Is.EqualTo(16));
            Assert.That(match.Seats.Count(s => s.Kind == SeatKind.Ai), Is.EqualTo(8));
            Assert.That(match.FreeSeat(), Is.EqualTo(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => match.Join(16, SeatKind.Human, "seventeenth"));
            Assert.Throws<InvalidOperationException>(() => match.Join(3, SeatKind.Human, "taken"));
        }

        [Test]
        public void AMatchNeedsTwoSeats_AndStartsAtFullHealthWithTheLowestSeat()
        {
            var match = new Match();
            match.Join(4, SeatKind.Human, "Ann");
            Assert.Throws<InvalidOperationException>(() => match.Start());
            match.Join(2, SeatKind.Ai, "Bot");
            match.Start();

            Assert.That(match.Phase, Is.EqualTo(MatchPhase.Aiming));
            Assert.That(match.Round, Is.EqualTo(1));
            Assert.That(match.Current, Is.EqualTo(2));
            Assert.That(match.TimeLeft, Is.EqualTo(20));
            Assert.That(match.Seats.All(s => s.Health == 100 && s.Alive), Is.True);
        }

        [Test]
        public void OnlyTheCurrentSeatMayFire_AndOnlyOnce()
        {
            Match match = Table(3);
            Assert.That(match.MayFire(1), Is.True, "practice: anyone fires");
            match.Start();

            Assert.That(match.Fire(1), Is.False, "not seat 1's turn");
            Assert.That(match.Fire(0), Is.True);
            Assert.That(match.Phase, Is.EqualTo(MatchPhase.ShotInFlight));
            Assert.That(match.Fire(0), Is.False, "one shot a turn");
        }

        [Test]
        public void TheTurnPassesInSeatOrder_AfterTheShotLands_SkippingTheDead_AndWrapping()
        {
            Match match = Table(4);
            match.Start();
            var turns = new List<int>();
            match.TurnStarted += turns.Add;

            match.Leave(2);
            PlayTurn(match, 0);
            PlayTurn(match, 1);
            PlayTurn(match, 3);
            Assert.That(turns, Is.EqualTo(new[] { 1, 3, 0 }), "seat 2 is out; after 3 it wraps to 0");
        }

        [Test]
        public void TheTimerRunsOnlyWhileAiming_BeepsTheLastFourSeconds_AndPassesTheTurnWhenItRunsOut()
        {
            Match match = Table(2);
            match.Start();
            var beeps = new List<int>();
            var timedOut = new List<int>();
            match.Countdown += beeps.Add;
            match.TimedOut += timedOut.Add;

            for (int frame = 0; frame < 19 * 60; frame++)
            {
                match.Tick(1.0 / 60);
            }

            Assert.That(match.TimeLeft, Is.EqualTo(1).Within(1e-6));
            Assert.That(beeps, Is.EqualTo(new[] { 4, 3, 2, 1 }));
            Assert.That(match.Current, Is.EqualTo(0));

            match.Tick(1.01);
            Assert.That(timedOut, Is.EqualTo(new[] { 0 }));
            Assert.That(match.Current, Is.EqualTo(1));
            Assert.That(match.TimeLeft, Is.EqualTo(20), "a fresh clock for the next seat");

            match.Fire(1);
            match.Tick(30);
            Assert.That(match.Current, Is.EqualTo(1), "the clock stops while the shot is in the air");
            Assert.That(match.TimeLeft, Is.EqualTo(20));
        }

        [Test]
        public void DamageFollowsHitGml_OpponentsGetAFifthPerBounceAndADiceRoll_SelfHitsTheFullBasePerBounce()
        {
            MatchRules rules = MatchRules.Default;
            Assert.That(rules.Damage(self: false, bounces: 0, roll: 0), Is.EqualTo(10));
            Assert.That(rules.Damage(false, 3, 0), Is.EqualTo(16), "10 + ceil(10 / 5) x 3");
            Assert.That(rules.Damage(false, 3, 1), Is.EqualTo(17), "the dice add up to a tenth");
            Assert.That(rules.Damage(false, 3, -1), Is.EqualTo(15));
            Assert.That(rules.Damage(true, 2, 1), Is.EqualTo(30), "10 + 10 x 2, no dice on yourself");
            Assert.That(rules.Damage(false, -4, double.NaN), Is.EqualTo(10), "garbage in counts as none");
        }

        [Test]
        public void AHitTakesHealth_AndTheTurnEnds()
        {
            Match match = Table(3);
            match.Start();
            var damaged = new List<(int seat, int damage, int by)>();
            match.Damaged += (seat, damage, by) => damaged.Add((seat, damage, by));

            match.Fire(0);
            int done = match.ShotLanded(0, 2, bounces: 1);
            Assert.That(done, Is.InRange(11, 13), "10 + 2 for the bounce, +/-1");
            Assert.That(match.Seat(2).Health, Is.EqualTo(100 - done));
            Assert.That(damaged, Is.EqualTo(new[] { (2, done, 0) }));
            Assert.That(match.Phase, Is.EqualTo(MatchPhase.TurnOver));

            match.Tick(match.Rules.EndTurnSeconds + 0.01);
            Assert.That(match.Current, Is.EqualTo(1));
        }

        [Test]
        public void AtZeroHealthASeatIsOut_AndTheLastOneStandingWinsTheRound_ThenANewRoundStartsWithTheWinner()
        {
            var rules = new MatchRules { MaxHealth = 10 };
            Match match = Table(3, rules);
            match.Start();
            var eliminated = new List<int>();
            int winner = -2;
            match.Eliminated += eliminated.Add;
            match.RoundEnded += w => winner = w;

            match.Fire(0);
            match.ShotLanded(0, 1, 0);
            Assert.That(eliminated, Is.EqualTo(new[] { 1 }));
            Assert.That(match.Seat(1).Alive, Is.False);
            match.Tick(rules.EndTurnSeconds + 0.01);
            Assert.That(match.Current, Is.EqualTo(2), "seat 1 is skipped");

            match.Fire(2);
            match.ShotLanded(2, 0, 0);
            Assert.That(match.Phase, Is.EqualTo(MatchPhase.RoundOver));
            Assert.That(winner, Is.EqualTo(2));
            Assert.That(match.Winner, Is.EqualTo(2));
            Assert.That(match.MayFire(2), Is.False, "nobody fires between rounds");

            match.Tick(rules.RoundOverSeconds + 0.01);
            Assert.That(match.Round, Is.EqualTo(2));
            Assert.That(match.Current, Is.EqualTo(2), "the winner opens the next round");
            Assert.That(match.Seats.All(s => s.Alive && s.Health == 10), Is.True);
        }

        [Test]
        public void ShootingYourselfOutPassesTheTurn_OrEndsTheRoundForTheOther()
        {
            var rules = new MatchRules { MaxHealth = 10 };
            Match match = Table(2, rules);
            match.Start();
            match.Fire(0);
            Assert.That(match.ShotLanded(0, 0, 0), Is.EqualTo(10));
            Assert.That(match.Phase, Is.EqualTo(MatchPhase.RoundOver));
            Assert.That(match.Winner, Is.EqualTo(1));
        }

        [Test]
        public void ALeaverDropsOut_PassingTheTurnIfItWasTheirs()
        {
            Match match = Table(3);
            match.Start();
            match.Leave(0);
            Assert.That(match.Current, Is.EqualTo(1));
            Assert.That(match.Seat(0).Alive, Is.False);

            match.Leave(2);
            Assert.That(match.Phase, Is.EqualTo(MatchPhase.RoundOver), "one left standing");
            Assert.That(match.Winner, Is.EqualTo(1));

            match.Tick(match.Rules.RoundOverSeconds + 0.01);
            Assert.That(match.Phase, Is.EqualTo(MatchPhase.Practice), "nobody to play: back to practice");
        }

        [Test]
        public void ALateJoinerSitsOutTheRound_AndPlaysTheNext()
        {
            var rules = new MatchRules { MaxHealth = 10 };
            Match match = Table(2, rules);
            match.Start();
            MatchSeat late = match.Join(match.FreeSeat(), SeatKind.Human, "Late");
            Assert.That(late.Index, Is.EqualTo(2));
            Assert.That(late.Alive, Is.False);
            Assert.That(match.TurnOrder(), Is.EqualTo(new[] { 0, 1 }));

            match.Fire(0);
            match.ShotLanded(0, 1, 0);
            match.Tick(rules.RoundOverSeconds + 0.01);
            Assert.That(late.Alive, Is.True);
            Assert.That(match.TurnOrder(), Is.EqualTo(new[] { 0, 1, 2 }));
        }

        [Test]
        public void TheTurnOrderStartsWithTheCurrentSeat()
        {
            Match match = Table(5);
            match.Start();
            PlayTurn(match, 0);
            PlayTurn(match, 1);
            match.Leave(4);
            Assert.That(match.TurnOrder(), Is.EqualTo(new[] { 2, 3, 0, 1 }));
        }

        [Test]
        public void TheClockReadsLikeDrawTurntime()
        {
            Assert.That(Seating.FormatClock(20), Is.EqualTo("20.00"));
            Assert.That(Seating.FormatClock(4.371), Is.EqualTo("04.37"));
            Assert.That(Seating.FormatClock(0.05), Is.EqualTo("00.05"));
            Assert.That(Seating.FormatClock(-1), Is.EqualTo("00.00"));
            Assert.That(Seating.FormatClock(double.NaN), Is.EqualTo("00.00"));
        }

        [Test]
        public void SixteenSpawnPointsInsideTheLevel_ApartFromEachOtherAndClearOfEveryWall()
        {
            Level level = Level.Default();
            var spots = Enumerable.Range(0, MatchRules.MaxSeats).Select(Seating.SpawnPoint).ToList();
            for (int i = 0; i < spots.Count; i++)
            {
                Vec2 p = spots[i];
                Assert.That(p.X - level.MinX > 0.8 && level.MaxX - p.X > 0.8 && p.Y - level.MinY > 0.8 && level.MaxY - p.Y > 0.8, Is.True,
                    "seat " + i + " at " + p + " is too near the border");
                for (int j = 0; j < i; j++)
                {
                    Assert.That(Vec2.Distance(p, spots[j]), Is.GreaterThan(1.4), "seats " + j + " and " + i);
                }

                foreach (Wall wall in level.Walls)
                {
                    Assert.That(SegmentDistance(p, wall.A, wall.B), Is.GreaterThan(0.8), "seat " + i + " against a wall");
                }

                foreach (Redirector redirector in level.Redirectors)
                {
                    Assert.That(SegmentDistance(p, redirector.A, redirector.B), Is.GreaterThan(0.8), "seat " + i + " against a redirector");
                }
            }

            Assert.That(Seating.SpawnPoint(16), Is.EqualTo(spots[0]), "indices wrap");
        }

        [Test]
        public void SixteenBarSlots_FourEachSideOfTheTimer_InTwoRows()
        {
            Seating.BarSlot(3, out int column, out int row);
            Assert.That((column, row), Is.EqualTo((3, 0)));
            Seating.BarSlot(12, out column, out row);
            Assert.That((column, row), Is.EqualTo((4, 1)));
            Seating.BarSlot(99, out column, out row);
            Assert.That((column, row), Is.EqualTo((7, 1)), "clamped to the sixteenth");
        }

        [Test]
        public void TheStandInAiAimsAtTheNearestOpponentItCanReach_NeverItself()
        {
            Level level = Level.Default();
            Vec2 me = Seating.SpawnPoint(0);
            var targets = new List<Target>
            {
                new Target("seat:0", me, Seating.PlayerRadius),
                new Target("seat:1", Seating.SpawnPoint(1), Seating.PlayerRadius),
                new Target("seat:4", Seating.SpawnPoint(4), Seating.PlayerRadius),
            };

            double heading = SimpleAi.ChooseHeading(me, "seat:0", level, targets, ShotRules.Default, new Random(1), out string expected);
            Assert.That(expected, Is.EqualTo("seat:1"), "seat 1 is nearer than seat 4");
            ShotPath path = BounceSolver.Solve(me, heading, SimpleAi.Power, level, targets);
            Assert.That(path.TargetId, Is.EqualTo("seat:1"));
        }

        private static void PlayTurn(Match match, int seat)
        {
            Assert.That(match.Current, Is.EqualTo(seat));
            Assert.That(match.Fire(seat), Is.True);
            match.ShotLanded(seat, -1, 0);
            match.Tick(match.Rules.EndTurnSeconds + 0.01);
        }

        private static double SegmentDistance(Vec2 p, Vec2 a, Vec2 b)
        {
            Vec2 ab = b - a;
            double t = Math.Max(0, Math.Min(1, Vec2.Dot(p - a, ab) / ab.LengthSquared));
            return Vec2.Distance(p, a + ab * t);
        }
    }
}
