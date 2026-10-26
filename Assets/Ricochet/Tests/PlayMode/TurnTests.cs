// The match on the wire (UR-T4): a host and one client start a match, with or without an AI opponent, and every peer
// sees the same seats, health, whose turn it is and the turn clock; the server refuses a shot out of turn, hurts whoever
// a shot lands on, passes the turn when the shot lands or the clock runs out, and plays the AI seat's turn itself; and
// (UR-T12) the client flies the volley the server solved, point for point, and gets the server's damage for it.
// The tests that aim at a seat play the fixed Practice Range (TestMatches), since the random arena moves every seat (UR-T68).
// The turn rules themselves are tested without Unity: dotnet test Core.slnx (Tests/Core.Tests/MatchTests.cs).
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Ricochet.Core.Aim;
using Ricochet.Core.Level;
using Ricochet.Core.Match;
using Unity.Multiplayer;
using Unity.Multiplayer.Core;
using Unity.Multiplayer.Testing;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ricochet.Tests
{
    public class TurnTests : NetSessionTest
    {
        private double _turnSeconds;
        private double _endTurnSeconds;
        private double _aiThinkSeconds;

        public TurnTests() : base(Topology.Host, clients: 1) { }

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
            _turnSeconds = Arena.MatchRules.TurnSeconds;
            _endTurnSeconds = Arena.MatchRules.EndTurnSeconds;
            _aiThinkSeconds = Arena.MatchRules.AiThinkSeconds;
        }

        [TearDown]
        public void PutTheRulesBack()
        {
            Arena.MatchRules.TurnSeconds = _turnSeconds;
            Arena.MatchRules.EndTurnSeconds = _endTurnSeconds;
            Arena.MatchRules.AiThinkSeconds = _aiThinkSeconds;
        }

        [UnityTest]
        public IEnumerator AMatchSeatsBothPlayersAndAnAi_AndTheClientSeesEverySeatsHealthAndWhoseTurnItIs()
        {
            Arena arena = null;
            Player host = null, ann = null;
            yield return Begin(a => arena = a, (h, c) => { host = h; ann = c; });

            Assert.That(arena.StartMatch(1), Is.True);
            Turns turns = null;
            yield return WaitUntil(() => (turns = Turns.For(Client)) != null && turns.Round == 1, "the client never got the match");
            yield return WaitUntil(() => Player.In(Client).Count() == 3, () => "the client sees " + Player.In(Client).Count() + " players");

            Player[] seated = Player.In(Client).OrderBy(p => p.Seat).ToArray();
            yield return WaitUntil(() => seated.All(p => p.InRound), "every seat in the round on the client");
            Assert.That(seated.Select(p => p.Seat), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(seated.Select(p => p.IsAi), Is.EqualTo(new[] { false, false, true }), "the humans first, in join order, then the AI");
            Assert.That(seated[2].DisplayName, Is.EqualTo("AI 1"));
            Assert.That(seated.All(p => p.Health == Arena.MatchRules.MaxHealth), Is.True);
            Assert.That(seated[2].Position, Is.EqualTo(Arena.SpawnPoint(2)));

            Assert.That(turns.MatchPhase, Is.EqualTo(MatchPhase.Aiming));
            Assert.That(turns.Current, Is.EqualTo(0), "seat 0 opens");
            Assert.That(turns.ShownTimeLeft, Is.GreaterThan(Arena.MatchRules.TurnSeconds - 2));
            Assert.That(Client.Objects.Contains(Arena.DummyKey(0)), Is.False, "the practice dummies are cleared away");
            Assert.That(Arena.Targets(Client).Select(t => t.Id), Is.EquivalentTo(new[] { "seat:0", "seat:1", "seat:2" }));
        }

        [UnityTest]
        public IEnumerator AShotOutOfTurnIsRefused_TheTurnPassesWhenAShotLands_ThenItIsTheirs()
        {
            Arena.MatchRules.EndTurnSeconds = 0.2;
            Arena arena = null;
            Player host = null, ann = null;
            yield return Begin(a => arena = a, (h, c) => { host = h; ann = c; });
            Assert.That(arena.StartMatch(0), Is.True);

            Player annOnHerClient = Player.In(Client).First(p => p.OwnerClientId == Client.LocalClientId);
            yield return WaitUntil(() => Turns.For(Client) != null && annOnHerClient.Seat == ann.Seat, "her client never got the match");
            Assert.That(Turns.For(Server).Current, Is.EqualTo(host.Seat), "the host's seat opens");

            annOnHerClient.Fire(0f, 0f);                           // not her turn
            yield return new WaitForSeconds(0.3f);
            Assert.That(ann.Shots, Is.EqualTo(0), "the server took a shot out of turn");

            host.ServerFire(0f, 0f);                               // the host's turn: a short shot to the right, at nobody
            Assert.That(host.Shots, Is.EqualTo(1));
            yield return WaitUntil(() => Turns.For(Client).Current == ann.Seat && Turns.For(Client).MatchPhase == MatchPhase.Aiming,
                () => "the turn never passed to Ann: " + Turns.For(Client).MatchPhase + " seat " + Turns.For(Client).Current);

            annOnHerClient.Fire(0f, 0f);
            yield return WaitUntil(() => ann.Shots == 1, "her shot on her own turn was refused");
        }

        [UnityTest]
        public IEnumerator AHitTakesHealthOffThePlayerItLandsOn_ForEveryone()
        {
            Arena arena = null;
            Player host = null, ann = null;
            yield return Begin(a => arena = a, (h, c) => { host = h; ann = c; });
            Assert.That(arena.StartMatch(TestMatches.OnThePracticeRange()), Is.True);
            yield return WaitUntil(() => Turns.For(Client) != null, "the client never got the match");

            string target = Arena.SeatTarget(ann.Seat);
            double? heading = BounceSolver.FindHeading(Arena.ToCore(host.Position), 1, target, Arena.Level, Arena.Targets(Server), Arena.Rules);
            Assert.That(heading, Is.Not.Null, "no aim from " + host.Position + " reaches Ann at " + ann.Position);
            host.ServerFire((float)heading.Value, 1f);
            Assert.That(host.LastShot.TargetId, Is.EqualTo(target));
            int bounces = host.LastShot.Bounces;

            Player annOnHerClient = Player.In(Client).First(p => p.OwnerClientId == Client.LocalClientId);
            yield return WaitUntil(() => annOnHerClient.Health < Arena.MatchRules.MaxHealth, "her client never saw her hurt", 20f);
            int damage = Arena.MatchRules.MaxHealth - annOnHerClient.Health;
            int expected = Arena.MatchRules.Damage(false, bounces, 0);
            Assert.That(damage, Is.InRange(expected - 1, expected + 1), "10 + 2 a bounce, +/- the dice");
            Assert.That(annOnHerClient.InRound, Is.True);
        }

        [UnityTest]
        public IEnumerator TheServerSendsTheBounceItSolvedAndTheDamageItDealt_AndTheClientShowsExactlyThat()
        {
            Arena arena = null;
            Player host = null, ann = null;
            yield return Begin(a => arena = a, (h, c) => { host = h; ann = c; });
            Assert.That(arena.StartMatch(TestMatches.OnThePracticeRange()), Is.True);
            yield return WaitUntil(() => Turns.For(Client) != null, "the client never got the match");

            string target = Arena.SeatTarget(ann.Seat);
            double? heading = BounceSolver.FindHeading(Arena.ToCore(host.Position), 1, target, Arena.Level, Arena.Targets(Server), Arena.Rules);
            Assert.That(heading, Is.Not.Null, "no aim from " + host.Position + " reaches Ann at " + ann.Position);
            host.ServerFire((float)heading.Value, 1f);
            Assert.That(host.LastVolleyShot, Is.EqualTo(1));

            Player hostOnClient = Player.In(Client).First(p => p.OwnerClientId == Server.LocalClientId);
            yield return WaitUntil(() => hostOnClient.LastVolleyShot == 1, "the client never got the server's volley");
            Assert.That(hostOnClient.LastShot.Points, Is.EqualTo(host.LastShot.Points), "the client flies the server's bounce, point for point");
            Assert.That(hostOnClient.LastShot.TargetId, Is.EqualTo(target));

            yield return WaitUntil(() => host.LastOutcome != null, "the server never landed the shot", 20f);
            yield return WaitUntil(() => hostOnClient.LastOutcomeShot == 1, "the client never got the shot's result", 5f);
            Assert.That(hostOnClient.LastOutcome.Seats, Is.EqualTo(new[] { ann.Seat }));
            Assert.That(hostOnClient.LastOutcome.Damage, Is.EqualTo(host.LastOutcome.Damage), "the same damage on every peer");
            Player annOnHerClient = Player.In(Client).First(p => p.OwnerClientId == Client.LocalClientId);
            yield return WaitUntil(() => annOnHerClient.Health == Arena.MatchRules.MaxHealth - host.LastOutcome.DamageTo(ann.Seat),
                () => "her client has her at " + annOnHerClient.Health + " after " + host.LastOutcome.DamageTo(ann.Seat) + " damage");
        }

        [UnityTest]
        public IEnumerator WhenTheClockRunsOutTheTurnPasses()
        {
            Arena arena = null;
            Player host = null, ann = null;
            yield return Begin(a => arena = a, (h, c) => { host = h; ann = c; });
            Assert.That(arena.StartMatch(new MatchSetup { AiCount = 0, TurnSeconds = MatchSetup.MinTurnSeconds }), Is.True);
            // StartMatch applies the setup's rules, so the test's tuning goes in after it (the first turn keeps the setup's 5 s clock):
            Arena.MatchRules.TurnSeconds = 1;
            Turns turns = null;
            yield return WaitUntil(() => (turns = Turns.For(Client)) != null, "the client never got the match");

            yield return WaitUntil(() => turns.Current == ann.Seat, "the clock never passed the turn", 12f);
            Assert.That(turns.ShownTimeLeft, Is.LessThanOrEqualTo(1f));
            Assert.That(Seating.FormatClock(turns.ShownTimeLeft), Does.Match(@"^0[01]\.\d\d$"));
        }

        [UnityTest]
        public IEnumerator AnAiSeatTakesItsTurnOnItsOwn()
        {
            Arena.MatchRules.AiThinkSeconds = 0.2;                 // and the AI fires well inside its own
            Arena.MatchRules.EndTurnSeconds = 0.2;
            Arena arena = null;
            Player host = null, ann = null;
            yield return Begin(a => arena = a, (h, c) => { host = h; ann = c; });
            Assert.That(arena.StartMatch(new MatchSetup { AiCount = 1, TurnSeconds = MatchSetup.MinTurnSeconds }), Is.True);
            // StartMatch applies the setup's rules, so the test's tuning goes in after it (the first turn keeps the setup's 5 s clock):
            Arena.MatchRules.TurnSeconds = 0.5;  // the humans let their clocks run out

            Player ai = arena.PlayerAt(2);
            Assert.That(ai, Is.Not.Null);
            Assert.That(ai.IsAi, Is.True);
            yield return WaitUntil(() => ai.Shots >= 1, "the AI never fired", 20f);
            Player aiOnClient = null;
            yield return WaitUntil(() => (aiOnClient = Player.In(Client).FirstOrDefault(p => p.IsAi)) != null && aiOnClient.LastShot != null,
                "the client never saw the AI's shot");
        }

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
