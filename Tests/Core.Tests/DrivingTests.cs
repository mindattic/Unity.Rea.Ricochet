using NUnit.Framework;

namespace Ricochet.Core.Tests
{
    using Ricochet.Core.Aim;
    using Ricochet.Core.Level;
    using Ricochet.Core.Match;
    public class DrivingTests
    {
        private static readonly Level Open = new Level(-10, -5, 10, 5);

        private static Match Started()
        {
            var match = new Match(NoReadyCard.Rules(), seed: 7);
            match.Join(0, SeatKind.Human, "A");
            match.Join(1, SeatKind.Human, "B");
            match.Start();
            return match;
        }

        [Test]
        public void ForwardFollowsTheBarrel_BackwardIsTheOpposite()
        {
            Vec2 forward = Driving.Step(new Vec2(0, 0), 0, 1, 1, Open, 0.45);
            Assert.That(forward.X, Is.EqualTo(ShotRules.Default.DriveSpeed).Within(1e-9));
            Assert.That(forward.Y, Is.EqualTo(0).Within(1e-9));

            Vec2 back = Driving.Step(new Vec2(0, 0), 90, -1, 1, Open, 0.45);
            Assert.That(back.X, Is.EqualTo(0).Within(1e-9));
            Assert.That(back.Y, Is.EqualTo(-ShotRules.Default.DriveSpeed).Within(1e-9));

            Assert.That(Driving.Step(new Vec2(1, 1), 45, 0, 1, Open, 0.45), Is.EqualTo(new Vec2(1, 1)));
        }

        [Test]
        public void ATankStaysInsideTheBorder_AndOutOfWalls_SlidingAlongOneAxis()
        {
            Vec2 edge = Driving.Step(new Vec2(9.4, 0), 0, 1, 1, Open, 0.45);
            Assert.That(edge, Is.EqualTo(new Vec2(9.4, 0)), "straight into the border: stays");

            Vec2 slid = Driving.Step(new Vec2(9.4, 0), 45, 1, 0.1, Open, 0.45);
            Assert.That(slid.X, Is.EqualTo(9.4).Within(1e-9));
            Assert.That(slid.Y, Is.GreaterThan(0));

            var walled = new Level(-10, -5, 10, 5, new[] { new Wall(new Vec2(2, -3), new Vec2(2, 3)) });
            Assert.That(Driving.Step(new Vec2(1.0, 0), 0, 1, 0.2, walled, 0.45).X, Is.EqualTo(1.0).Within(1e-9), "a wall in the way");
        }

        [Test]
        public void DrivingForfeitsTheShot_UntilTheTurnPasses()
        {
            Match match = Started();
            int first = match.Current;
            Assert.That(match.Move(1 - first), Is.False, "not its turn");
            Assert.That(match.Move(first), Is.True);
            Assert.That(match.HasMoved, Is.True);
            Assert.That(match.Fire(first), Is.False, "cannot move and shoot in the same turn");
            Assert.That(match.Phase, Is.EqualTo(MatchPhase.Aiming));

            Assert.That(match.EndTurn(first), Is.True);
            Assert.That(match.Current, Is.EqualTo(1 - first));
            Assert.That(match.HasMoved, Is.False);
            Assert.That(match.Fire(1 - first), Is.True);
        }

        [Test]
        public void DrivingAgainInTheSameTurnStaysMoved_AndPracticeHasNoShotToForfeit()
        {
            Match match = Started();
            Assert.That(match.Move(match.Current), Is.True);
            Assert.That(match.Move(match.Current), Is.True);
            Assert.That(match.HasMoved, Is.True);

            var practice = new Match();
            Assert.That(practice.Move(0), Is.False, "Match.Move is for a running turn; practice drives freely in the Arena");
            Assert.That(practice.HasMoved, Is.False);
        }

        [Test]
        public void EndTurn_PassesTheTurnOnlyForTheSeatOnTurnBeforeItFires()
        {
            Match match = Started();
            int first = match.Current;
            Assert.That(match.EndTurn(1 - first), Is.False);
            Assert.That(match.Current, Is.EqualTo(first));

            match.Fire(first);
            Assert.That(match.EndTurn(first), Is.False, "a shot is in the air");
            Assert.That(new Match().EndTurn(0), Is.False, "no match");
        }
    }
}
