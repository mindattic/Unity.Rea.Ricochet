// The kick vote (UR-T14): a host and one client, Ann, play a match with an AI. The host votes to kick Ann; it passes (the
// host is the only one voting: Ann is the target) and the kit disconnects her with the game's line, frees her seat at once
// and her match seat drops out while the host and the AI play on. Ann cannot vote out the host.
// What the votes say and who may be kicked is tested without Unity: dotnet test Core.slnx (Tests/Core.Tests/VoteMenuTests.cs).
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Ricochet.Core;
using Unity.Multiplayer;
using Unity.Multiplayer.Core;
using Unity.Multiplayer.Testing;
using UnityEngine.TestTools;

namespace Ricochet.Tests
{
    public class KickVoteTests : NetSessionTest
    {
        public KickVoteTests() : base(Topology.Host, clients: 1) { }

        protected override void OnSessionsCreated()
        {
            SetPlayerPrefab(GamePrefabs.Player);
            RegisterPrefab(GamePrefabs.Dummy);
            RegisterPrefab(GamePrefabs.Opponent);
            RegisterPrefab(GamePrefabs.Turns);
            RegisterPrefab(GamePrefabs.Crate);
            foreach (NetSession session in Sessions)
            {
                VoteBoard.RegisterPrefab(session);                // what NetVotes does in the scene
            }

            Client.Identity = new PlayerIdentity("ann-token", "Ann", new PlayerColor(230, 80, 60));
        }

        [UnityTest]
        public IEnumerator AKickVoteThatPasses_RemovesThePlayer_AndTheirSeatDropsOutOfTheMatch()
        {
            Arena arena = null;
            Player ann = null;
            VoteBoard board = null;
            yield return Begin(a => arena = a, p => ann = p, b => board = b);
            Assert.That(arena.StartMatch(1), Is.True); // the host, Ann and an AI
            int annSeat = ann.Seat;
            ulong annId = Client.LocalClientId;

            board.StartKickVote(annId);
            yield return WaitUntil(() => Client.LastDisconnect.Reason == DisconnectReason.Kicked, "Ann was never removed", 10f);
            Assert.That(Client.LastDisconnect.Message, Does.Contain(VoteMenu.KickedLine), "she is told why");
            yield return WaitUntil(() => !arena.Match.Seat(annSeat).Present, "her match seat never dropped out");
            Assert.That(arena.Match.Seat(annSeat).Alive, Is.False);
            Assert.That(arena.Match.TurnOrder(), Has.No.Member(annSeat));
            Assert.That(arena.Match.Phase, Is.Not.EqualTo(MatchPhase.RoundOver), "the host and the AI play on");
            Assert.That(Server.ConnectedClientIds.Contains(annId), Is.False);
        }

        [UnityTest]
        public IEnumerator NobodyCanVoteOutTheHost()
        {
            VoteBoard board = null;
            yield return Begin(_ => { }, _ => { }, b => board = b);
            VoteBoard onClient = null;
            yield return WaitUntil(() => (onClient = VoteBoard.For(Client)) != null, "the client never got the vote board");
            string refusal = null;
            onClient.VoteRefused += (_, why) => refusal = why;

            onClient.StartKickVote(Server.LocalClientId);
            yield return WaitUntil(() => refusal != null, "the kick of the host was never refused");
            Assert.That(refusal, Does.Contain("host"));
            Assert.That(board.IsVoteOpen, Is.False);
        }

        // Starts both sessions, serves the arena and the votes (the kick with the game's lines), and waits for Ann's player.
        private IEnumerator Begin(System.Action<Arena> arena, System.Action<Player> annPlayer, System.Action<VoteBoard> votes)
        {
            yield return StartAndJoinAll();
            Arena served = Arena.Serve(Server, GamePrefabs.Dummy);
            VoteBoard board = VoteBoard.Spawn(Server);
            board.AddKind(served.CreateKickVote());
            var ann = WaitForSpawn<Player>(Server, owner: Client.LocalClientId);
            yield return ann;
            yield return WaitUntil(() => Player.In(Client).Count() == 2, "the client never saw both players");
            arena(served);
            annPlayer(ann.Result);
            votes(board);
        }
    }
}
