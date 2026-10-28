// U.RICO-T56: the endgame room's podium as obj_endgame laid it out.
using NUnit.Framework;
using Ricochet.Core.Menu;

namespace Ricochet.Core.Tests
{
    public sealed class PodiumTests
    {
        [Test]
        public void TheFirstThreeStandCentreLeftAndRightOnThePedestal()
        {
            Assert.That(Podium.Pedestal.X, Is.EqualTo(960));
            Assert.That(Podium.Pedestal.Y, Is.EqualTo(864));
            Assert.That(Podium.Spot(0).X, Is.EqualTo(960));
            Assert.That(Podium.Spot(0).Y, Is.EqualTo(786));
            Assert.That(Podium.Spot(1).X, Is.EqualTo(871));
            Assert.That(Podium.Spot(1).Y, Is.EqualTo(831));
            Assert.That(Podium.Spot(2).X, Is.EqualTo(1049));
            Assert.That(Podium.Spot(2).Y, Is.EqualTo(880));
            Assert.That(() => Podium.Spot(3), Throws.InstanceOf<System.ArgumentOutOfRangeException>());
        }

        [TestCase(0, 0)]
        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(3, 3)]
        [TestCase(8, 3)]
        public void AtMostThreeStandOnIt(int seats, int expected)
        {
            Assert.That(Podium.OnPodium(seats), Is.EqualTo(expected));
        }

        [Test]
        public void OnlyTheChampionSpins_ADegreeAStepClockwiseFromNinety()
        {
            Assert.That(Podium.Angle(0, 0), Is.EqualTo(270));
            Assert.That(Podium.Angle(0, 1), Is.EqualTo(89));
            Assert.That(Podium.Angle(0, 90), Is.EqualTo(0));
            Assert.That(Podium.Angle(0, 91), Is.EqualTo(359));
            Assert.That(Podium.Angle(0, 450), Is.EqualTo(0));
            Assert.That(Podium.Angle(1, 500), Is.EqualTo(270));
            Assert.That(Podium.Angle(2, 500), Is.EqualTo(270));
        }

        [Test]
        public void TheLeaderboardRowsAreThirtyApartUnderTheHeadings()
        {
            Assert.That(Podium.HeaderY, Is.EqualTo(100));
            Assert.That(Podium.RowY(0), Is.EqualTo(130));
            Assert.That(Podium.RowY(7), Is.EqualTo(340));
            Assert.That(Podium.RankX, Is.EqualTo(760));
            Assert.That(Podium.ScoreX, Is.EqualTo(1160));
            Assert.That(Podium.ChampionName.Y, Is.EqualTo(508));
        }

        [Test]
        public void TheRoomPlaysTheVictoryTrackOnceAndTheCrowdCheers()
        {
            Assert.That(Podium.Track, Is.EqualTo(MusicTrack.Victory));
            Assert.That(Podium.TrackLoops, Is.False);
            Assert.That(Podium.Cheer, Is.EqualTo(new[] { SoundCue.Woot, SoundCue.Applause }));
        }
    }
}
