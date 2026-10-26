using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Ricochet.Core.Tests
{
    using Ricochet.Core.Aim;
    using Ricochet.Core.Art;
    using Ricochet.Core.Level;

    // U.RICO-T46: setup_backgrounds' floor pick, obj_lights' lights, obj_cloud_parent's drift and obj_spotlight's pulse, and
    // the shapes a random level lays for their sprites.
    public class BackdropTests
    {
        private static BackdropRules Rules => BackdropRules.Default;

        [Test]
        public void TheStockRulesAreTheOriginals()
        {
            Assert.That(Rules.Groups.Select(g => g.Count), Is.EqualTo(new[] { 16, 16, 5 }));
            Assert.That(Rules.Groups[0], Does.Contain("bg_dirt").And.Contain("bg_apple"));
            Assert.That(Rules.Groups[2], Is.EqualTo(new[] { "bg_potato", "bg_skin2", "bg_stucco", "bg_brownskin", "bg_cardboard" }));
            Assert.That(Rules.Groups.SelectMany(g => g), Has.None.EqualTo("bg_redslate"), "bg_redslate is in no group (its image is missing)");
            Assert.That(Rules.Clouds, Has.Count.EqualTo(8));
            Assert.That(Rules.CloudAlpha, Is.EqualTo(0.15));
            Assert.That(Rules.LightCount, Is.EqualTo(10));
            Assert.That((Rules.LightMinRadius, Rules.LightMaxRadius), Is.EqualTo((50, 300)));
            Assert.That((Rules.SpotlightMinAlpha, Rules.SpotlightMaxAlpha), Is.EqualTo((0.05, 0.15)));
            Assert.That((Rules.TopBorder, Rules.BottomBorder, Rules.LeftBorder, Rules.RightBorder),
                Is.EqualTo(("spr_top_border", "spr_bottom_border", "spr_left_border", "spr_right_border")));
        }

        [Test]
        public void TheSameSeedGivesTheSameBackdrop_AndRoundsDiffer()
        {
            BackdropPlan a = Backdrop.Plan(Backdrop.SeedFor(1234, 7));
            BackdropPlan b = Backdrop.Plan(Backdrop.SeedFor(1234, 7));
            Assert.That(b.Background, Is.EqualTo(a.Background));
            Assert.That(b.Lights, Is.EqualTo(a.Lights));

            var floors = new HashSet<string>();
            for (int version = 1; version <= 40; version++)
            {
                floors.Add(Backdrop.Plan(Backdrop.SeedFor(0, version)).Background); // a hand-laid map: seed 0, a new version a round
            }

            Assert.That(floors.Count, Is.GreaterThan(5), "every round of a hand-laid map got the same floor");
        }

        [Test]
        public void TheFloorComesFromItsGroup_AndEveryGroupIsChosen()
        {
            var groups = new int[Rules.Groups.Count];
            var seen = new HashSet<string>();
            for (int seed = 0; seed < 3000; seed++)
            {
                BackdropPlan plan = Backdrop.Plan(seed);
                Assert.That(Rules.Groups[plan.Group], Does.Contain(plan.Background));
                groups[plan.Group]++;
                seen.Add(plan.Background);
            }

            // choose(bg_group1, bg_group2, bg_group3): a third each, so a group-3 floor is far likelier than a group-1 one.
            Assert.That(groups, Has.All.InRange(850, 1150));
            Assert.That(seen, Is.EquivalentTo(Rules.Groups.SelectMany(g => g).Distinct()));
        }

        [Test]
        public void TenLights_OfRadius50To300_InColoursOf32To196_AllOverTheRoom()
        {
            var ys = new List<int>();
            for (int seed = 0; seed < 200; seed++)
            {
                BackdropPlan plan = Backdrop.Plan(seed);
                Assert.That(plan.Lights, Has.Count.EqualTo(10));
                foreach (BackdropLight light in plan.Lights)
                {
                    Assert.That(light.Radius, Is.InRange(50, 300));
                    Assert.That(new[] { light.R, light.G, light.B }, Has.All.InRange((byte)32, (byte)196));
                    Assert.That(light.X, Is.InRange(0, 1920));
                    Assert.That(light.Y, Is.InRange(0, 1080), "lightY rolls the room's height (the original rolled its width)");
                    ys.Add(light.Y);
                }
            }

            Assert.That(ys.Max(), Is.GreaterThan(1000));
        }

        [Test]
        public void ACloudDriftsAPixelAStep_AndComesBackInAtTheRight()
        {
            var dice = new Random(5);
            double x = 100, y = 304;
            Assert.That(Backdrop.DriftCloud(ref x, ref y, 851, 1, Rules, dice), Is.False);
            Assert.That((x, y), Is.EqualTo((99.0, 304.0)));

            x = -850;
            Assert.That(Backdrop.DriftCloud(ref x, ref y, 851, 1, Rules, dice), Is.False, "still a pixel of it in the room");
            Assert.That(Backdrop.DriftCloud(ref x, ref y, 851, 1, Rules, dice), Is.True);
            Assert.That(x, Is.EqualTo(1920));
            Assert.That(y, Is.InRange(-100, 980));
        }

        [Test]
        public void TheSpotlightSwingsBetween005And015()
        {
            var pulse = new SpotlightPulse(Rules);
            Assert.That(pulse.Alpha, Is.EqualTo(0.05));
            double low = 1, high = 0;
            int turns = 0;
            bool fading = pulse.Fading;
            for (int step = 0; step < 1000; step++)
            {
                pulse.Step();
                low = Math.Min(low, pulse.Alpha);
                high = Math.Max(high, pulse.Alpha);
                if (pulse.Fading != fading)
                {
                    turns++;
                    fading = pulse.Fading;
                }
            }

            Assert.That(low, Is.EqualTo(0.05).Within(0.0011));
            Assert.That(high, Is.EqualTo(0.15).Within(0.0011));
            Assert.That(turns, Is.GreaterThanOrEqualTo(9), "about 100 steps each way");
        }

        [Test]
        public void ARandomLevelRecordsTheShapesItLaid_WhereTheirWallsAre()
        {
            RoundLayout layout = LevelGenerator.Generate(77);
            Assert.That(layout.Shapes, Has.Count.EqualTo(layout.Polygons));
            int walls = 0;
            foreach (LaidShape shape in layout.Shapes)
            {
                Vec2[] outline = ShapeLibrary.Outline(shape.Id);
                double r = shape.Degrees * Math.PI / 180;
                for (int i = 0; i < outline.Length; i++)
                {
                    // The sprite drawn at Origin turned by Degrees covers the outline the walls follow.
                    double x = outline[i].X * Math.Cos(r) - outline[i].Y * Math.Sin(r) + shape.Origin.X;
                    double y = outline[i].X * Math.Sin(r) + outline[i].Y * Math.Cos(r) + shape.Origin.Y;
                    Assert.That(shape.Corners[i].X, Is.EqualTo(x).Within(1e-9));
                    Assert.That(shape.Corners[i].Y, Is.EqualTo(y).Within(1e-9));
                }

                walls += shape.Corners.Length;
            }

            Assert.That(layout.Level.Walls, Has.Count.EqualTo(walls));
            Assert.That(Maps.At(0).Layout(0).Shapes, Is.Empty, "a hand-laid map lays no shapes");
        }
    }
}
