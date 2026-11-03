// AI opponents (UR-T7). The 2013 original (design reference only, UR-LAW-2) ran an AI seat's turn from obj_CheckAI and
// check_ai.gml: it put up one of thirty "is thinking..." lines (obj_ai_gui pulsed it over the blacked-out controls), swept
// every whole degree for a straight shot that reached a living opponent before the first wall (find_target_angle.gml),
// then, failing that, fired an invisible tracer at every degree to find a bank shot (shoot_tracer and
// find_target_angle_from_bounce.gml), and failing that picked a random angle; it nudged the angle it found by up to
// 10 - skill degrees either way (pSkill, 0 to 10 per player), turned the barrel towards it a step at a time
// (check_rotate_clockwise, rotate_left/right) and fired a bullet once it was pointing there.
// Here each pass is the same exact solver the preview and the server's shot use (BounceSolver), as the original's did
// it (U.RICO-T98): the straight-line pass takes the first whole degree from 0 whose line, out to the first shape or the
// border, touches a living opponent; the tracer pass flies a shell at every whole degree off the shapes and the border
// (tracer_step: no hazards, no redirectors) and takes the one that reaches an opponent soonest, a tracer that comes back
// into its own tank being lost; the AI always fires a cannon shell (shoot_bullet, which spends nothing). Aim assist (the
// item) borrows the same targeting until U.RICO-T97 restores its own.
// U.RICO-T116 (spike, never the default): match.json's "smartAi" (false as shipped) brings back, as an option, the
// rebuild's smarter AI that U.RICO-T98 took out: PlanSmart sweeps every half degree in one pass, prefers a straight shot
// to a bank shot to a railgun slug through the walls (when it carries one), takes the nearest opponent and aims at the
// middle of the widest run of headings that reach them; its AiTurn thinks for aiThinkSeconds before it decides and turns
// at aiDegreesPerSecond, faster when the clock is short. With the flag off none of it is reached.
using System;
using System.Collections.Generic;

namespace Rea.Ricochet.Core.Ai
{
    using Rea.Ricochet.Core.Aim;
    using Rea.Ricochet.Core.Arsenal;
    using Rea.Ricochet.Core.Level;
    /// <summary>How an AI's planned shot reaches its target.</summary>
    public enum AiShotKind
    {
        /// <summary>A straight line to an opponent, no bounce (find_target_angle).</summary>
        Direct,

        /// <summary>Off the border and the shapes onto an opponent (shoot_tracer: the tracer that reached one first).</summary>
        Bank,

        /// <summary>No aim reaches anyone: a random heading, so the turn is not wasted on the clock.</summary>
        Blind,

        /// <summary>U.RICO-T116 (smart AI only): a railgun slug straight through the walls.</summary>
        Railgun,
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

        /// <summary>The weapon it fires: always the cannon (obj_check_ai calls shoot_bullet); the smart AI's railgun shot (U.RICO-T116) fires the railgun.</summary>
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

        /// <summary>The skill a match's AI seats play at unless set (config.ini's skill1-8 = 10).</summary>
        public const int DefaultSkill = 10;

        /// <summary>Degrees between the headings the passes try (check_ai's whole degrees).</summary>
        public const double Step = 1;

        /// <summary>U.RICO-T116: degrees between the headings the smart AI's sweep tries.</summary>
        public const double SmartStep = 0.5;

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
        /// check_ai.gml: an AI's whole decision for one turn: the straight shot, else the tracers' bank shot
        /// (<see cref="Aim"/>), nudged by a whole number of degrees up to 10 - <paramref name="skill"/>; or, when nothing
        /// reaches anyone, a random whole degree. <paramref name="targets"/> may include the AI itself
        /// (<paramref name="selfId"/>), which it never aims at.
        /// </summary>
        public static AiPlan Plan(Vec2 origin, string selfId, Level level, IReadOnlyList<Target> targets, int skill, Random dice,
            ShotRules rules = null)
        {
            dice = dice ?? new Random();
            AiPlan aimed = Aim(origin, selfId, level, targets, rules);
            if (aimed == null)
            {
                double random = dice.Next(0, 360); // obj_check_ai: target_angle = irandom_range(0, 359)
                return new AiPlan(AiShotKind.Blind, Weapon.Cannon, random, random, null);
            }

            return new AiPlan(aimed.Kind, Weapon.Cannon, aimed.Aim, aimed.Aim + Jitter(skill, dice), aimed.TargetId);
        }

        /// <summary>
        /// The aim-assist item: the shot an AI at full skill would take with the cannon. Null when no aim reaches anyone.
        /// </summary>
        public static AiPlan Assist(Vec2 origin, string selfId, Level level, IReadOnlyList<Target> targets, ShotRules rules = null)
        {
            return Aim(origin, selfId, level, targets, rules);
        }

        /// <summary>
        /// check_ai.gml's two passes. find_target_angle: the first whole degree from 0 whose straight line, out to the first
        /// shape or the border, touches a living opponent. Failing that, shoot_tracer: a shell at every whole degree,
        /// bouncing off the shapes and the border up to the bounce limit, and the degree of the one that reaches an opponent
        /// soonest (the shortest flight; the lower degree on a tie), one that comes back into the AI's own tank being lost.
        /// Null when neither reaches anyone. Hazards and redirectors take no part, as they took none in the original.
        /// </summary>
        public static AiPlan Aim(Vec2 origin, string selfId, Level level, IReadOnlyList<Target> targets, ShotRules rules = null)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            rules = rules ?? ShotRules.Default;
            var shapes = new Level(level.MinX, level.MinY, level.MaxX, level.MaxY, level.Walls);
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

            ShotRules line = rules.Clone();
            line.MaxBounces = 1;
            int degrees = (int)Math.Round(360 / Step);
            for (int d = 0; d < degrees; d++)
            {
                ShotPath path = BounceSolver.Solve(origin, d * Step, shapes, opponents, line);
                if (path.End == ShotEndKind.Target)
                {
                    return new AiPlan(AiShotKind.Direct, Weapon.Cannon, d * Step, d * Step, path.TargetId);
                }
            }

            AiPlan soonest = null;
            double shortest = double.MaxValue;
            for (int d = 0; d < degrees; d++)
            {
                ShotPath path = BounceSolver.Solve(origin, d * Step, shapes, targets, rules);
                if (path.End == ShotEndKind.Target && path.TargetId != selfId && path.Length < shortest - 1e-9)
                {
                    shortest = path.Length;
                    soonest = new AiPlan(AiShotKind.Bank, Weapon.Cannon, d * Step, d * Step, path.TargetId);
                }
            }

            return soonest;
        }

        /// <summary>
        /// U.RICO-T116 (smart AI, never the default): the smarter decision: <see cref="AimSmart"/> (with the railgun
        /// considered when <paramref name="loadout"/> carries one), nudged by <paramref name="skill"/>; or a random heading
        /// when no shot reaches anyone.
        /// </summary>
        public static AiPlan PlanSmart(Vec2 origin, string selfId, Level level, IReadOnlyList<Target> targets, int skill, Random dice,
            ShotRules rules = null, ArsenalRules arsenal = null, Loadout loadout = null)
        {
            dice = dice ?? new Random();
            bool railgun = loadout != null && loadout.Has(Weapon.Railgun);
            AiPlan aimed = AimSmart(origin, selfId, level, targets, rules, arsenal, railgun);
            if (aimed == null)
            {
                double random = dice.NextDouble() * 360;
                return new AiPlan(AiShotKind.Blind, Weapon.Cannon, random, random, null);
            }

            return new AiPlan(aimed.Kind, aimed.Weapon, aimed.Aim, aimed.Aim + Jitter(skill, dice), aimed.TargetId);
        }

        /// <summary>
        /// U.RICO-T116 (smart AI): the one-sweep targeting. Every heading at <see cref="SmartStep"/>-degree steps is solved
        /// once; a straight shot onto an opponent beats a bank shot, which beats a railgun shot (tried only when
        /// <paramref name="railgun"/>); among those, the nearest opponent, then the widest run of headings that reach them,
        /// aimed at its middle. Null when no heading reaches anyone.
        /// </summary>
        public static AiPlan AimSmart(Vec2 origin, string selfId, Level level, IReadOnlyList<Target> targets, ShotRules rules = null,
            ArsenalRules arsenal = null, bool railgun = false)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            rules = rules ?? ShotRules.Default;
            int steps = (int)Math.Round(360 / SmartStep);
            var hits = new Hit[steps];
            bool any = false;
            for (int i = 0; i < steps; i++)
            {
                ShotPath path = BounceSolver.Solve(origin, i * SmartStep, level, targets, rules);
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
                Volley volley = Arsenal.Fire(Weapon.Railgun, origin, i * SmartStep, 0, 0, level, targets, rules, arsenal);
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

        // Smart AI (U.RICO-T116): a shot kind's rank (straight, then bank, then railgun).
        private static int Rank(AiShotKind kind) => kind == AiShotKind.Direct ? 0 : kind == AiShotKind.Bank ? 1 : 2;

        // Smart AI: the best run of headings: by kind, then the nearest target, then the widest run.
        private static AiPlan Best(Hit[] hits, Vec2 origin, IReadOnlyList<Target> targets, Weapon weapon)
        {
            List<Run> runs = Runs(hits);
            Run best = null;
            double bestDistance = 0;
            foreach (Run run in runs)
            {
                double distance = Distance(origin, run.Hit.TargetId, targets);
                bool better = best == null
                    || Rank(run.Hit.Kind) < Rank(best.Hit.Kind)
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

            double aim = (best.Start + (best.Length - 1) / 2.0) * SmartStep;
            return new AiPlan(best.Hit.Kind, weapon, aim, aim, best.Hit.TargetId);
        }

        // Smart AI: unbroken runs of headings that reach the same target the same way; a run across 0 degrees is one run.
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
        /// <summary>Decide now (<see cref="Ai.Plan"/>) and hand the plan to <see cref="AiTurn.SetPlan"/>.</summary>
        Plan,

        /// <summary>Turn the barrel to the heading given.</summary>
        Turn,

        /// <summary>Aimed: fire the plan.</summary>
        Fire,

        /// <summary>U.RICO-T116 (smart AI only): still thinking (the "is thinking..." line is up).</summary>
        Think,
    }

    /// <summary>
    /// One AI seat's turn, as obj_check_ai ran it each step from the moment its turn started (obj_ready's ExecuteOnce):
    /// decide at once (check_ai), turn the barrel towards the planned heading as a player turns it (rotate_left/right at
    /// the rules' turn rate, the shorter way round, check_rotate_clockwise), and fire once it points there. Its
    /// "is thinking..." line stays up while it turns. A turn whose clock runs out first passes, as any turn does.
    /// </summary>
    public sealed class AiTurn
    {
        public AiTurn(int seat, int message, double degreesPerSecond)
        {
            Seat = seat;
            Message = message;
            DegreesPerSecond = Math.Max(1, degreesPerSecond);
        }

        /// <summary>
        /// U.RICO-T116 (smart AI, never the default): a turn that thinks for <paramref name="thinkSeconds"/> before it
        /// decides and turns faster when the clock is short, so it always fires in time
        /// (<see cref="Step(double, double, double, out double)"/>).
        /// </summary>
        public AiTurn(int seat, int message, double thinkSeconds, double degreesPerSecond)
            : this(seat, message, degreesPerSecond)
        {
            ThinkSeconds = Math.Max(0, thinkSeconds);
        }

        /// <summary>U.RICO-T116: seconds the smart AI thinks before it decides (0 for the original's turn).</summary>
        public double ThinkSeconds { get; }

        /// <summary>U.RICO-T116: seconds thought so far.</summary>
        public double Thought { get; private set; }

        /// <summary>The seat whose turn it is.</summary>
        public int Seat { get; }

        /// <summary>The "is thinking..." line it shows (<see cref="Ai.ThinkingLine"/>).</summary>
        public int Message { get; }

        public double DegreesPerSecond { get; }

        /// <summary>The decision, once made.</summary>
        public AiPlan Plan { get; private set; }

        public void SetPlan(AiPlan plan) => Plan = plan;

        /// <summary>
        /// Advances the turn by <paramref name="seconds"/> with the barrel at <paramref name="heading"/>.
        /// <paramref name="next"/> is where the barrel should point now.
        /// </summary>
        public AiStep Step(double seconds, double heading, out double next)
        {
            next = heading;
            if (Plan == null)
            {
                return AiStep.Plan;
            }

            seconds = double.IsNaN(seconds) ? 0 : Math.Max(0, seconds);
            double target = Plan.Heading;
            if (Math.Abs(Ai.Difference(heading, target)) <= 1e-3)
            {
                next = target;
                return AiStep.Fire;
            }

            next = Ai.TurnToward(heading, target, DegreesPerSecond * seconds);
            return Math.Abs(Ai.Difference(next, target)) <= 1e-3 ? AiStep.Fire : AiStep.Turn;
        }

        /// <summary>
        /// U.RICO-T116 (smart AI): as <see cref="Step(double, double, out double)"/>, but it thinks for
        /// <see cref="ThinkSeconds"/> first and, with <paramref name="timeLeft"/> on the clock, turns fast enough to arrive
        /// with half of it to spare, firing where it points once the clock is out.
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

            double rate = Math.Max(DegreesPerSecond, delta / Math.Max(1e-3, timeLeft * 0.5));
            next = Ai.TurnToward(heading, target, rate * seconds);
            return Math.Abs(Ai.Difference(next, target)) <= 1e-3 ? AiStep.Fire : AiStep.Turn;
        }
    }
}
