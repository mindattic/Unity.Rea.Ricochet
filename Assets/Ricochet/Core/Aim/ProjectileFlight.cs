// A fired shot in the air: walks along its solved ShotPath at the rules' speed. The server runs one to know when the
// impact lands; every client runs its own from the same inputs to draw the projectile, so nothing per-frame goes over
// the wire. bullet_step.gml moved the bullet by speed every step and tested what it touched; the path is already
// solved here, so a step only has to move along it.
using System;

namespace Ricochet.Core.Aim
{
    public sealed class ProjectileFlight
    {
        private readonly double _speed;

        public ProjectileFlight(ShotPath path, double speed)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            if (speed <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(speed), "A shot needs a positive speed.");
            }

            _speed = speed;
            Position = path.Start;
            Heading = path.Heading;
        }

        public ProjectileFlight(ShotPath path, ShotRules rules)
            : this(path, (rules ?? ShotRules.Default).Speed)
        {
        }

        public ShotPath Path { get; }

        /// <summary>Distance flown from the muzzle.</summary>
        public double Travelled { get; private set; }

        public Vec2 Position { get; private set; }

        /// <summary>The direction of travel, in degrees.</summary>
        public double Heading { get; private set; }

        /// <summary>Contacts passed so far (the HUD's running "Bounces: n").</summary>
        public int Contacts { get; private set; }

        /// <summary>True once the shot has reached its impact point.</summary>
        public bool Landed { get; private set; }

        /// <summary>Seconds the whole flight takes.</summary>
        public double Duration => Path.Length / _speed;

        /// <summary>
        /// Moves the shot on by <paramref name="seconds"/>. Returns how many contacts it passed in this step (for a bounce
        /// sound or puff each); <see cref="Landed"/> turns true on the step it reaches the impact.
        /// </summary>
        public int Advance(double seconds)
        {
            if (Landed || seconds <= 0)
            {
                return 0;
            }

            Travelled = Math.Min(Path.Length, Travelled + seconds * _speed);
            Position = Path.PositionAt(Travelled, out double heading);
            Heading = heading;
            int before = Contacts;
            Contacts = Path.ContactsBefore(Travelled);
            if (Travelled >= Path.Length)
            {
                Landed = true;
                Position = Path.Impact;
            }

            return Contacts - before;
        }
    }
}
