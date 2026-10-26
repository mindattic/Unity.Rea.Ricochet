using NUnit.Framework;

namespace Ricochet.Core.Tests
{
    using Ricochet.Core.Art;

    // U.RICO-T44: the font pipeline's engine-free half (the glyph table, the SDF atlas, draw_text_outline's outline).
    public class GmlFontTests
    {
        private static GmlGlyph[] Sample() => new[]
        {
            new GmlGlyph('A', 0, 0, 8, 15, 9, 0),
            new GmlGlyph('M', 9, 0, 10, 15, 11, 1),
            new GmlGlyph('g', 20, 0, 7, 19, 8, 0), // a descender: the cell runs past the baseline
        };

        [Test]
        public void TheLineIsTheMCellAndTheDescentIsTheDeepestCellBelowIt()
        {
            Assert.That(GmlFontMetrics.LineHeight(Sample()), Is.EqualTo(15));
            Assert.That(GmlFontMetrics.Descent(Sample()), Is.EqualTo(-4));
        }

        [TestCase(11, 4)]  // fnt_small / fnt_count: 8 pt bitmaps are sampled up four times
        [TestCase(15, 4)]  // fnt_score
        [TestCase(31, 3)]  // fnt_mono
        [TestCase(40, 2)]  // fnt_impact
        [TestCase(200, 1)]
        public void SmallBitmapsAreSampledUpForTheirDistanceField(int line, int scale)
        {
            Assert.That(GmlFontMetrics.SamplingScale(line), Is.EqualTo(scale));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(4)]
        public void TheOutlineAndTheDilationTogetherNeverPassTheShadersFullRange(int scale)
        {
            // TextMeshPro keeps its scale ratio only while dilate + outline <= 1: past that the outline would shrink.
            for (int pixels = 1; pixels <= GmlFontMetrics.MaxOutlinePixels; pixels++)
            {
                float width = GmlFontMetrics.OutlineWidth(pixels, scale);
                Assert.That(2f * width, Is.LessThanOrEqualTo(1f));
                // The band spans width x padding texels: exactly the outline in GameMaker pixels, at the sampling scale.
                Assert.That(width * GmlFontMetrics.Padding(scale), Is.EqualTo(pixels * scale).Within(1e-4));
            }
        }

        [Test]
        public void PackingKeepsTheGapBetweenEveryCellAndRoundTheEdge()
        {
            PackedAtlas packed = GmlFontMetrics.Pack(Sample(), 5);
            for (int i = 0; i < packed.Glyphs.Length; i++)
            {
                GmlGlyph a = packed.Glyphs[i];
                Assert.That(a.X, Is.GreaterThanOrEqualTo(5));
                Assert.That(a.Y, Is.GreaterThanOrEqualTo(5));
                Assert.That(a.X + a.Width + 5, Is.LessThanOrEqualTo(packed.Width));
                Assert.That(a.Y + a.Height + 5, Is.LessThanOrEqualTo(packed.Height));
                Assert.That(a.Character, Is.EqualTo(Sample()[i].Character));
                for (int j = i + 1; j < packed.Glyphs.Length; j++)
                {
                    GmlGlyph b = packed.Glyphs[j];
                    bool apart = a.X + a.Width + 5 <= b.X || b.X + b.Width + 5 <= a.X || a.Y + a.Height + 5 <= b.Y || b.Y + b.Height + 5 <= a.Y;
                    Assert.That(apart, Is.True, $"{(char)a.Character} and {(char)b.Character} are closer than the gap");
                }
            }
        }

        [Test]
        public void TheSpriteFontGridIsReadInsideItsRules()
        {
            GmlGlyph[] glyphs = GmlFontMetrics.GridGlyphs(33, 94, 10, 20, 20, 40, 51, 1);
            Assert.That(glyphs.Length, Is.EqualTo(94));
            Assert.That(glyphs[0].Character, Is.EqualTo('!'));
            Assert.That((glyphs[0].X, glyphs[0].Y, glyphs[0].Width, glyphs[0].Height), Is.EqualTo((21, 21, 39, 50)));
            Assert.That(glyphs[93].Character, Is.EqualTo('~'));
            Assert.That((glyphs[93].X, glyphs[93].Y), Is.EqualTo((20 + 3 * 40 + 1, 20 + 9 * 51 + 1)));
        }

        [Test]
        public void TheDistanceFieldIsHalfOnTheEdgeAndGrowsInward()
        {
            // A 10x10 square of ink in a 30x30 image.
            const int Size = 30;
            var coverage = new byte[Size * Size];
            for (int y = 10; y < 20; y++)
            {
                for (int x = 10; x < 20; x++)
                {
                    coverage[y * Size + x] = 255;
                }
            }

            const float Gradient = 11f;
            byte[] field = SignedDistanceField.Encode(coverage, Size, Size, Gradient);
            // Texel centres half a texel either side of the edge read as +0.5 and -0.5 texels.
            Assert.That(SignedDistanceField.Decode(field[15 * Size + 10], Gradient), Is.EqualTo(0.5).Within(0.05));
            Assert.That(SignedDistanceField.Decode(field[15 * Size + 9], Gradient), Is.EqualTo(-0.5).Within(0.05));
            Assert.That(SignedDistanceField.Decode(field[15 * Size + 6], Gradient), Is.EqualTo(-3.5).Within(0.05));
            Assert.That(SignedDistanceField.Decode(field[15 * Size + 14], Gradient), Is.EqualTo(4.5).Within(0.05));
            // Diagonally off a corner the distance is Euclidean.
            double corner = SignedDistanceField.Decode(field[6 * Size + 6], Gradient);
            Assert.That(corner, Is.EqualTo(-(System.Math.Sqrt(32) - 0.5)).Within(0.05));
            // Far away it clamps to empty.
            Assert.That(field[0], Is.EqualTo(0));
        }

        [Test]
        public void UpsamplingKeepsSolidInkSolidAndSpreadsEdgesOverTheNewTexels()
        {
            // Three identical rows, so the middle of the result reads them alone.
            byte[] coverage = { 0, 255, 255, 0, 0, 255, 255, 0, 0, 255, 255, 0 };
            byte[] up = SignedDistanceField.Upsample(coverage, 4, 3, 4);
            Assert.That(up.Length, Is.EqualTo(16 * 12));
            int row = 5 * 16;
            Assert.That(up[row + 7], Is.EqualTo(255));
            Assert.That(up[row + 8], Is.EqualTo(255));
            Assert.That(up[row + 0], Is.EqualTo(0));
            Assert.That(up[row + 2], Is.GreaterThan(0).And.LessThan(255));
        }
    }
}
