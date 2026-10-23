// The numbers a match plays by. Defaults follow the 2013 original (globals.gml, hit.gml, turn_countdown.gml, design
// reference only, UR-LAW-2): 100 health a player (maxhp), a 20-second turn (turnTime; draw_turntime showed "20.00" before
// the first turn), a countdown beep on each of the last four seconds, and a cannon that does 10 damage (weapon_damage)
// plus a fifth of that per bounce to an opponent, or the full 10 per bounce to yourself, with a dice roll of a tenth
// either way on an opponent hit.
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

        public static MatchRules Default => new MatchRules();

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
