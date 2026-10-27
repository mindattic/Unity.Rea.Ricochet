using NUnit.Framework;

namespace Ricochet.Core.Tests
{
    using Ricochet.Core.Art;

    // U.RICO-T84: where the original's menu rooms put their controls.
    public class RoomLayoutTests
    {
        [Test]
        public void TheCornersAndTheMainMenuColumnAreWhereTheRoomsPutThem()
        {
            Assert.That(RoomLayout.Back.ToString(), Is.EqualTo("128,1024 226x64"));
            Assert.That(RoomLayout.Primary.ToString(), Is.EqualTo("1792,1024 226x64"));
            Assert.That(RoomLayout.Reset.ToString(), Is.EqualTo("1792,928 226x64"));
            Assert.That(RoomLayout.Back.Left, Is.EqualTo(15));
            for (int i = 0; i < 5; i++)
            {
                RoomRect slot = RoomLayout.ButtonColumn(i, 5);
                Assert.That(slot.X, Is.EqualTo(960));
                Assert.That(slot.Y, Is.EqualTo(352 + 96 * i), "button " + i);
            }
        }

        [Test]
        public void ARowIsAsWideAsItsTwoArrowsAndTheGap()
        {
            Assert.That(RoomLayout.RowWidth, Is.EqualTo(409));
            Assert.That(RoomLayout.SlideLast.X, Is.EqualTo(512));
            Assert.That(RoomLayout.SlideNext.X, Is.EqualTo(1280));
        }

        [Test]
        public void ItemsFlowDownThePlayersRoomsRowsAndWrapToANewColumnWhereTheRoomRunsOut()
        {
            var heights = new System.Collections.Generic.List<double>();
            for (int i = 0; i < 4; i++)
            {
                heights.Add(64);
            }

            FlowPlan one = RoomLayout.Flow(heights, 0, 240, 1000, 864, 1);
            Assert.That(one, Is.Not.Null);
            Assert.That(one.Gap, Is.EqualTo(16));
            Assert.That(one.Slots[1].Top, Is.EqualTo(240 + 64 + 16));
            Assert.That(one.ColumnCentres, Is.EqualTo(new[] { 500.0 }));

            for (int i = 0; i < 6; i++)
            {
                heights.Add(64);
            }

            Assert.That(RoomLayout.Flow(heights, 0, 240, 1000, 864, 1), Is.Null, "ten rows are too tall for one column");
            FlowPlan two = RoomLayout.Flow(heights, 0, 240, 1000, 864, 2);
            Assert.That(two, Is.Not.Null);
            Assert.That(two.Slots[7].Column, Is.EqualTo(0), "eight 80 px rows fill 240 ... 864");
            Assert.That(two.Slots[8].Column, Is.EqualTo(1));
            Assert.That(two.Slots[8].Top, Is.EqualTo(240));
            Assert.That(two.Slots[9].Top, Is.EqualTo(240 + 64 + 16));
            Assert.That(two.ColumnCentres, Is.EqualTo(new[] { 250.0, 750.0 }));
        }

        [Test]
        public void AScreenTooLongForTheColumnsGetsNoPlan()
        {
            var heights = new System.Collections.Generic.List<double>();
            for (int i = 0; i < 100; i++)
            {
                heights.Add(64);
            }

            Assert.That(RoomLayout.Flow(heights, 0, 240, 1000, 864, 4), Is.Null);
        }
    }
}
