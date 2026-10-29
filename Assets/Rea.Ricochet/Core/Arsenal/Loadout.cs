// What one player carries: a count of each weapon and item, the one of each that is selected, and their grenade fuse.
// Ports cycle_weapon_left/right.gml and cycle_item_left/right.gml (from the selected kind, walk the others in order,
// wrapping, and select the first one the player has any of; with none, the selection stays), the spend rule of
// fire_weapon.gml and use_item.gml (a count of 10 is unlimited and never goes down; spending the last one moves the
// selection right), add_random_weapon/item.gml's "+ amount" (capped below unlimited here, where the original could
// count a player up into it), and the fuse keys of obj_game (1 shorter, 2 longer, 3 longest). The server keeps one per
// player; it travels to every peer packed into two ints (Pack/Unpack), since a [Synced] field cannot be an array.
using System;

namespace Ricochet.Core.Arsenal
{
    public sealed class Loadout
    {
        private const int CountBits = 4;
        private const int CountMask = (1 << CountBits) - 1;
        private const int SelectedShift = ArsenalRules.Kinds * CountBits; // 24
        private const int SelectedMask = 0x7;
        private const int FuseShift = SelectedShift + 3;                   // 27
        private const int FuseMask = 0xF;                                  // half-seconds, 1..15

        private readonly int[] _weapons = new int[ArsenalRules.Kinds];
        private readonly int[] _items = new int[ArsenalRules.Kinds];

        public Loadout(ArsenalRules rules = null)
        {
            Rules = rules ?? ArsenalRules.Default;
            Reset();
        }

        public ArsenalRules Rules { get; }

        public Weapon Weapon { get; private set; }

        public Item Item { get; private set; }

        /// <summary>The grenade's fuse, in seconds.</summary>
        public double Fuse { get; private set; }

        public int Count(Weapon weapon) => _weapons[(int)weapon];

        public int Count(Item item) => _items[(int)item];

        public bool Has(Weapon weapon) => _weapons[(int)weapon] > 0;

        public bool Has(Item item) => _items[(int)item] > 0;

        /// <summary>
        /// Back to the rules' starting counts (each player's count_X), the cannon and extra bounce selected (or the
        /// first thing carried right of them when there are none), and the default fuse.
        /// </summary>
        public void Reset()
        {
            for (int i = 0; i < ArsenalRules.Kinds; i++)
            {
                _weapons[i] = Rules.StartCount((Weapon)i);
                _items[i] = Rules.StartCount((Item)i);
            }

            Weapon = Weapon.Cannon;
            Item = Item.ExtraBounce;
            if (!Has(Weapon))
            {
                CycleWeapon(1);
            }

            if (!Has(Item))
            {
                CycleItem(1);
            }

            Fuse = Rules.ClampFuse(Rules.DefaultFuse);
        }

        /// <summary>
        /// cycle_weapon_right (<paramref name="direction"/> &gt; 0) or cycle_weapon_left (&lt; 0): selects the next weapon
        /// the player has any of. Returns true when the selection changed.
        /// </summary>
        public bool CycleWeapon(int direction)
        {
            int next = Next((int)Weapon, direction, _weapons);
            bool changed = next != (int)Weapon;
            Weapon = (Weapon)next;
            return changed;
        }

        /// <summary>
        /// obj_icon_weapon_*'s Left Pressed (U.RICO-T49): selects <paramref name="weapon"/> when the player has any of it
        /// (the original's sfx_flick), and refuses otherwise (sfx_disabled). Returns false when refused.
        /// </summary>
        public bool Select(Weapon weapon)
        {
            if ((int)weapon < 0 || (int)weapon >= ArsenalRules.Kinds || !Has(weapon))
            {
                return false;
            }

            Weapon = weapon;
            return true;
        }

        /// <summary>obj_item_*'s Left Pressed (U.RICO-T49): selects <paramref name="item"/> when the player has any of it.</summary>
        public bool Select(Item item)
        {
            if ((int)item < 0 || (int)item >= ArsenalRules.Kinds || !Has(item))
            {
                return false;
            }

            Item = item;
            return true;
        }

        /// <summary>cycle_item_right / cycle_item_left: selects the next item the player has any of.</summary>
        public bool CycleItem(int direction)
        {
            int next = Next((int)Item, direction, _items);
            bool changed = next != (int)Item;
            Item = (Item)next;
            return changed;
        }

        /// <summary>
        /// fire_weapon.gml: the selected weapon is fired if the player has one, spending it unless unlimited; spending the
        /// last moves the selection right. Returns the weapon fired, or null (nothing spent) when there is none.
        /// </summary>
        public Weapon? Fire()
        {
            Weapon fired = Weapon;
            if (!Spend(_weapons, (int)fired))
            {
                return null;
            }

            if (!Has(fired))
            {
                CycleWeapon(1);
            }

            return fired;
        }

        /// <summary>use_item.gml: the selected item is used if the player has one; as <see cref="Fire"/>.</summary>
        public Item? Use()
        {
            Item used = Item;
            if (!Spend(_items, (int)used))
            {
                return null;
            }

            if (!Has(used))
            {
                CycleItem(1);
            }

            return used;
        }

        /// <summary>add_random_weapon.gml's "+= amount", up to <see cref="ArsenalRules.MaxCount"/> (unlimited stays so).</summary>
        public void Add(Weapon weapon, int amount = 1) => Add(_weapons, (int)weapon, amount);

        public void Add(Item item, int amount = 1) => Add(_items, (int)item, amount);

        /// <summary>Key 1: half a second shorter, no shorter than the minimum.</summary>
        public void ShortenFuse() => Fuse = Rules.ClampFuse(Fuse - Rules.FuseStep);

        /// <summary>Key 2: half a second longer, no longer than the maximum.</summary>
        public void LengthenFuse() => Fuse = Rules.ClampFuse(Fuse + Rules.FuseStep);

        /// <summary>Key 3: the longest fuse.</summary>
        public void MaxOutFuse() => Fuse = Rules.ClampFuse(Rules.MaxFuse);

        /// <summary>Sets the fuse (clamped and snapped to the rules' steps).</summary>
        public void SetFuse(double seconds) => Fuse = Rules.ClampFuse(seconds);

        /// <summary>The weapon counts, the selected weapon and the fuse, in one int.</summary>
        public int PackWeapons()
        {
            int packed = PackCounts(_weapons) | ((int)Weapon & SelectedMask) << SelectedShift;
            int halfSeconds = Math.Max(1, Math.Min(FuseMask, (int)Math.Round(Fuse * 2)));
            return packed | halfSeconds << FuseShift;
        }

        /// <summary>The item counts and the selected item, in one int.</summary>
        public int PackItems()
        {
            return PackCounts(_items) | ((int)Item & SelectedMask) << SelectedShift;
        }

        /// <summary>A loadout read back from <see cref="PackWeapons"/> and <see cref="PackItems"/> (what a client shows).</summary>
        public static Loadout Unpack(int weapons, int items, ArsenalRules rules = null)
        {
            var loadout = new Loadout(rules);
            loadout.UnpackFrom(weapons, items);
            return loadout;
        }

        /// <summary>Overwrites this loadout with the packed one.</summary>
        public void UnpackFrom(int weapons, int items)
        {
            UnpackCounts(weapons, _weapons);
            UnpackCounts(items, _items);
            Weapon = (Weapon)Math.Min(ArsenalRules.Kinds - 1, (weapons >> SelectedShift) & SelectedMask);
            Item = (Item)Math.Min(ArsenalRules.Kinds - 1, (items >> SelectedShift) & SelectedMask);
            int halfSeconds = (weapons >> FuseShift) & FuseMask;
            Fuse = halfSeconds == 0 ? Rules.ClampFuse(Rules.DefaultFuse) : halfSeconds / 2.0;
        }

        private static int Next(int current, int direction, int[] counts)
        {
            if (direction == 0)
            {
                return current;
            }

            int step = direction > 0 ? 1 : -1;
            int n = counts.Length;
            for (int i = 1; i < n; i++)
            {
                int candidate = ((current + step * i) % n + n) % n;
                if (counts[candidate] > 0)
                {
                    return candidate;
                }
            }

            return current;
        }

        private static bool Spend(int[] counts, int index)
        {
            if (counts[index] <= 0)
            {
                return false;
            }

            if (counts[index] < ArsenalRules.Unlimited)
            {
                counts[index]--;
            }

            return true;
        }

        private static void Add(int[] counts, int index, int amount)
        {
            if (counts[index] >= ArsenalRules.Unlimited || amount <= 0)
            {
                return;
            }

            counts[index] = Math.Min(ArsenalRules.MaxCount, counts[index] + amount);
        }

        private static int PackCounts(int[] counts)
        {
            int packed = 0;
            for (int i = 0; i < counts.Length; i++)
            {
                packed |= (ArsenalRules.ClampCount(counts[i]) & CountMask) << (i * CountBits);
            }

            return packed;
        }

        private static void UnpackCounts(int packed, int[] counts)
        {
            for (int i = 0; i < counts.Length; i++)
            {
                counts[i] = ArsenalRules.ClampCount((packed >> (i * CountBits)) & CountMask);
            }
        }
    }
}
