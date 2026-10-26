using System.Linq;
using NUnit.Framework;

namespace Ricochet.Core.Tests
{
    using Ricochet.Core.Arsenal;
    using Ricochet.Core.Art;

    // U.RICO-T49: the HUD details as the original drew them (obj_icon_weapon_*, obj_item_*, setup_weapons / setup_items,
    // draw_angle and obj_degree, obj_guide, obj_healthbar) and the icons' hover text from arsenal.json.
    public class HudLooksTests
    {
        [Test]
        public void TheIconsSitInTheOriginalsTwoRows_BottomLeft()
        {
            // The room's instances: the weapons at y 1024 and the items at 1056 for the first, as their Create events set
            // them (x = border, border*2 + 10, border*3 + 20, ...).
            Assert.That(Enumerable.Range(0, 6).Select(HudLooks.IconX), Is.EqualTo(new[] { 32, 74, 116, 158, 200, 242 }));
            Assert.That(HudLooks.WeaponRowY, Is.EqualTo(1016));
            Assert.That(HudLooks.ItemRowY, Is.EqualTo(1048));
        }

        [Test]
        public void AnIconIsOffAtAFifthWithNone_PlainWhenCarried_AndSelectedWhenItIsTheOneInHand()
        {
            Assert.That(HudLooks.State(0, false), Is.EqualTo(IconState.Off));
            Assert.That(HudLooks.State(3, false), Is.EqualTo(IconState.Normal));
            Assert.That(HudLooks.State(3, true), Is.EqualTo(IconState.Selected));
            Assert.That(HudLooks.Alpha(0), Is.EqualTo(0.2));
            Assert.That(HudLooks.Alpha(1), Is.EqualTo(1));

            Assert.That(HudLooks.WeaponIcon(Weapon.Cannon, IconState.Off), Is.EqualTo("spr_icon_weapon_cannon_off"));
            Assert.That(HudLooks.WeaponIcon(Weapon.Vortex, IconState.Selected), Is.EqualTo("spr_icon_weapon_vortex_selected"));
            Assert.That(HudLooks.ItemIcon(Item.AimAssist, IconState.Normal), Is.EqualTo("spr_item_aimassist"));
            Assert.That(HudLooks.ItemIcon(Item.Redirector, IconState.Off), Is.EqualTo("spr_item_redirector_off"));
        }

        [Test]
        public void TheCountShowsOnlyForOneCarried_AndAnUnlimitedOneIsTheInfinitySign()
        {
            Assert.That(HudLooks.ShowsCount(0), Is.False);
            Assert.That(HudLooks.CountText(0), Is.Empty);
            Assert.That(HudLooks.CountText(3), Is.EqualTo("3"));
            Assert.That(HudLooks.ShowsInfinity(ArsenalRules.Unlimited), Is.True);
            Assert.That(HudLooks.CountText(ArsenalRules.Unlimited), Is.Empty, "spr_infinity stands in for the figure");
            Assert.That(HudLooks.ShowsInfinity(ArsenalRules.MaxCount), Is.False);
        }

        [Test]
        public void TheAngleReadsInWholeDegrees_UnderATankAimingUp_OverOneAimingDown()
        {
            Assert.That(HudLooks.AngleText(89.6), Is.EqualTo("90"));
            Assert.That(HudLooks.AngleText(-1), Is.EqualTo("359"));
            Assert.That(HudLooks.AngleText(359.7), Is.EqualTo("0"));
            Assert.That(HudLooks.AngleOffsetY(90), Is.EqualTo(36), "y + 36: under the tank");
            Assert.That(HudLooks.AngleOffsetY(270), Is.EqualTo(-60), "y - 60: over it");
            Assert.That(HudLooks.DegreeX(30), Is.EqualTo(35), "obj_degree: string_width / 2 + 20");
        }

        [Test]
        public void TheGuideShowsUntilThePlayerDrives_TwoRingsInTheirColour_TwoInWhite()
        {
            Assert.That(HudLooks.GuideShows(true, false), Is.True);
            Assert.That(HudLooks.GuideShows(true, true), Is.False, "obj_guide: !hasMoved");
            Assert.That(HudLooks.GuideShows(false, false), Is.False);
            GuideRing[] rings = HudLooks.GuideRings;
            Assert.That(rings.Where(r => !r.White).Select(r => r.Radius), Is.EquivalentTo(new[] { 60, 61 }));
            Assert.That(rings.Where(r => r.White).Select(r => r.Radius), Is.EquivalentTo(new[] { 59, 62 }));
            Assert.That(rings.Where(r => !r.White).All(r => r.Alpha == 0.7), Is.True);
            Assert.That(rings.Where(r => r.White).All(r => r.Alpha == 0.4), Is.True);
        }

        [Test]
        public void TheHealthBarSaysShownOverMax_AndFillsToTheShare()
        {
            Assert.That(HudLooks.HealthText(73, 100), Is.EqualTo("73/100"));
            Assert.That(HudLooks.HealthFill(50, 100), Is.EqualTo(0.5));
            Assert.That(HudLooks.HealthFill(-4, 100), Is.EqualTo(0));
            Assert.That(HudLooks.HealthFill(130, 100), Is.EqualTo(1));
        }

        [Test]
        public void EveryWeaponAndItemSaysWhatItDoes_OnHover()
        {
            for (int i = 0; i < ArsenalRules.Kinds; i++)
            {
                Assert.That(ArsenalRules.About((Weapon)i), Is.Not.Empty, ((Weapon)i).ToString());
                Assert.That(ArsenalRules.About((Item)i), Is.Not.Empty, ((Item)i).ToString());
            }

            Assert.That(HudLooks.Tooltip("Grenade", "Bounces."), Is.EqualTo("Grenade: Bounces."));
            Assert.That(HudLooks.Tooltip("Grenade", string.Empty), Is.EqualTo("Grenade"));
            Assert.That(HudLooks.ExtraBounceShows(0), Is.False);
            Assert.That(HudLooks.ExtraBounceShows(1), Is.True);
        }
    }
}
