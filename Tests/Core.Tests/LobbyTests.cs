using System.Linq;
using NUnit.Framework;

namespace Ricochet.Core.Tests
{
    // UR-T11: the host's lobby: how the sixteen seats fill as people join (everyone in the lobby, then as many of the AI
    // opponents asked for as still fit), whether Start can begin a match, and what the lobby's match box says.
    public class LobbyTests
    {
        [Test]
        public void PeopleSitFirst_ThenAsManyAiAsFit_UpToSixteen()
        {
            Assert.That(Lobby.HumanSeats(3), Is.EqualTo(3));
            Assert.That(Lobby.HumanSeats(20), Is.EqualTo(MatchRules.MaxSeats), "a seventeenth person watches");
            Assert.That(Lobby.HumanSeats(-1), Is.EqualTo(0));
            Assert.That(Lobby.AiSeats(3, 5), Is.EqualTo(5));
            Assert.That(Lobby.AiSeats(14, 5), Is.EqualTo(2));
            Assert.That(Lobby.AiSeats(16, 5), Is.EqualTo(0));
            Assert.That(Lobby.AiSeats(2, -4), Is.EqualTo(0));
        }

        [Test]
        public void AMatchNeedsTwoSeats_PeopleOrAi()
        {
            Assert.That(Lobby.CanStart(1, 0), Is.False, "the host alone");
            Assert.That(Lobby.CanStart(1, 1), Is.True, "the host and an AI");
            Assert.That(Lobby.CanStart(2, 0), Is.True, "two people");
            Assert.That(Lobby.CanStart(0, 1), Is.False, "nobody to play the AI");
            Assert.That(Lobby.CanStart(0, 2), Is.True, "two AI on a dedicated server");
        }

        [Test]
        public void TheSeatLineCountsPeopleAndAi_AndSaysWhatIsMissing()
        {
            Assert.That(Lobby.SeatLine(2, 3), Is.EqualTo("2 players and 3 AI opponents: 5 of 16 seats"));
            Assert.That(Lobby.SeatLine(1, 1), Is.EqualTo("1 player and 1 AI opponent: 2 of 16 seats"));
            Assert.That(Lobby.SeatLine(1, 0), Does.StartWith("1 player and no AI opponents: a match needs 2"));
            Assert.That(Lobby.SeatLine(15, 3), Is.EqualTo("15 players and 1 AI opponent: 16 of 16 seats (2 AI left out, the table is full)"));
        }

        [Test]
        public void ThePlanNamesTheMapAndGoal_TheSeats_TheAi_AndTheAnnouncer()
        {
            var setup = new MatchSetup { Map = 1, Goal = 1000, AiCount = 2, AiSkill = 7, AimAssists = 0, Announcements = 0 };
            string[] plan = Lobby.Plan(setup, 1).ToArray();
            Assert.That(plan, Is.EqualTo(new[]
            {
                Maps.At(1).Name + ", first to 1000 points",
                "1 player and 2 AI opponents: 3 of 16 seats",
                "AI skill 7 of 10, no aim assist",
                "No announcer",
            }));
            Assert.That(Lobby.Plan(null, 2)[1], Is.EqualTo(Lobby.SeatLine(2, new MatchSetup().AiCount)), "no setup: the defaults");
        }
    }
}
