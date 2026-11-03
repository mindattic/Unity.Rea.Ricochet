// The numbers a shot plays by. Defaults follow the 2013 original where it had one: maxBounces = 5 (globals.gml), a
// shot that detonates on the contact that reaches the limit (bullet_step.gml), and a muzzle a little way out from the
// player along the aim (shoot_bullet.gml's Len = 40 px). There is no power: bulletSpeed was a constant and a shot flies
// until its bounces run out, however far that is (U.RICO-T88).
//
// U.RICO-T89 spike, never the default: shot.json's powerMeter (false as shipped) brings back the rebuild's power meter, a
// held key charging the shot's power from 0 to 1 at powerPerSecond (AimState.Charge) and the power setting its range,
// minRange at none to maxRange at full. Compared with the original's constant-speed shot: the speed is the same either way
// (the meter sets how far, never how fast), but with the meter a shell can die in mid-air short of its bounces (Spent), so a
// long bank shot needs a full charge and a short one can be pulled up before a wall it would otherwise reach; the original
// asks only for an angle, so every shot is a full-length bank shot and the preview always shows where it ends. The meter adds
// a second thing to judge each turn and makes the cheap long ricochet harder; the original stays the default (BIBLE).

namespace Rea.Ricochet.Core.Aim
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

        /// <summary>U.RICO-T89 spike (shot.json powerMeter, never the default): the held power sets a shell's range.</summary>
        public bool PowerMeter { get; set; }

        /// <summary>With the power meter: how far a shell flies at no power.</summary>
        public double MinRange { get; set; }

        /// <summary>With the power meter: how far a shell flies at full power.</summary>
        public double MaxRange { get; set; }

        /// <summary>With the power meter: how much of the meter (0 to 1) a second of holding the key fills.</summary>
        public double PowerPerSecond { get; set; }

        /// <summary>This shot's power, 0 to 1 (full unless the power meter set it; ignored without the meter).</summary>
        public double Power { get; set; } = 1;

        /// <summary>
        /// How far a shell flies: no limit (the original, until its bounces run out) unless the power meter is on, then
        /// <see cref="MinRange"/> at no power to <see cref="MaxRange"/> at full.
        /// </summary>
        public double Range
        {
            get
            {
                if (!PowerMeter)
                {
                    return double.PositiveInfinity;
                }

                double power = double.IsNaN(Power) ? 0 : Power < 0 ? 0 : Power > 1 ? 1 : Power;
                return MinRange + power * (MaxRange - MinRange);
            }
        }

        /// <summary>The stock rules, from shot.json (UR-T35): a new set starts as a copy of them.</summary>
        public ShotRules()
        {
            CopyFrom(Rea.Ricochet.Core.Data.GameData.Current.Shot);
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
            PowerMeter = other.PowerMeter;
            MinRange = other.MinRange;
            MaxRange = other.MaxRange;
            PowerPerSecond = other.PowerPerSecond;
            Power = other.Power;
        }

        /// <summary>A copy, to change for one shot (an extra bounce, a grenade's fuse) without touching the shared rules.</summary>
        public ShotRules Clone()
        {
            return (ShotRules)MemberwiseClone();
        }
    }
}
