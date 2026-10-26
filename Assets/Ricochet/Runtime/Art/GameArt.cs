// The presentation layer's one door to the original's art (U.RICO-T44): views ask for a GML sprite, background or font
// by its GML name and get the imported asset from the ArtCatalogue, or null when the catalogue lacks it (so a view
// keeps its code-drawn fallback rather than failing). Nothing outside presentation touches art.
using TMPro;
using UnityEngine;

namespace Ricochet
{
    /// <summary>The GML names of the sprites the views draw.</summary>
    public static class GmlSprites
    {
        // The tank (obj_player's turret over obj_bottom's hull) and the prints it leaves (obj_tracks).
        public const string TankTop = "spr_tank_top";
        public const string TankBottom = "spr_tank_bottom";
        public const string Tracks = "spr_tracks";

        // Shots in the air (obj_bullet, obj_pellet, obj_laser, obj_rail_swirl, obj_grenade, obj_vortex).
        public const string Shell = "spr_mortar";
        public const string Pellet = "spr_pellet";
        public const string LaserPulse = "spr_raingun_ring";
        public const string RailSwirl = "spr_railgun_swirl";
        public const string Grenade = "spr_grenade";
        public const string VortexDevice = "spr_vortex_device";

        // What a landing sets off (obj_explosion where a shell lands, obj_grenade_explosion for a grenade; obj_boom_normal
        // where a hit lands and obj_boom where a vortex is set off share spr_smallexplosion).
        public const string SmallExplosion = "spr_smallexplosion";
        public const string BigExplosion = "spr_bigexplosion";

        // A vortex set off (obj_vortex once it sucks, and obj_vortex_child over it).
        public const string Vortex = "spr_vortex";

        // The items that stand on the level (obj_shieldwall, obj_redirector).
        public const string ShieldWall = "spr_shieldwall";
        public const string RedirectorArrow = "spr_redirector_arrow";

        // Crates falling and landed (obj_weapondrop, obj_itemdrop, obj_healthdrop, obj_powerup) and the icon over them.
        public const string WeaponFall = "spr_weapon_fall";
        public const string WeaponLand = "spr_weapon_land";
        public const string ItemFall = "spr_item_fall";
        public const string ItemLand = "spr_item_land";
        public const string HealthFall = "spr_healthdrop_fall";
        public const string HealthLand = "spr_healthdrop_land";
        public const string PowerupFall = "spr_powerup_fall";
        public const string PowerupLand = "spr_powerup_land";
        public const string IconGun = "spr_icon_gun";
        public const string IconGear = "spr_icon_gear";
        public const string IconHealth = "spr_icon_health";

        // An unlimited count (obj_infinity), drawn after the weapon and item names on the turn card.
        public const string Infinity = "spr_infinity";

        /// <summary>
        /// The sprite of the level shape <paramref name="kind"/> number <paramref name="index"/> (0 to 15): obj_polygon1..16,
        /// obj_triangle1..16, obj_bar1..16, obj_circle1..16 and obj_square1..16 draw spr_polygon1..16 and so on (U.RICO-T46).
        /// </summary>
        public static string Shape(Ricochet.Core.Level.ShapeKind kind, int index)
        {
            string stem;
            switch (kind)
            {
                case Ricochet.Core.Level.ShapeKind.Abstract: stem = "spr_polygon"; break;
                case Ricochet.Core.Level.ShapeKind.Triangles: stem = "spr_triangle"; break;
                case Ricochet.Core.Level.ShapeKind.Bars: stem = "spr_bar"; break;
                case Ricochet.Core.Level.ShapeKind.Circles: stem = "spr_circle"; break;
                case Ricochet.Core.Level.ShapeKind.Squares: stem = "spr_square"; break;
                default: return null;
            }

            return stem + (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    /// <summary>The GML names of the original's nine fonts and its sprite font.</summary>
    public static class GmlFonts
    {
        public const string Impact = "fnt_impact";     // titles, the Ready card
        public const string Mono = "fnt_mono";         // menu buttons and labels (Monotype Corsiva)
        public const string Score = "fnt_score";       // the turn clock, scores, weapon and item lines, floating figures
        public const string ScoreShadow = "fnt_score_shadow";
        public const string Count = "fnt_count";       // the on-screen buttons and inventory counts
        public const string Small = "fnt_small";       // the crates' debug countdown, the fuse
        public const string Credits = "fnt_credits";
        public const string Font4 = "font4";
        public const string Font5 = "font5";
        public const string SpriteFont = "spr_spritefont";
    }

    public static class GameArt
    {
        private static ArtCatalogue s_catalogue;
        private static bool s_loaded;

        /// <summary>The catalogue in Resources (null if the art was never built).</summary>
        public static ArtCatalogue Catalogue
        {
            get
            {
                if (!s_loaded || s_catalogue == null)
                {
                    s_catalogue = Resources.Load<ArtCatalogue>(ArtCatalogue.ResourcePath);
                    s_loaded = true;
                }

                return s_catalogue;
            }
        }

        /// <summary>The frames of GML sprite <paramref name="name"/>, or null.</summary>
        public static Sprite[] Frames(string name)
        {
            ArtCatalogue.SpriteEntry entry = Catalogue != null ? Catalogue.Sprite(name) : null;
            return entry != null && entry.frames != null && entry.frames.Length > 0 ? entry.frames : null;
        }

        /// <summary>Frame <paramref name="frame"/> of GML sprite <paramref name="name"/> (wrapping as image_index does), or null.</summary>
        public static Sprite Frame(string name, int frame = 0)
        {
            Sprite[] frames = Frames(name);
            if (frames == null)
            {
                return null;
            }

            int i = frame % frames.Length;
            return frames[i < 0 ? i + frames.Length : i];
        }

        /// <summary>
        /// Frames a second the sprite animates at: <paramref name="imageSpeed"/> (or, when it is negative, the image_speed the
        /// first object showing it sets; objects sharing a sprite can disagree, so a view names its own object's) at the
        /// room speed (normalFPS).
        /// </summary>
        public static float FramesPerSecond(string name, float imageSpeed = -1f)
        {
            ArtCatalogue.SpriteEntry entry = Catalogue != null ? Catalogue.Sprite(name) : null;
            return entry != null ? (imageSpeed >= 0f ? imageSpeed : entry.imageSpeed) * Catalogue.RoomSpeed : 0f;
        }

        /// <summary>
        /// The frame showing <paramref name="seconds"/> after the sprite started (GameMaker's image_index advancing by
        /// image_speed a step), looping, or holding the last frame when <paramref name="loop"/> is false; null if missing.
        /// </summary>
        public static Sprite FrameAt(string name, float seconds, bool loop = true, float imageSpeed = -1f)
        {
            Sprite[] frames = Frames(name);
            if (frames == null)
            {
                return null;
            }

            int index = Mathf.FloorToInt(Mathf.Max(0f, seconds) * FramesPerSecond(name, imageSpeed));
            return frames[loop ? index % frames.Length : Mathf.Min(index, frames.Length - 1)];
        }

        /// <summary>How long one pass through the sprite's frames takes, in seconds (0 if it does not animate).</summary>
        public static float Duration(string name, float imageSpeed = -1f)
        {
            Sprite[] frames = Frames(name);
            float fps = FramesPerSecond(name, imageSpeed);
            return frames != null && fps > 0f ? frames.Length / fps : 0f;
        }

        /// <summary>The background <paramref name="name"/> as a sprite, or null.</summary>
        public static Sprite Background(string name)
        {
            ArtCatalogue.BackgroundEntry entry = Catalogue != null ? Catalogue.Background(name) : null;
            return entry != null ? entry.sprite : null;
        }

        /// <summary>The GML font <paramref name="name"/> as a TextMeshPro font, or null.</summary>
        public static TMP_FontAsset Font(string name)
        {
            ArtCatalogue.FontEntry entry = Catalogue != null ? Catalogue.Font(name) : null;
            return entry != null ? entry.font : null;
        }

        /// <summary>draw_text_outline's material for <paramref name="name"/>: white face, black outline <paramref name="pixels"/> (1 or 2) wide.</summary>
        public static Material Outline(string name, int pixels)
        {
            ArtCatalogue.FontEntry entry = Catalogue != null ? Catalogue.Font(name) : null;
            if (entry == null)
            {
                return null;
            }

            return pixels >= 2 ? entry.outline2 : pixels == 1 ? entry.outline1 : null;
        }

        /// <summary>
        /// The world TextMeshPro font size that draws GML font <paramref name="name"/> at the original's pixel size (a
        /// world point is a tenth of a unit, and the font's face is its font.gmx size), or 0 when the catalogue lacks it.
        /// </summary>
        public static float WorldFontSize(string name)
        {
            ArtCatalogue.FontEntry entry = Catalogue != null ? Catalogue.Font(name) : null;
            return entry != null && entry.font != null ? entry.size * 10f / Catalogue.PixelsPerUnit : 0f;
        }

        /// <summary>
        /// Draws <paramref name="text"/> in GML font <paramref name="name"/>, outlined as draw_text_outline does when
        /// <paramref name="outline"/> is 1 or 2 (black, that many pixels wide; draw_text_outline draws the face white, and
        /// draw_text_outline_color in a colour of the caller's, so the colour stays the caller's to set). Returns false,
        /// leaving the text as it was, when the catalogue lacks the font.
        /// </summary>
        public static bool ApplyFont(TMP_Text text, string name, int outline = 0)
        {
            TMP_FontAsset font = Font(name);
            if (text == null || font == null)
            {
                return false;
            }

            if (text.font != font)
            {
                text.font = font;
            }

            Material material = outline > 0 ? Outline(name, outline) : null;
            text.fontSharedMaterial = material != null ? material : font.material;
            text.fontStyle &= ~FontStyles.Bold; // GameMaker baked the face as it is; bold would only thicken the field
            return true;
        }
    }
}
