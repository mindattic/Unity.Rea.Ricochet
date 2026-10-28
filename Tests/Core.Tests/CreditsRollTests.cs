using System.Linq;
using NUnit.Framework;

namespace Ricochet.Core.Tests
{
    using Ricochet.Core.Menu;

    // U.RICO-T55: the credits roll climbs over spr_creditsimage from just under its bottom edge until the last line has left
    // its top, a pixel a step at 60 steps a second by default, then starts over.
    public class CreditsRollTests
    {
        [Test]
        public void TheRollStartsUnderThePicture_ClimbsAtItsSpeed_AndStartsOverOnceGone()
        {
            const double window = 714, content = 900, speed = CreditsRoll.DefaultSpeed;
            Assert.That(CreditsRoll.Top(0, speed, window, content), Is.EqualTo(window), "the first line waits just under the bottom edge");
            Assert.That(CreditsRoll.Top(1, speed, window, content), Is.EqualTo(window - 60).Within(1e-9), "a pixel a step at 60 steps a second");
            double period = CreditsRoll.Period(speed, window, content);
            Assert.That(period, Is.EqualTo((window + content) / speed).Within(1e-9));
            Assert.That(CreditsRoll.Top(period - 1e-6, speed, window, content), Is.EqualTo(-content).Within(1e-3), "the last line has just left the top");
            Assert.That(CreditsRoll.Top(period + 1, speed, window, content), Is.EqualTo(window - 60).Within(1e-6), "then it starts over");
        }

        [Test]
        public void NothingMoves_WithoutASpeedOrAnythingToRoll()
        {
            Assert.That(CreditsRoll.Top(5, 0, 714, 900), Is.EqualTo(0));
            Assert.That(CreditsRoll.Top(5, 60, 0, 0), Is.EqualTo(0));
            Assert.That(CreditsRoll.Period(0, 714, 900), Is.EqualTo(0));
        }

        [Test]
        public void TheLinesListEachHeadingBeforeItsLines()
        {
            var sections = new[] { new CreditsSection("Music", "Kevin MacLeod"), new CreditsSection("Thanks for playing") };
            Assert.That(CreditsRoll.Lines(sections).ToArray(), Is.EqualTo(new[] { "Music", "Kevin MacLeod", "Thanks for playing" }));
        }
    }
}
