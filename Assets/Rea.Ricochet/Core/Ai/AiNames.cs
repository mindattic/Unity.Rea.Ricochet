// The names an AI opponent can wear (UR-T28). The original's players room gave every player a default name
// (write_default_names: the eight in backup.ini) or a random one (obj_btn_random_names, get_random_name: a first name out
// of names_f.ini and names_m.ini, about 5,300 of them). Those two files are carried over as data (AiNamesData): an AI's name is an
// index into them, or Default (-1, "AI n"), so a name travels as one small number.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Ricochet.Core.Ai
{
    public static class AiNames
    {
        /// <summary>The index meaning "no chosen name": the AI is called "AI n".</summary>
        public const int Default = -1;

        // The original's two pools (names_f.ini then names_m.ini), as one list: an index below FemaleCount is a female name.
        private static readonly string[] s_names = Join(AiNamesData.Female, AiNamesData.Male);

        // The pools share some names (Sam, Dana, ...): a draw never gives two seats the same one.
        private static readonly int s_distinct = new HashSet<string>(s_names, StringComparer.Ordinal).Count;

        private static string[] Join(string[] first, string[] second)
        {
            var all = new string[first.Length + second.Length];
            first.CopyTo(all, 0);
            second.CopyTo(all, first.Length);
            return all;
        }

        /// <summary>How many names can be picked.</summary>
        public static int Count => s_names.Length;

        /// <summary>The picked names, in order.</summary>
        public static IReadOnlyList<string> All => s_names;

        /// <summary>A name index the list has, or <see cref="Default"/> for anything else.</summary>
        public static int Clamp(int index) => index >= 0 && index < s_names.Length ? index : Default;

        /// <summary>The name shown for AI number <paramref name="number"/> (1 and up): its picked name, or "AI n".</summary>
        public static string NameFor(int number, int index)
        {
            index = Clamp(index);
            return index == Default ? "AI " + number.ToString(CultureInfo.InvariantCulture) : s_names[index];
        }

        /// <summary>The Random names button: <paramref name="count"/> distinct names (as many as the pools hold), drawn with <paramref name="random"/>.</summary>
        public static int[] RandomIndices(int count, Random random)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            // get_random_name: a coin picks names_m or names_f, then a line of it; names here are also kept distinct.
            int femaleCount = AiNamesData.Female.Length;
            var taken = new HashSet<string>(StringComparer.Ordinal);
            var picks = new int[Math.Max(0, count)];
            for (int i = 0; i < picks.Length; i++)
            {
                if (taken.Count >= s_distinct)
                {
                    picks[i] = Default;
                    continue;
                }

                int pick;
                do
                {
                    pick = random.Next(2) == 0 ? random.Next(femaleCount) : femaleCount + random.Next(AiNamesData.Male.Length);
                }
                while (!taken.Add(s_names[pick]));
                picks[i] = pick;
            }

            return picks;
        }
    }
}
