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
            if (rules.WeaponDamage == MatchSetup.DefaultWeaponDamage)
            {
                rules.WeaponDamage = 10; // the stock hit is a knock-out (100); these fixtures play multi-hit fights, so they keep a 10-point cannon unless a test sets its own
            }

            rules.ReadyHoldSeconds = 0;
            rules.ReadyFadeSeconds = 0;
            return rules;
        }
    }
}
