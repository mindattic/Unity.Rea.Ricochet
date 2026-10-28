using System;
using System.Collections.Generic;

namespace Ricochet.Core.Data
{
    using Ricochet.Core.Level;
    using Ricochet.Core.Match;
    using Ricochet.Core.Menu;

    /// <summary>The announcer's personalities, how often each frequency setting speaks, and the lines (announcer.json).</summary>
    public sealed class AnnouncerData
    {
        internal AnnouncerData(IReadOnlyList<AnnouncerVoice> voices, string[] frequencyNames, int[] odds, string[] welcome, string[] win, string[] lose)
        {
            Voices = voices;
            FrequencyNames = frequencyNames;
            Odds = odds;
            Welcome = welcome;
            Win = win;
            Lose = lose;
        }

        public IReadOnlyList<AnnouncerVoice> Voices { get; }

        /// <summary>The Settings screen's word for each frequency setting, 0 upwards.</summary>
        public IReadOnlyList<string> FrequencyNames { get; }

        /// <summary>A win or lose line is said one time in this many for each frequency (0 never).</summary>
        public IReadOnlyList<int> Odds { get; }

        /// <summary>{0} is the subject's name, {1} the other's.</summary>
        public IReadOnlyList<string> Welcome { get; }

        public IReadOnlyList<string> Win { get; }

        public IReadOnlyList<string> Lose { get; }

        public IReadOnlyList<string> Lines(AnnouncementKind kind)
        {
            switch (kind)
            {
                case AnnouncementKind.Win: return Win;
                case AnnouncementKind.Lose: return Lose;
                default: return Welcome;
            }
        }
    }

    /// <summary>The menu flow's text: tagline, room titles, controls, how-to-play slides and credits (menu.json).</summary>
    public sealed class MenuData
    {
        internal MenuData(string tagline, Dictionary<Room, string> titles, Room[] offTheMainMenu, ControlBinding[] controls, Slide[] slides, string[] credits,
            Dictionary<string, string> tips = null, double fadeSeconds = 0)
        {
            FadeSeconds = fadeSeconds;
            Tagline = tagline;
            Titles = titles;
            OffTheMainMenu = offTheMainMenu;
            Controls = controls;
            Slides = slides;
            Credits = credits;
            Tips = tips ?? new Dictionary<string, string>();
        }

        /// <summary>U.RICO-T52: the Start screen's hover tips by name (MenuFlow.TipNames).</summary>
        public IReadOnlyDictionary<string, string> Tips { get; }

        /// <summary>U.RICO-T53: how long a room fade takes, in seconds (0: the original's, RoomFade.DefaultSeconds).</summary>
        public double FadeSeconds { get; }

        public string Tagline { get; }

        public IReadOnlyDictionary<Room, string> Titles { get; }

        public IReadOnlyList<Room> OffTheMainMenu { get; }

        public IReadOnlyList<ControlBinding> Controls { get; }

        public IReadOnlyList<Slide> Slides { get; }

        public IReadOnlyList<string> Credits { get; }
    }

    /// <summary>The original's 80 level-shape outlines, by kind (shapes.json).</summary>
    public sealed class ShapeTable
    {
        private readonly int[][][] _kinds;

        internal ShapeTable(int[][][] kinds)
        {
            _kinds = kinds;
        }

        /// <summary>The kind's sixteen outlines as flat x,y pixel pairs.</summary>
        public int[][] Table(ShapeKind kind)
        {
            int index = (int)kind;
            if (index < 0 || index >= _kinds.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(kind), "Only the five concrete shape kinds have outlines.");
            }

            return _kinds[index];
        }
    }

    /// <summary>What kind of control the Settings screen shows for a setting.</summary>
    public enum SettingKind
    {
        Integer,
        Number,
        Flag,
        Choice,
    }

    /// <summary>
    /// One entry of the Settings screen's registry (registry.json): a tunable, the data file and key it lives in, and how
    /// to edit it. The screen builds its controls from these rather than from code per setting.
    /// </summary>
    public sealed class SettingDef
    {
        internal SettingDef(string id, string label, string group, string file, string key, SettingKind kind, double min, double max, double step, string[] choices, double number, bool flag, string choice)
        {
            Id = id;
            Label = label;
            Group = group;
            File = file;
            Key = key;
            Kind = kind;
            Min = min;
            Max = max;
            Step = step;
            Choices = choices;
            Number = number;
            Flag = flag;
            Choice = choice;
        }

        /// <summary>Unique: "match.turnSeconds".</summary>
        public string Id { get; }

        public string Label { get; }

        /// <summary>The section of the screen it sits in.</summary>
        public string Group { get; }

        /// <summary>The data file and top-level key the stock value is read from.</summary>
        public string File { get; }

        public string Key { get; }

        public SettingKind Kind { get; }

        /// <summary>The range and step of an Integer or Number setting (0 otherwise).</summary>
        public double Min { get; }

        public double Max { get; }

        public double Step { get; }

        /// <summary>The names a Choice setting offers.</summary>
        public IReadOnlyList<string> Choices { get; }

        /// <summary>The stock value of an Integer or Number setting.</summary>
        public double Number { get; }

        /// <summary>The stock value of a Flag setting.</summary>
        public bool Flag { get; }

        /// <summary>The stock value of a Choice setting.</summary>
        public string Choice { get; }
    }
}
