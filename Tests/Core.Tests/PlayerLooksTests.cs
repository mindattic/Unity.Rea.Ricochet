using NUnit.Framework;

namespace Ricochet.Core.Tests
{
    // UR-T21: two default profiles were both "Default" in teal everywhere.
    public class PlayerLooksTests
    {
        [Test]
        public void TwoIdenticalProfiles_GetDistinctColoursAndNames()
        {
            var r = PlayerLooks.Resolve(new[] { new PlayerLook("Default", 0x00B4B4), new PlayerLook("Default", 0x00B4B4), new PlayerLook("AI 1", 0xE6194B) });
            Assert.That(r[0].Name, Is.EqualTo("Default"));
            Assert.That(r[1].Name, Is.EqualTo("Default 2"));
            Assert.That(r[0].Rgb, Is.EqualTo(0x00B4B4));
            Assert.That(r[1].Rgb, Is.Not.EqualTo(r[0].Rgb));
            Assert.That(r[1].Rgb, Is.Not.EqualTo(r[2].Rgb));
        }

        [Test]
        public void DistinctProfiles_AreUntouched()
        {
            var r = PlayerLooks.Resolve(new[] { new PlayerLook("Ann", 0xFF0000), new PlayerLook("Bob", 0x0000FF) });
            Assert.That(r[0].Name, Is.EqualTo("Ann"));
            Assert.That(r[1].Rgb, Is.EqualTo(0x0000FF));
        }

        [Test]
        public void SixteenIdentical_AllDistinct()
        {
            var wanted = new PlayerLook[16];
            for (int i = 0; i < 16; i++)
            {
                wanted[i] = new PlayerLook("Default", 0x00B4B4);
            }

            var r = PlayerLooks.Resolve(wanted);
            for (int i = 0; i < 16; i++)
            {
                for (int j = i + 1; j < 16; j++)
                {
                    Assert.That(r[i].Rgb, Is.Not.EqualTo(r[j].Rgb));
                    Assert.That(r[i].Name, Is.Not.EqualTo(r[j].Name));
                }
            }
        }

        [Test]
        public void SuffixAvoidsAnExistingName()
        {
            var r = PlayerLooks.Resolve(new[] { new PlayerLook("Default", 1), new PlayerLook("Default", 1), new PlayerLook("Default 2", 0x00FF00) });
            Assert.That(r[1].Name, Is.EqualTo("Default 3"));
        }
    }
}
