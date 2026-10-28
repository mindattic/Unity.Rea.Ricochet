using System.Linq;
using NUnit.Framework;

namespace Ricochet.Core.Tests
{
    using Ricochet.Core.Art;
    using Ricochet.Core.Level;
    using Ricochet.Core.Match;

    // U.RICO-T52: the title's and the main menu's drifting letters (obj_letter_parent) and the Start screen's level settings
    // (the titlescreen room's setup panel), which every random round's seed carries to every peer.
    public class TitleAndStartTests
    {
        [Test]
        public void TheLettersStartWhereTheRoomsPlacedThem_AndDriftAPixelRightAndUpAStep()
        {
            var title = new TitleLetters(LetterRoom.Title);
            var menu = new TitleLetters(LetterRoom.MainMenu);
            Assert.That((title.X(0), title.Y(0), title.X(7), title.Y(7)), Is.EqualTo((64.0, 576.0, 512.0, 352.0)));
            Assert.That((menu.X(0), menu.Y(0), menu.X(7), menu.Y(7)), Is.EqualTo((128.0, 416.0, 576.0, 192.0)));
            Assert.That(Enumerable.Range(0, 8).Select(title.Sprite), Is.EqualTo(MenuLooks.TitleLetters), "moving right: their own letters");

            title.Step(10);
            Assert.That((title.X(0), title.Y(0)), Is.EqualTo((74.0, 566.0)));
            Assert.That(title.FloorOffset, Is.EqualTo(0), "the title's floor stands still");
            menu.Step(10);
            Assert.That(menu.FloorOffset, Is.EqualTo(10), "the main menu's floor scrolls a pixel a step");
        }

        [Test]
        public void ALetterTurnsBackPastAnEdge_AndShowsTheMirrorLetterMovingLeft()
        {
            var title = new TitleLetters(LetterRoom.Title);
            // T starts at y 352 going up: past the top (y < 0) after 353 steps, then it turns down.
            title.Step(353);
            Assert.That(title.Y(7), Is.EqualTo(-1));
            title.Step();
            Assert.That((title.VSpeed(7), title.Y(7)), Is.EqualTo((1.0, 0.0)));

            // R starts at x 64 going right: past the right edge (x > 1920) after 1857 steps, then it moves left as T.
            var r = new TitleLetters(LetterRoom.Title);
            r.Step(1857);
            Assert.That(r.X(0), Is.EqualTo(1921));
            Assert.That(r.Sprite(0), Is.EqualTo("spr_letter_R"));
            r.Step();
            Assert.That((r.HSpeed(0), r.X(0)), Is.EqualTo((-1.0, 1920.0)));
            Assert.That(r.Sprite(0), Is.EqualTo("spr_letter_T"), "R moving left shows T");
            Assert.That(r.Sprite(3), Is.EqualTo("spr_letter_O"));
        }

        [Test]
        public void AStockSeedIsTheStockLevel_AndASetupSeedCarriesTheStartScreensSettings()
        {
            Assert.That(LevelSeeds.Compose(12345, null), Is.EqualTo(12345));
            Assert.That(LevelSeeds.Compose(12345, LevelSettings.Default), Is.EqualTo(12345), "the stock settings keep the old seeds");
            Assert.That(LevelSeeds.Settings(12345).SameAs(LevelSettings.Default), Is.True);

            var setup = new MatchSetup { ShapeCount = 35, ShapeType = ShapeKind.Circles, Rotation = 45, Overlap = true };
            int seed = LevelSeeds.Compose(987654, setup.Level());
            Assert.That(seed, Is.LessThan(0));
            LevelSettings back = LevelSeeds.Settings(seed);
            Assert.That((back.PolygonCount, back.Shapes, back.Rotation, back.Overlap), Is.EqualTo((35, ShapeKind.Circles, 45.0, true)));
            Assert.That(LevelSeeds.Dice(seed), Is.EqualTo(987654 & ((1 << 21) - 1)));
        }

        [Test]
        public void TheStartScreensChoicesSnapToTheOriginalsSteps_TravelInTheVote_AndResetToTheDefaults()
        {
            var setup = new MatchSetup { ShapeCount = 23, Rotation = 80 };
            Assert.That((setup.ShapeCount, setup.Rotation), Is.EqualTo((25, 90)));
            setup.ShapeType = ShapeKind.Bars;
            setup.Overlap = true;

            MatchSetup back = MatchSetup.Parse(setup.AiCount, setup.Format());
            Assert.That((back.ShapeCount, back.ShapeType, back.Rotation, back.Overlap), Is.EqualTo((25, ShapeKind.Bars, 90, true)));
            Assert.That(back.SameAs(setup), Is.True);

            var stock = new MatchSetup();
            Assert.That((stock.ShapeCount, stock.ShapeType, stock.Rotation, stock.Overlap), Is.EqualTo((20, ShapeKind.Abstract, 360, false)),
                "the original's defaults: 20 abstract shapes, any rotation, no overlap");
            Assert.That(stock.Level().SameAs(LevelSettings.Default), Is.True);
        }
    }
}
