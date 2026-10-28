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
    public class HazardTests
    {
        // An open 20 x 10 room: nothing to bounce off but the border.
        private static Level Open() => new Level(-10, -5, 10, 5);

        private static Target T(string id, double x, double y) => new Target(id, new Vec2(x, y), 0.45);

        private static HazardField Field(Level level = null, HazardRules rules = null) => new HazardField(level ?? Open(), rules, new Random(7));

        [Test]
        public void ABarrierStopsTheShotWhereItTouches_InFrontOfATargetBehindIt()
        {
            HazardField field = Field();
            Hazard arc = field.AddArc(new Vec2(4, -2), new Vec2(4, 2));
            ShotPath path = BounceSolver.Solve(Vec2.Zero, 0, field.LevelFor(0), new[] { T("ann", 6, 0) });

            Assert.That(path.End, Is.EqualTo(ShotEndKind.Absorbed));
            Assert.That(path.HazardId, Is.EqualTo(arc.Id));
            Assert.That(path.Impact.X, Is.EqualTo(4).Within(1e-9));
            Assert.That(path.TargetId, Is.Null);
            Assert.That(path.Contacts, Is.Empty, "a barrier is not a bounce");
        }

        [Test]
        public void ATargetInFrontOfABarrierIsStillHit()
        {
            HazardField field = Field();
            field.AddArc(new Vec2(4, -2), new Vec2(4, 2));
            ShotPath path = BounceSolver.Solve(Vec2.Zero, 0, field.LevelFor(0), new[] { T("ann", 2.5, 0) });
            Assert.That(path.End, Is.EqualTo(ShotEndKind.Target));
            Assert.That(path.TargetId, Is.EqualTo("ann"));
        }

        [Test]
        public void AShieldWallStandsAcrossTheAim_60PxAhead_AndOnlyItsOwnersShotsPassThrough()
        {
            HazardField field = Field();
            Hazard wall = field.AddShieldWall(2, Vec2.Zero, 0);

            Assert.That(wall.Kind, Is.EqualTo(HazardKind.ShieldWall));
            Assert.That(wall.Center.X, Is.EqualTo(0.6875).Within(1e-9), "60 px ahead");
            Assert.That(wall.Center.Y, Is.EqualTo(0).Within(1e-9));
            Assert.That(Vec2.Distance(wall.A, wall.B), Is.EqualTo(HazardRules.Default.ShieldWallLength).Within(1e-9));
            Assert.That(wall.A.X, Is.EqualTo(0.6875).Within(1e-9), "across the aim");
            Assert.That(wall.LifeLeft, Is.InRange(20, 120));

            ShotPath own = BounceSolver.Solve(new Vec2(-3, 0), 0, field.LevelFor(2), new[] { T("ann", 6, 0) });
            Assert.That(own.End, Is.EqualTo(ShotEndKind.Target), "the owner's shot passes through");

            ShotPath theirs = BounceSolver.Solve(new Vec2(-3, 0), 0, field.LevelFor(5), new[] { T("ann", 6, 0) });
            Assert.That(theirs.End, Is.EqualTo(ShotEndKind.Absorbed));
            Assert.That(theirs.HazardId, Is.EqualTo(wall.Id));
        }

        [Test]
        public void StrikingAShieldWallShattersIt_ButNotForItsOwner_AndArcsAndLasersStand()
        {
            HazardField field = Field();
            Hazard wall = field.AddShieldWall(2, Vec2.Zero, 0);
            Hazard arc = field.AddArc(new Vec2(4, -2), new Vec2(4, 2));
            Hazard gate = field.AddGate(new Vec2(-4, -2), new Vec2(-4, 2));
            Hazard tuft = field.AddFoliage(new Vec2(0, 3));
            var removed = new List<Hazard>();
            field.Removed += removed.Add;

            Assert.That(field.Struck(wall.Id, 2), Is.Null, "its owner cannot shatter it");
            Assert.That(field.Struck(arc.Id, 2), Is.Null);
            Assert.That(field.Struck(gate.Id, 2), Is.Null);
            Assert.That(field.Struck(wall.Id, 3), Is.SameAs(wall));
            Assert.That(field.Struck(tuft.Id, 3), Is.SameAs(tuft), "a tuft is cut down");
            Assert.That(field.Struck(999, 3), Is.Null);

            Assert.That(removed, Is.EqualTo(new[] { wall, tuft }));
            Assert.That(field.Hazards, Is.EqualTo(new[] { arc, gate }));
            Assert.That(wall.End, Is.EqualTo(HazardEnd.Struck));
            Assert.That(arc.End, Is.EqualTo(HazardEnd.None));
            field.Clear();
            Assert.That(arc.End, Is.EqualTo(HazardEnd.Cleared));
        }

        [Test]
        public void AShieldWallDecaysWhenItsTimeIsUp_TheRestStay()
        {
            var rules = new HazardRules { ShieldWallMinLife = 5, ShieldWallMaxLife = 5 };
            HazardField field = Field(rules: rules);
            Hazard wall = field.AddShieldWall(0, Vec2.Zero, 90);
            field.AddArc(new Vec2(4, -2), new Vec2(4, 2));

            field.Tick(4.9);
            Assert.That(field.Hazards, Has.Member(wall));
            field.Tick(0.2);
            Assert.That(field.Hazards, Has.No.Member(wall));
            Assert.That(field.Hazards.Count, Is.EqualTo(1));
            Assert.That(wall.End, Is.EqualTo(HazardEnd.Decayed));
        }

        [Test]
        public void ALaserGateIsOnEveryOtherTurn_AndOnlyStopsShotsWhileItIsOn()
        {
            HazardField field = Field();
            Hazard gate = field.AddGate(new Vec2(4, -2), new Vec2(4, 2));
            var changed = new List<Hazard>();
            field.Changed += changed.Add;
            Assert.That(gate.Active, Is.True);
            Assert.That(BounceSolver.Solve(Vec2.Zero, 0, field.LevelFor(0), new[] { T("ann", 6, 0) }).End, Is.EqualTo(ShotEndKind.Absorbed));

            field.NextTurn();
            Assert.That(gate.Active, Is.False);
            Assert.That(changed, Is.EqualTo(new[] { gate }));
            Assert.That(BounceSolver.Solve(Vec2.Zero, 0, field.LevelFor(0), new[] { T("ann", 6, 0) }).End, Is.EqualTo(ShotEndKind.Target));

            field.NextTurn();
            Assert.That(gate.Active, Is.True);

            var steady = Field(rules: new HazardRules { LasersAlternate = false });
            Hazard always = steady.AddGate(new Vec2(4, -2), new Vec2(4, 2));
            steady.NextTurn();
            Assert.That(always.Active, Is.True);
        }

        [Test]
        public void ATuftOfFoliageIsARoundBarrier()
        {
            HazardField field = Field();
            Hazard tuft = field.AddFoliage(new Vec2(5, 0.1));
            ShotPath path = BounceSolver.Solve(Vec2.Zero, 0, field.LevelFor(0));
            Assert.That(path.End, Is.EqualTo(ShotEndKind.Absorbed));
            Assert.That(path.HazardId, Is.EqualTo(tuft.Id));
            Assert.That(Vec2.Distance(path.Impact, tuft.A), Is.EqualTo(tuft.Radius).Within(1e-9));

            ShotPath past = BounceSolver.Solve(new Vec2(0, 2), 0, field.LevelFor(0), new[] { T("ann", 8, 2) });
            Assert.That(past.End, Is.EqualTo(ShotEndKind.Target), "a shot clear of the tuft flies on");
        }

        [Test]
        public void AShotReachingABreachFliesOutOfTheLevel_ElsewhereTheBorderStillBounces()
        {
            HazardField field = Field();
            field.AddBreach(new Vec2(10, -1), new Vec2(10, 1));
            ShotPath out_ = BounceSolver.Solve(Vec2.Zero, 0, field.LevelFor(0));
            Assert.That(out_.End, Is.EqualTo(ShotEndKind.Escaped));
            Assert.That(out_.Impact.X, Is.EqualTo(10).Within(1e-9));
            Assert.That(out_.Contacts, Is.Empty);

            ShotPath bounced = BounceSolver.Solve(new Vec2(0, 3), 0, field.LevelFor(0));
            Assert.That(bounced.Contacts[0].Kind, Is.EqualTo(ContactKind.Border));
            Assert.That(bounced.End, Is.Not.EqualTo(ShotEndKind.Escaped));

            ShotPath top = BounceSolver.Solve(Vec2.Zero, 90, field.LevelFor(0));
            Assert.That(top.Contacts[0].Point.Y, Is.EqualTo(5).Within(1e-9), "the top is untouched");
        }

        [Test]
        public void TheLayoutLaysTheArcsAndGates_AndFoliageClearOfThePlayersAndWalls()
        {
            Level level = Level.Default();
            var field = new HazardField(level, HazardRules.Default, new Random(3));
            Vec2[] seats = Enumerable.Range(0, 16).Select(Seating.SpawnPoint).ToArray();
            field.LayOut(seats);

            Assert.That(field.Hazards.Count(h => h.Kind == HazardKind.Electricity), Is.EqualTo(HazardRules.Default.Arcs.Count));
            Assert.That(field.Hazards.Count(h => h.Kind == HazardKind.Laser), Is.EqualTo(HazardRules.Default.Gates.Count));
            Hazard[] tufts = field.Hazards.Where(h => h.Kind == HazardKind.Foliage).ToArray();
            Assert.That(tufts.Length, Is.GreaterThan(0).And.LessThanOrEqualTo(HazardRules.Default.FoliageCount));
            foreach (Hazard tuft in tufts)
            {
                Assert.That(level.Contains(tuft.A), Is.True);
                Assert.That(seats.Min(s => Vec2.Distance(s, tuft.A)), Is.GreaterThanOrEqualTo(HazardRules.Default.FoliageClearance));
                Assert.That(level.Walls.Min(w => Geometry.SegmentDistance(w.A, w.B, tuft.A, out _)), Is.GreaterThanOrEqualTo(HazardRules.Default.FoliageClearance));
            }

            // Nobody's seat is boxed in: every seat can still be shot at from somewhere.
            foreach (Vec2 arcEnd in field.Hazards.Where(h => h.Kind != HazardKind.Foliage).SelectMany(h => new[] { h.A, h.B }))
            {
                Assert.That(level.Contains(arcEnd), Is.True);
                Assert.That(seats.Min(s => Vec2.Distance(s, arcEnd)), Is.GreaterThan(1), "a pylon or emitter on top of a seat");
            }

            int before = field.Hazards.Count;
            field.Clear();
            Assert.That(field.Hazards, Is.Empty);
            Assert.That(before, Is.GreaterThan(2));
        }

        [Test]
        public void APlacedRedirectorSendsShotsAlongItsHeading()
        {
            HazardField field = Field();
            ShotPath flight = Arsenal.FlyRedirector(Vec2.Zero, 0, field.Level, ShotRules.Default);
            Assert.That(flight.Contacts.Count, Is.GreaterThan(5), "it flies on, bouncing, until its owner stops it");
            double stopAt = 6;
            Vec2 spot = Arsenal.PlaceRedirector(flight, stopAt, field.Level, ShotRules.Default, out double heading);
            Assert.That(spot.X, Is.EqualTo(ShotRules.Default.MuzzleOffset + stopAt).Within(1e-9), "it stops where it is");
            Assert.That(heading, Is.EqualTo(0).Within(1e-9));

            field.AddRedirector(0, spot, heading);
            ShotPath shot = BounceSolver.Solve(new Vec2(4, -1), 20, field.LevelFor(3));
            Assert.That(shot.Contacts[0].Kind, Is.EqualTo(ContactKind.Redirector));
            Assert.That(shot.Contacts[0].Point.X, Is.EqualTo(spot.X).Within(1e-9), "it stands across the way it flew");
            Assert.That(shot.Contacts[0].HeadingOut, Is.EqualTo(0).Within(1e-9), "and sends shots on the way it flew");

        }

        [Test]
        public void AVortexSetOffInMidFlightGoesOffThere_HurtingNoOneWithoutAShapeToFling()
        {
            Target[] targets = { T("me", 0, 0), T("near", 4, 2), T("atTheEnd", 9.4, 0) };
            Volley whole = Arsenal.Fire(Weapon.Vortex, Vec2.Zero, 0, 2, 0, Open(), targets);
            Assert.That(whole.HasBlast, Is.True);
            Assert.That(whole.Main.End, Is.EqualTo(ShotEndKind.Target));
            Assert.That(whole.Strikes, Is.Empty, "no shape to fling: nobody is hurt");

            Volley early = Arsenal.Detonate(whole, 3.4, targets, Open());
            Assert.That(early.Main.End, Is.EqualTo(ShotEndKind.Detonated));
            Assert.That(early.BlastCenter.X, Is.EqualTo(4).Within(1e-9), "3.4 past the muzzle at 0.6");
            Assert.That(early.Main.Length, Is.EqualTo(3.4).Within(1e-9));
            Assert.That(early.Strikes, Is.Empty, "a vortex hurts no one itself, only the shapes it flings (U.RICO-T96)");
            Assert.That(early.Speed, Is.EqualTo(whole.Speed));

            Assert.That(Arsenal.Detonate(whole, 100, targets, Open()), Is.SameAs(whole), "too late: it lands where it was going");
            Volley cannon = Arsenal.Fire(Weapon.Cannon, Vec2.Zero, 0, 2, 0, Open(), targets);
            Assert.That(Arsenal.Detonate(cannon, 2, targets, Open()), Is.SameAs(cannon), "only a vortex can be set off");
        }

        // A square shape of side 1 whose corner sits at (x, y).
        private static Wall[] Square(double x, double y) => new[]
        {
            new Wall(new Vec2(x, y), new Vec2(x + 1, y)), new Wall(new Vec2(x + 1, y), new Vec2(x + 1, y + 1)),
            new Wall(new Vec2(x + 1, y + 1), new Vec2(x, y + 1)), new Wall(new Vec2(x, y + 1), new Vec2(x, y)),
        };

        [Test]
        public void AVortexHurtsATankOnlyWhenAFlungShapeStrikesIt_AndItsRadiusIs200Pixels()
        {
            Assert.That(ArsenalRules.Default.VortexRadius, Is.EqualTo(200 * 22.0 / 1920.0).Within(1e-3));

            // No shapes in reach: a tank sitting right at the vortex is not hurt by the blast itself.
            var bare = new Level(-10, -5, 10, 5);
            Assert.That(VortexStorm.Strike(new Vec2(0, 0), 2.29, bare, new[] { T("a", 0.5, 0) }, 1), Is.Null);

            // A shape in reach is pulled onto the vortex and flung out again: a tank on top of the vortex is struck.
            var shapes = new Level(-10, -5, 10, 5, Square(1, 1));
            Target? hit = VortexStorm.Strike(new Vec2(0, 0), 2.29, shapes, new[] { T("far", 8, 4), T("here", 0, 0) }, 1);
            Assert.That(hit.HasValue && hit.Value.Id == "here", Is.True);

            // A shape out of reach is not touched, so a tank beside it is safe.
            var out_of_reach = new Level(-10, -5, 10, 5, Square(5, 3));
            Assert.That(VortexStorm.Strike(new Vec2(0, 0), 2.29, out_of_reach, new[] { T("here", 0, 0), T("beside", 5.5, 3.5) }, 1), Is.Null);
        }

        [Test]
        public void ATruncatedPathKeepsTheContactsBeforeTheCut()
        {
            ShotPath path = BounceSolver.Solve(Vec2.Zero, 0, new Level(-3, -3, 3, 3));
            double cut = path.Contacts[1].Distance + 0.5;
            ShotPath shorter = path.Truncate(cut);
            Assert.That(shorter.Contacts.Count, Is.EqualTo(2));
            Assert.That(shorter.Length, Is.EqualTo(cut).Within(1e-9));
            Assert.That(shorter.Impact.X, Is.EqualTo(path.PositionAt(cut, out _).X).Within(1e-9));
            Assert.That(path.Truncate(path.Length + 1), Is.SameAs(path));
        }

        [Test]
        public void AGrenadeOrVortexStoppedByABarrierIsLostWithoutGoingOff()
        {
            HazardField field = Field();
            field.AddArc(new Vec2(3, -2), new Vec2(3, 2));
            Target[] targets = { T("near", 4, 1) };
            Volley grenade = Arsenal.Fire(Weapon.Grenade, Vec2.Zero, 0, 2, 0, field.LevelFor(0), targets);
            Volley vortex = Arsenal.Fire(Weapon.Vortex, Vec2.Zero, 0, 2, 0, field.LevelFor(0), targets);
            foreach (Volley volley in new[] { grenade, vortex })
            {
                Assert.That(volley.Main.End, Is.EqualTo(ShotEndKind.Absorbed));
                Assert.That(volley.HasBlast, Is.False);
                Assert.That(volley.Strikes, Is.Empty);
                Assert.That(volley.HazardsHit, Is.EqualTo(new[] { field.Hazards[0].Id }));
            }
        }

        [Test]
        public void TheRailgunGoesThroughBarriers_ButNotThroughABreach()
        {
            HazardField field = Field();
            field.AddArc(new Vec2(3, -2), new Vec2(3, 2));
            Volley rail = Arsenal.Fire(Weapon.Railgun, Vec2.Zero, 0, 2, 0, field.LevelFor(0), new[] { T("behind", 6, 0) });
            Assert.That(rail.Strikes.Select(s => s.TargetId), Is.EqualTo(new[] { "behind" }));
            Assert.That(rail.HazardsHit, Is.Empty);
        }

        [Test]
        public void BuildLeavesOutTheShootersOwnWalls_AndBuildsTheSameLevelFromHazardsReadBackOffTheWire()
        {
            HazardField field = Field();
            Hazard wall = field.AddShieldWall(4, Vec2.Zero, 0);
            Hazard gate = field.AddGate(new Vec2(-4, -2), new Vec2(-4, 2));
            field.NextTurn();
            Hazard copyOfWall = new Hazard(wall.Id, wall.Kind, wall.A, wall.B, wall.Radius, wall.Owner, wall.Heading, wall.Active);
            Hazard copyOfGate = new Hazard(gate.Id, gate.Kind, gate.A, gate.B, gate.Radius, gate.Owner, gate.Heading, gate.Active);

            Level mine = HazardField.Build(Level.Default(), new[] { copyOfWall, copyOfGate }, 4);
            Level theirs = HazardField.Build(Level.Default(), new[] { copyOfWall, copyOfGate }, 1);
            Assert.That(mine.Barriers, Is.Empty, "my wall passes my shots, and the gate is off");
            Assert.That(theirs.Barriers.Select(b => b.Id), Is.EqualTo(new[] { wall.Id }));
            Assert.That(HazardField.Build(Level.Default(), Array.Empty<Hazard>(), 1).Barriers, Is.Empty);
        }
    }
}
