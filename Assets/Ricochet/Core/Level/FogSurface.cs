// The original's fog surface (obj_fogscreen, UR-T77): a small grid, one cell per 16 room pixels, that starts fully
// fogged and has light sources (spr_lightsource, drawn bm_src_color) stamped into it. It is never cleared between
// stamps, so whatever a light has reached stays revealed until the surface is reset (reset_fogofwar: a new fogscreen for
// each new level).
using System;

namespace Ricochet.Core.Level
{
    public sealed class FogSurface
    {
        /// <summary>room_width / 16: the original's surface is this many times smaller than the room (surfScale = 1/16).</summary>
        public const int PixelsPerCell = 16;

        /// <summary>spr_lightsource is 200 pixels wide, origin at its middle: it reaches this many room pixels at size 1.</summary>
        public const double LightRadiusPixels = 100.0;

        /// <summary>The original's obj_fogscreen redraws on this alarm: every 10 steps of its 60 a second.</summary>
        public const double UpdateSeconds = 10.0 / 60.0;

        // spr_lightsource's alpha every 10 pixels out from its middle (read off the sprite): 255 at the middle to 0 at the edge.
        private static readonly double[] Falloff = { 255, 233, 177, 112, 59, 26, 9, 3, 1, 0, 0 };

        private readonly double[] _fog; // 1 = fully fogged, 0 = clear
        private readonly double _minX;
        private readonly double _minY;
        private readonly double _cell; // arena units per cell

        /// <summary>A fresh, fully fogged surface over the arena rectangle, at <paramref name="unitsPerPixel"/> arena units per room pixel.</summary>
        public FogSurface(double minX, double minY, double maxX, double maxY, double unitsPerPixel)
        {
            _minX = minX;
            _minY = minY;
            _cell = PixelsPerCell * unitsPerPixel;
            UnitsPerPixel = unitsPerPixel;
            Width = Math.Max(1, (int)Math.Ceiling((maxX - minX) / _cell - 1e-6));
            Height = Math.Max(1, (int)Math.Ceiling((maxY - minY) / _cell - 1e-6));
            _fog = new double[Width * Height];
            Clear();
        }

        public int Width { get; }

        public int Height { get; }

        public double UnitsPerPixel { get; }

        /// <summary>Arena units per cell.</summary>
        public double CellSize => _cell;

        /// <summary>The arena x of the surface's left edge.</summary>
        public double MinX => _minX;

        /// <summary>The arena y of the surface's bottom edge.</summary>
        public double MinY => _minY;

        /// <summary>The fog's opacity (0 clear, 1 black) at cell <paramref name="x"/>, <paramref name="y"/> (y up from the arena's bottom).</summary>
        public double Opacity(int x, int y) => _fog[y * Width + x];

        /// <summary>The fog's opacity over an arena point (1 outside the surface).</summary>
        public double OpacityAt(double worldX, double worldY)
        {
            int x = (int)Math.Floor((worldX - _minX) / _cell);
            int y = (int)Math.Floor((worldY - _minY) / _cell);
            return x < 0 || y < 0 || x >= Width || y >= Height ? 1.0 : _fog[y * Width + x];
        }

        /// <summary>Fogs everything again (draw_clear(c_black), at the surface's creation).</summary>
        public void Clear()
        {
            Array.Fill(_fog, 1.0);
        }

        /// <summary>
        /// Stamps spr_lightsource, scaled by <paramref name="size"/> (the object's lightSize) and at <paramref name="strength"/>
        /// (lightStrength, the draw alpha), centred on an arena point. Overlapping lights add up and never darken.
        /// </summary>
        public void Light(double worldX, double worldY, double size, double strength = 1.0)
        {
            double radiusCells = LightRadiusPixels * size / PixelsPerCell;
            double cx = (worldX - _minX) / _cell;
            double cy = (worldY - _minY) / _cell;
            int x0 = Math.Max(0, (int)Math.Floor(cx - radiusCells));
            int x1 = Math.Min(Width - 1, (int)Math.Ceiling(cx + radiusCells));
            int y0 = Math.Max(0, (int)Math.Floor(cy - radiusCells));
            int y1 = Math.Min(Height - 1, (int)Math.Ceiling(cy + radiusCells));
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    double dx = x + 0.5 - cx;
                    double dy = y + 0.5 - cy;
                    double fraction = Math.Sqrt(dx * dx + dy * dy) / radiusCells; // 0 at the middle, 1 at the sprite's edge
                    if (fraction >= 1.0)
                    {
                        continue;
                    }

                    double at = fraction * (Falloff.Length - 1);
                    int i = (int)at;
                    double alpha = (Falloff[i] + (Falloff[i + 1] - Falloff[i]) * (at - i)) / 255.0 * strength;
                    _fog[y * Width + x] *= 1.0 - alpha;
                }
            }
        }
    }
}
