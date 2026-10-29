// The Ready card and the turn-start ping on the wire (UR-T42): a host and one client start a match on the stock rules;
// every turn opens with the Ready card (obj_ready) on the client's HUD, the player's name on it and over their tank, while
// the server holds the turn clock full and refuses a shot; once the card has held and faded the clock starts, the client
// pings the tank (ping_player's rings) and the shot is taken. An AI seat waits for its card too before it thinks or fires.
// The hold's rules are tested without Unity: dotnet test Core.slnx (Tests/Core.Tests/MatchTests.cs, CommandTests.cs).
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Ricochet.Core.Match;
using Unity.Multiplayer;
using Unity.Multiplayer.Core;
using Unity.Multiplayer.Testing;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ricochet.Tests
{
    public class ReadyCardTests : NetSessionTest
    {
        private double _endTurnSeconds;
        private MatchHud _hud;

        public ReadyCardTests() : base(Topology.Host, clients: 1) { }

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
            TestMatches.StockReadyCard(); // the stock hold, whatever an earlier fixture left
        }

        [TearDown]
        public void PutTheRulesBack()
        {
            Arena.MatchRules.EndTurnSeconds = _endTurnSeconds;
            TestMatches.StockReadyCard();
            if (_hud != null)
            {
                Object.Destroy(_hud.gameObject);
                _hud = null;
            }
        }

        [UnityTest]
        public IEnumerator EveryTurnOpensWithTheReadyCard_TheClockHoldsAndNobodyFires_ThenThePingAndTheClock()
        {
            Arena arena = null;
            Player host = null, ann = null;
            yield return Begin(a => arena = a, (h, c) => { host = h; ann = c; });
            _hud = MatchHud.Create(Client);

            Assert.That(arena.StartMatch(TestMatches.OnThePracticeRange()), Is.True);
            Turns serverTurns = Turns.For(Server);
            Assert.That(serverTurns.MatchPhase, Is.EqualTo(MatchPhase.Ready), "the first turn opens with the card");
            Turns turns = null;
            yield return WaitUntil(() => (turns = Turns.For(Client)) != null && turns.MatchPhase == MatchPhase.Ready, "the client never saw the Ready card's turn");
            yield return WaitUntil(() => _hud.Ready.Showing, "the client's HUD never put the Ready card up");
            Assert.That(_hud.Ready.Seat, Is.EqualTo(host.Seat));
            Assert.That(_hud.Ready.NameText, Is.EqualTo(host.DisplayName), "the card names whose turn it is");
            Assert.That(_hud.Ready.OverTankText, Is.EqualTo(host.DisplayName), "and the name is over their tank");
            Color hostColor = Player.In(Client).First(p => p.Seat == host.Seat).Color;
            Assert.That(_hud.Ready.NameColor, Is.EqualTo(hostColor), "in their colour");
            Assert.That(_hud.Ready.Alpha, Is.EqualTo(1f), "full strength while it holds");

            host.ServerFire(0f);
            Assert.That(host.Shots, Is.EqualTo(0), "no shot while the card is up");
            yield return new WaitForSeconds(0.8f);
            Assert.That(serverTurns.MatchPhase, Is.EqualTo(MatchPhase.Ready), "still holding");
            Assert.That(turns.ShownTimeLeft, Is.EqualTo((float)Arena.MatchRules.TurnSeconds).Within(0.01f), "the clock does not run during the hold");
            Assert.That(_hud.Ready.Pings, Is.EqualTo(0), "no ping before the clock");

            yield return WaitUntil(() => _hud.Ready.Alpha < 1f && _hud.Ready.Alpha > 0f, "the card never faded", 3f);
            yield return WaitUntil(() => _hud.Ready.Pings >= 1, "the tank was never pinged as the clock started", 3f);
            Assert.That(_hud.Ready.LastPingSeat, Is.EqualTo(host.Seat));
            Assert.That(_hud.Ready.RingsShowing, Is.EqualTo(2), "a white ring and one in their colour");
            Assert.That(turns.MatchPhase, Is.EqualTo(MatchPhase.Aiming));
            Assert.That(_hud.Ready.Showing, Is.False, "the card is gone once the clock runs");
            Assert.That(_hud.Ready.OverTankText, Is.Empty);

            host.ServerFire(0f);
            Assert.That(host.Shots, Is.EqualTo(1), "the turn is theirs now");

            // The next turn gets its own card.
            yield return WaitUntil(() => _hud.Ready.Showing && _hud.Ready.Seat == ann.Seat, "Ann's turn never opened with her card", 10f);
            Assert.That(_hud.Ready.NameText, Is.EqualTo("Ann"));
            Assert.That(_hud.Ready.Cards, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator AnAiSeatWaitsForItsReadyCard_BeforeItThinksOrFires()
        {
            Arena.MatchRules.EndTurnSeconds = 0.2;
            Arena arena = null;
            Player host = null, ann = null;
            yield return Begin(a => arena = a, (h, c) => { host = h; ann = c; });
            Assert.That(arena.StartMatch(TestMatches.OnThePracticeRange(1)), Is.True);
            Turns turns = Turns.For(Server);
            Player ai = arena.PlayerAt(2);
            Assert.That(ai != null && ai.IsAi, Is.True);

            // The humans each take a short shot at nobody once their own clock runs.
            foreach (Player human in new[] { host, ann })
            {
                yield return WaitUntil(() => turns.Current == human.Seat && turns.MatchPhase == MatchPhase.Aiming, "seat " + human.Seat + "'s clock never started", 10f);
                human.ServerFire(0f);
                Assert.That(human.Shots, Is.EqualTo(1));
            }

            yield return WaitUntil(() => turns.Current == ai.Seat && turns.MatchPhase == MatchPhase.Ready, "the AI's turn never opened with its card", 10f);
            float until = Time.time + (float)Arena.MatchRules.ReadySeconds - 0.3f;
            while (Time.time < until)
            {
                Assert.That(turns.MatchPhase, Is.EqualTo(MatchPhase.Ready));
                Assert.That(ai.Shots, Is.EqualTo(0), "the AI fired during its card");
                Assert.That(turns.ThinkingLine, Is.EqualTo(-1), "the AI is thinking during its card");
                yield return null;
            }

            yield return WaitUntil(() => turns.MatchPhase == MatchPhase.Aiming, "the AI's clock never started", 2f);
            yield return WaitUntil(() => ai.Shots >= 1, "the AI never fired once its clock ran", 10f);
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
