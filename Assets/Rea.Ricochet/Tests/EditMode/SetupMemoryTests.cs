// UR-T41: the remembered setup's place in a Unity.Shared profile: the game block's "matchSetup" object, written and read
// back through the profile's own JSON (JsonObject), beside whatever else the game block holds. No real profile is touched.
using NUnit.Framework;
using Ricochet.Core.Match;
using Unity.Shared.Core.Json;

namespace Ricochet.EditorTests
{
    public class SetupMemoryTests
    {
        [Test]
        public void TheSetupGoesIntoTheGameBlockAndComesBackThroughItsJson()
        {
            var game = new JsonObject();
            game.Set("inputBindingOverrides", "{\"bindings\":[]}");
            var chosen = new MatchSetup { AiCount = 5, AiSkill = 9, AimAssists = 0, Announcements = 2, Goal = 2000, Map = 1 };

            SetupMemory.Write(game, chosen);
            JsonObject reread = JsonObject.Parse(game.ToJson());

            Assert.That(reread.GetObject(SetupMemory.ProfileKey), Is.Not.Null);
            Assert.That(reread.GetString("inputBindingOverrides"), Is.EqualTo("{\"bindings\":[]}"), "the rebinds stay beside it");
            Assert.That(SetupMemory.Read(reread).SameAs(chosen), Is.True, reread.ToJson());
        }

        [Test]
        public void EachAisSkillAndName_AreKeptInTheGameBlock_AndAnOldSingleSkillStillReads()
        {
            var game = new JsonObject();
            var chosen = new MatchSetup { AiCount = 3 };
            chosen.SetSkill(1, 9);
            chosen.SetNameIndex(2, 4);
            SetupMemory.Write(game, chosen);
            MatchSetup back = SetupMemory.Read(JsonObject.Parse(game.ToJson()));
            Assert.That(back.SameAs(chosen), Is.True);
            Assert.That((back.SkillOf(1), back.NameIndexOf(2)), Is.EqualTo((9, 4)));

            MatchSetup old = SetupMemory.Read(JsonObject.Parse("{\"matchSetup\":{\"ai\":2,\"skill\":7}}"));
            Assert.That((old.SkillOf(0), old.SkillOf(1), old.SkillOf(14)), Is.EqualTo((7, 7, 7)));
        }

        [Test]
        public void AProfileWithNoSetup_OrAJunkOne_GivesTheDefaults()
        {
            Assert.That(SetupMemory.Read(new JsonObject()).SameAs(new MatchSetup()), Is.True);
            Assert.That(SetupMemory.Read(null).SameAs(new MatchSetup()), Is.True);

            JsonObject junk = JsonObject.Parse("{\"matchSetup\":{\"ai\":\"lots\",\"goal\":1499.6,\"colour\":3}}");
            MatchSetup read = SetupMemory.Read(junk);
            Assert.That(read.AiCount, Is.EqualTo(new MatchSetup().AiCount), "a string is not a count");
            Assert.That(read.Goal, Is.EqualTo(1500));
        }
    }
}
