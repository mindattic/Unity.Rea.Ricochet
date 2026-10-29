// Where the original's menu rooms put their controls (U.RICO-T84, docs/BIBLE.md "Menus and pictures"), engine-free so the
// Core tests can hold it to the rooms. Every menu room is 1920 x 1080 (the views are the whole room) and every object's origin
// is its sprite's centre, so a position below is where a control's centre was, in room pixels (y down), and a size is its
// sprite's own size, drawn at scale 1:
//   obj_btn_* (spr_button 226x64): the main menu's five buttons at x 960, y 352, 448, 544, 640, 736 (96 apart, the column
//                         centred on y 544); obj_btn_back_<room> at 128, 1024 in every room that has one; obj_btn_start / _setup /
//                         _players (the room's way on) at 1792, 1024; obj_btn_default (Default) at 1792, 928.
//   obj_lbl_* with its less / more arrows (spr_arrowleft / right 89x64): the arrows 160 px either side of the label's centre,
//                         the caption 24 px above the centre and the value 20 px under it; the rows 64 apart (80 in the
//                         players room).
//   obj_checkbox_* (64x64): the text 55 px to the right of its centre.  obj_inputbox* (387x64).
//   The room's title ("Controls", "Setup Game" ...) is drawn at (border, border) with border = 32, left aligned.
//   obj_keyboardlayout at 960, 512, obj_creditsimage at 960, 576, obj_slide at 896, 576 with obj_btn_lastslide at 512, 576 and
//                         obj_btn_nextslide at 1280, 576 (spr_arrowleft / right), the slide's counter 280 px above the room's
//                         middle and its text 260 px below it, both at the slide's left edge.
// A rebuilt screen holds controls the original's rooms did not (the kit's name and colour, the lobby's code, a list), so the
// ones with no object of their own flow down the room as the players room's rows did (80 apart from y 272), a new column
// beginning where the room runs out.
using System;
using System.Collections.Generic;

namespace Ricochet.Core.Art
{
    /// <summary>A rectangle in room pixels (y down): its centre and size.</summary>
    public readonly struct RoomRect
    {
        public RoomRect(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        /// <summary>The centre.</summary>
        public double X { get; }

        public double Y { get; }

        public double Width { get; }

        public double Height { get; }

        public double Left => X - Width / 2.0;

        public double Top => Y - Height / 2.0;

        public override string ToString() => X + "," + Y + " " + Width + "x" + Height;
    }

    /// <summary>Where a flowing item stands: its column and its top edge.</summary>
    public readonly struct FlowSlot
    {
        public FlowSlot(int column, double top)
        {
            Column = column;
            Top = top;
        }

        public int Column { get; }

        public double Top { get; }
    }

    /// <summary>The result of <see cref="RoomLayout.Flow"/>.</summary>
    public sealed class FlowPlan
    {
        public FlowPlan(FlowSlot[] slots, double[] columnCentres, double gap)
        {
            Slots = slots;
            ColumnCentres = columnCentres;
            Gap = gap;
        }

        /// <summary>One slot for each item, in order.</summary>
        public FlowSlot[] Slots { get; }

        /// <summary>The columns' centres, left to right.</summary>
        public double[] ColumnCentres { get; }

        /// <summary>The space left between two items.</summary>
        public double Gap { get; }

        /// <summary>The width each column has.</summary>
        public double ColumnWidth(double regionWidth) => regionWidth / ColumnCentres.Length;
    }

    public static class RoomLayout
    {
        /// <summary>The room, in pixels.</summary>
        public const int Width = 1920;
        public const int Height = 1080;

        /// <summary>globalvar border: where a room's title is drawn.</summary>
        public const int Border = 32;

        public static readonly RoomRect Title = new RoomRect(Border, Border, 0, 0);

        /// <summary>The sprites' own sizes.</summary>
        public const double ButtonWidth = 226;
        public const double ButtonHeight = 64;
        public const double ArrowWidth = 89;
        public const double ArrowHeight = 64;
        public const double CheckboxSize = 64;
        public const double InputWidth = 387;
        public const double InputHeight = 64;

        /// <summary>obj_btn_back_&lt;room&gt;.</summary>
        public static readonly RoomRect Back = new RoomRect(128, 1024, ButtonWidth, ButtonHeight);

        /// <summary>obj_btn_start / _setup / _players: the room's way on.</summary>
        public static readonly RoomRect Primary = new RoomRect(1792, 1024, ButtonWidth, ButtonHeight);

        /// <summary>obj_btn_default.</summary>
        public static readonly RoomRect Reset = new RoomRect(1792, 928, ButtonWidth, ButtonHeight);

        /// <summary>The main menu's button column: the centre x, and the gap between buttons.</summary>
        public const double ColumnX = 960;
        public const double ColumnGap = 96;
        public const double ColumnMiddle = 544;

        /// <summary>How far a less / more arrow stands from its label's centre.</summary>
        public const double ArrowOffset = 160;

        /// <summary>A label's caption is drawn this far above its centre, its value this far under it.</summary>
        public const double CaptionRise = 24;
        public const double ValueDrop = 20;

        /// <summary>A checkbox's text starts this far right of the checkbox's centre.</summary>
        public const double CheckboxTextOffset = 55;

        /// <summary>The players room's rows: the first one's centre, and the step.</summary>
        public const double FirstRowY = 272;
        public const double RowStep = 80;

        /// <summary>The picture rooms.</summary>
        public static readonly RoomRect Keyboard = new RoomRect(960, 512, 1814, 972);
        public static readonly RoomRect Credits = new RoomRect(960, 576, 486, 714);
        public static readonly RoomRect Slide = new RoomRect(896, 576, 640, 480);
        public static readonly RoomRect SlideLast = new RoomRect(512, 576, ArrowWidth, ArrowHeight);
        public static readonly RoomRect SlideNext = new RoomRect(1280, 576, ArrowWidth, ArrowHeight);

        /// <summary>The slide's counter and its text: this far above and below the room's middle (obj_slide's Draw).</summary>
        public const double SlideCounterRise = 280;
        public const double SlideTextDrop = 260;

        /// <summary>The label row's box: the arrows either side of the caption (a row is as wide as both arrows and the gap).</summary>
        public static double RowWidth => 2 * ArrowOffset + ArrowWidth;

        /// <summary>
        /// The <paramref name="index"/>th of <paramref name="count"/> buttons of a screen that is only buttons (the main
        /// menu): in a column at x 960, 96 apart, centred on y 544 (five buttons stand at 352 ... 736 as the room placed them).
        /// </summary>
        public static RoomRect ButtonColumn(int index, int count)
        {
            return new RoomRect(ColumnX, ColumnMiddle + ColumnGap * (index - (count - 1) / 2.0), ButtonWidth, ButtonHeight);
        }

        /// <summary>The top of the region screens flow into (the players room's first row's top), and its bottom (above Default).</summary>
        public const double FlowTop = FirstRowY - ArrowHeight / 2.0;
        public static readonly double FlowBottom = Reset.Top - 16;

        /// <summary>
        /// Lays <paramref name="heights"/> (the items' heights, in order) down a region from <paramref name="top"/> to
        /// <paramref name="bottom"/> and <paramref name="left"/> to <paramref name="right"/>, a new column where one runs out;
        /// null when they need more than <paramref name="maxColumns"/> even with the items touching. The room's own gap
        /// (16 px, as its 80 px row step is over a 64 px row) is tried first, then 8, then none.
        /// </summary>
        public static FlowPlan Flow(IReadOnlyList<double> heights, double left, double top, double right, double bottom, int maxColumns)
        {
            if (heights == null)
            {
                throw new ArgumentNullException(nameof(heights));
            }

            foreach (double gap in new[] { 16.0, 8.0, 0.0 })
            {
                var slots = new FlowSlot[heights.Count];
                int column = 0;
                double y = top;
                for (int i = 0; i < heights.Count; i++)
                {
                    if (y > top && y + heights[i] > bottom)
                    {
                        column++;
                        y = top;
                    }

                    slots[i] = new FlowSlot(column, y);
                    y += heights[i] + gap;
                }

                int columns = Math.Max(1, column + 1);
                if (columns > maxColumns)
                {
                    continue;
                }

                var centres = new double[columns];
                for (int c = 0; c < columns; c++)
                {
                    centres[c] = left + (right - left) * (c + 0.5) / columns;
                }

                return new FlowPlan(slots, centres, gap);
            }

            return null;
        }
    }
}
