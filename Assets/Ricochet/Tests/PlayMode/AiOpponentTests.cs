// AI opponents and aim assist on the wire (UR-T7): a match started with a setup seats its AI at that skill and gives
// everyone its aim assists; on its turn the AI's "is thinking..." line reaches every peer, then it turns its barrel onto
// an opponent and hits them; a client's aim assist turns her aim onto the host, spends one, and the shot she fires along
// it lands. The targeting itself is tested without Unity: dotnet test Core.slnx (Tests/Core.Tests/AiTests.cs).
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
    public class AiOpponentTests : NetSessionTest
    {
        private double _turnSeconds;
        private double _endTurnSeconds;
        private double _aiThinkSeconds;

        public AiOpponentTests() : base(Topology.Host, clients: 1) { }

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
            new MatchSetup().ApplyTo(Arena.ArsenalRules);
        }

        [UnityTest]
        public IEnumerator AFullySkilledAiThinksOutLoud_TurnsOntoAnOpponent_AndHitsThem()
        {
            Arena.MatchRules.TurnSeconds = 1.5;                    // the humans let their clocks run out
            Arena.MatchRules.AiThinkSeconds = 0.5;
            Arena.MatchRules.EndTurnSeconds = 0.2;
            Arena arena = null;
            Player host = null, ann = null;
            yield return Begin(a => arena = a, (h, c) => { host = h; ann = c; });
            Assert.That(arena.StartMatch(new MatchSetup { AiCount = 1, AiSkill = Ai.MaxSkill }), Is.True);
            Assert.That(arena.Setup.AiSkill, Is.EqualTo(Ai.MaxSkill));

            Player ai = arena.PlayerAt(2);
            Assert.That(ai.IsAi, Is.True);
            Turns turns = null;
            yield return WaitUntil(() => (turns = Turns.For(Client)) != null && turns.Current == ai.Seat && turns.ThinkingLine >= 0,
                "the client never saw the AI thinking", 10f);
            Assert.That(Ai.ThinkingLine(turns.ThinkingLine), Does.StartWith(" ").And.EndWith("..."));

            yield return WaitUntil(() => ai.Shots >= 1, "the AI never fired", 5f);
            AiPlan plan = arena.AiTurn != null ? arena.AiTurn.Plan : null;
            Assert.That(ai.LastShot.End, Is.EqualTo(ShotEndKind.Target), "plan " + plan);
            int hit = Arena.SeatOf(ai.LastShot.TargetId);
            Assert.That(hit, Is.EqualTo(host.Seat).Or.EqualTo(ann.Seat), "it aimed at an opponent, not itself");
            Assert.That(ai.ShotWeapon, Is.EqualTo((int)Weapon.Cannon), "a straight shot is the cannon's");

            Player hurt = Player.In(Client).First(p => p.Seat == hit);
            yield return WaitUntil(() => hurt.Health < Arena.MatchRules.MaxHealth, "the client never saw the AI's hit", 10f);
            yield return WaitUntil(() => turns.ThinkingLine < 0, "the thinking line outlived the AI's turn", 5f);
        }

        [UnityTest]
        public IEnumerator AClientsAimAssistTurnsHerAimOntoTheHost_SpendsOne_AndHerShotLands()
        {
            Arena.MatchRules.EndTurnSeconds = 0.2;
            Arena arena = null;
            Player host = null, ann = null;
            yield return Begin(a => arena = a, (h, c) => { host = h; ann = c; });
            Assert.That(arena.StartMatch(new MatchSetup { AiCount = 0, AimAssists = 2 }), Is.True);
            Player annOnHerClient = Player.In(Client).First(p => p.OwnerClientId == Client.LocalClientId);
            yield return WaitUntil(() => Turns.For(Client) != null && annOnHerClient.Seat == ann.Seat, "her client never got the match");
            yield return WaitUntil(() => annOnHerClient.Loadout.Count(Item.AimAssist) == 2, () => "she starts with " + annOnHerClient.Loadout.Count(Item.AimAssist));

            host.ServerFire(0f, 0f);                               // the host's turn: a short shot at nobody
            yield return WaitUntil(() => Turns.For(Client).Current == ann.Seat && Turns.For(Client).MatchPhase == MatchPhase.Aiming,
                "the turn never passed to Ann");

            annOnHerClient.CycleItem(-1);                          // left from the extra bounce: the redirector (UR-T6),
            yield return WaitUntil(() => ann.Loadout.Item == Item.Redirector, () => "her item is " + ann.Loadout.Item);
            annOnHerClient.CycleItem(-1);                          // then aim assist
            yield return WaitUntil(() => ann.Loadout.Item == Item.AimAssist, () => "her item is " + ann.Loadout.Item);
            annOnHerClient.UseItem(270f, 0.2f);                    // aimed at the floor, nearly no power
            yield return WaitUntil(() => annOnHerClient.Assists == 1, "the assist never came back to her client");
            Assert.That(ann.Loadout.Count(Item.AimAssist), Is.EqualTo(1), "one spent");
            Assert.That(annOnHerClient.AssistShown, Is.True, "her client draws the beam");

            string target = Arena.SeatTarget(host.Seat);
            Assert.That(ann.Solve(ann.Heading, ann.Power).TargetId, Is.EqualTo(target), "the server turned her aim onto the host");
            yield return WaitUntil(() => annOnHerClient.Preview != null && annOnHerClient.Preview.Main.TargetId == target,
                "her own aim never followed the assist");

            annOnHerClient.Fire(annOnHerClient.AssistHeading, annOnHerClient.AssistPower);
            Player hostOnClient = Player.In(Client).First(p => p.Seat == host.Seat);
            yield return WaitUntil(() => hostOnClient.Health < Arena.MatchRules.MaxHealth, "her assisted shot never hurt the host", 10f);
            Assert.That(annOnHerClient.AssistShown, Is.False, "the beam goes with the shot");
        }

        [UnityTest]
        public IEnumerator AMatchWithNoAimAssistTakesTheItemAway()
        {
            Arena arena = null;
            Player host = null, ann = null;
            yield return Begin(a => arena = a, (h, c) => { host = h; ann = c; });
            Assert.That(arena.StartMatch(new MatchSetup { AiCount = 0, AimAssists = 0 }), Is.True);
            Player annOnHerClient = Player.In(Client).First(p => p.OwnerClientId == Client.LocalClientId);
            yield return WaitUntil(() => Turns.For(Client) != null && annOnHerClient.Seat == ann.Seat, "her client never got the match");
            Assert.That(host.Loadout.Count(Item.AimAssist), Is.EqualTo(0));
            Assert.That(Arena.ArsenalRules.InPlay(Item.AimAssist), Is.False, "and no crate gives one");
            yield return WaitUntil(() => annOnHerClient.Loadout.Count(Item.ExtraBounce) == 3 && annOnHerClient.Loadout.Count(Item.AimAssist) == 0,
                "her client still shows aim assists");
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
