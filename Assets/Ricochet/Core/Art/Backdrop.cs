// The level's backdrop (U.RICO-T46, docs/BIBLE.md "Level backdrop"): the rules behind what the original drew under and
// over a round, kept engine-free so every peer derives the same picture from what it already shares. setup_backgrounds.gml
// rolls one floor from each of its three groups and chooses among the three; obj_lights rolls its coloured lights;
// obj_cloud_parent drifts each cloud a pixel a step to the left and sends it back in at the right; obj_spotlight's alpha
// swings between 0.05 and 0.15. The numbers and names are backdrop.json's (BackdropRules); the views only draw them.
using System;
using System.Collections.Generic;

namespace Ricochet.Core.Art
{
    /// <summary>One of the room's clouds as gamescreen placed it: its sprite and its top-left corner in room pixels.</summary>
    public readonly struct CloudSpot
    {
        public CloudSpot(string sprite, double x, double y)
        {
            Sprite = sprite;
            X = x;
            Y = y;
        }

        public string Sprite { get; }

        public double X { get; }

        public double Y { get; }
    }

    /// <summary>One of obj_lights' coloured lights: its centre and radius in room pixels and its colour.</summary>
    public readonly struct BackdropLight
    {
        public BackdropLight(int x, int y, int radius, byte r, byte g, byte b)
        {
            X = x;
            Y = y;
            Radius = radius;
            R = r;
            G = g;
            B = b;
        }

        public int X { get; }

        public int Y { get; }

        public int Radius { get; }

        public byte R { get; }

        public byte G { get; }

        public byte B { get; }
    }

    /// <summary>backdrop.json: the floors, clouds, spotlight, lights and border sprites, with the original's numbers.</summary>
    public sealed class BackdropRules
    {
        /// <summary>The stock rules (backdrop.json).</summary>
        public static BackdropRules Default => Ricochet.Core.Data.GameData.Current.Backdrop;

        /// <summary>The original's room in pixels (1920 x 1080): what the room-pixel numbers below are measured in.</summary>
        public int RoomWidth { get; internal set; }

        public int RoomHeight { get; internal set; }

        /// <summary>setup_backgrounds' bg_group1 (outdoor), bg_group2 (tiles and patterns) and bg_group3 (skins and stucco).</summary>
        public IReadOnlyList<IReadOnlyList<string>> Groups { get; internal set; }

        public IReadOnlyList<CloudSpot> Clouds { get; internal set; }

        /// <summary>obj_cloud_parent's image_alpha.</summary>
        public double CloudAlpha { get; internal set; }

        /// <summary>Room pixels a cloud moves left each step (hspeed = -1).</summary>
        public double CloudSpeed { get; internal set; }

        /// <summary>The range a cloud's new y is rolled from when it comes back in: irandom_range(top, room_height - bottomGap).</summary>
        public int CloudWrapTop { get; internal set; }

        public int CloudWrapBottomGap { get; internal set; }

        public string SpotlightSprite { get; internal set; }

        /// <summary>Where the spotlight stands, in room pixels (its sprite's origin).</summary>
        public double SpotlightX { get; internal set; }

        public double SpotlightY { get; internal set; }

        public double SpotlightMinAlpha { get; internal set; }

        public double SpotlightMaxAlpha { get; internal set; }

        /// <summary>Alpha gained or lost each step.</summary>
        public double SpotlightRate { get; internal set; }

        /// <summary>obj_lights' lightCount.</summary>
        public int LightCount { get; internal set; }

        public int LightMinRadius { get; internal set; }

        public int LightMaxRadius { get; internal set; }

        /// <summary>Each of a light's red, green and blue is irandom_range(min, max).</summary>
        public int LightMinChannel { get; internal set; }

        public int LightMaxChannel { get; internal set; }

        public string TopBorder { get; internal set; }

        public string BottomBorder { get; internal set; }

        public string LeftBorder { get; internal set; }

        public string RightBorder { get; internal set; }

        /// <summary>draw_borders' `border`: how far the side frames reach in (the top and bottom frames are twice it).</summary>
        public int Border { get; internal set; }
    }

    /// <summary>One round's backdrop: the floor setup_backgrounds chose and obj_lights' lights.</summary>
    public sealed class BackdropPlan
    {
        internal BackdropPlan(int group, string background, IReadOnlyList<BackdropLight> lights)
        {
            Group = group;
            Background = background;
            Lights = lights;
        }

        /// <summary>Which group the floor came from (0 to 2: bg_group1 to bg_group3).</summary>
        public int Group { get; }

        /// <summary>The floor's GML background name ("bg_dirt").</summary>
        public string Background { get; }

        public IReadOnlyList<BackdropLight> Lights { get; }
    }

    public static class Backdrop
    {
        /// <summary>
        /// The seed a round's backdrop is rolled from: the level's seed and version, which every peer already has (Arena's
        /// LevelSeed and LevelVersion), so the floor and lights agree everywhere without anything new on the wire. The
        /// version changes every round, so a hand-laid map (seed 0) still gets a new floor each round, as randomize_level did.
        /// </summary>
        public static int SeedFor(int levelSeed, int levelVersion)
        {
            unchecked
            {
                return (levelSeed * 486187739) ^ (levelVersion * 16777619) ^ 0x5bd1e995;
            }
        }

        /// <summary>The backdrop for <paramref name="seed"/>: the same seed always gives the same plan.</summary>
        public static BackdropPlan Plan(int seed, BackdropRules rules = null)
        {
            rules = rules ?? BackdropRules.Default;
            var dice = new Random(seed);

            // setup_backgrounds: every group rolls its member first (choose() evaluates all three), then one group is chosen.
            var rolled = new string[rules.Groups.Count];
            for (int g = 0; g < rolled.Length; g++)
            {
                IReadOnlyList<string> group = rules.Groups[g];
                rolled[g] = group[dice.Next(group.Count)];
            }

            int chosen = dice.Next(rolled.Length);

            // obj_lights' Create: x, y, size, then red, green and blue, for each light in turn.
            var lights = new BackdropLight[rules.LightCount];
            for (int i = 0; i < lights.Length; i++)
            {
                int x = Between(dice, 0, rules.RoomWidth);
                int y = Between(dice, 0, rules.RoomHeight);
                int radius = Between(dice, rules.LightMinRadius, rules.LightMaxRadius);
                byte r = (byte)Between(dice, rules.LightMinChannel, rules.LightMaxChannel);
                byte g = (byte)Between(dice, rules.LightMinChannel, rules.LightMaxChannel);
                byte b = (byte)Between(dice, rules.LightMinChannel, rules.LightMaxChannel);
                lights[i] = new BackdropLight(x, y, radius, r, g, b);
            }

            return new BackdropPlan(chosen, rolled[chosen], lights);
        }

        /// <summary>
        /// obj_cloud_parent's Step for one cloud <paramref name="width"/> room pixels wide: <paramref name="steps"/> steps
        /// of drifting left, and once it is wholly off the left edge it comes back in at the right edge at a new height rolled
        /// with <paramref name="dice"/>. Returns true when it wrapped.
        /// </summary>
        public static bool DriftCloud(ref double x, ref double y, double width, double steps, BackdropRules rules, Random dice)
        {
            x -= rules.CloudSpeed * steps;
            if (x >= -width)
            {
                return false;
            }

            x = rules.RoomWidth;
            y = Between(dice, rules.CloudWrapTop, rules.RoomHeight - rules.CloudWrapBottomGap);
            return true;
        }

        // GameMaker's irandom_range: a whole number from low to high, both included.
        private static int Between(Random dice, int low, int high) => high <= low ? low : dice.Next(low, high + 1);
    }

    /// <summary>obj_spotlight: its alpha falls to the minimum, then climbs to the maximum, then falls again, a rate a step.</summary>
    public sealed class SpotlightPulse
    {
        private readonly double _min, _max, _rate;
        private bool _fading = true;

        public SpotlightPulse(double min, double max, double rate)
        {
            _min = min;
            _max = max;
            _rate = rate;
            Alpha = min; // Create: image_alpha = minalpha, fadeout = true
        }

        public SpotlightPulse(BackdropRules rules) : this(rules.SpotlightMinAlpha, rules.SpotlightMaxAlpha, rules.SpotlightRate)
        {
        }

        public double Alpha { get; private set; }

        /// <summary>True while the alpha is falling.</summary>
        public bool Fading => _fading;

        /// <summary>One step of obj_spotlight's Step: move by the rate, or turn round at an end (a step spent turning).</summary>
        public void Step()
        {
            if (_fading)
            {
                if (Alpha > _min)
                {
                    Alpha -= _rate;
                }
                else
                {
                    _fading = false;
                }
            }
            else
            {
                if (Alpha < _max)
                {
                    Alpha += _rate;
                }
                else
                {
                    _fading = true;
                }
            }
        }
    }
}
