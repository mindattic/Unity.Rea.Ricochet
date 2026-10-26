// A crate on the wire: one per Core Drop, which the server spawns under a key ("crate:1", "crate:2", ...) when its
// DropField drops one (Arena), and despawns when it bursts, is collected, or the round ends. Every peer draws it falling
// from above the top of the level to its spot at the rules' speed (the original's tick(10) a frame), then sitting there.
// Only a landed crate can be collected (by a shot passing over it, Arena), so only the landing is synced.
//
// U.RICO-T44: it is the original's art (GameArt): obj_weapondrop's spr_weapon_fall becoming spr_weapon_land as it lands
// (obj_itemdrop's spr_item_*, obj_healthdrop's spr_healthdrop_*, obj_powerup's spr_powerup_*), with its kind's icon
// (spr_icon_gun, spr_icon_gear, spr_icon_health; the powerup has none) drawn at 70% 34 pixels above it, bobbing five
// pixels up and down a pixel every five steps once landed (the drop's alarm 0). Without the art it falls back to a
// coloured square marked W, I, + or ?.
//
// U.RICO-T48 (HitLooks): falling, it trails ef_smokeup in darkgrey 25 px over itself every step; landing, it throws
// ef_firework in brown 10 px under itself and plays sfx_crash; obj_powerup, landed, throws ef_spark in white or orange 30 px
// over itself (less its hover) every step. Where it goes (taken: a ring; burst: obj_powerup_explosion) is FieldEffects',
// from the server's CrateGone.
using Ricochet.Core.Arsenal;
using Ricochet.Core.Art;
using Ricochet.Core.Level;
using Ricochet.Core.Menu;
using Unity.Multiplayer;      // NetEntity, NetKey
using Unity.Multiplayer.Core; // [Synced]
using UnityEngine;

namespace Ricochet
{
    public partial class Crate : NetEntity
    {
        public const string KeyPrefix = "crate:";

        /// <summary>The fallback square is drawn this wide.</summary>
        public const float Size = 0.6f;

        /// <summary>The icon sits this many GML pixels above the crate's origin (draw_sprite(spr_icon_*, 0, x, y - 34 - hoverY)).</summary>
        public const float IconRise = 34f;

        /// <summary>The icon is drawn at this alpha.</summary>
        public const float IconAlpha = 0.7f;

        /// <summary>The bob's reach either way, in GML pixels (hoverAmount).</summary>
        public const int HoverAmount = 5;

        /// <summary>Steps between bob moves (the drop's rate, alarm[0]).</summary>
        public const int HoverRate = 5;

        // Written by the server (Arena) as it spawns the crate, and when it lands.
        [Synced] private int _kind;
        [Synced] private Vector2 _spot;
        [Synced] private bool _landed;

        private SpriteRenderer _body;
        private SpriteRenderer _icon;
        private bool _art;
        private float _shownY = float.NaN;
        private TMPro.TextMeshPro _mark; // the fallback's world label over the square (UR-T38; it was IMGUI)
        private float _hoverClock;
        private int _hoverY;
        private bool _hoverUp = true;
        private bool _falling;       // this peer has drawn it falling (so it lands here, with its firework and crash)
        private float _effectClock;  // steps toward the next smoke puff or spark (U.RICO-T48)
        private static readonly System.Random s_dice = new System.Random();

        /// <summary>True once this peer has drawn it fall and land (its firework and sfx_crash), for tests.</summary>
        public bool LandedHere { get; private set; }

        public DropKind DropKind => (DropKind)Mathf.Clamp(Kind, 0, DropRules.KindCount - 1);

        /// <summary>Where this peer draws it now (falling, or at its spot).</summary>
        public Vector2 Shown => new Vector2(Spot.x, float.IsNaN(_shownY) ? Spot.y : _shownY);

        /// <summary>True when it is drawn from the original's sprites rather than the fallback square.</summary>
        public bool DrawnFromArt => _art;

        /// <summary>The sprite it shows now (for tests).</summary>
        public Sprite BodySprite => _body != null ? _body.sprite : null;

        public static string KeyFor(int id) => KeyPrefix + id;

        /// <summary>The colour of a kind's fallback crate.</summary>
        public static Color ColorOf(DropKind kind)
        {
            switch (kind)
            {
                case DropKind.Weapon: return new Color(0.95f, 0.55f, 0.2f);
                case DropKind.Item: return new Color(0.35f, 0.65f, 1f);
                case DropKind.Health: return new Color(0.3f, 0.85f, 0.35f);
                default: return new Color(0.85f, 0.4f, 0.95f);
            }
        }

        /// <summary>The mark drawn over a kind's fallback crate.</summary>
        public static string MarkOf(DropKind kind)
        {
            switch (kind)
            {
                case DropKind.Weapon: return "W";
                case DropKind.Item: return "I";
                case DropKind.Health: return "+";
                default: return "?";
            }
        }

        /// <summary>The GML sprite a kind's crate shows while falling and once landed.</summary>
        public static string SpriteOf(DropKind kind, bool landed)
        {
            switch (kind)
            {
                case DropKind.Weapon: return landed ? GmlSprites.WeaponLand : GmlSprites.WeaponFall;
                case DropKind.Item: return landed ? GmlSprites.ItemLand : GmlSprites.ItemFall;
                case DropKind.Health: return landed ? GmlSprites.HealthLand : GmlSprites.HealthFall;
                default: return landed ? GmlSprites.PowerupLand : GmlSprites.PowerupFall;
            }
        }

        /// <summary>The icon over a kind's crate, or null (the powerup has none).</summary>
        public static string IconOf(DropKind kind)
        {
            switch (kind)
            {
                case DropKind.Weapon: return GmlSprites.IconGun;
                case DropKind.Item: return GmlSprites.IconGear;
                case DropKind.Health: return GmlSprites.IconHealth;
                default: return null;
            }
        }

        protected override void OnSpawned()
        {
            _body = GetComponent<SpriteRenderer>();
            if (_body == null)
            {
                _body = gameObject.AddComponent<SpriteRenderer>();
            }

            _body.sortingOrder = -1;
            _art = GameArt.Frame(SpriteOf(DropKind, false)) != null;
            if (_art)
            {
                transform.localScale = Vector3.one;
                if (_icon == null)
                {
                    var icon = new GameObject("Icon");
                    icon.transform.SetParent(transform, false);
                    _icon = icon.AddComponent<SpriteRenderer>();
                    _icon.sortingOrder = 0;
                }
            }
            else
            {
                Shapes.Square(gameObject, Size, -1);
                if (_mark == null)
                {
                    // The crate's own scale is its size, so the label undoes it to stay a normal size in the world.
                    _mark = WorldText.Create(transform, "Mark", 0.35f, Color.white, bold: true);
                    _mark.transform.localScale = Vector3.one / Size;
                }
            }

            _shownY = Landed ? Spot.y : (float)(Arena.Level.MaxY + 1);
            Place();
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            float steps = Time.deltaTime * Steps.PerSecond;
            if (Landed || float.IsNaN(_shownY))
            {
                if (_falling)
                {
                    Land(); // this peer's fall reached the spot as the server's did
                }

                _shownY = Spot.y;
                Hover(Time.deltaTime);
                Spark(steps);
            }
            else
            {
                _shownY = Mathf.Max(Spot.y, _shownY - (float)Arena.DropRules.FallSpeed * Time.deltaTime);
                if (_shownY > Spot.y)
                {
                    Smoke(steps); // (down on this peer a moment before the server's landing arrives: no more smoke)
                }
            }

            Place();
        }

        // The drop's Step while it falls: ef_smokeup in darkgrey 25 px over it, every step (U.RICO-T48).
        private void Smoke(float steps)
        {
            _falling = true;
            _effectClock += steps;
            while (_effectClock >= 1f)
            {
                _effectClock -= 1f;
                GmlParticles.Play(GmEffect.SmokeUp, Shown + Vector2.up * ((float)HitLooks.CrateSmokeAbove * Steps.Pixel), 0, FloatText.Of(HitLooks.DarkGrey));
            }
        }

        // The drop's landing: ef_firework in brown 10 px under it and sfx_crash (a crate already down when this peer saw it
        // spawn landed before it was here, so it makes neither).
        private void Land()
        {
            _falling = false;
            _effectClock = 0f;
            LandedHere = true;
            GmlParticles.Play(GmEffect.Firework, (Vector2)Spot + Vector2.down * ((float)HitLooks.CrateFireworkBelow * Steps.Pixel), 0, FloatText.Of(HitLooks.Brown));
            GameAudio.Play(SoundCue.Crash);
        }

        // obj_powerup once landed: ef_spark in white or orange 30 px over it, less its hover, every step.
        private void Spark(float steps)
        {
            if (DropKind != DropKind.Powerup)
            {
                return;
            }

            _effectClock += steps;
            while (_effectClock >= 1f)
            {
                _effectClock -= 1f;
                Vector2 at = (Vector2)Spot + Vector2.up * ((float)(HitLooks.CrateSparkAbove + _hoverY) * Steps.Pixel);
                GmlParticles.Play(GmEffect.Spark, at, 1, FloatText.Of(HitLooks.SparkColor(s_dice)));
            }
        }

        // The drop's alarm 0: every five steps the icon moves a pixel toward the end of its reach, turning round a step there.
        private void Hover(float seconds)
        {
            float stepSeconds = HoverRate / (float)(GameArt.Catalogue != null ? GameArt.Catalogue.RoomSpeed : 60);
            _hoverClock += seconds;
            while (_hoverClock >= stepSeconds)
            {
                _hoverClock -= stepSeconds;
                if (_hoverUp)
                {
                    if (_hoverY < HoverAmount) { _hoverY++; } else { _hoverUp = false; }
                }
                else
                {
                    if (_hoverY > -HoverAmount) { _hoverY--; } else { _hoverUp = true; }
                }
            }
        }

        private void Place()
        {
            transform.position = Shown;
            if (_body == null)
            {
                return;
            }

            if (_art)
            {
                Sprite body = GameArt.Frame(SpriteOf(DropKind, Landed));
                if (body != null && _body.sprite != body)
                {
                    _body.sprite = body;
                }

                _body.color = Color.white;
                string iconName = IconOf(DropKind);
                Sprite icon = iconName != null ? GameArt.Frame(iconName) : null;
                if (_icon != null)
                {
                    _icon.enabled = icon != null;
                    if (icon != null)
                    {
                        _icon.sprite = icon;
                        _icon.color = new Color(1f, 1f, 1f, IconAlpha);
                        float ppu = GameArt.Catalogue.PixelsPerUnit;
                        _icon.transform.localPosition = new Vector3(0f, (IconRise + _hoverY) / ppu, 0f);
                    }
                }

                return;
            }

            Color color = ColorOf(DropKind);
            color.a = Landed ? 1f : 0.7f;
            _body.color = color;
            if (_mark != null)
            {
                WorldText.Set(_mark, MarkOf(DropKind));
            }
        }
    }
}
