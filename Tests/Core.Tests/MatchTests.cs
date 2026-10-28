using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Ricochet.Core.Tests
{
    using Ricochet.Core.Ai;
    using Ricochet.Core.Aim;
    using Ricochet.Core.Arsenal;
    using Ricochet.Core.Level;
    using Ricochet.Core.Match;
    public class MatchTests
    {
        private static Match Table(int seats, MatchRules rules = null)
        {
            var match = new Match(NoReadyCard.Rules(rules), seed: 7);
            for (int i = 0; i < seats; i++)
            {
                match.Join(i, i % 2 == 0 ? SeatKind.Human : SeatKind.Ai, "P" + i);
            }

            return match;
        }

        // U.RICO-T107: obj_endturn's alarm of 50 steps, re-armed for another 50 while the announcement still plays.
        [Test]
        public void TheNextTurnWaitsFiftyStepsAndOutWaitsTheAnnouncementInWholeAlarms()
        {
            Match match = Table(2);
            match.Start();
            double alarm = match.Rules.EndTurnSeconds;
            Assert.That(alarm, Is.EqualTo(50.0 / 60).Within(1e-9), "50 steps at 60 a second");
            Assert.That(match.EndTurnWait(0), Is.EqualTo(alarm));
            Assert.That(match.EndTurnWait(alarm * 2.2), Is.EqualTo(alarm * 3).Within(1e-9));

            Assert.That(match.Fire(match.Current), Is.True);
            int shooter = match.Current;
            match.HoldTurnEnd(alarm * 1.5); // said as the shot landed
            match.ShotLanded(shooter, -1, 0);
            match.Tick(alarm + 0.01);
            Assert.That(match.Current, Is.EqualTo(shooter), "still speaking: the alarm re-arms");
            match.Tick(alarm);
            Assert.That(match.Current, Is.Not.EqualTo(shooter));

            Assert.That(match.Fire(match.Current), Is.True);
            match.ShotLanded(match.Current, -1, 0);
            int second = match.Current;
            match.Tick(alarm + 0.01);
            Assert.That(match.Current, Is.Not.EqualTo(second), "a new turn forgets the last announcement");
        }

        [Test]
        public void EightSeatsHumanAndAiMixed_NoNinth()
        {
            Match match = Table(8);
            Assert.That(match.SeatCount, Is.EqualTo(8));
            Assert.That(match.Seats.Count(s => s.Kind == SeatKind.Ai), Is.EqualTo(4));
            Assert.That(match.FreeSeat(), Is.EqualTo(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => match.Join(8, SeatKind.Human, "ninth"));
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

            Assert.That(match.Phase, Is.EqualTo(MatchPhase.Ready), "the Ready card first");
            Assert.That(match.Round, Is.EqualTo(1));
            Assert.That(match.Current, Is.EqualTo(2));
            Assert.That(match.TimeLeft, Is.EqualTo(20));
            Assert.That(match.Seats.All(s => s.Health == 100 && s.Alive), Is.True);
            match.Tick(match.Rules.ReadySeconds + 0.01);
            Assert.That(match.Phase, Is.EqualTo(MatchPhase.Aiming));
        }

        [Test]
        public void TheReadyCardHoldsEightyStepsAndFadesFifty_AtSixtyASecond()
        {
            MatchRules rules = MatchRules.Default;
            Assert.That(rules.ReadyHoldSeconds, Is.EqualTo(80.0 / 60).Within(1e-12), "obj_ready's alarm[0] = 80");
            Assert.That(rules.ReadyFadeSeconds, Is.EqualTo(50.0 / 60).Within(1e-12), "alpha -= 0.02 a step");
            Assert.That(rules.ReadySeconds, Is.EqualTo(130.0 / 60).Within(1e-12));
        }

        [Test]
        public void EveryTurnOpensWithTheReadyCard_TheClockStandsFull_NobodyActs_ThenTheClockStarts()
        {
            var match = new Match(seed: 7);
            match.Join(0, SeatKind.Human, "Ann");
            match.Join(1, SeatKind.Ai, "Bot");
            var started = new List<int>();
            var clocks = new List<int>();
            match.TurnStarted += started.Add;
            match.ClockStarted += clocks.Add;
            match.Start();

            Assert.That(started, Is.EqualTo(new[] { 0 }));
            Assert.That(clocks, Is.Empty, "the clock waits for the card");
            Assert.That(match.Phase, Is.EqualTo(MatchPhase.Ready));
            Assert.That(match.MayFire(0), Is.False, "turnStarted is false while the card is up");
            Assert.That(match.Fire(0), Is.False);
            Assert.That(match.Move(0), Is.False);
            Assert.That(match.EndTurn(0), Is.False);

            match.Tick(match.Rules.ReadySeconds - 0.05);
            Assert.That(match.Phase, Is.EqualTo(MatchPhase.Ready));
            Assert.That(match.TimeLeft, Is.EqualTo(20), "the clock does not run during the hold");

            match.Tick(0.1);
            Assert.That(match.Phase, Is.EqualTo(MatchPhase.Aiming));
            Assert.That(clocks, Is.EqualTo(new[] { 0 }));
            Assert.That(match.TimeLeft, Is.EqualTo(20), "the hold's leftover is not taken off the clock");
            match.Tick(1);
            Assert.That(match.TimeLeft, Is.EqualTo(19).Within(1e-9));

            // The next turn gets its own card: after the shot's pause the AI's card goes up, and its clock waits too.
            Assert.That(match.Fire(0), Is.True);
            match.ShotLanded(0, -1, 0);
            match.Tick(match.Rules.EndTurnSeconds + 0.01);
            Assert.That(match.Current, Is.EqualTo(1));
            Assert.That(match.Phase, Is.EqualTo(MatchPhase.Ready));
            Assert.That(clocks, Is.EqualTo(new[] { 0 }));
            match.Tick(match.Rules.ReadySeconds + 0.01);
            Assert.That(clocks, Is.EqualTo(new[] { 0, 1 }));
        }

        [Test]
        public void ALeaverDuringTheirReadyCard_PassesTheTurn_AndWithNoHoldTheClockStartsAtOnce()
        {
            var match = new Match(seed: 7);
            match.Join(0, SeatKind.Human, "Ann");
            match.Join(1, SeatKind.Human, "Bob");
            match.Join(2, SeatKind.Human, "Cy");
            match.Start();
            match.Leave(0);
            Assert.That(match.Current, Is.EqualTo(1));
            Assert.That(match.Phase, Is.EqualTo(MatchPhase.Ready), "Bob's own card");

            Match instant = Table(2);
            var clocks = new List<int>();
            instant.ClockStarted += clocks.Add;
            instant.Start();
            Assert.That(instant.Phase, Is.EqualTo(MatchPhase.Aiming));
            Assert.That(clocks, Is.EqualTo(new[] { 0 }));
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
        public void ADroppedPlayerOnTheirTurn_KeepsSeatAndHealth_TheClockWaits_AndTheyResumeTheSameTurn()
        {
            Match match = Table(3); // humans at 0 and 2, an AI at 1
            match.Start();
            match.Fire(0);
            match.ShotLanded(0, 2, 0);
            int hurt = match.Seat(2).Health;
            Assert.That(hurt, Is.LessThan(100));
            match.Tick(match.Rules.EndTurnSeconds + 0.01);
            match.Fire(1);
            match.ShotOver(1);
            match.Tick(match.Rules.EndTurnSeconds + 0.01);
            Assert.That(match.Current, Is.EqualTo(2));
            match.Tick(5);

            var dropped = new List<int>();
            var back = new List<int>();
            match.Dropped += dropped.Add;
            match.Rejoined += back.Add;
            Assert.That(match.Drop(2, 60), Is.True);
            Assert.That(dropped, Is.EqualTo(new[] { 2 }));
            Assert.That(match.WaitingForCurrent, Is.True);
            Assert.That(match.MayFire(2), Is.False, "nobody fires for them while they are away");

            match.Tick(30); // longer than a whole turn
            Assert.That(match.Current, Is.EqualTo(2), "the turn waits");
            Assert.That(match.TimeLeft, Is.EqualTo(15).Within(1e-9), "the clock stopped where they dropped");
            Assert.That(match.Seat(2).AwayLeft, Is.EqualTo(30).Within(1e-9));
            Assert.That(match.Seat(2).Health, Is.EqualTo(hurt), "health held");
            Assert.That(match.Seat(2).Alive, Is.True);
            Assert.That(match.TurnOrder(), Is.EqualTo(new[] { 2, 0, 1 }), "still in the turn order");

            Assert.That(match.Rejoin(2), Is.True);
            Assert.That(back, Is.EqualTo(new[] { 2 }));
            Assert.That(match.Rejoin(2), Is.False, "already back");
            Assert.That(match.WaitingForCurrent, Is.False);
            Assert.That(match.Current, Is.EqualTo(2), "the same turn");
            Assert.That(match.MayFire(2), Is.True);
            match.Tick(1);
            Assert.That(match.TimeLeft, Is.EqualTo(14).Within(1e-9), "the clock runs on from where it stopped");
        }

        [Test]
        public void ADroppedPlayerOffTheirTurn_StaysInTheRound_AndTheTurnWaitsWhenItComesToThem()
        {
            Match match = Table(3);
            match.Start();
            Assert.That(match.Drop(2, 60), Is.True);
            Assert.That(match.WaitingForCurrent, Is.False, "seat 0's turn goes on");
            match.Fire(0);
            match.ShotLanded(0, 2, 0);
            Assert.That(match.Seat(2).Health, Is.LessThan(100), "an away seat can still be hit");
            match.Tick(match.Rules.EndTurnSeconds + 0.01);
            match.Fire(1);
            match.ShotOver(1);
            match.Tick(match.Rules.EndTurnSeconds + 0.01);

            Assert.That(match.Current, Is.EqualTo(2));
            Assert.That(match.WaitingForCurrent, Is.True);
            match.Tick(match.Rules.TurnSeconds + 1);
            Assert.That(match.Current, Is.EqualTo(2), "no time-out while they are away");
            Assert.That(match.TimeLeft, Is.EqualTo(match.Rules.TurnSeconds));
        }

        [Test]
        public void ADroppedPlayerWhoDoesNotComeBackLeaves_PassingTheTurn()
        {
            Match match = Table(3);
            match.Start();
            match.Drop(0, 60);
            match.Tick(10);
            Assert.That(match.Current, Is.EqualTo(0));

            match.Leave(0); // the kit gave the seat up
            Assert.That(match.Seat(0).Away, Is.False);
            Assert.That(match.Seat(0).Alive, Is.False);
            Assert.That(match.Current, Is.EqualTo(1));
            Assert.That(match.Rejoin(0), Is.False, "too late to come back to the match seat");
        }

        [Test]
        public void OnlyAPresentHumanCanDrop()
        {
            Match match = Table(3);
            match.Start();
            Assert.That(match.Drop(1, 60), Is.False, "an AI never drops");
            Assert.That(match.Drop(5, 60), Is.False, "nobody in seat 5");
            Assert.That(match.Drop(0, 60), Is.True);
            Assert.That(match.Drop(0, 60), Is.False, "already away");
            match.Leave(2);
            Assert.That(match.Drop(2, 60), Is.False, "gone already");
            Assert.That(match.Seat(0).ToString(), Does.EndWith(", away)"));
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
        public void EightBarSlots_FourEachSideOfTheTimer_InOneRow()
        {
            Seating.BarSlot(3, out int column, out int row);
            Assert.That((column, row), Is.EqualTo((3, 0)));
            Seating.BarSlot(4, out column, out row);
            Assert.That((column, row), Is.EqualTo((4, 0)));
            Seating.BarSlot(99, out column, out row);
            Assert.That((column, row), Is.EqualTo((7, 0)), "clamped to the eighth");
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
