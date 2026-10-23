// Reconnects in a match (UR-T13): a host and one client, Ann, play a match; Ann's connection drops on her own turn. The
// kit holds her seat and player object for the reconnect grace; the match keeps her seat, health and score, the turn clock
// waits for her (every peer's HUD says so) and the host does not get to play her. Back in time she resumes the same turn
// and her shot is taken; not back in time, she leaves the match and the turn passes.
// The rules themselves are tested without Unity: dotnet test Core.slnx (Tests/Core.Tests/MatchTests.cs).
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
    public class ReconnectTests : NetSessionTest
    {
        private double _endTurnSeconds;

        public ReconnectTests() : base(Topology.Host, clients: 1) { }

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
        public void KeepTheRules() => _endTurnSeconds = Arena.MatchRules.EndTurnSeconds;

        [TearDown]
        public void PutTheRulesBack() => Arena.MatchRules.EndTurnSeconds = _endTurnSeconds;

        [UnityTest]
        public IEnumerator APlayerWhoDropsOnTheirTurn_AndReconnectsInTime_ResumesTheSameTurnWithTheirSeatAndHealth()
        {
            Arena.MatchRules.EndTurnSeconds = 0.2;
            Arena arena = null;
            Player host = null, ann = null;
            yield return Begin(60, a => arena = a, (h, c) => { host = h; ann = c; });
            Assert.That(arena.StartMatch(0), Is.True);
            int annSeat = ann.Seat;

            // The host hits Ann, and the turn passes to her.
            string target = Arena.SeatTarget(annSeat);
            double? heading = BounceSolver.FindHeading(Arena.ToCore(host.Position), 1, target, Arena.Level, Arena.Targets(Server), Arena.Rules);
            Assert.That(heading, Is.Not.Null, "no aim from " + host.Position + " reaches Ann at " + ann.Position);
            host.ServerFire((float)heading.Value, 1f);
            yield return WaitUntil(() => arena.Match.Current == annSeat && arena.Match.Phase == MatchPhase.Aiming, "the turn never passed to Ann", 20f);
            int health = ann.Health;
            int score = host.Score;
            Assert.That(health, Is.LessThan(Arena.MatchRules.MaxHealth));
            ulong firstId = Client.LocalClientId;

            // Her connection drops.
            Client.Leave();
            yield return WaitUntil(() => ann.Away, "the server never marked Ann away");
            Assert.That(ann != null && ann.IsSpawned, Is.True, "her player waits with her seat");
            Assert.That(arena.Match.WaitingForCurrent, Is.True);
            Turns turns = Turns.For(Server);
            yield return WaitUntil(() => turns.Waiting && turns.WaitingSeconds > 0, "the turn state never said it was waiting for her");
            Assert.That(ann.DisplayName, Is.EqualTo("Ann"), "still named for her, not for the host who holds her object now");
            Assert.That(ann.IsLocalHuman, Is.False, "the host does not play her while she is away");
            double timeLeft = arena.Match.TimeLeft;
            yield return new WaitForSeconds(1f);
            Assert.That(arena.Match.TimeLeft, Is.EqualTo(timeLeft), "the clock waits for her");
            Assert.That(arena.Match.Current, Is.EqualTo(annSeat));
            Assert.That(ann.Health, Is.EqualTo(health), "her health is held");
            Assert.That(ann.InRound, Is.True);
            Assert.That(ann.Seat, Is.EqualTo(annSeat));

            // ...and she reconnects in time.
            yield return WaitUntil(() => Client.CanStart, "her session never finished leaving");
            yield return Join(Client);
            Assert.That(Client.LocalClientId, Is.Not.EqualTo(firstId), "a new connection");
            yield return WaitUntil(() => !ann.Away, "the server never gave Ann her seat back");
            Assert.That(ann.OwnerClientId, Is.EqualTo(Client.LocalClientId), "the same player object, hers again");
            Assert.That(arena.Match.WaitingForCurrent, Is.False);
            Assert.That(arena.Match.Current, Is.EqualTo(annSeat), "the same turn");
            Assert.That(arena.Match.TimeLeft, Is.EqualTo(timeLeft).Within(0.5), "with the time it had left");
            Assert.That(host.Score, Is.EqualTo(score));

            Player annOnHerClient = null;
            yield return WaitUntil(() => (annOnHerClient = Player.In(Client).FirstOrDefault(p => p.OwnerClientId == Client.LocalClientId)) != null
                && annOnHerClient.Seat == annSeat && Turns.For(Client) != null && !Turns.For(Client).Waiting, "her client never got her seat back");
            Assert.That(annOnHerClient.Health, Is.EqualTo(health), "her client sees the health she left with");
            Assert.That(Turns.For(Client).Current, Is.EqualTo(annSeat));
            Assert.That(Turns.MayFire(Client, annSeat), Is.True);

            int shots = ann.Shots;
            annOnHerClient.Fire(0f, 0f);
            yield return WaitUntil(() => ann.Shots == shots + 1, "her shot on her resumed turn was refused");
        }

        [UnityTest]
        public IEnumerator APlayerWhoDoesNotReconnectInTime_LeavesTheMatch_AndTheTurnPasses()
        {
            Arena arena = null;
            Player host = null, ann = null;
            yield return Begin(1.5, a => arena = a, (h, c) => { host = h; ann = c; });
            Assert.That(arena.StartMatch(1), Is.True); // the host, Ann and an AI, so the round goes on without her
            int annSeat = ann.Seat;
            Assert.That(arena.Match.Current, Is.EqualTo(host.Seat));

            Client.Leave();
            yield return WaitUntil(() => arena.Match.Seat(annSeat).Away, "the match never held her seat");
            Assert.That(arena.Match.Seat(annSeat).Alive, Is.True);

            yield return WaitUntil(() => !arena.Match.Seat(annSeat).Present, "her seat was never given up", 10f);
            Assert.That(arena.Match.Seat(annSeat).Alive, Is.False, "she is out of the round");
            Assert.That(arena.Match.TurnOrder(), Has.No.Member(annSeat));
            Assert.That(arena.Match.Phase, Is.Not.EqualTo(MatchPhase.RoundOver), "the host and the AI play on");
        }

        // Starts both sessions with a reconnect grace, serves the arena, and waits for both players to have their seats.
        private IEnumerator Begin(double grace, System.Action<Arena> arena, System.Action<Player, Player> players)
        {
            NetConfig config = Config();
            config.ReconnectGraceSeconds = grace;
            yield return StartAndJoinAll(config);
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
