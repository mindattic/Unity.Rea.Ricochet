using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Ricochet.Core.Tests
{
    using Ricochet.Core.Ai;
    using Ricochet.Core.Aim;
    using Ricochet.Core.Arsenal;
    using Ricochet.Core.Level;
    using Ricochet.Core.Match;
    using Ricochet.Core.Menu;
    // UR-T9: the room graph (titlescreen -> mainmenu -> players -> maproom -> gamescreen -> endgame), the how-to-play
    // slides, the maps, the game's goal and the endgame recap.
    public class MenuFlowTests
    {
        [Test]
        public void TheRoomGraphRunsTitleToMainMenuToPlayersToMapToLobbyToGameToEndgame_AndBackToTheMainMenu()
        {
            var walked = new List<Room> { Room.Title };
            for (int i = 0; i < 7; i++)
            {
                walked.Add(MenuFlow.Next(walked[walked.Count - 1]));
            }

            Assert.That(walked, Is.EqualTo(new[]
            {
                Room.Title, Room.MainMenu, Room.Players, Room.MapRoom, Room.Lobby, Room.Game, Room.Endgame, Room.MainMenu,
            }));
            Assert.That(MenuFlow.Back(Room.Lobby), Is.EqualTo(Room.MainMenu), "leaving the lobby ends the hosted game");
            Assert.That(MenuFlow.Title(Room.Lobby), Is.EqualTo("Lobby"), "the kit's lobby screen's title");
        }

        [Test]
        public void EveryRoomOffTheMainMenuGoesBackToIt_TheMapRoomBackToPlayers_AndTheMainMenuBackToTheTitle()
        {
            foreach (Room room in new[] { Room.Players, Room.Settings, Room.Controls, Room.HowToPlay, Room.Credits, Room.Endgame })
            {
                Assert.That(MenuFlow.Back(room), Is.EqualTo(Room.MainMenu), room.ToString());
            }

            Assert.That(MenuFlow.Back(Room.MapRoom), Is.EqualTo(Room.Players));
            Assert.That(MenuFlow.Back(Room.MainMenu), Is.EqualTo(Room.Title));
            Assert.That(MenuFlow.OffTheMainMenu, Does.Not.Contain(Room.Game).And.Not.Contain(Room.Title));
            foreach (Room room in Enum.GetValues(typeof(Room)))
            {
                Assert.That(MenuFlow.Title(room), Is.Not.Empty, room.ToString());
            }
        }

        [Test]
        public void TheSlidesWrapBothWays_AndCountFromOne()
        {
            int count = MenuFlow.Slides.Count;
            Assert.That(count, Is.GreaterThanOrEqualTo(5));
            Assert.That(MenuFlow.NextSlide(count - 1), Is.EqualTo(0));
            Assert.That(MenuFlow.PreviousSlide(0), Is.EqualTo(count - 1));
            Assert.That(MenuFlow.NextSlide(0), Is.EqualTo(1));
            Assert.That(MenuFlow.SlideCounter(0), Is.EqualTo("1/" + count));
            Assert.That(MenuFlow.SlideCounter(99), Is.EqualTo("1/" + count), "out of range shows the first");
            Assert.That(MenuFlow.Slides.All(s => s.Title.Length > 0 && s.Lines.Count > 0), Is.True);
        }

        [Test]
        public void TheSlidesNameEveryWeaponAndItem_AndTheControlsEveryKey()
        {
            string all = string.Join("\n", MenuFlow.Slides.Select(s => s.Text)).ToLowerInvariant();
            foreach (Weapon weapon in Enum.GetValues(typeof(Weapon)))
            {
                Assert.That(all, Does.Contain(ArsenalRules.Name(weapon).ToLowerInvariant()), weapon.ToString());
            }

            foreach (Item item in Enum.GetValues(typeof(Item)))
            {
                Assert.That(all, Does.Contain(ArsenalRules.Name(item).ToLowerInvariant()), item.ToString());
            }

            string keys = string.Join("\n", MenuFlow.Controls.Select(c => c.Keys));
            foreach (string key in new[] { "Left", "Up", "Space", "Q", "Z", "F", "1", "P", "Esc" })
            {
                Assert.That(keys, Does.Contain(key));
            }

            Assert.That(MenuFlow.Credits, Is.Not.Empty);
        }

        [Test]
        public void TheMapsAreNamed_WrapAndClamp_AndTheFirstIsThePracticeRange()
        {
            Assert.That(Maps.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(Maps.All.Select(m => m.Index), Is.EqualTo(Enumerable.Range(0, Maps.Count)));
            Assert.That(Maps.All.Select(m => m.Name).Distinct().Count(), Is.EqualTo(Maps.Count));
            Assert.That(Maps.Clamp(-1), Is.EqualTo(0));
            Assert.That(Maps.Clamp(Maps.Count), Is.EqualTo(0));
            Assert.That(Maps.Next(Maps.Count - 1), Is.EqualTo(0));
            Assert.That(Maps.Previous(0), Is.EqualTo(Maps.Count - 1));

            Level practice = Maps.At(0).Build();
            Level original = Level.Default();
            Assert.That(practice.Walls.Count, Is.EqualTo(original.Walls.Count));
            Assert.That(practice.Redirectors.Count, Is.EqualTo(original.Redirectors.Count));
        }

        [Test]
        public void EveryMapKeepsItsWallsAndRedirectorsClearOfEverySeat_TheDummies_AndTheLaidHazards()
        {
            var spots = new List<Vec2>();
            for (int i = 0; i < MatchRules.MaxSeats; i++)
            {
                spots.Add(Seating.SpawnPoint(i));
            }

            spots.AddRange(new[] { new Vec2(-7, 1), new Vec2(7, -3), new Vec2(0, 3.5) }); // Arena's dummy starts
            HazardRules hazards = HazardRules.Default;
            var laid = hazards.Arcs.Concat(hazards.Gates).ToList();

            foreach (MapInfo map in Maps.All.Where(m => !m.IsRandom))
            {
                Level level = map.Build();
                Assert.That((level.MinX, level.MinY, level.MaxX, level.MaxY), Is.EqualTo((-11.0, -6.0, 11.0, 6.0)), map.Name + " keeps the border");
                var segments = level.Walls.Select(w => (w.A, w.B)).Concat(level.Redirectors.Select(r => (r.A, r.B))).ToList();
                foreach ((Vec2 a, Vec2 b) in segments)
                {
                    Assert.That(level.Contains(a) && level.Contains(b), Is.True, map.Name + ": inside the border");
                    foreach (Vec2 spot in spots)
                    {
                        Assert.That(Distance(spot, a, b), Is.GreaterThan(Seating.PlayerRadius * 2), map.Name + ": clear of " + spot);
                    }

                    foreach (Vec2[] hazard in laid)
                    {
                        Assert.That(SegmentsDistance(a, b, hazard[0], hazard[1]), Is.GreaterThan(0.5), map.Name + ": clear of a laid hazard");
                    }
                }
            }
        }

        [Test]
        public void EverySeatCanReachSomeoneOnEveryMap()
        {
            foreach (MapInfo map in Maps.All.Where(m => !m.IsRandom))
            {
                Level level = map.Build();
                var targets = Enumerable.Range(0, 4).Select(i => new Target("seat:" + i, Seating.SpawnPoint(i), Seating.PlayerRadius)).ToList();
                for (int seat = 0; seat < 4; seat++)
                {
                    bool reaches = targets.Where(t => t.Id != "seat:" + seat)
                        .Any(t => BounceSolver.FindHeading(Seating.SpawnPoint(seat), 1, t.Id, level, targets) != null);
                    Assert.That(reaches, Is.True, map.Name + ": seat " + seat + " reaches nobody");
                }
            }
        }

        [Test]
        public void TheGoalStepsOneRound_500_To10000_AndWraps()
        {
            Assert.That(new MatchSetup().Goal, Is.EqualTo(500));
            Assert.That(MatchSetup.NextGoal(MatchSetup.OneRound), Is.EqualTo(500));
            Assert.That(MatchSetup.NextGoal(500), Is.EqualTo(1000));
            Assert.That(MatchSetup.NextGoal(10000), Is.EqualTo(MatchSetup.OneRound));
            Assert.That(MatchSetup.PreviousGoal(500), Is.EqualTo(MatchSetup.OneRound));
            Assert.That(MatchSetup.PreviousGoal(MatchSetup.OneRound), Is.EqualTo(10000));
            Assert.That(MatchSetup.ClampGoal(0), Is.EqualTo(MatchSetup.OneRound));
            Assert.That(MatchSetup.ClampGoal(740), Is.EqualTo(500));
            Assert.That(MatchSetup.ClampGoal(99999), Is.EqualTo(10000));
            Assert.That(MatchSetup.DescribeGoal(1), Is.EqualTo("One round"));
            Assert.That(MatchSetup.DescribeGoal(1500), Is.EqualTo("First to 1500 points"));
        }

        [Test]
        public void TheVoteCarriesTheGoalAndTheMap()
        {
            var setup = new MatchSetup { AiCount = 2, Goal = 2000, Map = 2 };
            MatchSetup back = MatchSetup.Parse(2, setup.Format());
            Assert.That((back.Goal, back.Map), Is.EqualTo((2000, 2)));
            Assert.That(MatchSetup.Parse(2, "skill=5").Goal, Is.EqualTo(500), "an old vote keeps the default goal");
            Assert.That(MatchSetup.Parse(2, "map=99").Map, Is.EqualTo(0));
            Assert.That(setup.DescribeGame(), Is.EqualTo(Maps.At(2).Name + ", first to 2000 points"));
        }

        [Test]
        public void ARoundEndingWithTheGoalReached_EndsTheGame_WithTheTopScoreItsChampion()
        {
            var rules = NoReadyCard.Rules(new MatchRules { WeaponDamage = 1000 }); // every hit a knock-out
            var match = new Match(rules, seed: 3) { Goal = 200 };
            match.Join(0, SeatKind.Human, "Ann");
            match.Join(1, SeatKind.Ai, "Bot");
            int ended = -2;
            match.GameEnded += seat => ended = seat;
            match.Start();

            // Round 1: Ann knocks the bot out for 100: under the goal, so round 2 follows.
            match.Fire(0);
            match.ShotLanded(0, 1, 0);
            Assert.That(match.Phase, Is.EqualTo(MatchPhase.RoundOver));
            match.Tick(rules.RoundOverSeconds + 0.1);
            Assert.That((match.Phase, match.Round), Is.EqualTo((MatchPhase.Aiming, 2)));

            // Round 2: Ann (the winner goes first) does it again: 200, the goal.
            Assert.That(match.Current, Is.EqualTo(0));
            match.Fire(0);
            match.ShotLanded(0, 1, 0);
            Assert.That(match.Phase, Is.EqualTo(MatchPhase.RoundOver), "the round's result is up first");
            Assert.That(ended, Is.EqualTo(-2));
            match.Tick(rules.RoundOverSeconds + 0.1);

            Assert.That(match.Phase, Is.EqualTo(MatchPhase.GameOver));
            Assert.That(match.Champion, Is.EqualTo(0));
            Assert.That(ended, Is.EqualTo(0));
            Assert.That(match.IsRunning, Is.True);
            Assert.That(match.MayFire(0) || match.MayFire(1), Is.False, "nobody fires once the game is over");
            Assert.That(match.Strike(1, 0, 0), Is.EqualTo(0));
            match.Tick(60);
            Assert.That(match.Phase, Is.EqualTo(MatchPhase.GameOver), "it stays over until a new match");
            Assert.That(match.Standings().Select(s => s.Index), Is.EqualTo(new[] { 0, 1 }));

            match.Start();
            Assert.That((match.Phase, match.Round, match.Champion), Is.EqualTo((MatchPhase.Aiming, 1, -1)));
            Assert.That(match.Seats.All(s => s.Score == 0), Is.True);
        }

        [Test]
        public void AOneRoundGameEndsAfterItsFirstRound_AndNoGoalNeverEnds()
        {
            var rules = NoReadyCard.Rules(new MatchRules { WeaponDamage = 1000 });
            var oneRound = new Match(rules, seed: 3) { Goal = MatchSetup.OneRound };
            var endless = new Match(rules, seed: 3) { Goal = 0 };
            foreach (Match match in new[] { oneRound, endless })
            {
                match.Join(0, SeatKind.Human, "Ann");
                match.Join(1, SeatKind.Human, "Bea");
                match.Start();
                match.Fire(0);
                match.ShotLanded(0, 0, 0); // Ann knocks herself out: nobody scores
                match.Tick(rules.RoundOverSeconds + 0.1);
            }

            Assert.That(oneRound.Phase, Is.EqualTo(MatchPhase.GameOver));
            Assert.That(oneRound.Champion, Is.EqualTo(0), "a tie at nothing goes to the lower seat");
            Assert.That((endless.Phase, endless.Round), Is.EqualTo((MatchPhase.Aiming, 2)));
        }

        [Test]
        public void TheRecapRanksByScore_TheLowerSeatFirstOnATie_WithPlacesAndAHeadline()
        {
            List<RecapEntry> ranked = Recap.Rank(new[]
            {
                new RecapEntry(0, "Ann", 300), new RecapEntry(1, "Bot", 740, isAi: true), new RecapEntry(2, "Cy", 300), new RecapEntry(3, "Di", -25),
            });

            Assert.That(ranked.Select(e => e.Name), Is.EqualTo(new[] { "Bot", "Ann", "Cy", "Di" }));
            Assert.That(ranked[3].Score, Is.EqualTo(0), "a negative score shows as 0");
            Assert.That(Recap.Headline(ranked), Is.EqualTo("Bot wins!"));
            Assert.That(Recap.Headline(Recap.Rank(null)), Is.EqualTo("Nobody wins."));
            Assert.That(Recap.Line(1, ranked[0]), Is.EqualTo("1st   [AI] Bot   740"));
            Assert.That(new[] { 1, 2, 3, 4, 11, 12, 13, 16, 21, 22, 23, 111 }.Select(Recap.Place),
                Is.EqualTo(new[] { "1st", "2nd", "3rd", "4th", "11th", "12th", "13th", "16th", "21st", "22nd", "23rd", "111th" }));
        }

        private static double Distance(Vec2 p, Vec2 a, Vec2 b)
        {
            Vec2 ab = b - a;
            double t = Math.Max(0, Math.Min(1, ((p.X - a.X) * ab.X + (p.Y - a.Y) * ab.Y) / (ab.X * ab.X + ab.Y * ab.Y)));
            return Vec2.Distance(p, new Vec2(a.X + ab.X * t, a.Y + ab.Y * t));
        }

        private static double SegmentsDistance(Vec2 a, Vec2 b, Vec2 c, Vec2 d)
        {
            static double Cross(Vec2 o, Vec2 p, Vec2 q) => (p.X - o.X) * (q.Y - o.Y) - (p.Y - o.Y) * (q.X - o.X);
            if (Cross(a, b, c) * Cross(a, b, d) < 0 && Cross(c, d, a) * Cross(c, d, b) < 0)
            {
                return 0;
            }

            return Math.Min(Math.Min(Distance(a, c, d), Distance(b, c, d)), Math.Min(Distance(c, a, b), Distance(d, a, b)));
        }
    }
}
