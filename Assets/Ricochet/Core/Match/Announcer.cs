// The announcer (UR-T8): an on-air personality whose portrait and name box slide in with a line of commentary between
// turns. The 2013 original (design reference only, UR-LAW-2) had seven personalities, picked on the settings screen
// (obl_lbl_announcer, obj_btn_announcer_last/next: Kurt Sly, Rye Wheatley, Sid Steinburger, Vlad, Matt Baxter, Doctor and
// Haggis McKnight), each with ten recorded lines for each of three moments: a welcome when the game starts
// (random_announcement_welcome, from obj_game's create event), a win when a shot knocks an opponent out
// (random_announcement_win, from hit.gml) and a lose when a shot goes off without hitting anyone
// (random_announcement_lose, from obj_explosion, which bullet_step.gml made at the bounce limit). The "announcements"
// setting (obj_btn_announcer_less/more, 0 to 4) is how often it speaks: 0 never; 1 every time; 2, 3 and 4 one time in 3,
// 6 and 9 (the welcome always, unless 0). Lines are drawn without repeats until all ten of a kind have been heard
// (random_win_number's shuffled list). The original's lines were voice recordings; here they are written lines with the
// players' names in them, the same for every personality, shown in the personality's name box.
using System;
using System.Collections.Generic;

namespace Ricochet.Core.Match
{
    public enum AnnouncementKind
    {
        /// <summary>The match is starting (random_announcement_welcome).</summary>
        Welcome,

        /// <summary>A shot knocked an opponent out (random_announcement_win).</summary>
        Win,

        /// <summary>A shot went off without hitting anyone (random_announcement_lose).</summary>
        Lose,
    }

    /// <summary>One of the on-air personalities.</summary>
    public sealed class AnnouncerVoice
    {
        internal AnnouncerVoice(string key, string name, byte r, byte g, byte b)
        {
            Key = key;
            Name = name;
            R = r;
            G = g;
            B = b;
        }

        /// <summary>The original's settings key ("sly", "rye", ...).</summary>
        public string Key { get; }

        /// <summary>The name in the name box ("Kurt Sly").</summary>
        public string Name { get; }

        /// <summary>The portrait's colour (the original had a drawn profile for two of them, a placeholder for the rest).</summary>
        public byte R { get; }

        public byte G { get; }

        public byte B { get; }

        /// <summary>The portrait's monogram: the first letter of each word ("KS", "V").</summary>
        public string Initials
        {
            get
            {
                string initials = string.Empty;
                foreach (string word in Name.Split(' '))
                {
                    if (word.Length > 0 && initials.Length < 2)
                    {
                        initials += char.ToUpperInvariant(word[0]);
                    }
                }

                return initials;
            }
        }

        public override string ToString() => Name;
    }

    /// <summary>A line the announcer says: what kind, which of the ten, and the seats it is about (-1 for none).</summary>
    public readonly struct Announcement
    {
        public Announcement(AnnouncementKind kind, int line, int subject, int other)
        {
            Kind = kind;
            Line = line;
            Subject = subject;
            Other = other;
        }

        public AnnouncementKind Kind { get; }

        /// <summary>0 to <see cref="Announcer.LinesPerKind"/> - 1.</summary>
        public int Line { get; }

        /// <summary>The seat the line is about: the shooter of a win or a lose; -1 for a welcome.</summary>
        public int Subject { get; }

        /// <summary>The second seat a line names: who a win knocked out; otherwise -1.</summary>
        public int Other { get; }
    }

    public sealed class Announcer
    {
        /// <summary>Lines of each kind (the original recorded ten per kind per personality).</summary>
        public const int LinesPerKind = 10;

        /// <summary>The highest "announcements" setting (the rarest).</summary>
        public const int MaxFrequency = 4;

        /// <summary>The setting the original started with: every time.</summary>
        public const int DefaultFrequency = 1;

        /// <summary>The personality the original started with (globals.gml's announcer = "sly").</summary>
        public const int DefaultVoice = 0;

        private static readonly AnnouncerVoice[] s_voices =
        {
            new AnnouncerVoice("sly", "Kurt Sly", 214, 158, 46),
            new AnnouncerVoice("rye", "Rye Wheatley", 196, 120, 60),
            new AnnouncerVoice("sid", "Sid Steinburger", 72, 150, 210),
            new AnnouncerVoice("vlad", "Vlad", 150, 60, 170),
            new AnnouncerVoice("baxter", "Matt Baxter", 70, 170, 110),
            new AnnouncerVoice("doctor", "Doctor", 200, 70, 80),
            new AnnouncerVoice("scottish", "Haggis McKnight", 60, 120, 90),
        };

        // {0} is the subject's name, {1} the other's.
        private static readonly string[] s_welcome =
        {
            "Welcome, one and all, to another night of Ricochet!",
            "Good evening, and welcome to the bounce house. Mind the walls.",
            "Pick your angles, everybody: Ricochet is on the air!",
            "Welcome back! The shortest way to a target is rarely a straight line.",
            "A beautiful night for geometry. Welcome to Ricochet!",
            "The arena is set and the walls are polished. Let's play!",
            "Welcome, contestants. Everything bounces, and nothing forgives.",
            "Live from the arena, it's Ricochet! Keep your heads down.",
            "Greetings, sharpshooters. Tonight we find out who really knows their angles.",
            "Welcome to the show where every wall is a second chance.",
        };

        private static readonly string[] s_win =
        {
            "{0} sends {1} packing!",
            "And {1} is out! A beautiful shot from {0}.",
            "What a bank shot! {0} takes {1} out of the round!",
            "{1} never saw it coming. Well played, {0}.",
            "Down goes {1}! {0} makes it look easy.",
            "Textbook! {0} puts {1} on the bench.",
            "{0} has been studying the angles. {1} has not.",
            "Off the wall and right into {1}! Take a bow, {0}.",
            "That's the end of the line for {1}, courtesy of {0}.",
            "Ricochet! {0} knocks {1} clean out of the round!",
        };

        private static readonly string[] s_lose =
        {
            "Oh, so close, {0}. So very close.",
            "{0} hits... absolutely nothing.",
            "The walls thank you for your contribution, {0}.",
            "That one's going to need some work, {0}.",
            "{0} finds the only empty spot in the arena.",
            "A bold choice of angle, {0}. A wrong one, but bold.",
            "Somewhere a geometry teacher is weeping, {0}.",
            "Not even close, {0}! The crowd goes mild.",
            "{0} may want to check that aim.",
            "And it fizzles out. Better luck next turn, {0}.",
        };

        private readonly Random _dice;
        private readonly List<int>[] _decks = { new List<int>(), new List<int>(), new List<int>() };

        public Announcer(int voice = DefaultVoice, int frequency = DefaultFrequency, Random dice = null)
        {
            Voice = ClampVoice(voice);
            Frequency = ClampFrequency(frequency);
            _dice = dice ?? new Random();
        }

        /// <summary>Every personality, in the settings screen's order (obj_btn_announcer_next).</summary>
        public static IReadOnlyList<AnnouncerVoice> Voices => s_voices;

        /// <summary>The personality on the air (an index into <see cref="Voices"/>).</summary>
        public int Voice { get; }

        /// <summary>How often it speaks: 0 never, 1 always, 2 to 4 rarer and rarer.</summary>
        public int Frequency { get; }

        public static int ClampVoice(int voice) => Math.Max(0, Math.Min(s_voices.Length - 1, voice));

        public static int ClampFrequency(int frequency) => Math.Max(0, Math.Min(MaxFrequency, frequency));

        /// <summary>The personality at <paramref name="voice"/> (clamped into range).</summary>
        public static AnnouncerVoice VoiceAt(int voice) => s_voices[ClampVoice(voice)];

        /// <summary>
        /// random_announcement_win/lose's range: a win or lose line is said one time in this many (0 for never). The
        /// welcome is said whenever this is above 0.
        /// </summary>
        public static int Odds(int frequency)
        {
            switch (ClampFrequency(frequency))
            {
                case 0: return 0;
                case 1: return 1;
                case 2: return 3;
                case 3: return 6;
                default: return 9;
            }
        }

        /// <summary>The settings screen's word for a frequency: "Off", "Every time", "Often", "Sometimes", "Rarely".</summary>
        public static string FrequencyName(int frequency)
        {
            switch (ClampFrequency(frequency))
            {
                case 0: return "Off";
                case 1: return "Every time";
                case 2: return "Often";
                case 3: return "Sometimes";
                default: return "Rarely";
            }
        }

        /// <summary>The match is starting: a welcome line, unless the announcer is off.</summary>
        public Announcement? Welcome()
        {
            return Frequency > 0 ? new Announcement(AnnouncementKind.Welcome, Draw(AnnouncementKind.Welcome), -1, -1) : (Announcement?)null;
        }

        /// <summary><paramref name="shooter"/> knocked <paramref name="knockedOut"/> out: a win line, as often as the setting says.</summary>
        public Announcement? Win(int shooter, int knockedOut)
        {
            return Speaks() ? new Announcement(AnnouncementKind.Win, Draw(AnnouncementKind.Win), shooter, knockedOut) : (Announcement?)null;
        }

        /// <summary><paramref name="shooter"/>'s shot went off without hitting anyone: a lose line, as often as the setting says.</summary>
        public Announcement? Lose(int shooter)
        {
            return Speaks() ? new Announcement(AnnouncementKind.Lose, Draw(AnnouncementKind.Lose), shooter, -1) : (Announcement?)null;
        }

        /// <summary>
        /// The words of <paramref name="announcement"/>, with the names <paramref name="nameOf"/> gives its seats ("someone"
        /// for a seat it does not know).
        /// </summary>
        public static string Text(Announcement announcement, Func<int, string> nameOf)
        {
            string[] lines = LinesOf(announcement.Kind);
            string line = lines[Math.Max(0, Math.Min(lines.Length - 1, announcement.Line))];
            return string.Format(line, NameOf(announcement.Subject, nameOf), NameOf(announcement.Other, nameOf));
        }

        private static string NameOf(int seat, Func<int, string> nameOf)
        {
            string name = seat >= 0 && nameOf != null ? nameOf(seat) : null;
            return string.IsNullOrEmpty(name) ? "someone" : name;
        }

        private static string[] LinesOf(AnnouncementKind kind)
        {
            switch (kind)
            {
                case AnnouncementKind.Win: return s_win;
                case AnnouncementKind.Lose: return s_lose;
                default: return s_welcome;
            }
        }

        private bool Speaks()
        {
            int odds = Odds(Frequency);
            return odds > 0 && _dice.Next(odds) == 0;
        }

        // random_win_number.gml: the ten lines of a kind in a shuffled order, each once, then shuffled again.
        private int Draw(AnnouncementKind kind)
        {
            List<int> deck = _decks[(int)kind];
            if (deck.Count == 0)
            {
                for (int i = 0; i < LinesPerKind; i++)
                {
                    deck.Add(i);
                }

                for (int i = deck.Count - 1; i > 0; i--)
                {
                    int j = _dice.Next(i + 1);
                    int swap = deck[i];
                    deck[i] = deck[j];
                    deck[j] = swap;
                }
            }

            int line = deck[0];
            deck.RemoveAt(0);
            return line;
        }
    }
}
