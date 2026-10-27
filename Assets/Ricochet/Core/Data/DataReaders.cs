using System;
using System.Collections.Generic;
using System.Globalization;

namespace Ricochet.Core.Data
{
    using Ricochet.Core.Aim;
    using Ricochet.Core.Arsenal;
    using Ricochet.Core.Level;
    using Ricochet.Core.Match;
    using Ricochet.Core.Menu;

    /// <summary>Reads and validates each data file into the objects Core plays with. Every problem is added to the load's error list.</summary>
    internal static class DataReaders
    {
        // The arena the maps and hazards live in is fixed (Seating's spots, the camera): coordinates outside it are mistakes.
        private const double Reach = 50;

        public static MatchRules ReadMatch(SettingsReader r)
        {
            var rules = new MatchRules(true)
            {
                MaxHealth = r.Integer("maxHealth", 1, 100000),
                TurnSeconds = r.Number("turnSeconds", 0.1, 3600),
                CountdownBeeps = r.Integer("countdownBeeps", 0, 60),
                WeaponDamage = r.Integer("weaponDamage", 1, 10000),
                EndTurnSeconds = r.Number("endTurnSeconds", 0, 60),
                RoundOverSeconds = r.Number("roundOverSeconds", 0, 600),
                ReadyHoldSeconds = r.Number("readyHoldSeconds", 0, 60),
                ReadyFadeSeconds = r.Number("readyFadeSeconds", 0, 60),
                AiThinkSeconds = r.Number("aiThinkSeconds", 0, 60),
                AiDegreesPerSecond = r.Number("aiDegreesPerSecond", 1, 3600),
                PointsHit = r.Integer("pointsHit", 0, 1000000),
                PointsBounce = r.Integer("pointsBounce", 0, 1000000),
                PointsFlightTime = r.Integer("pointsFlightTime", 0, 1000000),
                FlightBonusSeconds = r.Number("flightBonusSeconds", 0.001, 60),
            };
            r.Done();
            return rules;
        }

        public static ShotRules ReadShot(SettingsReader r)
        {
            var rules = new ShotRules(true)
            {
                MaxBounces = r.Integer("maxBounces", 1, 100),
                MuzzleOffset = r.Number("muzzleOffset", 0, 10),
                Speed = r.Number("speed", 0.1, 1000),
                DegreesPerSecond = r.Number("degreesPerSecond", 1, 3600),
                DriveSpeed = r.Number("driveSpeed", 0, 100),
            };
            r.Done();
            return rules;
        }

        public static HazardRules ReadHazards(SettingsReader r)
        {
            var rules = new HazardRules(true)
            {
                ShieldWallOffset = r.Number("shieldWallOffset", 0, 20),
                ShieldWallLength = r.Number("shieldWallLength", 0.1, 20),
                ShieldWallMinLife = r.Number("shieldWallMinLife", 0, 3600),
                ShieldWallMaxLife = r.Number("shieldWallMaxLife", 0, 3600),
                RedirectorLength = r.Number("redirectorLength", 0.1, 20),
                FoliageCount = r.Integer("foliageCount", 0, 1000),
                FoliageRadius = r.Number("foliageRadius", 0.01, 10),
                FoliageClearance = r.Number("foliageClearance", 0, 20),
                LasersAlternate = r.Flag("lasersAlternate"),
            };
            r.Check(rules.ShieldWallMinLife <= rules.ShieldWallMaxLife, "shieldWallMaxLife", "must not be less than shieldWallMinLife");
            foreach (double[] arc in r.Rows("arcs", 4, false, -Reach, Reach))
            {
                CheckSegment(r, "arcs", arc);
                rules.Arcs.Add(new[] { new Vec2(arc[0], arc[1]), new Vec2(arc[2], arc[3]) });
            }

            foreach (double[] gate in r.Rows("gates", 4, false, -Reach, Reach))
            {
                CheckSegment(r, "gates", gate);
                rules.Gates.Add(new[] { new Vec2(gate[0], gate[1]), new Vec2(gate[2], gate[3]) });
            }

            r.Done();
            return rules;
        }

        public static void ReadArsenal(SettingsReader r, GameData data)
        {
            var rules = new ArsenalRules(true)
            {
                DefaultFuse = r.Number("defaultFuse", 0, 600),
                MinFuse = r.Number("minFuse", 0, 600),
                MaxFuse = r.Number("maxFuse", 0.1, 600),
                FuseStep = r.Number("fuseStep", 0, 600),
                GrenadeRadius = r.Number("grenadeRadius", 0.1, 50),
                VortexRadius = r.Number("vortexRadius", 0.1, 50),
                Pellets = r.Integer("pellets", 1, 100),
                PelletSpread = r.Number("pelletSpread", 0, 90),
                PelletDamage = r.Number("pelletDamage", 0.01, 10),
                LaserSpeed = r.Number("laserSpeed", 0.1, 100),
                RailgunSpeed = r.Number("railgunSpeed", 0.1, 100),
            };
            r.Check(rules.MinFuse <= rules.DefaultFuse && rules.DefaultFuse <= rules.MaxFuse, "defaultFuse", "must lie between minFuse and maxFuse");

            var weapons = new string[ArsenalRules.Kinds];
            var items = new string[ArsenalRules.Kinds];
            var weaponsAbout = new string[ArsenalRules.Kinds];
            var itemsAbout = new string[ArsenalRules.Kinds];
            ReadKinds(r, "weapons", typeof(Weapon), weapons, weaponsAbout, (i, start, inPlay) =>
            {
                rules.SetStartCount((Weapon)i, start);
                rules.SetInPlay((Weapon)i, inPlay);
            });
            ReadKinds(r, "items", typeof(Item), items, itemsAbout, (i, start, inPlay) =>
            {
                rules.SetStartCount((Item)i, start);
                rules.SetInPlay((Item)i, inPlay);
            });
            r.Done();
            data.Arsenal = rules;
            data.WeaponNames = weapons;
            data.ItemNames = items;
            data.WeaponAbout = weaponsAbout;
            data.ItemAbout = itemsAbout;
        }

        // Each entry's id must be the enum's name for that place (so an id can only name a weapon or item that exists, once).
        private static void ReadKinds(SettingsReader r, string key, Type enumType, string[] names, string[] about, Action<int, int, bool> set)
        {
            string[] expected = Enum.GetNames(enumType);
            var seen = new HashSet<string>();
            var entries = r.Objects(key);
            r.Check(entries.Count == expected.Length, key, "must list all " + expected.Length + " (" + string.Join(", ", expected) + "), in that order (has " + entries.Count + ")");
            for (int i = 0; i < entries.Count; i++)
            {
                SettingsReader e = entries[i];
                string id = e.Text("id");
                string name = e.Text("name");
                int start = e.Integer("start", 0, ArsenalRules.Unlimited);
                bool inPlay = e.Flag("inPlay");
                string what = e.Text("about"); // its HUD icon's hover text (U.RICO-T49)
                e.Done();
                if (!seen.Add(id))
                {
                    e.Error("id '" + id + "' is used twice");
                }
                else if (i >= expected.Length || id != expected[i])
                {
                    e.Error("id '" + id + "' must be " + (i < expected.Length ? "'" + expected[i] + "'" : "one of " + string.Join(", ", expected)) + " here (the original's order)");
                }

                if (i < names.Length)
                {
                    names[i] = name;
                    about[i] = what;
                    set(i, start, inPlay);
                }
            }

            for (int i = 0; i < names.Length; i++)
            {
                names[i] = names[i] ?? expected[i];
                about[i] = about[i] ?? string.Empty;
            }
        }

        public static LevelSettings ReadLevel(SettingsReader r)
        {
            var settings = new LevelSettings(true)
            {
                PolygonCount = r.Integer("polygonCount", 0, 200),
                Rotation = r.Number("rotation", 0, 360),
                Overlap = r.Flag("overlap"),
                Shapes = r.Choice<ShapeKind>("shapes"),
            };
            r.Done();
            return settings;
        }

        private static readonly ShapeKind[] s_concrete =
        {
            ShapeKind.Abstract, ShapeKind.Triangles, ShapeKind.Bars, ShapeKind.Circles, ShapeKind.Squares,
        };

        public static ShapeTable ReadShapes(SettingsReader r)
        {
            var kinds = new int[s_concrete.Length][][];
            for (int k = 0; k < s_concrete.Length; k++)
            {
                List<double[]> rows = r.Rows(s_concrete[k].ToString(), 2, true, -2000, 2000, ShapeLibrary.PerKind);
                r.Check(rows.Count == 0 || rows.Count == ShapeLibrary.PerKind, s_concrete[k].ToString(), "must hold exactly " + ShapeLibrary.PerKind + " outlines (has " + rows.Count + ")");
                var table = new int[rows.Count][];
                for (int i = 0; i < rows.Count; i++)
                {
                    double[] row = rows[i];
                    r.Check(row.Length >= 6, s_concrete[k].ToString(), "outline " + i + " needs at least three points");
                    table[i] = new int[row.Length];
                    for (int n = 0; n < row.Length; n++)
                    {
                        r.Check(Math.Floor(row[n]) == row[n], s_concrete[k].ToString(), "outline " + i + " must hold whole pixel numbers (has " + row[n].ToString("G6", CultureInfo.InvariantCulture) + ")");
                        table[i][n] = (int)row[n];
                    }
                }

                kinds[k] = table;
            }

            r.Done();
            return new ShapeTable(kinds);
        }

        public static void ReadMaps(SettingsReader r, GameData data)
        {
            var maps = new List<MapInfo>();
            var ids = new HashSet<string>();
            var names = new HashSet<string>();
            int random = -1;
            List<SettingsReader> entries = r.Objects("maps", 1);
            for (int i = 0; i < entries.Count; i++)
            {
                SettingsReader e = entries[i];
                string id = e.Text("id");
                string name = e.Text("name");
                string description = e.Text("description");
                bool isRandom = e.Flag("random");
                double[] border = e.Numbers("border", 4, false, -Reach, Reach);
                List<double[]> walls = e.Rows("walls", 4, false, -Reach, Reach);
                List<double[]> redirectors = e.Rows("redirectors", 5, false, -360, 360);
                e.Done();
                if (!ids.Add(id))
                {
                    e.Error("id '" + id + "' is used twice");
                }

                if (!names.Add(name))
                {
                    e.Error("name '" + name + "' is used twice");
                }

                if (!(border[2] > border[0] && border[3] > border[1]))
                {
                    e.Error("'border' must be [minX, minY, maxX, maxY] with a positive width and height");
                    continue;
                }

                if (isRandom)
                {
                    e.Check(random < 0, "random", "only one map may be the random arena");
                    random = i;
                    e.Check(walls.Count == 0 && redirectors.Count == 0, "random", "the random arena is laid afresh each round, so it has no walls or redirectors");
                }
                else
                {
                    foreach (double[] wall in walls)
                    {
                        CheckInside(e, "walls", border, wall[0], wall[1], wall[2], wall[3]);
                    }

                    foreach (double[] redirector in redirectors)
                    {
                        CheckInside(e, "redirectors", border, redirector[0], redirector[1], redirector[2], redirector[3]);
                        e.Check(redirector[4] >= 0 && redirector[4] <= 360, "redirectors", "a heading must be between 0 and 360 degrees (is " + redirector[4].ToString("G6", CultureInfo.InvariantCulture) + ")");
                    }
                }

                if (i == 0)
                {
                    e.Check(!isRandom, "random", "the first map is the practice level and must be laid by hand");
                }

                maps.Add(Build(i, id, name, description, isRandom, border, walls, redirectors));
            }

            r.Check(random >= 0, "maps", "one map must be the random arena (\"random\": true)");
            r.Done();
            data.Maps = maps;
            data.RandomMapIndex = random;
        }

        private static MapInfo Build(int index, string id, string name, string description, bool isRandom, double[] border, List<double[]> walls, List<double[]> redirectors)
        {
            Func<Ricochet.Core.Level.Level> build = () =>
            {
                var w = new List<Wall>();
                foreach (double[] wall in walls)
                {
                    w.Add(new Wall(new Vec2(wall[0], wall[1]), new Vec2(wall[2], wall[3])));
                }

                var d = new List<Redirector>();
                foreach (double[] redirector in redirectors)
                {
                    d.Add(new Redirector(new Vec2(redirector[0], redirector[1]), new Vec2(redirector[2], redirector[3]), redirector[4]));
                }

                return new Ricochet.Core.Level.Level(border[0], border[1], border[2], border[3], w, d);
            };
            return new MapInfo(index, id, name, description, build, isRandom);
        }

        public static AnnouncerData ReadAnnouncer(SettingsReader r)
        {
            var voices = new List<AnnouncerVoice>();
            var keys = new HashSet<string>();
            foreach (SettingsReader v in r.Objects("voices", 1))
            {
                string id = v.Text("id");
                string name = v.Text("name");
                string portrait = v.Text("portrait");
                double[] color = v.Numbers("color", 3, false, 0, 255);
                v.Done();
                if (!keys.Add(id))
                {
                    v.Error("id '" + id + "' is used twice");
                }

                v.Check(string.IsNullOrEmpty(id) || System.Text.RegularExpressions.Regex.IsMatch(id, "^[a-z]+$"), "id", "must be lower-case letters only (it names the recordings, sfx_<id>_win1)");
                v.Check(string.IsNullOrEmpty(portrait) || portrait.StartsWith("spr_", StringComparison.Ordinal), "portrait", "must name a GML sprite (spr_...)");
                v.Check(color[0] == Math.Floor(color[0]) && color[1] == Math.Floor(color[1]) && color[2] == Math.Floor(color[2]), "color", "must be three whole numbers 0 to 255");
                voices.Add(new AnnouncerVoice(id, name, (byte)color[0], (byte)color[1], (byte)color[2], portrait));
            }

            var names = new List<string>();
            var odds = new List<int>();
            foreach (SettingsReader f in r.Objects("frequencies"))
            {
                names.Add(f.Text("name"));
                odds.Add(f.Integer("odds", 0, 1000));
                f.Done();
            }

            r.Check(names.Count == Announcer.MaxFrequency + 1, "frequencies", "must list " + (Announcer.MaxFrequency + 1) + " settings, 0 to " + Announcer.MaxFrequency + " (has " + names.Count + ")");
            r.Check(odds.Count == 0 || odds[0] == 0, "frequencies", "setting 0 must be never (odds 0)");
            r.Check(odds.Count < 2 || odds[1] == 1, "frequencies", "setting 1 must be every time (odds 1)");

            string[] welcome = ReadLines(r, "welcome", 0);
            string[] win = ReadLines(r, "win", 2);
            string[] lose = ReadLines(r, "lose", 1);
            r.Done();
            return new AnnouncerData(voices, names.ToArray(), odds.ToArray(), welcome, win, lose);
        }

        // A kind has Announcer.LinesPerKind lines, and may use {0}..{names - 1} only.
        private static string[] ReadLines(SettingsReader r, string key, int names)
        {
            List<string> lines = r.Texts(key);
            r.Check(lines.Count == Announcer.LinesPerKind, key, "must hold exactly " + Announcer.LinesPerKind + " lines (has " + lines.Count + ")");
            for (int i = 0; i < lines.Count; i++)
            {
                try
                {
                    var args = new object[] { "A", "B" };
                    string text = string.Format(CultureInfo.InvariantCulture, lines[i], args);
                    if (lines[i].Contains("{1}") && names < 2 || lines[i].Contains("{0}") && names < 1)
                    {
                        r.Check(false, key, "line " + i + " uses a name this kind of announcement does not have (" + text + ")");
                    }
                }
                catch (FormatException)
                {
                    r.Check(false, key, "line " + i + " is not a valid format string; only {0} and {1} may appear, braces otherwise doubled");
                }
            }

            while (lines.Count < Announcer.LinesPerKind)
            {
                lines.Add(string.Empty);
            }

            return lines.ToArray();
        }

        public static MenuData ReadMenu(SettingsReader r)
        {
            string tagline = r.Text("tagline");

            var titles = new Dictionary<Room, string>();
            SettingsReader t = r.Section("titles");
            foreach (Room room in Enum.GetValues(typeof(Room)))
            {
                titles[room] = t.Text(room.ToString());
            }

            t.Done();

            var off = new List<Room>();
            foreach (string name in r.Texts("offTheMainMenu", 1))
            {
                if (Enum.TryParse(name, false, out Room room) && Enum.IsDefined(typeof(Room), room))
                {
                    r.Check(!off.Contains(room), "offTheMainMenu", "'" + name + "' is listed twice");
                    off.Add(room);
                }
                else
                {
                    r.Check(false, "offTheMainMenu", "'" + name + "' is not a room (rooms: " + string.Join(", ", Enum.GetNames(typeof(Room))) + ")");
                }
            }

            var controls = new List<ControlBinding>();
            foreach (SettingsReader c in r.Objects("controls", 1))
            {
                controls.Add(new ControlBinding(c.Text("keys"), c.Text("action")));
                c.Done();
            }

            var slides = new List<Slide>();
            foreach (SettingsReader s in r.Objects("slides", 1))
            {
                slides.Add(new Slide(s.Text("title"), s.Texts("lines", 1).ToArray()));
                s.Done();
            }

            List<string> credits = r.Texts("credits", 1);
            r.Done();
            return new MenuData(tagline, titles, off.ToArray(), controls.ToArray(), slides.ToArray(), credits.ToArray());
        }

        public static List<SettingDef> ReadRegistry(SettingsReader r, Dictionary<string, JsonValue> docs, List<string> errors)
        {
            var defs = new List<SettingDef>();
            var ids = new HashSet<string>();
            foreach (SettingsReader e in r.Objects("settings", 1))
            {
                string id = e.Text("id");
                string label = e.Text("label");
                string group = e.Text("group");
                string file = e.Text("file");
                string key = e.Text("key");
                SettingKind kind = e.Choice<SettingKind>("kind");
                bool numeric = kind == SettingKind.Integer || kind == SettingKind.Number;
                double min = 0, max = 0, step = 0;
                string[] choices = Array.Empty<string>();
                if (numeric)
                {
                    min = e.Number("min");
                    max = e.Number("max");
                    step = e.Number("step", 0.0001);
                    e.Check(min < max, "max", "must be greater than min");
                }
                else if (kind == SettingKind.Choice)
                {
                    choices = e.Texts("choices", 2).ToArray();
                }

                e.Done();
                if (!ids.Add(id))
                {
                    e.Error("id '" + id + "' is used twice");
                }

                double number = 0;
                bool flag = false;
                string choice = string.Empty;
                if (!docs.TryGetValue(file, out JsonValue doc))
                {
                    if (file.Length > 0)
                    {
                        e.Error("'file' names '" + file + "', which is not one of the data files (" + string.Join(", ", GameData.Files) + ")");
                    }
                }
                else
                {
                    JsonValue value = doc.Get(key);
                    if (value == null)
                    {
                        e.Error("'key' names '" + key + "', which " + file + " does not have");
                    }
                    else if (numeric && value.Kind == JsonKind.Number)
                    {
                        number = value.Number;
                        e.Check(number >= min && number <= max, "id", "the stock value " + number.ToString("G6", CultureInfo.InvariantCulture) + " in " + file + " is outside the setting's range " + min.ToString("G6", CultureInfo.InvariantCulture) + " to " + max.ToString("G6", CultureInfo.InvariantCulture));
                        e.Check(kind != SettingKind.Integer || Math.Floor(number) == number, "kind", "is Integer but " + file + "'s '" + key + "' is not a whole number");
                    }
                    else if (kind == SettingKind.Flag && value.Kind == JsonKind.Bool)
                    {
                        flag = value.Bool;
                    }
                    else if (kind == SettingKind.Choice && value.Kind == JsonKind.String)
                    {
                        choice = value.Text;
                        e.Check(Array.IndexOf(choices, choice) >= 0, "id", "the stock value '" + choice + "' in " + file + " is not one of the setting's choices");
                    }
                    else
                    {
                        e.Error("'kind' is " + kind + " but " + file + "'s '" + key + "' is " + value.Kind);
                    }
                }

                defs.Add(new SettingDef(id, label, group, file, key, kind, min, max, step, choices, number, flag, choice));
            }

            r.Done();
            return defs;
        }

        // U.RICO-T46: the level backdrop. Room numbers are the original's pixels; GML names are checked against the art
        // catalogue by the editor tests (Core has no art), so here they only need to be there and not repeat within a group.
        public static Ricochet.Core.Art.BackdropRules ReadBackdrop(SettingsReader r)
        {
            var rules = new Ricochet.Core.Art.BackdropRules
            {
                RoomWidth = r.Integer("roomWidth", 1, 10000),
                RoomHeight = r.Integer("roomHeight", 1, 10000),
            };

            var groups = new List<IReadOnlyList<string>>();
            foreach (SettingsReader g in r.Objects("groups", 1))
            {
                g.Text("name");
                List<string> backgrounds = g.Texts("backgrounds", 1);
                g.Check(new HashSet<string>(backgrounds).Count == backgrounds.Count, "backgrounds", "names a background twice");
                g.Done();
                if (backgrounds.Count > 0)
                {
                    groups.Add(backgrounds);
                }
            }

            rules.Groups = groups;

            var clouds = new List<Ricochet.Core.Art.CloudSpot>();
            foreach (SettingsReader c in r.Objects("clouds"))
            {
                clouds.Add(new Ricochet.Core.Art.CloudSpot(c.Text("sprite"), c.Number("x", -10000, 10000), c.Number("y", -10000, 10000)));
                c.Done();
            }

            rules.Clouds = clouds;
            rules.CloudAlpha = r.Number("cloudAlpha", 0, 1);
            rules.CloudSpeed = r.Number("cloudSpeed", 0, 100);
            rules.CloudWrapTop = r.Integer("cloudWrapTop", -10000, 10000);
            rules.CloudWrapBottomGap = r.Integer("cloudWrapBottomGap", -10000, 10000);
            r.Check(rules.CloudWrapTop <= rules.RoomHeight - rules.CloudWrapBottomGap, "cloudWrapTop", "must not be below roomHeight - cloudWrapBottomGap");

            SettingsReader spot = r.Section("spotlight");
            rules.SpotlightSprite = spot.Text("sprite");
            rules.SpotlightX = spot.Number("x", -10000, 10000);
            rules.SpotlightY = spot.Number("y", -10000, 10000);
            rules.SpotlightMinAlpha = spot.Number("minAlpha", 0, 1);
            rules.SpotlightMaxAlpha = spot.Number("maxAlpha", 0, 1);
            rules.SpotlightRate = spot.Number("rate", 1e-6, 1);
            spot.Check(rules.SpotlightMinAlpha <= rules.SpotlightMaxAlpha, "maxAlpha", "must be at least minAlpha");
            spot.Done();

            SettingsReader lights = r.Section("lights");
            rules.LightCount = lights.Integer("count", 0, 100);
            rules.LightMinRadius = lights.Integer("minRadius", 1, 10000);
            rules.LightMaxRadius = lights.Integer("maxRadius", 1, 10000);
            rules.LightMinChannel = lights.Integer("minChannel", 0, 255);
            rules.LightMaxChannel = lights.Integer("maxChannel", 0, 255);
            lights.Check(rules.LightMinRadius <= rules.LightMaxRadius, "maxRadius", "must be at least minRadius");
            lights.Check(rules.LightMinChannel <= rules.LightMaxChannel, "maxChannel", "must be at least minChannel");
            lights.Done();

            SettingsReader borders = r.Section("borders");
            rules.TopBorder = borders.Text("top");
            rules.BottomBorder = borders.Text("bottom");
            rules.LeftBorder = borders.Text("left");
            rules.RightBorder = borders.Text("right");
            rules.Border = borders.Integer("border", 0, 1000);
            borders.Done();

            r.Done();
            return rules;
        }

        private static void CheckSegment(SettingsReader r, string key, double[] s)
        {
            r.Check(Math.Abs(s[0] - s[2]) > 1e-6 || Math.Abs(s[1] - s[3]) > 1e-6, key, "a segment needs two different end points");
        }

        private static void CheckInside(SettingsReader r, string key, double[] border, double ax, double ay, double bx, double by)
        {
            r.Check(Math.Abs(ax - bx) > 1e-6 || Math.Abs(ay - by) > 1e-6, key, "a segment needs two different end points");
            bool inside = ax >= border[0] && ax <= border[2] && bx >= border[0] && bx <= border[2]
                && ay >= border[1] && ay <= border[3] && by >= border[1] && by <= border[3];
            r.Check(inside, key, "a segment lies outside the map's border");
        }
    }
}
