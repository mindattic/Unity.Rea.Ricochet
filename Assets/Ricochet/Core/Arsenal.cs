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

namespace Ricochet.Core
{
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

        private readonly int[] _startWeapons = { Unlimited, 3, 3, 3, 3, 3 };
        private readonly int[] _startItems = { 3, 3, 3, 3, 3, 3 };
        private readonly bool[] _weaponsInPlay = { true, true, true, true, true, true };
        private readonly bool[] _itemsInPlay = { true, true, true, true, true, true };

        /// <summary>A grenade's fuse at the start, in seconds (player.fuseTime = 2).</summary>
        public double DefaultFuse { get; set; } = 2;

        /// <summary>The shortest fuse (key 1 stops at 0.5).</summary>
        public double MinFuse { get; set; } = 0.5;

        /// <summary>The longest fuse (maxFuse = 6).</summary>
        public double MaxFuse { get; set; } = 6;

        /// <summary>One press of the fuse keys (fuseIncrement = 0.5).</summary>
        public double FuseStep { get; set; } = 0.5;

        /// <summary>A grenade's blast radius (grenadeRadius = 150 px; the original's room was about 58 px a unit).</summary>
        public double GrenadeRadius { get; set; } = 2.5;

        /// <summary>The vortex's pull radius (vortexRadius = 200 px).</summary>
        public double VortexRadius { get; set; } = 3.4;

        /// <summary>Shotgun pellets, fanned evenly (pelletCount was 12 at random angles; five, one along the aim, read better).</summary>
        public int Pellets { get; set; } = 5;

        /// <summary>Degrees either side of the aim the pellets fan across (shotgunSpread = 4).</summary>
        public double PelletSpread { get; set; } = 4;

        /// <summary>The share of a hit's damage one pellet does.</summary>
        public double PelletDamage { get; set; } = 0.35;

        /// <summary>The laser flies this many times a shell's speed.</summary>
        public double LaserSpeed { get; set; } = 3;

        /// <summary>The railgun's slug flies this many times a shell's speed (near enough instant).</summary>
        public double RailgunSpeed { get; set; } = 8;

        /// <summary>What a shrink ray leaves of the player it hits (their hit radius and size), for the rest of the round.</summary>
        public double ShrinkScale { get; set; } = 0.6;

        public static ArsenalRules Default => new ArsenalRules();

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

        /// <summary>draw_weaponinfo.gml's names.</summary>
        public static string Name(Weapon weapon)
        {
            switch (weapon)
            {
                case Weapon.Cannon: return "Cannon";
                case Weapon.Laser: return "Laser";
                case Weapon.Shotgun: return "Shotgun";
                case Weapon.Grenade: return "Grenade";
                case Weapon.Railgun: return "Railgun";
                case Weapon.Vortex: return "Vortex Device";
                default: return weapon.ToString();
            }
        }

        /// <summary>draw_iteminfo.gml's names.</summary>
        public static string Name(Item item)
        {
            switch (item)
            {
                case Item.ExtraBounce: return "Extra Bounce";
                case Item.ShieldWall: return "Shield Wall";
                case Item.ShrinkRay: return "Shrink Ray";
                case Item.Teleporter: return "Teleporter";
                case Item.AimAssist: return "Aim Assist";
                case Item.Redirector: return "Redirector";
                default: return item.ToString();
            }
        }

        /// <summary>"x3", or "unlimited" (the original drew an infinity sign; the default HUD font may lack the glyph).</summary>
        public static string CountLabel(int count) => count >= Unlimited ? "unlimited" : "x" + count.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
