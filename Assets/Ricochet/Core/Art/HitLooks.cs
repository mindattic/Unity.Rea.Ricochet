// How the original's hit and knock-out effects look (U.RICO-T48, docs/BIBLE.md "Hit and knock-out effects"), step by
// GameMaker step, engine-free so the Core tests can hold it to the original. The presentation layer (FieldEffects,
// FloatText, GmlParticles, ShotView, Crate) runs these rules at the room speed (60 steps a second).
//   obj_float_text        show_float_text(_ext/_color): fnt_score text outlined 1 px (draw_text_outline_color), drawn 10 px
//                         over its spot, rising floatspeed (1) px a step and losing fadespeed (0.008, or 0.01 from the _ext and
//                         _color callers) of its alpha a step; when flash is on its colour moves on every step as colorCycle
//                         runs 1 to 5 and back to 0: white, red, yellow, blue, blue, white, and round again.
//   obj_hitscore          the same "+N" in the same cycle; nothing ever created it (hit.gml uses show_float_text).
//   obj_scorchmark(_small) spr_scorchmark / spr_scorchmark_small under everything, losing 0.0001 of its alpha a step, swept
//                         away with the round (clear_objects).
//   obj_playerexplosion   a knocked-out tank: spr_bigexplosion at image_speed 0.5 that holds its last frame (Animation End sets
//                         image_index 19) until the round is swept, sfx_explode, a big scorch mark, and 30 steps in (alarm 1)
//                         obj_flame: ef_smokeup every step in a random dark grey (0-40 each channel) 5 px above the spot.
//   obj_powerup_explosion a crate that lay too long: spr_bigexplosion at image_speed 0.5, gone at its last frame, sfx_explode,
//                         a big scorch mark.
//   spawn_glass / obj_glass 20-30 shards scattered 20 px either way, each one of spr_glass1-5, turning -2..2 degrees and
//                         moving -5..5 px a step either way, losing 0.02 of its alpha a step.
//   the drops             falling 10 px a step with ef_smokeup in darkgrey 25 px over them every step; landing, ef_firework in
//                         brown 10 px under them and sfx_crash; obj_powerup then throws ef_spark in white or orange 30 px over
//                         itself (less its hover) every step; a collected crate leaves ef_ring 25 px over where it stood.
// GameMaker's built-in effects (ef_ring, ef_firework, ef_smokeup, ef_spark) were drawn by the runner, not by the game, so
// GmEffects below is the rebuild's reading of them (sizes 0-2 as small, medium, large), recorded in the BIBLE as such.
using System;
using System.Collections.Generic;

namespace Ricochet.Core.Art
{
    /// <summary>One shard of spawn_glass: where it starts from the wall, its sprite, its turn and its speed (pixels and degrees a step).</summary>
    public readonly struct GlassShard
    {
        public GlassShard(int offsetX, int offsetY, int sprite, int spin, int speedX, int speedY)
        {
            OffsetX = offsetX;
            OffsetY = offsetY;
            Sprite = sprite;
            Spin = spin;
            SpeedX = speedX;
            SpeedY = speedY;
        }

        /// <summary>Pixels right of the wall's centre (GameMaker's x).</summary>
        public int OffsetX { get; }

        /// <summary>Pixels below the wall's centre (GameMaker's y grows downward).</summary>
        public int OffsetY { get; }

        /// <summary>Which of spr_glass1..5 it is, 0-based.</summary>
        public int Sprite { get; }

        /// <summary>Degrees it turns a step (obj_glass's rotation).</summary>
        public int Spin { get; }

        /// <summary>Pixels it moves right a step (hspeed).</summary>
        public int SpeedX { get; }

        /// <summary>Pixels it moves down a step (vspeed).</summary>
        public int SpeedY { get; }
    }

    /// <summary>GameMaker's built-in effects the original used.</summary>
    public enum GmEffect
    {
        /// <summary>ef_ring: a ring that spreads and fades.</summary>
        Ring,

        /// <summary>ef_firework: a burst of sparks flying apart.</summary>
        Firework,

        /// <summary>ef_smokeup: a puff of smoke drifting up.</summary>
        SmokeUp,

        /// <summary>ef_spark: a small flare that shrinks away.</summary>
        Spark,
    }

    /// <summary>How the rebuild draws one of GameMaker's built-in effects at one size (all in pixels and steps).</summary>
    public sealed class GmEffectLook
    {
        /// <summary>Particles it throws.</summary>
        public int Count { get; set; } = 1;

        /// <summary>Each particle lives this many steps, from <see cref="LifeMin"/> to <see cref="LifeMax"/>.</summary>
        public int LifeMin { get; set; }

        public int LifeMax { get; set; }

        /// <summary>A particle's size (its diameter, in pixels) as it starts, and what it gains (or loses) a step.</summary>
        public double Size { get; set; }

        public double SizeGrowth { get; set; }

        /// <summary>Speed in pixels a step, from <see cref="SpeedMin"/> to <see cref="SpeedMax"/>.</summary>
        public double SpeedMin { get; set; }

        public double SpeedMax { get; set; }

        /// <summary>True: every particle heads straight up (ef_smokeup); false: any way at random.</summary>
        public bool Upward { get; set; }

        /// <summary>The alpha it starts at; it fades to nothing over its life.</summary>
        public double Alpha { get; set; } = 1;
    }

    public static class HitLooks
    {
        // ------------------------------------------------------------------ colours (GameMaker's and globals.gml's)

        public static readonly GmlColor White = new GmlColor(255, 255, 255);
        public static readonly GmlColor Black = new GmlColor(0, 0, 0);

        /// <summary>c_red.</summary>
        public static readonly GmlColor Red = new GmlColor(255, 0, 0);

        /// <summary>c_yellow.</summary>
        public static readonly GmlColor Yellow = new GmlColor(255, 255, 0);

        /// <summary>c_blue.</summary>
        public static readonly GmlColor Blue = new GmlColor(0, 0, 255);

        /// <summary>c_green, add_random_health's text.</summary>
        public static readonly GmlColor Green = new GmlColor(0, 128, 0);

        /// <summary>c_orange, one of obj_powerup's spark colours.</summary>
        public static readonly GmlColor Orange = new GmlColor(255, 160, 64);

        /// <summary>globals.gml's darkgrey, make_color_rgb(32, 32, 32): the falling crates' smoke.</summary>
        public static readonly GmlColor DarkGrey = new GmlColor(32, 32, 32);

        /// <summary>globals.gml's brown, make_color_rgb(165, 42, 42): the landing crates' firework.</summary>
        public static readonly GmlColor Brown = new GmlColor(165, 42, 42);

        // ------------------------------------------------------------------ obj_float_text

        /// <summary>obj_float_text's fadespeed: show_float_text's alpha lost a step.</summary>
        public const double FloatFadePerStep = 0.008;

        /// <summary>The fadespeed hit.gml's damage, the pickups and add_random_health pass (show_float_text_ext / _color).</summary>
        public const double FloatQuickFadePerStep = 0.01;

        /// <summary>floatspeed: pixels it rises a step.</summary>
        public const double FloatRisePerStep = 1;

        /// <summary>It is drawn this many pixels over its spot (draw_text_outline_color(x, y - 10, ...)).</summary>
        public const double FloatDrawnAbove = 10;

        /// <summary>draw_text_outline_color's outline, 1 px.</summary>
        public const int FloatOutlinePixels = 1;

        /// <summary>
        /// The colour a flashing obj_float_text draws in on its <paramref name="step"/>th step (1 for the first): its Step
        /// moves colorCycle on before the Draw, 1 to 5 and back to 0, and 0 and 1 are white, 2 red, 3 yellow, 4 blue, and 5
        /// matches no case so the blue stays.
        /// </summary>
        public static GmlColor FlashColorAt(int step)
        {
            int cycle = ((step % 6) + 6) % 6;
            switch (cycle)
            {
                case 2: return Red;
                case 3: return Yellow;
                case 4:
                case 5: return Blue;
                default: return White;
            }
        }

        /// <summary>A float text's alpha <paramref name="steps"/> in, at <paramref name="fadePerStep"/> (0 once gone).</summary>
        public static double FloatAlphaAt(double steps, double fadePerStep) => Math.Max(0, 1 - fadePerStep * Math.Max(0, steps));

        /// <summary>The steps a float text lasts at <paramref name="fadePerStep"/>.</summary>
        public static int FloatLifeSteps(double fadePerStep) => fadePerStep > 0 ? (int)Math.Ceiling(1 / fadePerStep - 1e-9) : int.MaxValue;

        /// <summary>hit.gml's text over the shooter: "+N pts".</summary>
        public static string PointsText(int points) => "+" + points + " pts";

        /// <summary>hit.gml's text over whoever was struck: "-N".</summary>
        public static string DamageText(int damage) => "-" + damage;

        /// <summary>use_item.gml's text for an extra bounce.</summary>
        public const string ExtraBounceText = "Bounce +1";

        // ------------------------------------------------------------------ obj_scorchmark, obj_scorchmark_small

        /// <summary>scorchFade: a scorch mark's alpha lost a step.</summary>
        public const double ScorchFadePerStep = 0.0001;

        public static double ScorchAlphaAt(double steps) => Math.Max(0, 1 - ScorchFadePerStep * Math.Max(0, steps));

        // ------------------------------------------------------------------ obj_playerexplosion, obj_flame, obj_powerup_explosion

        /// <summary>obj_playerexplosion's image_speed.</summary>
        public const double KnockOutImageSpeed = 0.5;

        /// <summary>Its Animation End: image_index = 19, the last frame of spr_bigexplosion, held until the round is swept.</summary>
        public const int KnockOutHoldFrame = 19;

        /// <summary>Its alarm[1]: obj_flame starts smoking this many steps in.</summary>
        public const int FlameDelaySteps = 30;

        /// <summary>obj_flame's ef_smokeup goes this many pixels over the spot (y - 5).</summary>
        public const double FlameSmokeAbove = 5;

        /// <summary>obj_flame's smoke: make_color_rgb with each channel irandom_range(0, 40), the same random for none of them.</summary>
        public const int FlameGreyMax = 40;

        /// <summary>A random obj_flame smoke colour.</summary>
        public static GmlColor FlameColor(Random dice)
        {
            dice = dice ?? new Random();
            return new GmlColor((byte)dice.Next(FlameGreyMax + 1), (byte)dice.Next(FlameGreyMax + 1), (byte)dice.Next(FlameGreyMax + 1));
        }

        /// <summary>obj_powerup_explosion's image_speed (it goes at its last frame).</summary>
        public const double CrateBurstImageSpeed = 0.5;

        // ------------------------------------------------------------------ spawn_glass, obj_glass

        /// <summary>spawn_glass: irandom_range(20, 30) shards.</summary>
        public const int GlassMin = 20;

        public const int GlassMax = 30;

        /// <summary>obj_glass's Create: x and y each moved irandom_range(-20, 20).</summary>
        public const int GlassScatter = 20;

        /// <summary>rotation = irandom_range(-2, 2) degrees a step.</summary>
        public const int GlassSpin = 2;

        /// <summary>hspeed and vspeed = irandom_range(-5, 5).</summary>
        public const int GlassSpeed = 5;

        /// <summary>A shard loses this much of its alpha a step.</summary>
        public const double GlassFadePerStep = 0.02;

        /// <summary>spr_glass1..5. The original rolled five and drew spr_glass1 for two of them and spr_glass5 for none (a fixed defect: each is drawn).</summary>
        public const int GlassSprites = 5;

        /// <summary>spawn_glass's shards, rolled from <paramref name="dice"/>.</summary>
        public static List<GlassShard> SpawnGlass(Random dice)
        {
            dice = dice ?? new Random();
            int count = dice.Next(GlassMin, GlassMax + 1);
            var shards = new List<GlassShard>(count);
            for (int i = 0; i < count; i++)
            {
                int x = dice.Next(-GlassScatter, GlassScatter + 1);
                int y = dice.Next(-GlassScatter, GlassScatter + 1);
                int spin = dice.Next(-GlassSpin, GlassSpin + 1);
                int sprite = dice.Next(GlassSprites);
                int speedX = dice.Next(-GlassSpeed, GlassSpeed + 1);
                int speedY = dice.Next(-GlassSpeed, GlassSpeed + 1);
                shards.Add(new GlassShard(x, y, sprite, spin, speedX, speedY));
            }

            return shards;
        }

        /// <summary>The steps a shard lasts.</summary>
        public static int GlassLifeSteps => (int)Math.Ceiling(1 / GlassFadePerStep - 1e-9);

        // ------------------------------------------------------------------ the drops

        /// <summary>A falling crate's ef_smokeup goes this many pixels over it (y - 25).</summary>
        public const double CrateSmokeAbove = 25;

        /// <summary>A landing crate's ef_firework goes this many pixels under it (y + 10).</summary>
        public const double CrateFireworkBelow = 10;

        /// <summary>obj_powerup's ef_spark goes this many pixels over it, less its hover (y - 30 - hoverY).</summary>
        public const double CrateSparkAbove = 30;

        /// <summary>A collected crate's ef_ring goes this many pixels over where it stood (the collision events' y - 25).</summary>
        public const double CratePickupAbove = 25;

        /// <summary>obj_powerup's spark colour: choose(c_white, c_orange).</summary>
        public static GmlColor SparkColor(Random dice) => (dice ?? new Random()).Next(2) == 0 ? White : Orange;

        // ------------------------------------------------------------------ GameMaker's built-in effects

        /// <summary>
        /// How the rebuild draws <paramref name="effect"/> at GameMaker size <paramref name="size"/> (0 small, 1 medium, 2
        /// large; anything else is taken as the nearest).
        /// </summary>
        public static GmEffectLook Look(GmEffect effect, int size)
        {
            size = Math.Max(0, Math.Min(2, size));
            double scale = size == 0 ? 1 : size == 1 ? 1.5 : 2.5;
            switch (effect)
            {
                case GmEffect.Ring:
                    return new GmEffectLook { LifeMin = 12, LifeMax = 12, Size = 0, SizeGrowth = 8 * scale };
                case GmEffect.Firework:
                    return new GmEffectLook { Count = (int)(20 * scale), LifeMin = 15, LifeMax = 30, Size = 3, SizeGrowth = 0, SpeedMin = 0.5, SpeedMax = 3 * scale };
                case GmEffect.SmokeUp:
                    return new GmEffectLook { LifeMin = 30, LifeMax = 40, Size = 12 * scale, SizeGrowth = 0.25 * scale, SpeedMin = 1, SpeedMax = 1.5, Upward = true, Alpha = 0.5 };
                default:
                    return new GmEffectLook { LifeMin = 8, LifeMax = 12, Size = 10 * scale, SizeGrowth = -0.8 * scale };
            }
        }
    }
}
