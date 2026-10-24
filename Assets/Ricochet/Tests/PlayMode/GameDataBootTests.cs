// The game's data at boot (UR-T35, verified in Unity by UR-T68): entering play mode, GameDataBoot points Core at
// StreamingAssets/Settings and loads every shipped file before the first scene; a bad file is logged naming the file and
// stops the game instead of letting it run on half-read rules.
// The loader and its validation are tested without Unity: dotnet test Core.slnx (Tests/Core.Tests/GameDataTests.cs).
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Ricochet.Core.Data;
using Ricochet.Core.Level;
using Ricochet.Core.Match;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ricochet.Tests
{
    public class GameDataBootTests
    {
        private string _scratch;

        [TearDown]
        public void PutTheShippedDataBack()
        {
            if (_scratch != null)
            {
                Directory.Delete(_scratch, recursive: true);
                _scratch = null;
                Assert.That(GameDataBoot.Boot(GameDataBoot.Folder, () => Assert.Fail("the shipped data no longer loads")), Is.True);
            }
        }

        [Test]
        public void TheGameLoadsTheShippedDataFromStreamingAssetsAtStartup()
        {
            Assert.That(GameDataBoot.LoadedFrom, Is.EqualTo(GameDataBoot.Folder), "GameDataBoot did not load StreamingAssets/Settings on entering play mode");
            foreach (string file in GameData.Files)
            {
                Assert.That(File.Exists(Path.Combine(GameDataBoot.Folder, file)), Is.True, file + " does not ship in StreamingAssets/Settings");
            }

            GameData data = GameData.Current;
            Assert.That(data.Maps, Is.Not.Empty, "maps");
            Assert.That(data.WeaponNames, Is.Not.Empty, "weapons");
            Assert.That(data.Registry, Is.Not.Empty, "the Settings screen's registry");
            Assert.That(Maps.All, Is.Not.Empty, "Maps.All reads the loaded data");
            Assert.That(MatchRules.Default, Is.Not.Null, "MatchRules.Default reads the loaded data");
        }

        [Test]
        public void ABadDataFileIsLoggedByName_AndStopsTheGame()
        {
            _scratch = Path.Combine(Path.GetTempPath(), "RicochetBadData-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratch);
            foreach (string file in GameData.Files)
            {
                File.Copy(Path.Combine(GameDataBoot.Folder, file), Path.Combine(_scratch, file));
            }

            File.WriteAllText(Path.Combine(_scratch, GameData.MatchFile), "{ \"this is\": not json");

            bool stopped = false;
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(GameData.MatchFile)));
            bool loaded = GameDataBoot.Boot(_scratch, () => stopped = true);

            Assert.That(loaded, Is.False, "a bad file loaded");
            Assert.That(stopped, Is.True, "a bad file did not stop the game");
            Assert.That(GameDataBoot.LoadedFrom, Is.Null);
            Assert.Throws<SettingsException>(() => { GameData unused = GameData.Current; }, "the rules read on from a bad file");
        }
    }
}
