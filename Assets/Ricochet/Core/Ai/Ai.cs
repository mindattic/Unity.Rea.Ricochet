// AI opponents (UR-T7). The 2013 original (design reference only, UR-LAW-2) ran an AI seat's turn from obj_CheckAI and
// check_ai.gml: it put up one of thirty "is thinking..." lines (obj_ai_gui pulsed it over the blacked-out controls), swept
// every whole degree for a straight shot that reached a living opponent before the first wall (find_target_angle.gml),
// then, failing that, fired an invisible tracer at every degree to find a bank shot (shoot_tracer and
// find_target_angle_from_bounce.gml), and failing that picked a random angle; it nudged the angle it found by up to
// 10 - skill degrees either way (pSkill, 0 to 10 per player), turned the barrel towards it a step at a time
// (check_rotate_clockwise, rotate_left/right) and fired a bullet once it was pointing there.
// Here the sweep is the same exact solver the preview and the server's shot use (BounceSolver), at half-degree steps,
// and every pass comes from one sweep: a straight shot first, then a bank shot, then (new) a railgun shot through the
// walls when the AI carries one, then a random angle. Among the shots that reach someone it takes the nearest opponent
// and the middle of the widest run of headings that reach them, so a skilled AI's small nudge still lands (the
// original took the first degree that touched, often a graze). Aim assist (the item) is the same targeting at full
// skill, offered to a human: it turns their aim onto the shot the AI would take.
using System;
using System.Collections.Generic;

namespace Ricochet.Core.Ai
{
    using Ricochet.Core.Aim;
    using Ricochet.Core.Arsenal;
    using Ricochet.Core.Level;
    /// <summary>How an AI's planned shot reaches its target.</summary>
    public enum AiShotKind
    {
        /// <summary>A straight line to an opponent, no bounce (find_target_angle).</summary>
        Direct,

        /// <summary>Off the border, walls or redirectors onto an opponent (find_target_angle_from_bounce).</summary>
        Bank,

        /// <summary>A railgun slug straight through the walls.</summary>
        Railgun,

        /// <summary>No aim reaches anyone: a random heading, so the turn is not wasted on the clock.</summary>
        Blind,
    }

    /// <summary>The shot an AI (or an aim assist) decided on.</summary>
    public sealed class AiPlan
    {
        public AiPlan(AiShotKind kind, Weapon weapon, double aim, double heading, string targetId)
        {
            Kind = kind;
            Weapon = weapon;
            Aim = Angles.Normalize(aim);
            Heading = Angles.Normalize(heading);
            TargetId = targetId;
        }

        public AiShotKind Kind { get; }

        /// <summary>The weapon to fire: the cannon, or the railgun for a <see cref="AiShotKind.Railgun"/> shot.</summary>
        public Weapon Weapon { get; }

        /// <summary>The heading that reaches the target, before the AI's skill nudges it.</summary>
        public double Aim { get; }

        /// <summary>The heading it fires at: <see cref="Aim"/> nudged by up to 10 - skill degrees.</summary>
        public double Heading { get; }

        /// <summary>The opponent it means to hit, or null for a blind shot.</summary>
        public string TargetId { get; }

        public bool Found => TargetId != null;

        public override string ToString() => Kind + " " + Weapon + " at " + Heading.ToString("0.#") + " (aim " + Aim.ToString("0.#") + ") -> " + (TargetId ?? "nobody");
    }

    public static class Ai
    {
        /// <summary>The least skilled an AI can be: its aim is nudged up to 10 degrees either way.</summary>
        public const int MinSkill = 0;

        /// <summary>The most skilled: it fires exactly where it aimed.</summary>
        public const int MaxSkill = 10;

        /// <summary>The skill a match's AI seats play at unless set (the original's players defaulted to 0-2).</summary>
        public const int DefaultSkill = 5;

        /// <summary>Degrees between the headings the sweep tries.</summary>
        public const double Step = 0.5;

        // check_ai.gml's thirty lines, the typos put right.
        private static readonly string[] s_thinking =
        {
            " is calculating the best course of action...",
            " is making up their mind...",
            " is thinking...",
            " is considering the possibilities...",
            " is weighing their options...",
            " is deep in thought...",
            " is doing the math...",
            " is strategizing...",
            " is working it out...",
            " is taking their time...",
            " is thinking it over...",
            " is doing what's best for them...",
            " is plotting and scheming...",
            " has a plan...",
            " knows what to do...",
            " has no clue what to do next...",
            " has done their homework...",
            " knows there is a solution...",
            " is formulating a plan...",
            " wonders what to do next...",
            " needs encouragement...",
            " can't quite decide...",
            " must trust their instincts...",
            " has a gut feeling...",
            " is eyeing it up...",
            " is working on the problem...",
            " needs this to go their way...",
            " is willing to take a risk...",
            " will go when they're good and ready...",
            " is ready to make a move...",
        };

        /// <summary>How many "is thinking..." lines there are.</summary>
        public static int ThinkingLineCount => s_thinking.Length;

        /// <summary>The <paramref name="index"/>th line (wrapping), to follow the AI's name.</summary>
        public static string ThinkingLine(int index)
        {
            int n = s_thinking.Length;
            return s_thinking[((index % n) + n) % n];
        }

        public static int ClampSkill(int skill) => Math.Max(MinSkill, Math.Min(MaxSkill, skill));

        /// <summary>The most an AI of <paramref name="skill"/> nudges its aim either way, in degrees (10 - skill).</summary>
        public static int Spread(int skill) => MaxSkill - ClampSkill(skill);

        /// <summary>check_ai.gml's nudge: a whole number of degrees from -spread to +spread.</summary>
        public static double Jitter(int skill, Random dice)
        {
            int spread = Spread(skill);
            return spread == 0 ? 0 : (dice ?? new Random()).Next(-spread, spread + 1);
        }

        /// <summary>
        /// An AI's whole decision for one turn: the shot that reaches an opponent (<see cref="Aim"/>, with
        /// the railgun considered when <paramref name="loadout"/> carries one), nudged by <paramref name="skill"/>; or a
        /// random heading when no shot reaches anyone. <paramref name="targets"/> may include the AI itself
        /// (<paramref name="selfId"/>), which it never aims at.
        /// </summary>
        public static AiPlan Plan(Vec2 origin, string selfId, Level level, IReadOnlyList<Target> targets, int skill, Random dice,
            ShotRules rules = null, ArsenalRules arsenal = null, Loadout loadout = null)
        {
            dice = dice ?? new Random();
            bool railgun = loadout != null && loadout.Has(Weapon.Railgun);
            AiPlan aimed = Aim(origin, selfId, level, targets, rules, arsenal, railgun);
            if (aimed == null)
            {
                double random = dice.NextDouble() * 360;
                return new AiPlan(AiShotKind.Blind, Weapon.Cannon, random, random, null);
            }

            return new AiPlan(aimed.Kind, aimed.Weapon, aimed.Aim, aimed.Aim + Jitter(skill, dice), aimed.TargetId);
        }

        /// <summary>
        /// The aim-assist item: the shot an AI at full skill would take with the cannon. Null when no aim reaches anyone.
        /// </summary>
        public static AiPlan Assist(Vec2 origin, string selfId, Level level, IReadOnlyList<Target> targets, ShotRules rules = null)
        {
            return Aim(origin, selfId, level, targets, rules, null, false);
        }

        /// <summary>
        /// The targeting sweep (find_target_angle and find_target_angle_from_bounce in one): every heading at
        /// <see cref="Step"/>-degree steps is solved once; a straight shot onto an opponent beats a bank shot, which beats
        /// a railgun shot (tried only when <paramref name="railgun"/>); among those, the nearest opponent, then the widest
        /// run of headings that reach them, aimed at its middle. Null when no heading reaches anyone.
        /// </summary>
        public static AiPlan Aim(Vec2 origin, string selfId, Level level, IReadOnlyList<Target> targets, ShotRules rules = null,
            ArsenalRules arsenal = null, bool railgun = false)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            rules = rules ?? ShotRules.Default;
            int steps = (int)Math.Round(360 / Step);
            var hits = new Hit[steps];
            bool any = false;
            for (int i = 0; i < steps; i++)
            {
                ShotPath path = BounceSolver.Solve(origin, i * Step, level, targets, rules);
                if (path.End == ShotEndKind.Target && path.TargetId != null && path.TargetId != selfId)
                {
                    hits[i] = new Hit(path.TargetId, path.Contacts.Count == 0 ? AiShotKind.Direct : AiShotKind.Bank);
                    any = true;
                }
            }

            AiPlan best = any ? Best(hits, origin, targets, Weapon.Cannon) : null;
            if (best != null || !railgun)
            {
                return best;
            }

            for (int i = 0; i < steps; i++)
            {
                Volley volley = Arsenal.Fire(Weapon.Railgun, origin, i * Step, 0, 0, level, targets, rules, arsenal);
                string nearest = null;
                double nearestDistance = double.MaxValue;
                foreach (Strike strike in volley.Strikes)
                {
                    double distance = Distance(origin, strike.TargetId, targets);
                    if (strike.TargetId != null && strike.TargetId != selfId && distance < nearestDistance)
                    {
                        nearest = strike.TargetId;
                        nearestDistance = distance;
                    }
                }

                hits[i] = nearest != null ? new Hit(nearest, AiShotKind.Railgun) : default;
                any |= nearest != null;
            }

            return any ? Best(hits, origin, targets, Weapon.Railgun) : null;
        }

        /// <summary>
        /// check_rotate_clockwise + rotate_left/right: <paramref name="heading"/> turned towards <paramref name="target"/>
        /// the shorter way round, by at most <paramref name="maxStep"/> degrees.
        /// </summary>
        public static double TurnToward(double heading, double target, double maxStep)
        {
            double delta = Difference(heading, target);
            if (Math.Abs(delta) <= Math.Max(0, maxStep))
            {
                return Angles.Normalize(target);
            }

            return Angles.Normalize(heading + Math.Sign(delta) * Math.Max(0, maxStep));
        }

        /// <summary>The signed shortest turn from <paramref name="from"/> to <paramref name="to"/>, in (-180, 180].</summary>
        public static double Difference(double from, double to)
        {
            double delta = Angles.Normalize(to - from);
            return delta > 180 ? delta - 360 : delta;
        }

        // The best run of headings: by kind (straight, bank, railgun), then the nearest target, then the widest run.
        private static AiPlan Best(Hit[] hits, Vec2 origin, IReadOnlyList<Target> targets, Weapon weapon)
        {
            List<Run> runs = Runs(hits);
            Run best = null;
            double bestDistance = 0;
            foreach (Run run in runs)
            {
                double distance = Distance(origin, run.Hit.TargetId, targets);
                bool better = best == null
                    || run.Hit.Kind < best.Hit.Kind
                    || (run.Hit.Kind == best.Hit.Kind && distance < bestDistance - 1e-9)
                    || (run.Hit.Kind == best.Hit.Kind && Math.Abs(distance - bestDistance) <= 1e-9 && run.Length > best.Length);
                if (better)
                {
                    best = run;
                    bestDistance = distance;
                }
            }

            if (best == null)
            {
                return null;
            }

            double aim = (best.Start + (best.Length - 1) / 2.0) * Step;
            return new AiPlan(best.Hit.Kind, weapon, aim, aim, best.Hit.TargetId);
        }

        // Unbroken runs of headings that reach the same target the same way; a run across 0 degrees is one run.
        private static List<Run> Runs(Hit[] hits)
        {
            var runs = new List<Run>();
            Run current = null;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].TargetId == null)
                {
                    current = null;
                    continue;
                }

                if (current != null && current.Hit.Same(hits[i]))
                {
                    current.Length++;
                    continue;
                }

                current = new Run { Start = i, Length = 1, Hit = hits[i] };
                runs.Add(current);
            }

            if (runs.Count > 1)
            {
                Run first = runs[0];
                Run last = runs[runs.Count - 1];
                if (first.Start == 0 && last.Start + last.Length == hits.Length && first.Hit.Same(last.Hit))
                {
                    last.Length += first.Length;
                    runs.RemoveAt(0);
                }
            }

            return runs;
        }

        private static double Distance(Vec2 origin, string targetId, IReadOnlyList<Target> targets)
        {
            if (targets != null)
            {
                foreach (Target target in targets)
                {
                    if (target.Id == targetId)
                    {
                        return Vec2.Distance(origin, target.Center);
                    }
                }
            }

            return double.MaxValue;
        }

        private readonly struct Hit
        {
            public Hit(string targetId, AiShotKind kind)
            {
                TargetId = targetId;
                Kind = kind;
            }

            public string TargetId { get; }

            public AiShotKind Kind { get; }

            public bool Same(Hit other) => TargetId == other.TargetId && Kind == other.Kind;
        }

        private sealed class Run
        {
            public int Start;
            public int Length;
            public Hit Hit;
        }
    }

    /// <summary>What an AI seat does on this frame of its turn (<see cref="AiTurn.Step"/>).</summary>
    public enum AiStep
    {
        /// <summary>Still thinking (the "is thinking..." line is up).</summary>
        Think,

        /// <summary>Done thinking: decide now (<see cref="Ai.Plan"/>) and hand the plan to <see cref="AiTurn.SetPlan"/>.</summary>
        Plan,

        /// <summary>Turn the barrel to the heading given.</summary>
        Turn,

        /// <summary>Aimed: fire the plan.</summary>
        Fire,
    }

    /// <summary>
    /// One AI seat's turn, as obj_CheckAI ran it each step: think for a moment, plan once, turn the barrel towards the
    /// planned heading at a steady rate (faster when the clock is short, so it always fires in time), then fire.
    /// </summary>
    public sealed class AiTurn
    {
        public AiTurn(int seat, int message, double thinkSeconds, double degreesPerSecond)
        {
            Seat = seat;
            Message = message;
            ThinkSeconds = Math.Max(0, thinkSeconds);
            DegreesPerSecond = Math.Max(1, degreesPerSecond);
        }

        /// <summary>The seat whose turn it is.</summary>
        public int Seat { get; }

        /// <summary>The "is thinking..." line it shows (<see cref="Ai.ThinkingLine"/>).</summary>
        public int Message { get; }

        public double ThinkSeconds { get; }

        public double DegreesPerSecond { get; }

        /// <summary>Seconds thought so far.</summary>
        public double Thought { get; private set; }

        /// <summary>The decision, once made.</summary>
        public AiPlan Plan { get; private set; }

        public void SetPlan(AiPlan plan) => Plan = plan;

        /// <summary>
        /// Advances the turn by <paramref name="seconds"/> with the barrel at <paramref name="heading"/> and
        /// <paramref name="timeLeft"/> on the clock. <paramref name="next"/> is where the barrel should point now.
        /// </summary>
        public AiStep Step(double seconds, double heading, double timeLeft, out double next)
        {
            next = heading;
            seconds = double.IsNaN(seconds) ? 0 : Math.Max(0, seconds);
            if (Plan == null)
            {
                Thought += seconds;
                return Thought >= ThinkSeconds ? AiStep.Plan : AiStep.Think;
            }

            double target = Plan.Heading;
            double delta = Math.Abs(Ai.Difference(heading, target));
            if (delta <= 1e-3 || timeLeft <= 0)
            {
                next = target;
                return AiStep.Fire;
            }

            // Arrive with at least half the remaining clock to spare.
            double rate = Math.Max(DegreesPerSecond, delta / Math.Max(1e-3, timeLeft * 0.5));
            next = Ai.TurnToward(heading, target, rate * seconds);
            return Math.Abs(Ai.Difference(next, target)) <= 1e-3 ? AiStep.Fire : AiStep.Turn;
        }
    }
}
