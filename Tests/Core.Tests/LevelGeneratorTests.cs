using System.Linq;
using NUnit.Framework;

namespace Ricochet.Core.Tests
{
    // UR-T23: every round is a freshly randomised level (randomize_level.gml / randomize_player_position.gml).
    public class LevelGeneratorTests
    {
        [Test]
        public void TheSameSeedMakesTheSameRound_AndAnotherSeedAnotherOne()
        {
            RoundLayout a = LevelGenerator.Generate(42);
            RoundLayout b = LevelGenerator.Generate(42);
            RoundLayout c = LevelGenerator.Generate(43);
            Assert.That(b.Level.Walls.Select(w => (w.A, w.B)), Is.EqualTo(a.Level.Walls.Select(w => (w.A, w.B))));
            Assert.That(b.Spots, Is.EqualTo(a.Spots));
            Assert.That(c.Level.Walls.Select(w => w.A), Is.Not.EqualTo(a.Level.Walls.Select(w => w.A)));
            Assert.That(c.Spots, Is.Not.EqualTo(a.Spots));
        }

        [Test]
        public void TheDefaultSettingsLayTwentyPolygons_InsideTheBorder()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                RoundLayout round = LevelGenerator.Generate(seed);
                Level level = round.Level;
                Assert.That(round.Polygons, Is.EqualTo(20), "seed " + seed);
                Assert.That(level.Walls.Count, Is.InRange(20 * 3, 20 * 6), "seed " + seed);
                Assert.That(level.Walls.All(w => level.Contains(w.A) && level.Contains(w.B)), Is.True, "seed " + seed);
            }
        }

        [Test]
        public void ThePolygonCountSetting_IsHonoured()
        {
            var none = new LevelSettings { PolygonCount = 0 };
            Assert.That(LevelGenerator.Generate(5, none).Level.Walls, Is.Empty);
            var triangles = new LevelSettings { PolygonCount = 3, MinSides = 3, MaxSides = 3 };
            Assert.That(LevelGenerator.Generate(5, triangles).Level.Walls.Count, Is.EqualTo(9));
        }

        [Test]
        public void SixteenSeatSpots_AreInsideTheLevel_ClearOfEveryWall_TheDummies_AndEachOther()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                RoundLayout round = LevelGenerator.Generate(seed);
                Assert.That(round.Spots.Count, Is.EqualTo(MatchRules.MaxSeats));
                for (int i = 0; i < round.Spots.Count; i++)
                {
                    Vec2 spot = round.Spots[i];
                    Assert.That(round.Level.Contains(spot), Is.True, "seed " + seed + " seat " + i);
                    foreach (Wall wall in round.Level.Walls)
                    {
                        Assert.That(Geometry.SegmentDistance(wall.A, wall.B, spot, out _), Is.GreaterThan(Seating.PlayerRadius), "seed " + seed + " seat " + i + " on a wall");
                    }

                    foreach (Vec2 dummy in Seating.DummyStarts)
                    {
                        Assert.That(Vec2.Distance(spot, dummy), Is.GreaterThan(Seating.PlayerRadius * 2), "seed " + seed + " seat " + i + " on a dummy");
                    }

                    for (int j = i + 1; j < round.Spots.Count; j++)
                    {
                        Assert.That(Vec2.Distance(spot, round.Spots[j]), Is.GreaterThan(Seating.PlayerRadius * 2), "seed " + seed + " seats " + i + "," + j);
                    }
                }
            }
        }

        [Test]
        public void TheLaidHazardsAreKeptClear_OnEverySeed()
        {
            HazardRules hazards = HazardRules.Default;
            for (int seed = 0; seed < 50; seed++)
            {
                RoundLayout round = LevelGenerator.Generate(seed);
                foreach (Vec2[] hazard in hazards.Arcs.Concat(hazards.Gates))
                {
                    foreach (Wall wall in round.Level.Walls)
                    {
                        Assert.That(Geometry.SegmentDistance(wall.A, wall.B, hazard[0], out _), Is.GreaterThan(0.5), "seed " + seed);
                        Assert.That(Geometry.SegmentDistance(wall.A, wall.B, hazard[1], out _), Is.GreaterThan(0.5), "seed " + seed);
                    }

                    foreach (Vec2 spot in round.Spots)
                    {
                        Assert.That(Geometry.SegmentDistance(hazard[0], hazard[1], spot, out _), Is.GreaterThan(Seating.PlayerRadius * 2), "seed " + seed);
                    }
                }
            }
        }

        [Test]
        public void ACrowdedLevel_StillSeatsEveryoneClearOfEveryWall()
        {
            var crowded = new LevelSettings { PolygonCount = 30, Overlap = true };
            for (int seed = 0; seed < 20; seed++)
            {
                RoundLayout round = LevelGenerator.Generate(seed, crowded);
                foreach (Vec2 spot in round.Spots)
                {
                    foreach (Wall wall in round.Level.Walls)
                    {
                        Assert.That(Geometry.SegmentDistance(wall.A, wall.B, spot, out _), Is.GreaterThan(Seating.PlayerRadius), "seed " + seed);
                    }
                }
            }
        }

        [Test]
        public void WithoutOverlap_PolygonsKeepApart_AndWithItTheyMay()
        {
            // Two polygons never touch when overlap is off: no wall of one comes near another's walls' circle.
            var tight = new LevelSettings { PolygonCount = 12, MinSides = 4, MaxSides = 4, Overlap = false };
            for (int seed = 0; seed < 30; seed++)
            {
                var walls = LevelGenerator.Generate(seed, tight).Level.Walls;
                Assert.That(walls.Count, Is.EqualTo(48), "seed " + seed);
                for (int a = 0; a < walls.Count; a++)
                {
                    for (int b = a + 1; b < walls.Count; b++)
                    {
                        if (a / 4 == b / 4)
                        {
                            continue;
                        }

                        Assert.That(Geometry.SegmentIntersection(walls[a].A, walls[a].B, walls[b].A, walls[b].B, true, out _, out _), Is.False, "seed " + seed);
                    }
                }
            }
        }

        [Test]
        public void SeatsDifferRoundToRound()
        {
            RoundLayout a = LevelGenerator.Generate(1);
            RoundLayout b = LevelGenerator.Generate(2);
            Assert.That(a.SpawnPoint(0), Is.Not.EqualTo(b.SpawnPoint(0)));
            Assert.That(a.SpawnPoint(16), Is.EqualTo(a.SpawnPoint(0)), "seat indices wrap");
        }

        [Test]
        public void TheRandomMapRollsAFreshRoundPerSeed_AndTheHandLaidMapsStayPut()
        {
            MapInfo random = Maps.All.Single(m => m.IsRandom);
            Assert.That(Maps.RandomIndex, Is.EqualTo(random.Index));
            Assert.That(new MatchSetup().Map, Is.EqualTo(random.Index), "random is the default, as in the original");
            Assert.That(random.Layout(7).Spots, Is.Not.EqualTo(random.Layout(8).Spots));

            MapInfo pillars = Maps.At(2);
            Assert.That(pillars.Layout(7).Level.Walls.Count, Is.EqualTo(pillars.Layout(8).Level.Walls.Count));
            Assert.That(pillars.Layout(7).SpawnPoint(3), Is.EqualTo(Seating.SpawnPoint(3)));
        }
    }
}
