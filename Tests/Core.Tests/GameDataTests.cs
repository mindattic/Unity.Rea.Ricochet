using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Ricochet.Core.Aim;
using Ricochet.Core.Arsenal;
using Ricochet.Core.Data;
using Ricochet.Core.Level;
using Ricochet.Core.Match;
using Ricochet.Core.Menu;

namespace Ricochet.Core.Tests
{
    // UR-T35: the rules and content are data files, loaded and validated by Core.
    public class GameDataTests
    {
        private static ISettingsSource Stock => GameData.FindStock();

        private static SettingsException Fails(Func<ISettingsSource, ISettingsSource> patch)
        {
            return Assert.Throws<SettingsException>(() => GameData.Load(patch(Stock)));
        }

        private static MemorySettingsSource Patched(ISettingsSource stock, string file, Func<string, string> edit)
        {
            return new MemorySettingsSource(stock).Set(file, edit(stock.Read(file)));
        }

        [Test]
        public void EveryShippedFileLoadsAndValidates()
        {
            var stock = (DirectorySettingsSource)Stock;
            foreach (string file in GameData.Files)
            {
                Assert.That(File.Exists(Path.Combine(stock.Directory, file)), file + " is shipped");
            }

            var shipped = Directory.GetFiles(stock.Directory, "*.json").Select(Path.GetFileName).OrderBy(n => n).ToArray();
            Assert.That(shipped, Is.EqualTo(GameData.Files.OrderBy(n => n).ToArray()), "no data file is left unloaded");

            GameData data = GameData.Load(stock);
            Assert.That(data.Maps.Count, Is.EqualTo(5));
            Assert.That(data.Registry.Count, Is.GreaterThan(0));
        }

        [Test]
        public void StockRulesReproduceTheCodedDefaults()
        {
            var match = MatchRules.Default;
            Assert.That(match.MaxHealth, Is.EqualTo(100));
            Assert.That(match.TurnSeconds, Is.EqualTo(20));
            Assert.That(match.CountdownBeeps, Is.EqualTo(4));
            Assert.That(match.WeaponDamage, Is.EqualTo(10));
            Assert.That(match.EndTurnSeconds, Is.EqualTo(50.0 / 60));
            Assert.That(match.RoundOverSeconds, Is.EqualTo(4));
            Assert.That(match.PointsHit, Is.EqualTo(100));
            Assert.That(match.PointsBounce, Is.EqualTo(25));
            Assert.That(match.PointsFlightTime, Is.EqualTo(5));
            Assert.That(match.FlightBonusSeconds, Is.EqualTo(100.0 / 1020.0));

            var shot = ShotRules.Default;
            Assert.That(new double[] { shot.MaxBounces, shot.MuzzleOffset, shot.Speed, shot.DegreesPerSecond, shot.DriveSpeed },
                Is.EqualTo(new double[] { 5, 0.6, 18, 90, 3 }));

            var hazards = HazardRules.Default;
            Assert.That(new double[] { hazards.ShieldWallOffset, hazards.ShieldWallLength, hazards.ShieldWallMinLife, hazards.ShieldWallMaxLife, hazards.RedirectorLength, hazards.FoliageCount, hazards.FoliageRadius, hazards.FoliageClearance },
                Is.EqualTo(new[] { 1.0, 2.2, 20, 120, 1.0, 6, 0.25, 1.2 }));
            Assert.That(hazards.LasersAlternate, Is.True);
            Assert.That(hazards.Arcs.Select(a => (a[0].X, a[0].Y, a[1].X, a[1].Y)), Is.EqualTo(new[] { (-6.5, 3.2, -6.5, 5.6) }));
            Assert.That(hazards.Gates.Select(a => (a[0].X, a[0].Y, a[1].X, a[1].Y)), Is.EqualTo(new[] { (4.6, 4.6, 8.2, 4.6) }));

            var arsenal = ArsenalRules.Default;
            Assert.That(arsenal.StartCount(Weapon.Cannon), Is.EqualTo(ArsenalRules.Unlimited));
            foreach (Weapon w in Enum.GetValues(typeof(Weapon)).Cast<Weapon>().Skip(1))
            {
                Assert.That(arsenal.StartCount(w), Is.EqualTo(3));
                Assert.That(arsenal.InPlay(w), Is.True);
            }

            foreach (Item i in Enum.GetValues(typeof(Item)))
            {
                Assert.That(arsenal.StartCount(i), Is.EqualTo(3));
                Assert.That(arsenal.InPlay(i), Is.True);
            }

            Assert.That(new double[] { arsenal.DefaultFuse, arsenal.MinFuse, arsenal.MaxFuse, arsenal.FuseStep, arsenal.GrenadeRadius, arsenal.VortexRadius, arsenal.Pellets, arsenal.PelletSpread, arsenal.PelletDamage, arsenal.LaserSpeed, arsenal.RailgunSpeed },
                Is.EqualTo(new[] { 2, 0.5, 6, 0.5, 2.5, 3.4, 5, 4, 0.35, 3, 8 }));
            Assert.That(ArsenalRules.Name(Weapon.Vortex), Is.EqualTo("Vortex Device"));
            Assert.That(ArsenalRules.Name(Item.AimAssist), Is.EqualTo("Aim Assist"));

            var level = LevelSettings.Default;
            Assert.That((level.PolygonCount, level.Rotation, level.Overlap, level.Shapes), Is.EqualTo((20, 360.0, false, ShapeKind.Abstract)));
        }

        [Test]
        public void ChangingOneSetOfRulesLeavesTheStockAlone()
        {
            var rules = new MatchRules { MaxHealth = 7 };
            rules.PointsHit = 1;
            Assert.That(MatchRules.Default.MaxHealth, Is.EqualTo(100));
            Assert.That(MatchRules.Default.PointsHit, Is.EqualTo(100));

            var hazards = new HazardRules();
            hazards.Arcs.Clear();
            Assert.That(new HazardRules().Arcs.Count, Is.EqualTo(1));

            var arsenal = new ArsenalRules();
            arsenal.SetStartCount(Weapon.Laser, 0);
            Assert.That(new ArsenalRules().StartCount(Weapon.Laser), Is.EqualTo(3));
        }

        [Test]
        public void StockContentReproducesTheCodedContent()
        {
            Assert.That(Maps.All.Select(m => m.Name), Is.EqualTo(new[] { "Practice Range", "Crossfire", "Pillars", "Funnel", "Random Arena" }));
            Assert.That(Maps.RandomIndex, Is.EqualTo(4));
            Level.Level crossfire = Maps.At(1).Build();
            Assert.That(crossfire.Walls.Count, Is.EqualTo(4));
            Assert.That(crossfire.Walls[2].A.X, Is.EqualTo(-11));
            Assert.That(crossfire.Walls[2].B.Y, Is.EqualTo(-6));
            Assert.That(crossfire.Redirectors[1].Heading, Is.EqualTo(180));
            Level.Level practice = Level.Level.Default();
            Assert.That((practice.Walls.Count, practice.Redirectors.Count, practice.MinX, practice.MaxY), Is.EqualTo((3, 2, -11.0, 6.0)));
            Assert.That(practice.Redirectors[1].Heading, Is.EqualTo(225));
            Assert.That(Maps.At(3).Build().Redirectors.Count, Is.EqualTo(2));
            Assert.That(Maps.At(2).Build().Walls.Count, Is.EqualTo(7));

            Assert.That(Announcer.Voices.Select(v => v.Key), Is.EqualTo(new[] { "sly", "rye", "sid", "vlad", "baxter", "doctor", "scottish" }));
            Assert.That(Announcer.VoiceAt(0).Name, Is.EqualTo("Kurt Sly"));
            Assert.That(Announcer.VoiceAt(6).Name, Is.EqualTo("Haggis McKnight"));
            Assert.That(Enumerable.Range(0, 5).Select(Announcer.Odds), Is.EqualTo(new[] { 0, 1, 3, 6, 9 }));
            Assert.That(Enumerable.Range(0, 5).Select(Announcer.FrequencyName), Is.EqualTo(new[] { "Off", "Every time", "Often", "Sometimes", "Rarely" }));
            Assert.That(Announcer.Text(new Announcement(AnnouncementKind.Win, 0, 0, 1), s => s == 0 ? "Ann" : "Bo"), Is.EqualTo("Ann sends Bo packing!"));
            Assert.That(Announcer.Text(new Announcement(AnnouncementKind.Lose, 9, 0, -1), s => "Ann"), Is.EqualTo("And it fizzles out. Better luck next turn, Ann."));

            Assert.That(MenuFlow.Tagline, Is.EqualTo("Bank it off the walls. Land it on your friends."));
            Assert.That(MenuFlow.Slides.Count, Is.EqualTo(11));
            Assert.That(MenuFlow.Slides[0].Title, Is.EqualTo("Introduction"));
            Assert.That(MenuFlow.Slides[5].Lines.Count, Is.EqualTo(4));
            Assert.That(MenuFlow.Controls.Count, Is.EqualTo(10));
            Assert.That(MenuFlow.Controls[0].ToString(), Is.EqualTo("Left / Right (A / D): Turn your tank and its aim"));
            Assert.That(MenuFlow.Credits.Count, Is.EqualTo(7));
            Assert.That(MenuFlow.Title(Room.Settings), Is.EqualTo("Game settings"));
            Assert.That(MenuFlow.OffTheMainMenu, Is.EqualTo(new[] { Room.Players, Room.Settings, Room.Controls, Room.HowToPlay, Room.Credits }));

            // The 80 outlines of UR-T60, spot-checked at their first and last points.
            foreach (ShapeKind kind in new[] { ShapeKind.Abstract, ShapeKind.Triangles, ShapeKind.Bars, ShapeKind.Circles, ShapeKind.Squares })
            {
                for (int i = 0; i < ShapeLibrary.PerKind; i++)
                {
                    Assert.That(ShapeLibrary.Outline(new ShapeId(kind, i)).Length, Is.GreaterThanOrEqualTo(3));
                }
            }

            Vec2[] first = ShapeLibrary.Outline(new ShapeId(ShapeKind.Abstract, 0));
            Assert.That(first.Length, Is.EqualTo(7));
            Assert.That(first[0].X, Is.EqualTo(71 * ShapeLibrary.UnitsPerPixel));
            Assert.That(first[6].Y, Is.EqualTo(13 * ShapeLibrary.UnitsPerPixel));
            Assert.That(ShapeLibrary.Outline(new ShapeId(ShapeKind.Squares, 15))[2].X, Is.EqualTo(94 * ShapeLibrary.UnitsPerPixel));
        }

        [Test]
        public void RegistryHoldsTheLevelSettingsAndEveryEntryMatchesTheData()
        {
            GameData data = GameData.Load(Stock);
            Assert.That(data.Registry.Select(s => s.Id).Distinct().Count(), Is.EqualTo(data.Registry.Count), "ids are unique");

            SettingDef shapes = data.Registry.Single(s => s.Id == "level.shapes");
            Assert.That(shapes.Kind, Is.EqualTo(SettingKind.Choice));
            Assert.That(shapes.Choice, Is.EqualTo("Abstract"));
            Assert.That(shapes.Choices, Is.EqualTo(Enum.GetNames(typeof(ShapeKind))));

            SettingDef rotation = data.Registry.Single(s => s.Id == "level.rotation");
            Assert.That(rotation.Number, Is.EqualTo(360));
            Assert.That(data.Registry.Single(s => s.Id == "match.turnSeconds").Number, Is.EqualTo(MatchRules.Default.TurnSeconds));
        }

        [Test]
        public void AnOutOfRangeNumberNamesItsFileKeyAndLine()
        {
            var ex = Fails(s => Patched(s, "match.json", t => t.Replace("\"turnSeconds\": 20", "\"turnSeconds\": -3")));
            Assert.That(ex.Errors[0], Does.Contain("match.json").And.Contain("'turnSeconds'").And.Contain("between 0.1 and 3600").And.Contain("(line 4)"));
            Assert.That(ex.Errors.Any(e => e.Contains("registry.json") && e.Contains("outside the setting's range")), Is.True, "the registry notices the stock value left its range too");
        }

        [Test]
        public void AUnknownKeyAndAMissingKeyAreBothReported()
        {
            var ex = Fails(s => Patched(s, "shot.json", t => t.Replace("\"speed\": 18", "\"spede\": 18")));
            Assert.That(ex.Errors.Any(e => e.Contains("shot.json") && e.Contains("'speed' is missing")), Is.True, string.Join("\n", ex.Errors));
            Assert.That(ex.Errors.Any(e => e.Contains("unknown key 'spede'")), Is.True);
        }

        [Test]
        public void EveryProblemInEveryFileIsReportedTogether()
        {
            var source = new MemorySettingsSource(Stock)
                .Set("match.json", Stock.Read("match.json").Replace("\"maxHealth\": 100", "\"maxHealth\": 0"))
                .Set("hazards.json", "{ not json")
                .Set("level.json", null);
            var ex = Assert.Throws<SettingsException>(() => GameData.Load(source));
            Assert.That(ex.Errors.Any(e => e.Contains("match.json") && e.Contains("maxHealth")), Is.True);
            Assert.That(ex.Errors.Any(e => e.Contains("hazards.json") && e.Contains("not valid JSON") && e.Contains("line 1")), Is.True);
            Assert.That(ex.Errors.Any(e => e.Contains("level.json") && e.Contains("missing")), Is.True);
        }

        [Test]
        public void MapIdsMustBeUniqueAndInsideTheirBorder()
        {
            var duplicate = Fails(s => Patched(s, "maps.json", t => t.Replace("\"id\": \"crossfire\"", "\"id\": \"practice\"")));
            Assert.That(duplicate.Errors.Any(e => e.Contains("maps.json") && e.Contains("id 'practice' is used twice")), Is.True);

            var outside = Fails(s => Patched(s, "maps.json", t => t.Replace("[-11, 3, -8, 6]", "[-11, 3, -8, 60]")));
            Assert.That(outside.Errors.Any(e => e.Contains("maps.json") && e.Contains("'walls'")), Is.True);

            var noRandom = Fails(s => Patched(s, "maps.json", t => t.Replace("\"random\": true", "\"random\": false")));
            Assert.That(noRandom.Errors.Any(e => e.Contains("random arena")), Is.True);
        }

        [Test]
        public void ArsenalIdsMustBeTheEnumNamesInOrder()
        {
            var ex = Fails(s => Patched(s, "arsenal.json", t => t.Replace("\"id\": \"Laser\"", "\"id\": \"Lazer\"")));
            Assert.That(ex.Errors.Any(e => e.Contains("arsenal.json") && e.Contains("'Lazer'") && e.Contains("'Laser'")), Is.True, string.Join("\n", ex.Errors));
        }

        [Test]
        public void AnnouncerLinesMustBeTenAndUseOnlyTheirOwnNames()
        {
            var ex = Fails(s => Patched(s, "announcer.json", t => t.Replace("\"Welcome, one and all, to another night of Ricochet!\"", "\"Hello {0}\", \"Extra\"")));
            Assert.That(ex.Errors.Any(e => e.Contains("announcer.json") && e.Contains("'welcome'") && e.Contains("exactly 10")), Is.True, string.Join("\n", ex.Errors));
            Assert.That(ex.Errors.Any(e => e.Contains("uses a name")), Is.True);
        }

        [Test]
        public void MenuMustTitleEveryRoomAndOnlyNameRealRooms()
        {
            var ex = Fails(s => Patched(s, "menu.json", t => t.Replace("\r\n", "\n").Replace("\"Lobby\": \"Lobby\"", "\"Lobbi\": \"Lobby\"").Replace("\"Credits\"\n  ],\n  \"controls\"", "\"Creditz\"\n  ],\n  \"controls\"")));
            Assert.That(ex.Errors.Any(e => e.Contains("menu.json") && e.Contains("'Lobby' is missing")), Is.True, string.Join("\n", ex.Errors));
            Assert.That(ex.Errors.Any(e => e.Contains("unknown key 'Lobbi'")), Is.True);
            Assert.That(ex.Errors.Any(e => e.Contains("'Creditz' is not a room")), Is.True, string.Join("\n", ex.Errors));
        }

        [Test]
        public void TheRegistryMustNameRealKeysInRange()
        {
            var ex = Fails(s => Patched(s, "registry.json", t => t
                .Replace("\"key\": \"maxHealth\"", "\"key\": \"maxHelth\"")
                .Replace("\"id\": \"match.turnSeconds\"", "\"id\": \"match.maxHealth\"")
                .Replace("\"file\": \"shot.json\", \"key\": \"speed\"", "\"file\": \"nope.json\", \"key\": \"speed\"")
                .Replace("\"key\": \"pellets\", \"kind\": \"Integer\", \"min\": 1, \"max\": 50", "\"key\": \"pellets\", \"kind\": \"Integer\", \"min\": 8, \"max\": 50")));
            Assert.That(ex.Errors.Any(e => e.Contains("registry.json") && e.Contains("'maxHelth'") && e.Contains("does not have")), Is.True, string.Join("\n", ex.Errors));
            Assert.That(ex.Errors.Any(e => e.Contains("id 'match.maxHealth' is used twice")), Is.True);
            Assert.That(ex.Errors.Any(e => e.Contains("nope.json")), Is.True);
            Assert.That(ex.Errors.Any(e => e.Contains("outside the setting's range")), Is.True);
        }

        [Test]
        public void ShapeOutlinesMustBeSixteenOfThreePointsOrMore()
        {
            var ex = Fails(s => Patched(s, "shapes.json", t => t.Replace("[-70, -70, 70, -70, 70, 70, -70, 70]", "[-70, -70, 70, -70]")));
            Assert.That(ex.Errors.Any(e => e.Contains("shapes.json") && e.Contains("'Squares'") && e.Contains("at least three points")), Is.True, string.Join("\n", ex.Errors));
        }
    }
}
