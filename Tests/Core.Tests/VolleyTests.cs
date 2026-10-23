using System.Linq;
using NUnit.Framework;

namespace Ricochet.Core.Tests
{
    public class VolleyTests
    {
        // An open 20 x 10 room: nothing to bounce off but the border.
        private static Level Open() => new Level(-10, -5, 10, 5);

        private static Target T(string id, double x, double y) => new Target(id, new Vec2(x, y), 0.45);

        private static Volley Fire(Weapon weapon, double heading, double power, Level level, params Target[] targets)
        {
            return Arsenal.Fire(weapon, Vec2.Zero, heading, power, 2, 0, level, targets.ToList());
        }

        [Test]
        public void TheCannonIsTheSingleShotItAlwaysWas()
        {
            Volley volley = Fire(Weapon.Cannon, 0, 1, Open(), T("me", 0, 0), T("ann", 5, 0));
            ShotPath alone = BounceSolver.Solve(Vec2.Zero, 0, 1, Open(), new[] { T("me", 0, 0), T("ann", 5, 0) });

            Assert.That(volley.Paths.Count, Is.EqualTo(1));
            Assert.That(volley.Main.Impact.X, Is.EqualTo(alone.Impact.X).Within(1e-9));
            Assert.That(volley.Strikes.Select(s => s.TargetId), Is.EqualTo(new[] { "ann" }));
            Assert.That(volley.Strikes[0].Scale, Is.EqualTo(1));
            Assert.That(volley.HasBlast, Is.False);
            Assert.That(volley.Speed, Is.EqualTo(ShotRules.Default.Speed));
        }

        [Test]
        public void AnExtraBounceLetsTheShotSurviveOneMoreContact()
        {
            var level = new Level(-3, -3, 3, 3);
            Volley plain = Arsenal.Fire(Weapon.Cannon, Vec2.Zero, 10, 1, 2, 0, level, null);
            Volley extra = Arsenal.Fire(Weapon.Cannon, Vec2.Zero, 10, 1, 2, 1, level, null);
            Assert.That(plain.Main.End, Is.EqualTo(ShotEndKind.BounceLimit));
            Assert.That(plain.Main.Contacts.Count, Is.EqualTo(5));
            Assert.That(extra.Main.Contacts.Count, Is.EqualTo(6));
            Assert.That(ShotRules.Default.MaxBounces, Is.EqualTo(5), "the shared rules are untouched");
        }

        [Test]
        public void TheLaserIgnoresPower_AndFliesThreeTimesAsFast()
        {
            Volley laser = Fire(Weapon.Laser, 0, 0, Open());
            Volley cannon = Fire(Weapon.Cannon, 0, 0, Open());
            Assert.That(cannon.Main.Length, Is.EqualTo(ShotRules.Default.MinRange).Within(1e-9));
            Assert.That(laser.Main.Length, Is.GreaterThan(cannon.Main.Length * 3));
            Assert.That(laser.Speed, Is.EqualTo(ShotRules.Default.Speed * 3));
        }

        [Test]
        public void TheShotgunFansFivePellets_EachStrikingForAShare()
        {
            Volley close = Fire(Weapon.Shotgun, 0, 1, Open(), T("me", 0, 0), T("ann", 4, 0));
            Assert.That(close.Paths.Count, Is.EqualTo(5));
            Assert.That(close.Main.Heading, Is.EqualTo(0).Within(1e-9), "the middle pellet flies along the aim");
            Assert.That(close.Paths.Select(p => p.Heading > 180 ? p.Heading - 360 : p.Heading).OrderBy(h => h).ToArray(),
                Is.EqualTo(new[] { -4.0, -2.0, 0.0, 2.0, 4.0 }).Within(1e-9));
            Assert.That(close.Strikes.Count, Is.EqualTo(5), "point blank, every pellet hits");
            Assert.That(close.Strikes.All(s => s.TargetId == "ann" && s.Scale == ArsenalRules.Default.PelletDamage), Is.True);

            Volley far = Fire(Weapon.Shotgun, 0, 1, Open(), T("ann", 9.5, 0));
            Assert.That(far.Strikes.Count, Is.EqualTo(3), "at range the outer pellets spread past");
        }

        [Test]
        public void AGrenadePassesThroughPlayers_AndGoesOffWhenItsFuseRunsOut()
        {
            // Half a second at 18 units a second: 9 units from the muzzle (0.6 out), short of the right border.
            Volley grenade = Arsenal.Fire(Weapon.Grenade, Vec2.Zero, 0, 1, 0.5, 0, Open(),
                new[] { T("me", 0, 0), T("inTheWay", 5, 0), T("nearTheBlast", 9, 1), T("far", 0, 4) });

            Assert.That(grenade.HasBlast, Is.True);
            Assert.That(grenade.Main.End, Is.EqualTo(ShotEndKind.Spent));
            Assert.That(grenade.BlastCenter.X, Is.EqualTo(9.6).Within(1e-9));
            Assert.That(grenade.BlastRadius, Is.EqualTo(ArsenalRules.Default.GrenadeRadius));
            Assert.That(grenade.Strikes.Select(s => s.TargetId), Is.EqualTo(new[] { "nearTheBlast" }));
        }

        [Test]
        public void ALongerFuseCarriesTheGrenadeFurther_OffTheBorders()
        {
            // Two seconds: 36 units: right border (9.4), back across (20), and 6.6 more.
            Volley grenade = Arsenal.Fire(Weapon.Grenade, Vec2.Zero, 0, 0, 2, 0, Open(), new[] { T("me", 0, 0) });
            Assert.That(grenade.Main.Bounces, Is.EqualTo(2));
            Assert.That(grenade.BlastCenter.X, Is.EqualTo(-3.4).Within(1e-9));
            Assert.That(grenade.Strikes.Count, Is.EqualTo(0));

            Volley self = Arsenal.Fire(Weapon.Grenade, Vec2.Zero, 0, 0, 1, 0, new Level(-5, -5, 5, 5), new[] { T("me", 0, 0) });
            Assert.That(self.Strikes.Select(s => s.TargetId), Is.EqualTo(new[] { "me" }), "a grenade that comes back hurts its thrower");
        }

        [Test]
        public void AGrenadeAlsoGoesOffOnItsBounceLimit()
        {
            Volley grenade = Arsenal.Fire(Weapon.Grenade, Vec2.Zero, 10, 0, 6, 0, new Level(-2, -2, 2, 2), null);
            Assert.That(grenade.Main.End, Is.EqualTo(ShotEndKind.BounceLimit));
            Assert.That(grenade.BlastCenter, Is.EqualTo(grenade.Main.Impact));
        }

        [Test]
        public void TheRailgunGoesStraightThroughWalls_HittingEveryoneOnTheLine()
        {
            var level = new Level(-10, -5, 10, 5, new[] { new Wall(new Vec2(5, -2), new Vec2(5, 2)) },
                new[] { new Redirector(new Vec2(3, -1), new Vec2(3, 1), 90) });
            Volley rail = Fire(Weapon.Railgun, 0, 0, level, T("me", 0, 0), T("ann", 7, 0), T("bob", 9, 0.2), T("off", 7, 2));

            Assert.That(rail.Main.Contacts.Count, Is.EqualTo(1));
            Assert.That(rail.Main.Contacts[0].Kind, Is.EqualTo(ContactKind.Border));
            Assert.That(rail.Main.Impact.X, Is.EqualTo(10).Within(1e-9));
            Assert.That(rail.Strikes.Select(s => s.TargetId), Is.EquivalentTo(new[] { "ann", "bob" }));
            Assert.That(rail.Strikes.All(s => s.Bounces == 0), Is.True);
        }

        [Test]
        public void AVolleyKnowsWhatItPassesOver()
        {
            Volley volley = Fire(Weapon.Cannon, 0, 1, Open());
            Assert.That(volley.PassDistance(new Vec2(5, 0.3), 0.5), Is.EqualTo(4.4).Within(1e-9));
            Assert.That(volley.PassDistance(new Vec2(5, 3), 0.5), Is.Null);
            Assert.That(volley.Duration, Is.EqualTo(volley.Main.Length / volley.Speed).Within(1e-9));
        }

        [Test]
        public void AVolleyLandsWhenItsLastProjectileDoes()
        {
            Volley volley = Fire(Weapon.Shotgun, 30, 0.5, Open());
            var flight = new VolleyFlight(volley);
            Assert.That(flight.Flights.Count, Is.EqualTo(5));
            double longest = volley.Paths.Max(p => p.Length);
            double shortest = volley.Paths.Min(p => p.Length);
            flight.Advance(shortest / volley.Speed + 1e-6);
            Assert.That(flight.Flights.Any(f => f.Landed), Is.True);
            if (longest > shortest + 1e-6)
            {
                Assert.That(flight.Landed, Is.False);
            }

            flight.Advance(volley.Duration);
            Assert.That(flight.Landed, Is.True);
            Assert.That(volley.Duration, Is.EqualTo(longest / volley.Speed).Within(1e-9));
        }

        [Test]
        public void TheTeleporterLandsYouJustShortOfWhereItStops()
        {
            Vec2 spot = Arsenal.Teleport(Vec2.Zero, 0, 0, Open(), null, 0.45, out ShotPath path);
            Assert.That(path.Impact.X, Is.EqualTo(6.6).Within(1e-9));
            Assert.That(spot.X, Is.EqualTo(6).Within(1e-9));
            Assert.That(spot.Y, Is.EqualTo(0).Within(1e-9));

            Vec2 back = Arsenal.Teleport(Vec2.Zero, 0, 0, new Level(-3, -3, 3, 3), null, 0.45, out _);
            Assert.That(back.X, Is.EqualTo(0).Within(1e-9), "stepped back along its last heading, after the bounce");

            for (double heading = 0; heading < 360; heading += 15)
            {
                Vec2 anywhere = Arsenal.Teleport(new Vec2(2.8, 2.8), heading, 1, new Level(-3, -3, 3, 3), null, 0.45, out _);
                Assert.That(anywhere.X, Is.InRange(-2.55, 2.55));
                Assert.That(anywhere.Y, Is.InRange(-2.55, 2.55));
            }
        }

        [Test]
        public void TheShrinkRayFindsTheFirstOtherPlayerOnTheAim()
        {
            string hit = Arsenal.ShrinkRay(Vec2.Zero, 0, "me", Open(), new[] { T("me", 0, 0), T("ann", 5, 0) }, null, out ShotPath path);
            Assert.That(hit, Is.EqualTo("ann"));
            Assert.That(path.End, Is.EqualTo(ShotEndKind.Target));
            Assert.That(Arsenal.ShrinkRay(Vec2.Zero, 90, "me", Open(), new[] { T("me", 0, 0), T("ann", 5, 0) }, null, out _), Is.Null,
                "straight up and down never meets Ann, and never its own shooter");
        }
    }
}
