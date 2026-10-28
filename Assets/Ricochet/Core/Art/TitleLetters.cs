// The title's and the main menu's RICOCHET letters (U.RICO-T52), engine-free so the Core tests can hold them to the original.
// obj_letter_parent started every letter moving 1 px right and 1 px up a step (hspeed 1, vspeed -1); each step it turned a
// letter back (its speed negated) once it stood past a room edge (x < 0 or x > room_width, y < 0 or y > room_height), and
// obj_letter_R ... obj_letter_T then showed the mirror letter while moving left (R <-> T, I <-> E, C <-> H, O <-> C2) and their
// own while moving right. GameMaker moved an instance by its speed after its step event, so a step is: check the edges, pick
// the sprite, then move. The titlescreen and mainmenu rooms ran at 180 steps a second, on a 1920 x 1080 room; the title
// placed letter i at (64 + 64 i, 576 - 32 i), the main menu at (128 + 64 i, 416 - 32 i). Nothing in the original made the
// letters drop in: they drift from where the room placed them (docs/BIBLE.md "Title and main menu").
// The main menu's floor also scrolled: mainmenu's bg_titlescreen, tiled, moving 1 px right a step; the title's stood still.
namespace Ricochet.Core.Art
{
    /// <summary>Which room the letters are in: where they start.</summary>
    public enum LetterRoom
    {
        /// <summary>titlescreen: letter i at (64 + 64 i, 576 - 32 i), a still floor.</summary>
        Title,

        /// <summary>mainmenu: letter i at (128 + 64 i, 416 - 32 i), the floor scrolling right.</summary>
        MainMenu,
    }

    /// <summary>The eight drifting letters of one room, stepped as obj_letter_parent stepped them.</summary>
    public sealed class TitleLetters
    {
        /// <summary>The titlescreen and mainmenu rooms' speed: steps a second.</summary>
        public const int StepsPerSecond = 180;

        /// <summary>The rooms' size.</summary>
        public const double RoomWidth = 1920;
        public const double RoomHeight = 1080;

        /// <summary>mainmenu's bg_titlescreen hspeed: pixels a step to the right (the title's is 0).</summary>
        public const double FloorScrollPerStep = 1;

        /// <summary>How many letters (R I C O C H E T).</summary>
        public const int Count = 8;

        private readonly double[] _x = new double[Count];
        private readonly double[] _y = new double[Count];
        private readonly double[] _h = new double[Count];
        private readonly double[] _v = new double[Count];

        public TitleLetters(LetterRoom room)
        {
            Room = room;
            for (int i = 0; i < Count; i++)
            {
                double[] start = Start(room, i);
                _x[i] = start[0];
                _y[i] = start[1];
                _h[i] = 1;
                _v[i] = -1;
            }
        }

        public LetterRoom Room { get; }

        /// <summary>Steps taken.</summary>
        public long Steps { get; private set; }

        /// <summary>How far the room's floor has scrolled right, in pixels (0 on the title).</summary>
        public double FloorOffset => Room == LetterRoom.MainMenu ? Steps * FloorScrollPerStep : 0;

        /// <summary>Where the room placed letter <paramref name="index"/> (x, y; room pixels, y down).</summary>
        public static double[] Start(LetterRoom room, int index)
        {
            return room == LetterRoom.Title
                ? new[] { 64.0 + 64.0 * index, 576.0 - 32.0 * index }
                : new[] { 128.0 + 64.0 * index, 416.0 - 32.0 * index };
        }

        /// <summary>Letter <paramref name="index"/>'s x now.</summary>
        public double X(int index) => _x[index];

        /// <summary>Letter <paramref name="index"/>'s y now (room pixels, y down).</summary>
        public double Y(int index) => _y[index];

        /// <summary>Letter <paramref name="index"/>'s hspeed now.</summary>
        public double HSpeed(int index) => _h[index];

        /// <summary>Letter <paramref name="index"/>'s vspeed now.</summary>
        public double VSpeed(int index) => _v[index];

        /// <summary>
        /// The sprite letter <paramref name="index"/> shows now: its own (MenuLooks.TitleLetters) moving right, the mirror
        /// letter's (the one at 7 - index) moving left.
        /// </summary>
        public string Sprite(int index) => MenuLooks.TitleLetters[_h[index] < 0 ? Count - 1 - index : index];

        /// <summary>One step of every letter: turn back past an edge, then move.</summary>
        public void Step()
        {
            for (int i = 0; i < Count; i++)
            {
                if (_x[i] < 0 || _x[i] > RoomWidth)
                {
                    _h[i] = -_h[i];
                }

                if (_y[i] < 0 || _y[i] > RoomHeight)
                {
                    _v[i] = -_v[i];
                }

                _x[i] += _h[i];
                _y[i] += _v[i];
            }

            Steps++;
        }

        /// <summary>Takes <paramref name="steps"/> steps.</summary>
        public void Step(int steps)
        {
            for (int i = 0; i < steps; i++)
            {
                Step();
            }
        }
    }
}
