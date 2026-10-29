// U.RICO-T55, the credits roll. The original's credits room (rooms/credits.room.gmx) was bg_titlescreen with
// obj_creditsimage centring spr_creditsimage (486 x 714, every name already painted into the picture), obj_btn_back_credits
// at (128, 1024) going back to the main menu, and obj_credits, which only sized the view and toggled full screen on F1:
// nothing moved and no text was drawn (fnt_credits, Verdana 10, sat on disk outside the project and no code set it).
// The rebuild keeps the picture where the room put it and rolls this project's own credits up over it, in fnt_credits
// with fnt_mono headings, from just under the picture's bottom edge until the last line has left its top, then again.
// The speed is data (menu.json's creditsScrollSpeed, room pixels a second); without it, a pixel a step at the rooms'
// 60 steps a second, the pace the menu rooms' floors scrolled at.
using System;
using System.Collections.Generic;

namespace Ricochet.Core.Menu
{
    /// <summary>One block of the credits: a heading and the lines under it (menu.json's "credits").</summary>
    public sealed class CreditsSection
    {
        public CreditsSection(string heading, params string[] lines)
        {
            Heading = heading ?? string.Empty;
            Lines = lines ?? Array.Empty<string>();
        }

        public string Heading { get; }

        public IReadOnlyList<string> Lines { get; }
    }

    /// <summary>Where the credits roll stands over the picture at a given time.</summary>
    public static class CreditsRoll
    {
        /// <summary>A pixel a step at 60 steps a second, in room pixels a second.</summary>
        public const double DefaultSpeed = 60.0;

        /// <summary>The headings' font (the menu rooms' captions) and the names' (fnt_credits).</summary>
        public const string HeadingFont = "fnt_mono";
        public const string LineFont = "fnt_credits";

        /// <summary>The roll's speed from menu.json, in room pixels a second (<see cref="DefaultSpeed"/> without the data).</summary>
        public static double Speed
        {
            get
            {
                Ricochet.Core.Data.GameData data = Ricochet.Core.Data.GameData.Current;
                double speed = data != null && data.Menu != null ? data.Menu.CreditsScrollSpeed : 0;
                return speed > 0 ? speed : DefaultSpeed;
            }
        }

        /// <summary>
        /// How far below the window's top edge the roll's top edge is <paramref name="seconds"/> after the roll started:
        /// <paramref name="window"/> at the start (the first line just under the bottom edge), falling by
        /// <paramref name="speed"/> a second until the roll (<paramref name="content"/> tall) has left the top, then
        /// starting over. 0 when nothing can move.
        /// </summary>
        public static double Top(double seconds, double speed, double window, double content)
        {
            double travel = Period(speed, window, content) * speed;
            if (travel <= 0)
            {
                return 0;
            }

            double moved = (Math.Max(0, seconds) * speed) % travel;
            return window - moved;
        }

        /// <summary>How long one pass takes, in seconds: from entering at the bottom until gone at the top (0 when still).</summary>
        public static double Period(double speed, double window, double content)
        {
            double travel = Math.Max(0, window) + Math.Max(0, content);
            return speed > 0 && travel > 0 ? travel / speed : 0;
        }

        /// <summary>Every line of the credits in order, each heading before its lines (for a list without the roll).</summary>
        public static IEnumerable<string> Lines(IEnumerable<CreditsSection> sections)
        {
            foreach (CreditsSection section in sections)
            {
                if (section.Heading.Length > 0)
                {
                    yield return section.Heading;
                }

                foreach (string line in section.Lines)
                {
                    yield return line;
                }
            }
        }
    }
}
