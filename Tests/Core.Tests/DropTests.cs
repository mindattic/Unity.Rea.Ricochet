using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Ricochet.Core.Tests
{
    using Ricochet.Core.Aim;
    using Ricochet.Core.Arsenal;
    using Ricochet.Core.Level;
    using Ricochet.Core.Match;
    public class DropTests
    {
        private static Level Open() => new Level(-10, -5, 10, 5);

        private static DropRules Every(double seconds) => new DropRules { MinDelay = seconds, MaxDelay = seconds, MinLife = 3, MaxLife = 3 };

        [Test]
        public void EachKindFallsOnItsOwnTimer_FromAboveTheTopToASpotInsideTheMargins()
        {
            var field = new DropField(Open(), Every(1), new Random(3));
            var spawned = new List<Drop>();
            field.Spawned += spawned.Add;

            field.Tick(0.5);
            Assert.That(spawned, Is.Empty);
            field.Tick(0.5);
            Assert.That(spawned.Select(d => d.Kind), Is.EquivalentTo(new[] { DropKind.Weapon, DropKind.Item, DropKind.Health, DropKind.Powerup }));
            foreach (Drop drop in spawned)
            {
                Assert.That(drop.Y, Is.GreaterThan(Open().MaxY));
                Assert.That(drop.Landed, Is.False);
                Assert.That(drop.Spot.X, Is.InRange(-8.5, 8.5));
                Assert.That(drop.Spot.Y, Is.InRange(-3.5, 3.5));
            }

            Assert.That(spawned.Select(d => d.Id).Distinct().Count(), Is.EqualTo(4));
            Assert.That(field.UntilNext(DropKind.Weapon), Is.EqualTo(1).Within(1e-9), "the timer starts over");
        }

        [Test]
        public void AKindThatIsOffNeverFalls()
        {
            DropRules rules = Every(1);
            rules.SetEnabled(DropKind.Health, false);
            rules.SetEnabled(DropKind.Powerup, false);
            var field = new DropField(Open(), rules, new Random(3));
            field.Tick(1);
            Assert.That(field.Drops.Select(d => d.Kind), Is.EquivalentTo(new[] { DropKind.Weapon, DropKind.Item }));
        }

        [Test]
        public void ACrateFallsLandsAndBurstsAfterItsLife()
        {
            var field = new DropField(Open(), Every(1000), new Random(3));
            var landed = new List<Drop>();
            var removed = new List<Drop>();
            field.Landed += landed.Add;
            field.Removed += removed.Add;

            Drop drop = field.Spawn(DropKind.Health, new Vec2(2, 0));
            Assert.That(drop.Y, Is.EqualTo(6));
            field.Tick(0.3);
            Assert.That(drop.Y, Is.EqualTo(3).Within(1e-9), "ten units a second");
            field.Tick(0.3);
            Assert.That(drop.Landed, Is.True);
            Assert.That(drop.Position, Is.EqualTo(new Vec2(2, 0)));
            Assert.That(landed, Is.EqualTo(new[] { drop }));

            field.Tick(2.9);
            Assert.That(field.Drops, Has.Member(drop));
            field.Tick(0.2);
            Assert.That(field.Drops, Is.Empty);
            Assert.That(removed, Is.EqualTo(new[] { drop }));
        }

        [Test]
        public void NoMoreThanTheCapAtOnce()
        {
            var field = new DropField(Open(), new DropRules { MaxOnField = 2 }, new Random(1));
            Assert.That(field.Spawn(DropKind.Weapon), Is.Not.Null);
            Assert.That(field.Spawn(DropKind.Item), Is.Not.Null);
            Assert.That(field.Spawn(DropKind.Health), Is.Null);
        }

        [Test]
        public void AShotThatPassesOverALandedCrateCollectsIt_NearestFirst_ButNotOneStillFalling()
        {
            var field = new DropField(Open(), Every(1000), new Random(3));
            Drop near = field.Spawn(DropKind.Weapon, new Vec2(3, 0.2));
            Drop far = field.Spawn(DropKind.Item, new Vec2(6, -0.3));
            Drop aside = field.Spawn(DropKind.Health, new Vec2(4, 3));
            field.Tick(2);
            Drop falling = field.Spawn(DropKind.Powerup, new Vec2(5, 0));

            Volley shot = Arsenal.Fire(Weapon.Cannon, Vec2.Zero, 0, 1, 2, 0, Open(), null);
            List<Drop> collected = field.Collect(shot);

            Assert.That(collected, Is.EqualTo(new[] { near, far }));
            Assert.That(field.Drops, Is.EquivalentTo(new[] { aside, falling }));
            Assert.That(field.Collect(shot), Is.Empty);
        }

        [Test]
        public void ClearTakesEveryCrateAway()
        {
            var field = new DropField(Open(), Every(1), new Random(3));
            field.Tick(1);
            int removed = 0;
            field.Removed += _ => removed++;
            field.Clear();
            Assert.That(field.Drops, Is.Empty);
            Assert.That(removed, Is.EqualTo(4));
        }

        [Test]
        public void AWeaponCrateGivesAWeaponInPlay_NeverTheUnlimitedCannon()
        {
            var dice = new Random(11);
            var seen = new HashSet<Weapon>();
            for (int i = 0; i < 300; i++)
            {
                Weapon? weapon = Pickups.RandomWeapon(dice, ArsenalRules.Default);
                Assert.That(weapon, Is.Not.Null);
                seen.Add(weapon.Value);
            }

            Assert.That(seen, Is.EquivalentTo(new[] { Weapon.Laser, Weapon.Shotgun, Weapon.Grenade, Weapon.Railgun, Weapon.Vortex }));
        }

        [Test]
        public void AnItemCrateGivesAnItemInPlay()
        {
            var dice = new Random(11);
            var seen = new HashSet<Item>();
            for (int i = 0; i < 300; i++)
            {
                seen.Add(Pickups.RandomItem(dice, ArsenalRules.Default).Value);
            }

            Assert.That(seen, Is.EquivalentTo(new[] { Item.ExtraBounce, Item.ShieldWall, Item.ShrinkRay, Item.Teleporter, Item.AimAssist, Item.Redirector }));

            var none = new ArsenalRules();
            for (int i = 0; i < ArsenalRules.Kinds; i++)
            {
                none.SetInPlay((Item)i, false);
            }

            Assert.That(Pickups.RandomItem(dice, none), Is.Null);
        }

        [Test]
        public void AHealthCrateGivesHalfToAllOfAHitsDamage_GiveOrTakeATenth()
        {
            var dice = new Random(5);
            var amounts = Enumerable.Range(0, 500).Select(_ => Pickups.HealAmount(dice, 10)).ToList();
            Assert.That(amounts.Min(), Is.EqualTo(4));
            Assert.That(amounts.Max(), Is.EqualTo(11));
            Assert.That(Pickups.HealAmount(dice, 0), Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void StrikesTakeAShareOfAHit_HealthComesBackUpToTheMaximum_AndTheTurnEndsOnceTheVolleyIsOver()
        {
            var match = new Match(seed: 7);
            match.Join(0, SeatKind.Human, "Ann");
            match.Join(1, SeatKind.Human, "Bob");
            var rounds = new List<int>();
            match.RoundStarted += rounds.Add;
            match.Start();
            Assert.That(rounds, Is.EqualTo(new[] { 1 }));

            Assert.That(match.Fire(0), Is.True);
            int pellet = match.Strike(0, 1, 0, 0.35);
            Assert.That(pellet, Is.InRange(3, 4), "a third and a bit of 10 +/- 1");
            int full = match.Strike(0, 1, 2);
            Assert.That(full, Is.InRange(13, 15));
            Assert.That(match.Phase, Is.EqualTo(MatchPhase.ShotInFlight), "a strike alone does not end the turn");
            Assert.That(match.Seat(1).Health, Is.EqualTo(100 - pellet - full));

            Assert.That(match.Heal(1, 5), Is.EqualTo(5));
            Assert.That(match.Heal(1, 500), Is.EqualTo(pellet + full - 5));
            Assert.That(match.Seat(1).Health, Is.EqualTo(100));

            match.ShotOver(0);
            Assert.That(match.Phase, Is.EqualTo(MatchPhase.TurnOver));
        }

        [Test]
        public void AVolleyThatKillsTheLastOpponentEndsTheRound()
        {
            var match = new Match(new MatchRules { WeaponDamage = 200 }, seed: 7);
            match.Join(0, SeatKind.Human, "Ann");
            match.Join(1, SeatKind.Human, "Bob");
            match.Start();
            match.Fire(0);
            match.Strike(0, 1, 0);
            Assert.That(match.Seat(1).Alive, Is.False);
            Assert.That(match.Heal(1, 10), Is.EqualTo(0), "no health for the dead");
            match.ShotOver(0);
            Assert.That(match.Phase, Is.EqualTo(MatchPhase.RoundOver));
            Assert.That(match.Winner, Is.EqualTo(0));
        }
    }
}
