using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Ricochet.Core.Tests
{
    using Ricochet.Core.Aim;
    using Ricochet.Core.Arsenal;
    using Ricochet.Core.Commands;
    using Ricochet.Core.Level;
    using Ricochet.Core.Match;

    // UR-T34: every command goes through the one authority, which answers with the ordered events it caused. Each test
    // gives one command and reads the events and the state it left, with no engine and no Runtime.
    public class CommandTests
    {
        private sealed class World : IGameWorld
        {
            public readonly List<Target> Dummies = new List<Target>();

            public IReadOnlyList<Target> DummyTargets() => Dummies;
        }

        private static List<T> Of<T>(IEnumerable<GameEvent> events) where T : GameEvent => events.OfType<T>().ToList();

        private static GameAuthority Practice(out PlayerState ann, out World world, Action<ArsenalRules> arms = null)
        {
            var rules = ArsenalRules.Default;
            arms?.Invoke(rules);
            world = new World();
            var authority = new GameAuthority(world, arms: rules, seed: 7);
            ann = authority.AddPlayer(1);
            authority.SetSeat(1, 0);
            authority.Place(1, Seating.SpawnPoint(0));
            return authority;
        }

        // Two seats, A (player 1) and B (player 2), playing to one round; health 1 so a single hit knocks out.
        private static GameAuthority TwoSeats(out PlayerState a, out PlayerState b, int health = 100, Action<ArsenalRules> arms = null,
            int goal = MatchSetup.OneRound, bool readyCard = false)
        {
            var rules = ArsenalRules.Default;
            arms?.Invoke(rules);
            var matchRules = readyCard ? MatchRules.Default : NoReadyCard.Rules();
            matchRules.MaxHealth = health;
            var authority = new GameAuthority(new World(), matchRules: matchRules, arms: rules, seed: 7);
            a = authority.AddPlayer(1);
            b = authority.AddPlayer(2);
            authority.SetSeat(1, 0);
            authority.SetSeat(2, 1);
            Match match = authority.BeginMatch(0, goal);
            match.Join(0, SeatKind.Human, "A");
            match.Join(1, SeatKind.Human, "B");
            authority.StartMatch();
            return authority;
        }

        // A straight shot along the floor from seat 0 at seat 1 (to its left).
        private const double AtSeatOne = 180;

        // ---- Aim, SetPower

        [Test]
        public void Aim_TurnsTheBarrel_NormalisedAndNaNKeepsTheLastGoodHeading()
        {
            GameAuthority authority = Practice(out PlayerState ann, out _);

            IReadOnlyList<GameEvent> events = authority.Apply(new AimCommand(1, -90));
            Assert.That(ann.Heading, Is.EqualTo(270));
            Assert.That(Of<Aimed>(events).Single().Heading, Is.EqualTo(270));

            authority.Apply(new AimCommand(1, double.NaN));
            Assert.That(ann.Heading, Is.EqualTo(270), "a NaN heading keeps the last good one");
            authority.Apply(new AimCommand(1, double.PositiveInfinity));
            authority.Apply(new FireCommand(1, double.NegativeInfinity, 1));
            Assert.That(ann.Heading, Is.EqualTo(270), "an infinite heading is garbage too");
        }

        [Test]
        public void SetPower_ClampsToZeroToOne_AndNaNIsZero()
        {
            GameAuthority authority = Practice(out PlayerState ann, out _);

            authority.Apply(new SetPowerCommand(1, 5));
            Assert.That(ann.Power, Is.EqualTo(1));
            IReadOnlyList<GameEvent> events = authority.Apply(new SetPowerCommand(1, -3));
            Assert.That(ann.Power, Is.EqualTo(0));
            Assert.That(Of<Aimed>(events).Single().Power, Is.EqualTo(0));
            authority.Apply(new SetPowerCommand(1, 0.8));
            authority.Apply(new SetPowerCommand(1, double.NaN));
            Assert.That(ann.Power, Is.EqualTo(0));
        }

        [Test]
        public void ACommandFromAnUnknownPlayer_ChangesNothingAndAnswersWithNoEvents()
        {
            GameAuthority authority = Practice(out PlayerState ann, out _);
            Assert.That(authority.Apply(new AimCommand(99, 10)), Is.Empty);
            Assert.That(authority.Apply(new FireCommand(99, 10, 1)), Is.Empty);
            Assert.That(authority.Apply(new EndTurnCommand(99)), Is.Empty);
            Assert.That(ann.Heading, Is.EqualTo(90));
            Assert.Throws<ArgumentNullException>(() => authority.Apply(null));
        }

        // ---- Fire

        [Test]
        public void Fire_InPractice_SolvesTheVolleyAndOnlyOneIsInTheAirAtATime()
        {
            GameAuthority authority = Practice(out PlayerState ann, out _);

            IReadOnlyList<GameEvent> events = authority.Apply(new FireCommand(1, 45, 1));
            ShotFired fired = Of<ShotFired>(events).Single();
            Assert.That(fired.PlayerId, Is.EqualTo(1));
            Assert.That(fired.Shot, Is.EqualTo(1));
            Assert.That(fired.Weapon, Is.EqualTo(Weapon.Cannon));
            Assert.That(fired.Heading, Is.EqualTo(45));
            Assert.That(fired.Volley.Main.Points.Count, Is.GreaterThan(1), "the authority solved the path");
            Assert.That(events.Last(), Is.SameAs(fired), "the shot is the last thing that happened");
            Assert.That(ann.InFlight, Is.True);
            Assert.That(ann.Shots, Is.EqualTo(1));

            Assert.That(authority.Apply(new FireCommand(1, 90, 1)), Is.Empty, "a second shot while one is in the air is ignored");
            Assert.That(ann.Shots, Is.EqualTo(1));
        }

        [Test]
        public void Fire_InPractice_SpendsNothing()
        {
            GameAuthority authority = Practice(out PlayerState ann, out _);
            authority.Apply(new CycleWeaponCommand(1, 1)); // the laser
            Assert.That(ann.Loadout.Weapon, Is.EqualTo(Weapon.Laser));
            authority.Apply(new FireCommand(1, 45, 1));
            Assert.That(ann.Loadout.Count(Weapon.Laser), Is.EqualTo(3), "practice is free fire");
        }

        [Test]
        public void Fire_InAMatch_OnlyOnYourTurn_AndItSpendsTheWeapon()
        {
            GameAuthority authority = TwoSeats(out PlayerState a, out PlayerState b);
            Assert.That(authority.Match.Current, Is.EqualTo(0));

            Assert.That(authority.Apply(new FireCommand(2, 0, 1)), Is.Empty, "not B's turn");
            Assert.That(b.Shots, Is.EqualTo(0));

            authority.Apply(new CycleWeaponCommand(1, 1));
            IReadOnlyList<GameEvent> events = authority.Apply(new FireCommand(1, 90, 1));
            Assert.That(Of<ShotFired>(events).Single().Weapon, Is.EqualTo(Weapon.Laser));
            Assert.That(a.Loadout.Count(Weapon.Laser), Is.EqualTo(2));
            Assert.That(authority.Match.Phase, Is.EqualTo(MatchPhase.ShotInFlight), "the turn clock stops while it flies");
        }

        [Test]
        public void WhileTheReadyCardIsUp_NobodyFiresDrivesOrEndsTheTurn_UntilTheClockStarts()
        {
            // UR-T42: obj_ready's hold; turnStarted is false, so fire_weapon, move_forward and next_player all refuse.
            GameAuthority authority = TwoSeats(out PlayerState a, out _, readyCard: true);
            Assert.That(authority.Match.Phase, Is.EqualTo(MatchPhase.Ready));
            Assert.That(authority.Apply(new FireCommand(1, 90, 1)), Is.Empty);
            authority.Apply(new DriveCommand(1, 1));
            authority.AdvanceDrive(1, 0.1);
            Assert.That(authority.Match.HasMoved, Is.False, "no driving before the clock");
            Assert.That(authority.Apply(new EndTurnCommand(1)), Is.Empty);
            Assert.That(authority.Match.Current, Is.EqualTo(0));
            Assert.That(a.Shots, Is.EqualTo(0));

            authority.Tick(MatchRules.Default.ReadySeconds + 0.01);
            Assert.That(authority.Match.Phase, Is.EqualTo(MatchPhase.Aiming));
            Assert.That(authority.Match.TimeLeft, Is.EqualTo(MatchRules.Default.TurnSeconds), "the clock starts full");
            Assert.That(Of<ShotFired>(authority.Apply(new FireCommand(1, 90, 1))), Has.Count.EqualTo(1));
        }

        [Test]
        public void ARandomRoundIsLaidInFull_ClearOfOverlaps_BeforeItsFirstTurnStarts()
        {
            // UR-T42: check_isready held the round until every shape and player stood clear; here the level and every
            // seat's spot are rolled clear of overlaps (LevelGenerator) before the first turn (and its Ready card) begins.
            var authority = new GameAuthority(new World(), matchRules: NoReadyCard.Rules(), seed: 11);
            PlayerState a = authority.AddPlayer(1);
            PlayerState b = authority.AddPlayer(2);
            authority.SetSeat(1, 0);
            authority.SetSeat(2, 1);
            Match match = authority.BeginMatch(Maps.RandomIndex, 0);
            match.Join(0, SeatKind.Human, "A");
            match.Join(1, SeatKind.Human, "B");
            var atFirstTurn = new List<(Vec2 a, Vec2 b, Vec2 spotA, Vec2 spotB, int walls)>();
            match.TurnStarted += _ => atFirstTurn.Add((a.Position, b.Position, authority.Layout.SpawnPoint(0), authority.Layout.SpawnPoint(1), authority.Layout.Level.Walls.Count));
            authority.StartMatch();

            Assert.That(atFirstTurn, Has.Count.EqualTo(1));
            var first = atFirstTurn[0];
            Assert.That(first.walls, Is.GreaterThan(0), "the round's level is laid");
            Assert.That(Vec2.Distance(first.a, first.spotA), Is.LessThan(1e-3), "everyone stands at their round's spot");
            Assert.That(Vec2.Distance(first.b, first.spotB), Is.LessThan(1e-3));
            foreach (Wall wall in authority.Layout.Level.Walls)
            {
                Assert.That(Geometry.SegmentDistance(wall.A, wall.B, first.a, out _), Is.GreaterThan(Seating.PlayerRadius));
                Assert.That(Geometry.SegmentDistance(wall.A, wall.B, first.b, out _), Is.GreaterThan(Seating.PlayerRadius));
            }

            Assert.That(Vec2.Distance(first.a, first.b), Is.GreaterThan(Seating.PlayerRadius * 2));
        }

        [Test]
        public void Fire_AfterDriving_IsForfeit()
        {
            GameAuthority authority = TwoSeats(out PlayerState a, out _);
            authority.Apply(new DriveCommand(1, 1));
            authority.AdvanceDrive(1, 0.1);
            Assert.That(authority.Match.HasMoved, Is.True);
            Assert.That(authority.Apply(new FireCommand(1, 90, 1)), Is.Empty, "you cannot move and shoot within the same turn");
            Assert.That(a.Shots, Is.EqualTo(0));
        }

        [Test]
        public void AFlyingShot_PassesBouncesInOrder_ThenLands()
        {
            GameAuthority authority = Practice(out PlayerState ann, out _);
            authority.Apply(new FireCommand(1, 20, 1)); // up and to the right: it banks off the walls
            Volley volley = ann.LastVolley;
            int contacts = volley.Main.Contacts.Count;
            Assert.That(contacts, Is.GreaterThan(0));

            var all = new List<GameEvent>();
            for (int i = 0; i < 2000 && ann.InFlight; i++)
            {
                all.AddRange(authority.AdvanceFlight(1, 0.05));
            }

            Assert.That(ann.InFlight, Is.False);
            List<Bounced> bounces = Of<Bounced>(all);
            Assert.That(bounces.Select(b => b.Bounces), Is.EqualTo(Enumerable.Range(1, contacts)), "one event per contact, counting up");
            Assert.That(bounces.All(b => b.Shot == 1 && b.PlayerId == 1));
            Assert.That(all.Last(), Is.TypeOf<ShotLanded>());
            Assert.That(all.IndexOf(bounces.Last()), Is.LessThan(all.Count - 1), "every bounce comes before the landing");
        }

        [Test]
        public void AShotThatLandsOnADummy_IsAHitOnIt()
        {
            GameAuthority authority = Practice(out PlayerState ann, out World world);
            world.Dummies.Add(new Target("dummy:0", new Vec2(-4, -4.5), 0.45));

            authority.Apply(new FireCommand(1, AtSeatOne, 1));
            var all = new List<GameEvent>();
            for (int i = 0; i < 400 && ann.InFlight; i++)
            {
                all.AddRange(authority.AdvanceFlight(1, 0.05));
            }

            Hit hit = Of<Hit>(all).Single();
            Assert.That(hit.TargetId, Is.EqualTo("dummy:0"));
            Assert.That(hit.IsDummy, Is.True);
            Assert.That(Of<ShotLanded>(all).Single().Outcome, Is.Null, "practice has no damage to report");
        }

        [Test]
        public void AShotThatLandsOnAPlayer_HurtsScoresAndKnocksOut_InOrder()
        {
            GameAuthority authority = TwoSeats(out PlayerState a, out PlayerState b, health: 1);
            Assert.That(authority.Targets().Select(t => t.Id), Does.Contain("seat:1"), "a player in the round is a target");

            authority.Apply(new FireCommand(1, AtSeatOne, 1));
            var all = new List<GameEvent>();
            for (int i = 0; i < 400 && a.InFlight; i++)
            {
                all.AddRange(authority.AdvanceFlight(1, 0.05));
            }

            Hit hit = Of<Hit>(all).Single();
            Assert.That(hit.Seat, Is.EqualTo(1));
            Assert.That(hit.PlayerId, Is.EqualTo(1));
            Assert.That(Of<Scored>(all).Single().Seat, Is.EqualTo(0));
            Damaged damaged = Of<Damaged>(all).Single();
            Assert.That(damaged.Seat, Is.EqualTo(1));
            Assert.That(damaged.BySeat, Is.EqualTo(0));
            Assert.That(Of<Eliminated>(all).Single().Seat, Is.EqualTo(1));
            ShotLanded landed = Of<ShotLanded>(all).Single();
            Assert.That(landed.KnockedOut, Is.EqualTo(1));
            Assert.That(landed.StruckAPlayer, Is.True);
            Assert.That(landed.Outcome.DamageTo(1), Is.EqualTo(damaged.Damage));
            Assert.That(landed.Outcome.Points, Is.EqualTo(Of<Scored>(all).Single().Points), "U.RICO-T48: the \"+N pts\" floated over the shooter");
            Assert.That(landed.Outcome.Points, Is.GreaterThan(0));
            Assert.That(Of<RoundOver>(all).Single().Winner, Is.EqualTo(0));

            int Index<T>() where T : GameEvent => all.FindIndex(e => e is T);
            Assert.That(Index<Hit>(), Is.LessThan(Index<Damaged>()));
            Assert.That(Index<Damaged>(), Is.LessThan(Index<Eliminated>()));
            Assert.That(Index<Eliminated>(), Is.LessThan(Index<ShotLanded>()));
            Assert.That(authority.Targets().Select(t => t.Id), Has.No.Member("seat:1"), "a player knocked out is no longer a target");
        }

        [Test]
        public void ALandedShot_PassesTheTurn_AndTheNextTurnStartsAfterThePause()
        {
            GameAuthority authority = TwoSeats(out PlayerState a, out _);
            authority.Apply(new FireCommand(1, 90, 0));
            var landing = new List<GameEvent>();
            for (int i = 0; i < 400 && a.InFlight; i++)
            {
                landing.AddRange(authority.AdvanceFlight(1, 0.05));
            }

            TurnPassed passed = Of<TurnPassed>(landing).Single();
            Assert.That(passed.Seat, Is.EqualTo(0));
            Assert.That(passed.Reason, Is.EqualTo(TurnPassReason.ShotLanded));
            Assert.That(Of<ShotLanded>(landing).Single().StruckAPlayer, Is.False);

            IReadOnlyList<GameEvent> later = authority.Tick(MatchRules.Default.EndTurnSeconds + 0.1);
            Assert.That(Of<TurnStarted>(later).Single().Seat, Is.EqualTo(1));
        }

        // ---- CycleWeapon, CycleItem, Fuse

        [Test]
        public void CycleWeapon_SelectsTheNextAndPreviousWeapon_AndNotOutOfTurn()
        {
            GameAuthority authority = TwoSeats(out PlayerState a, out PlayerState b);

            IReadOnlyList<GameEvent> events = authority.Apply(new CycleWeaponCommand(1, 1));
            Assert.That(Of<LoadoutChanged>(events).Single().PlayerId, Is.EqualTo(1));
            Assert.That(a.Loadout.Weapon, Is.EqualTo(Weapon.Laser));
            authority.Apply(new CycleWeaponCommand(1, -1));
            Assert.That(a.Loadout.Weapon, Is.EqualTo(Weapon.Cannon));

            Assert.That(authority.Apply(new CycleWeaponCommand(2, 1)), Is.Empty, "B may not cycle on A's turn");
            Assert.That(b.Loadout.Weapon, Is.EqualTo(Weapon.Cannon));
        }

        [Test]
        public void CycleWeapon_WithOnlyTheCannon_ChangesNothing()
        {
            GameAuthority authority = Practice(out PlayerState ann, out _, rules =>
            {
                for (int i = 1; i < ArsenalRules.Kinds; i++)
                {
                    rules.SetStartCount((Weapon)i, 0);
                }
            });
            Assert.That(authority.Apply(new CycleWeaponCommand(1, 1)), Is.Empty);
            Assert.That(ann.Loadout.Weapon, Is.EqualTo(Weapon.Cannon));
        }

        [Test]
        public void CycleItem_SelectsTheNextItem_AndNotWhileAShotIsInTheAir()
        {
            GameAuthority authority = Practice(out PlayerState ann, out _);
            Assert.That(Of<LoadoutChanged>(authority.Apply(new CycleItemCommand(1, 1))), Has.Count.EqualTo(1));
            Assert.That(ann.Loadout.Item, Is.EqualTo(Item.ShieldWall));

            authority.Apply(new FireCommand(1, 90, 1));
            Assert.That(authority.Apply(new CycleItemCommand(1, 1)), Is.Empty, "nothing of yours may change while it flies");
            Assert.That(ann.Loadout.Item, Is.EqualTo(Item.ShieldWall));
        }

        [Test]
        public void SelectWeaponAndItem_PickWhatTheIconNames_OnlyWhenCarried_AndNotWhileAShotIsInTheAir()
        {
            // U.RICO-T49: obj_icon_weapon_* and obj_item_*'s Left Pressed (sfx_flick when carried, sfx_disabled when not).
            GameAuthority authority = Practice(out PlayerState ann, out _, rules => rules.SetStartCount(Weapon.Railgun, 0));
            Assert.That(Of<LoadoutChanged>(authority.Apply(new SelectWeaponCommand(1, Weapon.Grenade))), Has.Count.EqualTo(1));
            Assert.That(ann.Loadout.Weapon, Is.EqualTo(Weapon.Grenade));
            Assert.That(authority.Apply(new SelectWeaponCommand(1, Weapon.Railgun)), Is.Empty, "none carried: refused");
            Assert.That(ann.Loadout.Weapon, Is.EqualTo(Weapon.Grenade));
            Assert.That(authority.Apply(new SelectWeaponCommand(1, Weapon.Grenade)), Is.Empty, "already selected: nothing changes");

            Assert.That(Of<LoadoutChanged>(authority.Apply(new SelectItemCommand(1, Item.Teleporter))), Has.Count.EqualTo(1));
            Assert.That(ann.Loadout.Item, Is.EqualTo(Item.Teleporter));

            authority.Apply(new FireCommand(1, 90, 1));
            Assert.That(authority.Apply(new SelectItemCommand(1, Item.ShieldWall)), Is.Empty, "nothing of yours may change while it flies");
            Assert.That(ann.Loadout.Item, Is.EqualTo(Item.Teleporter));
        }

        [Test]
        public void Fuse_ShortensLengthensAndMaxesOut_OnlyForTheCaller()
        {
            GameAuthority authority = Practice(out PlayerState ann, out _);
            Assert.That(ann.Loadout.Fuse, Is.EqualTo(2));
            authority.Apply(new FuseCommand(1, 1));
            Assert.That(ann.Loadout.Fuse, Is.EqualTo(2.5));
            authority.Apply(new FuseCommand(1, -1));
            authority.Apply(new FuseCommand(1, -1));
            Assert.That(ann.Loadout.Fuse, Is.EqualTo(1.5));
            Assert.That(Of<LoadoutChanged>(authority.Apply(new FuseCommand(1, 2))), Has.Count.EqualTo(1));
            Assert.That(ann.Loadout.Fuse, Is.EqualTo(authority.Arms.MaxFuse));
        }

        // ---- UseItem

        [Test]
        public void UseItem_ExtraBounce_IsCarriedByTheNextShotThenSpent_AndClearsWhenTheTurnPasses()
        {
            GameAuthority authority = TwoSeats(out PlayerState a, out _);
            IReadOnlyList<GameEvent> used = authority.Apply(new UseItemCommand(1, 90, 0.5));
            Assert.That(Of<ItemUsed>(used).Single().Item, Is.EqualTo(Item.ExtraBounce));
            Assert.That(a.ExtraBounces, Is.EqualTo(1));
            Assert.That(a.Loadout.Count(Item.ExtraBounce), Is.EqualTo(2), "a match spends it");

            ShotFired fired = Of<ShotFired>(authority.Apply(new FireCommand(1, 90, 0.5))).Single();
            Assert.That(fired.ExtraBounces, Is.EqualTo(1), "the shot carries the extra bounce");
            Assert.That(a.ExtraBounces, Is.EqualTo(0));
        }

        [Test]
        public void UseItem_InPractice_SpendsNothing_AndExtraBouncesCarryOverToTheShot()
        {
            GameAuthority authority = Practice(out PlayerState ann, out _);
            authority.Apply(new UseItemCommand(1, 90, 0.5));
            Assert.That(ann.Loadout.Count(Item.ExtraBounce), Is.EqualTo(3));
            Assert.That(ann.ExtraBounces, Is.EqualTo(1));
        }

        [Test]
        public void UseItem_ExtraBounceUnusedIsDroppedWhenTheTurnPasses()
        {
            GameAuthority authority = TwoSeats(out PlayerState a, out _);
            authority.Apply(new UseItemCommand(1, 90, 0.5));
            authority.Apply(new EndTurnCommand(1));
            Assert.That(a.ExtraBounces, Is.EqualTo(0));
        }

        [Test]
        public void UseItem_Teleporter_MovesTheTank()
        {
            GameAuthority authority = Practice(out PlayerState ann, out _);
            authority.Apply(new CycleItemCommand(1, 1));
            authority.Apply(new CycleItemCommand(1, 1));
            authority.Apply(new CycleItemCommand(1, 1)); // shield wall, shrink ray, teleporter
            Assert.That(ann.Loadout.Item, Is.EqualTo(Item.Teleporter));
            Vec2 before = ann.Position;

            IReadOnlyList<GameEvent> events = authority.Apply(new UseItemCommand(1, 180, 0.5));
            Moved moved = Of<Moved>(events).Single();
            Assert.That(moved.Cause, Is.EqualTo(MoveCause.Teleported));
            Assert.That(moved.From, Is.EqualTo(before));
            Assert.That(ann.Position, Is.EqualTo(moved.To));
            Assert.That(ann.Position, Is.Not.EqualTo(before));
        }

        [Test]
        public void UseItem_ShieldWall_PutsAWallOnTheLevel_AndRedirectorSetsOneDown()
        {
            GameAuthority authority = Practice(out PlayerState ann, out _);
            authority.Apply(new CycleItemCommand(1, 1)); // shield wall
            authority.Apply(new UseItemCommand(1, 90, 0.5));
            Assert.That(authority.Hazards.Hazards.Count(h => h.Kind == HazardKind.ShieldWall), Is.EqualTo(1));
            Assert.That(authority.Hazards.Hazards.Single(h => h.Kind == HazardKind.ShieldWall).Owner, Is.EqualTo(0));

            for (int i = 0; i < 4; i++)
            {
                authority.Apply(new CycleItemCommand(1, 1)); // shrink ray, teleporter, aim assist, redirector
            }

            Assert.That(ann.Loadout.Item, Is.EqualTo(Item.Redirector));
            authority.Apply(new UseItemCommand(1, 90, 0.5));
            Assert.That(authority.Hazards.Hazards.Count(h => h.Kind == HazardKind.Redirector), Is.EqualTo(1));
        }

        [Test]
        public void UseItem_ShrinkRay_ShrinksTheFirstPlayerItReaches()
        {
            GameAuthority authority = TwoSeats(out PlayerState a, out PlayerState b);
            authority.Apply(new CycleItemCommand(1, 1));
            authority.Apply(new CycleItemCommand(1, 1)); // shrink ray
            Assert.That(a.Loadout.Item, Is.EqualTo(Item.ShrinkRay));

            IReadOnlyList<GameEvent> events = authority.Apply(new UseItemCommand(1, AtSeatOne, 0.5));
            Shrunk shrunk = Of<Shrunk>(events).Single();
            Assert.That(shrunk.PlayerId, Is.EqualTo(2));
            Assert.That(shrunk.BySeat, Is.EqualTo(0));
            Assert.That(b.Scale, Is.EqualTo((float)authority.Arms.ShrinkScale));
            Assert.That(b.Radius, Is.LessThan(Seating.PlayerRadius));
        }

        [Test]
        public void UseItem_AimAssist_TurnsTheAimOntoAShotAtAnOpponent()
        {
            GameAuthority authority = TwoSeats(out PlayerState a, out _);
            for (int i = 0; i < 4; i++)
            {
                authority.Apply(new CycleItemCommand(1, 1)); // shield wall, shrink ray, teleporter, aim assist
            }

            Assert.That(a.Loadout.Item, Is.EqualTo(Item.AimAssist));
            IReadOnlyList<GameEvent> events = authority.Apply(new UseItemCommand(1, 90, 0.5));
            AssistApplied assist = Of<AssistApplied>(events).Single();
            Assert.That(assist.Found, Is.True);
            Assert.That(a.Assists, Is.EqualTo(1));
            Assert.That(a.AssistHeading, Is.EqualTo(a.Heading));
            Assert.That(a.AssistPower, Is.EqualTo(a.Power));
            Assert.That(a.Heading, Is.Not.EqualTo(90), "the aim moved onto a shot");
        }

        [Test]
        public void UseItem_OutOfTurnOrWithoutTheItem_ChangesNothing()
        {
            GameAuthority authority = TwoSeats(out _, out PlayerState b, arms: rules => rules.SetStartCount(Item.ExtraBounce, 0));
            Assert.That(authority.Apply(new UseItemCommand(2, 90, 0.5)), Is.Empty);
            Assert.That(b.ExtraBounces, Is.EqualTo(0));

            GameAuthority practice = Practice(out PlayerState ann, out _, rules =>
            {
                for (int i = 0; i < ArsenalRules.Kinds; i++)
                {
                    rules.SetStartCount((Item)i, 0);
                }
            });
            Assert.That(practice.Apply(new UseItemCommand(1, 90, 0.5)), Is.Empty, "nothing carried, nothing to use");
        }

        // ---- Detonate

        [Test]
        public void Detonate_CutsAVortexShortWhereItIs_AndLandsItAtOnce()
        {
            GameAuthority authority = Practice(out PlayerState ann, out World world, rules =>
            {
                rules.SetStartCount(Weapon.Cannon, 0);
                rules.SetStartCount(Weapon.Laser, 0);
                rules.SetStartCount(Weapon.Shotgun, 0);
                rules.SetStartCount(Weapon.Grenade, 0);
                rules.SetStartCount(Weapon.Railgun, 0);
            });
            Assert.That(ann.Loadout.Weapon, Is.EqualTo(Weapon.Vortex));

            authority.Apply(new FireCommand(1, 180, 1)); // along the floor, a long way
            authority.AdvanceFlight(1, 0.05);
            Assert.That(ann.InFlight, Is.True);

            double before = ann.Flight.Flights[0].Travelled;
            Assert.That(before, Is.GreaterThan(0));
            IReadOnlyList<GameEvent> events = authority.Apply(new DetonateCommand(1));
            ShotCut cut = Of<ShotCut>(events).Single();
            Assert.That(cut.At, Is.EqualTo((float)before).Within(1e-6));
            Assert.That(cut.Volley.Main.End, Is.EqualTo(ShotEndKind.Detonated));
            Assert.That(ann.ShotDetonation, Is.EqualTo(cut.At));
            Assert.That(Of<ShotLanded>(events), Has.Count.EqualTo(1), "it lands at once");
            Assert.That(ann.InFlight, Is.False);
            Assert.That(events.TakeWhile(e => !(e is ShotLanded)).Any(e => e is ShotCut));

            Assert.That(authority.Apply(new DetonateCommand(1)), Is.Empty, "only once");
        }

        [Test]
        public void Detonate_WithNothingInTheAirOrANonVortex_ChangesNothing()
        {
            GameAuthority authority = Practice(out PlayerState ann, out _);
            Assert.That(authority.Apply(new DetonateCommand(1)), Is.Empty);
            authority.Apply(new FireCommand(1, 45, 1));
            Assert.That(authority.Apply(new DetonateCommand(1)), Is.Empty, "a cannon shell cannot be set off");
        }

        // ---- Drive

        [Test]
        public void Drive_MovesTheTankAlongTheBarrel_WhileTheCommandIsHeld()
        {
            GameAuthority authority = Practice(out PlayerState ann, out _);
            authority.Apply(new AimCommand(1, 0)); // to the right
            Vec2 start = ann.Position;

            Assert.That(authority.Apply(new DriveCommand(1, 1)), Is.Empty, "the command only sets the direction");
            IReadOnlyList<GameEvent> events = authority.AdvanceDrive(1, 0.1);
            Moved moved = Of<Moved>(events).Single();
            Assert.That(moved.Cause, Is.EqualTo(MoveCause.Drove));
            Assert.That(ann.Position.X, Is.GreaterThan(start.X));
            Assert.That(ann.Position.Y, Is.EqualTo(start.Y).Within(1e-6));

            authority.Apply(new DriveCommand(1, -1));
            double x = ann.Position.X;
            authority.AdvanceDrive(1, 0.1);
            Assert.That(ann.Position.X, Is.LessThan(x), "back is the other way");
        }

        [Test]
        public void Drive_StopsOnAZeroCommand_AndWhenTheCommandsStopArriving()
        {
            GameAuthority authority = Practice(out PlayerState ann, out _);
            authority.Apply(new AimCommand(1, 0));
            authority.Apply(new DriveCommand(1, 1));
            authority.Apply(new DriveCommand(1, 0));
            Assert.That(authority.AdvanceDrive(1, 0.1), Is.Empty);

            authority.Apply(new DriveCommand(1, 1));
            int steps = 0;
            while (authority.AdvanceDrive(1, 0.1).Count > 0 && steps < 100)
            {
                steps++;
            }

            Assert.That(steps, Is.LessThanOrEqualTo(3), "past " + GameAuthority.DriveHold + " seconds with no new command the tank stops");
            Assert.That(ann.DriveDirection, Is.EqualTo(0));
        }

        [Test]
        public void Drive_InAMatch_OnlyOnYourTurn_AndTheFirstDriveForfeitsTheShot()
        {
            GameAuthority authority = TwoSeats(out PlayerState a, out PlayerState b);
            authority.Apply(new DriveCommand(2, 1));
            Assert.That(b.DriveDirection, Is.EqualTo(0), "not B's turn");

            authority.Apply(new AimCommand(1, 0));
            authority.Apply(new DriveCommand(1, 1));
            IReadOnlyList<GameEvent> first = authority.AdvanceDrive(1, 0.05);
            Assert.That(Of<DriveStarted>(first).Single().Seat, Is.EqualTo(0));
            IReadOnlyList<GameEvent> second = authority.AdvanceDrive(1, 0.05);
            Assert.That(Of<DriveStarted>(second), Is.Empty, "only the first drive of a turn is told");
            Assert.That(authority.Match.HasMoved, Is.True);
        }

        [Test]
        public void Drive_NeverDrivesOutThroughAWall()
        {
            GameAuthority authority = Practice(out PlayerState ann, out _);
            authority.Apply(new AimCommand(1, 270)); // straight down: the floor is right there
            for (int i = 0; i < 200; i++)
            {
                authority.Apply(new DriveCommand(1, 1));
                authority.AdvanceDrive(1, 0.1);
            }

            Assert.That(ann.Position.Y, Is.GreaterThan(authority.Layout.Level.MinY));
        }

        // ---- EndTurn

        [Test]
        public void EndTurn_GivesUpTheTurn_ThePassIsToldBeforeTheNextTurnStarts()
        {
            GameAuthority authority = TwoSeats(out _, out _);
            IReadOnlyList<GameEvent> events = authority.Apply(new EndTurnCommand(1));

            TurnPassed passed = Of<TurnPassed>(events).Single();
            Assert.That(passed.Seat, Is.EqualTo(0));
            Assert.That(passed.Reason, Is.EqualTo(TurnPassReason.EndedEarly));
            Assert.That(Of<TurnStarted>(events).Single().Seat, Is.EqualTo(1));
            Assert.That(events.ToList().FindIndex(e => e is TurnPassed), Is.LessThan(events.ToList().FindIndex(e => e is TurnStarted)));
            Assert.That(authority.Match.Current, Is.EqualTo(1));
        }

        [Test]
        public void EndTurn_OutOfTurnInPracticeOrWithAShotInTheAir_ChangesNothing()
        {
            GameAuthority authority = TwoSeats(out _, out _);
            Assert.That(authority.Apply(new EndTurnCommand(2)), Is.Empty, "not B's turn");
            Assert.That(authority.Match.Current, Is.EqualTo(0));

            authority.Apply(new FireCommand(1, 90, 1));
            Assert.That(authority.Apply(new EndTurnCommand(1)), Is.Empty, "too late: it is in the air");

            GameAuthority practice = Practice(out _, out _);
            Assert.That(practice.Apply(new EndTurnCommand(1)), Is.Empty, "practice has no turns");
        }

        [Test]
        public void ATurnThatRunsOutOfTime_CountsDownThenPassesAsTimedOut()
        {
            GameAuthority authority = TwoSeats(out _, out _);
            var all = new List<GameEvent>();
            for (int i = 0; i < 25; i++)
            {
                all.AddRange(authority.Tick(1));
            }

            Assert.That(Of<Countdown>(all).Select(c => c.SecondsLeft), Is.EqualTo(new[] { 4, 3, 2, 1 }));
            TurnPassed passed = Of<TurnPassed>(all).First();
            Assert.That(passed.Reason, Is.EqualTo(TurnPassReason.TimedOut));
            Assert.That(passed.Seat, Is.EqualTo(0));
            Assert.That(Of<TurnStarted>(all).First().Seat, Is.EqualTo(1));
        }

        // ---- Reroll

        [Test]
        public void Reroll_InPractice_RollsAnotherLevelAndStandsEveryoneAtANewSpot()
        {
            GameAuthority authority = Practice(out PlayerState ann, out _);
            PlayerState bob = authority.AddPlayer(2);
            authority.SetSeat(2, 1);
            int version = authority.LayoutVersion;

            IReadOnlyList<GameEvent> events = authority.Apply(new RerollCommand(1));

            LayoutChanged layout = Of<LayoutChanged>(events).Single();
            Assert.That(layout.Rolled, Is.True);
            Assert.That(layout.Version, Is.EqualTo(version + 1));
            Assert.That(layout.Map, Is.EqualTo(Maps.RandomIndex));
            Assert.That(layout.Seed, Is.GreaterThan(0));
            Assert.That(Of<Moved>(events).Select(m => m.PlayerId), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(ann.Position, Is.EqualTo(Quantised(authority.Layout.SpawnPoint(0))));
            Assert.That(bob.Position, Is.EqualTo(Quantised(authority.Layout.SpawnPoint(1))));
            LevelRerolled rerolled = Of<LevelRerolled>(events).Single();
            Assert.That(rerolled.PlayerId, Is.EqualTo(1));
            Assert.That(rerolled.Seed, Is.EqualTo(layout.Seed));
            Assert.That(events.Last(), Is.SameAs(rerolled));

            int seed = authority.LayoutSeed;
            authority.Apply(new RerollCommand(1));
            Assert.That(authority.LayoutSeed, Is.Not.EqualTo(seed), "another roll, another level");
            Assert.That(authority.LayoutVersion, Is.EqualTo(version + 2));
        }

        [Test]
        public void Reroll_DuringAMatch_IsRefused()
        {
            GameAuthority authority = TwoSeats(out _, out _);
            int version = authority.LayoutVersion;
            Assert.That(authority.Apply(new RerollCommand(1)), Is.Empty);
            Assert.That(authority.LayoutVersion, Is.EqualTo(version));
        }

        [Test]
        public void AdoptLayout_PlaysTheLevelAPeerAlreadyHas_AndTheVersionNeverGoesBack()
        {
            GameAuthority authority = Practice(out _, out _);
            authority.AdoptLayout(Maps.RandomIndex, 1234, 7);
            Assert.That(authority.MapIndex, Is.EqualTo(Maps.RandomIndex));
            Assert.That(authority.LayoutSeed, Is.EqualTo(1234));
            Assert.That(authority.Layout.SpawnPoint(0), Is.EqualTo(Maps.At(Maps.RandomIndex).Layout(1234).SpawnPoint(0)));

            IReadOnlyList<GameEvent> events = authority.Reroll(1);
            Assert.That(Of<LayoutChanged>(events).Single().Version, Is.EqualTo(8));
        }

        private static Vec2 Quantised(Vec2 v) => new Vec2((float)v.X, (float)v.Y);

        // ---- Vote

        [Test]
        public void Vote_PassesAtTwoThirds_AndEachVoterCountsOnce()
        {
            GameAuthority authority = Practice(out _, out _);
            for (int id = 2; id <= 4; id++)
            {
                authority.AddPlayer(id);
            }

            authority.OpenVote("kick", 4);
            Assert.That(authority.VoteOpen("kick"), Is.True);

            IReadOnlyList<GameEvent> first = authority.Apply(new VoteCommand(1, "kick", true));
            Assert.That(Of<VoteCast>(first).Single().Yes, Is.True);
            Assert.That(Of<VotePassed>(first), Is.Empty);
            authority.Apply(new VoteCommand(1, "kick", true));
            Assert.That(authority.VoteOpen("kick"), Is.True, "the same voter twice is still one vote");
            Assert.That(Of<VotePassed>(authority.Apply(new VoteCommand(2, "kick", true))), Is.Empty);

            IReadOnlyList<GameEvent> last = authority.Apply(new VoteCommand(3, "kick", true));
            VotePassed passed = Of<VotePassed>(last).Single();
            Assert.That(passed.Yes, Is.EqualTo(3));
            Assert.That(passed.Kind, Is.EqualTo("kick"));
            Assert.That(authority.VoteOpen("kick"), Is.False, "a decided vote closes");
        }

        [Test]
        public void Vote_FailsOnceItCannotPass()
        {
            GameAuthority authority = Practice(out _, out _);
            for (int id = 2; id <= 3; id++)
            {
                authority.AddPlayer(id);
            }

            authority.OpenVote("restart", 3);
            Assert.That(Of<VoteFailed>(authority.Apply(new VoteCommand(1, "restart", false))), Is.Empty, "two yes votes could still pass it");
            VoteFailed failed = Of<VoteFailed>(authority.Apply(new VoteCommand(2, "restart", false))).Single();
            Assert.That(failed.No, Is.EqualTo(2));
            Assert.That(authority.VoteOpen("restart"), Is.False);
        }

        [Test]
        public void Vote_ChangingYourMindCounts_AndAVoteThatIsNotOpenIsRefused()
        {
            GameAuthority authority = Practice(out _, out _);
            Assert.That(authority.Apply(new VoteCommand(1, "kick", true)), Is.Empty, "nothing to vote on");

            authority.AddPlayer(2);
            authority.OpenVote("kick", 2, 1.0);
            authority.Apply(new VoteCommand(1, "kick", false));
            Assert.That(authority.VoteOpen("kick"), Is.False, "one no already decides a unanimous vote");

            authority.OpenVote("kick", 3, 1.0);
            authority.Apply(new VoteCommand(1, "kick", true));
            Assert.That(Of<VotePassed>(authority.Apply(new VoteCommand(2, "kick", true))), Is.Empty);
            Assert.That(authority.VoteOpen("kick"), Is.True);
        }

        // ---- Crates

        [Test]
        public void ACrate_FallsLandsAndIsCollectedByAShotThatPassesOverIt()
        {
            GameAuthority authority = TwoSeats(out PlayerState a, out _);
            Drop crate = authority.DropCrate(DropKind.Item, new Vec2(0, -1));
            Assert.That(crate, Is.Not.Null);

            IReadOnlyList<GameEvent> landing = authority.Tick(5);
            Assert.That(Of<CrateLanded>(landing).Single().Crate, Is.SameAs(crate));

            int items = Enumerable.Range(0, ArsenalRules.Kinds).Sum(i => a.Loadout.Count((Item)i));
            authority.Apply(new FireCommand(1, 90, 1));
            var all = new List<GameEvent>();
            for (int i = 0; i < 400 && a.InFlight; i++)
            {
                all.AddRange(authority.AdvanceFlight(1, 0.05));
            }

            CratePickedUp picked = Of<CratePickedUp>(all).Single();
            Assert.That(picked.PlayerId, Is.EqualTo(1));
            Assert.That(picked.Kind, Is.EqualTo(DropKind.Item));
            Assert.That(Of<CrateRemoved>(all).Single().Crate, Is.SameAs(crate));
            Assert.That(Enumerable.Range(0, ArsenalRules.Kinds).Sum(i => a.Loadout.Count((Item)i)), Is.EqualTo(items + 1));
            Assert.That(a.Pickups, Is.EqualTo(1));
            Assert.That(a.LastPickup / 1000, Is.EqualTo((int)DropKind.Item));
        }

        [Test]
        public void DropCrate_OutsideAMatch_IsNull()
        {
            GameAuthority authority = Practice(out _, out _);
            Assert.That(authority.DropCrate(DropKind.Item, new Vec2(0, 0)), Is.Null);
        }

        // ---- Rounds

        [Test]
        public void ARoundOverWithTheGoalReached_EndsTheGame_AndAnotherStartsWhenItIsNot()
        {
            GameAuthority single = TwoSeats(out PlayerState a, out _, health: 1);
            KnockOutSeatOne(single, a);
            IReadOnlyList<GameEvent> after = single.Tick(MatchRules.Default.RoundOverSeconds + 0.1);
            Assert.That(Of<GameOver>(after).Single().Champion, Is.EqualTo(0));
            Assert.That(single.Match.Phase, Is.EqualTo(MatchPhase.GameOver));

            GameAuthority long_ = TwoSeats(out PlayerState c, out PlayerState d, health: 1, goal: 10000);
            KnockOutSeatOne(long_, c);
            IReadOnlyList<GameEvent> next = long_.Tick(MatchRules.Default.RoundOverSeconds + 0.1);
            Assert.That(Of<RoundStarted>(next).Single().Round, Is.EqualTo(2));
            Assert.That(Of<LayoutChanged>(next).Single().Rolled, Is.True);
            Assert.That(Of<Moved>(next).Select(m => m.PlayerId), Is.EquivalentTo(new[] { 1, 2 }), "everyone stands at a spot again");
            Assert.That(Of<LoadoutChanged>(next).Select(l => l.PlayerId), Is.EquivalentTo(new[] { 1, 2 }), "and has the starting loadout");
            Assert.That(d.Loadout.Count(Weapon.Laser), Is.EqualTo(3));
        }

        private static void KnockOutSeatOne(GameAuthority authority, PlayerState shooter)
        {
            authority.Apply(new FireCommand(shooter.Id, AtSeatOne, 1));
            for (int i = 0; i < 400 && shooter.InFlight; i++)
            {
                authority.AdvanceFlight(shooter.Id, 0.05);
            }

            Assert.That(authority.Match.Phase, Is.EqualTo(MatchPhase.RoundOver));
        }

        // ---- the authority as a whole

        [Test]
        public void EveryEventIsPublishedAsItHappens_InTheSameOrderThePlayerIsAnswered()
        {
            GameAuthority authority = Practice(out _, out _);
            var published = new List<GameEvent>();
            authority.Published += published.Add;

            IReadOnlyList<GameEvent> answer = authority.Apply(new FireCommand(1, 45, 1));
            Assert.That(published, Is.EqualTo(answer));
            Assert.That(published, Is.Not.Empty);

            published.Clear();
            IReadOnlyList<GameEvent> flight = authority.AdvanceFlight(1, 0.5);
            Assert.That(published, Is.EqualTo(flight));
        }

        [Test]
        public void TwoAuthorities_ShareNothing_ThereIsNoCurrentPlayer()
        {
            GameAuthority one = Practice(out PlayerState oneAnn, out _);
            GameAuthority two = Practice(out PlayerState twoAnn, out _);

            one.Apply(new AimCommand(1, 10));
            two.Apply(new AimCommand(1, 200));
            Assert.That(oneAnn.Heading, Is.EqualTo(10));
            Assert.That(twoAnn.Heading, Is.EqualTo(200));
            one.Apply(new FireCommand(1, 10, 1));
            Assert.That(oneAnn.InFlight, Is.True);
            Assert.That(twoAnn.InFlight, Is.False);
        }

        [Test]
        public void Targets_AreTheDummiesInPractice_AndThePlayersStillInTheRoundInAMatch()
        {
            GameAuthority practice = Practice(out _, out World world);
            world.Dummies.Add(new Target("dummy:1", new Vec2(1, 1), 0.45));
            Assert.That(practice.Targets().Select(t => t.Id), Is.EqualTo(new[] { "dummy:1" }));

            GameAuthority match = TwoSeats(out PlayerState a, out _);
            Assert.That(match.Targets().Select(t => t.Id), Is.EqualTo(new[] { "seat:0", "seat:1" }));
            Assert.That(match.Targets().First().Center, Is.EqualTo(a.Position));
        }

        [Test]
        public void APlayerRestoredFromASnapshot_KeepsWhatItHad()
        {
            GameAuthority authority = Practice(out PlayerState ann, out _);
            authority.Apply(new AimCommand(1, 33));
            authority.Apply(new CycleWeaponCommand(1, 1));
            authority.Apply(new FuseCommand(1, 1));

            var other = new GameAuthority(new World(), seed: 3);
            PlayerState moved = other.AddPlayer(1, false, PlayerSnapshot.Of(ann));
            Assert.That(moved.Heading, Is.EqualTo(33));
            Assert.That(moved.Position, Is.EqualTo(ann.Position));
            Assert.That(moved.Loadout.Weapon, Is.EqualTo(Weapon.Laser));
            Assert.That(moved.Loadout.Fuse, Is.EqualTo(2.5));
            Assert.That(other.AddPlayer(1), Is.SameAs(moved), "adding the same id again returns the player");
        }

        [Test]
        public void ARemovedPlayer_IsNoLongerTakingCommands()
        {
            GameAuthority authority = Practice(out _, out _);
            authority.RemovePlayer(1);
            Assert.That(authority.Apply(new AimCommand(1, 10)), Is.Empty);
            Assert.That(authority.Player(1), Is.Null);
        }
    

        // ---- Seat lifecycle (UR-T65)

        [Test]
        public void JoinSeat_SeatsAHumanOrAnAi_AndRefusesATakenOrMissingSeat()
        {
            var authority = new GameAuthority(new World(), seed: 5);
            PlayerState a = authority.AddPlayer(1);
            PlayerState b = authority.AddPlayer(2, true);
            Assert.That(authority.Apply(new JoinSeatCommand(1, 0, false, "A")), Is.Empty, "no match yet");

            authority.BeginMatch(0, MatchSetup.OneRound);
            SeatJoined joined = Of<SeatJoined>(authority.Apply(new JoinSeatCommand(1, 0, false, "A"))).Single();
            Assert.That(joined.Seat, Is.EqualTo(0));
            Assert.That(joined.IsAi, Is.False);
            Assert.That(a.Seat, Is.EqualTo(0));
            Assert.That(authority.Match.Seat(0).Kind, Is.EqualTo(SeatKind.Human));

            Assert.That(authority.Apply(new JoinSeatCommand(2, 0, true, "AI")), Is.Empty, "the seat is taken");
            Assert.That(authority.Apply(new JoinSeatCommand(2, 99, true, "AI")), Is.Empty, "no such seat");
            Assert.That(Of<SeatJoined>(authority.Apply(new JoinSeatCommand(2, 1, true, "AI"))).Single().IsAi, Is.True);
            Assert.That(b.Seat, Is.EqualTo(1));
            Assert.That(authority.Match.Seat(1).Kind, Is.EqualTo(SeatKind.Ai));
        }

        [Test]
        public void DropAndRejoin_HoldTheSeat_AndOnlyForAHumanWhoIsSeated()
        {
            GameAuthority authority = TwoSeats(out _, out _);
            Assert.That(Of<SeatDropped>(authority.Apply(new DropSeatCommand(1, 30))).Single().Seat, Is.EqualTo(0));
            Assert.That(authority.Match.Seat(0).Away, Is.True);
            Assert.That(authority.Apply(new DropSeatCommand(1, 30)), Is.Empty, "already away");

            Assert.That(Of<SeatRejoined>(authority.Apply(new RejoinSeatCommand(1))).Single().Seat, Is.EqualTo(0));
            Assert.That(authority.Match.Seat(0).Away, Is.False);
            Assert.That(authority.Apply(new RejoinSeatCommand(1)), Is.Empty, "not away any more");
            Assert.That(authority.Apply(new DropSeatCommand(77, 30)), Is.Empty, "unknown player");
        }

        [Test]
        public void LeaveSeat_DropsTheSeatOut_EvenForAPlayerAlreadyRemoved()
        {
            GameAuthority authority = TwoSeats(out _, out _);
            authority.RemovePlayer(2); // the kit despawns the player with its connection, before or after the seat is released
            IReadOnlyList<GameEvent> events = authority.Apply(new LeaveSeatCommand(2, 1));
            Assert.That(Of<SeatLeft>(events).Single().Seat, Is.EqualTo(1));
            Assert.That(authority.Match.Seat(1).Present, Is.False);
            Assert.That(authority.Match.Phase, Is.EqualTo(MatchPhase.RoundOver), "one seat left standing ends the round");
            Assert.That(authority.Apply(new LeaveSeatCommand(2, 5)), Is.Empty, "no such seat");
        }

        [Test]
        public void LeaveSeat_NeverLeavesAnotherPlayersSeat()
        {
            GameAuthority authority = TwoSeats(out _, out _);
            Assert.That(authority.Apply(new LeaveSeatCommand(1, 1)), Is.Empty, "player 1 sits in seat 0, not seat 1");
            Assert.That(authority.Match.Seat(1).Present, Is.True);
            Assert.That(Of<SeatLeft>(authority.Apply(new LeaveSeatCommand(-1, 1))).Single().Seat, Is.EqualTo(1), "no such player: the seat named goes");
        }

        [Test]
        public void Vote_ThresholdIsExactForOddElectorates()
        {
            foreach ((int electorate, int needed) in new[] { (3, 2), (7, 3), (7, 5), (5, 4), (9, 6) })
            {
                var authority = new GameAuthority(new World(), seed: 5);
                for (int id = 1; id <= electorate; id++)
                {
                    authority.AddPlayer(id);
                }

                authority.OpenVote("restart", electorate, (double)needed / electorate);
                for (int id = 1; id < needed; id++)
                {
                    Assert.That(Of<VotePassed>(authority.Apply(new VoteCommand(id, "restart", true))), Is.Empty, electorate + ":" + needed);
                }

                Assert.That(Of<VotePassed>(authority.Apply(new VoteCommand(needed, "restart", true))), Has.Count.EqualTo(1), electorate + ":" + needed);
            }
        }

        // ---- U.RICO-T48: what every peer draws when a shield wall breaks or a crate goes

        [Test]
        public void AShieldWallThatBreaks_ShattersWithItsSound_OrDecaysSilently_AndOneSweptAwayRaisesNothing()
        {
            var authority = new GameAuthority(new World(), seed: 3);
            var heard = new List<GameEvent>();
            authority.Published += heard.Add;

            Hazard struck = authority.Hazards.AddShieldWall(0, Vec2.Zero, 0);
            Hazard decaying = authority.Hazards.AddShieldWall(0, new Vec2(3, 0), 0);
            authority.Hazards.Struck(struck.Id, 1);
            authority.Hazards.Tick(1000);
            Hazard swept = authority.Hazards.AddShieldWall(0, new Vec2(-3, 0), 0);
            Hazard tuft = authority.Hazards.AddFoliage(new Vec2(0, 3));
            authority.Hazards.Struck(tuft.Id, 1);
            authority.Hazards.Clear();

            List<ShieldWallShattered> broken = Of<ShieldWallShattered>(heard);
            Assert.That(broken.Select(b => b.HazardId), Is.EqualTo(new[] { struck.Id, decaying.Id }), "the swept wall and the tuft raise nothing");
            Assert.That(swept.End, Is.EqualTo(HazardEnd.Cleared));
            Assert.That(broken[0].Struck, Is.True, "a shot breaks it: sfx_shatter");
            Assert.That(broken[0].Center, Is.EqualTo(struck.Center));
            Assert.That(broken[1].Struck, Is.False, "decayDelay: no sound");
        }

        [Test]
        public void ACrateThatBursts_OrIsCollected_IsDrawnWhereItLay_AndOneSweptAwayIsNot()
        {
            var drops = new DropRules { MinLife = 1, MaxLife = 1, MinDelay = 1000, MaxDelay = 1000 };
            var authority = new GameAuthority(new World(), matchRules: NoReadyCard.Rules(), drops: drops, seed: 7);
            authority.AddPlayer(1);
            authority.AddPlayer(2);
            authority.SetSeat(1, 0);
            authority.SetSeat(2, 1);
            Match match = authority.BeginMatch(0, MatchSetup.OneRound);
            match.Join(0, SeatKind.Human, "A");
            match.Join(1, SeatKind.Human, "B");
            authority.StartMatch();

            Drop bursting = authority.DropCrate(DropKind.Health, new Vec2(2, 2));
            var all = new List<GameEvent>();
            for (int i = 0; i < 40; i++)
            {
                all.AddRange(authority.Tick(0.1));
            }

            CrateGone gone = Of<CrateGone>(all).Single();
            Assert.That(gone.CrateId, Is.EqualTo(bursting.Id));
            Assert.That(gone.End, Is.EqualTo(DropEnd.Burst));
            Assert.That(gone.Kind, Is.EqualTo(DropKind.Health));
            Assert.That(gone.Spot, Is.EqualTo(new Vec2(2, 2)));

            Drop swept = authority.DropCrate(DropKind.Item, new Vec2(-2, 2));
            Assert.That(swept, Is.Not.Null);
            IReadOnlyList<GameEvent> quit = authority.QuitMatch(1); // the pause menu's Quit round sweeps the crates away
            Assert.That(Of<CrateRemoved>(quit), Has.Count.EqualTo(1));
            Assert.That(Of<CrateGone>(quit), Is.Empty);
        }
    }
}
