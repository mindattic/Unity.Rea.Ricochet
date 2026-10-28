using System.Linq;
using NUnit.Framework;

namespace Ricochet.Core.Tests
{
    using Ricochet.Core.Aim;
    using Ricochet.Core.Level;
    using Ricochet.Core.Match;
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
                Assert.That(level.Walls.Count, Is.GreaterThanOrEqualTo(20 * 3), "seed " + seed);
                Assert.That(level.Walls.All(w => level.Contains(w.A) && level.Contains(w.B)), Is.True, "seed " + seed);
            }
        }

        [Test]
        public void ThePolygonCountSetting_IsHonoured()
        {
            var none = new LevelSettings { PolygonCount = 0 };
            Assert.That(LevelGenerator.Generate(5, none).Level.Walls, Is.Empty);
            var three = new LevelSettings { PolygonCount = 3, Shapes = ShapeKind.Triangles };
            Assert.That(LevelGenerator.Generate(5, three).Polygons, Is.EqualTo(3));
        }

        [Test]
        public void SeatsOneToThreeCanAlwaysTradeShotsWithSeatZero()
        {
            // U.RICO-T72: a random level used to leave about one seed in ten with no shot between seats 0 and 1.
            for (int seed = 1; seed <= 100; seed++)
            {
                RoundLayout round = LevelGenerator.Generate(seed);
                for (int seat = 1; seat < 4; seat++)
                {
                    var targets = new[] { new Target("a", round.SpawnPoint(0), Seating.PlayerRadius), new Target("b", round.SpawnPoint(seat), Seating.PlayerRadius) };
                    Assert.That(BounceSolver.FindHeading(round.SpawnPoint(0), "b", round.Level, targets), Is.Not.Null, "seed " + seed + " 0 to " + seat);
                    Assert.That(BounceSolver.FindHeading(round.SpawnPoint(seat), "a", round.Level, targets), Is.Not.Null, "seed " + seed + " " + seat + " to 0");
                }
            }
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
            var tight = new LevelSettings { PolygonCount = 12, Shapes = ShapeKind.Squares, Overlap = false };
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
        public void TheLibraryHoldsSixteenSimpleOutlinesOfEachOfTheFiveKinds()
        {
            foreach (ShapeKind kind in new[] { ShapeKind.Abstract, ShapeKind.Triangles, ShapeKind.Bars, ShapeKind.Circles, ShapeKind.Squares })
            {
                for (int i = 0; i < ShapeLibrary.PerKind; i++)
                {
                    Vec2[] outline = ShapeLibrary.Outline(new ShapeId(kind, i));
                    string name = kind + " " + (i + 1);
                    Assert.That(outline.Length, Is.GreaterThanOrEqualTo(3), name);
                    for (int a = 0; a < outline.Length; a++)
                    {
                        for (int b = a + 1; b < outline.Length; b++)
                        {
                            if (b == a + 1 || (a == 0 && b == outline.Length - 1))
                            {
                                continue;
                            }

                            Assert.That(
                                Geometry.SegmentIntersection(outline[a], outline[(a + 1) % outline.Length], outline[b], outline[(b + 1) % outline.Length], true, out _, out _),
                                Is.False,
                                name + " crosses itself");
                        }
                    }
                }
            }
        }

        [Test]
        public void TheKindsAreTheOriginalsOwnShapes()
        {
            // obj_square1 is a 140 x 140 pixel square, obj_bar1 a 26 x 381 pixel bar, obj_circle1 a 92 pixel round one.
            double px = ShapeLibrary.UnitsPerPixel;
            Vec2[] square = ShapeLibrary.Outline(new ShapeId(ShapeKind.Squares, 0));
            Assert.That(square.Length, Is.EqualTo(4));
            Assert.That(square.Max(v => v.X) - square.Min(v => v.X), Is.EqualTo(140 * px).Within(1e-9));
            Vec2[] bar = ShapeLibrary.Outline(new ShapeId(ShapeKind.Bars, 0));
            Assert.That(bar.Max(v => v.Y) - bar.Min(v => v.Y), Is.EqualTo(381 * px).Within(1e-9));
            Vec2[] circle = ShapeLibrary.Outline(new ShapeId(ShapeKind.Circles, 0));
            Assert.That(circle.Length, Is.GreaterThan(8));
            Assert.That(circle.Max(v => v.X) - circle.Min(v => v.X), Is.EqualTo(92 * px).Within(3 * px));
        }

        [Test]
        public void EachShapeSettingPicksItsOwnKind_AndRandomMixesAllFive()
        {
            foreach (ShapeKind kind in new[] { ShapeKind.Triangles, ShapeKind.Bars, ShapeKind.Squares })
            {
                var dice = new System.Random(3);
                for (int i = 0; i < 100; i++)
                {
                    Assert.That(ShapeLibrary.Pick(kind, dice).Kind, Is.EqualTo(kind));
                }
            }

            var mixer = new System.Random(4);
            var seen = new System.Collections.Generic.HashSet<ShapeKind>();
            var indexes = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < 400; i++)
            {
                ShapeId id = ShapeLibrary.Pick(ShapeKind.Random, mixer);
                Assert.That(id.Kind, Is.Not.EqualTo(ShapeKind.Random));
                seen.Add(id.Kind);
                indexes.Add(id.Index);
            }

            Assert.That(seen.Count, Is.EqualTo(5));
            Assert.That(indexes.Count, Is.EqualTo(ShapeLibrary.PerKind));
        }

        [Test]
        public void EveryShapeSetting_LaysShapesInsideTheBorder_WithSeatsClear()
        {
            foreach (ShapeKind kind in System.Enum.GetValues(typeof(ShapeKind)))
            {
                var settings = new LevelSettings { Shapes = kind };
                for (int seed = 0; seed < 15; seed++)
                {
                    RoundLayout round = LevelGenerator.Generate(seed, settings);
                    string name = kind + " seed " + seed;
                    Assert.That(round.Polygons, Is.GreaterThanOrEqualTo(18), name);
                    Assert.That(round.Level.Walls.All(w => round.Level.Contains(w.A) && round.Level.Contains(w.B)), Is.True, name);
                    foreach (Vec2 spot in round.Spots)
                    {
                        foreach (Wall wall in round.Level.Walls)
                        {
                            Assert.That(Geometry.SegmentDistance(wall.A, wall.B, spot, out _), Is.GreaterThan(Seating.PlayerRadius), name);
                        }
                    }
                }
            }
        }

        [Test]
        public void RotationNone_KeepsBarsUpright_AndNinetyTurnsThemInQuarters()
        {
            var upright = new LevelSettings { PolygonCount = 5, Shapes = ShapeKind.Bars, Rotation = 0 };
            var quarter = new LevelSettings { PolygonCount = 5, Shapes = ShapeKind.Bars, Rotation = 90 };
            bool sideways = false;
            for (int seed = 0; seed < 20; seed++)
            {
                foreach (Wall w in LevelGenerator.Generate(seed, upright).Level.Walls)
                {
                    Assert.That(w.A.X == w.B.X || w.A.Y == w.B.Y, Is.True);
                }

                foreach (Wall w in LevelGenerator.Generate(seed, quarter).Level.Walls)
                {
                    Assert.That(System.Math.Min(System.Math.Abs(w.A.X - w.B.X), System.Math.Abs(w.A.Y - w.B.Y)), Is.LessThan(1e-9), "bars turn in quarters");
                    sideways |= w.A.Y == w.B.Y && System.Math.Abs(w.A.X - w.B.X) > 100 * ShapeLibrary.UnitsPerPixel;
                }
            }

            Assert.That(sideways, Is.True, "some bar lies on its side");
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

            MapInfo pillars = Maps.At(0); // the practice range, the one hand-laid fixture
            Assert.That(pillars.Layout(7).Level.Walls.Count, Is.EqualTo(pillars.Layout(8).Level.Walls.Count));
            Assert.That(pillars.Layout(7).SpawnPoint(3), Is.EqualTo(Seating.SpawnPoint(3)));
        }
    }
}
