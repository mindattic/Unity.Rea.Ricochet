// How the original's items look in flight and the endgame room's camera flashes (U.RICO-T79, docs/BIBLE.md "Item flights
// and endgame flashes"), step by GameMaker step, engine-free so the Core tests can hold it to the original. The
// presentation layer (ItemFlights, EndgameFlashes) runs these rules at the room speed (60 steps a second); an item flies
// its path at its multiple of the rebuild's shot speed, as the original's flew at a multiple of bulletSpeed.
//   obj_teleporter     shoot_teleporter: spr_teleporter (six frames at image_speed 1) at bulletSpeed * 2, image_angle set
//                      to the aim once; teleport_player: ef_ring (small, white) where the shooter stood, then ef_ring (medium) in
//                      white and in the shooter's colour and ef_firework (small, white) where the shooter lands.
//   obj_shrinkray      shoot_shrinkray: at bulletSpeed * 4, an ef_spark (small, lime) every step; striking a player,
//                      ef_firework (medium, lime). Its spr_shrinkball (turning 20 degrees a step) is never drawn: its Draw
//                      event is "//draw_self();", and obj_shrinkray_child is never created (commented out of shoot_shrinkray).
//   obj_redirector     shoot_redirector / redirector_step's "flying" mode: spr_redirector_arrow at bulletSpeed * 2 from
//                      0.15 of its size growing 0.01 a step to 0.30, turning 3 degrees a step, turned back to its heading
//                      at every bounce; where it stops it becomes the standing redirector (WeaponLooks.RedirectorScaleAt).
//   obj_flashemitter   the endgame room: an obj_flash every step at a random spot in the 1920x1080 room, gone at once when
//                      it falls in the lower half's middle third; each plays spr_flash's six frames once (image_speed 1).
using System;
using Ricochet.Core.Arsenal;

namespace Ricochet.Core.Art
{
    public static class ItemLooks
    {
        // ------------------------------------------------------------------ the flights

        /// <summary>shoot_teleporter: speed = bulletSpeed * 2.</summary>
        public const double TeleporterSpeedFactor = 2;

        /// <summary>shoot_shrinkray: speed = bulletSpeed * 4.</summary>
        public const double ShrinkRaySpeedFactor = 4;

        /// <summary>shoot_redirector: speed = bulletSpeed * 2.</summary>
        public const double RedirectorSpeedFactor = 2;

        /// <summary>
        /// How much flight an item's path is solved for (U.RICO-T80). The original's redirector flew on until its owner pressed
        /// the item key again or the turn ended (next_player destroys it); a flight has to be a path of some length to cross
        /// the wire, so it is solved for the longest turn there is (MatchSetup.MaxTurnSeconds), and one that runs out unstopped
        /// is gone with nothing placed, as when its turn ends.
        /// </summary>
        public const double RedirectorMaxSeconds = 60;

        /// <summary>obj_teleporter shows spr_teleporter at GameMaker's default image_speed.</summary>
        public const double TeleporterImageSpeed = 1;

        /// <summary>globals.gml's lime, make_color_rgb(128, 255, 128): the shrink ray's sparks and firework (8454016).</summary>
        public static readonly GmlColor Lime = new GmlColor(128, 255, 128);

        /// <summary>obj_shrinkray's Step: action_effect(ef_spark, x, y, small, lime) every step.</summary>
        public const int ShrinkSparkSize = 0;

        /// <summary>obj_shrinkray striking a player: effect_create_above(ef_firework, x, y, 1, lime).</summary>
        public const int ShrinkHitFireworkSize = 1;

        /// <summary>obj_redirector's Create: image_xscale = 0.15.</summary>
        public const double RedirectorFlightStartScale = 0.15;

        /// <summary>redirector_step, flying: image_xscale += 0.01 a step while under 0.30.</summary>
        public const double RedirectorFlightGrowPerStep = 0.01;

        public const double RedirectorFlightFullScale = 0.3;

        /// <summary>redirector_step, flying: image_angle += 3 a step (set back to its direction at each bounce).</summary>
        public const double RedirectorFlightSpinPerStep = 3;

        /// <summary>teleport_player: ef_ring at size 0 where the shooter stood.</summary>
        public const int TeleportLeaveRingSize = 0;

        /// <summary>teleport_player: ef_ring at size 1, in white and in the shooter's colour, where the shooter lands.</summary>
        public const int TeleportArriveRingSize = 1;

        /// <summary>teleport_player: ef_firework at size 0, white, where the shooter lands.</summary>
        public const int TeleportArriveFireworkSize = 0;

        /// <summary>True for the items the original sent flying (a teleporter, a shrink ray, a redirector).</summary>
        public static bool Flies(Item item) => item == Item.Teleporter || item == Item.ShrinkRay || item == Item.Redirector;

        /// <summary>The multiple of bulletSpeed <paramref name="item"/> flies at (0 for one that does not fly).</summary>
        public static double SpeedFactor(Item item)
        {
            switch (item)
            {
                case Item.Teleporter: return TeleporterSpeedFactor;
                case Item.ShrinkRay: return ShrinkRaySpeedFactor;
                case Item.Redirector: return RedirectorSpeedFactor;
                default: return 0;
            }
        }

        /// <summary>
        /// Seconds <paramref name="item"/> takes to fly a path <paramref name="length"/> long when a shell flies
        /// <paramref name="shotSpeed"/> units a second (0 for one that does not fly).
        /// </summary>
        public static double FlightSeconds(Item item, double length, double shotSpeed)
        {
            double speed = SpeedFactor(item) * shotSpeed;
            return speed > 0 && length > 0 ? length / speed : 0;
        }

        /// <summary>The flying redirector's size <paramref name="steps"/> after it set off: 0.15 growing 0.01 a step to 0.30.</summary>
        public static double RedirectorFlightScaleAt(double steps)
        {
            return Math.Min(RedirectorFlightFullScale, RedirectorFlightStartScale + RedirectorFlightGrowPerStep * Math.Max(0, steps));
        }

        /// <summary>
        /// The flying redirector's image_angle: the heading it took at its last bounce (or set off along),
        /// <paramref name="segmentHeading"/>, turned 3 degrees for every step since, <paramref name="stepsSinceBounce"/>.
        /// </summary>
        public static double RedirectorFlightAngle(double segmentHeading, double stepsSinceBounce)
        {
            return segmentHeading + RedirectorFlightSpinPerStep * Math.Max(0, stepsSinceBounce);
        }

        // ------------------------------------------------------------------ the endgame room's camera flashes

        /// <summary>The endgame room's size, in pixels.</summary>
        public const int RoomWidth = 1920;

        public const int RoomHeight = 1080;

        /// <summary>obj_flashemitter's Step: one obj_flash a step.</summary>
        public const int FlashesPerStep = 1;

        /// <summary>obj_flash shows spr_flash at GameMaker's default image_speed, destroyed at its Animation End.</summary>
        public const double FlashImageSpeed = 1;

        /// <summary>
        /// obj_flash's Create: a flash at (<paramref name="x"/>, <paramref name="y"/>) (room pixels, y growing downward)
        /// stays unless it is in the lower half and the middle third across (the pedestal), where it is destroyed at once.
        /// </summary>
        public static bool FlashKept(int x, int y)
        {
            bool lowerHalf = y > RoomHeight / 2.0;
            bool middleThird = x > RoomWidth / 3.0 && x < RoomWidth - RoomWidth / 3.0;
            return !(lowerHalf && middleThird);
        }

        /// <summary>
        /// One step of obj_flashemitter: a flash at irandom_range(0, room_width), irandom_range(0, room_height). True, with
        /// the spot, when it stays; false when it fell where <see cref="FlashKept"/> destroys it.
        /// </summary>
        public static bool NextFlash(Random dice, out int x, out int y)
        {
            dice = dice ?? new Random();
            x = dice.Next(0, RoomWidth + 1);
            y = dice.Next(0, RoomHeight + 1);
            return FlashKept(x, y);
        }
    }
}
