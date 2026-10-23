using NUnit.Framework;

namespace Ricochet.Core.Tests
{
    // UR-T17: below ~1400px wide, MatchHud's four unscaled 150px bar columns left of the clock ran off the left
    // edge of a 1280x720 window (found by UR-T15). Seating.BarScale/BarX are the pure layout math MatchHud now
    // scales its bars with; these tests check the result always fits the screen and never shrinks on a wide one.
    public class SeatingTests
    {
        private const double BarWidth = 150;
        private const double BarGap = 8;
        private const double ClockGap = 150;

        [Test]
        public void BarScale_AtDesignWidthOrWider_IsFull()
        {
            // 4 columns a side: 4 * (150 + 8) * 2 + 150 = 1414, plus the 40px side padding BarScale defaults to.
            Assert.That(Seating.BarScale(1454, BarWidth, BarGap, ClockGap), Is.EqualTo(1).Within(1e-9));
            Assert.That(Seating.BarScale(1920, BarWidth, BarGap, ClockGap), Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void BarScale_Below1400_ShrinksBelowOne()
        {
            double scale = Seating.BarScale(1280, BarWidth, BarGap, ClockGap);
            Assert.That(scale, Is.LessThan(1));
            Assert.That(scale, Is.GreaterThan(0));
        }

        [TestCase(1280)]
        [TestCase(1024)]
        [TestCase(800)]
        [TestCase(640)]
        public void BarX_EveryColumn_StaysOnScreenAtNarrowWidths(double screenWidth)
        {
            double scale = Seating.BarScale(screenWidth, BarWidth, BarGap, ClockGap);
            double barWidth = BarWidth * scale;
            double barGap = BarGap * scale;
            double clockGap = ClockGap * scale;
            double centre = screenWidth / 2.0;

            for (int column = 0; column < Seating.BarsPerRow; column++)
            {
                double x = Seating.BarX(centre, column, barWidth, barGap, clockGap);
                Assert.That(x, Is.GreaterThanOrEqualTo(0), "column " + column + " left edge ran off screen at width " + screenWidth);
                Assert.That(x + barWidth, Is.LessThanOrEqualTo(screenWidth), "column " + column + " right edge ran off screen at width " + screenWidth);
            }
        }

        [Test]
        public void BarX_AtScaleOne_MatchesTheOriginalUnscaledLayout()
        {
            // Regression: at design width the scaled formula must reduce to the pre-UR-T17 placement exactly.
            double centre = 960;
            Assert.That(Seating.BarX(centre, 0, BarWidth, BarGap, ClockGap), Is.EqualTo(centre - ClockGap / 2 - 4 * (BarWidth + BarGap)).Within(1e-9));
            Assert.That(Seating.BarX(centre, 7, BarWidth, BarGap, ClockGap), Is.EqualTo(centre + ClockGap / 2 + 3 * (BarWidth + BarGap)).Within(1e-9));
        }

        [Test]
        public void BarScale_NeverGoesToZeroOrNegative()
        {
            Assert.That(Seating.BarScale(1, BarWidth, BarGap, ClockGap), Is.GreaterThan(0));
        }
    }
}
