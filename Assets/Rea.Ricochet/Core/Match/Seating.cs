// Where sixteen seats stand and how the HUD lays them out. The original placed players at random (randomize_player_
// position.gml) and its health bars in a row along the top, the first four left of the turn timer and the rest right
// of it (setup_players.gml, draw_scores.gml); draw_turntime.gml printed the timer as seconds and a fraction at the top
// centre. Here the spots are fixed so every peer agrees on them without sending them, and the bars are two rows of
// eight, four each side of the timer.
using System;
using System.Globalization;

namespace Ricochet.Core.Match
{
    using Ricochet.Core.Aim;
    using Ricochet.Core.Level;
    public static class Seating
    {
        /// <summary>A player's hit radius (their square is drawn 0.8 wide).</summary>
        public const double PlayerRadius = 0.45;

        /// <summary>Health bars per HUD row: four left of the timer, four right.</summary>
        public const int BarsPerRow = 8;

        // The first five are UR-T3's five columns along the bottom; the rest fill the level around the walls.
        private static readonly Vec2[] Spots =
        {
            new Vec2(0, -4.5), new Vec2(-4, -4.5), new Vec2(4, -4.5), new Vec2(-8, -4.5), new Vec2(8, -4.5),
            new Vec2(-2, -2.5), new Vec2(2, -2.5), new Vec2(-6, -3), new Vec2(5, -1), new Vec2(-9.5, -1.5),
            new Vec2(9.5, -1.5), new Vec2(-4, 2.5), new Vec2(3.5, 2.5), new Vec2(-9, 1), new Vec2(9, 1),
            new Vec2(0, 4.5),
        };

        /// <summary>Where the practice dummies stand (the arena's three; every level keeps its walls clear of them).</summary>
        public static readonly Vec2[] DummyStarts = { new Vec2(-7, 1), new Vec2(7, -3), new Vec2(0, 3.5) };

        /// <summary>Where seat <paramref name="seat"/> stands on the hand-laid maps (any index maps onto the sixteen spots);
        /// a random round has its own spots, see <see cref="RoundLayout"/>.</summary>
        public static Vec2 SpawnPoint(int seat)
        {
            int n = Spots.Length;
            return Spots[((seat % n) + n) % n];
        }

        /// <summary>The HUD slot for the <paramref name="slot"/>th bar: its column (0-7) and row (0-1); columns 0-3 sit
        /// left of the timer and 4-7 right of it.</summary>
        public static void BarSlot(int slot, out int column, out int row)
        {
            slot = Math.Max(0, Math.Min(MatchRules.MaxSeats - 1, slot));
            column = slot % BarsPerRow;
            row = slot / BarsPerRow;
        }

        /// <summary>
        /// How much to shrink the HUD's bars so four columns a side, the gaps between them and the clock's own gap
        /// all fit within <paramref name="screenWidth"/> (UR-T17: below ~1400px wide, unscaled bars ran off the left
        /// edge). 1 at design width or wider; smaller on a narrower window, never below <paramref name="minScale"/>.
        /// <summary>obj_healthbar's drain: its alarm moves the drawn value one hit point toward the real one every 2 steps of a
        /// 60-steps-a-second room, 30 points a second, up as well as down, never overshooting (UR-T67).</summary>
        public const double HealthEaseRate = 30;

        /// <summary>The bar's drawn health after <paramref name="dt"/> seconds easing from <paramref name="shown"/> toward <paramref name="health"/>.</summary>
        public static double EaseHealth(double shown, double health, double dt)
        {
            double step = HealthEaseRate * (dt > 0 ? dt : 0);
            if (shown < health)
            {
                return System.Math.Min(health, shown + step);
            }

            return System.Math.Max(health, shown - step);
        }


        /// </summary>
        public static double BarScale(double screenWidth, double barWidth, double barGap, double clockGap, double sidePadding = 40)
        {
            const double minScale = 0.3;
            double half = BarsPerRow / 2.0;
            double needed = half * (barWidth + barGap) * 2 + clockGap;
            double available = Math.Max(1, screenWidth - sidePadding);
            double scale = available / needed;
            return Math.Max(minScale, Math.Min(1, scale));
        }

        /// <summary>The x of the <paramref name="column"/>th bar (0-7), centred on <paramref name="centre"/> with the
        /// clock's own <paramref name="clockGap"/> between the two halves and <paramref name="barGap"/> between bars.</summary>
        public static double BarX(double centre, int column, double barWidth, double barGap, double clockGap)
        {
            double half = BarsPerRow / 2.0;
            return column < half
                ? centre - clockGap / 2 - (half - column) * (barWidth + barGap)
                : centre + clockGap / 2 + (column - half) * (barWidth + barGap);
        }

        /// <summary>
        /// draw_turntime.gml's clock: whole seconds, a dot, and hundredths, both two digits ("20.00", "04.37"). The
        /// original printed the frame count within the second (0-59) and padded it on the wrong side ("5" became "50");
        /// this prints true hundredths, rounded down so it reads 00.00 only when time is up.
        /// </summary>
        public static string FormatClock(double seconds)
        {
            if (double.IsNaN(seconds) || seconds < 0)
            {
                seconds = 0;
            }

            long hundredths = (long)Math.Floor(seconds * 100 + 1e-6);
            long whole = hundredths / 100;
            long fraction = hundredths % 100;
            return whole.ToString("00", CultureInfo.InvariantCulture) + "." + fraction.ToString("00", CultureInfo.InvariantCulture);
        }
    }
}
