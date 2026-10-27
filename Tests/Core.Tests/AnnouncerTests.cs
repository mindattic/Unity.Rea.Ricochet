using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Ricochet.Core.Tests
{
    using Ricochet.Core.Ai;
    using Ricochet.Core.Arsenal;
    using Ricochet.Core.Match;
    // UR-T8: the announcer (random_announcement_welcome/win/lose, random_win_number) and the scores (hit.gml's pScore,
    // draw_potential.gml's point_combo).
    public class AnnouncerTests
    {
        [Test]
        public void SevenPersonalities_InTheSettingsScreensOrder_KurtSlyFirst()
        {
            Assert.That(Announcer.Voices.Select(v => v.Name), Is.EqualTo(new[]
            {
                "Kurt Sly", "Rye Wheatley", "Sid Steinburger", "Vlad", "Matt Baxter", "Doctor", "Haggis McKnight",
            }));
            Assert.That(Announcer.VoiceAt(Announcer.DefaultVoice).Key, Is.EqualTo("baxter"));
            Assert.That(Announcer.VoiceAt(0).Initials, Is.EqualTo("KS"));
            Assert.That(Announcer.VoiceAt(3).Initials, Is.EqualTo("V"));
            Assert.That(Announcer.VoiceAt(99).Name, Is.EqualTo("Haggis McKnight"), "clamped");
            Assert.That(Announcer.VoiceAt(-5).Name, Is.EqualTo("Kurt Sly"), "clamped");
        }

        [Test]
        public void TheAnnouncementsSetting_IsOffEveryTimeOrOneIn3_6_9()
        {
            Assert.That(Enumerable.Range(0, 5).Select(Announcer.Odds), Is.EqualTo(new[] { 0, 1, 3, 6, 9 }));
            Assert.That(Announcer.Odds(-1), Is.EqualTo(0));
            Assert.That(Announcer.Odds(12), Is.EqualTo(9));
            Assert.That(Announcer.FrequencyName(0), Is.EqualTo("Off"));
            Assert.That(Announcer.FrequencyName(1), Is.EqualTo("Every time"));
        }

        [Test]
        public void OffItNeverSpeaks_EveryTimeItAlwaysDoes()
        {
            var off = new Announcer(0, 0, new Random(1));
            Assert.That(off.Welcome(), Is.Null);
            Assert.That(off.Win(0, 1), Is.Null);
            Assert.That(off.Lose(0), Is.Null);

            var always = new Announcer(2, 1, new Random(1));
            Announcement? welcome = always.Welcome();
            Assert.That(welcome, Is.Not.Null);
            Assert.That(welcome.Value.Kind, Is.EqualTo(AnnouncementKind.Welcome));
            Assert.That((welcome.Value.Subject, welcome.Value.Other), Is.EqualTo((-1, -1)));

            Announcement? win = always.Win(3, 5);
            Assert.That(win.Value.Kind, Is.EqualTo(AnnouncementKind.Win));
            Assert.That((win.Value.Subject, win.Value.Other), Is.EqualTo((3, 5)));

            Announcement? lose = always.Lose(4);
            Assert.That(lose.Value.Kind, Is.EqualTo(AnnouncementKind.Lose));
            Assert.That(lose.Value.Subject, Is.EqualTo(4));
        }

        [Test]
        public void RarelyMeansAboutOneShotInNine_ButTheWelcomeAlwaysComes()
        {
            var rarely = new Announcer(0, 4, new Random(42));
            int spoke = Enumerable.Range(0, 9000).Count(_ => rarely.Lose(0) != null);
            Assert.That(spoke, Is.InRange(850, 1150), "about 1000 of 9000");
            Assert.That(Enumerable.Range(0, 20).All(_ => rarely.Welcome() != null), Is.True);
        }

        [Test]
        public void EveryLineOfAKindIsHeardOnce_BeforeAnyRepeats()
        {
            var announcer = new Announcer(0, 1, new Random(7));
            var first = new List<int>();
            var second = new List<int>();
            for (int i = 0; i < Announcer.LinesPerKind; i++)
            {
                first.Add(announcer.Win(0, 1).Value.Line);
            }

            for (int i = 0; i < Announcer.LinesPerKind; i++)
            {
                second.Add(announcer.Win(0, 1).Value.Line);
            }

            Assert.That(first, Is.EquivalentTo(Enumerable.Range(0, Announcer.LinesPerKind)));
            Assert.That(second, Is.EquivalentTo(Enumerable.Range(0, Announcer.LinesPerKind)));
            Assert.That(announcer.Lose(0).Value.Line, Is.InRange(0, Announcer.LinesPerKind - 1), "each kind has its own deck");
        }

        [Test]
        public void ALineNamesItsPlayers()
        {
            string[] names = { "Ann", "Bob" };
            Func<int, string> nameOf = seat => seat >= 0 && seat < names.Length ? names[seat] : null;
            for (int line = 0; line < Announcer.LinesPerKind; line++)
            {
                string win = Announcer.Text(new Announcement(AnnouncementKind.Win, line, 0, 1), nameOf);
                Assert.That(win, Does.Contain("Ann").And.Contain("Bob"), win);
                string lose = Announcer.Text(new Announcement(AnnouncementKind.Lose, line, 1, -1), nameOf);
                Assert.That(lose, Does.Contain("Bob"), lose);
                string welcome = Announcer.Text(new Announcement(AnnouncementKind.Welcome, line, -1, -1), nameOf);
                Assert.That(welcome, Is.Not.Empty.And.Not.Contain("{"));
            }

            Assert.That(Announcer.Text(new Announcement(AnnouncementKind.Lose, 0, 9, -1), nameOf), Does.Contain("someone"));
            Assert.That(Announcer.Text(new Announcement(AnnouncementKind.Lose, 99, 0, -1), null), Does.Contain("someone"), "line clamped, no names");
        }

        // U.RICO-T51: the drawn line picks the personality's recording, random_<key>_<kind>'s sfx_<key>_<kind><n>.
        [Test]
        public void TheDrawnLinePicksThePersonalitysRecording()
        {
            Assert.That(Announcer.Recording(Announcer.VoiceAt(0), new Announcement(AnnouncementKind.Win, 2, 0, 1)), Is.EqualTo("sfx_sly_win3"));
            Assert.That(Announcer.Recording(Announcer.VoiceAt(6), new Announcement(AnnouncementKind.Welcome, 9, -1, -1)), Is.EqualTo("sfx_scottish_welcome10"));
            Assert.That(Announcer.Recording(Announcer.VoiceAt(2), new Announcement(AnnouncementKind.Lose, 0, 0, -1)), Is.EqualTo("sfx_sid_lose1"));
            Assert.That(Announcer.Recording(Announcer.VoiceAt(3), new Announcement(AnnouncementKind.Lose, 99, 0, -1)), Is.EqualTo("sfx_vlad_lose10"), "line clamped");
            Assert.That(Announcer.ResourcePath(Announcer.VoiceAt(4), new Announcement(AnnouncementKind.Win, 0, 0, 1)), Is.EqualTo("Ricochet/Voice/sfx_baxter_win1"));

            // Every personality has all thirty, one per kind and line.
            var all = new HashSet<string>();
            foreach (AnnouncerVoice voice in Announcer.Voices)
            {
                foreach (AnnouncementKind kind in Enum.GetValues(typeof(AnnouncementKind)))
                {
                    for (int line = 0; line < Announcer.LinesPerKind; line++)
                    {
                        all.Add(Announcer.Recording(voice, new Announcement(kind, line, 0, 1)));
                    }
                }
            }

            Assert.That(all.Count, Is.EqualTo(210));
        }

        [Test]
        public void TheCardHoldsWhileTheLineIsHeard_OrForTheCaptionWhenMuted()
        {
            Assert.That(Announcer.CardSeconds(2.7), Is.EqualTo(2.7), "obj_announcer: on screen while audio_is_playing");
            Assert.That(Announcer.CardSeconds(0), Is.EqualTo(Announcer.CaptionSeconds), "nobody hears it: the written line stands alone");
            Assert.That(Announcer.CardSeconds(double.NaN), Is.EqualTo(Announcer.CaptionSeconds));
        }

        [Test]
        public void EachPersonalityHasTheOriginalsPortrait_FourSharingTheDummy()
        {
            Assert.That(Announcer.Voices.Select(v => v.Portrait), Is.EqualTo(new[]
            {
                "spr_profile_sly", "spr_profile_rye", "spr_profile_sid",
                "spr_profile_dummy", "spr_profile_dummy", "spr_profile_dummy", "spr_profile_dummy",
            }));
        }

        [Test]
        public void TheSetupCarriesTheAnnouncer_ThroughTheVotesText()
        {
            var setup = new MatchSetup { AiCount = 2, AnnouncerVoice = 4, Announcements = 3 };
            MatchSetup back = MatchSetup.Parse(2, setup.Format());
            Assert.That((back.AnnouncerVoice, back.Announcements), Is.EqualTo((4, 3)));
            Assert.That(back.DescribeAnnouncer(), Is.EqualTo("Matt Baxter on the air, sometimes"));

            MatchSetup defaults = MatchSetup.Parse(1, "skill=5;assist=3");
            Assert.That((defaults.AnnouncerVoice, defaults.Announcements), Is.EqualTo((Announcer.DefaultVoice, Announcer.DefaultFrequency)));
            Assert.That(MatchSetup.Parse(0, "voice=40;announce=-2").DescribeAnnouncer(), Is.EqualTo("No announcer"));
            Assert.That(MatchSetup.Parse(0, "voice=40;announce=9").AnnouncerVoice, Is.EqualTo(Announcer.Voices.Count - 1));
        }

        [Test]
        public void AHitScores100_Plus25ABounce_Plus5ATenthOfASecondInTheAir()
        {
            var rules = new MatchRules();
            Assert.That(rules.Points(0, 0), Is.EqualTo(100));
            Assert.That(rules.Points(3, 0), Is.EqualTo(175));
            Assert.That(rules.Points(0, 1.0), Is.EqualTo(100 + 5 * 10), "100 steps at 1020 fps is a tick every 0.098 s");
            Assert.That(rules.Points(-2, double.NaN), Is.EqualTo(100));
        }

        [Test]
        public void TheShooterScoresForHittingAnOpponent_NotForHittingThemselves_AndScoresLastTheMatch()
        {
            var match = new Match(seed: 3);
            match.Join(0, SeatKind.Human, "Ann");
            match.Join(1, SeatKind.Ai, "Bob");
            var scored = new List<(int, int)>();
            match.Scored += (seat, points) => scored.Add((seat, points));
            match.Start();

            match.Fire(0);
            match.Strike(0, 1, 2, 1, 0.5);
            Assert.That(match.Seat(0).Score, Is.EqualTo(100 + 50 + 25));
            Assert.That(scored, Is.EqualTo(new[] { (0, 175) }));

            match.Strike(0, 0, 1);
            Assert.That(match.Seat(0).Score, Is.EqualTo(175), "hurting yourself scores nothing");

            match.Strike(0, 1, 0, 0.2);
            Assert.That(match.Seat(0).Score, Is.EqualTo(175 + 20), "a pellet scores its share");

            // Knock Bob out: the round is over, and the score carries into the next.
            while (match.Seat(1).Alive)
            {
                match.Strike(0, 1, 0);
            }

            match.ShotOver(0);
            int afterRound = match.Seat(0).Score;
            Assert.That(match.Phase, Is.EqualTo(MatchPhase.RoundOver));
            match.Tick(match.Rules.RoundOverSeconds + 0.1);
            Assert.That(match.Round, Is.EqualTo(2));
            Assert.That(match.Seat(0).Score, Is.EqualTo(afterRound));

            match.Start();
            Assert.That(match.Seats.All(s => s.Score == 0), Is.True, "a new match starts at nothing");
        }
    }
}
