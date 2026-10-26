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
            Assert.That(keys, Is.EqualTo(new[]
            {
                MatchSetup.Keys.Ai, MatchSetup.Keys.Skill, MatchSetup.Keys.Assist, MatchSetup.Keys.Voice,
                MatchSetup.Keys.Announce, MatchSetup.Keys.Goal, MatchSetup.Keys.Map,
            }));
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
        public void TheVoteTextNeverCarriesTheAiCount()
        {
            Assert.That(MatchSetup.Parse(2, "ai=9;skill=4").AiCount, Is.EqualTo(2));
        }
    }
}
