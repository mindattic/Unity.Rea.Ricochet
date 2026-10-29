// The splash and the startup credits (U.RICO-T57), engine-free: the original's splashscreen and startup_credits rooms, both
// 1920 x 1080 at 60 steps a second on the rooms' silver (12632256), stepped here a room step at a time; SplashScreen draws
// what these say and plays what they ask for.
//
// splashscreen: object_splashscreen plays sfx_tech as the room's music (audio_play_music(sfx_tech, 1): looping) and writes
// "A game by" (fnt_score) and "Ryan DeBraal" (fnt_impact), white outlined 2 px in black, centred at room_height / 2 + 230
// and + 250. obj_lotus_card stands spr_lotus_card_noborder at the room's middle and draws it again where it trembles (every
// 2 steps a pixel toward a spot up to 6 px off the middle, a new spot once there); obj_spotlight's spr_spotlight pulses at
// (960, 712) (SpotlightPulse). Over the card obj_lotus_card_picture (depth -2) shows spr_beach out of nothing, 0.008 more
// alpha every 5 steps with sfx_startup from the first; until it is whole it flickers (hidden, nudged up to 4 px, a
// spr_ryan frame now and then, and one of sfx_static1-6), less the more of it shows; obj_line_emitter (depth -3) meanwhile
// sends obj_static_line's faint lines (0.3 alpha) down the card one step in ten, and spr_lotus_card_frame (obj_lotus_card_top,
// depth -4) frames it all. 200 steps after it is whole, or at once on a key or a click, it is finished, and the room's
// curtain (obj_fade_splashscreen) goes to black for startup_credits.
//
// startup_credits: obj_creditsimage's spr_creditsimage at (960, 576), and obj_startup_credits' note in red outlined in black
// (fnt_score) centred at room_height - 60. The music stops (audio_stop_music) and the pointer is hidden (obj_mouse.visible =
// false). After 600 steps, or at once on a click or any key but F1 and F12, the curtain (obj_fade_credits) goes to black and
// on. The rebuild's note is this project's own, typed out as obj_typewriter typed (a character every 2 steps, sfx_type_quiet
// on every second one). See docs/BIBLE.md, "Splash and startup credits", for what was fixed and what is not carried.
using System;
using System.Collections.Generic;
using Ricochet.Core.Art;

namespace Ricochet.Core.Menu
{
    /// <summary>The splash and startup credits rooms' places, art, words and timings, as the original had them.</summary>
    public static class SplashRoom
    {
        /// <summary>Both rooms' size in pixels and speed in steps a second.</summary>
        public const int RoomWidth = 1920;

        public const int RoomHeight = 1080;

        public const int StepsPerSecond = 60;

        /// <summary>Both rooms' colour (12632256: silver); neither has a background.</summary>
        public const int RoomColourRgb = 0xC0C0C0;

        /// <summary>The room's middle, where obj_lotus_card, obj_lotus_card_picture, obj_lotus_card_top and obj_line_emitter put themselves.</summary>
        public static readonly RoomSpot Middle = new RoomSpot(RoomWidth / 2, RoomHeight / 2);

        public const string CardSprite = "spr_lotus_card_noborder";

        public const string FrameSprite = "spr_lotus_card_frame";

        public const string PictureSprite = "spr_beach";

        /// <summary>The frame the picture flickers to now and then while it shows.</summary>
        public const string FlickerSprite = "spr_ryan";

        public const string SpotlightSprite = "spr_spotlight";

        /// <summary>obj_spotlight where the splashscreen room placed it, and its pulse.</summary>
        public static readonly RoomSpot Spotlight = new RoomSpot(960, 712);

        public const double SpotlightMinAlpha = 0.05;

        public const double SpotlightMaxAlpha = 0.15;

        public const double SpotlightRate = 0.001;

        /// <summary>object_splashscreen's two lines: fnt_score then fnt_impact, white outlined 2 px in black, centred, tops at these y.</summary>
        public const string ByLine = "A game by";

        public const int ByLineY = RoomHeight / 2 + 230;

        public const string AuthorLine = "Ryan DeBraal";

        public const int AuthorLineY = RoomHeight / 2 + 250;

        /// <summary>The words' outline, in pixels (draw_text_outline_color's 2).</summary>
        public const int Outline = 2;

        /// <summary>The splash's music: object_splashscreen's audio_play_music(sfx_tech, 1), looping.</summary>
        public const SoundCue Music = SoundCue.Tech;

        /// <summary>The picture's sound as it starts to show (stopped when a key or click finishes it).</summary>
        public const SoundCue Startup = SoundCue.Startup;

        /// <summary>obj_lotus_card_picture's choose(sfx_static1, ..., sfx_static6).</summary>
        public static readonly SoundCue[] Statics =
        {
            SoundCue.Static1, SoundCue.Static2, SoundCue.Static3, SoundCue.Static4, SoundCue.Static5, SoundCue.Static6,
        };

        // ------------------------------------------------------------------ startup_credits

        /// <summary>obj_creditsimage's picture and where the startup_credits room placed it.</summary>
        public const string CreditsImage = MenuLooks.CreditsImage;

        public static readonly RoomSpot CreditsImageSpot = new RoomSpot(960, 576);

        /// <summary>The note's top (room_height - 60), centred on the room's middle.</summary>
        public const int NoteY = RoomHeight - 60;

        /// <summary>The note's colour (c_red) and outline (c_black, 2 px).</summary>
        public const int NoteColourRgb = 0xFF0000;

        /// <summary>
        /// The note, in this project's own words. The original's warned of a GameMaker trouble with a wireless Xbox 360
        /// controller ("a known issue with the development software"), which the rebuild does not have.
        /// </summary>
        public const string Note = "Ricochet: Ryan DeBraal's 2013 GameMaker game, rebuilt in Unity by MindAttic.\n" +
                                   "The rooms, art and sounds are the original's.";

        /// <summary>obj_startup_credits' alarm[0]: steps before the room goes on by itself.</summary>
        public const int CreditsSteps = 600;

        /// <summary>The keys obj_startup_credits' any-key press passed over (F1 the window, F12 game_restart).</summary>
        public static bool SkipsCredits(string key)
        {
            return !string.Equals(key, "F1", StringComparison.OrdinalIgnoreCase) && !string.Equals(key, "F12", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>A whole room step's worth of <paramref name="seconds"/>: how many steps to run now, carrying the rest in <paramref name="clock"/>.</summary>
        public static int Steps(ref double clock, double seconds)
        {
            if (seconds > 0)
            {
                clock += seconds * StepsPerSecond;
            }

            int steps = (int)Math.Floor(clock);
            clock -= steps;
            return Math.Min(steps, StepsPerSecond); // a long hitch runs a second at most
        }

        // GameMaker's irandom_range: a whole number from low to high, both included.
        internal static int Between(Random dice, int low, int high) => high <= low ? low : dice.Next(low, high + 1);
    }

    /// <summary>obj_lotus_card's tremble: where its second card is drawn, a pixel at a time toward a spot near the middle.</summary>
    public sealed class LotusCard
    {
        /// <summary>How far off the middle a new spot may be (range) and how many steps between moves (delay).</summary>
        public const int Range = 6;

        public const int Delay = 2;

        private readonly Random _dice;
        private int _alarm = 1;
        private bool _moving;
        private int _targetX, _targetY;

        public LotusCard(Random dice)
        {
            _dice = dice ?? new Random();
            X = _targetX = SplashRoom.Middle.X;
            Y = _targetY = SplashRoom.Middle.Y;
        }

        /// <summary>Where the trembling card is drawn now (room pixels).</summary>
        public int X { get; private set; }

        public int Y { get; private set; }

        /// <summary>One room step.</summary>
        public void Step()
        {
            if (--_alarm > 0)
            {
                return;
            }

            _alarm = Delay;
            if (!_moving)
            {
                _targetX = SplashRoom.Middle.X + SplashRoom.Between(_dice, -Range, Range);
                _targetY = SplashRoom.Middle.Y + SplashRoom.Between(_dice, -Range, Range);
                _moving = true;
            }

            X += Math.Sign(_targetX - X);
            Y += Math.Sign(_targetY - Y);
            if (X == _targetX && Y == _targetY)
            {
                _moving = false;
            }
        }
    }

    /// <summary>One of obj_static_line's lines: its y on the card, falling its speed a step until near the card's foot.</summary>
    public sealed class StaticLine
    {
        internal StaticLine(double y, int speed)
        {
            Y = y;
            Speed = speed;
        }

        public double Y { get; internal set; }

        public int Speed { get; }
    }

    /// <summary>
    /// obj_lotus_card_picture with obj_line_emitter and its lines: the picture coming up out of nothing on the card, and when
    /// it is done.
    /// </summary>
    public sealed class SplashReveal
    {
        /// <summary>Steps between the picture's alpha steps (delay), alpha added each (fadespeed), and the hold once whole.</summary>
        public const int FadeDelay = 5;

        public const double FadeSpeed = 0.008;

        /// <summary>Alpha steps from nothing to whole (1 / 0.008).</summary>
        public const int FadeSteps = 125;

        public const int HoldSteps = 200;

        /// <summary>How far the picture is nudged when it shakes, and the one-in-ten chance of a new line each step.</summary>
        public const int Shake = 4;

        public const int LineChance = 10;

        /// <summary>The card's height (spr_lotus_card_frame, which obj_static_line wears) and width the lines span.</summary>
        public const int CardWidth = 238;

        public const int CardHeight = 343;

        /// <summary>A line stops this far above the card's foot.</summary>
        public const int LineStop = 20;

        public const int LineMinSpeed = 3;

        public const int LineMaxSpeed = 10;

        /// <summary>A line's alpha (draw_set_alpha(0.3)).</summary>
        public const double LineAlpha = 0.3;

        private readonly Random _dice;
        private readonly List<StaticLine> _lines = new List<StaticLine>();
        private int _fadeAlarm = 1;
        private int _holdAlarm;
        private int _fade;
        private bool _waiting;

        public SplashReveal(Random dice)
        {
            _dice = dice ?? new Random();
            Sprite = SplashRoom.PictureSprite;
            Visible = true;
            Flicker = 2;
            Emitting = true; // fixed: the picture starts to show as the room opens (the original waited for a key; the BIBLE)
        }

        /// <summary>The picture's alpha (image_alpha), 0 to 1.</summary>
        public double Alpha => _fade >= FadeSteps ? 1.0 : _fade * FadeSpeed;

        /// <summary>True once the picture is whole.</summary>
        public bool Whole => _fade >= FadeSteps;

        /// <summary>Whether the picture is drawn this step, and which frame.</summary>
        public bool Visible { get; private set; }

        public string Sprite { get; private set; }

        /// <summary>The picture's nudge off the middle this step (room pixels).</summary>
        public int OffsetX { get; private set; }

        public int OffsetY { get; private set; }

        /// <summary>The flicker's one-in-n: the more of the picture shows, the rarer each flicker.</summary>
        public int Flicker { get; private set; }

        /// <summary>True while obj_line_emitter sends lines.</summary>
        public bool Emitting { get; private set; }

        /// <summary>The lines falling down the card now.</summary>
        public IReadOnlyList<StaticLine> Lines => _lines;

        /// <summary>True once the picture is finished: its hold is over, or a key or a click finished it.</summary>
        public bool Finished { get; private set; }

        /// <summary>True from the step the curtain is to go to black (obj_fade_splashscreen.mode = "fadein").</summary>
        public bool Leaving { get; private set; }

        /// <summary>
        /// obj_lotus_card_picture's key and click: once the picture has started to show, it is finished and whole at once,
        /// and sfx_startup stops. True when it did (the caller stops the sound).
        /// </summary>
        public bool Press()
        {
            if (_fade <= 0 || Finished)
            {
                return false;
            }

            Finished = true;
            Emitting = false;
            Visible = true;
            _fade = FadeSteps;
            return true;
        }

        /// <summary>
        /// One room step: the alarms, then the picture's Step, the emitter's and the lines'. The sounds it starts go into
        /// <paramref name="sounds"/>.
        /// </summary>
        public void Step(ICollection<SoundCue> sounds)
        {
            // alarm[0]: the next alpha step, with sfx_startup on the first; once whole, the hold starts.
            if (--_fadeAlarm <= 0)
            {
                _fadeAlarm = FadeDelay;
                if (_fade == 0)
                {
                    sounds?.Add(SplashRoom.Startup);
                }

                if (_fade < FadeSteps)
                {
                    _fade++;
                }
                else
                {
                    Visible = true;
                    if (!_waiting)
                    {
                        _waiting = true;
                        _holdAlarm = HoldSteps;
                    }
                }
            }

            // alarm[1]: the hold is over.
            if (_holdAlarm > 0 && --_holdAlarm == 0)
            {
                Finished = true;
                Emitting = false;
            }

            if (Whole)
            {
                OffsetX = OffsetY = 0;
                Emitting = false;
                Sprite = SplashRoom.PictureSprite;
                if (Finished)
                {
                    Leaving = true;
                }
            }
            else
            {
                FlickerStep(sounds);
            }

            // obj_line_emitter, then each obj_static_line.
            if (Emitting && SplashRoom.Between(_dice, 1, LineChance) == 1)
            {
                _lines.Add(new StaticLine(SplashRoom.Middle.Y - CardHeight / 2.0, SplashRoom.Between(_dice, LineMinSpeed, LineMaxSpeed)));
            }

            for (int i = _lines.Count - 1; i >= 0; i--)
            {
                StaticLine line = _lines[i];
                if (line.Y < SplashRoom.Middle.Y + CardHeight / 2.0 - LineStop)
                {
                    line.Y += line.Speed;
                }
                else
                {
                    _lines.RemoveAt(i);
                }
            }
        }

        private void FlickerStep(ICollection<SoundCue> sounds)
        {
            // Fixed: the original's ladder tested > 0.5 first, so its rarer flickers were never reached (the BIBLE).
            double alpha = Alpha;
            if (alpha > 0.9) { Flicker = 32; }
            else if (alpha > 0.8) { Flicker = 16; }
            else if (alpha > 0.7) { Flicker = 12; }
            else if (alpha > 0.6) { Flicker = 6; }
            else if (alpha > 0.5) { Flicker = 4; }

            Sprite = SplashRoom.Between(_dice, 1, Flicker * 4) == 1 ? SplashRoom.FlickerSprite : SplashRoom.PictureSprite;
            if (SplashRoom.Between(_dice, 1, Flicker * 2) == 1)
            {
                sounds?.Add(SplashRoom.Statics[SplashRoom.Between(_dice, 0, SplashRoom.Statics.Length - 1)]);
            }

            Visible = SplashRoom.Between(_dice, 1, Flicker) != 1;
            OffsetX = OffsetY = 0;
            if (SplashRoom.Between(_dice, 1, Flicker) == 1)
            {
                OffsetX = SplashRoom.Between(_dice, -Shake, Shake);
                OffsetY = SplashRoom.Between(_dice, -Shake, Shake);
            }
        }
    }

    /// <summary>obj_typewriter: a text written out a character every few steps, with its typing sound.</summary>
    public sealed class Typewriter
    {
        /// <summary>type_speed: steps between characters.</summary>
        public const int TypeSpeed = 2;

        private readonly string _text;
        private int _alarm = TypeSpeed;

        public Typewriter(string text)
        {
            _text = text ?? string.Empty;
        }

        /// <summary>The whole text, and how much of it shows now.</summary>
        public string Text => _text;

        public int Shown { get; private set; }

        public string Written => _text.Substring(0, Shown);

        public bool Done => Shown >= _text.Length;

        /// <summary>One room step: true when it types a character that sounds (sfx_type_quiet on every second one).</summary>
        public bool Step()
        {
            if (Done || --_alarm > 0)
            {
                return false;
            }

            _alarm = TypeSpeed;
            Shown++;
            return Shown % 2 == 0;
        }

        /// <summary>The whole text at once.</summary>
        public void Finish()
        {
            Shown = _text.Length;
        }
    }

    /// <summary>obj_startup_credits: the note typed out, and when the room goes on.</summary>
    public sealed class StartupCredits
    {
        private int _steps;

        public StartupCredits(string note = SplashRoom.Note)
        {
            Typing = new Typewriter(note);
        }

        public Typewriter Typing { get; }

        /// <summary>Steps since the room opened.</summary>
        public int Steps => _steps;

        /// <summary>True from the step the curtain is to go to black.</summary>
        public bool Leaving { get; private set; }

        /// <summary>One room step: true when the typewriter's sound plays.</summary>
        public bool Step()
        {
            _steps++;
            if (_steps >= SplashRoom.CreditsSteps)
            {
                Leaving = true;
            }

            return Typing.Step();
        }

        /// <summary>A click, or the key named <paramref name="key"/> (null for a click or a pad's button).</summary>
        public void Press(string key = null)
        {
            if (key == null || SplashRoom.SkipsCredits(key))
            {
                Leaving = true;
            }
        }
    }
}
