// A player's aim: an angle and a power, turned and charged while keys are held. aim.gml swapped the player into an
// "aim" action and rotated image_angle; the power meter is new (see ShotRules). Plain so the input rules are testable.
namespace Ricochet.Core
{
    public sealed class AimState
    {
        public AimState(double heading = 90, double power = 0.5)
        {
            Heading = Angles.Normalize(heading);
            Power = ShotRules.ClampPower(power);
        }

        /// <summary>Degrees, 0 = right, 90 = up.</summary>
        public double Heading { get; private set; }

        /// <summary>0..1.</summary>
        public double Power { get; private set; }

        /// <summary>
        /// Applies held input for <paramref name="seconds"/>: <paramref name="turn"/> +1 turns counterclockwise (left),
        /// -1 clockwise; <paramref name="charge"/> +1 raises power, -1 lowers it. Returns true when the aim changed.
        /// </summary>
        public bool Step(double turn, double charge, double seconds, ShotRules rules = null)
        {
            rules = rules ?? ShotRules.Default;
            double heading = Angles.Normalize(Heading + Clamp1(turn) * rules.DegreesPerSecond * seconds);
            double power = ShotRules.ClampPower(Power + Clamp1(charge) * rules.PowerPerSecond * seconds);
            bool changed = heading != Heading || power != Power;
            Heading = heading;
            Power = power;
            return changed;
        }

        public void Set(double heading, double power)
        {
            Heading = Angles.Normalize(heading);
            Power = ShotRules.ClampPower(power);
        }

        private static double Clamp1(double value)
        {
            return double.IsNaN(value) ? 0 : value < -1 ? -1 : value > 1 ? 1 : value;
        }
    }
}
