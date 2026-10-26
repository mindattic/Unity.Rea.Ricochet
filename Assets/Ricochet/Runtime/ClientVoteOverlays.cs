// A joined client's vote overlay (UR-T15 found none ever showed; U.RICO-T20). The kit auto-wires a VoteOverlay when a board spawns,
// but only if the peer already counts as a connected client at that moment; a joining client's copy of the board spawns during the
// initial sync, before its session is live, so it was skipped for good: votes reached the server but showed nowhere on the client.
// This runs every frame from Game and gives every board whose session is a live client the overlay it missed (Create is idempotent,
// so a board the kit already wired is left alone). A dedicated server or a bot has no UI to show, and is never a client.
using Unity.Multiplayer;
using Unity.Multiplayer.UI;

namespace Ricochet
{
    public static class ClientVoteOverlays
    {
        /// <summary>Gives every board on a live client session an overlay; returns how many it made.</summary>
        public static int Ensure()
        {
            int made = 0;
            var boards = VoteBoards.All;
            for (int i = 0; i < boards.Count; i++)
            {
                IVoteBoard board = boards[i];
                if (board == null || !board.IsSpawned || board.Session == null || !board.Session.IsClient || VoteOverlay.For(board) != null)
                {
                    continue;
                }

                VoteOverlay.Create(board);
                made++;
            }

            return made;
        }
    }
}
