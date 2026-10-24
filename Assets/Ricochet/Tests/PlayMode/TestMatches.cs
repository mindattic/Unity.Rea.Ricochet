// A fixed arena for tests that need to know where the seats stand (UR-T64): the default map is the random arena, which
// seats players at random spots on a fresh level every round (UR-T23), so a test that aims at a spot asks for the
// practice range, whose seats are always at Seating.SpawnPoint.
using Ricochet.Core.Level;
using Ricochet.Core.Match;

namespace Ricochet.Tests
{
    public static class TestMatches
    {
        /// <summary>A match on the hand-laid Practice Range with <paramref name="aiCount"/> AI seats.</summary>
        public static MatchSetup OnThePracticeRange(int aiCount = 0) => new MatchSetup { AiCount = aiCount, Map = 0 };
    }
}
