// The announcer (UR-T8): an on-air personality whose portrait and name box slide in with a line of commentary between
// turns. The 2013 original (design reference only, UR-LAW-2) had seven personalities, picked on the settings screen
// (obl_lbl_announcer, obj_btn_announcer_last/next: Kurt Sly, Rye Wheatley, Sid Steinburger, Vlad, Matt Baxter, Doctor and
// Haggis McKnight), each with ten recorded lines for each of three moments: a welcome when the game starts
// (random_announcement_welcome, from obj_game's create event), a win when a shot knocks an opponent out
// (random_announcement_win, from hit.gml) and a lose when a shot goes off without hitting anyone
// (random_announcement_lose, from obj_explosion, which bullet_step.gml made at the bounce limit). The "announcements"
// setting (obj_btn_announcer_less/more, 0 to 4) is how often it speaks: 0 never; 1 every time; 2, 3 and 4 one time in 3,
// 6 and 9 (the welcome always, unless 0). Lines are drawn without repeats until all ten of a kind have been heard
// (random_win_number's shuffled list). The original's lines were voice recordings: U.RICO-T51 imports each personality's
// thirty (Resources/Ricochet/Voice, Tools/import-gml-sound.ps1) and the line drawn here picks the recording
// (Recording: sfx_<key>_<kind><line + 1>), which plays when its card appears; the written line, with the players' names
// in it, stays on the card as its caption, and is all there is when the sound is muted. Each personality's portrait is
// data too (Portrait: the original's sprite, spr_profile_dummy shared by four). The personalities, the odds and the lines are data (announcer.json, UR-T35).
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
        internal AnnouncerVoice(string key, string name, byte r, byte g, byte b, string portrait)
        {
            Key = key;
            Name = name;
            R = r;
            G = g;
            B = b;
            Portrait = portrait ?? string.Empty;
        }

        /// <summary>The original's settings key ("sly", "rye", ...), which also names the recordings (sfx_sly_win1).</summary>
        public string Key { get; }

        /// <summary>The name in the name box ("Kurt Sly").</summary>
        public string Name { get; }

        /// <summary>
        /// The GML sprite the card's portrait draws (obj_announcer_portrait's sprite_index): spr_profile_sly, spr_profile_rye,
        /// spr_profile_sid, or the shared spr_profile_dummy.
        /// </summary>
        public string Portrait { get; }

        /// <summary>The colour of the panel shown in the portrait's place only when the art is missing.</summary>
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

        /// <summary>
        /// The personality the original started with: Matt Baxter, as its shipped config.ini and the Default button
        /// (write_default_settings) set it (globals.gml's "sly" was always overwritten by read_config; U.RICO-T108). The
        /// index of "baxter" in announcer.json's voices.
        /// </summary>
        public const int DefaultVoice = 4;

        /// <summary>The Resources folder the voice lines are imported into (Tools/import-gml-sound.ps1, U.RICO-T51).</summary>
        public const string VoiceFolder = "Ricochet/Voice";

        /// <summary>How long the card holds a line nobody hears (muted, or the recording is missing): long enough to read it.</summary>
        public const double CaptionSeconds = 4.5;

        private readonly Random _dice;
        private readonly List<int>[] _decks = { new List<int>(), new List<int>(), new List<int>() };

        public Announcer(int voice = DefaultVoice, int frequency = DefaultFrequency, Random dice = null)
        {
            Voice = ClampVoice(voice);
            Frequency = ClampFrequency(frequency);
            _dice = dice ?? new Random();
        }

        /// <summary>Every personality, in the settings screen's order (obj_btn_announcer_next).</summary>
        public static IReadOnlyList<AnnouncerVoice> Voices => Data.Announcer.Voices;

        private static Ricochet.Core.Data.GameData Data => Ricochet.Core.Data.GameData.Current;

        /// <summary>The personality on the air (an index into <see cref="Voices"/>).</summary>
        public int Voice { get; }

        /// <summary>How often it speaks: 0 never, 1 always, 2 to 4 rarer and rarer.</summary>
        public int Frequency { get; }

        public static int ClampVoice(int voice) => Math.Max(0, Math.Min(Voices.Count - 1, voice));

        public static int ClampFrequency(int frequency) => Math.Max(0, Math.Min(MaxFrequency, frequency));

        /// <summary>The personality at <paramref name="voice"/> (clamped into range).</summary>
        public static AnnouncerVoice VoiceAt(int voice) => Voices[ClampVoice(voice)];

        /// <summary>
        /// random_announcement_win/lose's range: a win or lose line is said one time in this many (0 for never). The
        /// welcome is said whenever this is above 0.
        /// </summary>
        public static int Odds(int frequency) => Data.Announcer.Odds[ClampFrequency(frequency)];

        /// <summary>The settings screen's word for a frequency: "Off", "Every time", "Often", "Sometimes", "Rarely".</summary>
        public static string FrequencyName(int frequency) => Data.Announcer.FrequencyNames[ClampFrequency(frequency)];

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
            var lines = Data.Announcer.Lines(announcement.Kind);
            string line = lines[Math.Max(0, Math.Min(lines.Count - 1, announcement.Line))];
            return string.Format(line, NameOf(announcement.Subject, nameOf), NameOf(announcement.Other, nameOf));
        }

        /// <summary>
        /// The recording <paramref name="voice"/> says <paramref name="announcement"/> with: random_&lt;key&gt;_&lt;kind&gt;'s
        /// current_announcement, sfx_&lt;key&gt;_&lt;welcome|win|lose&gt;&lt;line + 1&gt; (sfx_sly_win3 for Kurt Sly's third win line).
        /// </summary>
        public static string Recording(AnnouncerVoice voice, Announcement announcement)
        {
            if (voice == null)
            {
                throw new ArgumentNullException(nameof(voice));
            }

            int line = Math.Max(0, Math.Min(LinesPerKind - 1, announcement.Line)) + 1;
            return "sfx_" + voice.Key + "_" + KindWord(announcement.Kind) + line.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>The Resources path the runtime loads <see cref="Recording"/> from.</summary>
        public static string ResourcePath(AnnouncerVoice voice, Announcement announcement) => VoiceFolder + "/" + Recording(voice, announcement);

        /// <summary>
        /// How long the card stays up for a line whose recording plays for <paramref name="heardSeconds"/> (0 when it is not
        /// heard). obj_announcer held the card while audio_is_playing(current_announcement); a line nobody hears keeps its
        /// written caption up for <see cref="CaptionSeconds"/> instead.
        /// </summary>
        public static double CardSeconds(double heardSeconds)
        {
            return heardSeconds > 0 && !double.IsNaN(heardSeconds) ? heardSeconds : CaptionSeconds;
        }

        private static string KindWord(AnnouncementKind kind)
        {
            switch (kind)
            {
                case AnnouncementKind.Welcome: return "welcome";
                case AnnouncementKind.Win: return "win";
                case AnnouncementKind.Lose: return "lose";
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private static string NameOf(int seat, Func<int, string> nameOf)
        {
            string name = seat >= 0 && nameOf != null ? nameOf(seat) : null;
            return string.IsNullOrEmpty(name) ? "someone" : name;
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
