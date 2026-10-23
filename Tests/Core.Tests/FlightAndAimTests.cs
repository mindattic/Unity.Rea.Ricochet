using NUnit.Framework;

namespace Ricochet.Core.Tests
{
    public class FlightAndAimTests
    {
        private static ShotPath BorderToBorder()
        {
            // From the centre of a 20 x 10 box straight right: 9.5 to the wall, then 20 per crossing.
            var rules = new ShotRules { MaxRange = 1000, MuzzleOffset = 0.5, MaxBounces = 3 };
            return BounceSolver.Solve(Vec2.Zero, 0, 1, new Level(-10, -5, 10, 5), null, rules);
        }

        [Test]
        public void AFlightWalksThePath_CountingContacts_AndLandsOnTheImpact()
        {
            ShotPath path = BorderToBorder();
            var flight = new ProjectileFlight(path, 10);
            Assert.That(flight.Duration, Is.EqualTo(path.Length / 10).Within(1e-9));

            Assert.That(flight.Advance(0.5), Is.EqualTo(0));
            Assert.That(flight.Position.X, Is.EqualTo(5.5).Within(1e-9));
            Assert.That(flight.Heading, Is.EqualTo(0).Within(1e-9));

            Assert.That(flight.Advance(1.0), Is.EqualTo(1), "past the right border");
            Assert.That(flight.Position.X, Is.EqualTo(4.5).Within(1e-9), "15 along: 9.5 out to x = 10, 5.5 back");
            Assert.That(flight.Heading, Is.EqualTo(180).Within(1e-9));
            Assert.That(flight.Landed, Is.False);

            flight.Advance(100);
            Assert.That(flight.Landed, Is.True);
            Assert.That(flight.Contacts, Is.EqualTo(3));
            Assert.That(flight.Position, Is.EqualTo(path.Impact));
            Assert.That(flight.Advance(1), Is.EqualTo(0), "a landed shot stays put");
        }

        [Test]
        public void SmallStepsAndOneBigStepLandInTheSamePlace()
        {
            ShotPath path = BounceSolver.Solve(new Vec2(0, -4), 71, 0.9, Level.Default());
            var stepped = new ProjectileFlight(path, ShotRules.Default);
            int contacts = 0;
            while (!stepped.Landed)
            {
                contacts += stepped.Advance(1.0 / 60);
            }

            Assert.That(contacts, Is.EqualTo(path.Contacts.Count));
            Assert.That(stepped.Position, Is.EqualTo(path.Impact));
        }

        [Test]
        public void AimTurnsAndCharges_WrappingTheAngleAndClampingThePower()
        {
            var rules = new ShotRules { DegreesPerSecond = 90, PowerPerSecond = 0.5 };
            var aim = new AimState(heading: 350, power: 0.9);

            Assert.That(aim.Step(turn: 1, charge: 1, seconds: 0.5, rules), Is.True);
            Assert.That(aim.Heading, Is.EqualTo(35).Within(1e-9), "350 + 45 wraps to 35");
            Assert.That(aim.Power, Is.EqualTo(1));

            aim.Step(turn: -1, charge: -1, seconds: 1, rules);
            Assert.That(aim.Heading, Is.EqualTo(305).Within(1e-9));
            Assert.That(aim.Power, Is.EqualTo(0.5).Within(1e-9));

            Assert.That(aim.Step(0, 0, 1, rules), Is.False, "no keys, no change");
        }

        [Test]
        public void AimIgnoresOverdrivenOrGarbageInput()
        {
            var rules = new ShotRules { DegreesPerSecond = 90 };
            var aim = new AimState(heading: 0);
            aim.Step(turn: 50, charge: double.NaN, seconds: 1, rules);
            Assert.That(aim.Heading, Is.EqualTo(90).Within(1e-9), "a turn input above 1 counts as 1");
            Assert.That(aim.Power, Is.EqualTo(0.5));
        }
    }
}
