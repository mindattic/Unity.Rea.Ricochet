// What players choose before a match (UR-T7): how many AI opponents fill the empty seats, how well they aim, and how
// many aim assists everyone starts with. The 2013 original set these on its setup screens (design reference only,
// UR-LAW-2): obj_btn_ai_less/more stepped aiCount, the skill arrows each AI player's pSkill (0 to 10), and the
// aim-assist checkbox and arrows hasAimAssist and count_AimAssist (0 to 10, 10 meaning unlimited; 3 by default).
// Each AI seat has its own skill and name (UR-T28: obj_btn_skillup/down_playerN and the names room's default and random
// names; a name is an index into AiNames, -1 for "AI n"), and an aim-assist level of 0 takes the item out of play (the unchecked box).
// The announcer (UR-T8) rides along: the personality on the air (obj_btn_announcer_last/next) and how often it speaks
// (the "announcements" setting, obj_btn_announcer_less/more: 0 off to 4 rarely). The AI count travels as a vote's number
// The game's goal and map (UR-T9) ride along too: the points that win the game (the setup screen's obj_btn_goal_less/more,
// 1 meaning a single round, then 500 to 10000 in steps of 500; 500 by default) and the map the match is played on (an
// index into Maps.All, the maproom's choice). The AI count travels as a vote's number and the rest as its text
// ("skill=5;assist=3;voice=0;announce=1;goal=500;map=0"). The whole setup is remembered between sessions (UR-T41, the
// original's config.ini) as ToRecord's named numbers, read back by FromRecord; ResetToDefaults is its Default button.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Ricochet.Core.Match
{
    using Ricochet.Core.Ai;
    using Ricochet.Core.Arsenal;
    using Ricochet.Core.Level;
    public sealed class MatchSetup
    {
        /// <summary>Aim assists each player starts with unless set (count_AimAssist = 3).</summary>
        public const int DefaultAimAssists = 3;

        /// <summary>The goal that ends the game after its first round, whatever the scores (the original's goal of 1).</summary>
        public const int OneRound = 1;

        /// <summary>The goal a game starts with (globals.gml's goal = 500).</summary>
        public const int DefaultGoal = 500;

        /// <summary>How far each step of the goal moves it (obj_btn_goal_less/more's 500).</summary>
        public const int GoalStep = 500;

        /// <summary>The highest goal (10000).</summary>
        public const int MaxGoal = 10000;

        private int _aiCount = 1;
        private readonly int[] _skills = NewFilled(Ai.DefaultSkill);
        private readonly int[] _names = NewFilled(AiNames.Default);
        private int _aimAssists = DefaultAimAssists;
        private int _voice = Announcer.DefaultVoice;
        private int _announcements = Announcer.DefaultFrequency;
        private int _goal = DefaultGoal;
        private int _map = Maps.RandomIndex;

        /// <summary>AI opponents to add (0 to 15; the server seats fewer when the table is full).</summary>
        public int AiCount
        {
            get { return _aiCount; }
            set { _aiCount = Math.Max(0, Math.Min(MatchRules.MaxSeats - 1, value)); }
        }

        /// <summary>
        /// The first AI seat's skill on reading, and every AI seat's on setting (the single skill the setup had before
        /// UR-T28; seats are set apart with <see cref="SetSkill"/>): 0 (aim nudged up to 10 degrees) to 10 (exact).
        /// </summary>
        public int AiSkill
        {
            get { return _skills[0]; }
            set
            {
                int skill = Ai.ClampSkill(value);
                for (int i = 0; i < _skills.Length; i++)
                {
                    _skills[i] = skill;
                }
            }
        }

        /// <summary>The skill of AI seat <paramref name="ai"/> (0 is the first AI), 0 to 10.</summary>
        public int SkillOf(int ai) => _skills[CheckAi(ai)];

        /// <summary>Sets the skill of AI seat <paramref name="ai"/> (clamped to 0 to 10).</summary>
        public void SetSkill(int ai, int skill) => _skills[CheckAi(ai)] = Ai.ClampSkill(skill);

        /// <summary>The name index of AI seat <paramref name="ai"/> (an index into <see cref="AiNames"/>, or <see cref="AiNames.Default"/>).</summary>
        public int NameIndexOf(int ai) => _names[CheckAi(ai)];

        /// <summary>Gives AI seat <paramref name="ai"/> the name at <paramref name="nameIndex"/> (anything the list lacks means "AI n").</summary>
        public void SetNameIndex(int ai, int nameIndex) => _names[CheckAi(ai)] = AiNames.Clamp(nameIndex);

        /// <summary>The name AI seat <paramref name="ai"/> goes by: its picked name, or "AI n".</summary>
        public string NameOf(int ai) => AiNames.NameFor(CheckAi(ai) + 1, _names[ai]);

        /// <summary>The names room's default names button: every AI back to "AI n".</summary>
        public void UseDefaultNames()
        {
            for (int i = 0; i < _names.Length; i++)
            {
                _names[i] = AiNames.Default;
            }
        }

        /// <summary>The names room's random names button: every AI gets a different name from the list.</summary>
        public void UseRandomNames(Random random)
        {
            int[] picks = AiNames.RandomIndices(_names.Length, random);
            for (int i = 0; i < _names.Length; i++)
            {
                _names[i] = picks[i];
            }
        }

        private static int CheckAi(int ai)
        {
            if (ai < 0 || ai >= MatchRules.MaxSeats - 1)
            {
                throw new ArgumentOutOfRangeException(nameof(ai));
            }

            return ai;
        }

        private static int[] NewFilled(int value)
        {
            var items = new int[MatchRules.MaxSeats - 1];
            for (int i = 0; i < items.Length; i++)
            {
                items[i] = value;
            }

            return items;
        }

        /// <summary>Aim assists each player starts a round with: 0 (none, and none drop), 1 to 9, or 10 (unlimited).</summary>
        public int AimAssists
        {
            get { return _aimAssists; }
            set { _aimAssists = ArsenalRules.ClampCount(value); }
        }

        /// <summary>The announcer on the air (an index into Announcer.Voices).</summary>
        public int AnnouncerVoice
        {
            get { return _voice; }
            set { _voice = Announcer.ClampVoice(value); }
        }

        /// <summary>How often the announcer speaks: 0 never, 1 every time, 2 to 4 rarer and rarer.</summary>
        public int Announcements
        {
            get { return _announcements; }
            set { _announcements = Announcer.ClampFrequency(value); }
        }

        /// <summary>The points that win the game: <see cref="OneRound"/>, or 500 to 10000 in steps of 500.</summary>
        public int Goal
        {
            get { return _goal; }
            set { _goal = ClampGoal(value); }
        }

        /// <summary>The map the match is played on (an index into <see cref="Maps.All"/>).</summary>
        public int Map
        {
            get { return _map; }
            set { _map = Maps.Clamp(value); }
        }

        /// <summary>
        /// A goal the setup screen can show: anything below a step is <see cref="OneRound"/>, anything else rounds to the
        /// nearest step, at most <see cref="MaxGoal"/>.
        /// </summary>
        public static int ClampGoal(int goal)
        {
            if (goal < GoalStep / 2)
            {
                return OneRound;
            }

            int steps = (int)Math.Round(Math.Min(goal, MaxGoal) / (double)GoalStep, MidpointRounding.AwayFromZero);
            return Math.Max(1, steps) * GoalStep;
        }

        /// <summary>obj_btn_goal_more: one step up (one round, 500, 1000 ... 10000, then back round to one round).</summary>
        public static int NextGoal(int goal)
        {
            goal = ClampGoal(goal);
            return goal == OneRound ? GoalStep : goal >= MaxGoal ? OneRound : goal + GoalStep;
        }

        /// <summary>obj_btn_goal_less: one step down (wrapping from one round to 10000).</summary>
        public static int PreviousGoal(int goal)
        {
            goal = ClampGoal(goal);
            return goal == OneRound ? MaxGoal : goal == GoalStep ? OneRound : goal - GoalStep;
        }

        /// <summary>"First to 500 points", or "One round".</summary>
        public static string DescribeGoal(int goal)
        {
            goal = ClampGoal(goal);
            return goal == OneRound ? "One round" : "First to " + goal.ToString(CultureInfo.InvariantCulture) + " points";
        }

        public MatchSetup Clone()
        {
            var copy = new MatchSetup();
            copy.CopyFrom(this);
            return copy;
        }

        /// <summary>
        /// The names a remembered setup is stored under (UR-T41, the original's config.ini): <c>ai</c> for the AI count, then
        /// the vote text's own names (<see cref="Format"/>).
        /// </summary>
        public static class Keys
        {
            public const string Ai = "ai";
            public const string Skill = "skill";
            public const string Assist = "assist";
            public const string Voice = "voice";
            public const string Announce = "announce";
            public const string Goal = "goal";
            public const string Map = "map";

            /// <summary>The prefix of one AI's skill, then its number from 1: <c>skill3</c> (UR-T28; plain <c>skill</c> is every AI's).</summary>
            public const string SkillOf = "skill";

            /// <summary>The prefix of one AI's name index, then its number from 1: <c>name3</c> (UR-T28).</summary>
            public const string NameOf = "name";
        }

        /// <summary>
        /// Everything this setup holds, as named whole numbers in a fixed order (UR-T41): what the profile stores so the
        /// next session starts where this one left off, as the original's write_config/write_settings did to config.ini.
        /// </summary>
        public IReadOnlyList<KeyValuePair<string, int>> ToRecord()
        {
            // "skill" (every AI's, all an older save holds) comes first so the per-AI entries after it win.
            var record = new List<KeyValuePair<string, int>>
            {
                new KeyValuePair<string, int>(Keys.Ai, AiCount),
                new KeyValuePair<string, int>(Keys.Skill, AiSkill),
                new KeyValuePair<string, int>(Keys.Assist, AimAssists),
                new KeyValuePair<string, int>(Keys.Voice, AnnouncerVoice),
                new KeyValuePair<string, int>(Keys.Announce, Announcements),
                new KeyValuePair<string, int>(Keys.Goal, Goal),
                new KeyValuePair<string, int>(Keys.Map, Map),
            };
            for (int i = 0; i < _skills.Length; i++)
            {
                record.Add(new KeyValuePair<string, int>(Keys.SkillOf + (i + 1).ToString(CultureInfo.InvariantCulture), _skills[i]));
                record.Add(new KeyValuePair<string, int>(Keys.NameOf + (i + 1).ToString(CultureInfo.InvariantCulture), _names[i]));
            }

            return record;
        }

        /// <summary>
        /// A setup read back from <see cref="ToRecord"/>'s values (the original's read_config): each value goes through
        /// its property's clamp, and anything missing or unknown keeps its default.
        /// </summary>
        public static MatchSetup FromRecord(IEnumerable<KeyValuePair<string, int>> record)
        {
            var setup = new MatchSetup();
            if (record == null)
            {
                return setup;
            }

            // An older save holds one "skill" for every AI: it goes in first, so any per-AI skill beside it wins.
            var rest = new List<KeyValuePair<string, int>>();
            foreach (KeyValuePair<string, int> entry in record)
            {
                if (entry.Key == Keys.Skill)
                {
                    setup.Set(entry.Key, entry.Value);
                }
                else
                {
                    rest.Add(entry);
                }
            }

            foreach (KeyValuePair<string, int> entry in rest)
            {
                setup.Set(entry.Key, entry.Value);
            }

            return setup;
        }

        /// <summary>Takes every choice from <paramref name="other"/> (the screens hold this instance, so it changes in place).</summary>
        public void CopyFrom(MatchSetup other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            _aiCount = other._aiCount;
            Array.Copy(other._skills, _skills, _skills.Length);
            Array.Copy(other._names, _names, _names.Length);
            _aimAssists = other._aimAssists;
            _voice = other._voice;
            _announcements = other._announcements;
            _goal = other._goal;
            _map = other._map;
        }

        /// <summary>The Default button (obj_btn_default, backup.ini): every choice back to the game's own defaults.</summary>
        public void ResetToDefaults() => CopyFrom(new MatchSetup());

        /// <summary>True when <paramref name="other"/> holds the same choices.</summary>
        public bool SameAs(MatchSetup other)
        {
            return other != null && _aiCount == other._aiCount && SameItems(_skills, other._skills) && SameItems(_names, other._names) && _aimAssists == other._aimAssists
                && _voice == other._voice && _announcements == other._announcements && _goal == other._goal && _map == other._map;
        }

        private static bool SameItems(int[] a, int[] b)
        {
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }

        // The AI number (1 and up) a key like "skill3" or "name12" names, or 0 when it is neither.
        private static int NumberAfter(string key, string prefix)
        {
            if (key.Length > prefix.Length && key.StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(key.Substring(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int number)
                && number >= 1 && number <= MatchRules.MaxSeats - 1)
            {
                return number;
            }

            return 0;
        }

        // One named value onto this setup (through its property's clamp); false for a name it does not know.
        private bool Set(string key, int value)
        {
            switch (key)
            {
                case Keys.Ai:
                    AiCount = value;
                    return true;
                case Keys.Skill:
                    AiSkill = value;
                    return true;
                case Keys.Assist:
                    AimAssists = value;
                    return true;
                case Keys.Voice:
                    AnnouncerVoice = value;
                    return true;
                case Keys.Announce:
                    Announcements = value;
                    return true;
                case Keys.Goal:
                    Goal = value;
                    return true;
                case Keys.Map:
                    Map = value;
                    return true;
                default:
                    int skill = NumberAfter(key, Keys.SkillOf);
                    if (skill > 0)
                    {
                        SetSkill(skill - 1, value);
                        return true;
                    }

                    int name = NumberAfter(key, Keys.NameOf);
                    if (name > 0)
                    {
                        SetNameIndex(name - 1, value);
                        return true;
                    }

                    return false;
            }
        }

        /// <summary>Gives <paramref name="rules"/> this setup's aim assists: the starting count, and in play only when above 0.</summary>
        public void ApplyTo(ArsenalRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            rules.SetStartCount(Item.AimAssist, AimAssists);
            rules.SetInPlay(Item.AimAssist, AimAssists > 0);
        }

        /// <summary>The vote's text payload: everything but the AI count.</summary>
        public string Format()
        {
            var seats = new System.Text.StringBuilder();
            for (int i = 0; i < AiCount; i++) // only the seats in play: the text stays short
            {
                seats.Append(";skill").Append(i + 1).Append('=').Append(_skills[i].ToString(CultureInfo.InvariantCulture));
                if (_names[i] != AiNames.Default)
                {
                    seats.Append(";name").Append(i + 1).Append('=').Append(_names[i].ToString(CultureInfo.InvariantCulture));
                }
            }

            return "skill=" + AiSkill.ToString(CultureInfo.InvariantCulture) + ";assist=" + AimAssists.ToString(CultureInfo.InvariantCulture)
                + ";voice=" + AnnouncerVoice.ToString(CultureInfo.InvariantCulture) + ";announce=" + Announcements.ToString(CultureInfo.InvariantCulture)
                + ";goal=" + Goal.ToString(CultureInfo.InvariantCulture) + ";map=" + Map.ToString(CultureInfo.InvariantCulture) + seats;
        }

        /// <summary>
        /// A setup read back from a vote's number (<paramref name="aiCount"/>) and text (<see cref="Format"/>); anything
        /// missing or unreadable keeps its default.
        /// </summary>
        public static MatchSetup Parse(long aiCount, string text)
        {
            var setup = new MatchSetup { AiCount = (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, aiCount)) };
            if (string.IsNullOrEmpty(text))
            {
                return setup;
            }

            foreach (string part in text.Split(';'))
            {
                int equals = part.IndexOf('=');
                if (equals <= 0 || !int.TryParse(part.Substring(equals + 1).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                {
                    continue;
                }

                string key = part.Substring(0, equals).Trim();
                if (key != Keys.Ai) // the vote's number carries the AI count, never its text
                {
                    setup.Set(key, value);
                }
            }

            return setup;
        }

        /// <summary>"Kurt Sly on the air, every time", or "No announcer".</summary>
        public string DescribeAnnouncer()
        {
            return Announcements == 0 ? "No announcer"
                : Announcer.VoiceAt(AnnouncerVoice).Name + " on the air, " + Announcer.FrequencyName(Announcements).ToLowerInvariant();
        }

        /// <summary>"Crossfire, first to 500 points": the map and the goal.</summary>
        public string DescribeGame()
        {
            return Maps.At(Map).Name + ", " + DescribeGoal(Goal).ToLowerInvariant();
        }

        /// <summary>"AI skill 5 of 10, 3 aim assists each" (the AI part only when there are AI opponents).</summary>
        public string Describe()
        {
            string assists = AimAssists == 0 ? "no aim assist"
                : AimAssists >= ArsenalRules.Unlimited ? "unlimited aim assist"
                : AimAssists + " aim assist" + (AimAssists == 1 ? string.Empty : "s") + " each";
            return AiCount > 0 ? DescribeSkills() + ", " + assists : char.ToUpperInvariant(assists[0]) + assists.Substring(1);
        }

        // "AI skill 5 of 10", or "AI skills 3 to 8 of 10" when the seats in play differ.
        private string DescribeSkills()
        {
            int low = int.MaxValue;
            int high = int.MinValue;
            for (int i = 0; i < AiCount; i++)
            {
                low = Math.Min(low, _skills[i]);
                high = Math.Max(high, _skills[i]);
            }

            return low == high ? "AI skill " + low + " of " + Ai.MaxSkill : "AI skills " + low + " to " + high + " of " + Ai.MaxSkill;
        }
    }
}
