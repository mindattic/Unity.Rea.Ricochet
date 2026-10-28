// U.RICO-T54, the how-to-play slide's pictures. The original's obj_slide drew spr_howtoplay1, one blank black 640 x 480
// frame, for every slide; here each slide (menu.json's "images") shows the game's own sprites for what it explains (the
// weapons' icons, the items', the crates, the announcer's portraits ...) laid out on that frame: one row of up to four,
// two rows past that, each in an equal square slot with a margin round the frame. Engine-free (UR-LAW-1): Runtime's
// Menus.BuildHowToPlay draws it; Tests/Core.Tests/KeyboardPictureTests checks it.
using System;
using System.Collections.Generic;

namespace Ricochet.Core.Art
{
    public static class SlidePicture
    {
        /// <summary>The frame's size: spr_howtoplay1's.</summary>
        public const double Width = 640;
        public const double Height = 480;

        /// <summary>The margin round the frame, and between slots.</summary>
        public const double Margin = 48;
        public const double Spacing = 24;

        /// <summary>At most this many pictures in a row.</summary>
        public const int PerRow = 4;

        /// <summary>The square slots of <paramref name="count"/> pictures, row by row, each row centred on the frame.</summary>
        public static IReadOnlyList<PictureRect> Slots(int count)
        {
            var slots = new List<PictureRect>();
            if (count <= 0)
            {
                return slots;
            }

            int rows = (count + PerRow - 1) / PerRow;
            int columns = Math.Min(count, (count + rows - 1) / rows);
            double size = Math.Min(
                (Width - 2 * Margin - (columns - 1) * Spacing) / columns,
                (Height - 2 * Margin - (rows - 1) * Spacing) / rows);
            size = Math.Min(size, 200);
            double top = (Height - rows * size - (rows - 1) * Spacing) / 2.0;
            for (int row = 0; row < rows; row++)
            {
                int inRow = Math.Min(columns, count - row * columns);
                double left = (Width - inRow * size - (inRow - 1) * Spacing) / 2.0;
                for (int i = 0; i < inRow; i++)
                {
                    slots.Add(new PictureRect(left + i * (size + Spacing), top + row * (size + Spacing), size, size));
                }
            }

            return slots;
        }
    }
}
