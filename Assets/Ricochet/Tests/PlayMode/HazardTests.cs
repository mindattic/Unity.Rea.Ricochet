// Level hazards on the wire (UR-T6): a shield wall a client stands up stops the host's shot and shatters on every peer
// while its owner's own shot passes; a redirector a client sets down reaches the host; a vortex set off in mid-flight
// goes off where it was set off on every peer, swallows a shield wall in reach and breaches the border, and a later shot
// flies out through the breach; and a laid level's electric arc stops a shot while its laser gate switches every turn.
// The hazard rules themselves are tested without Unity: dotnet test Core.slnx (HazardTests).
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Ricochet.Core;
using Unity.Multiplayer;
using Unity.Multiplayer.Core;
using Unity.Multiplayer.Testing;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ricochet.Tests
{
    public class HazardTests : NetSessionTest
    {
        private double _endTurnSeconds;

        public HazardTests() : base(Topology.Host, clients: 1) { }

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
            _endTurnSeconds = Arena.MatchRules.EndTurnSeconds;
        }

        [TearDown]
        public void PutTheRulesBack()
        {
            Arena.MatchRules.EndTurnSeconds = _endTurnSeconds;
        }

        [UnityTest]
        public IEnumerator AShieldWallStopsAnotherPlayersShot_AndShattersOnEveryPeer_WhileItsOwnersShotPasses()
        {
            Player host = null, ann = null;
            yield return Begin(false, _ => { }, (h, c) => { host = h; ann = c; });
            Player annOnHerClient = Mine();
            yield return WaitUntil(() => annOnHerClient.Position != Vector2.zero && annOnHerClient.Weapons != 0, "her client never got her seat");

            // Ann (seat 1, at x -4) faces the host (seat 0, at x 0) and stands her wall up: a unit in front, across her aim.
            annOnHerClient.CycleItem(1);
            yield return WaitUntil(() => ann.Loadout.Item == Item.ShieldWall, "the shield wall was never selected");
            annOnHerClient.UseItem(0f, 0.5f);
            LevelHazard wallOnClient = null;
            yield return WaitUntil(() => (wallOnClient = LevelHazard.In(Client).FirstOrDefault(h => h.Ready && h.HazardKind == HazardKind.ShieldWall)) != null,
                "her client never saw her wall");
            Assert.That(wallOnClient.OwnerSeat, Is.EqualTo(ann.Seat));
            Assert.That(wallOnClient.A.x, Is.EqualTo(-3f).Within(1e-4f));
            Assert.That(ann.Loadout.Count(Item.ShieldWall), Is.EqualTo(3), "practice is free: nothing spent");

            // Her own shot passes through it.
            annOnHerClient.Fire(0f, 1f);
            yield return WaitUntil(() => ann.LastVolley != null, "the server never took her shot");
            Assert.That(ann.LastVolley.Main.End, Is.Not.EqualTo(ShotEndKind.Absorbed));
            yield return WaitUntil(() => !ann.ShotInFlight, "her shot never landed", 10f);

            // The host's shot back at her stops in it, and the wall shatters everywhere.
            host.ServerFire(180f, 1f);
            Assert.That(host.LastVolley.Main.End, Is.EqualTo(ShotEndKind.Absorbed));
            Assert.That(host.LastVolley.Main.Impact.X, Is.EqualTo(-3).Within(1e-6));
            Player hostOnClient = Player.In(Client).First(p => p.Seat == host.Seat);
            yield return WaitUntil(() => hostOnClient.LastVolley != null, "her client never drew the host's shot");
            Assert.That(hostOnClient.LastVolley.Main.End, Is.EqualTo(ShotEndKind.Absorbed), "her client flew it into the wall too");
            yield return WaitUntil(() => !LevelHazard.In(Client).Any(), "the wall never shattered on her client", 10f);
            Assert.That(Arena.Current.HazardsOnField, Is.Empty);

            // With it gone the same shot flies on.
            yield return WaitUntil(() => !host.ShotInFlight, "the host's shot never landed");
            host.ServerFire(180f, 1f);
            Assert.That(host.LastVolley.Main.End, Is.Not.EqualTo(ShotEndKind.Absorbed));
        }

        [UnityTest]
        public IEnumerator ARedirectorAClientSetsDownSendsShotsOnTheWayItFlew()
        {
            Player host = null, ann = null;
            yield return Begin(false, _ => { }, (h, c) => { host = h; ann = c; });
            Player annOnHerClient = Mine();
            yield return WaitUntil(() => annOnHerClient.Position != Vector2.zero && annOnHerClient.Weapons != 0, "her client never got her seat");

            annOnHerClient.CycleItem(-1);
            yield return WaitUntil(() => ann.Loadout.Item == Item.Redirector, "the redirector was never selected");
            annOnHerClient.UseItem(0f, 0f); // flies right, as short as it goes: down at x -4 + 0.6 + 6
            LevelHazard placed = null;
            yield return WaitUntil(() => (placed = LevelHazard.In(Client).FirstOrDefault(h => h.Ready && h.HazardKind == HazardKind.Redirector)) != null,
                "her client never saw the redirector");
            Assert.That(placed.Heading, Is.EqualTo(0f).Within(1e-3f));
            Assert.That((placed.A.x + placed.B.x) / 2f, Is.EqualTo(2.6f).Within(1e-3f));

            // The host, below and left of it, fires up and right into it: it leaves heading right.
            Vec2 from = Arena.ToCore(host.Position);
            double heading = System.Math.Atan2(-4.5 - from.Y + 0.3, 2.6 - from.X) * 180 / System.Math.PI;
            host.ServerFire((float)heading, 1f);
            Assert.That(host.LastVolley.Main.Contacts.Count, Is.GreaterThan(0));
            Assert.That(host.LastVolley.Main.Contacts[0].Kind, Is.EqualTo(ContactKind.Redirector));
            Assert.That(host.LastVolley.Main.Contacts[0].HeadingOut, Is.EqualTo(0).Within(1e-6));
        }

        [UnityTest]
        public IEnumerator AVortexSetOffInMidFlightGoesOffThere_SwallowsAWall_AndBreachesTheBorderAShotThenFliesOutThrough()
        {
            Player host = null, ann = null;
            yield return Begin(false, _ => { }, (h, c) => { host = h; ann = c; });
            Player annOnHerClient = Mine();
            yield return WaitUntil(() => annOnHerClient.Position != Vector2.zero && annOnHerClient.Weapons != 0, "her client never got her seat");

            // Ann's wall, a unit above her (x -4): within the vortex's reach when it goes off to her left.
            annOnHerClient.CycleItem(1);
            yield return WaitUntil(() => ann.Loadout.Item == Item.ShieldWall, "the shield wall was never selected");
            annOnHerClient.UseItem(90f, 0.5f);
            yield return WaitUntil(() => LevelHazard.In(Client).Any(h => h.Ready && h.HazardKind == HazardKind.ShieldWall), "her client never saw her wall");

            // The host fires the vortex left along the bottom and sets it off on its way.
            host.ServerCycleWeapon(-1);
            Assert.That(host.Loadout.Weapon, Is.EqualTo(Weapon.Vortex));
            host.ServerFire(180f, 1f);
            Assert.That(host.LastVolley.Weapon, Is.EqualTo(Weapon.Vortex));
            Player hostOnClient = Player.In(Client).First(p => p.Seat == host.Seat);
            yield return WaitUntil(() => hostOnClient.LastVolley != null && hostOnClient.ShotInFlight, "her client never drew the vortex");
            yield return WaitUntil(() => host.ShotTravelled >= 4.5 || !host.ShotInFlight, "the vortex never flew", 5f);
            Assert.That(host.ShotInFlight, Is.True, "the vortex landed before it could be set off");
            host.ServerDetonate();

            Assert.That(host.ShotDetonation, Is.GreaterThan(0f));
            Assert.That(host.ShotInFlight, Is.False, "it went off at once");
            Volley onServer = host.LastVolley;
            Assert.That(onServer.Main.End, Is.EqualTo(ShotEndKind.Detonated));
            Assert.That(onServer.Main.Length, Is.EqualTo(host.ShotDetonation).Within(1e-4));
            Vec2 center = onServer.BlastCenter;
            Assert.That(center.X, Is.LessThan(-3).And.GreaterThan(-9), "it went off on its way, short of the far side");

            yield return WaitUntil(() => hostOnClient.LastVolley.Main.End == ShotEndKind.Detonated, "her client never cut its vortex short", 5f);
            Assert.That(hostOnClient.LastVolley.BlastCenter.X, Is.EqualTo(center.X).Within(1e-3));

            // The wall in reach is swallowed and the bottom border breached on every peer.
            Assert.That(Arena.Current.HazardsOnField.Any(h => h.Kind == HazardKind.ShieldWall), Is.False, "the wall was not swallowed");
            Hazard breach = Arena.Current.HazardsOnField.Single(h => h.Kind == HazardKind.Breach);
            Assert.That(breach.A.Y, Is.EqualTo(Arena.Level.MinY).Within(1e-6));
            yield return WaitUntil(() => LevelHazard.In(Client).Count() == 1 && LevelHazard.In(Client).Single().Ready && LevelHazard.In(Client).Single().HazardKind == HazardKind.Breach,
                () => "her client sees " + string.Join(", ", LevelHazard.In(Client).Select(h => h.HazardKind)), 5f);

            // Ann, standing over the breach, fires straight down: her shot flies out of the level.
            Assert.That(ann.Position.x, Is.InRange((float)System.Math.Min(breach.A.X, breach.B.X), (float)System.Math.Max(breach.A.X, breach.B.X)));
            annOnHerClient.Fire(270f, 1f);
            yield return WaitUntil(() => ann.LastVolley != null, "the server never took her shot");
            Assert.That(ann.LastVolley.Main.End, Is.EqualTo(ShotEndKind.Escaped));
            yield return WaitUntil(() => annOnHerClient.LastVolley != null, "her client never drew her shot");
            Assert.That(annOnHerClient.LastVolley.Main.End, Is.EqualTo(ShotEndKind.Escaped), "her client saw it fly out too");
        }

        [UnityTest]
        public IEnumerator ALaidLevelsArcStopsAShot_AndItsLaserGateSwitchesEveryTurn()
        {
            Arena.MatchRules.EndTurnSeconds = 0.2;
            Arena arena = null;
            Player host = null, ann = null;
            yield return Begin(true, a => arena = a, (h, c) => { host = h; ann = c; });
            Assert.That(arena.LaysHazards, Is.True);
            Assert.That(arena.StartMatch(0), Is.True);
            yield return WaitUntil(() => Turns.For(Client) != null, "the client never got the match");

            Hazard arc = arena.HazardsOnField.Single(h => h.Kind == HazardKind.Electricity);
            Assert.That(arena.HazardsOnField.Count(h => h.Kind == HazardKind.Laser), Is.EqualTo(1));
            yield return WaitUntil(() => new System.Collections.Generic.HashSet<int>(LevelHazard.In(Client).Where(h => h.Ready).Select(h => h.HazardId))
                .SetEquals(arena.HazardsOnField.Select(h => h.Id)), "her client never saw the level's hazards", 5f);
            LevelHazard gateOnClient = LevelHazard.In(Client).Single(h => h.Ready && h.HazardKind == HazardKind.Laser);
            Hazard gate = arena.HazardsOnField.Single(h => h.Kind == HazardKind.Laser);
            yield return WaitUntil(() => gateOnClient.Active == gate.Active, "her client's gate is out of step");
            bool before = gate.Active;

            // The host aims at the arc (the first heading whose shot stops in it) and fires: it stops there, the arc stands.
            Assert.That(Turns.For(Server).Current, Is.EqualTo(host.Seat));
            float? atArc = null;
            for (float heading = 0f; heading < 360f && atArc == null; heading += 0.5f)
            {
                ShotPath path = host.SolveVolley(Weapon.Cannon, heading, 1f, 2, 0).Main;
                if (path.End == ShotEndKind.Absorbed && path.HazardId == arc.Id)
                {
                    atArc = heading;
                }
            }

            Assert.That(atArc, Is.Not.Null, "no shot from the host's seat reaches the arc");
            host.ServerFire(atArc.Value, 1f);
            Assert.That(host.LastVolley.Main.End, Is.EqualTo(ShotEndKind.Absorbed));
            Assert.That(host.LastVolley.HazardsHit, Is.EqualTo(new[] { arc.Id }));

            // The turn passes once it lands: the gate switches, on every peer, and the arc is still there.
            yield return WaitUntil(() => Turns.For(Server).Current == ann.Seat, "the turn never passed", 15f);
            Assert.That(gate.Active, Is.EqualTo(!before));
            yield return WaitUntil(() => gateOnClient.Active == !before, "her client's gate never switched");
            Assert.That(arena.HazardsOnField, Has.Member(arc));
        }

        private Player Mine() => Player.In(Client).First(p => p.OwnerClientId == Client.LocalClientId);

        // Starts both sessions, serves the arena (laying its hazards or not), and waits for both players to have their seats.
        private IEnumerator Begin(bool layHazards, System.Action<Arena> arena, System.Action<Player, Player> players)
        {
            yield return StartAndJoinAll();
            arena(Arena.Serve(Server, GamePrefabs.Dummy, layHazards));
            var host = WaitForSpawn<Player>(Server, owner: Server.LocalClientId);
            yield return host;
            var ann = WaitForSpawn<Player>(Server, owner: Client.LocalClientId);
            yield return ann;
            yield return WaitUntil(() => Player.In(Client).Count() == 2, "the client never saw both players");
            players(host.Result, ann.Result);
        }
    }
}
