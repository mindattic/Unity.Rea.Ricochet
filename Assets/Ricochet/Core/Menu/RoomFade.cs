// U.RICO-T53, the room fades: every room of the original carried its own copy of one black curtain (obj_fade, obj_fade_game,
// obj_fade_mainmenu, obj_fade_maproom, obj_fade_settings, obj_fade_controls, obj_fade_credits, obj_fade_endgame,
// obj_fade_howtoplay, obj_fade_multiplayer, obj_fade_splashscreen): spr_black stretched over the room at depth -10000,
// created at image_alpha 1 in mode "fadeout" so the room comes up out of black, its alpha stepping 0.01 a step (fadespeed)
// until it is clear and the mode goes "off". Where a room left by fading to black first (obj_maproom, obj_startup_credits,
// obj_lotus_card_picture set the curtain's mode to "fadein"), the alpha climbed 0.01 a step and the room_goto came once
// it was black. The eleven copies differ only in where that room_goto went, so the rebuild has this one curtain for every
// screen change: Open (the new screen out of black) and Cover (to black, then the change). How long a fade takes is data
// (menu.json's fadeSeconds: 100 steps of 0.01 at the 60 steps a second of the rooms that carry one).
using System;

namespace Ricochet.Core.Menu
{
    /// <summary>What the curtain is doing (obj_fade's mode).</summary>
    public enum FadeMode
    {
        /// <summary>Still: clear after a fade out, black after a fade in ("off").</summary>
        Off,

        /// <summary>Going to black, before the screen changes ("fadein").</summary>
        In,

        /// <summary>Clearing from black, as a screen comes up ("fadeout").</summary>
        Out,
    }

    /// <summary>One black curtain over every screen (obj_fade and its ten copies): its alpha and where it is going.</summary>
    public sealed class RoomFade
    {
        /// <summary>The original's length of a fade: 100 steps of 0.01 at 60 steps a second.</summary>
        public const double DefaultSeconds = 100.0 / 60.0;

        private readonly double _seconds;

        /// <summary>A clear curtain whose fades each take <paramref name="seconds"/> (the data's, when not given).</summary>
        public RoomFade(double seconds = 0)
        {
            _seconds = seconds > 0 ? seconds : Seconds;
        }

        /// <summary>How long a fade takes, from menu.json (the original's 100 steps at 60 a second without the data).</summary>
        public static double Seconds
        {
            get
            {
                Ricochet.Core.Data.GameData data = Ricochet.Core.Data.GameData.Current;
                double seconds = data != null && data.Menu != null ? data.Menu.FadeSeconds : 0;
                return seconds > 0 ? seconds : DefaultSeconds;
            }
        }

        /// <summary>How black the curtain is: 0 clear, 1 black.</summary>
        public float Alpha { get; private set; }

        /// <summary>What the curtain is doing.</summary>
        public FadeMode Mode { get; private set; } = FadeMode.Off;

        /// <summary>True while the curtain is going to black or clearing.</summary>
        public bool Fading => Mode != FadeMode.Off;

        /// <summary>True while it is going to black, before the screen changes.</summary>
        public bool Covering => Mode == FadeMode.In;

        /// <summary>How long one fade takes, in seconds.</summary>
        public double FadeSeconds => _seconds;

        /// <summary>A new screen: black at once, then clearing (an obj_fade_* created with image_alpha 1, mode "fadeout").</summary>
        public void Open()
        {
            Alpha = 1f;
            Mode = FadeMode.Out;
        }

        /// <summary>To black from wherever it is now (mode "fadein"); <see cref="Step"/> says when it gets there.</summary>
        public void Cover()
        {
            Mode = FadeMode.In;
        }

        /// <summary>Clear and still at once.</summary>
        public void Clear()
        {
            Alpha = 0f;
            Mode = FadeMode.Off;
        }

        /// <summary>
        /// Moves the curtain on by <paramref name="seconds"/>; true on the step a fade in reaches black, when the screen is
        /// to change (the copies' room_goto once image_alpha reached 1).
        /// </summary>
        public bool Step(double seconds)
        {
            if (Mode == FadeMode.Off || seconds <= 0)
            {
                return false;
            }

            float delta = (float)(seconds / _seconds);
            if (Mode == FadeMode.Out)
            {
                Alpha = Math.Max(0f, Alpha - delta);
                if (Alpha <= 0f)
                {
                    Mode = FadeMode.Off;
                }

                return false;
            }

            Alpha = Math.Min(1f, Alpha + delta);
            if (Alpha < 1f)
            {
                return false;
            }

            Mode = FadeMode.Off;
            return true;
        }
    }
}
