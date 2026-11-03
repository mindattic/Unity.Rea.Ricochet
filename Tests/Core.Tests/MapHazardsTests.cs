using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Rea.Ricochet.Core.Tests
{
    using Rea.Ricochet.Core.Commands;
    using Rea.Ricochet.Core.Data;
    using Rea.Ricochet.Core.Level;
    using Rea.Ricochet.Core.Match;

    // U.RICO-T111 (spike, never the default): maps.json's per-map "hazards" lays the electric arcs, laser gates and foliage
    // on a map-room-only map's rounds. The original laid none (U.RICO-T91), so every shipped map says false and the random
    // arena and practice range may not ask for them.
    public class MapHazardsTests
    {
        private sealed class World : IGameWorld
        {
            public System.Collections.Generic.IReadOnlyList<Target> DummyTargets() => new System.Collections.Generic.List<Target>();
        }

        private static ISettingsSource Stock => GameData.FindStock();

        private static MemorySettingsSource WithHazards(string mapId)
        {
            string maps = Stock.Read("maps.json");
            var entry = new Regex("(\"id\": \"" + mapId + "\"[^}]*?\"hazards\": )false");
            Assert.That(entry.IsMatch(maps), mapId + " has a hazards flag");
            return new MemorySettingsSource(Stock).Set("maps.json", entry.Replace(maps, "${1}true", 1));
        }

        [TearDown]
        public void BackToStock() => GameData.Use(null);

        [Test]
        public void NoShippedMapLaysHazards()
        {
            GameData data = GameData.Load(Stock);
            Assert.That(data.Maps.Where(m => m.LaysHazards), Is.Empty);
        }

        [Test]
        public void AMapRoomOnlyMapMayLayHazards()
        {
            GameData data = GameData.Load(WithHazards("crossfire"));
            Assert.That(data.Maps.Where(m => m.LaysHazards).Select(m => m.Id), Is.EqualTo(new[] { "crossfire" }));
        }

        [TestCase("random")]
        [TestCase("practice")]
        public void TheDefaultLevelsMayNotLayHazards(string mapId)
        {
            var ex = Assert.Throws<SettingsException>(() => GameData.Load(WithHazards(mapId)));
            Assert.That(ex.Message, Does.Contain("hazards"));
        }

        [Test]
        public void AHazardMapsRoundsAreLaidWithArcsGatesAndFoliage_TheRandomArenaStaysBare()
        {
            GameData.Use(WithHazards("crossfire"));
            int crossfire = Maps.All.Single(m => m.Id == "crossfire").Index;

            Assert.That(Hazardous(crossfire), Is.True, "the hazard map lays them");
            Assert.That(Hazardous(Maps.RandomIndex), Is.False, "the random arena stays as the original's");
        }

        private static bool Hazardous(int map)
        {
            var authority = new GameAuthority(new World(), matchRules: NoReadyCard.Rules(), seed: 11);
            Assert.That(authority.LaysHazards, Is.False);
            authority.AddPlayer(1);
            authority.AddPlayer(2);
            authority.SetSeat(1, 0);
            authority.SetSeat(2, 1);
            Match match = authority.BeginMatch(map, 0);
            match.Join(0, SeatKind.Human, "A");
            match.Join(1, SeatKind.Human, "B");
            authority.StartMatch();
            return authority.Hazards.Hazards.Any(h => h.Kind == HazardKind.Electricity || h.Kind == HazardKind.Laser || h.Kind == HazardKind.Foliage);
        }
    }
}
