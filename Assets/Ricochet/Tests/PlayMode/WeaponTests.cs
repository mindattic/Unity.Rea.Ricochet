// Weapons, items and crates on the wire (UR-T5): a client cycles its weapon and item and sets a grenade's fuse through
// [ToServer] calls and sees the server's loadout come back; items take effect on the server (an extra bounce, a
// teleport); in a match a grenade spends one, its blast hurts whoever is near it on every peer, cycling out of turn is
// refused, and a crate a shot passes over is collected for the shooter. The loadout, volley and drop rules themselves
// are tested without Unity: dotnet test Core.slnx (LoadoutTests, VolleyTests, DropTests).
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Ricochet.Core.Aim;
using Ricochet.Core.Arsenal;
using Ricochet.Core.Level;
using Ricochet.Core.Match;
using Unity.Multiplayer;
using Unity.Multiplayer.Core;
using Unity.Multiplayer.Testing;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ricochet.Tests
{
    public class WeaponTests : NetSessionTest
    {
        private double _grenadeRadius;
        private double _endTurnSeconds;

        public WeaponTests() : base(Topology.Host, clients: 1) { }

        protected override void OnSessionsCreated()
        {
            SetPlayerPrefab(GamePrefabs.Player);
            RegisterPrefab(GamePrefabs.Dummy);
            RegisterPrefab(GamePrefabs.Opponent);
            RegisterPrefab(GamePrefabs.Turns);
            RegisterPrefab(GamePrefabs.Crate);
            Client.Identity = new PlayerIdentity("ann-token", "Ann", new PlayerColor(230, 80, 60));
        }

        [SetUp]
        public void KeepTheRules()
        {
            _grenadeRadius = Arena.ArsenalRules.GrenadeRadius;
            _endTurnSeconds = Arena.MatchRules.EndTurnSeconds;
            TestMatches.WithoutReadyCard();
        }

        [TearDown]
        public void PutTheRulesBack()
        {
            Arena.ArsenalRules.GrenadeRadius = _grenadeRadius;
            Arena.MatchRules.EndTurnSeconds = _endTurnSeconds;
            TestMatches.StockReadyCard();
        }

        [UnityTest]
        public IEnumerator AClientCyclesItsWeaponAndSetsTheFuse_AndSeesTheServersLoadoutComeBack()
        {
            Player host = null, ann = null;
            yield return Begin(_ => { }, (h, c) => { host = h; ann = c; });
            Player annOnHerClient = Mine();
            yield return WaitUntil(() => annOnHerClient.Weapons != 0, "her client never got her loadout");
            Assert.That(annOnHerClient.Loadout.Weapon, Is.EqualTo(Weapon.Cannon));
            Assert.That(annOnHerClient.Loadout.Count(Weapon.Grenade), Is.EqualTo(3));

            annOnHerClient.CycleWeapon(1);
            yield return WaitUntil(() => annOnHerClient.Loadout.Weapon == Weapon.Laser, () => "she still has the " + annOnHerClient.Loadout.Weapon);
            Assert.That(ann.Loadout.Weapon, Is.EqualTo(Weapon.Laser), "the server's loadout is the real one");

            annOnHerClient.CycleWeapon(1);
            annOnHerClient.CycleWeapon(1);
            yield return WaitUntil(() => annOnHerClient.Loadout.Weapon == Weapon.Grenade, () => "she has the " + annOnHerClient.Loadout.Weapon);
            annOnHerClient.Fuse(2);
            yield return WaitUntil(() => annOnHerClient.Loadout.Fuse == 6, () => "her fuse is " + annOnHerClient.Loadout.Fuse);
            annOnHerClient.Fuse(-1);
            yield return WaitUntil(() => annOnHerClient.Loadout.Fuse == 5.5, () => "her fuse is " + annOnHerClient.Loadout.Fuse);

            annOnHerClient.CycleItem(-1);                         // left from the extra bounce wraps to the redirector (UR-T6)
            yield return WaitUntil(() => annOnHerClient.Loadout.Item == Item.Redirector, () => "her item is " + annOnHerClient.Loadout.Item);
            annOnHerClient.CycleItem(-1);
            yield return WaitUntil(() => annOnHerClient.Loadout.Item == Item.AimAssist, () => "her item is " + annOnHerClient.Loadout.Item);
            annOnHerClient.CycleItem(-1);
            yield return WaitUntil(() => annOnHerClient.Loadout.Item == Item.Teleporter, () => "her item is " + annOnHerClient.Loadout.Item);
        }

        [UnityTest]
        public IEnumerator AnExtraBounceAndATeleportTakeEffectOnTheServer()
        {
            Player host = null, ann = null;
            yield return Begin(_ => { }, (h, c) => { host = h; ann = c; });
            Player annOnHerClient = Mine();
            yield return WaitUntil(() => annOnHerClient.Position != Vector2.zero && annOnHerClient.Weapons != 0, "her client never got her seat");

            annOnHerClient.UseItem(90f, 0.5f);
            yield return WaitUntil(() => annOnHerClient.ExtraBounces == 1, "the extra bounce never came back");
            Assert.That(ann.Loadout.Count(Item.ExtraBounce), Is.EqualTo(3), "practice is free: nothing spent");

            annOnHerClient.CycleItem(-1);                         // the redirector, then (aim assist, none, skipped) the teleporter
            yield return WaitUntil(() => ann.Loadout.Item == Item.Redirector, "the redirector was never selected");
            annOnHerClient.CycleItem(-1);
            yield return WaitUntil(() => ann.Loadout.Item == Item.AimAssist, "aim assist was never selected");
            annOnHerClient.CycleItem(-1);
            yield return WaitUntil(() => ann.Loadout.Item == Item.Teleporter, "the teleporter was never selected");
            Vector2 from = ann.Position;
            Vec2 expected = Arsenal.Teleport(Arena.ToCore(from), 0, Arena.Level, Arena.Rules, ann.Radius, out _);
            annOnHerClient.UseItem(0f, 0f);
            yield return WaitUntil(() => annOnHerClient.Position != from, "she never moved"); // when the teleporter arrives (U.RICO-T80)
            Assert.That(annOnHerClient.Position.x, Is.EqualTo((float)expected.X).Within(1e-4f));
            Assert.That(annOnHerClient.Position.y, Is.EqualTo((float)expected.Y).Within(1e-4f));

            annOnHerClient.Fire(90f, 0.5f);
            yield return WaitUntil(() => annOnHerClient.LastVolley != null, "her client never drew the shot");
            Assert.That(annOnHerClient.ShotExtraBounces, Is.EqualTo(1), "the shot carried the extra bounce");
            yield return WaitUntil(() => annOnHerClient.ExtraBounces == 0, "the extra bounce was never used up");
        }

        [UnityTest]
        public IEnumerator InAMatchAGrenadeSpendsOne_AndItsBlastHurtsWhoeverIsNearIt()
        {
            Arena.ArsenalRules.GrenadeRadius = 6;                 // seat 0 at x 0, seat 1 at x -4: a half-second fuse left
            Arena arena = null;                                   // goes off at x -9.6, 5.6 from Ann and 9.6 from the host
            Player host = null, ann = null;
            yield return Begin(a => arena = a, (h, c) => { host = h; ann = c; });
            Assert.That(arena.StartMatch(TestMatches.OnThePracticeRange()), Is.True);
            Player annOnHerClient = Mine();
            yield return WaitUntil(() => Turns.For(Client) != null && annOnHerClient.Seat == ann.Seat, "her client never got the match");
            Assert.That(Turns.For(Server).Current, Is.EqualTo(host.Seat));

            annOnHerClient.CycleWeapon(1);                        // not her turn
            yield return new WaitForSeconds(0.3f);
            Assert.That(ann.Loadout.Weapon, Is.EqualTo(Weapon.Cannon), "the server let her cycle out of turn");

            host.ServerCycleWeapon(-1);                           // the vortex, the railgun, the grenade
            host.ServerCycleWeapon(-1);
            host.ServerCycleWeapon(-1);
            Assert.That(host.Loadout.Weapon, Is.EqualTo(Weapon.Grenade));
            host.Loadout.SetFuse(0.5);
            host.MirrorLoadout();
            host.ServerFire(180f, 1f);
            Assert.That(host.Shots, Is.EqualTo(1));
            Assert.That(host.Loadout.Count(Weapon.Grenade), Is.EqualTo(2), "a grenade spent");
            Assert.That(host.LastVolley.HasBlast, Is.True);
            Assert.That(host.LastVolley.Strikes.Select(s => s.TargetId), Is.EqualTo(new[] { Arena.SeatTarget(ann.Seat) }));

            yield return WaitUntil(() => annOnHerClient.Health < Arena.MatchRules.MaxHealth, "her client never saw the blast hurt her", 10f);
            Player hostOnClient = Player.In(Client).First(p => p.Seat == host.Seat);
            Assert.That(hostOnClient.Health, Is.EqualTo(Arena.MatchRules.MaxHealth), "the thrower was out of the blast");
            Assert.That(hostOnClient.LastVolley.Weapon, Is.EqualTo(Weapon.Grenade), "her client flew the grenade too");
            Assert.That(hostOnClient.Loadout.Count(Weapon.Grenade), Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator ACrateAShotPassesOverIsCollectedForTheShooter()
        {
            Arena arena = null;
            Player host = null, ann = null;
            yield return Begin(a => arena = a, (h, c) => { host = h; ann = c; });
            Assert.That(arena.StartMatch(TestMatches.OnThePracticeRange()), Is.True);
            yield return WaitUntil(() => Turns.For(Client) != null, "the client never got the match");

            // Straight up from seat 0 at (0, -4.5): the shot passes (0, -2) on its way to the middle wall.
            Drop drop = arena.DropCrate(DropKind.Item, new Vector2(0f, -2f));
            Assert.That(drop, Is.Not.Null);
            Crate crate = null;
            yield return WaitUntil(() => Client.Objects.TryGet(Crate.KeyFor(drop.Id), out crate) && crate != null, "the client never saw the crate");
            yield return WaitUntil(() => crate.Landed, "the crate never landed", 5f);
            Assert.That(crate.DropKind, Is.EqualTo(DropKind.Item));
            Assert.That(crate.Spot, Is.EqualTo(new Vector2(0f, -2f)));

            int itemsBefore = Enumerable.Range(0, ArsenalRules.Kinds).Sum(i => host.Loadout.Count((Item)i));
            host.ServerFire(90f, 0f);
            Player hostOnClient = Player.In(Client).First(p => p.Seat == host.Seat);
            yield return WaitUntil(() => hostOnClient.PickupCount == 1, "her client never saw the host collect it", 10f);
            Assert.That(Client.Objects.Contains(Crate.KeyFor(drop.Id)), Is.False, "the crate is gone");
            Assert.That(Enumerable.Range(0, ArsenalRules.Kinds).Sum(i => hostOnClient.Loadout.Count((Item)i)), Is.EqualTo(itemsBefore + 1));
            Assert.That(Player.PickupText(hostOnClient.LastPickup), Does.EndWith(" x 1 collected"));
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
