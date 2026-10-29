// Turns a GameMaker font's anti-aliased glyph coverage into the signed distance field TextMeshPro's Distance Field
// shader draws (U.RICO-T44), so the original's own glyphs scale cleanly and draw_text_outline's black outline is a
// material property instead of nine draws of the same string. Engine-free so the Core tests can check it.
using System;

namespace Ricochet.Core.Art
{
    public static class SignedDistanceField
    {
        private const double Far = 1e20;

        /// <summary>
        /// <paramref name="coverage"/> (one byte a pixel, row by row, <paramref name="width"/> x <paramref name="height"/>)
        /// sampled up <paramref name="scale"/> times bilinearly, each new texel reading the source at its own centre;
        /// outside the image counts as empty.
        /// </summary>
        public static byte[] Upsample(byte[] coverage, int width, int height, int scale)
        {
            Check(coverage, width, height);
            if (scale < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(scale));
            }

            if (scale == 1)
            {
                return (byte[])coverage.Clone();
            }

            int w = width * scale, h = height * scale;
            var result = new byte[w * h];
            for (int y = 0; y < h; y++)
            {
                double sy = (y + 0.5) / scale - 0.5;
                int y0 = (int)Math.Floor(sy);
                double fy = sy - y0;
                for (int x = 0; x < w; x++)
                {
                    double sx = (x + 0.5) / scale - 0.5;
                    int x0 = (int)Math.Floor(sx);
                    double fx = sx - x0;
                    double top = Lerp(At(coverage, width, height, x0, y0), At(coverage, width, height, x0 + 1, y0), fx);
                    double bottom = Lerp(At(coverage, width, height, x0, y0 + 1), At(coverage, width, height, x0 + 1, y0 + 1), fx);
                    result[y * w + x] = (byte)Math.Round(Lerp(top, bottom, fy));
                }
            }

            return result;
        }

        /// <summary>
        /// The distance field of <paramref name="coverage"/>, thresholded at half: each texel's distance in texels to the
        /// glyph's edge, positive inside, encoded as TextMeshPro reads it (0.5 on the edge, one unit of alpha per
        /// <paramref name="gradientScale"/> texels, the material's _GradientScale), clamped to 0..255.
        /// </summary>
        public static byte[] Encode(byte[] coverage, int width, int height, float gradientScale)
        {
            Check(coverage, width, height);
            if (gradientScale <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(gradientScale));
            }

            int n = width * height;
            var toInside = new double[n];  // squared distance to the nearest inside texel
            var toOutside = new double[n]; // squared distance to the nearest outside texel
            for (int i = 0; i < n; i++)
            {
                bool inside = coverage[i] >= 128;
                toInside[i] = inside ? 0 : Far;
                toOutside[i] = inside ? Far : 0;
            }

            Transform(toInside, width, height);
            Transform(toOutside, width, height);

            var result = new byte[n];
            for (int i = 0; i < n; i++)
            {
                bool inside = coverage[i] >= 128;
                // The edge lies half a texel beyond the last texel on its side.
                double d = inside ? Math.Sqrt(toOutside[i]) - 0.5 : -(Math.Sqrt(toInside[i]) - 0.5);
                double a = 0.5 + d / gradientScale;
                result[i] = (byte)Math.Round(Math.Max(0.0, Math.Min(1.0, a)) * 255.0);
            }

            return result;
        }

        /// <summary>The distance <paramref name="alpha"/> stands for, in texels (positive inside), for a field encoded at <paramref name="gradientScale"/>.</summary>
        public static double Decode(byte alpha, float gradientScale) => (alpha / 255.0 - 0.5) * gradientScale;

        // Felzenszwalb and Huttenlocher's exact squared Euclidean distance transform, by columns then rows.
        private static void Transform(double[] grid, int width, int height)
        {
            int longest = Math.Max(width, height);
            var f = new double[longest];
            var d = new double[longest];
            var v = new int[longest];
            var z = new double[longest + 1];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    f[y] = grid[y * width + x];
                }

                Transform1D(f, height, d, v, z);
                for (int y = 0; y < height; y++)
                {
                    grid[y * width + x] = d[y];
                }
            }

            for (int y = 0; y < height; y++)
            {
                Array.Copy(grid, y * width, f, 0, width);
                Transform1D(f, width, d, v, z);
                Array.Copy(d, 0, grid, y * width, width);
            }
        }

        private static void Transform1D(double[] f, int n, double[] d, int[] v, double[] z)
        {
            int k = 0;
            v[0] = 0;
            z[0] = double.NegativeInfinity;
            z[1] = double.PositiveInfinity;
            for (int q = 1; q < n; q++)
            {
                double s = Intersection(f, q, v[k]);
                while (s <= z[k])
                {
                    k--;
                    s = Intersection(f, q, v[k]);
                }

                k++;
                v[k] = q;
                z[k] = s;
                z[k + 1] = double.PositiveInfinity;
            }

            k = 0;
            for (int q = 0; q < n; q++)
            {
                while (z[k + 1] < q)
                {
                    k++;
                }

                double dq = q - v[k];
                d[q] = dq * dq + f[v[k]];
            }
        }

        private static double Intersection(double[] f, int q, int p) =>
            ((f[q] + (double)q * q) - (f[p] + (double)p * p)) / (2.0 * q - 2.0 * p);

        private static double At(byte[] coverage, int width, int height, int x, int y) =>
            x < 0 || y < 0 || x >= width || y >= height ? 0.0 : coverage[y * width + x];

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;

        private static void Check(byte[] coverage, int width, int height)
        {
            if (coverage == null || width <= 0 || height <= 0 || coverage.Length != width * height)
            {
                throw new ArgumentException($"Coverage must hold {width} x {height} bytes");
            }
        }
    }
}
