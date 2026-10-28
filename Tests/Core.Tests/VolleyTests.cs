using System.Linq;
using NUnit.Framework;

namespace Ricochet.Core.Tests
{
    using Ricochet.Core.Aim;
    using Ricochet.Core.Arsenal;
    using Ricochet.Core.Level;
    public class VolleyTests
    {
        // An open 20 x 10 room: nothing to bounce off but the border.
        private static Level Open() => new Level(-10, -5, 10, 5);

        private static Target T(string id, double x, double y) => new Target(id, new Vec2(x, y), 0.45);

        private static Volley Fire(Weapon weapon, double heading, Level level, params Target[] targets)
        {
            return Arsenal.Fire(weapon, Vec2.Zero, heading, 2, 0, level, targets.ToList());
        }

        [Test]
        public void TheCannonIsTheSingleShotItAlwaysWas()
        {
            Volley volley = Fire(Weapon.Cannon, 0, Open(), T("me", 0, 0), T("ann", 5, 0));
            ShotPath alone = BounceSolver.Solve(Vec2.Zero, 0, Open(), new[] { T("me", 0, 0), T("ann", 5, 0) });

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
            Volley plain = Arsenal.Fire(Weapon.Cannon, Vec2.Zero, 10, 2, 0, level, null);
            Volley extra = Arsenal.Fire(Weapon.Cannon, Vec2.Zero, 10, 2, 1, level, null);
            Assert.That(plain.Main.End, Is.EqualTo(ShotEndKind.BounceLimit));
            Assert.That(plain.Main.Contacts.Count, Is.EqualTo(5));
            Assert.That(extra.Main.Contacts.Count, Is.EqualTo(6));
            Assert.That(ShotRules.Default.MaxBounces, Is.EqualTo(5), "the shared rules are untouched");
        }

        [Test]
        public void TheLaserFliesTheCannonsPath_AtAShellsSpeed()
        {
            Volley laser = Fire(Weapon.Laser, 0, Open());
            Volley cannon = Fire(Weapon.Cannon, 0, Open());
            Assert.That(laser.Main.Length, Is.EqualTo(cannon.Main.Length).Within(1e-9));
            Assert.That(laser.Speed, Is.EqualTo(ShotRules.Default.Speed));
        }

        [Test]
        public void TheShotgunFansFivePellets_EachStrikingForAShare()
        {
            Volley close = Fire(Weapon.Shotgun, 0, Open(), T("me", 0, 0), T("ann", 4, 0));
            Assert.That(close.Paths.Count, Is.EqualTo(5));
            Assert.That(close.Main.Heading, Is.EqualTo(0).Within(1e-9), "the middle pellet flies along the aim");
            Assert.That(close.Paths.Select(p => p.Heading > 180 ? p.Heading - 360 : p.Heading).OrderBy(h => h).ToArray(),
                Is.EqualTo(new[] { -4.0, -2.0, 0.0, 2.0, 4.0 }).Within(1e-9));
            Assert.That(close.Strikes.Count, Is.EqualTo(5), "point blank, every pellet hits");
            Assert.That(close.Strikes.All(s => s.TargetId == "ann" && s.Scale == ArsenalRules.Default.PelletDamage), Is.True);

            Volley far = Fire(Weapon.Shotgun, 0, Open(), T("ann", 9.5, 0));
            Assert.That(far.Strikes.Count, Is.EqualTo(3), "at range the outer pellets spread past");
        }

        [Test]
        public void AGrenadePassesThroughPlayers_AndGoesOffWhenItsFuseRunsOut()
        {
            // Half a second at 18 units a second: 9 units from the muzzle (0.6 out), short of the right border.
            Volley grenade = Arsenal.Fire(Weapon.Grenade, Vec2.Zero, 0, 0.5, 0, Open(),
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
            Volley grenade = Arsenal.Fire(Weapon.Grenade, Vec2.Zero, 0, 2, 0, Open(), new[] { T("me", 0, 0) });
            Assert.That(grenade.Main.Bounces, Is.EqualTo(2));
            Assert.That(grenade.BlastCenter.X, Is.EqualTo(-3.4).Within(1e-9));
            Assert.That(grenade.Strikes.Count, Is.EqualTo(0));

            Volley self = Arsenal.Fire(Weapon.Grenade, Vec2.Zero, 0, 1, 0, new Level(-5, -5, 5, 5), new[] { T("me", 0, 0) });
            Assert.That(self.Strikes.Select(s => s.TargetId), Is.EqualTo(new[] { "me" }), "a grenade that comes back hurts its thrower");
        }

        [Test]
        public void AGrenadeAlsoGoesOffOnItsBounceLimit()
        {
            Volley grenade = Arsenal.Fire(Weapon.Grenade, Vec2.Zero, 10, 6, 0, new Level(-2, -2, 2, 2), null);
            Assert.That(grenade.Main.End, Is.EqualTo(ShotEndKind.BounceLimit));
            Assert.That(grenade.BlastCenter, Is.EqualTo(grenade.Main.Impact));
        }

        [Test]
        public void TheRailgunGoesStraightThroughWalls_HittingEveryoneOnTheLine()
        {
            var level = new Level(-10, -5, 10, 5, new[] { new Wall(new Vec2(5, -2), new Vec2(5, 2)) },
                new[] { new Redirector(new Vec2(3, -1), new Vec2(3, 1), 90) });
            Volley rail = Fire(Weapon.Railgun, 0, level, T("me", 0, 0), T("ann", 7, 0), T("bob", 9, 0.2), T("off", 7, 2));

            Assert.That(rail.Main.Contacts.Count, Is.EqualTo(1));
            Assert.That(rail.Main.Contacts[0].Kind, Is.EqualTo(ContactKind.Border));
            Assert.That(rail.Main.Impact.X, Is.EqualTo(10).Within(1e-9));
            Assert.That(rail.Strikes.Select(s => s.TargetId), Is.EquivalentTo(new[] { "ann", "bob" }));
            Assert.That(rail.Strikes.All(s => s.Bounces == 0), Is.True);
        }

        [Test]
        public void AVolleyKnowsWhatItPassesOver()
        {
            Volley volley = Fire(Weapon.Cannon, 0, Open());
            Assert.That(volley.PassDistance(new Vec2(5, 0.3), 0.5), Is.EqualTo(4.4).Within(1e-9));
            Assert.That(volley.PassDistance(new Vec2(5, 3), 0.5), Is.Null);
            Assert.That(volley.Duration, Is.EqualTo(volley.Main.Length / volley.Speed).Within(1e-9));
        }

        [Test]
        public void AVolleyLandsWhenItsLastProjectileDoes()
        {
            Volley volley = Fire(Weapon.Shotgun, 30, Open());
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
        public void TheTeleporterLandsYouJustShortOfTheFirstThingItStrikes()
        {
            // U.RICO-T80: obj_teleporter teleports at its first touch of the border or a shape; it never bounces.
            Vec2 spot = Arsenal.Teleport(Vec2.Zero, 0, Open(), null, 0.45, out ShotPath path);
            Assert.That(path.Impact.X, Is.EqualTo(10).Within(1e-9), "it flies to the border, however far");
            Assert.That(path.Contacts, Is.Empty, "and stops there");
            Assert.That(spot.X, Is.EqualTo(10 - ShotRules.Default.MuzzleOffset).Within(1e-9));
            Assert.That(spot.Y, Is.EqualTo(0).Within(1e-9));

            var walled = new Level(-10, -5, 10, 5, new[] { new Wall(new Vec2(4, -2), new Vec2(4, 2)) });
            Vec2 wall = Arsenal.Teleport(Vec2.Zero, 0, walled, null, 0.45, out ShotPath toWall);
            Assert.That(toWall.Impact.X, Is.EqualTo(4).Within(1e-9), "a shape stops it too");
            Assert.That(wall.X, Is.EqualTo(4 - ShotRules.Default.MuzzleOffset).Within(1e-9), "stepped back along its heading");

            var turned = new Level(-10, -5, 10, 5, null, new[] { new Redirector(new Vec2(4, -1), new Vec2(4, 1), 90) });
            Arsenal.Teleport(Vec2.Zero, 0, turned, null, 0.45, out ShotPath redirected);
            Assert.That(redirected.Impact.Y, Is.EqualTo(5).Within(1e-9), "a standing redirector turns it on, it does not stop it");

            var hazards = new Level(-10, -5, 10, 5).With(null, new[] { new Barrier(1, HazardKind.ShieldWall, new Vec2(3, -2), new Vec2(3, 2)) }, null);
            Arsenal.Teleport(Vec2.Zero, 0, hazards, null, 0.45, out ShotPath through);
            Assert.That(through.Impact.X, Is.EqualTo(10).Within(1e-9), "it passes through hazards");

            for (double heading = 0; heading < 360; heading += 15)
            {
                Vec2 anywhere = Arsenal.Teleport(new Vec2(2.8, 2.8), heading, new Level(-3, -3, 3, 3), null, 0.45, out _);
                Assert.That(anywhere.X, Is.InRange(-2.55, 2.55));
                Assert.That(anywhere.Y, Is.InRange(-2.55, 2.55));
            }
        }

        [Test]
        public void TheShrinkRayStrikesTheFirstShapeOnTheAim_ThroughTanksAndHazards()
        {
            // U.RICO-T90: obj_shrinkray's only collision is obj_polygon_parent (the shapes and the border pieces).
            var level = new Level(-10, -5, 10, 5, new[] { new Wall(new Vec2(-1, 2), new Vec2(1, 2)), new Wall(new Vec2(6, -2), new Vec2(6, 2)) })
                .With(null, new[] { new Barrier(1, HazardKind.ShieldWall, new Vec2(3, -2), new Vec2(3, 2)) }, null);
            int hit = Arsenal.ShrinkRay(Vec2.Zero, 0, level, null, out ShotPath path);
            Assert.That(hit, Is.EqualTo(1), "the wall at x 6, through the shield wall at x 3");
            Assert.That(path.Impact.X, Is.EqualTo(6).Within(1e-9));
            Assert.That(path.Contacts, Is.Empty, "it stops there, it never bounces");

            Assert.That(Arsenal.ShrinkRay(Vec2.Zero, 90, level, null, out ShotPath up), Is.EqualTo(0));
            Assert.That(up.Impact.Y, Is.EqualTo(2).Within(1e-9));
            Assert.That(Arsenal.ShrinkRay(Vec2.Zero, 180, level, null, out ShotPath left), Is.EqualTo(-1), "the border");
            Assert.That(left.Impact.X, Is.EqualTo(-10).Within(1e-9));
        }

        [Test]
        public void AShrunkShapeIsGoneFromTheLevelEveryShotSees()
        {
            var level = new Level(-10, -5, 10, 5, new[]
            {
                new Wall(new Vec2(4, -1), new Vec2(4, 1)), new Wall(new Vec2(4, 1), new Vec2(5, 0)), new Wall(new Vec2(5, 0), new Vec2(4, -1)),
                new Wall(new Vec2(-4, -1), new Vec2(-4, 1)),
            });
            var field = new HazardField(level);
            Assert.That(field.AddShrunkShape(0, 0, 3), Is.Not.Null);
            Assert.That(field.AddShrunkShape(0, 0, 3), Is.Null, "a shape shrinks once");
            Level seen = field.LevelFor(0);
            Assert.That(seen.Walls, Has.Count.EqualTo(1));
            Assert.That(seen.Walls[0], Is.SameAs(level.Walls[3]), "the other shapes stay");
            Assert.That(BounceSolver.Solve(Vec2.Zero, 0, seen).Contacts[0].Kind, Is.EqualTo(ContactKind.Border), "a shot flies through where it stood");

            Hazard wire = field.Hazards[0];
            var onClient = new Hazard(wire.Id, wire.Kind, wire.A, wire.B, wire.Radius, wire.Owner, wire.Heading, wire.Active);
            Assert.That(HazardField.Build(level, new[] { onClient }, 0).Walls, Has.Count.EqualTo(1), "every peer builds the same level from it");
        }
    }
}
