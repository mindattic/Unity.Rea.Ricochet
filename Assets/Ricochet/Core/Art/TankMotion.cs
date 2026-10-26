// How the original's tank looks while it drives (U.RICO-T45, docs/BIBLE.md "Tanks"), step by GameMaker step, engine-free
// so the Core tests can hold it to the original: align_bottom_to_top.gml turns the hull (obj_bottom, spr_tank_bottom) a
// degree a step toward the turret (obj_player, spr_tank_top) while a drive key is held, spawn_tracks.gml lays a
// spr_tracks print under the hull every five steps of driving (the first ten steps after the tank appears), and each
// obj_tracks print holds at alpha 0.7 for its 400-step alarm, then loses 0.003 a step until it is gone.
using System;

namespace Ricochet.Core.Art
{
    public static class TankMotion
    {
        /// <summary>Degrees align_bottom_to_top turns the hull per step.</summary>
        public const double HullTurnPerStep = 1;

        /// <summary>obj_bottom's Create: trackTime = 10, the steps of driving before the first print.</summary>
        public const int FirstTrackSteps = 10;

        /// <summary>spawn_tracks: trackTime = 5 after each print.</summary>
        public const int TrackEverySteps = 5;

        /// <summary>obj_tracks' Create: image_alpha = 0.7.</summary>
        public const double TrackAlpha = 0.7;

        /// <summary>obj_tracks' alarm[0] = 400: the steps a print holds before it starts to fade.</summary>
        public const int TrackHoldSteps = 400;

        /// <summary>obj_tracks' Step once fading: image_alpha -= 0.003.</summary>
        public const double TrackFadePerStep = 0.003;

        /// <summary>
        /// One step of align_bottom_to_top.gml: the hull's angle <paramref name="hull"/> a degree nearer the turret's
        /// <paramref name="turret"/> the short way round (the long way when they are exactly 180 apart, as the original's
        /// comparisons fall), wrapped into [0, 360); unchanged once they agree.
        /// </summary>
        public static double TurnHull(double hull, double turret)
        {
            double t = Wrap(turret);
            double b = Wrap(hull);
            if (t > b)
            {
                b += t - b < 180 ? HullTurnPerStep : -HullTurnPerStep;
            }
            else if (b > t)
            {
                b += b - t < 180 ? -HullTurnPerStep : HullTurnPerStep;
            }

            return Wrap(b);
        }

        /// <summary><paramref name="degrees"/> in [0, 360).</summary>
        public static double Wrap(double degrees)
        {
            if (double.IsNaN(degrees) || double.IsInfinity(degrees))
            {
                return 0;
            }

            double wrapped = degrees % 360;
            return wrapped < 0 ? wrapped + 360 : wrapped;
        }

        /// <summary>
        /// A print's alpha <paramref name="steps"/> after it was laid: 0.7 through its alarm, then 0.003 less a step, and
        /// 0 once it is gone (obj_tracks destroys itself the step after its alpha reaches 0 or below).
        /// </summary>
        public static double TrackAlphaAt(double steps)
        {
            if (steps <= TrackHoldSteps)
            {
                return TrackAlpha;
            }

            return Math.Max(0, TrackAlpha - TrackFadePerStep * (steps - TrackHoldSteps));
        }

        /// <summary>The steps a print lasts from being laid to being destroyed.</summary>
        public static int TrackLifeSteps => TrackHoldSteps + (int)Math.Ceiling(TrackAlpha / TrackFadePerStep - 1e-9) + 1;
    }

    /// <summary>
    /// spawn_tracks.gml's trackTime for one tank: counted down only on steps the tank is driving, a print laid when it
    /// runs out and the count set back to five. It is never reset when the tank stops, so a tank that drove three steps
    /// lays its next print two steps into its next drive.
    /// </summary>
    public sealed class TrackLayer
    {
        public int TrackTime { get; private set; } = TankMotion.FirstTrackSteps;

        /// <summary>One step; true when a print is laid under the hull this step.</summary>
        public bool Step(bool driving)
        {
            if (!driving)
            {
                return false;
            }

            TrackTime -= 1;
            if (TrackTime > 0)
            {
                return false;
            }

            TrackTime = TankMotion.TrackEverySteps;
            return true;
        }
    }
}
