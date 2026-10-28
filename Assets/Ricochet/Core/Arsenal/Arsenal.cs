// The weapons and items a player carries, and the numbers they play by. The 2013 original (design reference only,
// UR-LAW-2) numbered its six weapons and six items in globals.gml (weapon_cannon = 0 .. weapon_vortex = 5,
// item_extrabounce = 0 .. item_redirector = 5), gave every player count_X of each at the start (3 by default), treated a
// count of 10 as unlimited (fire_weapon.gml and use_item.gml only spend one "if count < 10"), set a grenade's fuse with
// keys 1-3 between 0.5 and 6 seconds in half-second steps (default 2), and named them in draw_weaponinfo.gml /
// draw_iteminfo.gml. What differs here: the cannon is unlimited by default, so a player who has spent everything else
// can still take their turn (the original's empty-handed player could only let the clock run out). Aim assist (UR-T7)
// starts at the original's count_AimAssist of 3; a match's setup (MatchSetup) changes that, or takes it out of play. The
// vortex, the shield wall and the redirector came into play with the level hazards (UR-T6).
using System;

namespace Ricochet.Core.Arsenal
{
    using Ricochet.Core.Level;
    using Ricochet.Core.Match;
    /// <summary>The six weapons, in the original's order (the order cycling walks).</summary>
    public enum Weapon
    {
        Cannon = 0,
        Laser = 1,
        Shotgun = 2,
        Grenade = 3,
        Railgun = 4,
        Vortex = 5,
    }

    /// <summary>The six items, in the original's order (the order cycling walks).</summary>
    public enum Item
    {
        ExtraBounce = 0,
        ShieldWall = 1,
        ShrinkRay = 2,
        Teleporter = 3,
        AimAssist = 4,
        Redirector = 5,
    }

    public sealed class ArsenalRules
    {
        /// <summary>Kinds of weapon (and of item).</summary>
        public const int Kinds = 6;

        /// <summary>A count of this means unlimited: firing or using it never spends one (the original's 10).</summary>
        public const int Unlimited = 10;

        /// <summary>The most a finite count can reach by picking up drops (one short of <see cref="Unlimited"/>).</summary>
        public const int MaxCount = 9;

        private readonly int[] _startWeapons = new int[Kinds];
        private readonly int[] _startItems = new int[Kinds];
        private readonly bool[] _weaponsInPlay = new bool[Kinds];
        private readonly bool[] _itemsInPlay = new bool[Kinds];

        /// <summary>A grenade's fuse at the start, in seconds (player.fuseTime = 2).</summary>
        public double DefaultFuse { get; set; }

        /// <summary>The shortest fuse (key 1 stops at 0.5).</summary>
        public double MinFuse { get; set; }

        /// <summary>The longest fuse (maxFuse = 6).</summary>
        public double MaxFuse { get; set; }

        /// <summary>One press of the fuse keys (fuseIncrement = 0.5).</summary>
        public double FuseStep { get; set; }

        /// <summary>A grenade's blast radius (grenadeRadius = 150 px; the original's room was about 58 px a unit).</summary>
        public double GrenadeRadius { get; set; }

        /// <summary>The vortex's pull radius (vortexRadius = 200 px).</summary>
        public double VortexRadius { get; set; }

        /// <summary>Shotgun pellets, fanned evenly (pelletCount was 12 at random angles; five, one along the aim, read better).</summary>
        public int Pellets { get; set; }

        /// <summary>Degrees either side of the aim the pellets fan across (shotgunSpread = 4).</summary>
        public double PelletSpread { get; set; }

        /// <summary>The share of a hit's damage one pellet does.</summary>
        public double PelletDamage { get; set; }

        /// <summary>The laser flies this many times a shell's speed (1 in the original).</summary>
        public double LaserSpeed { get; set; }

        /// <summary>The railgun's slug flies this many times a shell's speed (near enough instant).</summary>
        public double RailgunSpeed { get; set; }

        /// <summary>The stock rules, from arsenal.json (UR-T35): a new set starts as a copy of them.</summary>
        public ArsenalRules()
        {
            CopyFrom(Ricochet.Core.Data.GameData.Current.Arsenal);
        }

        internal ArsenalRules(bool blank)
        {
        }

        public static ArsenalRules Default => new ArsenalRules();

        internal void CopyFrom(ArsenalRules other)
        {
            DefaultFuse = other.DefaultFuse;
            MinFuse = other.MinFuse;
            MaxFuse = other.MaxFuse;
            FuseStep = other.FuseStep;
            GrenadeRadius = other.GrenadeRadius;
            VortexRadius = other.VortexRadius;
            Pellets = other.Pellets;
            PelletSpread = other.PelletSpread;
            PelletDamage = other.PelletDamage;
            LaserSpeed = other.LaserSpeed;
            RailgunSpeed = other.RailgunSpeed;
            Array.Copy(other._startWeapons, _startWeapons, Kinds);
            Array.Copy(other._startItems, _startItems, Kinds);
            Array.Copy(other._weaponsInPlay, _weaponsInPlay, Kinds);
            Array.Copy(other._itemsInPlay, _itemsInPlay, Kinds);
        }

        public int StartCount(Weapon weapon) => _startWeapons[(int)weapon];

        public int StartCount(Item item) => _startItems[(int)item];

        public void SetStartCount(Weapon weapon, int count) => _startWeapons[(int)weapon] = ClampCount(count);

        public void SetStartCount(Item item, int count) => _startItems[(int)item] = ClampCount(count);

        /// <summary>The original's hasCannon .. hasVortex: whether drops may give it (a weapon out of play never drops).</summary>
        public bool InPlay(Weapon weapon) => _weaponsInPlay[(int)weapon];

        public bool InPlay(Item item) => _itemsInPlay[(int)item];

        public void SetInPlay(Weapon weapon, bool inPlay) => _weaponsInPlay[(int)weapon] = inPlay;

        public void SetInPlay(Item item, bool inPlay) => _itemsInPlay[(int)item] = inPlay;

        /// <summary>Clamps a fuse to the allowed range and snaps it to the nearest step.</summary>
        public double ClampFuse(double seconds)
        {
            if (double.IsNaN(seconds))
            {
                seconds = DefaultFuse;
            }

            double clamped = Math.Max(MinFuse, Math.Min(MaxFuse, seconds));
            return FuseStep > 0 ? Math.Max(MinFuse, Math.Min(MaxFuse, Math.Round(clamped / FuseStep) * FuseStep)) : clamped;
        }

        public static int ClampCount(int count) => Math.Max(0, Math.Min(Unlimited, count));

        /// <summary>draw_weaponinfo.gml's names (arsenal.json).</summary>
        public static string Name(Weapon weapon)
        {
            int index = (int)weapon;
            var names = Ricochet.Core.Data.GameData.Current.WeaponNames;
            return index >= 0 && index < names.Count ? names[index] : weapon.ToString();
        }

        /// <summary>draw_iteminfo.gml's names (arsenal.json).</summary>
        public static string Name(Item item)
        {
            int index = (int)item;
            var names = Ricochet.Core.Data.GameData.Current.ItemNames;
            return index >= 0 && index < names.Count ? names[index] : item.ToString();
        }

        /// <summary>What <paramref name="weapon"/> does: its HUD icon's hover text (arsenal.json's about, U.RICO-T49).</summary>
        public static string About(Weapon weapon)
        {
            int index = (int)weapon;
            var about = Ricochet.Core.Data.GameData.Current.WeaponAbout;
            return about != null && index >= 0 && index < about.Count ? about[index] : string.Empty;
        }

        /// <summary>What <paramref name="item"/> does: its HUD icon's hover text (arsenal.json's about, U.RICO-T49).</summary>
        public static string About(Item item)
        {
            int index = (int)item;
            var about = Ricochet.Core.Data.GameData.Current.ItemAbout;
            return about != null && index >= 0 && index < about.Count ? about[index] : string.Empty;
        }

        /// <summary>"x3", or "unlimited" (the original drew an infinity sign; the default HUD font may lack the glyph).</summary>
        public static string CountLabel(int count) => count >= Unlimited ? "unlimited" : "x" + count.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
