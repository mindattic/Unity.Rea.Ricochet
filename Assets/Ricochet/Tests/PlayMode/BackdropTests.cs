// U.RICO-T46: the level backdrop: the round's floor from setup_backgrounds' groups, the eight clouds at 0.15 drifting left,
// the spotlight's pulse, obj_lights' ten lights through the render style, the four border sprites, and a random level's
// shapes drawn as their sprites. A new round (a reroll) rolls a new floor and lights from the level's seed and version.
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Ricochet.Core.Art;
using Ricochet.Core.Level;
using Unity.Multiplayer;
using Unity.Multiplayer.Core;
using Unity.Multiplayer.Testing;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ricochet.Tests
{
    public class BackdropTests : NetSessionTest
    {
        private BackdropView _backdrop;
        private LevelView _level;

        public BackdropTests() : base(Topology.Host, clients: 1) { }

        protected override void OnSessionsCreated()
        {
            SetPlayerPrefab(GamePrefabs.Player);
            RegisterPrefab(GamePrefabs.Dummy);
        }

        [TearDown]
        public void Clear()
        {
            if (_backdrop != null)
            {
                Object.Destroy(_backdrop.gameObject);
            }

            if (_level != null)
            {
                Object.Destroy(_level.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator TheRoundsFloorCloudsSpotlightLightsAndBorders_AreTheOriginals()
        {
            yield return StartAndJoinAll();
            Arena arena = Arena.Serve(Server, GamePrefabs.Dummy);
            bool paused = false;
            _backdrop = BackdropView.Create(() => paused);
            BackdropRules rules = BackdropRules.Default;

            BackdropPlan plan = Backdrop.Plan(Backdrop.SeedFor(Arena.LevelSeed, Arena.LevelVersion));
            Assert.That(_backdrop.Background, Is.EqualTo(plan.Background), "the floor is the one Core rolls from the level's seed and version");
            Assert.That(rules.Groups[_backdrop.Plan.Group], Does.Contain(_backdrop.Background));
            Assert.That(_backdrop.Floor.sprite, Is.Not.Null.And.SameAs(GameArt.Background(plan.Background)), "the floor's art is missing");
            Assert.That(_backdrop.Floor.drawMode, Is.EqualTo(SpriteDrawMode.Tiled));
            Level level = Arena.Level;
            Assert.That(_backdrop.Floor.bounds.min.x, Is.LessThan((float)level.MinX));
            Assert.That(_backdrop.Floor.bounds.max.y, Is.GreaterThan((float)level.MaxY), "the floor reaches under the frame");

            // Eight clouds from the art at 0.15, drifting left a pixel a step.
            Assert.That(_backdrop.CloudCount, Is.EqualTo(8));
            for (int i = 0; i < _backdrop.CloudCount; i++)
            {
                Assert.That(_backdrop.CloudRenderer(i).sprite, Is.SameAs(GameArt.Frame(rules.Clouds[i].Sprite)), "cloud " + i);
                Assert.That(_backdrop.CloudRenderer(i).color.a, Is.EqualTo(0.15f).Within(1e-4f));
            }

            float x0 = _backdrop.CloudRoomPosition(1).x;
            yield return new WaitForSeconds(0.5f);
            float drift = x0 - _backdrop.CloudRoomPosition(1).x;
            Assert.That(drift, Is.InRange(15f, 45f), "about 30 room pixels in half a second at 60 steps a second (moved " + drift + ")");

            // Paused, they stand still (isPaused).
            paused = true;
            yield return null;
            float held = _backdrop.CloudRoomPosition(1).x;
            yield return new WaitForSeconds(0.2f);
            Assert.That(_backdrop.CloudRoomPosition(1).x, Is.EqualTo(held));
            paused = false;

            // The spotlight pulses inside obj_spotlight's range.
            Assert.That(_backdrop.Spotlight.sprite, Is.SameAs(GameArt.Frame(rules.SpotlightSprite)));
            Assert.That(_backdrop.Spotlight.color.a, Is.InRange(0.049f, 0.151f));

            // The four border sprites frame the level from outside.
            Assert.That(_backdrop.Borders.Select(b => b.sprite.name), Is.EquivalentTo(new[]
            {
                GameArt.Frame(rules.TopBorder).name, GameArt.Frame(rules.BottomBorder).name,
                GameArt.Frame(rules.LeftBorder).name, GameArt.Frame(rules.RightBorder).name,
            }));
            Assert.That(_backdrop.Borders.Min(b => b.sortingOrder), Is.GreaterThan(FogOfWar.SortingOrder), "draw_borders is over the fog");
            SpriteRenderer top = _backdrop.Borders.First(b => b.name == "Top border");
            Assert.That(top.bounds.min.y, Is.EqualTo((float)level.MaxY).Within(1e-3f));

            // obj_lights' ten lights through the render style (URP's 2D lights; none on the built-in pipeline).
            bool urp = RenderStyle.Current.Name == "URP 2D";
            Assert.That(_backdrop.LightCount, Is.EqualTo(urp ? 10 : 0));

            // A new round: a fresh random level, whose shapes are drawn as their sprites, and a backdrop rolled for it.
            _level = LevelView.Create(Arena.Level);
            string before = _backdrop.Background;
            int seedBefore = Backdrop.SeedFor(Arena.LevelSeed, Arena.LevelVersion);
            Assert.That(arena.RerollLevel(), Is.True);
            yield return null;
            yield return null;
            int seedAfter = Backdrop.SeedFor(Arena.LevelSeed, Arena.LevelVersion);
            Assert.That(seedAfter, Is.Not.EqualTo(seedBefore));
            Assert.That(_backdrop.Background, Is.EqualTo(Backdrop.Plan(seedAfter).Background));
            Assert.That(_backdrop.LightCount, Is.EqualTo(urp ? 10 : 0), "the old round's lights went");
            RoundLayout layout = Arena.Layout;
            Assert.That(layout.Shapes.Count, Is.GreaterThan(0));
            Assert.That(_level.ShapesFromArt, Is.EqualTo(layout.Shapes.Count), "a shape fell back to its outline");
            LaidShape first = layout.Shapes[0];
            Transform drawn = _level.transform.Find("Shape 0");
            Assert.That(drawn.GetComponent<SpriteRenderer>().sprite, Is.SameAs(GameArt.Frame(GmlSprites.Shape(first.Id.Kind, first.Id.Index))));
            Assert.That(Vector2.Distance(drawn.position, Arena.ToUnity(first.Origin)), Is.LessThan(1e-4f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(drawn.eulerAngles.z, (float)first.Degrees)), Is.LessThan(1e-3f));
            Debug.Log("[Ricochet] Backdrop " + before + " -> " + _backdrop.Background);
        }
    }
}
