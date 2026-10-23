// The numbers a match plays by. Defaults follow the 2013 original (globals.gml, hit.gml, turn_countdown.gml, design
// reference only, UR-LAW-2): 100 health a player (maxhp), a 20-second turn (turnTime; draw_turntime showed "20.00" before
// the first turn), a countdown beep on each of the last four seconds, and a cannon that does 10 damage (weapon_damage)
// plus a fifth of that per bounce to an opponent, or the full 10 per bounce to yourself, with a dice roll of a tenth
// either way on an opponent hit. Scoring (UR-T8) follows hit.gml and draw_potential.gml: a hit on an opponent pays the
// shooter 100 points (points_kill), 25 a bounce (points_bounce) and 5 for every tenth of a second in the air
// (points_flighttime on obj_bullet's alarm); scores carry over from round to round for the whole match.
using System;

namespace Ricochet.Core
{
    public sealed class MatchRules
    {
        /// <summary>The most seats a match has, humans and AI together (the original's sixteen).</summary>
        public const int MaxSeats = 16;

        /// <summary>Health a player starts each round with (maxhp).</summary>
        public int MaxHealth { get; set; } = 100;

        /// <summary>Seconds a player has to fire (turnTime).</summary>
        public double TurnSeconds { get; set; } = 20;

        /// <summary>The last this-many whole seconds of a turn each beep (turn_countdown's 4, 3, 2, 1).</summary>
        public int CountdownBeeps { get; set; } = 4;

        /// <summary>A hit's base damage (weapon_damage, the same for every weapon; a shotgun pellet does a share, Match.Strike).</summary>
        public int WeaponDamage { get; set; } = 10;

        /// <summary>Seconds between a shot landing and the next player's turn (obj_endturn's pause).</summary>
        public double EndTurnSeconds { get; set; } = 1.25;

        /// <summary>Seconds the round's result stays up before the next round starts.</summary>
        public double RoundOverSeconds { get; set; } = 4;

        /// <summary>Seconds an AI seat "thinks" before it fires (the original's "is thinking..." pause).</summary>
        public double AiThinkSeconds { get; set; } = 1.5;

        /// <summary>Degrees a second an AI seat turns its barrel towards its aim (rotate_left/right); faster when the clock is short.</summary>
        public double AiDegreesPerSecond { get; set; } = 180;

        /// <summary>Points for landing a hit on an opponent (points_kill; hit.gml paid it on every hit, not only a kill).</summary>
        public int PointsHit { get; set; } = 100;

        /// <summary>Points for every bounce the striking shot made first (points_bounce).</summary>
        public int PointsBounce { get; set; } = 25;

        /// <summary>Points for every <see cref="FlightBonusSeconds"/> the shot was in the air (points_flighttime).</summary>
        public int PointsFlightTime { get; set; } = 5;

        /// <summary>
        /// How often the flight bonus ticks: obj_bullet's alarm added points_flighttime every 100 steps at shootFPS (1020),
        /// about a tenth of a second.
        /// </summary>
        public double FlightBonusSeconds { get; set; } = 100.0 / 1020.0;

        public static MatchRules Default => new MatchRules();

        /// <summary>
        /// draw_potential.gml's point_combo: what a hit on an opponent scores, points_kill + points_bounce a bounce + the
        /// flight bonus for <paramref name="flightSeconds"/> in the air. Never negative.
        /// </summary>
        public int Points(int bounces, double flightSeconds)
        {
            int flight = double.IsNaN(flightSeconds) || flightSeconds <= 0 || FlightBonusSeconds <= 0
                ? 0
                : (int)Math.Min(int.MaxValue / 4, Math.Floor(flightSeconds / FlightBonusSeconds + 1e-9));
            long points = (long)Math.Max(0, PointsHit) + (long)Math.Max(0, PointsBounce) * Math.Max(0, bounces) + (long)Math.Max(0, PointsFlightTime) * flight;
            return (int)Math.Min(int.MaxValue / 4, points);
        }

        /// <summary>
        /// hit.gml's damage: to an opponent, base + ceil(base / 5) per bounce, plus a roll of up to base / 10 either way;
        /// to yourself, base + base per bounce and no roll. <paramref name="roll"/> is a number in [-1, 1] (the server's
        /// dice), scaled to the spread; never less than 1.
        /// </summary>
        public int Damage(bool self, int bounces, double roll)
        {
            bounces = Math.Max(0, bounces);
            int baseDamage = Math.Max(1, WeaponDamage);
            if (self)
            {
                return baseDamage + baseDamage * bounces;
            }

            int spread = baseDamage / 10;
            double clamped = double.IsNaN(roll) ? 0 : Math.Max(-1, Math.Min(1, roll));
            int dice = (int)Math.Round(clamped * spread, MidpointRounding.AwayFromZero);
            int perBounce = (int)Math.Ceiling(baseDamage / 5.0);
            return Math.Max(1, baseDamage + perBounce * bounces + dice);
        }
    }
}
