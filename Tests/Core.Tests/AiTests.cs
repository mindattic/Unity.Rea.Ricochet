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
    public class AiTests
    {
        private static Level Box(params Wall[] walls) => new Level(-10, -5, 10, 5, walls);

        private static Target Me => new Target("seat:0", Vec2.Zero, Seating.PlayerRadius);

        // Four walls boxing (x, y) in: no shell gets in or out.
        private static Wall[] Cage(double x, double y) => new[]
        {
            new Wall(new Vec2(x - 1, y - 1), new Vec2(x + 1, y - 1)),
            new Wall(new Vec2(x + 1, y - 1), new Vec2(x + 1, y + 1)),
            new Wall(new Vec2(x + 1, y + 1), new Vec2(x - 1, y + 1)),
            new Wall(new Vec2(x - 1, y + 1), new Vec2(x - 1, y - 1)),
        };

        [Test]
        public void WithALineOfSightTheAiTakesTheStraightShot_AtTheNearestOpponent_NeverItself()
        {
            var targets = new List<Target> { Me, new Target("seat:1", new Vec2(6, 0), 0.45), new Target("seat:2", new Vec2(0, 3), 0.45) };
            AiPlan plan = Ai.Plan(Vec2.Zero, "seat:0", Box(), targets, Ai.MaxSkill, new Random(1));

            Assert.That(plan.Kind, Is.EqualTo(AiShotKind.Direct));
            Assert.That(plan.TargetId, Is.EqualTo("seat:2"), "3 away beats 6 away");
            Assert.That(plan.Weapon, Is.EqualTo(Weapon.Cannon));
            Assert.That(plan.Heading, Is.EqualTo(90).Within(0.5), "the middle of the headings that reach it");
            ShotPath path = BounceSolver.Solve(Vec2.Zero, plan.Heading, Box(), targets);
            Assert.That(path.TargetId, Is.EqualTo("seat:2"));
            Assert.That(path.Bounces, Is.EqualTo(0));
        }

        [Test]
        public void WithAWallInTheWayTheAiBanksItsShotOntoTheOpponent()
        {
            Level level = Box(new Wall(new Vec2(3, -2), new Vec2(3, 2)));
            var targets = new List<Target> { Me, new Target("seat:1", new Vec2(6, 0), 0.45) };
            AiPlan plan = Ai.Plan(Vec2.Zero, "seat:0", level, targets, Ai.MaxSkill, new Random(1));

            Assert.That(plan.Kind, Is.EqualTo(AiShotKind.Bank));
            Assert.That(plan.TargetId, Is.EqualTo("seat:1"));
            ShotPath path = BounceSolver.Solve(Vec2.Zero, plan.Heading, level, targets);
            Assert.That(path.TargetId, Is.EqualTo("seat:1"), plan.ToString());
            Assert.That(path.Bounces, Is.GreaterThan(0));
        }

        [Test]
        public void WhenNoShellCanReachAnyoneTheAiUsesItsRailgun_OrElseFiresBlind()
        {
            Level level = Box(Cage(6, 0));
            var targets = new List<Target> { Me, new Target("seat:1", new Vec2(6, 0), 0.45) };

            AiPlan railgun = Ai.Plan(Vec2.Zero, "seat:0", level, targets, Ai.MaxSkill, new Random(1), loadout: new Loadout());
            Assert.That(railgun.Kind, Is.EqualTo(AiShotKind.Railgun));
            Assert.That(railgun.Weapon, Is.EqualTo(Weapon.Railgun));
            Volley volley = Arsenal.Fire(Weapon.Railgun, Vec2.Zero, railgun.Heading, 0, 0, level, targets);
            Assert.That(volley.Strikes.Select(s => s.TargetId), Does.Contain("seat:1"));

            var empty = new ArsenalRules();
            empty.SetStartCount(Weapon.Railgun, 0);
            AiPlan blind = Ai.Plan(Vec2.Zero, "seat:0", level, targets, Ai.MaxSkill, new Random(1), loadout: new Loadout(empty));
            Assert.That(blind.Kind, Is.EqualTo(AiShotKind.Blind));
            Assert.That(blind.Found, Is.False);
            Assert.That(blind.Weapon, Is.EqualTo(Weapon.Cannon));
            Assert.That(blind.Heading, Is.InRange(0, 360));
        }

        [Test]
        public void SkillNudgesTheAimByUpTo10MinusSkillWholeDegrees()
        {
            var targets = new List<Target> { Me, new Target("seat:1", new Vec2(6, 0), 0.45) };
            var dice = new Random(3);
            var nudges = new HashSet<double>();
            for (int i = 0; i < 200; i++)
            {
                AiPlan plan = Ai.Plan(Vec2.Zero, "seat:0", Box(), targets, 7, dice);
                double nudge = Ai.Difference(plan.Aim, plan.Heading);
                Assert.That(Math.Abs(nudge), Is.LessThanOrEqualTo(3 + 1e-9));
                Assert.That(nudge, Is.EqualTo(Math.Round(nudge)).Within(1e-9), "whole degrees");
                nudges.Add(Math.Round(nudge));
            }

            Assert.That(nudges, Is.EquivalentTo(new[] { -3.0, -2.0, -1.0, 0.0, 1.0, 2.0, 3.0 }));
            AiPlan exact = Ai.Plan(Vec2.Zero, "seat:0", Box(), targets, Ai.MaxSkill, dice);
            Assert.That(exact.Heading, Is.EqualTo(exact.Aim), "skill 10 fires exactly where it aimed");
            Assert.That(Ai.Spread(-4), Is.EqualTo(10), "skill clamps to 0..10");
            Assert.That(Ai.Spread(99), Is.EqualTo(0));
        }

        [Test]
        public void ARunOfHittingHeadingsAcrossZeroDegreesIsAimedAtItsMiddle()
        {
            var targets = new List<Target> { Me, new Target("seat:1", new Vec2(6, 0), 0.45) };
            AiPlan plan = Ai.Aim(Vec2.Zero, "seat:0", Box(), targets);
            Assert.That(Math.Abs(Ai.Difference(0, plan.Aim)), Is.LessThan(0.5), "not 0 + half the run, and not 360 - half of it: " + plan.Aim);
        }

        [Test]
        public void AimAssistAimsLikeAFullySkilledAi()
        {
            var rules = new ShotRules();
            var near = new List<Target> { Me, new Target("seat:1", new Vec2(0, 1.5), 0.45) };
            AiPlan close = Ai.Assist(Vec2.Zero, "seat:0", Box(), near, rules);
            Assert.That(close.TargetId, Is.EqualTo("seat:1"));
            Assert.That(close.Heading, Is.EqualTo(close.Aim));

            var far = new List<Target> { Me, new Target("seat:1", new Vec2(8, 0), 0.45) };
            AiPlan stretched = Ai.Assist(Vec2.Zero, "seat:0", Box(), far, rules);
            Assert.That(stretched.TargetId, Is.EqualTo("seat:1"), "a shot has no range: it reaches anyone its bounces do");

            // (Off the whole numbers: a shot running exactly through the corner where two walls meet can slip between them.)
            var caged = new List<Target> { Me, new Target("seat:1", new Vec2(7.7, 0.3), 0.45) };
            Assert.That(Ai.Assist(Vec2.Zero, "seat:0", Box(Cage(7.7, 0.3)), caged, rules), Is.Null, "nobody in reach: no assist");
        }

        [Test]
        public void TheBarrelTurnsTheShorterWayRound()
        {
            Assert.That(Ai.TurnToward(350, 10, 5), Is.EqualTo(355).Within(1e-9));
            Assert.That(Ai.TurnToward(10, 350, 5), Is.EqualTo(5).Within(1e-9));
            Assert.That(Ai.TurnToward(358, 2, 5), Is.EqualTo(2).Within(1e-9));
            Assert.That(Ai.TurnToward(90, 270, 0), Is.EqualTo(90));
            Assert.That(Ai.Difference(0, 180), Is.EqualTo(180));
            Assert.That(Ai.Difference(10, 300), Is.EqualTo(-70).Within(1e-9));
        }

        [Test]
        public void AnAiTurnThinks_Plans_TurnsItsBarrel_ThenFires()
        {
            var turn = new AiTurn(2, 7, 1.0, 90);
            Assert.That(turn.Step(0.5, 0, 10, out _), Is.EqualTo(AiStep.Think));
            Assert.That(turn.Step(0.5, 0, 9.5, out _), Is.EqualTo(AiStep.Plan));
            turn.SetPlan(new AiPlan(AiShotKind.Direct, Weapon.Cannon, 90, 90, "seat:0"));

            double heading = 0;
            Assert.That(turn.Step(0.5, heading, 9, out heading), Is.EqualTo(AiStep.Turn));
            Assert.That(heading, Is.EqualTo(45).Within(1e-9), "90 degrees a second");
            Assert.That(turn.Step(0.5, heading, 8.5, out heading), Is.EqualTo(AiStep.Fire));
            Assert.That(heading, Is.EqualTo(90).Within(1e-9));
        }

        [Test]
        public void AnAiTurnsFasterWhenItsClockIsShort_AndFiresAtOnceWhenItHasRunOut()
        {
            var turn = new AiTurn(0, 0, 0, 10);
            turn.Step(0, 0, 1, out _);
            turn.SetPlan(new AiPlan(AiShotKind.Direct, Weapon.Cannon, 180, 180, "seat:1"));

            // 180 degrees with 1 second left: arrive in half of it (360 a second), not in 18 seconds at 10.
            Assert.That(turn.Step(0.25, 0, 1, out double heading), Is.EqualTo(AiStep.Turn));
            Assert.That(Math.Abs(Ai.Difference(0, heading)), Is.EqualTo(90).Within(1e-9));
            Assert.That(turn.Step(0.01, heading, 0, out heading), Is.EqualTo(AiStep.Fire));
            Assert.That(heading, Is.EqualTo(180));
        }

        [Test]
        public void ThereAreThirtyThinkingLines()
        {
            Assert.That(Ai.ThinkingLineCount, Is.EqualTo(30));
            Assert.That(Ai.ThinkingLine(12), Is.EqualTo(" is plotting and scheming..."));
            Assert.That(Ai.ThinkingLine(30), Is.EqualTo(Ai.ThinkingLine(0)), "wraps");
            Assert.That(Ai.ThinkingLine(-1), Is.EqualTo(Ai.ThinkingLine(29)));
        }

        [Test]
        public void AMatchSetupGoesOutAsAVotesNumberAndText_AndComesBackTheSame()
        {
            var setup = new MatchSetup { AiCount = 3, AiSkill = 8, AimAssists = 10 };
            MatchSetup back = MatchSetup.Parse(3, setup.Format());
            Assert.That((back.AiCount, back.AiSkill, back.AimAssists), Is.EqualTo((3, 8, 10)));
            Assert.That(setup.Describe(), Is.EqualTo("AI skill 8 of 10, unlimited aim assist"));

            MatchSetup defaults = MatchSetup.Parse(0, null);
            Assert.That((defaults.AiCount, defaults.AiSkill, defaults.AimAssists), Is.EqualTo((0, Ai.DefaultSkill, MatchSetup.DefaultAimAssists)));
            Assert.That(defaults.Describe(), Is.EqualTo("3 aim assists each"));

            MatchSetup clamped = MatchSetup.Parse(99, "skill=42;assist=-3;junk;color=red;skill");
            Assert.That((clamped.AiCount, clamped.AiSkill, clamped.AimAssists), Is.EqualTo((MatchRules.MaxSeats - 1, Ai.MaxSkill, 0)));
            Assert.That(clamped.Describe(), Is.EqualTo("AI skill 10 of 10, no aim assist"));
        }

        [Test]
        public void AMatchSetupGivesEveryoneItsAimAssists_OrTakesTheItemOutOfPlay()
        {
            var rules = new ArsenalRules();
            new MatchSetup { AimAssists = 5 }.ApplyTo(rules);
            Assert.That(new Loadout(rules).Count(Item.AimAssist), Is.EqualTo(5));
            Assert.That(rules.InPlay(Item.AimAssist), Is.True);

            new MatchSetup { AimAssists = 0 }.ApplyTo(rules);
            Assert.That(new Loadout(rules).Count(Item.AimAssist), Is.EqualTo(0));
            Assert.That(rules.InPlay(Item.AimAssist), Is.False, "no crate gives it either");
            var dice = new Random(4);
            Assert.That(Enumerable.Range(0, 200).Select(_ => Pickups.RandomItem(dice, rules)), Has.None.EqualTo(Item.AimAssist));
        }
    }
}
