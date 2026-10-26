// The names an AI opponent can wear (UR-T28). The original's players room gave every player a default name
// (write_default_names: the eight in backup.ini) or a random one (obj_btn_random_names, get_random_name: a first name out
// of names_f.ini and names_m.ini, about 5,300 of them). Here a short list of first names stands in: an AI's name is an
// index into it, or Default (-1, "AI n"), so a name travels as one small number.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Ricochet.Core.Ai
{
    public static class AiNames
    {
        /// <summary>The index meaning "no chosen name": the AI is called "AI n".</summary>
        public const int Default = -1;

        private static readonly string[] s_names =
        {
            "Ada", "Alma", "Barney", "Beth", "Bruno", "Cleo", "Cora", "Dale", "Dana", "Dex", "Edna", "Eli", "Ezra", "Fern", "Finn", "Gus",
            "Hank", "Hazel", "Ida", "Igor", "Ivy", "Jack", "Jade", "Kai", "Kurt", "Lena", "Leo", "Lily", "Max", "Mia", "Milo", "Nell",
            "Nico", "Olga", "Otto", "Pearl", "Pete", "Quinn", "Rae", "Rex", "Rita", "Rosa", "Sam", "Stella", "Tess", "Theo", "Uma", "Vic",
            "Vera", "Walt", "Wren", "Xena", "Yuri", "Zane", "Zoe", "Bea", "Cal", "Dot", "Gil", "Hugo", "Ines", "Joy", "Lou", "Moe",
        };

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

        /// <summary>The Random names button: <paramref name="count"/> distinct names (as many as the list holds), drawn with <paramref name="random"/>.</summary>
        public static int[] RandomIndices(int count, Random random)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            var pool = new List<int>(s_names.Length);
            for (int i = 0; i < s_names.Length; i++)
            {
                pool.Add(i);
            }

            var picks = new int[Math.Max(0, count)];
            for (int i = 0; i < picks.Length; i++)
            {
                if (pool.Count == 0)
                {
                    picks[i] = Default;
                    continue;
                }

                int at = random.Next(pool.Count);
                picks[i] = pool[at];
                pool.RemoveAt(at);
            }

            return picks;
        }
    }
}
