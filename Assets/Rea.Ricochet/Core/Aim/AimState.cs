// A player's aim: the barrel's angle, turned while a key is held. aim.gml swapped the player into an "aim" action and
// rotated image_angle; there is no power (U.RICO-T88). Plain so the input rules are testable.
namespace Ricochet.Core.Aim
{
    public sealed class AimState
    {
        public AimState(double heading = 90)
        {
            Heading = Angles.Normalize(heading);
        }

        /// <summary>Degrees, 0 = right, 90 = up.</summary>
        public double Heading { get; private set; }

        /// <summary>
        /// Applies held input for <paramref name="seconds"/>: <paramref name="turn"/> +1 turns counterclockwise (left),
        /// -1 clockwise. Returns true when the aim changed.
        /// </summary>
        public bool Step(double turn, double seconds, ShotRules rules = null)
        {
            rules = rules ?? ShotRules.Default;
            double heading = Angles.Normalize(Heading + Clamp1(turn) * rules.DegreesPerSecond * seconds);
            bool changed = heading != Heading;
            Heading = heading;
            return changed;
        }

        public void Set(double heading)
        {
            Heading = Angles.Normalize(heading);
        }

        private static double Clamp1(double value)
        {
            return double.IsNaN(value) ? 0 : value < -1 ? -1 : value > 1 ? 1 : value;
        }
    }
}
