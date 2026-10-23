using NUnit.Framework;

namespace Ricochet.Core.Tests
{
    public class GeometryTests
    {
        // LineLineIntersection_X.gml and _Y.gml, verbatim, as the reference the port must match.
        private static double GmlX(double a0, double a1, double a2, double a3, double a4, double a5, double a6, double a7) =>
            ((a0 * a3 - a1 * a2) * (a4 - a6) - (a0 - a2) * (a4 * a7 - a5 * a6)) / ((a0 - a2) * (a5 - a7) - (a1 - a3) * (a4 - a6));

        private static double GmlY(double a0, double a1, double a2, double a3, double a4, double a5, double a6, double a7) =>
            ((a0 * a3 - a1 * a2) * (a5 - a7) - (a1 - a3) * (a4 * a7 - a5 * a6)) / ((a0 - a2) * (a5 - a7) - (a1 - a3) * (a4 - a6));

        [Test]
        public void LineLineIntersection_CrossesTheDiagonalsOfASquareInItsMiddle()
        {
            Assert.That(Geometry.LineLineIntersection(new Vec2(0, 0), new Vec2(1, 1), new Vec2(0, 1), new Vec2(1, 0), out Vec2 p), Is.True);
            Assert.That(p.X, Is.EqualTo(0.5).Within(1e-12));
            Assert.That(p.Y, Is.EqualTo(0.5).Within(1e-12));
        }

        [TestCase(1.0, 2.0, 7.0, -3.0, -2.0, -4.0, 5.0, 6.5)]
        [TestCase(-10.0, 3.5, 4.0, 3.0, 0.0, -8.0, 0.25, 9.0)]
        [TestCase(2.0, 2.0, 3.0, 5.0, 100.0, 0.0, 101.0, 0.5)] // the crossing is far off both segments: lines are infinite
        public void LineLineIntersection_MatchesTheOriginalScripts(double x1, double y1, double x2, double y2, double x3, double y3, double x4, double y4)
        {
            Assert.That(Geometry.LineLineIntersection(new Vec2(x1, y1), new Vec2(x2, y2), new Vec2(x3, y3), new Vec2(x4, y4), out Vec2 p), Is.True);
            Assert.That(p.X, Is.EqualTo(GmlX(x1, y1, x2, y2, x3, y3, x4, y4)).Within(1e-9));
            Assert.That(p.Y, Is.EqualTo(GmlY(x1, y1, x2, y2, x3, y3, x4, y4)).Within(1e-9));
        }

        [Test]
        public void LineLineIntersection_ReportsParallelLinesInsteadOfDividingByZero()
        {
            Assert.That(Geometry.LineLineIntersection(new Vec2(0, 0), new Vec2(1, 1), new Vec2(0, 1), new Vec2(1, 2), out _), Is.False);
        }

        [Test]
        public void SegmentIntersection_GivesTheParametricPositionOnBothSegments()
        {
            Assert.That(Geometry.SegmentIntersection(new Vec2(0, 0), new Vec2(4, 0), new Vec2(1, -1), new Vec2(1, 3), true, out double t, out double u), Is.True);
            Assert.That(t, Is.EqualTo(0.25).Within(1e-12));
            Assert.That(u, Is.EqualTo(0.25).Within(1e-12));
        }

        [Test]
        public void SegmentIntersection_OnlyOnTheSegments_MissesACrossingBeyondAnEnd()
        {
            Assert.That(Geometry.SegmentIntersection(new Vec2(0, 0), new Vec2(4, 0), new Vec2(6, -1), new Vec2(6, 1), true, out _, out _), Is.False);
            Assert.That(Geometry.SegmentIntersection(new Vec2(0, 0), new Vec2(4, 0), new Vec2(6, -1), new Vec2(6, 1), false, out double t, out _), Is.True);
            Assert.That(t, Is.EqualTo(1.5).Within(1e-12));
        }

        [TestCase(30.0, 90.0, 330.0)]   // up-right off a floor or ceiling comes back down-right
        [TestCase(0.0, 180.0, 180.0)]   // straight into a wall comes straight back
        [TestCase(0.0, 135.0, 90.0)]    // right into a 45-degree mirror leaves straight up
        public void ReflectAngle_IsTheOriginalsTwoNormalMinus180MinusIncoming(double incoming, double normal, double expected)
        {
            Assert.That(Geometry.ReflectAngle(incoming, normal), Is.EqualTo(expected).Within(1e-9));
        }

        [TestCase(30.0, 90.0)]
        [TestCase(10.0, 135.0)]
        [TestCase(250.0, 20.0)]
        public void Reflect_AgreesWithReflectAngle(double incoming, double normal)
        {
            Vec2 reflected = Geometry.Reflect(Vec2.FromAngle(incoming), Vec2.FromAngle(normal));
            Assert.That(reflected.Angle, Is.EqualTo(Geometry.ReflectAngle(incoming, normal)).Within(1e-9));
        }

        [Test]
        public void RayCircle_FindsTheNearSideAndIgnoresCirclesBehind()
        {
            Assert.That(Geometry.RayCircle(new Vec2(0, 0), new Vec2(1, 0), new Vec2(5, 0), 1, out double d), Is.True);
            Assert.That(d, Is.EqualTo(4).Within(1e-12));
            Assert.That(Geometry.RayCircle(new Vec2(0, 0), new Vec2(1, 0), new Vec2(-5, 0), 1, out _), Is.False);
            Assert.That(Geometry.RayCircle(new Vec2(0, 0), new Vec2(1, 0), new Vec2(5, 2), 1, out _), Is.False);
            Assert.That(Geometry.RayCircle(new Vec2(5, 0.5), new Vec2(1, 0), new Vec2(5, 0), 1, out d), Is.True, "starting inside");
            Assert.That(d, Is.EqualTo(0));
        }

        [TestCase(-30.0, 330.0)]
        [TestCase(720.0, 0.0)]
        [TestCase(359.5, 359.5)]
        public void Angles_Normalize_WrapsIntoZeroTo360(double degrees, double expected)
        {
            Assert.That(Angles.Normalize(degrees), Is.EqualTo(expected).Within(1e-9));
        }
    }
}
