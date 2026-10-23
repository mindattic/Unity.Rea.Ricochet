using System.Linq;
using NUnit.Framework;

namespace Ricochet.Core.Tests
{
    using Ricochet.Core.Match;
    using Ricochet.Core.Menu;
    // UR-T14: the in-game votes: who a player may start a kick vote against (never themselves, the host or someone whose
    // seat only waits for them), what the kick room and the kick vote say, and the restart vote's line in each phase.
    public class VoteMenuTests
    {
        [Test]
        public void AKickVoteNamesAnyoneConnected_ButYouAndTheHost()
        {
            var roster = new[]
            {
                new VoteMember(0, "Hal", isHost: true),
                new VoteMember(1, "Ann", isLocal: true),
                new VoteMember(2, "Bob"),
                new VoteMember(3, "Cy", isConnected: false),
                new VoteMember(4, "Dee"),
            };

            var targets = VoteMenu.KickTargets(roster);

            Assert.That(targets.Select(t => t.Name), Is.EqualTo(new[] { "Bob", "Dee" }), "in roster order");
            Assert.That(targets.Select(t => t.ClientId), Is.EqualTo(new ulong[] { 2, 4 }));
        }

        [Test]
        public void TheHostMayVoteToKickEveryoneElse()
        {
            var roster = new[] { new VoteMember(0, "Hal", isHost: true, isLocal: true), new VoteMember(1, "Ann") };

            Assert.That(VoteMenu.KickTargets(roster).Single().Name, Is.EqualTo("Ann"));
        }

        [Test]
        public void AloneOrWithOnlyTheHost_ThereIsNobodyToKick()
        {
            Assert.That(VoteMenu.KickTargets(null), Is.Empty);
            Assert.That(VoteMenu.KickTargets(new[] { new VoteMember(0, "Hal", isHost: true, isLocal: true) }), Is.Empty);
            Assert.That(VoteMenu.KickTargets(new[] { new VoteMember(0, "Hal", isHost: true), new VoteMember(1, "Ann", isLocal: true) }), Is.Empty);
            Assert.That(VoteMenu.NobodyToKick, Does.Contain("host"));
        }

        [Test]
        public void TheKickRoomAndTheKickVote_SayWhatPassingDoes()
        {
            Assert.That(VoteMenu.KickId, Is.EqualTo("kick"), "the kit's own kind, so its checks and the overlay apply");
            Assert.That(VoteMenu.KickButton("Bob"), Is.EqualTo("Vote to kick Bob"));
            Assert.That(VoteMenu.KickButton(" "), Is.EqualTo("Vote to kick Player"));
            Assert.That(new VoteMember(5, null).Name, Is.EqualTo("Player"));
            Assert.That(VoteMenu.KickDescription(true), Does.Contain("seat drops out of the round"));
            Assert.That(VoteMenu.KickDescription(false), Does.Not.Contain("seat"));
            Assert.That(VoteMenu.KickedLine, Is.Not.Empty);
        }

        [Test]
        public void TheRestartVote_StartsTheMatchOver_ARematchOnceItIsOver_OrStandsTheDummiesUp()
        {
            Assert.That(VoteMenu.RestartTitle, Is.EqualTo("Restart the round?"));
            Assert.That(VoteMenu.RestartDescription(MatchPhase.Practice), Does.Contain("dummy"));
            Assert.That(VoteMenu.RestartDescription(MatchPhase.GameOver), Does.StartWith("A rematch"));
            foreach (MatchPhase phase in new[] { MatchPhase.Aiming, MatchPhase.ShotInFlight, MatchPhase.TurnOver, MatchPhase.RoundOver })
            {
                Assert.That(VoteMenu.RestartDescription(phase), Does.Contain("starts over"), phase.ToString());
            }
        }
    }
}
