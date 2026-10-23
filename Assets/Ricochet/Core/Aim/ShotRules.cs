// The numbers a shot plays by. Defaults follow the 2013 original where it had one: maxBounces = 5 (globals.gml), a
// shot that detonates on the contact that reaches the limit (bullet_step.gml), and a muzzle a little way out from the
// player along the aim (shoot_bullet.gml's Len = 40 px). The original had no power meter (bulletSpeed was a constant
// and a shot flew until its bounces ran out); here power is the shot's range, so a soft shot dies in mid-air.
using System;

namespace Ricochet.Core.Aim
{
    public sealed class ShotRules
    {
        /// <summary>Contacts (border, wall or redirector) a shot survives is MaxBounces - 1; it detonates on the last.</summary>
        public int MaxBounces { get; set; } = 5;

        /// <summary>How far ahead of the shooter's centre the shot starts.</summary>
        public double MuzzleOffset { get; set; } = 0.6;

        /// <summary>The distance a shot fired at power 0 travels.</summary>
        public double MinRange { get; set; } = 6;

        /// <summary>The distance a shot fired at power 1 travels.</summary>
        public double MaxRange { get; set; } = 70;

        /// <summary>Flight speed in units a second (for <see cref="ProjectileFlight"/>; the path does not depend on it).</summary>
        public double Speed { get; set; } = 18;

        /// <summary>Power steps the aim moves through per second while the power key is held.</summary>
        public double PowerPerSecond { get; set; } = 0.5;

        /// <summary>Degrees the aim turns per second while a turn key is held.</summary>
        public double DegreesPerSecond { get; set; } = 90;

        /// <summary>Units a second a tank drives while W or S is held (the original's moveSpeed, one pixel a step).</summary>
        public double DriveSpeed { get; set; } = 3;

        public static ShotRules Default => new ShotRules();

        /// <summary>A copy, to change for one shot (an extra bounce, a grenade's fuse) without touching the shared rules.</summary>
        public ShotRules Clone()
        {
            return (ShotRules)MemberwiseClone();
        }

        /// <summary>The range a shot of <paramref name="power"/> (clamped to 0..1) travels.</summary>
        public double RangeFor(double power)
        {
            return MinRange + ClampPower(power) * (MaxRange - MinRange);
        }

        /// <summary><paramref name="power"/> clamped to 0..1 (NaN counts as 0: a client cannot send garbage through).</summary>
        public static double ClampPower(double power)
        {
            return double.IsNaN(power) ? 0 : Math.Max(0, Math.Min(1, power));
        }
    }
}
