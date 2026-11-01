using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Rea.Ricochet.Core.Tests
{
    using Rea.Ricochet.Core.Aim;
    using Rea.Ricochet.Core.Arsenal;
    using Rea.Ricochet.Core.Commands;
    using Rea.Ricochet.Core.Level;
    using Rea.Ricochet.Core.Match;

    // U.RICO-T146 (follow-up to U.RICO-T58's by-hand pass): a deterministic, seeded Core-only replay of one full match
    // that fires every weapon and uses every item once, in a fixed order, with no engine and no Runtime. Every weapon and
    // item's own behaviour already has its tests (CommandTests and the Assets/Rea.Ricochet/Tests suites); this one instead
    // checks the thing those cannot: that a single continuous match can carry every one of them back to back with the
    // same authority, seed and level, the way U.RICO-T58's manual playthrough did by eye.
    public class SeededReplayTests
    {
        private sealed class World : IGameWorld
        {
            public readonly List<Target> Dummies = new List<Target>();

            public IReadOnlyList<Target> DummyTargets() => Dummies;
        }

        private static List<T> Of<T>(IEnumerable<GameEvent> events) where T : GameEvent => events.OfType<T>().ToList();

        // Two human seats, a round that never ends on points (OneRound) and health too high for this fixture's shots to
        // knock anyone out partway through: every weapon and item gets its turn regardless of what it hits.
        private static GameAuthority TwoSeats(out PlayerState a, out PlayerState b)
        {
            ArsenalRules arms = ArsenalRules.Default;
            MatchRules matchRules = NoReadyCard.Rules();
            matchRules.MaxHealth = 1_000_000;
            var authority = new GameAuthority(new World(), matchRules: matchRules, arms: arms, seed: 7);
            a = authority.AddPlayer(1);
            b = authority.AddPlayer(2);
            authority.SetSeat(1, 0);
            authority.SetSeat(2, 1);
            Match match = authority.BeginMatch(0, MatchSetup.OneRound);
            match.Join(0, SeatKind.Human, "A");
            match.Join(1, SeatKind.Human, "B");
            authority.StartMatch();
            return authority;
        }

        // A spread of headings so no two shots in the replay bank exactly the same way.
        private static double Heading(int step) => 30 + (step * 47 % 360);

        private static void ResolveShot(GameAuthority authority, PlayerState shooter)
        {
            for (int i = 0; i < 2000 && shooter.InFlight; i++)
            {
                authority.AdvanceFlight(shooter.Id, 0.05);
            }

            Assert.That(shooter.InFlight, Is.False, "seat " + shooter.Seat + "'s shot never landed within the budget");
        }

        private static void ResolveItem(GameAuthority authority, int playerId)
        {
            for (int i = 0; i < 2000 && authority.ItemsInFlight(playerId); i++)
            {
                authority.AdvanceItems(playerId, 0.05);
            }

            Assert.That(authority.ItemsInFlight(playerId), Is.False, "player " + playerId + "'s item never finished within the budget");
        }

        // A fired weapon ends its own turn once it lands and the server's pause after it elapses (GameEvents.cs,
        // TurnPassReason.ShotLanded); fall back to EndTurn for anything the fixture's Tick budget does not catch.
        private static void PassTurn(GameAuthority authority, int playerId)
        {
            int before = authority.Match.Current;
            for (int i = 0; i < 40 && authority.Match.Current == before; i++)
            {
                authority.Tick(0.5);
            }

            if (authority.Match.Current == before)
            {
                authority.Apply(new EndTurnCommand(playerId));
            }
        }

        [Test]
        public void FullMatch_SeededReplay_FiresEveryWeaponAndUsesEveryItemOnce()
        {
            GameAuthority authority = TwoSeats(out PlayerState a, out PlayerState b);
            var weapons = (Weapon[])Enum.GetValues(typeof(Weapon));
            var items = (Item[])Enum.GetValues(typeof(Item));
            Assert.That(weapons, Has.Length.EqualTo(ArsenalRules.Kinds), "every weapon, not a subset");
            Assert.That(items, Has.Length.EqualTo(ArsenalRules.Kinds), "every item, not a subset");

            var shotWeapons = new List<Weapon>();
            var usedItems = new List<Item>();

            for (int i = 0; i < ArsenalRules.Kinds; i++)
            {
                // A's turn: the next weapon in the original's cycling order (Arsenal.cs).
                Assert.That(authority.Match.Current, Is.EqualTo(0), "seat 0 (A) is up");
                authority.Apply(new SelectWeaponCommand(a.Id, weapons[i]));
                ShotFired shot = Of<ShotFired>(authority.Apply(new FireCommand(a.Id, Heading(i)))).SingleOrDefault();
                Assert.That(shot, Is.Not.Null, weapons[i] + " did not fire");
                shotWeapons.Add(shot.Weapon);
                ResolveShot(authority, a);
                PassTurn(authority, a.Id);

                // B's turn: the next item. Using one never ends a turn by itself (CommandTests), so EndTurn does.
                Assert.That(authority.Match.Current, Is.EqualTo(1), "seat 1 (B) is up");
                authority.Apply(new SelectItemCommand(b.Id, items[i]));
                ItemUsed used = Of<ItemUsed>(authority.Apply(new UseItemCommand(b.Id, Heading(i)))).SingleOrDefault();
                Assert.That(used, Is.Not.Null, items[i] + " was not used");
                usedItems.Add(used.Item);
                ResolveItem(authority, b.Id);
                authority.Apply(new EndTurnCommand(b.Id));
            }

            Assert.That(shotWeapons, Is.EquivalentTo(weapons), "the replay fired every weapon exactly once");
            Assert.That(usedItems, Is.EquivalentTo(items), "the replay used every item exactly once");
            Assert.That(a.Shots, Is.EqualTo(ArsenalRules.Kinds));
        }
    }
}
