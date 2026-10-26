// U.RICO-T76: the match rules the host sets (turn time, bounces, damage, arsenal, drops, pause_redirector) reach a joined client
// when the match starts, so its bounce preview and rules agree with the host's. The rules are shared statics (Arena.Rules and
// the like) so in one process the host's own apply is undone straight after StartMatch: only the client's adoption can bring them back.
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Ricochet.Core.Arsenal;
using Ricochet.Core.Match;
using Unity.Multiplayer;
using Unity.Multiplayer.Core;
using Unity.Multiplayer.Testing;
using UnityEngine.TestTools;

namespace Ricochet.Tests
{
    public class MatchRulesSyncTests : NetSessionTest
    {
        public MatchRulesSyncTests() : base(Topology.Host, clients: 1) { }

        protected override void OnSessionsCreated()
        {
            SetPlayerPrefab(GamePrefabs.Player);
            RegisterPrefab(GamePrefabs.Dummy);
            RegisterPrefab(GamePrefabs.Opponent);
            RegisterPrefab(GamePrefabs.Turns);
            RegisterPrefab(GamePrefabs.Crate);
        }

        [TearDown]
        public void PutTheRulesBack()
        {
            var stock = new MatchSetup();
            stock.ApplyTo(Arena.ArsenalRules);
            stock.ApplyTo(Arena.MatchRules, Arena.Rules, Arena.DropRules, Arena.HazardRules);
        }

        [UnityTest]
        public IEnumerator AClientTakesTheHostsMatchRulesAtMatchStart()
        {
            yield return StartAndJoinAll();
            Arena arena = Arena.Serve(Server, GamePrefabs.Dummy);
            yield return WaitUntil(() => Player.In(Client).Count() == 2, "the client never saw both players");

            var rules = new MatchSetup { AiCount = 0, TurnSeconds = 35, MaxBounces = 9, WeaponDamage = 40, PauseRedirector = false };
            rules.SetWeaponCount(Weapon.Cannon, ArsenalRules.Unlimited);
            rules.SetWeaponOn(Weapon.Grenade, false);
            Assert.That(arena.StartMatch(rules), Is.True);

            new MatchSetup().ApplyTo(Arena.ArsenalRules);          // undo the host's own apply: what shows next came over the wire
            new MatchSetup().ApplyTo(Arena.MatchRules, Arena.Rules, Arena.DropRules, Arena.HazardRules);
            Assert.That(Arena.Rules.MaxBounces, Is.EqualTo(MatchSetup.DefaultMaxBounces));

            yield return WaitUntil(() => Arena.Rules.MaxBounces == 9, "the client never took the host's bounces");
            Assert.That(Arena.MatchRules.TurnSeconds, Is.EqualTo(35).Within(0.001));
            Assert.That(Arena.MatchRules.WeaponDamage, Is.EqualTo(40));
            Assert.That(Arena.HazardRules.RedirectorsSpin, Is.True, "pause_redirector off makes redirectors turn");
            Assert.That(Arena.ArsenalRules.InPlay(Weapon.Grenade), Is.False, "the host took the grenade out of play");
        }
    }
}
