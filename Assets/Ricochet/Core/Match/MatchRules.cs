// The numbers a match plays by. Defaults follow the 2013 original (globals.gml, hit.gml, turn_countdown.gml, design
// reference only, UR-LAW-2): 100 health a player (maxhp), a 20-second turn (turnTime; draw_turntime showed "20.00" before
// the first turn), a countdown beep on each of the last four seconds, and a cannon that does 100 damage (weapon_damage, config.ini default; one hit is a knock-out)
// plus a fifth of that per bounce to an opponent, or the full 100 per bounce to yourself, with a dice roll of a tenth
// either way on an opponent hit. Scoring (UR-T8) follows hit.gml and draw_potential.gml: a hit on an opponent pays the
// shooter 100 points (points_kill), 25 a bounce (points_bounce) and 5 for every tenth of a second in the air
// (points_flighttime on obj_bullet's alarm); scores carry over from round to round for the whole match.
using System;

namespace Ricochet.Core.Match
{
    using Ricochet.Core.Arsenal;
    public sealed class MatchRules
    {
        /// <summary>The most seats a match has, humans and AI together (the original's eight: obj_btn_players_more and obj_btn_ai_more stop at 8).</summary>
        public const int MaxSeats = 8;

        /// <summary>Health a player starts each round with (maxhp).</summary>
        public int MaxHealth { get; set; }

        /// <summary>Seconds a player has to fire (turnTime).</summary>
        public double TurnSeconds { get; set; }

        /// <summary>The last this-many whole seconds of a turn each beep (turn_countdown's 4, 3, 2, 1).</summary>
        public int CountdownBeeps { get; set; }

        /// <summary>A hit's base damage (weapon_damage, the same for every weapon; a shotgun pellet does a share, Match.Strike).</summary>
        public int WeaponDamage { get; set; }

        /// <summary>Seconds between a shot landing and the next player's turn (obj_endturn's pause).</summary>
        public double EndTurnSeconds { get; set; }

        /// <summary>Seconds the round's result stays up before the next round starts.</summary>
        public double RoundOverSeconds { get; set; }

        /// <summary>
        /// Seconds the turn-start Ready card holds at full strength (UR-T42: obj_ready's alarm[0] = 80 steps at the game
        /// room's 60 a second) before it fades.
        /// </summary>
        public double ReadyHoldSeconds { get; set; }

        /// <summary>Seconds the Ready card takes to fade out (obj_ready's alpha -= 0.02 a step: 50 steps at 60 a second).</summary>
        public double ReadyFadeSeconds { get; set; }

        /// <summary>The whole hold before a turn's clock starts: the Ready card's hold and its fade.</summary>
        public double ReadySeconds => Math.Max(0, ReadyHoldSeconds) + Math.Max(0, ReadyFadeSeconds);

        /// <summary>Points for landing a hit on an opponent (points_kill; hit.gml paid it on every hit, not only a kill).</summary>
        public int PointsHit { get; set; }

        /// <summary>Points for every bounce the striking shot made first (points_bounce).</summary>
        public int PointsBounce { get; set; }

        /// <summary>Points for every <see cref="FlightBonusSeconds"/> the shot was in the air (points_flighttime).</summary>
        public int PointsFlightTime { get; set; }

        /// <summary>
        /// How often the flight bonus ticks: obj_bullet's alarm added points_flighttime every 100 steps at shootFPS (1020),
        /// about a tenth of a second.
        /// </summary>
        public double FlightBonusSeconds { get; set; }

        /// <summary>The stock rules, from match.json (UR-T35): a new set starts as a copy of them.</summary>
        public MatchRules()
        {
            CopyFrom(Ricochet.Core.Data.GameData.Current.Match);
        }

        internal MatchRules(bool blank)
        {
        }

        public static MatchRules Default => new MatchRules();

        internal void CopyFrom(MatchRules other)
        {
            MaxHealth = other.MaxHealth;
            TurnSeconds = other.TurnSeconds;
            CountdownBeeps = other.CountdownBeeps;
            WeaponDamage = other.WeaponDamage;
            EndTurnSeconds = other.EndTurnSeconds;
            RoundOverSeconds = other.RoundOverSeconds;
            ReadyHoldSeconds = other.ReadyHoldSeconds;
            ReadyFadeSeconds = other.ReadyFadeSeconds;
            PointsHit = other.PointsHit;
            PointsBounce = other.PointsBounce;
            PointsFlightTime = other.PointsFlightTime;
            FlightBonusSeconds = other.FlightBonusSeconds;
        }

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
