// How players tell each other apart (UR-T21). Two players on default profiles both arrive as "Default" in the same
// teal; the original gave every seat its own random colour (setup_players.gml) and a default name (write_default_names).
// Given who sits where, in seat order, this keeps the first of any colour or name and gives a later duplicate a colour
// no one else has and a numbered name ("Default 2"), so the lobby, bars, tank labels and turn order all read apart.
// Pure and deterministic: every peer resolves the same list the same way.
using System;
using System.Collections.Generic;

namespace Ricochet.Core.Menu
{
    /// <summary>A player's name and colour (0xRRGGBB).</summary>
    public readonly struct PlayerLook
    {
        public PlayerLook(string name, int rgb)
        {
            Name = name ?? string.Empty;
            Rgb = rgb & 0xFFFFFF;
        }

        public string Name { get; }

        public int Rgb { get; }
    }

    public static class PlayerLooks
    {
        /// <summary>Colours closer than this (summed channel difference) read as the same.</summary>
        public const int NearDistance = 60;

        /// <summary>Sixteen well-separated colours, one per possible seat.</summary>
        public static readonly int[] Palette =
        {
            0xE6194B, 0x3CB44B, 0xFFE119, 0x4363D8, 0xF58231, 0x911EB4, 0x46F0F0, 0xF032E6,
            0xBCF60C, 0xFABEBE, 0x008080, 0xE6BEFF, 0x9A6324, 0xFFFAC8, 0x800000, 0xAAFFC3,
        };

        /// <summary>
        /// The looks to show, in the order given: the first player keeps a colour and name, a later one that repeats an
        /// earlier colour (or one near it) gets the first palette colour nobody wants or holds, and a repeated name gets
        /// " 2", " 3" and so on.
        /// </summary>
        public static PlayerLook[] Resolve(IReadOnlyList<PlayerLook> wanted)
        {
            int n = wanted.Count;
            var result = new PlayerLook[n];
            var colours = new List<int>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < n; i++)
            {
                names.Add(wanted[i].Name);
            }

            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < n; i++)
            {
                int rgb = wanted[i].Rgb;
                if (Taken(colours, rgb))
                {
                    rgb = Pick(colours, wanted, i);
                }

                colours.Add(rgb);

                string name = wanted[i].Name;
                if (!seenNames.Add(name))
                {
                    int k = 2;
                    string candidate;
                    do
                    {
                        candidate = name + " " + k++;
                    }
                    while (names.Contains(candidate) || seenNames.Contains(candidate));

                    seenNames.Add(candidate);
                    name = candidate;
                }

                result[i] = new PlayerLook(name, rgb);
            }

            return result;
        }

        private static int Pick(List<int> held, IReadOnlyList<PlayerLook> wanted, int self)
        {
            // Prefer a colour no one else asked for either, so a later player is not pushed off their own pick.
            foreach (int candidate in Palette)
            {
                if (!Taken(held, candidate) && !AskedByOther(wanted, self, candidate))
                {
                    return candidate;
                }
            }

            foreach (int candidate in Palette)
            {
                if (!Taken(held, candidate))
                {
                    return candidate;
                }
            }

            return Palette[self % Palette.Length];
        }

        private static bool AskedByOther(IReadOnlyList<PlayerLook> wanted, int self, int rgb)
        {
            for (int i = 0; i < wanted.Count; i++)
            {
                if (i != self && Near(wanted[i].Rgb, rgb))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Taken(List<int> held, int rgb)
        {
            foreach (int other in held)
            {
                if (Near(other, rgb))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Near(int a, int b)
        {
            int d = Math.Abs(((a >> 16) & 255) - ((b >> 16) & 255)) + Math.Abs(((a >> 8) & 255) - ((b >> 8) & 255)) + Math.Abs((a & 255) - (b & 255));
            return d < NearDistance;
        }
    }
}
