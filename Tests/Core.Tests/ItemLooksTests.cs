using System;
using NUnit.Framework;

namespace Ricochet.Core.Tests
{
    using Ricochet.Core.Arsenal;
    using Ricochet.Core.Art;

    // U.RICO-T79: the items in flight and the endgame room's camera flashes, held to the original's numbers.
    public class ItemLooksTests
    {
        [Test]
        public void TheItemsThatFly_FlyAtTheirMultipleOfBulletSpeed()
        {
            Assert.That(ItemLooks.SpeedFactor(Item.Teleporter), Is.EqualTo(2), "shoot_teleporter: bulletSpeed * 2");
            Assert.That(ItemLooks.SpeedFactor(Item.ShrinkRay), Is.EqualTo(4), "shoot_shrinkray: bulletSpeed * 4");
            Assert.That(ItemLooks.SpeedFactor(Item.Redirector), Is.EqualTo(2), "shoot_redirector: bulletSpeed * 2");
            foreach (Item item in new[] { Item.ExtraBounce, Item.ShieldWall, Item.AimAssist })
            {
                Assert.That(ItemLooks.Flies(item), Is.False);
                Assert.That(ItemLooks.SpeedFactor(item), Is.EqualTo(0));
                Assert.That(ItemLooks.FlightSeconds(item, 10, 5), Is.EqualTo(0));
            }

            Assert.That(ItemLooks.FlightSeconds(Item.ShrinkRay, 20, 5), Is.EqualTo(1).Within(1e-12), "20 units at four times 5 a second");
            Assert.That(ItemLooks.FlightSeconds(Item.Teleporter, 20, 5), Is.EqualTo(2).Within(1e-12));
        }

        [Test]
        public void TheShrinkRaysSparksAndFirework_AreLime()
        {
            Assert.That(ItemLooks.Lime.R, Is.EqualTo(128));
            Assert.That(ItemLooks.Lime.G, Is.EqualTo(255));
            Assert.That(ItemLooks.Lime.B, Is.EqualTo(128));
            Assert.That(ItemLooks.Lime.R | ItemLooks.Lime.G << 8 | ItemLooks.Lime.B << 16, Is.EqualTo(8454016), "action_effect's colour, BGR");
        }

        [Test]
        public void TheFlyingRedirector_GrowsFromFifteenToThirtyPercent_TurningThreeDegreesAStep()
        {
            Assert.That(ItemLooks.RedirectorFlightScaleAt(0), Is.EqualTo(0.15));
            Assert.That(ItemLooks.RedirectorFlightScaleAt(5), Is.EqualTo(0.2).Within(1e-12));
            Assert.That(ItemLooks.RedirectorFlightScaleAt(15), Is.EqualTo(0.3).Within(1e-12));
            Assert.That(ItemLooks.RedirectorFlightScaleAt(500), Is.EqualTo(0.3), "it stops growing at 0.30");
            Assert.That(ItemLooks.RedirectorFlightScaleAt(0), Is.LessThan(WeaponLooks.RedirectorStartScale), "standing, it opens out from where its flight left it");
            Assert.That(ItemLooks.RedirectorFlightAngle(45, 10), Is.EqualTo(75));
            Assert.That(ItemLooks.RedirectorFlightAngle(45, -1), Is.EqualTo(45));
        }

        [Test]
        public void AFlash_IsDestroyedOnlyInTheLowerHalfsMiddleThird()
        {
            Assert.That(ItemLooks.FlashKept(960, 900), Is.False, "on the pedestal");
            Assert.That(ItemLooks.FlashKept(960, 540), Is.True, "y > room_height / 2 only");
            Assert.That(ItemLooks.FlashKept(640, 900), Is.True, "x > room_width / 3 only");
            Assert.That(ItemLooks.FlashKept(1280, 900), Is.True, "x < room_width - room_width / 3 only");
            Assert.That(ItemLooks.FlashKept(641, 541), Is.False);
            Assert.That(ItemLooks.FlashKept(960, 100), Is.True, "over the crowd");
            Assert.That(ItemLooks.FlashKept(100, 1000), Is.True, "beside the pedestal");
        }

        [Test]
        public void TheEmitter_ThrowsFlashesAllOverTheRoom_NoneOnThePedestal()
        {
            var dice = new Random(3);
            int kept = 0;
            const int Steps = 6000;
            for (int i = 0; i < Steps; i++)
            {
                if (ItemLooks.NextFlash(dice, out int x, out int y))
                {
                    kept++;
                    Assert.That(x, Is.InRange(0, ItemLooks.RoomWidth));
                    Assert.That(y, Is.InRange(0, ItemLooks.RoomHeight));
                    Assert.That(y > 540 && x > 640 && x < 1280, Is.False);
                }
            }

            // A sixth of the room is the pedestal's: about five in six flashes stay.
            Assert.That(kept / (double)Steps, Is.EqualTo(5.0 / 6.0).Within(0.03));
        }
    }
}
