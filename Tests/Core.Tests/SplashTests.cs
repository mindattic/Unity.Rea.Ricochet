using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Ricochet.Core.Tests
{
    using Ricochet.Core.Menu;

    // U.RICO-T57: the splash and the startup credits rooms' step logic.
    public class SplashTests
    {
        [Test]
        public void PictureStartsShowingAtOnceWithTheStartupSound()
        {
            var reveal = new SplashReveal(new Random(1));
            var sounds = new List<SoundCue>();
            reveal.Step(sounds);
            Assert.That(sounds, Does.Contain(SoundCue.Startup));
            Assert.That(reveal.Alpha, Is.GreaterThan(0));
        }

        [Test]
        public void PictureIsWholeThenHoldsThenLeaves()
        {
            var reveal = new SplashReveal(new Random(2));
            int steps = 0;
            while (!reveal.Leaving && steps < 10000)
            {
                reveal.Step(null);
                steps++;
            }

            Assert.That(reveal.Whole, Is.True);
            Assert.That(steps, Is.EqualTo(SplashReveal.FadeSteps * SplashReveal.FadeDelay + SplashReveal.HoldSteps + 1).Within(SplashReveal.FadeDelay + 1));
        }

        [Test]
        public void APressFinishesThePictureAndTheRoomLeaves()
        {
            var reveal = new SplashReveal(new Random(3));
            Assert.That(reveal.Press(), Is.False, "nothing to finish before the picture starts");
            reveal.Step(null);
            Assert.That(reveal.Press(), Is.True);
            reveal.Step(null);
            Assert.That(reveal.Whole && reveal.Leaving, Is.True);
        }

        [Test]
        public void CardTremblesNearTheMiddle()
        {
            var card = new LotusCard(new Random(4));
            for (int i = 0; i < 1000; i++)
            {
                card.Step();
                Assert.That(Math.Abs(card.X - SplashRoom.Middle.X), Is.LessThanOrEqualTo(LotusCard.Range));
                Assert.That(Math.Abs(card.Y - SplashRoom.Middle.Y), Is.LessThanOrEqualTo(LotusCard.Range));
            }
        }

        [Test]
        public void CreditsTypeTheNoteAndGoOnAfterTenSecondsOrAKey()
        {
            var credits = new StartupCredits("abcd");
            for (int i = 0; i < 8; i++)
            {
                credits.Step();
            }

            Assert.That(credits.Typing.Done, Is.True);
            Assert.That(credits.Leaving, Is.False);
            credits.Press("F1");
            credits.Press("F12");
            Assert.That(credits.Leaving, Is.False);
            credits.Press("Space");
            Assert.That(credits.Leaving, Is.True);

            var waiting = new StartupCredits();
            for (int i = 0; i < SplashRoom.CreditsSteps; i++)
            {
                waiting.Step();
            }

            Assert.That(waiting.Leaving, Is.True);
        }
    }
}
