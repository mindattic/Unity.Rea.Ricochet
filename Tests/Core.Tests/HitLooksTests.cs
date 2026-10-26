using System;
using System.Linq;
using NUnit.Framework;

namespace Ricochet.Core.Tests
{
    using Ricochet.Core.Art;

    // U.RICO-T48: obj_float_text's colour cycle and fade, obj_scorchmark's fade, spawn_glass's shards and the GameMaker
    // effects as the rebuild draws them.
    public class HitLooksTests
    {
        [Test]
        public void AFlashingFloatTextCyclesWhiteRedYellowBlueBlue_ThenWhiteAgain()
        {
            GmlColor[] cycle = Enumerable.Range(1, 12).Select(HitLooks.FlashColorAt).ToArray();
            GmlColor[] once = { HitLooks.White, HitLooks.Red, HitLooks.Yellow, HitLooks.Blue, HitLooks.Blue, HitLooks.White };
            Assert.That(cycle.Take(6), Is.EqualTo(once), "colorCycle 1, 2, 3, 4, 5 (no case: still blue), then 0");
            Assert.That(cycle.Skip(6), Is.EqualTo(once), "and round again");
        }

        [Test]
        public void AFloatTextFadesAtItsSpeed_ShowFloatTextsInAHundredAndTwentyFiveSteps_TheQuickOnesInAHundred()
        {
            Assert.That(HitLooks.FloatLifeSteps(HitLooks.FloatFadePerStep), Is.EqualTo(125));
            Assert.That(HitLooks.FloatLifeSteps(HitLooks.FloatQuickFadePerStep), Is.EqualTo(100));
            Assert.That(HitLooks.FloatAlphaAt(0, HitLooks.FloatFadePerStep), Is.EqualTo(1));
            Assert.That(HitLooks.FloatAlphaAt(50, HitLooks.FloatQuickFadePerStep), Is.EqualTo(0.5).Within(1e-9));
            Assert.That(HitLooks.FloatAlphaAt(500, HitLooks.FloatQuickFadePerStep), Is.EqualTo(0));
        }

        [Test]
        public void TheTextsSayWhatHitGmlAndUseItemGmlSaid()
        {
            Assert.That(HitLooks.PointsText(125), Is.EqualTo("+125 pts"));
            Assert.That(HitLooks.DamageText(23), Is.EqualTo("-23"));
            Assert.That(HitLooks.ExtraBounceText, Is.EqualTo("Bounce +1"));
        }

        [Test]
        public void AScorchMarkFadesAWholeTenThousandSteps()
        {
            Assert.That(HitLooks.ScorchAlphaAt(0), Is.EqualTo(1));
            Assert.That(HitLooks.ScorchAlphaAt(5000), Is.EqualTo(0.5).Within(1e-9));
            Assert.That(HitLooks.ScorchAlphaAt(10000), Is.EqualTo(0).Within(1e-9));
        }

        [Test]
        public void SpawnGlassThrowsTwentyToThirtyShards_ScatteredTurningAndFlyingWithinTheOriginalsRanges()
        {
            var dice = new Random(11);
            bool[] seen = new bool[HitLooks.GlassSprites];
            for (int roll = 0; roll < 50; roll++)
            {
                var shards = HitLooks.SpawnGlass(dice);
                Assert.That(shards.Count, Is.InRange(HitLooks.GlassMin, HitLooks.GlassMax));
                foreach (GlassShard shard in shards)
                {
                    Assert.That(shard.OffsetX, Is.InRange(-20, 20));
                    Assert.That(shard.OffsetY, Is.InRange(-20, 20));
                    Assert.That(shard.Spin, Is.InRange(-2, 2));
                    Assert.That(shard.SpeedX, Is.InRange(-5, 5));
                    Assert.That(shard.SpeedY, Is.InRange(-5, 5));
                    seen[shard.Sprite] = true;
                }
            }

            Assert.That(seen, Is.All.True, "every one of spr_glass1-5 is drawn (the original never drew spr_glass5)");
            Assert.That(HitLooks.GlassLifeSteps, Is.EqualTo(50), "0.02 a step");
        }

        [Test]
        public void ObjFlameSmokesInARandomDarkGrey_AndObjPowerupSparksWhiteOrOrange()
        {
            var dice = new Random(5);
            for (int i = 0; i < 100; i++)
            {
                GmlColor smoke = HitLooks.FlameColor(dice);
                Assert.That(Math.Max(smoke.R, Math.Max(smoke.G, smoke.B)), Is.LessThanOrEqualTo(40));
                GmlColor spark = HitLooks.SparkColor(dice);
                Assert.That(spark, Is.EqualTo(HitLooks.White).Or.EqualTo(HitLooks.Orange));
            }
        }

        [Test]
        public void TheGameMakerEffectsGrowWithTheirSize_AndLiveAMomentOnly()
        {
            foreach (GmEffect effect in Enum.GetValues(typeof(GmEffect)))
            {
                GmEffectLook small = HitLooks.Look(effect, 0);
                GmEffectLook large = HitLooks.Look(effect, 2);
                Assert.That(small.Count, Is.GreaterThanOrEqualTo(1), effect.ToString());
                Assert.That(small.LifeMin, Is.InRange(1, small.LifeMax), effect.ToString());
                Assert.That(small.LifeMax, Is.LessThanOrEqualTo(60), effect + ": under a second");
                Assert.That(large.Size + large.SizeGrowth * large.LifeMax, Is.GreaterThanOrEqualTo(small.Size + small.SizeGrowth * small.LifeMax), effect.ToString());
                Assert.That(HitLooks.Look(effect, 9).Size, Is.EqualTo(large.Size), "past large is large");
            }

            Assert.That(HitLooks.Look(GmEffect.SmokeUp, 0).Upward, Is.True, "ef_smokeup goes up");
            Assert.That(HitLooks.Look(GmEffect.Firework, 0).Count, Is.GreaterThan(1), "a burst of sparks");
        }
    }
}
