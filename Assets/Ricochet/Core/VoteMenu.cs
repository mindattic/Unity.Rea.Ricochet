// The in-game votes (UR-T14): what the restart and kick votes say and who a player may vote to kick. The 2013 original
// (design reference only, UR-LAW-2) had no votes: one keyboard was passed around, so whoever held it restarted or quit.
// Over the network the kit's NetVotes runs them instead: anyone may start one from the in-game menu, the kit's vote overlay
// asks everyone else Yes or No with the tally and the clock, and it passes on two thirds of the voters. The restart vote
// starts the match over (a rematch once a game is over, the dummies back up in practice); the kick vote disconnects a
// player, and in a match their seat drops out of the round as if they had left. Who can be kicked is the kit's rule:
// anyone connected but yourself and the host, and never an AI (it is no connection; set the AI count on Players instead).
using System.Collections.Generic;

namespace Ricochet.Core
{
    /// <summary>One player in the roster, as the kick room sees them.</summary>
    public readonly struct VoteMember
    {
        public VoteMember(ulong clientId, string name, bool isHost = false, bool isLocal = false, bool isConnected = true)
        {
            ClientId = clientId;
            Name = string.IsNullOrWhiteSpace(name) ? "Player" : name;
            IsHost = isHost;
            IsLocal = isLocal;
            IsConnected = isConnected;
        }

        /// <summary>Their connection's client id: what the kick vote names.</summary>
        public ulong ClientId { get; }

        /// <summary>The name everyone sees.</summary>
        public string Name { get; }

        /// <summary>The host's own player (the kit never kicks the host).</summary>
        public bool IsHost { get; }

        /// <summary>This peer's own player (nobody votes to kick themselves).</summary>
        public bool IsLocal { get; }

        /// <summary>False while they have dropped and their seat waits for them (there is no connection to remove).</summary>
        public bool IsConnected { get; }
    }

    public static class VoteMenu
    {
        /// <summary>The kick vote's id: the kit's own (<c>VoteBoard.KickKindId</c>), so the overlay and the kit's checks are the same.</summary>
        public const string KickId = "kick";

        /// <summary>The in-game menu's button for the kick room, and the room's title.</summary>
        public const string KickRoom = "Vote to kick";

        /// <summary>The in-game menu's restart button.</summary>
        public const string RestartButton = "Vote to restart";

        /// <summary>What a player removed by a kick vote is told.</summary>
        public const string KickedLine = "The other players voted to remove you from the game.";

        /// <summary>The kick room's line when there is someone to kick.</summary>
        public const string KickSubtitle = "Pick a player: everyone else votes, and if two thirds say Yes they are removed from the game.";

        /// <summary>The kick room's line when there is nobody to kick.</summary>
        public const string NobodyToKick = "There is nobody else here to vote out: the host can't be kicked, and neither can you.";

        /// <summary>
        /// The players this peer may start a kick vote against, in roster order: everyone connected except this peer's own
        /// player and the host.
        /// </summary>
        public static IReadOnlyList<VoteMember> KickTargets(IEnumerable<VoteMember> roster)
        {
            var targets = new List<VoteMember>();
            if (roster == null)
            {
                return targets;
            }

            foreach (VoteMember member in roster)
            {
                if (!member.IsLocal && !member.IsHost && member.IsConnected)
                {
                    targets.Add(member);
                }
            }

            return targets;
        }

        /// <summary>The kick room's button for <paramref name="name"/>.</summary>
        public static string KickButton(string name) => "Vote to kick " + (string.IsNullOrWhiteSpace(name) ? "Player" : name);

        /// <summary>The kick vote's second line, under the kit's "Kick Bob?".</summary>
        public static string KickDescription(bool inMatch) => inMatch
            ? "They are removed from the game: their seat drops out of the round, and the turn passes if it was theirs."
            : "They are removed from the game.";

        /// <summary>The restart vote's question.</summary>
        public const string RestartTitle = "Restart the round?";

        /// <summary>
        /// The restart vote's second line: in a match it starts over (a rematch once the game is over), in practice the
        /// dummies stand back up.
        /// </summary>
        public static string RestartDescription(MatchPhase phase)
        {
            switch (phase)
            {
                case MatchPhase.Practice:
                    return "Every practice dummy stands back up and the level is laid again.";
                case MatchPhase.GameOver:
                    return "A rematch: the same players, map and goal, from round one.";
                default:
                    return "Everyone back to full health; the match starts over.";
            }
        }
    }
}
