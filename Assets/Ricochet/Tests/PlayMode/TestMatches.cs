// A fixed arena for tests that need to know where the seats stand (UR-T64): the default map is the random arena, which
// seats players at random spots on a fresh level every round (UR-T23), so a test that aims at a spot asks for the
// practice range, whose seats are always at Seating.SpawnPoint.
// UR-T42: every turn opens with the Ready card's hold before anyone may act; tests about what comes after it (firing,
// the clock, the AI) take the hold out in their SetUp and put the stock one back in their TearDown. ReadyCardTests plays it.
using Ricochet.Core.Level;
using Ricochet.Core.Match;

namespace Ricochet.Tests
{
    public static class TestMatches
    {
        /// <summary>A match on the hand-laid Practice Range with <paramref name="aiCount"/> AI seats.</summary>
        public static MatchSetup OnThePracticeRange(int aiCount = 0) => new MatchSetup { AiCount = aiCount, Map = 0 };

        /// <summary>Takes the Ready card's hold out of the server's rules: each turn is aimable as it starts.</summary>
        public static void WithoutReadyCard()
        {
            Arena.MatchRules.ReadyHoldSeconds = 0;
            Arena.MatchRules.ReadyFadeSeconds = 0;
        }

        /// <summary>Puts the stock Ready card hold back.</summary>
        public static void StockReadyCard()
        {
            MatchRules stock = MatchRules.Default;
            Arena.MatchRules.ReadyHoldSeconds = stock.ReadyHoldSeconds;
            Arena.MatchRules.ReadyFadeSeconds = stock.ReadyFadeSeconds;
        }
    }
}
