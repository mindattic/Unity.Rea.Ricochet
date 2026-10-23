// What an AI seat does on its turn until the real AI lands (UR-T7: skill levels, aim assist, bank-shot preference):
// try the opponents nearest first, take the first aim whose solved path ends on one (BounceSolver.FindHeading, the
// original's find_target_angle), and when no aim reaches anyone, fire somewhere rather than sit out the clock.
using System;
using System.Collections.Generic;

namespace Ricochet.Core
{
    public static class SimpleAi
    {
        public const double Power = 1;

        /// <summary>
        /// The shot an AI at <paramref name="origin"/> takes: a heading and power, and the target it expects to hit (null
        /// when none could be reached). <paramref name="targets"/> may include the shooter's own id
        /// (<paramref name="selfId"/>), which it never aims at.
        /// </summary>
        public static double ChooseHeading(Vec2 origin, string selfId, Level level, IReadOnlyList<Target> targets, ShotRules rules,
            Random random, out string expected)
        {
            expected = null;
            var opponents = new List<Target>();
            if (targets != null)
            {
                foreach (Target target in targets)
                {
                    if (target.Id != selfId)
                    {
                        opponents.Add(target);
                    }
                }
            }

            opponents.Sort((a, b) => Vec2.Distance(origin, a.Center).CompareTo(Vec2.Distance(origin, b.Center)));
            foreach (Target opponent in opponents)
            {
                double? heading = BounceSolver.FindHeading(origin, Power, opponent.Id, level, targets, rules, 1.0);
                if (heading != null)
                {
                    expected = opponent.Id;
                    return heading.Value;
                }
            }

            return (random ?? new Random()).NextDouble() * 360;
        }
    }
}
