// U.RICO-T48: the hit and knock-out effects on a client watching the host: the "+N pts" flashing over the shooter, the
// damage in red over Ann and her tank's death blast holding with obj_flame's smoke after it, the scorch marks a shot leaves
// and the level sweeping them away, a shield wall's glass, "Bounce +1", and a crate's smoke, landing, ring when taken and
// burst when left. The step rules themselves are tested without Unity: dotnet test Core.slnx (HitLooksTests).
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Ricochet.Core.Arsenal;
using Ricochet.Core.Art;
using Unity.Multiplayer;
using Unity.Multiplayer.Core;
using Unity.Multiplayer.Testing;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ricochet.Tests
{
    public class HitEffectTests : NetSessionTest
    {
        private int _maxHealth;
        private double _minLife;
        private double _maxLife;

        public HitEffectTests() : base(Topology.Host, clients: 1) { }

        protected override void OnSessionsCreated()
        {
            SetPlayerPrefab(GamePrefabs.Player);
            RegisterPrefab(GamePrefabs.Dummy);
            RegisterPrefab(GamePrefabs.Opponent);
            RegisterPrefab(GamePrefabs.Turns);
            RegisterPrefab(GamePrefabs.Crate);
            RegisterPrefab(GamePrefabs.Hazard);
            Client.Identity = new PlayerIdentity("ann-token", "Ann", new PlayerColor(230, 80, 60));
        }

        [SetUp]
        public void KeepTheRules()
        {
            _maxHealth = Arena.MatchRules.MaxHealth;
            _minLife = Arena.DropRules.MinLife;
            _maxLife = Arena.DropRules.MaxLife;
            TestMatches.WithoutReadyCard();
        }

        [TearDown]
        public void PutTheRulesBack()
        {
            Arena.MatchRules.MaxHealth = _maxHealth;
            Arena.DropRules.MinLife = _minLife;
            Arena.DropRules.MaxLife = _maxLife;
            TestMatches.StockReadyCard();
            FieldEffects.Clear();
            GmlParticles.Clear();
        }

        [UnityTest]
        public IEnumerator AKnockOutFloatsThePointsAndTheDamage_BlowsTheTankUp_AndSmokesWhereItStood()
        {
            Arena.MatchRules.MaxHealth = 1; // one hit knocks her out
            Arena arena = null;
            Player host = null, ann = null;
            yield return Begin(a => arena = a, (h, c) => { host = h; ann = c; });
            Assert.That(arena.StartMatch(TestMatches.OnThePracticeRange()), Is.True);
            Player annOnHerClient = Mine();
            yield return WaitUntil(() => Turns.For(Client) != null && annOnHerClient.Seat == ann.Seat, "her client never got the match");
            int scorches = FieldEffects.Scorches;

            host.ServerFire(180f, 1f); // along the floor from seat 0 at x 0 to Ann's seat at x -4
            yield return WaitUntil(() => FieldEffects.KnockOuts > 0, "her tank never blew up on any peer", 10f);
            Assert.That(FloatText.Live.Any(f => f.Text.StartsWith("+") && f.Text.EndsWith(" pts") && f.Flashing), Is.True,
                "hit.gml's \"+N pts\" over the shooter: " + string.Join(", ", FloatText.Live.Select(f => f.Text)));
            FloatText damage = FloatText.Live.First(f => f.Text.StartsWith("-"));
            Assert.That(damage.Flashing, Is.False);
            Assert.That(damage.Color.r, Is.EqualTo(1f).Within(0.01f), "red");
            Assert.That(damage.Color.g, Is.EqualTo(0f).Within(0.01f), "red");
            Assert.That(FieldEffects.Scorches, Is.GreaterThan(scorches), "obj_boom_normal's and obj_playerexplosion's scorch marks");

            FloatText points = FloatText.Live.First(f => f.Text.EndsWith(" pts"));
            Color first = points.Color;
            yield return null;
            yield return null;
            yield return null;
            Assert.That(points == null || points.Color != first, Is.True, "the points never changed colour");

            yield return WaitUntil(() => FieldEffects.Flames > 0, "obj_flame never smoked", 5f);
            int smoke = GmlParticles.Thrown(GmEffect.SmokeUp);
            yield return new WaitForSeconds(0.3f);
            Assert.That(GmlParticles.Thrown(GmEffect.SmokeUp), Is.GreaterThan(smoke), "the smoke keeps rising");
            Assert.That(FieldEffects.KnockOuts, Is.GreaterThan(0), "the blast holds its last frame");
        }

        [UnityTest]
        public IEnumerator AShotsScorchMarkStaysUntilTheLevelGoes_AndAShieldWallShattersIntoGlass()
        {
            Arena arena = null;
            Player host = null, ann = null;
            yield return Begin(a => arena = a, (h, c) => { host = h; ann = c; });
            Player annOnHerClient = Mine();
            yield return WaitUntil(() => annOnHerClient.Position != Vector2.zero && annOnHerClient.Weapons != 0, "her client never got her seat");

            // Her extra bounce: "Bounce +1", flashing, over her on every peer.
            annOnHerClient.UseItem(90f, 0.5f);
            yield return WaitUntil(() => FloatText.Live.Any(f => f.Text == HitLooks.ExtraBounceText && f.Flashing), "\"Bounce +1\" never floated", 5f);

            // Her shield wall between her and the host; the host's shell shatters it.
            annOnHerClient.CycleItem(1);
            yield return WaitUntil(() => ann.Loadout.Item == Item.ShieldWall, "the shield wall was never selected");
            ann.ServerUseItem(0f, 0.5f);
            yield return WaitUntil(() => LevelHazard.In(Client).Any(h => h.Ready && h.HazardKind == Ricochet.Core.Level.HazardKind.ShieldWall), "the wall never stood", 5f);
            int rings = GmlParticles.Thrown(GmEffect.Ring);
            host.ServerFire(180f, 1f);
            yield return WaitUntil(() => FieldEffects.Shards > 0, "the wall's glass never flew", 10f);
            Assert.That(FieldEffects.Shards, Is.InRange(HitLooks.GlassMin, HitLooks.GlassMax * 2), "spawn_glass's 20-30 shards (on each peer of this process)");
            Assert.That(GmlParticles.Thrown(GmEffect.Ring), Is.GreaterThan(rings), "its ef_ring");
            yield return WaitUntil(() => FieldEffects.Shards == 0, "the glass never faded", 5f);

            // A shell on a practice dummy leaves obj_boom_normal's small scorch mark, which a new level sweeps away.
            yield return WaitUntil(() => !host.ShotInFlight, "the host's shot never landed", 10f);
            int before = FieldEffects.Scorches;
            Vector2 toDummy = Arena.DummyStart(0) - host.Position;
            host.ServerFire(Mathf.Atan2(toDummy.y, toDummy.x) * Mathf.Rad2Deg, 1f);
            yield return WaitUntil(() => FieldEffects.Scorches > before, () => "no scorch mark was left: the shot ended " + host.LastVolley?.Main.End, 30f);
            yield return new WaitForSeconds(0.5f);
            Assert.That(FieldEffects.Scorches, Is.GreaterThan(before), "a scorch mark stays for the round");
            Assert.That(arena.RerollLevel(), Is.True);
            yield return WaitUntil(() => FieldEffects.Scorches == 0, "the new level never swept the scorch marks away", 5f);
        }

        [UnityTest]
        public IEnumerator ACrateSmokesAsItFalls_LandsWithAFirework_RingsWhenTaken_AndBurstsWhenLeft()
        {
            Arena arena = null;
            Player host = null, ann = null;
            yield return Begin(a => arena = a, (h, c) => { host = h; ann = c; });
            Assert.That(arena.StartMatch(TestMatches.OnThePracticeRange()), Is.True);
            yield return WaitUntil(() => Turns.For(Client) != null, "the client never got the match");

            int smoke = GmlParticles.Thrown(GmEffect.SmokeUp);
            int fireworks = GmlParticles.Thrown(GmEffect.Firework);
            Drop drop = arena.DropCrate(DropKind.Weapon, new Vector2(0f, -2f)); // on the host's shot straight up
            Crate crate = null;
            yield return WaitUntil(() => Client.Objects.TryGet(Crate.KeyFor(drop.Id), out crate) && crate != null, "the client never saw the crate");
            yield return WaitUntil(() => GmlParticles.Thrown(GmEffect.SmokeUp) > smoke, "it never smoked as it fell", 5f);
            yield return WaitUntil(() => crate.LandedHere, "her client never saw it land", 5f);
            Assert.That(GmlParticles.Thrown(GmEffect.Firework), Is.GreaterThan(fireworks), "its landing firework");

            int rings = GmlParticles.Thrown(GmEffect.Ring);
            host.ServerFire(90f, 0f);
            yield return WaitUntil(() => GmlParticles.Thrown(GmEffect.Ring) > rings, "taking it never rang", 10f);
            yield return WaitUntil(() => FloatText.Live.Any(f => f.Text.EndsWith(" x 1 collected")), "what it gave never floated", 10f);

            Arena.DropRules.MinLife = 0.3;
            Arena.DropRules.MaxLife = 0.3;
            int scorches = FieldEffects.Scorches;
            Drop left = arena.DropCrate(DropKind.Health, new Vector2(5f, 2f));
            Assert.That(left, Is.Not.Null);
            yield return WaitUntil(() => FieldEffects.Scorches > scorches, "it never burst", 10f);
            Assert.That(arena.CratesOnField, Is.Empty);
        }

        private Player Mine() => Player.In(Client).First(p => p.OwnerClientId == Client.LocalClientId);

        // Starts both sessions, serves the arena, and waits for both players to have their seats.
        private IEnumerator Begin(System.Action<Arena> arena, System.Action<Player, Player> players)
        {
            yield return StartAndJoinAll();
            arena(Arena.Serve(Server, GamePrefabs.Dummy));
            var host = WaitForSpawn<Player>(Server, owner: Server.LocalClientId);
            yield return host;
            var ann = WaitForSpawn<Player>(Server, owner: Client.LocalClientId);
            yield return ann;
            yield return WaitUntil(() => Player.In(Client).Count() == 2, "the client never saw both players");
            players(host.Result, ann.Result);
        }
    }
}
