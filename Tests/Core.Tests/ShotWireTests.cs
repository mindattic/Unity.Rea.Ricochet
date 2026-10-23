using System.Linq;
using NUnit.Framework;

namespace Ricochet.Core.Tests
{
    public class ShotWireTests
    {
        // A room with a mirror wall and a redirector, so a shot makes every kind of contact.
        private static Level Room() => new Level(-10, -5, 10, 5,
            new[] { new Wall(new Vec2(4, -2), new Vec2(4, 2)) },
            new[] { new Redirector(new Vec2(-4, -1), new Vec2(-4, 1), 90) });

        private static Target T(string id, double x, double y) => new Target(id, new Vec2(x, y), 0.45);

        private static Volley RoundTrip(Volley volley)
        {
            ShotWire.Encode(volley, out double[] numbers, out string ids);
            return ShotWire.Decode(numbers, ids);
        }

        private static void AssertSame(Volley expected, Volley actual)
        {
            Assert.That(actual, Is.Not.Null);
            Assert.That(actual.Weapon, Is.EqualTo(expected.Weapon));
            Assert.That(actual.Speed, Is.EqualTo(expected.Speed));
            Assert.That(actual.HasBlast, Is.EqualTo(expected.HasBlast));
            Assert.That(actual.BlastCenter, Is.EqualTo(expected.BlastCenter));
            Assert.That(actual.BlastRadius, Is.EqualTo(expected.BlastRadius));
            Assert.That(actual.Duration, Is.EqualTo(expected.Duration));
            Assert.That(actual.Paths.Count, Is.EqualTo(expected.Paths.Count));
            for (int p = 0; p < expected.Paths.Count; p++)
            {
                ShotPath e = expected.Paths[p];
                ShotPath a = actual.Paths[p];
                Assert.That(a.Origin, Is.EqualTo(e.Origin));
                Assert.That(a.Heading, Is.EqualTo(e.Heading));
                Assert.That(a.Power, Is.EqualTo(e.Power));
                Assert.That(a.End, Is.EqualTo(e.End));
                Assert.That(a.TargetId, Is.EqualTo(e.TargetId));
                Assert.That(a.HazardId, Is.EqualTo(e.HazardId));
                Assert.That(a.Points, Is.EqualTo(e.Points), "the points are the server's, to the last bit");
                Assert.That(a.Length, Is.EqualTo(e.Length));
                Assert.That(a.Bounces, Is.EqualTo(e.Bounces));
                Assert.That(a.Contacts.Count, Is.EqualTo(e.Contacts.Count));
                for (int c = 0; c < e.Contacts.Count; c++)
                {
                    Assert.That(a.Contacts[c].Kind, Is.EqualTo(e.Contacts[c].Kind));
                    Assert.That(a.Contacts[c].Point, Is.EqualTo(e.Contacts[c].Point));
                    Assert.That(a.Contacts[c].Distance, Is.EqualTo(e.Contacts[c].Distance));
                    Assert.That(a.Contacts[c].HeadingIn, Is.EqualTo(e.Contacts[c].HeadingIn));
                    Assert.That(a.Contacts[c].HeadingOut, Is.EqualTo(e.Contacts[c].HeadingOut));
                    Assert.That(a.Contacts[c].Deflected, Is.EqualTo(e.Contacts[c].Deflected));
                    Assert.That(a.Contacts[c].Index, Is.EqualTo(e.Contacts[c].Index));
                }
            }

            Assert.That(actual.Strikes.Select(s => (s.TargetId, s.Bounces, s.Scale)),
                Is.EqualTo(expected.Strikes.Select(s => (s.TargetId, s.Bounces, s.Scale))));
            Assert.That(actual.HazardsHit, Is.EqualTo(expected.HazardsHit));
        }

        [TestCase(Weapon.Cannon)]
        [TestCase(Weapon.Laser)]
        [TestCase(Weapon.Shotgun)]
        [TestCase(Weapon.Grenade)]
        [TestCase(Weapon.Railgun)]
        [TestCase(Weapon.Vortex)]
        public void EveryWeaponsVolleyComesOutTheOtherSideAsTheServerSolvedIt(Weapon weapon)
        {
            var targets = new[] { T("seat:0", 0, 0), T("seat:3", 6, 1.2), T("dummy:1", -7, 3) };
            foreach (double heading in new[] { 0.0, 17.5, 160.0, 233.25 })
            {
                Volley solved = Arsenal.Fire(weapon, Vec2.Zero, heading, 0.8, 1.5, 1, Room(), targets);
                AssertSame(solved, RoundTrip(solved));
            }
        }

        [Test]
        public void AStrikeAndTheTargetItLandedOnCarryTheirIds()
        {
            Volley solved = Arsenal.Fire(Weapon.Cannon, Vec2.Zero, 0, 1, 2, 0, new Level(-10, -5, 10, 5), new[] { T("seat:0", 0, 0), T("seat:7", 3, 0) });
            Assume.That(solved.Main.End, Is.EqualTo(ShotEndKind.Target));
            ShotWire.Encode(solved, out _, out string ids);
            Assert.That(ids, Is.EqualTo("seat:7"), "each id goes over once");
            Volley onClient = RoundTrip(solved);
            Assert.That(onClient.Main.TargetId, Is.EqualTo("seat:7"));
            Assert.That(onClient.Strikes.Single().TargetId, Is.EqualTo("seat:7"));
        }

        [Test]
        public void AVortexSetOffInTheAirComesOverCutShort()
        {
            var targets = new[] { T("seat:0", 0, 0), T("seat:2", 3, 0.5) };
            Volley vortex = Arsenal.Fire(Weapon.Vortex, Vec2.Zero, 0, 1, 2, 0, new Level(-10, -5, 10, 5), targets);
            Volley cut = Arsenal.Detonate(vortex, 3, targets);
            Assume.That(cut.Main.End, Is.EqualTo(ShotEndKind.Detonated));
            AssertSame(cut, RoundTrip(cut));
        }

        [Test]
        public void AShotStoppedByAHazardKeepsTheHazardItRanInto()
        {
            var level = new Level(-10, -5, 10, 5).With(null, new[] { new Barrier(42, HazardKind.Electricity, new Vec2(3, -2), new Vec2(3, 2)) }, null);
            Volley solved = Arsenal.Fire(Weapon.Cannon, Vec2.Zero, 0, 1, 2, 0, level, null);
            Assume.That(solved.Main.End, Is.EqualTo(ShotEndKind.Absorbed));
            Volley onClient = RoundTrip(solved);
            Assert.That(onClient.Main.HazardId, Is.EqualTo(42));
            Assert.That(onClient.HazardsHit, Is.EqualTo(new[] { 42 }));
        }

        [Test]
        public void AnythingEncodeDidNotWriteDecodesToNothing()
        {
            Volley solved = Arsenal.Fire(Weapon.Shotgun, Vec2.Zero, 30, 1, 2, 0, Room(), new[] { T("seat:1", 5, 2) });
            ShotWire.Encode(solved, out double[] numbers, out string ids);

            Assert.That(ShotWire.Decode(null, ids), Is.Null);
            Assert.That(ShotWire.Decode(new double[0], ids), Is.Null);
            Assert.That(ShotWire.Decode(numbers.Take(numbers.Length - 1).ToArray(), ids), Is.Null, "cut short");
            Assert.That(ShotWire.Decode(numbers.Concat(new[] { 0.0 }).ToArray(), ids), Is.Null, "trailing numbers");

            double[] version = (double[])numbers.Clone();
            version[0] = ShotWire.Version + 1;
            Assert.That(ShotWire.Decode(version, ids), Is.Null, "another version");

            double[] weapon = (double[])numbers.Clone();
            weapon[1] = 99;
            Assert.That(ShotWire.Decode(weapon, ids), Is.Null, "no such weapon");

            double[] nan = (double[])numbers.Clone();
            nan[9] = double.NaN;
            Assert.That(ShotWire.Decode(nan, ids), Is.Null, "not a number");

            double[] noPaths = (double[])numbers.Clone();
            noPaths[7] = 0;
            Assert.That(ShotWire.Decode(noPaths, ids), Is.Null, "a volley has a path");

            if (solved.Strikes.Count > 0)
            {
                Assert.That(ShotWire.Decode(numbers, string.Empty), Is.Null, "a strike naming an id that did not come over");
            }
        }

        [Test]
        public void TheOutcomeAddsUpEachSeatsDamage_InTheOrderTheyWereStruck()
        {
            var outcome = new ShotOutcome();
            Assert.That(outcome.Missed, Is.True);
            outcome.Add(5, 3);
            outcome.Add(2, 10);
            outcome.Add(5, 4);
            outcome.Add(-1, 50);
            outcome.KnockOut(2);
            outcome.KnockOut(2);

            Assert.That(outcome.Missed, Is.False);
            Assert.That(outcome.Seats, Is.EqualTo(new[] { 5, 2 }));
            Assert.That(outcome.Damage, Is.EqualTo(new[] { 7, 10 }));
            Assert.That(outcome.DamageTo(5), Is.EqualTo(7));
            Assert.That(outcome.DamageTo(9), Is.EqualTo(0));
            Assert.That(outcome.Total, Is.EqualTo(17));
            Assert.That(outcome.KnockedOut, Is.EqualTo(new[] { 2 }));

            outcome.Encode(out int[] seats, out int[] damage, out int[] knockedOut);
            ShotOutcome onClient = ShotOutcome.Decode(seats, damage, knockedOut, 16);
            Assert.That(onClient.Seats, Is.EqualTo(outcome.Seats));
            Assert.That(onClient.Damage, Is.EqualTo(outcome.Damage));
            Assert.That(onClient.KnockedOut, Is.EqualTo(outcome.KnockedOut));
        }

        [Test]
        public void AnOutcomeWhoseListsDoNotBelongTogetherDecodesToNothing()
        {
            Assert.That(ShotOutcome.Decode(null, null, null, 16).Missed, Is.True, "nothing struck");
            Assert.That(ShotOutcome.Decode(new[] { 1 }, new int[0], null, 16), Is.Null, "lengths differ");
            Assert.That(ShotOutcome.Decode(new[] { 16 }, new[] { 5 }, null, 16), Is.Null, "no such seat");
            Assert.That(ShotOutcome.Decode(new[] { 1, 1 }, new[] { 5, 5 }, null, 16), Is.Null, "a seat twice");
            Assert.That(ShotOutcome.Decode(new[] { 1 }, new[] { -5 }, null, 16), Is.Null, "negative damage");
            Assert.That(ShotOutcome.Decode(new[] { 1 }, new[] { 5 }, new[] { 1, 1 }, 16), Is.Null, "knocked out twice");
        }
    }
}
