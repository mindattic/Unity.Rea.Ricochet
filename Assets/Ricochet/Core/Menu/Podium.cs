// The endgame room's podium (U.RICO-T56), engine-free: where obj_endgame put everything in the 1920 x 1080 endgame room and
// how its winner turns. obj_pedastal stands at the room's middle (x = room_width / 2, the room's y 864); the first three of
// the ranking (ds_grid_sort of every seat's score, highest first) stand on it as obj_winner_bottom's hull under
// obj_winner_top's turret, both in the player's colour: first at the centre (room_height - 294), second to the left
// (- 89, room_height - 249) and third to the right (+ 89, room_height - 200). Each turret is set to image_angle 270
// (facing down); the champion's alone spins, spinAngle starting at 90 and losing a degree every step. Over it "Wins!" in
// fnt_impact at the room's middle with the champion's name in fnt_mono, in their colour, a border (32) above it; at the
// top the leaderboard, "Rank" / "Player" / "Score" in fnt_score at y 100 and a row a seat 30 px apart from y 130, in each
// player's colour. obj_btn_back_endgame ("Main Menu", "Return to Title Screen") sits at (128, 1024). The room plays
// music_victory once through (audio_play_music(music_victory, 0)) on bg_stadium, tiled. Plain data and rules; the
// presentation's EndgameRecap draws what this says.
using System;

namespace Ricochet.Core.Menu
{
    /// <summary>A spot in the endgame room, in room pixels (y growing downward).</summary>
    public readonly struct RoomSpot
    {
        public RoomSpot(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }

        public int Y { get; }

        public override string ToString() => "(" + X + ", " + Y + ")";
    }

    /// <summary>obj_endgame's room: the podium's places, the winner's spin, the leaderboard's rows and the room's sound.</summary>
    public static class Podium
    {
        /// <summary>The endgame room's size, in pixels.</summary>
        public const int RoomWidth = 1920;

        public const int RoomHeight = 1080;

        /// <summary>The endgame room's background (htiled and vtiled).</summary>
        public const string Background = "bg_stadium";

        /// <summary>The endgame room's colour under its background (12632256: silver), shown when the art is missing.</summary>
        public const int RoomColourRgb = 0xC0C0C0;

        /// <summary>obj_pedastal's sprite (obj_pedastool, never placed, shares it).</summary>
        public const string PedestalSprite = "spr_pedastal";

        /// <summary>obj_pedastal: x = room_width / 2 (obj_endgame's create), y where the room placed it.</summary>
        public static readonly RoomSpot Pedestal = new RoomSpot(RoomWidth / 2, 864);

        /// <summary>How many of the ranking stand on the podium: first, second and third.</summary>
        public const int Places = 3;

        /// <summary>obj_winner_top's image_angle as created: 270, facing down.</summary>
        public const double TurretAngle = 270;

        /// <summary>obj_endgame's spinAngle before its first step.</summary>
        public const double SpinStart = 90;

        /// <summary>The degrees spinAngle loses each step (clockwise on screen).</summary>
        public const double SpinPerStep = 1;

        /// <summary>The room's speed: steps a second.</summary>
        public const int StepsPerSecond = 60;

        /// <summary>
        /// Where the tank in <paramref name="place"/> (0 first, 1 second, 2 third) stands: the centre, left and right of the
        /// pedestal. Throws for a place off the podium.
        /// </summary>
        public static RoomSpot Spot(int place)
        {
            switch (place)
            {
                case 0: return new RoomSpot(RoomWidth / 2, RoomHeight - 294);
                case 1: return new RoomSpot(RoomWidth / 2 - 89, RoomHeight - 249);
                case 2: return new RoomSpot(RoomWidth / 2 + 89, RoomHeight - 200);
                default: throw new ArgumentOutOfRangeException(nameof(place), place, "only the first three stand on the podium");
            }
        }

        /// <summary>How many tanks stand on the podium for <paramref name="seats"/> ranked seats (at most three).</summary>
        public static int OnPodium(int seats) => Math.Max(0, Math.Min(Places, seats));

        /// <summary>
        /// The turret angle (GameMaker degrees, counterclockwise, 0 to the right, in [0, 360)) of the tank in
        /// <paramref name="place"/> after <paramref name="steps"/> whole steps of the room: 270 for every place before
        /// the first step and for second and third throughout; the champion's then spinAngle, 90 less a degree a step
        /// (89 after the first).
        /// </summary>
        public static double Angle(int place, long steps)
        {
            if (place != 0 || steps <= 0)
            {
                return TurretAngle;
            }

            return Wrap(SpinStart - SpinPerStep * steps);
        }

        /// <summary><paramref name="degrees"/> brought into [0, 360).</summary>
        public static double Wrap(double degrees)
        {
            double wrapped = degrees % 360.0;
            return wrapped < 0 ? wrapped + 360.0 : wrapped;
        }

        // ------------------------------------------------------------------ the headline

        /// <summary>"Wins!": fnt_impact, white, outlined 2 px, centred at the room's middle (its top on y 540).</summary>
        public const string WinsText = "Wins!";

        public static readonly RoomSpot Wins = new RoomSpot(RoomWidth / 2, RoomHeight / 2);

        /// <summary>The champion's name: fnt_mono in their colour outlined 2 px in black, border (32) above "Wins!".</summary>
        public static readonly RoomSpot ChampionName = new RoomSpot(RoomWidth / 2, RoomHeight / 2 - 32);

        // ------------------------------------------------------------------ the leaderboard

        /// <summary>The headings' row (fnt_score, white).</summary>
        public const int HeaderY = 100;

        /// <summary>The first seat's row; every next one 30 px under it.</summary>
        public const int FirstRowY = 130;

        public const int RowHeight = 30;

        /// <summary>The rank's left edge, the player's centre and the score's right edge.</summary>
        public const int RankX = RoomWidth / 2 - 200;

        public const int PlayerX = RoomWidth / 2;

        public const int ScoreX = RoomWidth / 2 + 200;

        public const string RankHeading = "Rank";

        public const string PlayerHeading = "Player";

        public const string ScoreHeading = "Score";

        /// <summary>The top of the row for the seat in <paramref name="place"/> (0 first).</summary>
        public static int RowY(int place) => FirstRowY + RowHeight * place;

        // ------------------------------------------------------------------ the way out

        /// <summary>obj_btn_back_endgame's place, label and hover message.</summary>
        public static readonly RoomSpot MainMenuButton = new RoomSpot(128, 1024);

        public const string MainMenuLabel = "Main Menu";

        public const string MainMenuTip = "Return to Title Screen";

        /// <summary>The rebuild's vote for a rematch, where the menu rooms put their way on (1792, 1024).</summary>
        public static readonly RoomSpot RematchButton = new RoomSpot(1792, 1024);

        public const string RematchLabel = "Vote for a rematch";

        /// <summary>The rebuild's Close (look at the level), where the menu rooms put their second button (1792, 928).</summary>
        public static readonly RoomSpot CloseButton = new RoomSpot(1792, 928);

        public const string CloseLabel = "Close";

        // ------------------------------------------------------------------ the room's sound

        /// <summary>audio_play_music(music_victory, 0): the victory track, once through, in place of whatever played.</summary>
        public const MusicTrack Track = MusicTrack.Victory;

        public const bool TrackLoops = false;

        /// <summary>
        /// The crowd as the podium comes up: sfx_woot (in the original's folder, played nowhere) and sfx_applause (the
        /// round's last knock-out's, hit.gml). See the BIBLE's Endgame podium.
        /// </summary>
        public static readonly SoundCue[] Cheer = { SoundCue.Woot, SoundCue.Applause };
    }
}
