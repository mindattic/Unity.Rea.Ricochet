// U.RICO-T79: the items in flight on a client watching the host (a teleporter turned to the aim with teleport_player's rings
// where it sets off and lands, a shrink ray's lime sparks with no ball drawn, a redirector growing in flight while the
// standing one waits for it), and the endgame room's camera flashes, none on the pedestal. The step rules themselves are
// tested without Unity: dotnet test Core.slnx (ItemLooksTests, ShotWireTests, CommandTests).
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Ricochet.Core.Arsenal;
using Ricochet.Core.Art;
using Ricochet.Core.Level;
using Unity.Multiplayer;
using Unity.Multiplayer.Core;
using Unity.Multiplayer.Testing;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ricochet.Tests
{
    public class ItemFlightTests : NetSessionTest
    {
        public ItemFlightTests() : base(Topology.Host, clients: 1) { }

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

        [TearDown]
        public void SweepUp()
        {
            ItemFlights.Clear();
            GmlParticles.Clear();
        }

        [UnityTest]
        public IEnumerator EachItemThatFlies_IsDrawnInFlightOnEveryPeer_AsTheOriginalDrewIt()
        {
            Player host = null, ann = null;
            yield return Begin((h, c) => { host = h; ann = c; });
            Player annOnHerClient = Mine();
            yield return WaitUntil(() => annOnHerClient.Position != Vector2.zero && annOnHerClient.Items != 0, "her client never got her seat");

            // The teleporter: spr_teleporter at the aim, a ring where she stood and rings where she lands.
            yield return Select(annOnHerClient, ann, Item.Teleporter);
            int rings = GmlParticles.Thrown(GmEffect.Ring);
            int fireworks = GmlParticles.Thrown(GmEffect.Firework);
            annOnHerClient.UseItem(90f, 0.5f);
            yield return WaitUntil(() => ItemFlights.Count(Item.Teleporter) >= 2, "the teleporter never flew on both peers", 5f);
            SpriteRenderer teleporter = ItemFlights.RendererOf(Item.Teleporter);
            Assert.That(teleporter, Is.Not.Null);
            Assert.That(teleporter.sprite.name, Does.StartWith(ItemFlights.TeleporterSprite));
            Assert.That(Mathf.DeltaAngle(teleporter.transform.eulerAngles.z, 90f), Is.EqualTo(0f).Within(0.01f), "image_angle set to the aim once");
            Assert.That(GmlParticles.Thrown(GmEffect.Ring), Is.GreaterThan(rings), "teleport_player's ring where she stood");
            yield return WaitUntil(() => ItemFlights.Count(Item.Teleporter) == 0, "the teleporter never landed", 5f);
            Assert.That(GmlParticles.Thrown(GmEffect.Ring), Is.GreaterThanOrEqualTo(rings + 6), "a ring as it left and two as she landed, on each peer");
            Assert.That(GmlParticles.Thrown(GmEffect.Firework), Is.GreaterThan(fireworks), "and the firework");

            // The shrink ray at the host: lime sparks along its way, its ball never drawn, a lime firework on the host.
            yield return Select(annOnHerClient, ann, Item.ShrinkRay);
            Vector2 toHost = host.Position - ann.Position;
            int sparks = GmlParticles.Thrown(GmEffect.Spark);
            annOnHerClient.UseItem(Mathf.Atan2(toHost.y, toHost.x) * Mathf.Rad2Deg, 1f);
            yield return WaitUntil(() => ItemFlights.Count(Item.ShrinkRay) > 0, "the shrink ray never flew", 5f);
            Assert.That(ItemFlights.RendererOf(Item.ShrinkRay), Is.Null, "obj_shrinkray's Draw is commented out");
            yield return WaitUntil(() => ItemFlights.Count(Item.ShrinkRay) == 0, "the shrink ray never landed", 5f);
            Assert.That(GmlParticles.Thrown(GmEffect.Spark), Is.GreaterThan(sparks), "an ef_spark every step");

            // Wherever it strikes (a shape, or here the border of an open level) it bursts in a lime firework (U.RICO-T90).
            Arsenal.ShrinkRay(Ricochet.Core.Aim.Vec2.Zero, 0, new Level(-10, -5, 10, 5), Arena.Rules, out Ricochet.Core.Aim.ShotPath struck);
            fireworks = GmlParticles.Thrown(GmEffect.Firework);
            ItemFlights.Launch(null, Item.ShrinkRay, struck);
            yield return WaitUntil(() => ItemFlights.Count(Item.ShrinkRay) == 0, "that shrink ray never landed", 5f);
            Assert.That(GmlParticles.Thrown(GmEffect.Firework), Is.EqualTo(fireworks + 1), "its firework where it struck");

            // The redirector: growing from 0.15 in flight until she presses again (U.RICO-T80); the standing one shows only
            // once it stops.
            yield return Select(annOnHerClient, ann, Item.Redirector);
            annOnHerClient.UseItem(0f, 1f);
            yield return WaitUntil(() => ItemFlights.Count(Item.Redirector) > 0, "the redirector never flew", 5f);
            SpriteRenderer arrow = ItemFlights.RendererOf(Item.Redirector);
            Assert.That(arrow, Is.Not.Null);
            Assert.That(arrow.transform.localScale.x, Is.InRange(0.15f, 0.3f));
            LevelHazard standing = LevelHazard.In(Server).FirstOrDefault(h => h.HazardKind == HazardKind.Redirector);
            if (standing != null)
            {
                Assert.That(standing.Shown, Is.False, "it stands only where its flight stops");
            }

            Assert.That(LevelHazard.In(Server).Any(h => h.HazardKind == HazardKind.Redirector), Is.False, "nothing stands while it flies");
            annOnHerClient.UseItem(0f, 1f); // redirector_detonate
            yield return WaitUntil(() => ItemFlights.Count(Item.Redirector) == 0, "the redirector never stopped", 5f);
            yield return WaitUntil(() => LevelHazard.In(Server).Any(h => h.HazardKind == HazardKind.Redirector), "it never stood where it stopped", 5f);
            yield return null;
            Assert.That(LevelHazard.In(Server).Single(h => h.HazardKind == HazardKind.Redirector).Shown, Is.True);
        }

        [UnityTest]
        public IEnumerator TheEndgameFlashes_GoOffEveryStep_NoneOnThePedestal_AndStopWithTheRoom()
        {
            var flashes = new GameObject("Flashes under test").AddComponent<EndgameFlashes>();
            try
            {
                flashes.Begin();
                Assert.That(flashes.Canvas.sortingOrder, Is.GreaterThan(90), "over the endgame recap");
                yield return new WaitForSeconds(0.5f);
                Assert.That(flashes.Thrown, Is.GreaterThan(10), "one a step");
                Assert.That(flashes.Showing, Is.GreaterThan(0));
                Assert.That(flashes.Spots.All(s => ItemLooks.FlashKept(s.x, s.y)), Is.True, "none on the pedestal");
                Assert.That(flashes.Showing, Is.LessThanOrEqualTo(8), "each gone after spr_flash's six frames");

                flashes.End();
                Assert.That(flashes.Showing, Is.EqualTo(0));
                int thrown = flashes.Thrown;
                yield return null;
                yield return null;
                Assert.That(flashes.Thrown, Is.EqualTo(thrown), "the emitter stops with the room");
            }
            finally
            {
                Object.Destroy(flashes.gameObject);
            }
        }

        private IEnumerator Select(Player onClient, Player onServer, Item item)
        {
            onClient.SelectItem((int)item);
            yield return WaitUntil(() => onServer.Loadout.Item == item, item + " was never selected");
        }

        private Player Mine() => Player.In(Client).First(p => p.OwnerClientId == Client.LocalClientId);

        private IEnumerator Begin(System.Action<Player, Player> players)
        {
            yield return StartAndJoinAll();
            Arena.Serve(Server, GamePrefabs.Dummy);
            var host = WaitForSpawn<Player>(Server, owner: Server.LocalClientId);
            yield return host;
            var ann = WaitForSpawn<Player>(Server, owner: Client.LocalClientId);
            yield return ann;
            yield return WaitUntil(() => Player.In(Client).Count() == 2, "the client never saw both players");
            players(host.Result, ann.Result);
        }
    }
}
