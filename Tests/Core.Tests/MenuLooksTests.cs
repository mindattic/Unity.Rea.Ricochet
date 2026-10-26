using NUnit.Framework;

namespace Ricochet.Core.Tests
{
    using Ricochet.Core.Art;

    // U.RICO-T78: the menus' and the game screen's buttons as the original drew them (obj_btn_*, the less / more arrows,
    // obj_checkbox_*, obj_inputbox*, the tiny buttons and arrows) and the steppers' wrap.
    public class MenuLooksTests
    {
        [Test]
        public void EveryPlateDrawsItsOverOrBackSpriteAtHalf_UnderItsFace()
        {
            Assert.That(MenuLooks.BackAlpha, Is.EqualTo(0.5));
            Assert.That(MenuLooks.Face(Plate.Button), Is.EqualTo("spr_button"));
            Assert.That(MenuLooks.Back(Plate.Button, over: true), Is.EqualTo("spr_button_over"));
            Assert.That(MenuLooks.Back(Plate.Button, over: false), Is.EqualTo("spr_button_back"));
            Assert.That(MenuLooks.Back(Plate.ArrowLeft, true), Is.EqualTo("spr_arrowleft_over"));
            Assert.That(MenuLooks.Back(Plate.ArrowRight, false), Is.EqualTo("spr_arrowright_back"));
            Assert.That(MenuLooks.Back(Plate.TinyButton, true), Is.EqualTo("spr_tinybutton_over"));
            Assert.That(MenuLooks.Back(Plate.TinyArrowUp, false), Is.EqualTo("spr_tinyarrowup_back"));
            Assert.That(MenuLooks.Face(Plate.TinyArrowDown), Is.EqualTo("spr_tinyarrowdown"));
            Assert.That(MenuLooks.Face(Plate.TinyArrowLeft), Is.EqualTo("spr_tinyarrowleft"));
            Assert.That(MenuLooks.Face(Plate.TinyArrowRight), Is.EqualTo("spr_tinyarrowright"));
        }

        [Test]
        public void ACheckboxShowsItsTick_AndAnInputBoxHasNoHover()
        {
            Assert.That(MenuLooks.Face(Plate.Checkbox, isChecked: true), Is.EqualTo("spr_checkbox_checked"));
            Assert.That(MenuLooks.Face(Plate.Checkbox, isChecked: false), Is.EqualTo("spr_checkbox_unchecked"));
            Assert.That(MenuLooks.Back(Plate.Checkbox, true), Is.EqualTo("spr_checkbox_over"));
            Assert.That(MenuLooks.Back(Plate.Checkbox, false), Is.EqualTo("spr_checkbox_back"));
            Assert.That(MenuLooks.Face(Plate.InputBox), Is.EqualTo("spr_inputbox"));
            Assert.That(MenuLooks.Back(Plate.InputBox, true), Is.EqualTo("spr_inputbox_back"));
            Assert.That(MenuLooks.Back(Plate.InputBox, false), Is.EqualTo("spr_inputbox_back"));
        }

        [Test]
        public void TheLabelsAreInTheOriginalsFaces()
        {
            Assert.That(MenuLooks.ButtonFont, Is.EqualTo("fnt_mono"));
            Assert.That(MenuLooks.ButtonOutline, Is.EqualTo(2));
            Assert.That(MenuLooks.CheckboxFont, Is.EqualTo("fnt_score"));
            Assert.That(MenuLooks.CheckboxOutline, Is.EqualTo(2));
            Assert.That(MenuLooks.TinyFont, Is.EqualTo("fnt_count"));
            Assert.That(MenuLooks.TitleFont, Is.EqualTo("fnt_impact"));
        }

        [Test]
        public void AnArrowStepsOneNotch_WrappingPastEitherEnd()
        {
            // obj_btn_bounces_more: maxBounces += 1; if > 20 -> 1. obj_btn_cannon_less: 0 -> 10.
            Assert.That(MenuLooks.Step(5, 1, 1, 20), Is.EqualTo(6));
            Assert.That(MenuLooks.Step(20, 1, 1, 20), Is.EqualTo(1));
            Assert.That(MenuLooks.Step(0, -1, 0, 10), Is.EqualTo(10));
            Assert.That(MenuLooks.Step(10, -1, 0, 10), Is.EqualTo(9));
            Assert.That(MenuLooks.Step(0.95, 0.05, 0, 1), Is.EqualTo(1).Within(1e-9));
            Assert.That(MenuLooks.Step(3, 1, 3, 3), Is.EqualTo(3));
            Assert.That(MenuLooks.Notch(0, 100, wholeNumbers: true), Is.EqualTo(1));
            Assert.That(MenuLooks.Notch(0, 1, wholeNumbers: false), Is.EqualTo(0.05).Within(1e-9));
        }

        [Test]
        public void TheTitleSpellsRicochet()
        {
            Assert.That(string.Concat(System.Array.ConvertAll(MenuLooks.TitleLetters, s => s.Substring(s.Length - 1))), Is.EqualTo("RICOCHET"));
        }
    }
}
