// The game's own PlayMode tests, on the kit's test fixtures (Unity.Multiplayer.Testing): every test gets a real host or
// dedicated server and real clients over Unity Transport on a free loopback port, and the waits fail saying what never
// happened. Run them from Window > General > Test Runner > PlayMode, or in batch mode:
//   Unity -batchmode -projectPath . -runTests -testPlatform PlayMode -assemblyNames Ricochet.Tests
// The bounce math itself is tested without Unity: dotnet test Core.slnx (Tests/Core.Tests).
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
    internal static class GamePrefabs
    {
        public static GameObject Player => Load("Game/Player");
        public static GameObject Dummy => Load("Game/Dummy");

        private static GameObject Load(string path)
        {
            var prefab = Resources.Load<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, "Resources/" + path + ".prefab is missing");
            return prefab;
        }
    }

    /// <summary>A host and one client: the aim goes up through [ToServer] and comes back through [Synced], the server
    /// clamps it, and a fired shot is solved on the server, replayed on the client and lands on a dummy.</summary>
    public class PlayerTests : NetSessionTest
    {
        public PlayerTests() : base(Topology.Host, clients: 1) { }

        protected override void OnSessionsCreated()
        {
            SetPlayerPrefab(GamePrefabs.Player);
            RegisterPrefab(GamePrefabs.Dummy);
            Client.Identity = new PlayerIdentity("ann-token", "Ann", new PlayerColor(230, 80, 60));
        }

        [UnityTest]
        public IEnumerator TheServerTakesAClientsAim_AndEveryoneSeesItsNameAndColour()
        {
            yield return StartAndJoinAll();

            var annOnHerClient = WaitForSpawn<Player>(Client, owner: Client.LocalClientId);
            yield return annOnHerClient;
            var annOnHost = WaitForSpawn<Player>(Server, owner: Client.LocalClientId);
            yield return annOnHost;
            yield return WaitUntil(() => annOnHerClient.Result.Position != Vector2.zero, "Ann was never given her seat's spot");

            annOnHerClient.Result.Aim(30f, 0.8f);
            yield return WaitUntil(() => Mathf.Approximately(annOnHost.Result.Heading, 30f), () => "the host has her aim at " + annOnHost.Result.Heading);
            yield return WaitUntil(() => Mathf.Approximately(annOnHerClient.Result.Power, 0.8f), () => "her own client has her power at " + annOnHerClient.Result.Power);

            yield return WaitUntil(() => annOnHost.Result.DisplayName == "Ann", () => "the host sees her as " + annOnHost.Result.DisplayName);
            Assert.That(annOnHost.Result.Color, Is.EqualTo(new Color32(230, 80, 60, 255)));
        }

        [UnityTest]
        public IEnumerator TheServerClampsTheAim_SoAClientCannotSendGarbage()
        {
            yield return StartAndJoinAll();
            var ann = WaitForSpawn<Player>(Client, owner: Client.LocalClientId);
            yield return ann;

            ann.Result.Aim(-90f, 5f);                             // a cheating client
            yield return WaitUntil(() => Mathf.Approximately(ann.Result.Heading, 270f) && Mathf.Approximately(ann.Result.Power, 1f),
                () => "the aim came back as " + ann.Result.Heading + " / " + ann.Result.Power);

            ann.Result.Aim(float.NaN, -3f);
            yield return WaitUntil(() => Mathf.Approximately(ann.Result.Power, 0f), () => "the power came back as " + ann.Result.Power);
            Assert.That(ann.Result.Heading, Is.EqualTo(270f), "a NaN heading keeps the last good one");
        }

        [UnityTest]
        public IEnumerator AShotIsSolvedOnTheServer_FliesTheSamePathOnTheClient_AndKnocksDownTheDummyItLandsOn()
        {
            yield return StartAndJoinAll();
            Arena.Serve(Server, GamePrefabs.Dummy);
            var ann = WaitForSpawn<Player>(Client, owner: Client.LocalClientId);
            yield return ann;
            var annOnHost = WaitForSpawn<Player>(Server, owner: Client.LocalClientId);
            yield return annOnHost;

            string key = Arena.DummyKey(2);
            Dummy dummy = null;
            yield return WaitUntil(() => Client.Objects.TryGet(key, out dummy) && dummy.Position == Arena.DummyStart(2), "the client never got " + key);
            yield return WaitUntil(() => ann.Result.Position != Vector2.zero, "Ann was never given her seat's spot");

            double? heading = BounceSolver.FindHeading(Arena.ToCore(ann.Result.Position), 1, key, Arena.Level, Arena.Targets(Client), Arena.Rules);
            Assert.That(heading, Is.Not.Null, "no aim from " + ann.Result.Position + " reaches " + key);

            ann.Result.Fire((float)heading.Value, 1f);
            yield return WaitUntil(() => annOnHost.Result.Shots == 1 && annOnHost.Result.ShotInFlight, "the host never took the shot");
            yield return WaitUntil(() => ann.Result.LastShot != null, "her client never drew the shot");

            Assert.That(annOnHost.Result.LastShot.TargetId, Is.EqualTo(key), "the server's solve lands on the dummy");
            Assert.That(ann.Result.LastShot.Points, Is.EqualTo(annOnHost.Result.LastShot.Points), "the client flies the server's path");

            yield return WaitUntil(() => dummy.Down, "the client never saw " + key + " knocked down", 20f);
            Assert.That(Server.Objects.Get<Dummy>(key).Down, Is.True);
            Assert.That(Arena.Targets(Server).Select(t => t.Id), Has.No.Member(key), "a knocked dummy is no longer a target");
        }

        [UnityTest]
        public IEnumerator APlayerHasOneShotInTheAirAtATime()
        {
            yield return StartAndJoinAll();
            var ann = WaitForSpawn<Player>(Client, owner: Client.LocalClientId);
            yield return ann;
            var annOnHost = WaitForSpawn<Player>(Server, owner: Client.LocalClientId);
            yield return annOnHost;

            ann.Result.Fire(270f, 1f);                            // straight down: it bounces floor to ceiling for seconds
            ann.Result.Fire(0f, 1f);
            yield return WaitUntil(() => annOnHost.Result.Shots == 1, "the host never took the first shot");
            yield return new WaitForSeconds(0.3f);
            Assert.That(annOnHost.Result.Shots, Is.EqualTo(1), "the second Fire came while the first was in the air");
            Assert.That(annOnHost.Result.ShotHeading, Is.EqualTo(270f));
        }
    }

    /// <summary>The keyed practice dummies: spawned by key on the server, found by key on a client, knocked down, and stood
    /// back up by a vote.</summary>
    public class DummyTests : NetSessionTest
    {
        public DummyTests() : base(Topology.Host, clients: 1) { }

        protected override void OnSessionsCreated()
        {
            RegisterPrefab(GamePrefabs.Dummy);
            foreach (NetSession session in Sessions)
            {
                VoteBoard.RegisterPrefab(session);                // what NetVotes does in the scene
            }
        }

        [UnityTest]
        public IEnumerator EveryDummyIsFoundByItsKeyOnAClient()
        {
            yield return StartAndJoinAll();
            Arena.Serve(Server, GamePrefabs.Dummy);

            for (int i = 0; i < Arena.DummyCount; i++)
            {
                string key = Arena.DummyKey(i);
                Dummy dummy = null;
                yield return WaitUntil(() => Client.Objects.TryGet(key, out dummy), "the client never got " + key);
                yield return WaitForSynced(() => dummy.Position, Arena.DummyStart(i), key + "'s position");
                Assert.That(dummy.Key, Is.EqualTo(key));
                Assert.That(dummy.Down, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator ARestartVoteStandsEveryDummyBackUp()
        {
            yield return StartAndJoinAll();
            Arena arena = Arena.Serve(Server, GamePrefabs.Dummy);
            VoteBoard board = VoteBoard.Spawn(Server);
            board.AddKind(arena.CreateRestartVote());

            Assert.That(arena.Knock(Arena.DummyKey(0)), Is.True);
            Dummy knocked = null;
            yield return WaitUntil(() => Client.Objects.TryGet(Arena.DummyKey(0), out knocked) && knocked.Down,
                "the client never saw dummy:0 knocked down");

            VoteBoard onClient = null;
            yield return WaitUntil(() => (onClient = VoteBoard.For(Client)) != null, "the client never got the vote board");
            onClient.StartVote(Arena.RestartVote);                // the client starts it (its Yes counts)
            yield return WaitUntil(() => board.IsVoteOpen, "the vote never opened");
            board.CastVote(true);                                 // the host says Yes too: 2 of 2

            Dummy back = null;
            yield return WaitUntil(() => Client.Objects.TryGet(Arena.DummyKey(0), out back) && back != knocked && !back.Down
                && back.Position == Arena.DummyStart(0), "dummy:0 never stood back up after the vote");
        }
    }

    /// <summary>The nightly smoke without Docker: a dedicated server and two bots (GameBot) that join by code and each
    /// bank a shot onto their own dummy.</summary>
    public class BotTests : NetBotTest
    {
        public BotTests() : base(bots: 2) { }

        protected override float BotTimeoutSeconds => 60f;

        protected override void OnSessionsCreated()
        {
            SetPlayerPrefab(GamePrefabs.Player);
            RegisterPrefab(GamePrefabs.Dummy);
            Arena.Serve(Server, GamePrefabs.Dummy);               // the server's rules, on the fixture's server
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TwoBotsJoinByCode_AndEachKnocksDownItsDummy()
        {
            yield return RunBots(role => new GameBot(), "A", "B");

            foreach (NetBotRunner bot in Bots)
            {
                Assert.That(bot.PassMessage, Does.Contain(GameBot.DummyFor(bot.Role)));
                Assert.That(Server.Objects.Get<Dummy>(GameBot.DummyFor(bot.Role)).Down, Is.True, "the server knocked down " + GameBot.DummyFor(bot.Role));
            }
        }
    }
}
