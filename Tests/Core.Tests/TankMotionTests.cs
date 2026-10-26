using NUnit.Framework;

namespace Ricochet.Core.Tests
{
    using Ricochet.Core.Art;

    // U.RICO-T45: align_bottom_to_top's hull turn, spawn_tracks' every-five-steps prints and obj_tracks' fade.
    public class TankMotionTests
    {
        [TestCase(0, 90, 1)]      // the turret ahead anticlockwise: the hull follows anticlockwise
        [TestCase(90, 0, 89)]     // and back
        [TestCase(10, 350, 9)]    // the short way across 0
        [TestCase(350, 10, 351)]
        [TestCase(359.5, 10, 0.5)] // wrapping through 360
        [TestCase(0, 180, 359)]   // exactly opposite: the original's comparisons go the long way (clockwise from 0)
        [TestCase(45, 45, 45)]    // aligned: no turn
        public void TheHullTurnsADegreeAStepTowardTheTurret(double hull, double turret, double expected)
        {
            Assert.That(TankMotion.TurnHull(hull, turret), Is.EqualTo(expected).Within(1e-9));
        }

        [Test]
        public void TheHullCatchesTheTurretAndStays()
        {
            double hull = 300;
            for (int i = 0; i < 100; i++)
            {
                hull = TankMotion.TurnHull(hull, 20);
            }

            Assert.That(hull, Is.EqualTo(20).Within(1e-9)); // 80 steps the short way, then held
        }

        [Test]
        public void APrintIsLaidTenStepsIntoTheFirstDriveThenEveryFive()
        {
            var layer = new TrackLayer();
            var laid = new System.Collections.Generic.List<int>();
            for (int step = 1; step <= 25; step++)
            {
                if (layer.Step(driving: true))
                {
                    laid.Add(step);
                }
            }

            Assert.That(laid, Is.EqualTo(new[] { 10, 15, 20, 25 }));
        }

        [Test]
        public void StandingStillLaysNothingAndKeepsTheCount()
        {
            var layer = new TrackLayer();
            for (int i = 0; i < 7; i++)
            {
                Assert.That(layer.Step(driving: true), Is.False);
            }

            for (int i = 0; i < 50; i++)
            {
                Assert.That(layer.Step(driving: false), Is.False);
            }

            Assert.That(layer.Step(driving: true), Is.False);
            Assert.That(layer.Step(driving: true), Is.False);
            Assert.That(layer.Step(driving: true), Is.True); // the tenth step of driving, though fifty stood between
        }

        [Test]
        public void APrintHoldsAtSevenTenthsForItsAlarmThenFadesThreeThousandthsAStep()
        {
            Assert.That(TankMotion.TrackAlphaAt(0), Is.EqualTo(0.7));
            Assert.That(TankMotion.TrackAlphaAt(400), Is.EqualTo(0.7));
            Assert.That(TankMotion.TrackAlphaAt(500), Is.EqualTo(0.4).Within(1e-9));
            Assert.That(TankMotion.TrackAlphaAt(634), Is.EqualTo(0).Within(1e-9));
            Assert.That(TankMotion.TrackLifeSteps, Is.EqualTo(635)); // 400 held, 234 fading, gone the step after
        }
    }
}
