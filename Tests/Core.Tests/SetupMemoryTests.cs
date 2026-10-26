using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace Ricochet.Core.Tests
{
    using Ricochet.Core.Ai;
    using Ricochet.Core.Data;
    using Ricochet.Core.Level;
    using Ricochet.Core.Match;
    using Ricochet.Core.Menu;
    // UR-T41: the setup is remembered from session to session, as the original's config.ini was (write_config on every
    // change, read_config on start, the Default button restoring backup.ini). The profile keeps MatchSetup's record as
    // named whole numbers in the game block; these tests take it out and back, through JSON text, and reset it.
    public class SetupMemoryTests
    {
        private static MatchSetup Chosen() => new MatchSetup
        {
            AiCount = 7,
            AiSkill = 2,
            AimAssists = 10,
            AnnouncerVoice = Announcer.Voices.Count - 1,
            Announcements = 4,
            Goal = 3500,
            Map = Maps.Count - 1,
        };

        [Test]
        public void ASetupGoesIntoTheProfileAndComesBackTheSame()
        {
            MatchSetup chosen = Chosen();
            MatchSetup back = MatchSetup.FromRecord(chosen.ToRecord());
            Assert.That(back.SameAs(chosen), Is.True);
            Assert.That((back.AiCount, back.AiSkill, back.AimAssists, back.AnnouncerVoice, back.Announcements, back.Goal, back.Map),
                Is.EqualTo((7, 2, 10, Announcer.Voices.Count - 1, 4, 3500, Maps.Count - 1)));
        }

        [Test]
        public void ASetupSurvivesBeingWrittenAsJsonAndReadBack()
        {
            MatchSetup chosen = Chosen();
            var json = new StringBuilder("{");
            foreach (KeyValuePair<string, int> entry in chosen.ToRecord())
            {
                json.Append(json.Length > 1 ? "," : string.Empty).Append('"').Append(entry.Key).Append("\":")
                    .Append(entry.Value.ToString(CultureInfo.InvariantCulture));
            }

            JsonValue parsed = Json.Parse(json.Append('}').ToString());
            MatchSetup back = MatchSetup.FromRecord(parsed.Members.Select(m => new KeyValuePair<string, int>(m.Key, (int)m.Value.Number)));
            Assert.That(back.SameAs(chosen), Is.True, json.ToString());
        }

        [Test]
        public void TheRecordNamesEveryChoiceOnce()
        {
            string[] keys = new MatchSetup().ToRecord().Select(e => e.Key).ToArray();
            Assert.That(keys.Take(7), Is.EqualTo(new[]
            {
                MatchSetup.Keys.Ai, MatchSetup.Keys.Skill, MatchSetup.Keys.Assist, MatchSetup.Keys.Voice,
                MatchSetup.Keys.Announce, MatchSetup.Keys.Goal, MatchSetup.Keys.Map,
            }));
            Assert.That(keys.Skip(7), Is.EqualTo(Enumerable.Range(1, MatchRules.MaxSeats - 1).SelectMany(n => new[] { "skill" + n, "name" + n })));
            Assert.That(keys, Is.Unique);
        }

        [Test]
        public void NothingSaved_OrJunk_MeansTheDefaults_AndBadValuesAreClamped()
        {
            var defaults = new MatchSetup();
            Assert.That(MatchSetup.FromRecord(null).SameAs(defaults), Is.True);
            Assert.That(MatchSetup.FromRecord(new Dictionary<string, int>()).SameAs(defaults), Is.True);
            Assert.That(MatchSetup.FromRecord(new Dictionary<string, int> { ["colour"] = 3 }).SameAs(defaults), Is.True);

            MatchSetup clamped = MatchSetup.FromRecord(new Dictionary<string, int>
            {
                [MatchSetup.Keys.Ai] = 99,
                [MatchSetup.Keys.Skill] = -5,
                [MatchSetup.Keys.Goal] = 123456,
                [MatchSetup.Keys.Map] = 999,
            });
            Assert.That(clamped.AiCount, Is.EqualTo(MatchRules.MaxSeats - 1));
            Assert.That(clamped.AiSkill, Is.EqualTo(Ai.MinSkill));
            Assert.That(clamped.Goal, Is.EqualTo(MatchSetup.MaxGoal));
            Assert.That(clamped.Map, Is.EqualTo(Maps.Clamp(999)));
            Assert.That(clamped.AimAssists, Is.EqualTo(MatchSetup.DefaultAimAssists), "what was not saved keeps its default");
        }

        [Test]
        public void TheDefaultButtonPutsEveryChoiceBack_InPlace()
        {
            MatchSetup setup = Chosen();
            MatchSetup held = setup; // the screens hold this one instance
            setup.ResetToDefaults();
            Assert.That(held.SameAs(new MatchSetup()), Is.True);

            setup.CopyFrom(Chosen());
            Assert.That(held.SameAs(Chosen()), Is.True);
        }

        [Test]
        public void EachAiKeepsItsOwnSkillAndName_ThroughTheRecordAndTheVoteText()
        {
            MatchSetup chosen = Chosen();
            chosen.SetSkill(0, 1);
            chosen.SetSkill(3, 9);
            chosen.SetNameIndex(0, 5);
            chosen.SetNameIndex(6, AiNames.Count - 1);
            Assert.That(MatchSetup.FromRecord(chosen.ToRecord()).SameAs(chosen), Is.True);
            Assert.That(chosen.NameOf(0), Is.EqualTo(AiNames.All[5]));
            Assert.That(chosen.NameOf(1), Is.EqualTo("AI 2"));

            MatchSetup voted = MatchSetup.Parse(chosen.AiCount, chosen.Format());
            Assert.That(Enumerable.Range(0, 7).Select(voted.SkillOf), Is.EqualTo(Enumerable.Range(0, 7).Select(chosen.SkillOf)));
            Assert.That(Enumerable.Range(0, 7).Select(voted.NameIndexOf), Is.EqualTo(Enumerable.Range(0, 7).Select(chosen.NameIndexOf)));
            Assert.That(chosen.Describe(), Does.StartWith("AI skills 1 to 9 of 10"));
        }

        [Test]
        public void AnOlderSaveWithOneSkill_SetsEveryAi_AndAPerAiSkillBesideItWins()
        {
            MatchSetup old = MatchSetup.FromRecord(new[] { new KeyValuePair<string, int>("skill", 8), new KeyValuePair<string, int>("ai", 3) });
            Assert.That(Enumerable.Range(0, MatchRules.MaxSeats - 1).Select(old.SkillOf), Is.All.EqualTo(8));

            MatchSetup mixed = MatchSetup.FromRecord(new[] { new KeyValuePair<string, int>("skill2", 2), new KeyValuePair<string, int>("skill", 8) });
            Assert.That((mixed.SkillOf(0), mixed.SkillOf(1), mixed.SkillOf(2)), Is.EqualTo((8, 2, 8)));
        }

        [Test]
        public void DefaultNamesAreAiN_RandomNamesAreDistinct_AndTheResetClearsBoth()
        {
            var setup = new MatchSetup();
            Assert.That(setup.NameOf(2), Is.EqualTo("AI 3"));
            setup.UseRandomNames(new System.Random(7));
            int[] picked = Enumerable.Range(0, MatchRules.MaxSeats - 1).Select(setup.NameIndexOf).ToArray();
            Assert.That(picked, Is.Unique);
            Assert.That(picked, Is.All.GreaterThanOrEqualTo(0));
            setup.UseDefaultNames();
            Assert.That(setup.NameOf(0), Is.EqualTo("AI 1"));

            setup.SetSkill(4, 0);
            setup.UseRandomNames(new System.Random(1));
            setup.ResetToDefaults();
            Assert.That(setup.SameAs(new MatchSetup()), Is.True);
            Assert.That(setup.SkillOf(4), Is.EqualTo(Ai.DefaultSkill));
        }

        [Test]
        public void ACloneDoesNotShareItsSeatsWithTheOriginal()
        {
            var a = new MatchSetup();
            MatchSetup b = a.Clone();
            b.SetSkill(0, 1);
            b.SetNameIndex(0, 3);
            Assert.That(a.SkillOf(0), Is.EqualTo(Ai.DefaultSkill));
            Assert.That(a.NameIndexOf(0), Is.EqualTo(AiNames.Default));
        }

        [Test]
        public void TheVoteTextNeverCarriesTheAiCount()
        {
            Assert.That(MatchSetup.Parse(2, "ai=9;skill=4").AiCount, Is.EqualTo(2));
        }
    }
}
