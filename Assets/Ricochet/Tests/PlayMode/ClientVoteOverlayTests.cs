// U.RICO-T20: a joined client sees the vote. The host's board exists before the client joins, so the client's copy spawns during the
// initial sync (before its session is live), the case the kit's auto-wire once missed. Unity.Multiplayer's VoteOverlay now wires it
// itself (UM-T78; U.RICO-T87 removed the workaround): the client gets exactly one overlay, which shows the host's vote and casts its Yes.
using System.Collections;
using NUnit.Framework;
using Unity.Multiplayer;
using Unity.Multiplayer.Core;
using Unity.Multiplayer.Testing;
using Unity.Multiplayer.UI;
using UnityEngine.TestTools;

namespace Ricochet.Tests
{
    public class ClientVoteOverlayTests : NetSessionTest
    {
        public ClientVoteOverlayTests() : base(Topology.Host, clients: 1) { }

        protected override void OnSessionsCreated()
        {
            foreach (NetSession session in Sessions)
            {
                VoteBoard.RegisterPrefab(session);
            }
        }

        protected override IEnumerator OnSessionsTearDown()
        {
            foreach (VoteOverlay overlay in UnityEngine.Object.FindObjectsByType<VoteOverlay>(UnityEngine.FindObjectsSortMode.None))
            {
                overlay.Close();
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator AClientThatJoinsAfterTheBoardExists_GetsTheOverlay_AndCanVoteOnTheHostsVote()
        {
            int passed = 0;
            yield return StartServer();
            VoteBoard onHost = VoteBoard.Spawn(Server).AddKind(new VoteKind("reset", "Reset the level?") { Passed = _ => passed++ });
            yield return Join(Client);

            VoteBoard onClient = null;
            yield return WaitUntil(() => (onClient = VoteBoard.For(Client)) != null, "the client never got the vote board");
            VoteOverlay overlay = null;
            yield return WaitUntil(() => (overlay = VoteOverlay.For(onClient)) != null, "the client's board never got an overlay");

            onHost.StartVote("reset");
            yield return WaitUntil(() => overlay.Panel.Visible, "the client never saw the host's vote");
            Assert.That(overlay.YesButton.Interactable, Is.True, "the client can vote");

            onClient.CastVote(true); // the host's Yes plus the client's is the quorum of 2 of 2
            yield return WaitUntil(() => passed == 1, "the client's Yes never reached the server");
            yield return WaitUntil(() => !overlay.Panel.Visible, "the overlay never hid after the vote passed");
            Assert.That(UnityEngine.Object.FindObjectsByType<VoteOverlay>(UnityEngine.FindObjectsSortMode.None).Length, Is.EqualTo(2),
                "one overlay for the host's board and one for the client's, never two for the same board");
        }
    }
}
