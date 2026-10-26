// How the original's HUD details look (U.RICO-T49, docs/BIBLE.md "HUD details"), engine-free so the Core tests can hold it to
// the original. All positions are GameMaker pixels in the 1920x1080 room (y grows downward), which is the HUD canvas's own
// 1920x1080 reference; the presentation layer (HudIcons, MatchHud, TurnGuide, ControlPad) places them.
//   obj_icon_weapon_* / obj_item_*  bottom left, weapons on the row room_height - border*2, items on room_height - border,
//                         the n-th (0-based) at border*(n+1) + 10*n. setup_weapons / setup_items: _off at alpha 0.2 for a
//                         count of 0, the plain sprite at 1 for one carried, and the selected one's _selected sprite (its
//                         alpha left as the count made it). The Draw GUI: draw_self, then, for a count above 0, the count in
//                         fnt_count outlined 2 px (draw_text_outline_color, white on black) at x + sprite_width - 4, y + 15,
//                         or spr_infinity at x + sprite_width - 4, y + 24 for an unlimited one (10). Left Pressed selects it.
//   obj_lbl_count_*       the setup room's counts (spr_infinity_large for unlimited): the rebuild's setup is the kit's
//                         front end, so only the HUD's counts are carried here.
//   mouseover_message_draw   the hover text: fnt_score, white, centred at room_height - 30.
//   draw_angle / obj_degree    the current player's angle in fnt_score outlined 1 px, 36 px under the tank while it aims up
//                         (image_angle < 180) and 60 px over it otherwise, while the turn runs (isReady && turnStarted);
//                         spr_symbol_degree at x + string_width / 2 + 20. obj_degree_gui: the same angle at the turn
//                         buttons (obj_btn_degreeleft.x + 34, room_height - 46) with its degree sign.
//   obj_turnarrow         spr_turnarrow (6 frames at image_speed 0.2) beside the current player's score.
//   obj_guide             while the turn runs and its player has not driven: circles of 60 and 61 px in the player's colour
//                         at 0.7, of 59 and 62 px in white at 0.4, and a tick along the aim from 49 to 71 px, 3 px wide in
//                         white under one from 50 to 70 px, 1 px wide in the player's colour.
//   obj_healthbar         spr_healthbar_back at 0.25, spr_healthbar_fill in the player's colour scaled to the health shown,
//                         spr_healthbar over them, and "shown/max" in fnt_count outlined 1 px, centred, 4 px down.
//   obj_indicator_extrabounce  spr_indicator_extrabounce where the angle reads; never created in the original (fixed defect:
//                         shown while the current player has an extra bounce on this turn).
using System;
using System.Globalization;

namespace Ricochet.Core.Art
{
    using Ricochet.Core.Aim;
    using Ricochet.Core.Arsenal;

    /// <summary>Which of its three sprites an inventory icon shows (setup_weapons / setup_items).</summary>
    public enum IconState
    {
        /// <summary>None carried: the _off sprite.</summary>
        Off,

        /// <summary>Carried: the plain sprite.</summary>
        Normal,

        /// <summary>The selected one: the _selected sprite.</summary>
        Selected,
    }

    /// <summary>One ring of obj_guide: its radius in pixels, white or the player's colour, and its alpha.</summary>
    public readonly struct GuideRing
    {
        public GuideRing(int radius, bool white, double alpha)
        {
            Radius = radius;
            White = white;
            Alpha = alpha;
        }

        public int Radius { get; }

        public bool White { get; }

        public double Alpha { get; }
    }

    public static class HudLooks
    {
        /// <summary>The original room, which the HUD canvas's reference matches pixel for pixel.</summary>
        public const int RoomWidth = 1920;
        public const int RoomHeight = 1080;

        /// <summary>globals.gml's border.</summary>
        public const int Border = 32;

        /// <summary>The icons' sprites are 32x32 with their origin at the top left.</summary>
        public const int IconSize = 32;

        /// <summary>setup_weapons / setup_items: an icon with none carried draws at 0.2.</summary>
        public const double OffAlpha = 0.2;

        /// <summary>The count's place: x + sprite_width - 4, y + 15; spr_infinity's (centred, 11x11) y + 24.</summary>
        public const int CountOffsetX = IconSize - 4;
        public const int CountOffsetY = 15;
        public const int InfinityOffsetY = 24;

        /// <summary>draw_text_outline_color's outline around the counts.</summary>
        public const int CountOutline = 2;

        /// <summary>mouseover_message_draw's line: centred, room_height - 30.</summary>
        public const int TooltipY = RoomHeight - 30;

        /// <summary>draw_angle: 36 px under the tank while it aims up, 60 px over it otherwise.</summary>
        public const int AngleBelow = 36;
        public const int AngleAbove = 60;

        /// <summary>obj_degree: the degree sign at x + string_width / 2 + 20.</summary>
        public const int DegreeGap = 20;

        /// <summary>draw_angle's outline around the angle.</summary>
        public const int AngleOutline = 1;

        /// <summary>obj_turnarrow's image_speed.</summary>
        public const double TurnArrowImageSpeed = 0.2;

        /// <summary>obj_guide's tick: white from 49 to 71 px, 3 px wide, under the player's colour from 50 to 70 px, 1 px wide.</summary>
        public const int GuideTickWhiteFrom = 49;
        public const int GuideTickWhiteTo = 71;
        public const int GuideTickWhiteWidth = 3;
        public const int GuideTickFrom = 50;
        public const int GuideTickTo = 70;
        public const int GuideTickWidth = 1;

        /// <summary>obj_healthbar: spr_healthbar_back at 0.25, the text 4 px down, outlined 1 px.</summary>
        public const double HealthBackAlpha = 0.25;
        public const int HealthTextOffsetY = 4;
        public const int HealthOutline = 1;

        private static readonly GuideRing[] s_guideRings =
        {
            new GuideRing(60, false, 0.7),
            new GuideRing(61, false, 0.7),
            new GuideRing(62, true, 0.4),
            new GuideRing(59, true, 0.4),
        };

        private static readonly string[] s_weaponStems = { "cannon", "laser", "shotgun", "grenade", "railgun", "vortex" };
        private static readonly string[] s_itemStems = { "extrabounce", "shieldwall", "shrinkray", "teleporter", "aimassist", "redirector" };

        /// <summary>obj_guide's four circles, in the order it draws them.</summary>
        public static GuideRing[] GuideRings => (GuideRing[])s_guideRings.Clone();

        /// <summary>The left edge of the <paramref name="index"/>-th icon (0-based) of a row: border * (n + 1) + 10 * n.</summary>
        public static int IconX(int index) => Border * (index + 1) + 10 * index;

        /// <summary>The top of the weapons' row (room_height - border * 2).</summary>
        public static int WeaponRowY => RoomHeight - Border * 2;

        /// <summary>The top of the items' row (room_height - border).</summary>
        public static int ItemRowY => RoomHeight - Border;

        /// <summary>setup_weapons / setup_items: which sprite an icon shows for this count and selection.</summary>
        public static IconState State(int count, bool selected) => selected ? IconState.Selected : count > 0 ? IconState.Normal : IconState.Off;

        /// <summary>setup_weapons / setup_items: 1 for one carried, 0.2 for none (a selected one keeps its count's).</summary>
        public static double Alpha(int count) => count > 0 ? 1.0 : OffAlpha;

        /// <summary>The Draw GUI draws a count only for one carried.</summary>
        public static bool ShowsCount(int count) => count > 0;

        /// <summary>... and spr_infinity in its place for an unlimited one.</summary>
        public static bool ShowsInfinity(int count) => count >= ArsenalRules.Unlimited;

        /// <summary>The count as drawn: "3", or "" when none is drawn or spr_infinity stands in for it.</summary>
        public static string CountText(int count) => ShowsCount(count) && !ShowsInfinity(count) ? count.ToString(CultureInfo.InvariantCulture) : string.Empty;

        /// <summary>The GML sprite of <paramref name="weapon"/>'s icon: spr_icon_weapon_cannon, _off or _selected.</summary>
        public static string WeaponIcon(Weapon weapon, IconState state) => "spr_icon_weapon_" + s_weaponStems[(int)weapon] + Suffix(state);

        /// <summary>The GML sprite of <paramref name="item"/>'s icon: spr_item_extrabounce, _off or _selected.</summary>
        public static string ItemIcon(Item item, IconState state) => "spr_item_" + s_itemStems[(int)item] + Suffix(state);

        /// <summary>The hover text of an icon: its name and what it does.</summary>
        public static string Tooltip(string name, string about) => string.IsNullOrEmpty(about) ? name : name + ": " + about;

        /// <summary>The angle as draw_angle writes it: whole degrees, 0 to 359.</summary>
        public static string AngleText(double heading)
        {
            int whole = (int)Math.Round(Angles.Normalize(heading), MidpointRounding.AwayFromZero) % 360;
            return whole.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>draw_angle: true when the readout goes under the tank (it aims up, image_angle &lt; 180).</summary>
        public static bool AngleGoesBelow(double heading) => Angles.Normalize(heading) < 180.0;

        /// <summary>
        /// Pixels from the tank's centre down to the readout's top (negative: up), as draw_angle's degreeY = y + 36 or y - 60.
        /// </summary>
        public static int AngleOffsetY(double heading) => AngleGoesBelow(heading) ? AngleBelow : -AngleAbove;

        /// <summary>obj_degree: where the degree sign's left edge goes, from the readout's left edge, for text this wide.</summary>
        public static double DegreeX(double textWidth) => textWidth / 2.0 + DegreeGap;

        /// <summary>obj_guide: drawn while the turn runs and its player has not driven.</summary>
        public static bool GuideShows(bool turnRunning, bool moved) => turnRunning && !moved;

        /// <summary>obj_healthbar's text: "shown/max".</summary>
        public static string HealthText(int shown, int max) => shown.ToString(CultureInfo.InvariantCulture) + "/" + max.ToString(CultureInfo.InvariantCulture);

        /// <summary>obj_healthbar's fill: image_xscale = shown * 0.01 with maxhp 100, i.e. the share of the most health.</summary>
        public static double HealthFill(double shown, int max) => max <= 0 ? 0 : Math.Max(0, Math.Min(1, shown / max));

        /// <summary>The extra-bounce indicator shows while the current player has an extra bounce on this turn.</summary>
        public static bool ExtraBounceShows(int extraBounces) => extraBounces > 0;

        private static string Suffix(IconState state) => state == IconState.Off ? "_off" : state == IconState.Selected ? "_selected" : string.Empty;
    }
}
