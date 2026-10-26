namespace Ricochet.Core.Tests
{
    using Ricochet.Core.Match;

    // UR-T42: every turn opens with obj_ready's Ready card, a hold before the clock runs (MatchRules.ReadySeconds). Tests
    // about the turn loop past it (firing, driving, damage, the clock, drops) play on rules without the hold, so each
    // turn is aimable at once; MatchTests' Ready card tests and CommandTests' hold test play the stock hold.
    internal static class NoReadyCard
    {
        /// <summary><paramref name="rules"/> (stock rules when null) with the Ready card's hold taken out.</summary>
        public static MatchRules Rules(MatchRules rules = null)
        {
            rules = rules ?? new MatchRules();
            rules.ReadyHoldSeconds = 0;
            rules.ReadyFadeSeconds = 0;
            return rules;
        }
    }
}
