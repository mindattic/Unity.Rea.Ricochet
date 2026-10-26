// U.RICO-T20: a joined client sees the vote. The host's board exists before the client joins, so the client's copy spawns during the
// initial sync (before its session is live), the case the kit's auto-wire missed. ClientVoteOverlays.Ensure, which Game runs every
// frame, gives that board its overlay, and the overlay shows the host's vote and casts the client's Yes.
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
            yield return WaitUntil(() =>
            {
                ClientVoteOverlays.Ensure();
                return (overlay = VoteOverlay.For(onClient)) != null;
            }, "the client's board never got an overlay");

            onHost.StartVote("reset");
            yield return WaitUntil(() => overlay.Panel.Visible, "the client never saw the host's vote");
            Assert.That(overlay.YesButton.Interactable, Is.True, "the client can vote");

            onClient.CastVote(true); // the host's Yes plus the client's is the quorum of 2 of 2
            yield return WaitUntil(() => passed == 1, "the client's Yes never reached the server");
            yield return WaitUntil(() => !overlay.Panel.Visible, "the overlay never hid after the vote passed");
            Assert.That(ClientVoteOverlays.Ensure(), Is.EqualTo(0), "an overlay is never made twice");
        }
    }
}
