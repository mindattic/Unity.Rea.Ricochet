// How the original's menus, buttons and the game screen's small buttons look (U.RICO-T78, docs/BIBLE.md "Menus and
// pictures"), engine-free so the Core tests can hold it to the original. Every one of them is drawn the same way in its
// Draw event: draw_set_alpha(0.5), its _over sprite while the pointer is on it (showmsg / over, set by Mouse Enter and
// cleared by Mouse Leave) or its _back sprite otherwise, then draw_set_alpha(1) and the face sprite over that.
//   obj_btn_* (spr_button, 226x64)   the label centred, 24 px up, in fnt_mono outlined 2 px (draw_text_outline).
//   obj_btn_*_less / _more / _last / _next (spr_arrowleft / spr_arrowright, 89x64)  one notch down or up per click, wrapping
//                         past either end (turnTime < 1 -> 30, maxBounces > 20 -> 1, count 0 -> 10, ...), with sfx_select.
//   obj_checkbox_* (spr_checkbox_unchecked / _checked, 64x64)  _over or _back under it at 0.5; the text 55 px to the right,
//                         5 px down, in fnt_score outlined 2 px.
//   obj_inputbox* (spr_inputbox, 387x64)  spr_inputbox_back at 0.5 under it always (no hover), the text in fnt_score.
//   obj_lbl_* (spr_label, never drawn: it is only the hover mask)  the caption in fnt_mono outlined 2 px, the value under it
//                         in fnt_score.
//   obj_btn_fire / useitem / endturn / randomizelevel (spr_tinybutton, 75x32)  the label in fnt_count, not outlined, 8 px up.
//   obj_btn_degreeleft / degreeright (spr_tinyarrowleft / right, 33x43), obj_btn_moveforward / movebackward
//                         (spr_tinyarrowup / down, 28x25)  the game screen's turn and drive arrows.
//   The rooms' titles ("Controls", "How to Play") in fnt_impact outlined 2 px; obj_pausemenu's "Paused" in fnt_mono
//                         outlined 2 px (its spr_pausemenu / spr_pausemenu_back draws are commented out in the original).
//   obj_keyboardlayout (spr_keyboardlayout), obj_slide (spr_howtoplay1) and obj_creditsimage (spr_creditsimage) centred in
//                         the controls, how-to-play and credits rooms, every menu room on bg_titlescreen.
using System;

namespace Ricochet.Core.Art
{
    /// <summary>The original's menu and game-screen controls that draw a back sprite under a face sprite.</summary>
    public enum Plate
    {
        /// <summary>obj_btn_*: spr_button.</summary>
        Button,

        /// <summary>obj_btn_*_less / _last: spr_arrowleft.</summary>
        ArrowLeft,

        /// <summary>obj_btn_*_more / _next: spr_arrowright.</summary>
        ArrowRight,

        /// <summary>obj_checkbox_*: spr_checkbox_unchecked / spr_checkbox_checked.</summary>
        Checkbox,

        /// <summary>obj_inputbox*: spr_inputbox.</summary>
        InputBox,

        /// <summary>obj_btn_fire, obj_btn_useitem, obj_btn_endturn: spr_tinybutton.</summary>
        TinyButton,

        /// <summary>obj_btn_degreeleft: spr_tinyarrowleft.</summary>
        TinyArrowLeft,

        /// <summary>obj_btn_degreeright: spr_tinyarrowright.</summary>
        TinyArrowRight,

        /// <summary>obj_btn_moveforward: spr_tinyarrowup.</summary>
        TinyArrowUp,

        /// <summary>obj_btn_movebackward: spr_tinyarrowdown.</summary>
        TinyArrowDown,
    }

    public static class MenuLooks
    {
        /// <summary>draw_set_alpha(0.5) before the _over / _back sprite.</summary>
        public const double BackAlpha = 0.5;

        /// <summary>The buttons' and the captions' face: fnt_mono outlined 2 px (draw_text_outline(x, y - 24, 2, ...)).</summary>
        public const string ButtonFont = "fnt_mono";
        public const int ButtonOutline = 2;

        /// <summary>A checkbox's text: fnt_score outlined 2 px.</summary>
        public const string CheckboxFont = "fnt_score";
        public const int CheckboxOutline = 2;

        /// <summary>An input box's text and a caption's value: fnt_score, not outlined.</summary>
        public const string FieldFont = "fnt_score";

        /// <summary>The game screen's tiny buttons: fnt_count, not outlined (draw_text, not draw_text_outline).</summary>
        public const string TinyFont = "fnt_count";

        /// <summary>A room's title (obj_Controls, obj_howtoplay, obj_settings ...): fnt_impact outlined 2 px.</summary>
        public const string TitleFont = "fnt_impact";
        public const int TitleOutline = 2;

        /// <summary>The menu rooms' background.</summary>
        public const string RoomBackground = "bg_titlescreen";

        /// <summary>The pictures the controls, how-to-play and credits rooms centre.</summary>
        public const string KeyboardLayout = "spr_keyboardlayout";
        public const string HowToPlaySlide = "spr_howtoplay1";
        public const string CreditsImage = "spr_creditsimage";

        /// <summary>The title's letters, R I C O C H E T (obj_letter_R ... obj_letter_T; the second C is spr_letter_C again).</summary>
        public static readonly string[] TitleLetters =
        {
            "spr_letter_R", "spr_letter_I", "spr_letter_C", "spr_letter_O", "spr_letter_C", "spr_letter_H", "spr_letter_E", "spr_letter_T",
        };

        /// <summary>The face sprite of <paramref name="plate"/> (a checkbox's depends on <paramref name="isChecked"/>).</summary>
        public static string Face(Plate plate, bool isChecked = false)
        {
            switch (plate)
            {
                case Plate.Button: return "spr_button";
                case Plate.ArrowLeft: return "spr_arrowleft";
                case Plate.ArrowRight: return "spr_arrowright";
                case Plate.Checkbox: return isChecked ? "spr_checkbox_checked" : "spr_checkbox_unchecked";
                case Plate.InputBox: return "spr_inputbox";
                case Plate.TinyButton: return "spr_tinybutton";
                case Plate.TinyArrowLeft: return "spr_tinyarrowleft";
                case Plate.TinyArrowRight: return "spr_tinyarrowright";
                case Plate.TinyArrowUp: return "spr_tinyarrowup";
                case Plate.TinyArrowDown: return "spr_tinyarrowdown";
                default: throw new ArgumentOutOfRangeException(nameof(plate), plate, null);
            }
        }

        /// <summary>
        /// The sprite drawn at <see cref="BackAlpha"/> under the face: its _over sprite while <paramref name="over"/>, its
        /// _back sprite otherwise. The checkboxes share spr_checkbox_over / spr_checkbox_back; an input box has no hover.
        /// </summary>
        public static string Back(Plate plate, bool over)
        {
            switch (plate)
            {
                case Plate.Checkbox: return over ? "spr_checkbox_over" : "spr_checkbox_back";
                case Plate.InputBox: return "spr_inputbox_back";
                default: return Face(plate) + (over ? "_over" : "_back");
            }
        }

        /// <summary>
        /// One click of a less / more (or last / next) arrow: <paramref name="value"/> moved by <paramref name="delta"/>,
        /// wrapping past <paramref name="max"/> to <paramref name="min"/> and below <paramref name="min"/> to
        /// <paramref name="max"/>, as every obj_btn_*_less / _more did.
        /// </summary>
        public static double Step(double value, double delta, double min, double max)
        {
            if (max <= min)
            {
                return min;
            }

            double next = value + delta;
            const double slack = 1e-9;
            if (next > max + slack)
            {
                return min;
            }

            if (next < min - slack)
            {
                return max;
            }

            return Math.Max(min, Math.Min(max, next));
        }

        /// <summary>How far one arrow click moves a setting: one for a whole-number setting, a twentieth of the span otherwise.</summary>
        public static double Notch(double min, double max, bool wholeNumbers) => wholeNumbers ? 1 : (max - min) / 20.0;
    }
}
