// How the original's weapons, items and hazards look (U.RICO-T47, docs/BIBLE.md "Weapon and hazard visuals"), step by
// GameMaker step, engine-free so the Core tests can hold it to the original. The presentation layer (ShotView, the
// weapon effects, LevelHazard, the preview) runs these rules at the room speed (ArtCatalogue.RoomSpeed, 60 steps a
// second), except the vortex's implosion, which runs at the shot's own step rate (ShotStepsPerSecond; see Vortex).
//   draw_dottedline / draw_beam  6 px dashes with 6 px gaps, a black 4 px dash under a white 2 px one.
//   laser_draw                   a 4 px laser_color line at 0.7 under a 1 px white one at 0.9, from the laser's origin to
//                                its head; once the last bounce is spent the origin runs after the head at five times its
//                                speed (laser_step's originX += dX * 5) until the beam is gone.
//   obj_laser_pulse              a spr_raingun_ring sent from the muzzle along the beam 40-60 steps in, then every 25-75
//                                (obj_laser's alarm[0]); once the beam is spent each fades 0.02 a step (pulse_step).
//   railgun_draw / obj_rail_swirl a 9 px railColor line at 0.1 and a 1 px one at railAlpha, a spr_railgun_swirl every
//                                56 px (generate_swirl's swirlIncrement) tinted railColor at railAlpha; railAlpha starts
//                                at 0.8 and loses 0.005 a step (railgun_step) until the rail is gone.
//   obj_grenade_explosion        spr_bigexplosion at three times its size, 70% opaque, under a white circle at 0.7 that
//                                grows 2 px a step until it reaches grenadeRadius, when it is all gone.
//   obj_vortex                   spr_vortex_device from a quarter of its size growing 0.01 a step in flight; set off, it
//                                becomes spr_vortex at 0.5 (and obj_vortex_child over it) and runs vortex_step's modes.
//   obj_boom_normal / obj_boom   spr_smallexplosion where a hit lands (hit.gml, image_speed 0.5) and where a vortex is set
//                                off (vortex_detonate.gml, image_speed 0.1).
//   obj_shieldwall               spr_shieldwall playing at image_speed 1, fading in 0.01 a step to 0.9.
//   obj_redirector               spr_redirector_arrow from 0.3 of its size growing 0.01 a step once it stops.
using System;
using System.Collections.Generic;
using Ricochet.Core.Level;

namespace Ricochet.Core.Art
{
    /// <summary>An opaque colour, as GameMaker's make_color_rgb.</summary>
    public readonly struct GmlColor
    {
        public GmlColor(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }

        public byte R { get; }

        public byte G { get; }

        public byte B { get; }
    }

    public static class WeaponLooks
    {
        /// <summary>globals.gml's bulletSpeed: pixels a shell (and the laser's head, and its pulses) moves a step.</summary>
        public const double BulletSpeedPixels = 3;

        // draw_beam.gml: draw_dottedline(x1, y1, x2, y2, 6, 4, c_black, c_black), then the same 2 px wide in c_white.

        /// <summary>draw_dottedline's dash length (and the gap after each dash), in pixels.</summary>
        public const double DashPixels = 6;

        /// <summary>draw_beam's black dashes' width, in pixels.</summary>
        public const double BeamOuterWidth = 4;

        /// <summary>draw_beam's white dashes' width, in pixels.</summary>
        public const double BeamInnerWidth = 2;

        // laser_draw.gml / obj_laser_connector's Draw.

        /// <summary>globals.gml's laser_color, make_color_rgb(255, 0, 63).</summary>
        public static readonly GmlColor LaserColor = new GmlColor(255, 0, 63);

        /// <summary>The laser's coloured line: 4 px wide at alpha 0.7.</summary>
        public const double LaserOuterWidth = 4;

        public const double LaserOuterAlpha = 0.7;

        /// <summary>The laser's white core: 1 px wide at alpha 0.9.</summary>
        public const double LaserInnerWidth = 1;

        public const double LaserInnerAlpha = 0.9;

        /// <summary>laser_step: once spent, the beam's origin runs after the head at five times its speed (dX * 5).</summary>
        public const double LaserTailSpeedFactor = 5;

        /// <summary>obj_laser's Create: alarm[0] = irandom_range(40, 60), the first pulse.</summary>
        public const int FirstPulseMin = 40;

        public const int FirstPulseMax = 60;

        /// <summary>obj_laser's Alarm 0: alarm[0] = irandom_range(25, 75), each pulse after.</summary>
        public const int NextPulseMin = 25;

        public const int NextPulseMax = 75;

        /// <summary>pulse_step: a pulse loses 0.02 of its alpha a step once the beam is spent.</summary>
        public const double PulseFadePerStep = 0.02;

        // railgun_draw.gml, shoot_railgun.gml, railgun_step.gml, generate_swirl.gml, swirl_create.gml.

        /// <summary>globals.gml's railColor, make_color_rgb(200, 255, 200).</summary>
        public static readonly GmlColor RailColor = new GmlColor(200, 255, 200);

        /// <summary>shoot_railgun: railAlpha = 0.8.</summary>
        public const double RailStartAlpha = 0.8;

        /// <summary>globals.gml's railFade: railAlpha loses this a step.</summary>
        public const double RailFadePerStep = 0.005;

        /// <summary>railgun_draw's wide line: 9 px at alpha 0.1.</summary>
        public const double RailGlowWidth = 9;

        public const double RailGlowAlpha = 0.1;

        /// <summary>railgun_draw's core line: 1 px at railAlpha.</summary>
        public const double RailCoreWidth = 1;

        /// <summary>globals.gml's swirlIncrement: a swirl every 56 px along the rail.</summary>
        public const double SwirlSpacing = 56;

        // obj_grenade_explosion.

        /// <summary>Its circle grows this many pixels a step (radius += 2) until it reaches grenadeRadius.</summary>
        public const double BlastRingGrowth = 2;

        /// <summary>The circle and the sprite are drawn at alpha 0.7.</summary>
        public const double BlastAlpha = 0.7;

        // obj_boom_normal (hit.gml) and obj_boom (vortex_detonate.gml): spr_smallexplosion played once.

        /// <summary>obj_boom_normal's image_speed: the flash where a hit lands.</summary>
        public const double HitFlashImageSpeed = 0.5;

        /// <summary>obj_boom's image_speed: the flash where a vortex is set off.</summary>
        public const double DetonateFlashImageSpeed = 0.1;

        // obj_shieldwall.

        /// <summary>obj_shieldwall's image_speed (1 at normalFPS).</summary>
        public const double ShieldWallImageSpeed = 1;

        /// <summary>It fades in from 0 by 0.01 a step...</summary>
        public const double ShieldWallFadePerStep = 0.01;

        /// <summary>...until it reaches 0.9.</summary>
        public const double ShieldWallAlpha = 0.9;

        // obj_redirector (redirector_detonate.gml, redirector_step.gml).

        /// <summary>redirector_detonate: once it stops, image_xscale = 0.30...</summary>
        public const double RedirectorStartScale = 0.3;

        /// <summary>...growing 0.01 a step to its full size.</summary>
        public const double RedirectorGrowPerStep = 0.01;

        /// <summary>
        /// The original's steps a second while a shot flies (its room_speed went to shootFPS, as fast as the machine
        /// could go): the steps a shell flying at <paramref name="shotSpeed"/> units a second takes to cover bulletSpeed's
        /// 3 px a step. The vortex's implosion runs at this rate, so it lasts as long beside the flight as it did then.
        /// </summary>
        public static double ShotStepsPerSecond(double shotSpeed)
        {
            return shotSpeed > 0 ? shotSpeed / (BulletSpeedPixels * ShapeLibrary.UnitsPerPixel) : 60;
        }

        /// <summary>
        /// draw_dottedline.gml's dashes along a line <paramref name="length"/> long: one <paramref name="dash"/> long at the
        /// start and after every gap as long, as (from, to) distances from the start. As the original's loop does, the
        /// last dash is drawn whole even where it runs past the end.
        /// </summary>
        public static List<(double From, double To)> Dashes(double length, double dash)
        {
            var dashes = new List<(double, double)>();
            if (!(length > 0) || !(dash > 0))
            {
                return dashes;
            }

            for (double m = 0; m < length; m += dash * 2)
            {
                dashes.Add((m, m + dash));
            }

            return dashes;
        }

        /// <summary>railAlpha <paramref name="steps"/> after the rail was fired (0 once it has faded out).</summary>
        public static double RailAlpha(double steps)
        {
            return Math.Max(0, RailStartAlpha - RailFadePerStep * Math.Max(0, steps));
        }

        /// <summary>Steps until the rail has faded out (railAlpha reaching 0: 160).</summary>
        public static double RailSteps => RailStartAlpha / RailFadePerStep;

        /// <summary>generate_swirl.gml: the distances along a rail <paramref name="length"/> pixels long that a swirl stands at.</summary>
        public static List<double> SwirlOffsets(double length)
        {
            var offsets = new List<double>();
            for (double i = 0; i < length; i += SwirlSpacing)
            {
                offsets.Add(i);
            }

            return offsets;
        }

        /// <summary>The steps until obj_laser's next pulse: 40-60 for the first, 25-75 after (irandom_range is inclusive).</summary>
        public static int PulseSteps(Random dice, bool first)
        {
            return first ? dice.Next(FirstPulseMin, FirstPulseMax + 1) : dice.Next(NextPulseMin, NextPulseMax + 1);
        }

        /// <summary>obj_grenade_explosion's circle <paramref name="steps"/> in, in pixels, up to <paramref name="radius"/>.</summary>
        public static double BlastRing(double steps, double radius)
        {
            return Math.Min(radius, BlastRingGrowth * Math.Max(0, steps));
        }

        /// <summary>Steps obj_grenade_explosion lasts: until its circle reaches <paramref name="radius"/> pixels.</summary>
        public static double BlastSteps(double radius) => Math.Max(0, radius) / BlastRingGrowth;

        /// <summary>A shield wall's alpha <paramref name="steps"/> after it was put up.</summary>
        public static double ShieldWallAlphaAt(double steps)
        {
            return Math.Min(ShieldWallAlpha, ShieldWallFadePerStep * Math.Max(0, steps));
        }

        /// <summary>A redirector's scale <paramref name="steps"/> after it stopped.</summary>
        public static double RedirectorScaleAt(double steps)
        {
            return Math.Min(1, RedirectorStartScale + RedirectorGrowPerStep * Math.Max(0, steps));
        }
    }

    /// <summary>vortex_step.gml's modes once a vortex is set off (or stops: the rebuild's vortex implodes there too).</summary>
    public enum VortexMode
    {
        /// <summary>spr_vortex grows from nothing by 0.002 a step to its full size.</summary>
        Sucking,

        /// <summary>alarm[0] = 300: it turns in place.</summary>
        Waiting,

        /// <summary>It shrinks 0.01 a step back to nothing while its ring spreads 10 px a step.</summary>
        Blowing,

        /// <summary>alarm[1] = 300: only the ring, still spreading.</summary>
        Waiting2,

        /// <summary>finishing: gone.</summary>
        Done,
    }

    /// <summary>
    /// obj_vortex flying (vortex_create.gml, vortex_step.gml's "flying"): spr_vortex_device at a quarter of its size, growing
    /// 0.01 a step to its full size while it flies; and, once set off, the implosion (vortex_step.gml's other modes,
    /// vortex_draw.gml, obj_vortex_child): spr_vortex at 0.5 turning a degree a step backwards, obj_vortex_child at its size
    /// over it turning 4 degrees a step forwards from 90, and burstAlpha's 0.7 white circle spreading 10 px a step from
    /// the moment it starts to blow.
    /// </summary>
    public sealed class Vortex
    {
        /// <summary>vortex_create: image_xscale = 0.25.</summary>
        public const double FlyingStartScale = 0.25;

        /// <summary>vortex_step "flying": image_xscale += 0.01 while under 1.</summary>
        public const double FlyingGrowPerStep = 0.01;

        /// <summary>"sucking": image_xscale += 0.002 while under 1.</summary>
        public const double SuckGrowPerStep = 0.002;

        /// <summary>alarm[0] and alarm[1]: the two waits.</summary>
        public const int WaitSteps = 300;

        /// <summary>"blowing": image_xscale -= 0.01 while over 0.</summary>
        public const double BlowShrinkPerStep = 0.01;

        /// <summary>"blowing" and "waiting2": radius += 10.</summary>
        public const double BurstGrowth = 10;

        /// <summary>vortex_create's burstAlpha: the circle's alpha.</summary>
        public const double BurstAlpha = 0.7;

        /// <summary>"sucking": image_alpha = 0.5 (obj_vortex_child's too).</summary>
        public const double ImplodedAlpha = 0.5;

        /// <summary>obj_vortex_child's Create: image_angle = 90.</summary>
        public const double ChildStartAngle = 90;

        /// <summary>obj_vortex_child's Step: image_angle += 4.</summary>
        public const double ChildTurnPerStep = 4;

        private int _wait;

        /// <summary>A vortex just thrown: in flight, at a quarter of its size.</summary>
        public Vortex()
        {
            Scale = FlyingStartScale;
            Flying = true;
        }

        /// <summary>True until it is set off.</summary>
        public bool Flying { get; private set; }

        public VortexMode Mode { get; private set; } = VortexMode.Sucking;

        /// <summary>image_xscale (and image_yscale).</summary>
        public double Scale { get; private set; }

        /// <summary>The implosion's image_angle, from 0 when it is set off (its flight's own spin is the view's).</summary>
        public double Angle { get; private set; }

        /// <summary>obj_vortex_child's image_angle.</summary>
        public double ChildAngle { get; private set; } = ChildStartAngle;

        /// <summary>The white circle's radius, in pixels (0, not drawn, until it blows).</summary>
        public double Burst { get; private set; }

        /// <summary>True once it has finished.</summary>
        public bool Done => Mode == VortexMode.Done;

        /// <summary>vortex_detonate.gml: it stops where it is, at nothing, and starts sucking.</summary>
        public void SetOff()
        {
            if (!Flying)
            {
                return;
            }

            Flying = false;
            Mode = VortexMode.Sucking;
            Scale = 0;
            Angle = 0;
            ChildAngle = ChildStartAngle;
            Burst = 0;
        }

        /// <summary>One step of vortex_step.gml (and obj_vortex_child's).</summary>
        public void Step()
        {
            if (Flying)
            {
                if (Scale < 1)
                {
                    Scale += FlyingGrowPerStep;
                }

                return;
            }

            switch (Mode)
            {
                case VortexMode.Sucking:
                    if (Scale < 1)
                    {
                        Scale += SuckGrowPerStep;
                    }
                    else
                    {
                        Scale = 1;
                        _wait = WaitSteps;
                        Mode = VortexMode.Waiting;
                    }

                    Angle -= 1;
                    break;
                case VortexMode.Waiting:
                    Angle -= 1;
                    if (--_wait <= 0)
                    {
                        Mode = VortexMode.Blowing;
                    }

                    break;
                case VortexMode.Blowing:
                    if (Scale > 0)
                    {
                        Scale -= BlowShrinkPerStep;
                    }
                    else
                    {
                        Scale = 0;
                        _wait = WaitSteps;
                        Mode = VortexMode.Waiting2;
                    }

                    Angle -= 1;
                    Burst += BurstGrowth;
                    break;
                case VortexMode.Waiting2:
                    Burst += BurstGrowth;
                    if (--_wait <= 0)
                    {
                        Mode = VortexMode.Done;
                    }

                    break;
                default:
                    return;
            }

            ChildAngle += ChildTurnPerStep;
        }
    }
}
