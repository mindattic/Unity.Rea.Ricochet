// U.RICO-T47: the weapons drawn as the original drew them, on a client watching the host shoot: the laser's beam and the
// pulses riding it, running out once spent; the railgun's rail and swirls fading at railFade; a vortex set off growing,
// waiting and blowing; the grenade's circle and the hit flash on whoever it struck; the owner's preview in draw_beam's
// dots. The step rules themselves are tested without Unity: dotnet test Core.slnx (WeaponLooksTests).
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
    public class WeaponLookTests : NetSessionTest
    {
        private double _grenadeRadius;

        public WeaponLookTests() : base(Topology.Host, clients: 1) { }

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
            TestMatches.WithoutReadyCard();
        }

        [TearDown]
        public void PutTheRulesBack()
        {
            Arena.ArsenalRules.GrenadeRadius = _grenadeRadius;
            TestMatches.StockReadyCard();
        }

        [UnityTest]
        public IEnumerator TheLaserIsABeamWithPulsesThatRunsOut_AndTheRailFadesWithItsSwirls()
        {
            Player host = null;
            yield return Begin(_ => { }, (h, c) => host = h);
            Player annOnHerClient = Mine();
            yield return WaitUntil(() => annOnHerClient.PreviewBeam != null && annOnHerClient.PreviewBeam.DashCount > 0, "her preview was never drawn");
            Assert.That(annOnHerClient.PreviewBeam.Alpha, Is.EqualTo(1f), "draw_beam at full alpha");

            host.ServerCycleWeapon(1);
            Assert.That(host.Loadout.Weapon, Is.EqualTo(Weapon.Laser));
            host.MirrorLoadout();
            host.ServerFire(80f, 1f);
            ShotView laser = null;
            yield return WaitUntil(() => (laser = HostViewOnClient(host)) != null && laser.Flying && laser.LaserShowing, "her client never drew the beam");
            Assert.That(laser.ProjectileScale, Is.GreaterThan(0f));
            yield return WaitUntil(() => laser.LaserPulses > 0 || !laser.Flying, "no pulse was ever sent along the beam", 10f);
            yield return WaitUntil(() => !laser.Flying, "the laser never landed", 10f);
            yield return WaitUntil(() => !laser.LaserShowing, "the beam never ran out", 10f);

            host.ServerCycleWeapon(-1);                         // back to the cannon, then the vortex, the railgun
            host.ServerCycleWeapon(-1);
            host.ServerCycleWeapon(-1);
            Assert.That(host.Loadout.Weapon, Is.EqualTo(Weapon.Railgun));
            host.MirrorLoadout();
            host.ServerFire(80f, 1f);
            yield return WaitUntil(() => laser.RailShowing && laser.RailSwirls > 2, "her client never drew the rail and its swirls", 5f);
            float alpha = laser.RailAlpha;
            Assert.That(alpha, Is.LessThanOrEqualTo((float)WeaponLooks.RailStartAlpha));
            yield return new WaitForSeconds(0.5f);
            Assert.That(laser.RailAlpha, Is.LessThan(alpha), "the rail never faded");
            yield return WaitUntil(() => !laser.RailShowing, "the rail never went", 6f);
        }

        [UnityTest]
        public IEnumerator AVortexSetOffImplodes_AndGoesAway()
        {
            Player host = null;
            yield return Begin(_ => { }, (h, c) => host = h);
            host.ServerCycleWeapon(-1);
            Assert.That(host.Loadout.Weapon, Is.EqualTo(Weapon.Vortex));
            host.MirrorLoadout();
            host.ServerFire(80f, 1f);
            ShotView view = null;
            yield return WaitUntil(() => (view = HostViewOnClient(host)) != null && view.Flying, "her client never drew the vortex");
            yield return new WaitForSeconds(0.2f);
            Assert.That(view.ProjectileScale, Is.GreaterThan((float)Vortex.FlyingStartScale), "the vortex never grew in flight");
            host.ServerDetonate();
            yield return WaitUntil(() => view.Imploding, "it never imploded on her client", 5f);
            yield return WaitUntil(() => view.ImplosionMode == VortexMode.Blowing || !view.Imploding, "it never blew", 10f);
            yield return WaitUntil(() => !view.Imploding, "the implosion never ended", 10f);
        }

        [UnityTest]
        public IEnumerator AGrenadesCircleGrowsToItsBlast_AndWhoeverItHurtsFlashes()
        {
            Arena.ArsenalRules.GrenadeRadius = 6;                 // seat 0 at x 0, seat 1 at x -4 (WeaponTests' grenade)
            Arena arena = null;
            Player host = null, ann = null;
            yield return Begin(a => arena = a, (h, c) => { host = h; ann = c; });
            Assert.That(arena.StartMatch(TestMatches.OnThePracticeRange()), Is.True);
            Player annOnHerClient = Mine();
            yield return WaitUntil(() => Turns.For(Client) != null && annOnHerClient.Seat == ann.Seat, "her client never got the match");

            host.ServerCycleWeapon(-1);
            host.ServerCycleWeapon(-1);
            host.ServerCycleWeapon(-1);
            host.Loadout.SetFuse(0.5);
            host.MirrorLoadout();
            host.ServerFire(180f, 1f);
            ShotView view = null;
            yield return WaitUntil(() => (view = HostViewOnClient(host)) != null && view.Flight != null && view.Flight.Volley.Weapon == Weapon.Grenade, "her client never threw it");
            Player hostOnClient = Player.In(Client).First(p => p.Seat == host.Seat);
            yield return WaitUntil(() => hostOnClient.FuseLeft >= 0 || !view.Flying, "the fuse never counted down", 5f);
            yield return WaitUntil(() => view.BlastShowing, "the circle never showed", 10f);
            Assert.That(SpriteBurst.Playing, Is.GreaterThan(0), "obj_boom_normal never went off on Ann");
            float radius = view.BlastRadius;
            yield return new WaitForSeconds(0.2f);
            Assert.That(view.BlastRadius, Is.GreaterThan(radius), "the circle never grew");
            yield return WaitUntil(() => !view.BlastShowing, "the circle never reached the blast radius", 10f);
        }

        // The host's shots as her client draws them.
        private ShotView HostViewOnClient(Player host)
        {
            Player hostOnClient = Player.In(Client).FirstOrDefault(p => p.Seat == host.Seat);
            return Object.FindObjectsByType<ShotView>().FirstOrDefault(v => v.Owner == hostOnClient);
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
