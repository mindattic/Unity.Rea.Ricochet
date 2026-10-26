using System;
using NUnit.Framework;

namespace Ricochet.Core.Tests
{
    using Ricochet.Core.Art;

    // U.RICO-T47: draw_dottedline's dashes, the rail's fade and swirls, obj_laser's pulse timing, obj_grenade_explosion's
    // circle, obj_shieldwall's and obj_redirector's growth and vortex_step's modes.
    public class WeaponLooksTests
    {
        [Test]
        public void DashesStartAtTheStartAndRepeatEveryTwiceTheirLength()
        {
            var dashes = WeaponLooks.Dashes(30, 6);
            Assert.That(dashes, Has.Count.EqualTo(3));
            Assert.That(dashes[0], Is.EqualTo((0.0, 6.0)));
            Assert.That(dashes[1], Is.EqualTo((12.0, 18.0)));
            Assert.That(dashes[2], Is.EqualTo((24.0, 30.0)));
        }

        [Test]
        public void TheLastDashIsDrawnWholeEvenPastTheEnd()
        {
            var dashes = WeaponLooks.Dashes(13, 6); // the loop runs while m < len: 0 and 12
            Assert.That(dashes, Has.Count.EqualTo(2));
            Assert.That(dashes[1], Is.EqualTo((12.0, 18.0)));
        }

        [TestCase(0)]
        [TestCase(-5)]
        [TestCase(double.NaN)]
        public void NoLengthNoDashes(double length)
        {
            Assert.That(WeaponLooks.Dashes(length, 6), Is.Empty);
        }

        [Test]
        public void ASliceOfThePathRunsFromEndToEndThroughEveryCornerBetween()
        {
            // From the centre of a 20 x 10 box straight right: the muzzle at 0.5, the wall 9.5 on, then 20 per crossing.
            var rules = new Ricochet.Core.Aim.ShotRules { MaxRange = 1000, MuzzleOffset = 0.5, MaxBounces = 3 };
            var path = Ricochet.Core.Aim.BounceSolver.Solve(Ricochet.Core.Aim.Vec2.Zero, 0, 1, new Ricochet.Core.Level.Level(-10, -5, 10, 5), null, rules);
            var slice = new System.Collections.Generic.List<Ricochet.Core.Aim.Vec2>();

            path.Slice(5, 15, slice);
            Assert.That(slice, Has.Count.EqualTo(3));
            Assert.That(slice[0].X, Is.EqualTo(5.5).Within(1e-9));
            Assert.That(slice[1].X, Is.EqualTo(10).Within(1e-9));
            Assert.That(slice[2].X, Is.EqualTo(4.5).Within(1e-9));

            path.Slice(1, 2, slice);
            Assert.That(slice, Has.Count.EqualTo(2), "no corner between");

            path.Slice(7, 7, slice);
            Assert.That(slice, Is.Empty);

            path.Slice(-5, 1e9, slice);
            Assert.That(slice[0], Is.EqualTo(path.Start));
            Assert.That(slice[slice.Count - 1], Is.EqualTo(path.Impact));
            Assert.That(slice, Has.Count.EqualTo(path.Points.Count));
        }

        [Test]
        public void TheRailFadesFromPointEightByAHalfPercentAStep()
        {
            Assert.That(WeaponLooks.RailAlpha(0), Is.EqualTo(0.8).Within(1e-9));
            Assert.That(WeaponLooks.RailAlpha(60), Is.EqualTo(0.5).Within(1e-9));
            Assert.That(WeaponLooks.RailSteps, Is.EqualTo(160).Within(1e-9));
            Assert.That(WeaponLooks.RailAlpha(200), Is.EqualTo(0));
        }

        [Test]
        public void ASwirlStandsEveryFiftySixPixelsFromTheMuzzle()
        {
            var offsets = WeaponLooks.SwirlOffsets(200);
            Assert.That(offsets, Is.EqualTo(new[] { 0.0, 56.0, 112.0, 168.0 }));
        }

        [Test]
        public void TheFirstPulseComesFortyToSixtyStepsInThenEveryTwentyFiveToSeventyFive()
        {
            var dice = new Random(7);
            for (int i = 0; i < 500; i++)
            {
                Assert.That(WeaponLooks.PulseSteps(dice, true), Is.InRange(40, 60));
                Assert.That(WeaponLooks.PulseSteps(dice, false), Is.InRange(25, 75));
            }
        }

        [Test]
        public void TheGrenadeCircleGrowsTwoPixelsAStepToTheBlastRadius()
        {
            Assert.That(WeaponLooks.BlastRing(10, 150), Is.EqualTo(20));
            Assert.That(WeaponLooks.BlastRing(100, 150), Is.EqualTo(150));
            Assert.That(WeaponLooks.BlastSteps(150), Is.EqualTo(75));
        }

        [Test]
        public void AShieldWallFadesInToPointNine()
        {
            Assert.That(WeaponLooks.ShieldWallAlphaAt(50), Is.EqualTo(0.5).Within(1e-9));
            Assert.That(WeaponLooks.ShieldWallAlphaAt(500), Is.EqualTo(0.9).Within(1e-9));
        }

        [Test]
        public void ARedirectorGrowsFromThreeTenthsToFullSize()
        {
            Assert.That(WeaponLooks.RedirectorScaleAt(0), Is.EqualTo(0.3).Within(1e-9));
            Assert.That(WeaponLooks.RedirectorScaleAt(35), Is.EqualTo(0.65).Within(1e-9));
            Assert.That(WeaponLooks.RedirectorScaleAt(1000), Is.EqualTo(1));
        }

        [Test]
        public void TheShotStepRateIsAShellCoveringThreePixelsAStep()
        {
            // 18 units a second over 3 px of 22/1920 units: about 524 steps a second.
            Assert.That(WeaponLooks.ShotStepsPerSecond(18), Is.EqualTo(18 / (3 * 22.0 / 1920.0)).Within(1e-6));
            Assert.That(WeaponLooks.ShotStepsPerSecond(0), Is.EqualTo(60));
        }

        [Test]
        public void AVortexInFlightGrowsFromAQuarterToFullSize()
        {
            var vortex = new Vortex();
            Assert.That(vortex.Flying, Is.True);
            Assert.That(vortex.Scale, Is.EqualTo(0.25));
            for (int i = 0; i < 40; i++)
            {
                vortex.Step();
            }

            Assert.That(vortex.Scale, Is.EqualTo(0.65).Within(1e-9));
            for (int i = 0; i < 100; i++)
            {
                vortex.Step();
            }

            Assert.That(vortex.Scale, Is.InRange(1.0, 1.01));
        }

        [Test]
        public void ASetOffVortexSucksWaitsBlowsWaitsAndIsGone()
        {
            var vortex = new Vortex();
            vortex.Step();
            vortex.SetOff();
            Assert.That(vortex.Flying, Is.False);
            Assert.That(vortex.Mode, Is.EqualTo(VortexMode.Sucking));
            Assert.That(vortex.Scale, Is.EqualTo(0));

            int steps = 0;
            while (vortex.Mode == VortexMode.Sucking)
            {
                vortex.Step();
                steps++;
            }

            Assert.That(steps, Is.InRange(500, 502)); // 0.002 a step to 1
            Assert.That(vortex.Scale, Is.EqualTo(1));
            Assert.That(vortex.Angle, Is.EqualTo(-steps));
            Assert.That(vortex.ChildAngle, Is.EqualTo(90 + 4 * steps));
            Assert.That(vortex.Burst, Is.EqualTo(0));

            steps = 0;
            while (vortex.Mode == VortexMode.Waiting)
            {
                vortex.Step();
                steps++;
            }

            Assert.That(steps, Is.EqualTo(300));
            Assert.That(vortex.Mode, Is.EqualTo(VortexMode.Blowing));

            steps = 0;
            while (vortex.Mode == VortexMode.Blowing)
            {
                vortex.Step();
                steps++;
            }

            Assert.That(steps, Is.InRange(100, 102)); // 0.01 a step back to nothing
            Assert.That(vortex.Scale, Is.EqualTo(0));
            Assert.That(vortex.Burst, Is.EqualTo(10 * steps));

            double burst = vortex.Burst;
            steps = 0;
            while (!vortex.Done)
            {
                vortex.Step();
                steps++;
            }

            Assert.That(steps, Is.EqualTo(300));
            Assert.That(vortex.Burst, Is.EqualTo(burst + 3000));
        }
    }
}
