// The numbers a shot plays by. Defaults follow the 2013 original where it had one: maxBounces = 5 (globals.gml), a
// shot that detonates on the contact that reaches the limit (bullet_step.gml), and a muzzle a little way out from the
// player along the aim (shoot_bullet.gml's Len = 40 px). There is no power: bulletSpeed was a constant and a shot flies
// until its bounces run out, however far that is (U.RICO-T88).

namespace Ricochet.Core.Aim
{
    public sealed class ShotRules
    {
        /// <summary>Contacts (border, wall or redirector) a shot survives is MaxBounces - 1; it detonates on the last.</summary>
        public int MaxBounces { get; set; }

        /// <summary>How far ahead of the shooter's centre the shot starts.</summary>
        public double MuzzleOffset { get; set; }

        /// <summary>Flight speed in units a second (for <see cref="ProjectileFlight"/>; the path does not depend on it).</summary>
        public double Speed { get; set; }

        /// <summary>Degrees the aim turns per second while a turn key is held.</summary>
        public double DegreesPerSecond { get; set; }

        /// <summary>Units a second a tank drives while W or S is held (the original's moveSpeed, one pixel a step).</summary>
        public double DriveSpeed { get; set; }

        /// <summary>The stock rules, from shot.json (UR-T35): a new set starts as a copy of them.</summary>
        public ShotRules()
        {
            CopyFrom(Ricochet.Core.Data.GameData.Current.Shot);
        }

        internal ShotRules(bool blank)
        {
        }

        public static ShotRules Default => new ShotRules();

        internal void CopyFrom(ShotRules other)
        {
            MaxBounces = other.MaxBounces;
            MuzzleOffset = other.MuzzleOffset;
            Speed = other.Speed;
            DegreesPerSecond = other.DegreesPerSecond;
            DriveSpeed = other.DriveSpeed;
        }

        /// <summary>A copy, to change for one shot (an extra bounce, a grenade's fuse) without touching the shared rules.</summary>
        public ShotRules Clone()
        {
            return (ShotRules)MemberwiseClone();
        }
    }
}
