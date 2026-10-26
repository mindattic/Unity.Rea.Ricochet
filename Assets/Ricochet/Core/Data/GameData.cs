// The game's data (UR-T35): the rules, maps, announcer lines, menu text, level shapes and the Settings screen's registry
// live in JSON files (Assets/StreamingAssets/Settings), not in C# initialisers. Core reads them through an ISettingsSource
// (a folder, memory in a test) with its own small JSON parser, since Unity's .NET has no System.Text.Json, and validates
// the lot on load: every problem is reported with its file, key and line (ranges, unique ids, every referenced id exists).
// GameData.Current is the loaded stock data every static accessor (Maps.All, Announcer.Voices, MenuFlow.Slides,
// MatchRules.Default ...) reads, so the stock files reproduce what the code used to hard-code.
using System;
using System.Collections.Generic;
using System.IO;

namespace Ricochet.Core.Data
{
    using Ricochet.Core.Aim;
    using Ricochet.Core.Arsenal;
    using Ricochet.Core.Level;
    using Ricochet.Core.Match;

    public sealed class GameData
    {
        /// <summary>Where the stock data lives under a project: Assets/StreamingAssets/Settings.</summary>
        public static readonly string[] StockFolder = { "Assets", "StreamingAssets", "Settings" };

        public const string MatchFile = "match.json";
        public const string ShotFile = "shot.json";
        public const string HazardsFile = "hazards.json";
        public const string ArsenalFile = "arsenal.json";
        public const string LevelFile = "level.json";
        public const string ShapesFile = "shapes.json";
        public const string MapsFile = "maps.json";
        public const string AnnouncerFile = "announcer.json";
        public const string MenuFile = "menu.json";
        public const string RegistryFile = "registry.json";
        public const string BackdropFile = "backdrop.json";

        /// <summary>Every data file Core reads, in load order.</summary>
        public static readonly string[] Files =
        {
            MatchFile, ShotFile, HazardsFile, ArsenalFile, LevelFile, ShapesFile, MapsFile, AnnouncerFile, MenuFile, RegistryFile, BackdropFile,
        };

        private static readonly object s_lock = new object();
        private static ISettingsSource s_source;
        private static GameData s_current;
        private static SettingsException s_failure;

        internal GameData()
        {
        }

        /// <summary>The prototypes the rules classes copy their defaults from.</summary>
        internal MatchRules Match { get; set; }

        internal ShotRules Shot { get; set; }

        internal HazardRules Hazards { get; set; }

        internal ArsenalRules Arsenal { get; set; }

        internal LevelSettings Level { get; set; }

        /// <summary>The weapons' names, in <see cref="Weapon"/> order.</summary>
        public IReadOnlyList<string> WeaponNames { get; internal set; }

        /// <summary>The items' names, in <see cref="Item"/> order.</summary>
        public IReadOnlyList<string> ItemNames { get; internal set; }

        public IReadOnlyList<MapInfo> Maps { get; internal set; }

        /// <summary>The random arena's index in <see cref="Maps"/>.</summary>
        public int RandomMapIndex { get; internal set; }

        public AnnouncerData Announcer { get; internal set; }

        public MenuData Menu { get; internal set; }

        public ShapeTable Shapes { get; internal set; }

        /// <summary>The level backdrop (U.RICO-T46): floors, clouds, spotlight, lights and border sprites.</summary>
        public Ricochet.Core.Art.BackdropRules Backdrop { get; internal set; }

        /// <summary>The Settings screen's registry, in screen order.</summary>
        public IReadOnlyList<SettingDef> Registry { get; internal set; }

        /// <summary>The stock data: loaded on first use from <see cref="Use"/>'s source, or else the project's StreamingAssets/Settings.</summary>
        public static GameData Current
        {
            get
            {
                lock (s_lock)
                {
                    if (s_failure != null)
                    {
                        throw s_failure; // a bad file fails once, not by re-reading every file on every access
                    }

                    if (s_current == null)
                    {
                        try
                        {
                            s_current = Load(s_source ?? FindStock());
                        }
                        catch (SettingsException ex)
                        {
                            s_failure = ex;
                            throw;
                        }
                    }

                    return s_current;
                }
            }
        }

        /// <summary>Reads the data from <paramref name="source"/> from now on (the next use loads it; null goes back to the project's stock folder).</summary>
        public static void Use(ISettingsSource source)
        {
            lock (s_lock)
            {
                s_source = source;
                s_current = null;
                s_failure = null;
            }
        }

        /// <summary>The stock data folder, found by walking up from the program's and the working folder, for tests and the editor.</summary>
        public static ISettingsSource FindStock()
        {
            foreach (string start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
            {
                string dir = start;
                while (!string.IsNullOrEmpty(dir))
                {
                    string candidate = Path.Combine(dir, Path.Combine(StockFolder));
                    if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, MatchFile)))
                    {
                        return new DirectorySettingsSource(candidate);
                    }

                    dir = Path.GetDirectoryName(dir);
                }
            }

            throw new InvalidOperationException("Ricochet's data files (" + string.Join("/", StockFolder) + ") were not found; the game must call GameData.Use with its StreamingAssets folder before the first rule is read.");
        }

        /// <summary>
        /// Loads and validates every data file. Throws <see cref="SettingsException"/> listing every problem found, each
        /// naming its file, key and line.
        /// </summary>
        public static GameData Load(ISettingsSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var errors = new List<string>();
            var docs = new Dictionary<string, JsonValue>();
            foreach (string file in Files)
            {
                string text = source.Read(file);
                if (text == null)
                {
                    errors.Add(file + ": the file is missing");
                    continue;
                }

                try
                {
                    JsonValue doc = Json.Parse(text);
                    if (doc.Kind != JsonKind.Object)
                    {
                        errors.Add(file + ": the document must be a JSON object");
                        continue;
                    }

                    docs[file] = doc;
                }
                catch (JsonException ex)
                {
                    errors.Add(file + ": not valid JSON, " + ex.Message);
                }
            }

            var data = new GameData();
            SettingsReader Root(string file) => new SettingsReader(docs.TryGetValue(file, out JsonValue doc) ? doc : null, file, errors);

            data.Match = DataReaders.ReadMatch(Root(MatchFile));
            data.Shot = DataReaders.ReadShot(Root(ShotFile));
            data.Hazards = DataReaders.ReadHazards(Root(HazardsFile));
            DataReaders.ReadArsenal(Root(ArsenalFile), data);
            data.Level = DataReaders.ReadLevel(Root(LevelFile));
            data.Shapes = DataReaders.ReadShapes(Root(ShapesFile));
            DataReaders.ReadMaps(Root(MapsFile), data);
            data.Announcer = DataReaders.ReadAnnouncer(Root(AnnouncerFile));
            data.Menu = DataReaders.ReadMenu(Root(MenuFile));
            data.Registry = DataReaders.ReadRegistry(Root(RegistryFile), docs, errors);
            data.Backdrop = DataReaders.ReadBackdrop(Root(BackdropFile));

            if (errors.Count > 0)
            {
                throw new SettingsException(errors, source.Describe);
            }

            return data;
        }
    }
}
