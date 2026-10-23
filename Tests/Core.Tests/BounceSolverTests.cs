using System.Linq;
using NUnit.Framework;

namespace Ricochet.Core.Tests
{
    using Ricochet.Core.Aim;
    using Ricochet.Core.Level;
    public class BounceSolverTests
    {
        // A plain 20 x 10 box, and rules with a long range so the bounce limit, not power, ends a shot.
        private static Level Box(Wall[] walls = null, Redirector[] redirectors = null) => new Level(-10, -5, 10, 5, walls, redirectors);

        private static ShotRules LongRange => new ShotRules { MaxRange = 1000, MuzzleOffset = 0.5 };

        private static void AssertNear(Vec2 actual, double x, double y, string what = null)
        {
            Assert.That(actual.X, Is.EqualTo(x).Within(1e-6), what);
            Assert.That(actual.Y, Is.EqualTo(y).Within(1e-6), what);
        }

        [Test]
        public void AShotStartsAtTheMuzzle_AlongTheAim()
        {
            ShotPath path = BounceSolver.Solve(new Vec2(1, 1), 90, 1, Box(), null, LongRange);
            AssertNear(path.Start, 1, 1.5);
        }

        [Test]
        public void ABorderFlipsTheShot_AndTheShotDetonatesOnTheContactThatReachesTheLimit()
        {
            ShotPath path = BounceSolver.Solve(Vec2.Zero, 0, 1, Box(), null, LongRange);

            Assert.That(path.End, Is.EqualTo(ShotEndKind.BounceLimit));
            Assert.That(path.Contacts, Has.Count.EqualTo(5), "maxBounces = 5 contacts");
            Assert.That(path.Contacts.Select(c => c.Point.X), Is.EqualTo(new[] { 10.0, -10.0, 10.0, -10.0, 10.0 }).Within(1e-9));
            Assert.That(path.Contacts.All(c => c.Kind == ContactKind.Border));
            Assert.That(path.Contacts.Take(4).All(c => c.Deflected), Is.True);
            Assert.That(path.Contacts[4].Deflected, Is.False, "the fifth contact is the detonation");
            Assert.That(path.Bounces, Is.EqualTo(4));
            AssertNear(path.Impact, 10, 0);
            Assert.That(path.Length, Is.EqualTo(9.5 + 4 * 20).Within(1e-9));
        }

        [Test]
        public void ARaisedBounceLimitLetsTheShotFlyOn()
        {
            var rules = LongRange;
            rules.MaxBounces = 2;
            Assert.That(BounceSolver.Solve(Vec2.Zero, 0, 1, Box(), null, rules).Contacts, Has.Count.EqualTo(2));
        }

        [Test]
        public void AShotIntoACornerFlipsBothWays()
        {
            // From (0,0) at 45 degrees through a 10 x 10 box: straight into the (5,5) corner and straight back out.
            var level = new Level(-5, -5, 5, 5);
            ShotPath path = BounceSolver.Solve(Vec2.Zero, 45, 1, level, null, LongRange);

            AssertNear(path.Contacts[0].Point, 5, 5);
            Assert.That(path.Contacts[0].HeadingOut, Is.EqualTo(225).Within(1e-6));
            AssertNear(path.Contacts[1].Point, -5, -5);
        }

        [Test]
        public void ABorderBounceKeepsTheAngleOfIncidence()
        {
            ShotPath path = BounceSolver.Solve(Vec2.Zero, 30, 1, Box(), null, LongRange);
            Contact first = path.Contacts[0];
            Assert.That(first.Point.Y, Is.EqualTo(5).Within(1e-9), "the ceiling first");
            Assert.That(first.HeadingIn, Is.EqualTo(30).Within(1e-9));
            Assert.That(first.HeadingOut, Is.EqualTo(Geometry.ReflectAngle(30, 90)).Within(1e-9));
        }

        [Test]
        public void PowerIsRange_ASoftShotDiesInMidAir()
        {
            var rules = new ShotRules { MinRange = 3, MaxRange = 13, MuzzleOffset = 0.5 };
            ShotPath soft = BounceSolver.Solve(Vec2.Zero, 90, 0, Box(), null, rules);
            Assert.That(soft.End, Is.EqualTo(ShotEndKind.Spent));
            Assert.That(soft.Length, Is.EqualTo(3).Within(1e-9));
            AssertNear(soft.Impact, 0, 3.5);

            ShotPath half = BounceSolver.Solve(Vec2.Zero, 90, 0.5, Box(), null, rules);
            Assert.That(half.Length, Is.EqualTo(8).Within(1e-9));
            Assert.That(half.Contacts, Has.Count.EqualTo(1), "4.5 up to the ceiling, then 3.5 back down");
            AssertNear(half.Impact, 0, 1.5);
        }

        [Test]
        public void PowerOutsideZeroToOneIsClamped()
        {
            var rules = new ShotRules { MinRange = 3, MaxRange = 13 };
            Assert.That(BounceSolver.Solve(Vec2.Zero, 0, 7, Box(), null, rules).Length, Is.EqualTo(13).Within(1e-9));
            Assert.That(BounceSolver.Solve(Vec2.Zero, 0, double.NaN, Box(), null, rules).Length, Is.EqualTo(3).Within(1e-9));
        }

        [Test]
        public void AWallReflectsTheShotOffItsNormal()
        {
            var wall = new Wall(new Vec2(-5, 2), new Vec2(5, 2));
            ShotPath path = BounceSolver.Solve(Vec2.Zero, 90, 1, Box(new[] { wall }), null, LongRange);

            Assert.That(path.Contacts[0].Kind, Is.EqualTo(ContactKind.Wall));
            Assert.That(path.Contacts[0].Index, Is.EqualTo(0));
            AssertNear(path.Contacts[0].Point, 0, 2);
            Assert.That(path.Contacts[0].HeadingOut, Is.EqualTo(270).Within(1e-9));
            AssertNear(path.Contacts[1].Point, 0, -5, "back down to the floor");
        }

        [Test]
        public void ASlantedWallTurnsAShotAtRightAngles()
        {
            // A 45-degree mirror: a shot going right leaves going up.
            var wall = new Wall(new Vec2(2, -2), new Vec2(6, 2));
            ShotPath path = BounceSolver.Solve(new Vec2(0, 0), 0, 1, Box(new[] { wall }), null, LongRange);
            AssertNear(path.Contacts[0].Point, 4, 0);
            Assert.That(path.Contacts[0].HeadingOut, Is.EqualTo(90).Within(1e-9));
        }

        [Test]
        public void AShotPassesBesideTheEndOfAWall()
        {
            var wall = new Wall(new Vec2(1, 2), new Vec2(5, 2));
            ShotPath path = BounceSolver.Solve(Vec2.Zero, 90, 1, Box(new[] { wall }), null, LongRange);
            Assert.That(path.Contacts[0].Kind, Is.EqualTo(ContactKind.Border));
        }

        [Test]
        public void ARedirectorSendsTheShotAlongItsOwnHeading()
        {
            var redirector = new Redirector(new Vec2(3, -1), new Vec2(3, 1), 90);
            ShotPath path = BounceSolver.Solve(Vec2.Zero, 0, 1, Box(null, new[] { redirector }), null, LongRange);

            Assert.That(path.Contacts[0].Kind, Is.EqualTo(ContactKind.Redirector));
            AssertNear(path.Contacts[0].Point, 3, 0);
            Assert.That(path.Contacts[0].HeadingOut, Is.EqualTo(90).Within(1e-9));
            AssertNear(path.Contacts[1].Point, 3, 5, "straight up to the ceiling");
        }

        [Test]
        public void ATargetInTheWayEndsTheShot()
        {
            var targets = new[] { new Target("t", new Vec2(5, 0), 0.5) };
            ShotPath path = BounceSolver.Solve(Vec2.Zero, 0, 1, Box(), targets, LongRange);

            Assert.That(path.End, Is.EqualTo(ShotEndKind.Target));
            Assert.That(path.TargetId, Is.EqualTo("t"));
            AssertNear(path.Impact, 4.5, 0);
            Assert.That(path.Contacts, Is.Empty);
        }

        [Test]
        public void ATargetBehindAWallIsSafe_UntilTheShotBanksAroundIt()
        {
            var wall = new Wall(new Vec2(3, -1), new Vec2(3, 1));
            var targets = new[] { new Target("t", new Vec2(5, 0), 0.5) };
            ShotPath blocked = BounceSolver.Solve(Vec2.Zero, 0, 1, Box(new[] { wall }), targets, LongRange);
            Assert.That(blocked.Contacts[0].Kind, Is.EqualTo(ContactKind.Wall));
            Assert.That(blocked.End, Is.Not.EqualTo(ShotEndKind.Target));
        }

        [Test]
        public void ABankShotHitsATargetOffTheCeiling()
        {
            // 45 degrees from the centre meets the ceiling at (5,5) and comes down along (5+s, 5-s) through (7,3).
            var targets = new[] { new Target("t", new Vec2(7, 3), 0.3) };
            ShotPath path = BounceSolver.Solve(Vec2.Zero, 45, 1, Box(), targets, LongRange);

            Assert.That(path.End, Is.EqualTo(ShotEndKind.Target));
            Assert.That(path.Bounces, Is.EqualTo(1));
            AssertNear(path.Contacts[0].Point, 5, 5);
            Assert.That(Vec2.Distance(path.Impact, new Vec2(7, 3)), Is.EqualTo(0.3).Within(1e-9));
        }

        [Test]
        public void AMuzzleInsideATargetHitsItPointBlank()
        {
            var targets = new[] { new Target("t", new Vec2(0.5, 0), 0.5) };
            ShotPath path = BounceSolver.Solve(Vec2.Zero, 0, 1, Box(), targets, LongRange);
            Assert.That(path.End, Is.EqualTo(ShotEndKind.Target));
            Assert.That(path.Length, Is.EqualTo(0));
        }

        [Test]
        public void EveryShotOnTheDefaultLevelStaysInsideItsBorder()
        {
            Level level = Level.Default();
            var rules = new ShotRules();
            for (double heading = 0; heading < 360; heading += 0.7)
            {
                foreach (double power in new[] { 0.0, 0.5, 1.0 })
                {
                    ShotPath path = BounceSolver.Solve(new Vec2(0, -4), heading, power, level, null, rules);
                    foreach (Vec2 point in path.Points)
                    {
                        Assert.That(point.X, Is.InRange(level.MinX - 1e-6, level.MaxX + 1e-6), "heading " + heading);
                        Assert.That(point.Y, Is.InRange(level.MinY - 1e-6, level.MaxY + 1e-6), "heading " + heading);
                    }

                    Assert.That(path.Contacts.Count, Is.LessThanOrEqualTo(rules.MaxBounces));
                    Assert.That(path.Length, Is.LessThanOrEqualTo(rules.RangeFor(power) + 1e-6));
                }
            }
        }

        [Test]
        public void TheDefaultLevelHasEveryKindOfContact()
        {
            Level level = Level.Default();
            var seen = new System.Collections.Generic.HashSet<ContactKind>();
            for (double heading = 0; heading < 360; heading += 1)
            {
                foreach (Contact contact in BounceSolver.Solve(new Vec2(0, -4), heading, 1, level).Contacts)
                {
                    seen.Add(contact.Kind);
                }
            }

            Assert.That(seen, Is.EquivalentTo(new[] { ContactKind.Border, ContactKind.Wall, ContactKind.Redirector }));
        }

        [Test]
        public void TheSameShotSolvesTheSameEveryTime()
        {
            var targets = new[] { new Target("a", new Vec2(4, 3), 0.45), new Target("b", new Vec2(-7, -2), 0.45) };
            ShotPath first = BounceSolver.Solve(new Vec2(0, -4), 63.25, 0.8, Level.Default(), targets);
            ShotPath second = BounceSolver.Solve(new Vec2(0, -4), 63.25, 0.8, Level.Default(), targets);
            Assert.That(second.Points, Is.EqualTo(first.Points));
            Assert.That(second.End, Is.EqualTo(first.End));
        }

        [Test]
        public void FindHeading_ReturnsAnAimThatHitsTheTarget()
        {
            Level level = Level.Default();
            var targets = new[] { new Target("a", new Vec2(-7, 1), 0.45), new Target("b", new Vec2(7, -3), 0.45) };
            double? heading = BounceSolver.FindHeading(new Vec2(0, -4), 1, "a", level, targets);

            Assert.That(heading, Is.Not.Null);
            ShotPath path = BounceSolver.Solve(new Vec2(0, -4), heading.Value, 1, level, targets);
            Assert.That(path.TargetId, Is.EqualTo("a"));
        }

        // The game's seats (Arena.SpawnPoint's first row) and practice dummies (Arena.DummyStart), as of UR-T3.
        private static readonly Vec2[] Seats = { new Vec2(0, -4.5), new Vec2(-4, -4.5), new Vec2(4, -4.5), new Vec2(-8, -4.5), new Vec2(8, -4.5) };

        private static readonly Target[] Dummies =
        {
            new Target("dummy:0", new Vec2(-7, 1), 0.45), new Target("dummy:1", new Vec2(7, -3), 0.45), new Target("dummy:2", new Vec2(0, 3.5), 0.45),
        };

        [Test]
        public void EveryDummyOnTheDefaultLevelCanBeHitFromEverySeat_EvenAfterRoundingTheAimToAFloat()
        {
            foreach (Vec2 seat in Seats)
            {
                foreach (Target dummy in Dummies)
                {
                    double? heading = BounceSolver.FindHeading(seat, 1, dummy.Id, Level.Default(), Dummies);
                    Assert.That(heading, Is.Not.Null, dummy.Id + " from " + seat);
                    ShotPath path = BounceSolver.Solve(seat, (float)heading.Value, (float)1, Level.Default(), Dummies);
                    Assert.That(path.TargetId, Is.EqualTo(dummy.Id), dummy.Id + " from " + seat + " at " + heading);
                }
            }
        }

        [Test]
        public void FindHeading_IsNullForATargetNoShotCanReach()
        {
            var targets = new[] { new Target("outside", new Vec2(50, 50), 0.5) };
            Assert.That(BounceSolver.FindHeading(Vec2.Zero, 1, "outside", Box(), targets, LongRange, 5), Is.Null);
        }
    }
}
