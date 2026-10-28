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
// index into Maps.All, the maproom's choice). The match rules of the settings room (UR-T25) ride along as well: the turn
// time (turntime, 20 seconds), the bounces a shot makes (bounces, 5), a hit's damage (weapon_damage), each weapon's and item's
// on/off box and starting count (hasX and countX, 3 apiece, the cannon unlimited), whether weapon, item, health and random
// drops fall (weapondrops and the like) and pause_redirector (a redirector the item places stops turning, or keeps turning).
// The Start screen's level settings (U.RICO-T52, the title room's setup panel) ride along with the rules: maxPolygons
// (Shapes, 5 to 40 in fives), shapetype (Type), rotationtype (Rotation: none, 45, 90, 360) and overlap; Level() hands them
// to the match, whose every random round carries them in its seed (LevelSeeds).
// The AI count travels as a vote's number and the rest as its text
// ("skill=5;assist=3;voice=0;announce=1;goal=500;map=0"). The whole setup is remembered between sessions (UR-T41, the
// original's config.ini) as ToRecord's named numbers, read back by FromRecord; ResetToDefaults is its Default button.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Ricochet.Core.Match
{
    using Ricochet.Core.Ai;
    using Ricochet.Core.Aim;
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

        /// <summary>The seconds a turn lasts unless set (turnTime = 20).</summary>
        public const int DefaultTurnSeconds = 20;

        /// <summary>The shortest turn the settings screen sets (the original's turn time: 5 to 30 seconds).</summary>
        public const int MinTurnSeconds = 5;

        /// <summary>The longest turn the settings screen sets.</summary>
        public const int MaxTurnSeconds = 30;

        /// <summary>How far each step of the turn time moves it (obj_btn_turntime_more's 5).</summary>
        public const int TurnSecondsStep = 5;

        /// <summary>The bounces a shot makes unless set (maxBounces = 5).</summary>
        public const int DefaultMaxBounces = 5;

        /// <summary>The most bounces the settings screen sets.</summary>
        public const int MaxBouncesLimit = 20;

        /// <summary>A hit's base damage unless set (the stock match.json's weaponDamage).</summary>
        public const int DefaultWeaponDamage = 10;

        /// <summary>The most base damage the settings screen sets.</summary>
        public const int MaxWeaponDamage = 100;

        /// <summary>The damage choices the original's settings offered (obj_btn_damage_less/more): 5, 10, 25, 33, 50, 100.</summary>
        public static readonly int[] DamageChoices = { 5, 10, 25, 33, 50, 100 };

        private int _aiCount = 1;
        private readonly int[] _skills = NewFilled(Ai.DefaultSkill);
        private readonly int[] _names = NewFilled(AiNames.Default);
        private readonly bool[] _weaponOn = NewOn(ArsenalRules.Kinds);
        private readonly int[] _weaponCount = NewCounts(true);
        private readonly bool[] _itemOn = NewOn(ArsenalRules.Kinds);
        private readonly int[] _itemCount = NewCounts(false);
        private readonly bool[] _dropOn = NewOn(DropRules.KindCount);
        private int _turnSeconds = DefaultTurnSeconds;
        private int _maxBounces = DefaultMaxBounces;
        private int _weaponDamage = DefaultWeaponDamage;
        private bool _pauseRedirector = true;
        private bool _fogOfWar;
        private int _voice = Announcer.DefaultVoice;
        private int _announcements = Announcer.DefaultFrequency;
        private int _goal = DefaultGoal;
        private int _map = Maps.RandomIndex;
        private int _shapeCount = SnapShapes(LevelSettings.Default.PolygonCount);
        private ShapeKind _shapeKind = LevelSettings.Default.Shapes;
        private int _rotation = SnapRotation(LevelSettings.Default.Rotation);
        private bool _overlap = LevelSettings.Default.Overlap;

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
            get { return _itemCount[(int)Item.AimAssist]; }
            set { _itemCount[(int)Item.AimAssist] = ArsenalRules.ClampCount(value); }
        }

        /// <summary>Seconds a player has to fire (turnTime): 5 to 30.</summary>
        public int TurnSeconds
        {
            get { return _turnSeconds; }
            set { _turnSeconds = Math.Max(MinTurnSeconds, Math.Min(MaxTurnSeconds, value)); }
        }

        /// <summary>Contacts a shot makes before it detonates (maxBounces): 1 to 20.</summary>
        public int MaxBounces
        {
            get { return _maxBounces; }
            set { _maxBounces = Math.Max(1, Math.Min(MaxBouncesLimit, value)); }
        }

        /// <summary>A hit's base damage (weapon_damage): 1 to 100 (the settings screen offers <see cref="DamageChoices"/>).</summary>
        public int WeaponDamage
        {
            get { return _weaponDamage; }
            set { _weaponDamage = Math.Max(1, Math.Min(MaxWeaponDamage, value)); }
        }

        /// <summary>fogofwar (UR-T26): the arena is hidden except around each player's own tank (off by default, as in the original).</summary>
        public bool FogOfWar
        {
            get { return _fogOfWar; }
            set { _fogOfWar = value; }
        }

        /// <summary>pause_redirector: a redirector stops turning once placed (true), or keeps turning every turn (false).</summary>
        public bool PauseRedirector
        {
            get { return _pauseRedirector; }
            set { _pauseRedirector = value; }
        }

        /// <summary>The weapon's box (hasX): whether it is in play at all. The cannon is always in play and unlimited, so a player always has a shot.</summary>
        public bool WeaponOn(Weapon weapon) => weapon == Weapon.Cannon || _weaponOn[(int)weapon];

        public void SetWeaponOn(Weapon weapon, bool on) => _weaponOn[(int)weapon] = weapon == Weapon.Cannon || on;

        /// <summary>The count each player starts a round with (countX): 0 to 9, or 10 for unlimited.</summary>
        public int WeaponCount(Weapon weapon) => weapon == Weapon.Cannon ? ArsenalRules.Unlimited : _weaponCount[(int)weapon];

        public void SetWeaponCount(Weapon weapon, int count) => _weaponCount[(int)weapon] = weapon == Weapon.Cannon ? ArsenalRules.Unlimited : ArsenalRules.ClampCount(count);

        /// <summary>The item's box (hasX): whether it is in play at all (the aim assist is also out of play at a count of 0).</summary>
        public bool ItemOn(Item item) => _itemOn[(int)item];

        public void SetItemOn(Item item, bool on) => _itemOn[(int)item] = on;

        /// <summary>The count each player starts a round with (countX).</summary>
        public int ItemCount(Item item) => _itemCount[(int)item];

        public void SetItemCount(Item item, int count) => _itemCount[(int)item] = ArsenalRules.ClampCount(count);

        /// <summary>weapondrops / itemdrops / healthdrops / randomdrops: whether crates of this kind fall.</summary>
        public bool DropOn(DropKind kind) => _dropOn[(int)kind];

        public void SetDropOn(DropKind kind, bool on) => _dropOn[(int)kind] = on;

        private static bool[] NewOn(int length)
        {
            var on = new bool[length];
            for (int i = 0; i < length; i++)
            {
                on[i] = true;
            }

            return on;
        }

        // Every count starts at 3 (countX), but the cannon, which is never spent (the stock arsenal.json's 10).
        private static int[] NewCounts(bool weapons)
        {
            var counts = new int[ArsenalRules.Kinds];
            for (int i = 0; i < counts.Length; i++)
            {
                counts[i] = weapons && i == (int)Weapon.Cannon ? ArsenalRules.Unlimited : DefaultAimAssists;
            }

            return counts;
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

        /// <summary>U.RICO-T52, the Start screen's Shapes (maxPolygons): how many shapes a random level lays, 5 to 40 in fives.</summary>
        public int ShapeCount
        {
            get { return _shapeCount; }
            set { _shapeCount = SnapShapes(value); }
        }

        /// <summary>The Start screen's Type (shapetype): which shape set a random level's walls come from.</summary>
        public ShapeKind ShapeType
        {
            get { return _shapeKind; }
            set { _shapeKind = value; }
        }

        /// <summary>The Start screen's Rotation (rotationtype): 0 (none), 45, 90 or 360 degrees.</summary>
        public int Rotation
        {
            get { return _rotation; }
            set { _rotation = SnapRotation(value); }
        }

        /// <summary>The Start screen's Overlap: whether a random level's shapes may overlap.</summary>
        public bool Overlap
        {
            get { return _overlap; }
            set { _overlap = value; }
        }

        /// <summary>The level settings a random map's rounds are laid with (they travel in each round's seed, LevelSeeds).</summary>
        public LevelSettings Level()
        {
            var level = LevelSettings.Default;
            level.PolygonCount = ShapeCount;
            level.Shapes = ShapeType;
            level.Rotation = Rotation;
            level.Overlap = Overlap;
            return level;
        }

        /// <summary>The nearest of the Start screen's shape counts (<see cref="LevelSeeds.ShapeCounts"/>).</summary>
        public static int SnapShapes(int count) => Nearest(LevelSeeds.ShapeCounts, count);

        /// <summary>The nearest of the Start screen's rotations (<see cref="LevelSeeds.Rotations"/>).</summary>
        public static int SnapRotation(double degrees) => Nearest(LevelSeeds.Rotations, degrees);

        private static int Nearest(int[] values, double value)
        {
            int best = values[0];
            foreach (int candidate in values)
            {
                if (Math.Abs(candidate - value) < Math.Abs(best - value))
                {
                    best = candidate;
                }
            }

            return best;
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

            public const string Turn = "turn";
            public const string Bounces = "bounces";
            public const string Damage = "damage";
            public const string Pause = "pause";
            public const string Fog = "fog";

            /// <summary>Prefixes of one weapon's box and count, one item's box and count, and one drop kind's box, then its number from 0 (the enum's value): <c>wpn2</c>, <c>wpnct2</c>.</summary>
            public const string WeaponOn = "wpn";
            public const string WeaponCount = "wpnct";
            public const string ItemOn = "item";
            public const string ItemCount = "itemct";
            public const string DropOn = "drop";

            /// <summary>U.RICO-T52: the Start screen's level settings (maxPolygons, shapetype, rotationtype, overlap).</summary>
            public const string Shapes = "polys";
            public const string ShapeType = "shapes";
            public const string Rotation = "rot";
            public const string Overlap = "overlap";
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

            record.AddRange(RuleEntries());
            return record;
        }

        // The match rules (UR-T25) as named numbers, in a fixed order; the aim assist's count is the "assist" entry.
        private List<KeyValuePair<string, int>> RuleEntries()
        {
            var entries = new List<KeyValuePair<string, int>>
            {
                new KeyValuePair<string, int>(Keys.Turn, TurnSeconds),
                new KeyValuePair<string, int>(Keys.Bounces, MaxBounces),
                new KeyValuePair<string, int>(Keys.Damage, WeaponDamage),
                new KeyValuePair<string, int>(Keys.Pause, PauseRedirector ? 1 : 0),
                new KeyValuePair<string, int>(Keys.Fog, FogOfWar ? 1 : 0),
            };
            for (int i = 0; i < ArsenalRules.Kinds; i++)
            {
                entries.Add(Entry(Keys.WeaponOn, i, _weaponOn[i] ? 1 : 0));
                entries.Add(Entry(Keys.WeaponCount, i, _weaponCount[i]));
                entries.Add(Entry(Keys.ItemOn, i, _itemOn[i] ? 1 : 0));
                if (i != (int)Item.AimAssist)
                {
                    entries.Add(Entry(Keys.ItemCount, i, _itemCount[i]));
                }
            }

            for (int i = 0; i < DropRules.KindCount; i++)
            {
                entries.Add(Entry(Keys.DropOn, i, _dropOn[i] ? 1 : 0));
            }

            entries.Add(new KeyValuePair<string, int>(Keys.Shapes, ShapeCount));
            entries.Add(new KeyValuePair<string, int>(Keys.ShapeType, (int)ShapeType));
            entries.Add(new KeyValuePair<string, int>(Keys.Rotation, Rotation));
            entries.Add(new KeyValuePair<string, int>(Keys.Overlap, Overlap ? 1 : 0));
            return entries;
        }

        private static KeyValuePair<string, int> Entry(string prefix, int index, int value)
        {
            return new KeyValuePair<string, int>(prefix + index.ToString(CultureInfo.InvariantCulture), value);
        }

        // The index (0 and up, below count) a key like "wpn3" names after its prefix, or -1 when it is not one.
        private static int IndexAfter(string key, string prefix, int count)
        {
            if (key.Length > prefix.Length && key.StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(key.Substring(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int index)
                && index >= 0 && index < count)
            {
                return index;
            }

            return -1;
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
            Array.Copy(other._weaponOn, _weaponOn, _weaponOn.Length);
            Array.Copy(other._weaponCount, _weaponCount, _weaponCount.Length);
            Array.Copy(other._itemOn, _itemOn, _itemOn.Length);
            Array.Copy(other._itemCount, _itemCount, _itemCount.Length);
            Array.Copy(other._dropOn, _dropOn, _dropOn.Length);
            _turnSeconds = other._turnSeconds;
            _maxBounces = other._maxBounces;
            _weaponDamage = other._weaponDamage;
            _pauseRedirector = other._pauseRedirector;
            _fogOfWar = other._fogOfWar;
            _voice = other._voice;
            _announcements = other._announcements;
            _goal = other._goal;
            _map = other._map;
            _shapeCount = other._shapeCount;
            _shapeKind = other._shapeKind;
            _rotation = other._rotation;
            _overlap = other._overlap;
        }

        /// <summary>The Default button (obj_btn_default, backup.ini): every choice back to the game's own defaults.</summary>
        public void ResetToDefaults() => CopyFrom(new MatchSetup());

        /// <summary>True when <paramref name="other"/> holds the same choices.</summary>
        public bool SameAs(MatchSetup other)
        {
            return other != null && _aiCount == other._aiCount && SameItems(_skills, other._skills) && SameItems(_names, other._names)
                && SameItems(_weaponCount, other._weaponCount) && SameItems(_itemCount, other._itemCount) && SameFlags(_weaponOn, other._weaponOn)
                && SameFlags(_itemOn, other._itemOn) && SameFlags(_dropOn, other._dropOn) && _turnSeconds == other._turnSeconds
                && _maxBounces == other._maxBounces && _weaponDamage == other._weaponDamage && _pauseRedirector == other._pauseRedirector && _fogOfWar == other._fogOfWar
                && _voice == other._voice && _announcements == other._announcements && _goal == other._goal && _map == other._map
                && _shapeCount == other._shapeCount && _shapeKind == other._shapeKind && _rotation == other._rotation && _overlap == other._overlap;
        }

        private static readonly List<KeyValuePair<string, int>> DefaultRuleEntries = new MatchSetup().RuleEntries();

        private static bool SameFlags(bool[] a, bool[] b)
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
                case Keys.Turn:
                    TurnSeconds = value;
                    return true;
                case Keys.Bounces:
                    MaxBounces = value;
                    return true;
                case Keys.Damage:
                    WeaponDamage = value;
                    return true;
                case Keys.Pause:
                    PauseRedirector = value != 0;
                    return true;
                case Keys.Fog:
                    FogOfWar = value != 0;
                    return true;
                case Keys.Shapes:
                    ShapeCount = value;
                    return true;
                case Keys.ShapeType:
                    ShapeType = value >= 0 && value <= (int)ShapeKind.Random ? (ShapeKind)value : ShapeKind.Random;
                    return true;
                case Keys.Rotation:
                    Rotation = value;
                    return true;
                case Keys.Overlap:
                    Overlap = value != 0;
                    return true;
                default:
                    int index = IndexAfter(key, Keys.WeaponOn, ArsenalRules.Kinds);
                    if (index >= 0)
                    {
                        SetWeaponOn((Weapon)index, value != 0);
                        return true;
                    }

                    index = IndexAfter(key, Keys.WeaponCount, ArsenalRules.Kinds);
                    if (index >= 0)
                    {
                        SetWeaponCount((Weapon)index, value);
                        return true;
                    }

                    index = IndexAfter(key, Keys.ItemOn, ArsenalRules.Kinds);
                    if (index >= 0)
                    {
                        _itemOn[index] = value != 0;
                        return true;
                    }

                    index = IndexAfter(key, Keys.ItemCount, ArsenalRules.Kinds);
                    if (index >= 0)
                    {
                        _itemCount[index] = ArsenalRules.ClampCount(value);
                        return true;
                    }

                    index = IndexAfter(key, Keys.DropOn, DropRules.KindCount);
                    if (index >= 0)
                    {
                        _dropOn[index] = value != 0;
                        return true;
                    }

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

            for (int i = 0; i < ArsenalRules.Kinds; i++)
            {
                rules.SetStartCount((Weapon)i, WeaponCount((Weapon)i));
                rules.SetInPlay((Weapon)i, WeaponOn((Weapon)i));
                rules.SetStartCount((Item)i, _itemCount[i]);
                rules.SetInPlay((Item)i, _itemOn[i]);
            }

            rules.SetInPlay(Item.AimAssist, _itemOn[(int)Item.AimAssist] && AimAssists > 0);
        }

        /// <summary>
        /// Gives the match, shot, drop and hazard rules this setup's turn time, bounces, damage, drops and redirector
        /// choice (the arsenal's own come through <see cref="ApplyTo(ArsenalRules)"/>).
        /// </summary>
        public void ApplyTo(MatchRules match, ShotRules shot, DropRules drops, HazardRules hazards)
        {
            if (match == null || shot == null || drops == null || hazards == null)
            {
                throw new ArgumentNullException(match == null ? nameof(match) : shot == null ? nameof(shot) : drops == null ? nameof(drops) : nameof(hazards));
            }

            match.TurnSeconds = TurnSeconds;
            match.WeaponDamage = WeaponDamage;
            shot.MaxBounces = MaxBounces;
            for (int i = 0; i < DropRules.KindCount; i++)
            {
                drops.SetEnabled((DropKind)i, _dropOn[i]);
            }

            hazards.RedirectorsSpin = !PauseRedirector;
        }

        /// <summary>How many ints <see cref="PackRules"/> returns.</summary>
        public const int PackedRuleWords = 4;

        /// <summary>Set in the first packed word, so an all-zero (never written) pack is told from a real one.</summary>
        private const int PackedValid = 1 << 30;

        /// <summary>
        /// The match rules (turn time, bounces, damage, redirector pause, drops, each weapon's and item's box and starting
        /// count) packed into <see cref="PackedRuleWords"/> ints, for the wire (UR-T76): the kit syncs whole numbers, and
        /// the host's rules must reach every client so their previews and clocks agree. <see cref="UnpackRules"/> reads it back.
        /// </summary>
        public int[] PackRules()
        {
            int turn = TurnSeconds | MaxBounces << 6 | WeaponDamage << 11 | (PauseRedirector ? 1 : 0) << 18 | (FogOfWar ? 1 : 0) << 23;
            for (int i = 0; i < DropRules.KindCount; i++)
            {
                turn |= (_dropOn[i] ? 1 : 0) << (19 + i);
            }

            int boxes = 0, weapons = 0, items = 0;
            for (int i = 0; i < ArsenalRules.Kinds; i++)
            {
                boxes |= (WeaponOn((Weapon)i) ? 1 : 0) << i | (_itemOn[i] ? 1 : 0) << (6 + i);
                weapons |= WeaponCount((Weapon)i) << (4 * i);
                items |= _itemCount[i] << (4 * i);
            }

            return new[] { turn | PackedValid, boxes, weapons, items };
        }

        /// <summary>True when <paramref name="packed"/> came from <see cref="PackRules"/> (not an unwritten, all-zero pack).</summary>
        public static bool IsPackedRules(int[] packed) => packed != null && packed.Length == PackedRuleWords && (packed[0] & PackedValid) != 0;

        /// <summary>A setup holding the rules <see cref="PackRules"/> packed (everything else default); each value goes through its clamp.</summary>
        public static MatchSetup UnpackRules(int[] packed)
        {
            var setup = new MatchSetup();
            if (!IsPackedRules(packed))
            {
                return setup;
            }

            setup.TurnSeconds = packed[0] & 63;
            setup.MaxBounces = packed[0] >> 6 & 31;
            setup.WeaponDamage = packed[0] >> 11 & 127;
            setup.PauseRedirector = (packed[0] >> 18 & 1) != 0;
            setup.FogOfWar = (packed[0] >> 23 & 1) != 0;
            for (int i = 0; i < DropRules.KindCount; i++)
            {
                setup._dropOn[i] = (packed[0] >> (19 + i) & 1) != 0;
            }

            for (int i = 0; i < ArsenalRules.Kinds; i++)
            {
                setup.SetWeaponOn((Weapon)i, (packed[1] >> i & 1) != 0);
                setup._itemOn[i] = (packed[1] >> (6 + i) & 1) != 0;
                setup.SetWeaponCount((Weapon)i, packed[2] >> (4 * i) & 15);
                setup._itemCount[i] = ArsenalRules.ClampCount(packed[3] >> (4 * i) & 15);
            }

            return setup;
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

            // The rules (UR-T25) only where they differ from the defaults: the text stays short.
            var rules = new System.Text.StringBuilder();
            List<KeyValuePair<string, int>> mine = RuleEntries();
            List<KeyValuePair<string, int>> stock = DefaultRuleEntries;
            for (int i = 0; i < mine.Count; i++)
            {
                if (mine[i].Value != stock[i].Value)
                {
                    rules.Append(';').Append(mine[i].Key).Append('=').Append(mine[i].Value.ToString(CultureInfo.InvariantCulture));
                }
            }

            return "skill=" + AiSkill.ToString(CultureInfo.InvariantCulture) + ";assist=" + AimAssists.ToString(CultureInfo.InvariantCulture)
                + ";voice=" + AnnouncerVoice.ToString(CultureInfo.InvariantCulture) + ";announce=" + Announcements.ToString(CultureInfo.InvariantCulture)
                + ";goal=" + Goal.ToString(CultureInfo.InvariantCulture) + ";map=" + Map.ToString(CultureInfo.InvariantCulture) + seats + rules;
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
