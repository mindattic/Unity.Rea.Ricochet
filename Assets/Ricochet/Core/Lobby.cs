// The host's lobby (UR-T11): what it says about the match the host is about to start. The 2013 original (design
// reference only, UR-LAW-2) had multiplayer_hostorjoin/host/join rooms for passing one keyboard around; real network play
// replaces them with the kit's Host, Join by IP and Join by code and its lobby, which already shows the join code (or the
// address) and who is in. This is the game's part of that screen: the map and the goal, how the sixteen seats fill
// (everyone in the lobby in join order, then as many of the AI opponents asked for as still fit, the rule Arena.StartMatch
// seats a match by), and whether Start can begin a match yet.
using System;
using System.Collections.Generic;

namespace Ricochet.Core
{
    public static class Lobby
    {
        /// <summary>The fewest seats a match plays with.</summary>
        public const int MinSeats = 2;

        /// <summary>The seats the people in the lobby take: all of them, up to sixteen.</summary>
        public static int HumanSeats(int humans) => Math.Max(0, Math.Min(humans, MatchRules.MaxSeats));

        /// <summary>The AI opponents that take a seat: as many of <paramref name="aiCount"/> as fit after the people.</summary>
        public static int AiSeats(int humans, int aiCount) => Math.Max(0, Math.Min(aiCount, MatchRules.MaxSeats - HumanSeats(humans)));

        /// <summary>True when a match can start: at least two seats would play.</summary>
        public static bool CanStart(int humans, int aiCount) => HumanSeats(humans) + AiSeats(humans, aiCount) >= MinSeats;

        /// <summary>
        /// How the seats fill: "2 players and 3 AI opponents: 5 of 16 seats", with what is missing when a match cannot start
        /// yet and how many AI opponents were left out when the table is full.
        /// </summary>
        public static string SeatLine(int humans, int aiCount)
        {
            int people = HumanSeats(humans);
            int ai = AiSeats(humans, aiCount);
            string line = Count(people, "player") + " and " + (ai == 0 ? "no AI opponents" : Count(ai, "AI opponent"));
            if (people + ai < MinSeats)
            {
                return line + ": a match needs " + MinSeats + " (wait for someone to join, or add an AI opponent on Players)";
            }

            line += ": " + (people + ai) + " of " + MatchRules.MaxSeats + " seats";
            if (ai < Math.Max(0, aiCount))
            {
                line += " (" + (Math.Max(0, aiCount) - ai) + " AI left out, the table is full)";
            }

            return line;
        }

        /// <summary>
        /// The lobby's match box, a line each: the map and the goal, how the seats fill, the AI and aim assists, and the
        /// announcer.
        /// </summary>
        public static IReadOnlyList<string> Plan(MatchSetup setup, int humans)
        {
            setup = setup ?? new MatchSetup();
            return new[] { setup.DescribeGame(), SeatLine(humans, setup.AiCount), setup.Describe(), setup.DescribeAnnouncer() };
        }

        /// <summary>The lobby's line when the host hosted from the main menu, with no match chosen yet.</summary>
        public const string PracticeLine = "Free practice on the dummies. Start, then Esc > Vote to start a match when everyone is in.";

        /// <summary>The lobby's line for the others: how they get in.</summary>
        public const string JoinLine = "The others click Join by code (or Join by IP) on their main menu.";

        private static string Count(int n, string noun) => n + " " + noun + (n == 1 ? string.Empty : "s");
    }
}
