using NUnit.Framework;

namespace Ricochet.Core.Tests
{
    using Ricochet.Core.Arsenal;
    using Ricochet.Core.Level;
    public class LoadoutTests
    {
        [Test]
        public void APlayerStartsWithAnUnlimitedCannon_ThreeOfEveryWeaponInPlay_AndNoneOfTheRest()
        {
            var loadout = new Loadout();
            Assert.That(loadout.Weapon, Is.EqualTo(Weapon.Cannon));
            Assert.That(loadout.Count(Weapon.Cannon), Is.EqualTo(ArsenalRules.Unlimited));
            Assert.That(loadout.Count(Weapon.Laser), Is.EqualTo(3));
            Assert.That(loadout.Count(Weapon.Grenade), Is.EqualTo(3));
            Assert.That(loadout.Count(Weapon.Vortex), Is.EqualTo(3), "the vortex came into play with its detonation (UR-T6)");
            Assert.That(loadout.Item, Is.EqualTo(Item.ExtraBounce));
            Assert.That(loadout.Count(Item.ExtraBounce), Is.EqualTo(3));
            Assert.That(loadout.Count(Item.ShieldWall), Is.EqualTo(3));
            Assert.That(loadout.Count(Item.Redirector), Is.EqualTo(3));
            Assert.That(loadout.Count(Item.AimAssist), Is.EqualTo(3), "count_AimAssist = 3 (UR-T7)");
            Assert.That(loadout.Fuse, Is.EqualTo(2));
        }

        [Test]
        public void CyclingRightAndLeftWalksTheWeaponsInOrder_SkippingWhatThePlayerHasNoneOf()
        {
            var rules = new ArsenalRules();
            rules.SetStartCount(Weapon.Vortex, 0);
            var loadout = new Loadout(rules);
            Assert.That(loadout.CycleWeapon(1), Is.True);
            Assert.That(loadout.Weapon, Is.EqualTo(Weapon.Laser));
            loadout.CycleWeapon(1);
            loadout.CycleWeapon(1);
            loadout.CycleWeapon(1);
            Assert.That(loadout.Weapon, Is.EqualTo(Weapon.Railgun));
            loadout.CycleWeapon(1);
            Assert.That(loadout.Weapon, Is.EqualTo(Weapon.Cannon), "past the railgun the vortex (none) is skipped and it wraps");
            loadout.CycleWeapon(-1);
            Assert.That(loadout.Weapon, Is.EqualTo(Weapon.Railgun), "left from the cannon wraps the other way");
        }

        [Test]
        public void CyclingWithNothingElseCarriedKeepsTheSelection()
        {
            var rules = new ArsenalRules();
            foreach (Weapon w in new[] { Weapon.Laser, Weapon.Shotgun, Weapon.Grenade, Weapon.Railgun, Weapon.Vortex })
            {
                rules.SetStartCount(w, 0);
            }

            var loadout = new Loadout(rules);
            Assert.That(loadout.CycleWeapon(1), Is.False);
            Assert.That(loadout.CycleWeapon(-1), Is.False);
            Assert.That(loadout.Weapon, Is.EqualTo(Weapon.Cannon));
        }

        [Test]
        public void ItemsCycleTheSameWay()
        {
            var rules = new ArsenalRules();
            rules.SetStartCount(Item.ShieldWall, 0);
            rules.SetStartCount(Item.Redirector, 0);
            var loadout = new Loadout(rules);
            loadout.CycleItem(1);
            Assert.That(loadout.Item, Is.EqualTo(Item.ShrinkRay), "the shield wall (none) is skipped");
            loadout.CycleItem(1);
            Assert.That(loadout.Item, Is.EqualTo(Item.Teleporter));
            loadout.CycleItem(1);
            Assert.That(loadout.Item, Is.EqualTo(Item.AimAssist));
            loadout.CycleItem(1);
            Assert.That(loadout.Item, Is.EqualTo(Item.ExtraBounce), "past aim assist the redirector (none) is skipped and it wraps");
            loadout.CycleItem(-1);
            Assert.That(loadout.Item, Is.EqualTo(Item.AimAssist));
        }

        [Test]
        public void FiringSpendsOne_AnUnlimitedCannonNever_AndSpendingTheLastMovesTheSelectionRight()
        {
            var loadout = new Loadout();
            for (int i = 0; i < 20; i++)
            {
                Assert.That(loadout.Fire(), Is.EqualTo(Weapon.Cannon));
            }

            Assert.That(loadout.Count(Weapon.Cannon), Is.EqualTo(ArsenalRules.Unlimited));

            loadout.CycleWeapon(1);
            Assert.That(loadout.Fire(), Is.EqualTo(Weapon.Laser));
            Assert.That(loadout.Fire(), Is.EqualTo(Weapon.Laser));
            Assert.That(loadout.Weapon, Is.EqualTo(Weapon.Laser));
            Assert.That(loadout.Fire(), Is.EqualTo(Weapon.Laser));
            Assert.That(loadout.Count(Weapon.Laser), Is.EqualTo(0));
            Assert.That(loadout.Weapon, Is.EqualTo(Weapon.Shotgun), "the last laser spent moves on to the next weapon carried");
        }

        [Test]
        public void WithNothingAtAllThereIsNothingToFire()
        {
            var rules = new ArsenalRules();
            for (int i = 0; i < ArsenalRules.Kinds; i++)
            {
                rules.SetStartCount((Weapon)i, 0);
                rules.SetStartCount((Item)i, 0);
            }

            var loadout = new Loadout(rules);
            Assert.That(loadout.Fire(), Is.Null);
            Assert.That(loadout.Use(), Is.Null);
        }

        [Test]
        public void UsingTheLastItemMovesTheItemSelectionRight()
        {
            var loadout = new Loadout();
            Assert.That(loadout.Use(), Is.EqualTo(Item.ExtraBounce));
            Assert.That(loadout.Use(), Is.EqualTo(Item.ExtraBounce));
            Assert.That(loadout.Use(), Is.EqualTo(Item.ExtraBounce));
            Assert.That(loadout.Item, Is.EqualTo(Item.ShieldWall));
            Assert.That(loadout.Count(Item.ExtraBounce), Is.EqualTo(0));
        }

        [Test]
        public void PickingUpAddsUpToNine_AndAnUnlimitedCountStaysUnlimited()
        {
            var loadout = new Loadout();
            loadout.Add(Weapon.Grenade, 4);
            Assert.That(loadout.Count(Weapon.Grenade), Is.EqualTo(7));
            loadout.Add(Weapon.Grenade, 5);
            Assert.That(loadout.Count(Weapon.Grenade), Is.EqualTo(ArsenalRules.MaxCount), "never counted up into unlimited");
            loadout.Add(Weapon.Cannon);
            Assert.That(loadout.Count(Weapon.Cannon), Is.EqualTo(ArsenalRules.Unlimited));
            loadout.Add(Item.AimAssist);
            Assert.That(loadout.Count(Item.AimAssist), Is.EqualTo(4));
        }

        [Test]
        public void TheFuseKeysStepByHalfSecondsBetweenHalfASecondAndSix()
        {
            var loadout = new Loadout();
            for (int i = 0; i < 10; i++)
            {
                loadout.ShortenFuse();
            }

            Assert.That(loadout.Fuse, Is.EqualTo(0.5));
            loadout.LengthenFuse();
            Assert.That(loadout.Fuse, Is.EqualTo(1.0));
            loadout.MaxOutFuse();
            Assert.That(loadout.Fuse, Is.EqualTo(6));
            loadout.LengthenFuse();
            Assert.That(loadout.Fuse, Is.EqualTo(6));
            loadout.SetFuse(2.3);
            Assert.That(loadout.Fuse, Is.EqualTo(2.5), "snapped to a half-second step");
            loadout.SetFuse(double.NaN);
            Assert.That(loadout.Fuse, Is.EqualTo(2));
        }

        [Test]
        public void ALoadoutPacksIntoTwoIntsAndBack()
        {
            var loadout = new Loadout();
            loadout.Add(Weapon.Shotgun, 6);
            loadout.Fire();
            loadout.CycleWeapon(-1);
            loadout.Use();
            loadout.CycleItem(1);
            loadout.SetFuse(4.5);

            Loadout copy = Loadout.Unpack(loadout.PackWeapons(), loadout.PackItems());
            for (int i = 0; i < ArsenalRules.Kinds; i++)
            {
                Assert.That(copy.Count((Weapon)i), Is.EqualTo(loadout.Count((Weapon)i)), ((Weapon)i).ToString());
                Assert.That(copy.Count((Item)i), Is.EqualTo(loadout.Count((Item)i)), ((Item)i).ToString());
            }

            Assert.That(copy.Weapon, Is.EqualTo(Weapon.Vortex));
            Assert.That(copy.Item, Is.EqualTo(loadout.Item));
            Assert.That(copy.Fuse, Is.EqualTo(4.5));
            Assert.That(loadout.PackWeapons(), Is.GreaterThan(0));
        }

        [Test]
        public void NamesFollowTheOriginalsHud()
        {
            Assert.That(ArsenalRules.Name(Weapon.Vortex), Is.EqualTo("Vortex Device"));
            Assert.That(ArsenalRules.Name(Item.ExtraBounce), Is.EqualTo("Extra Bounce"));
            Assert.That(ArsenalRules.CountLabel(ArsenalRules.Unlimited), Is.EqualTo("unlimited"));
            Assert.That(ArsenalRules.CountLabel(3), Is.EqualTo("x3"));
        }
    }
}
