// Draws one player's volley in the air: each projectile walking its flight (one shell, a beam, a railgun slug, a
// grenade, or a shotgun's fan of pellets), the trail behind it, and a brief flash where it lands; a grenade's flash is
// its blast, drawn at the blast's size. Every peer flies the volley the server solved (UR-T12: Player's ShotResolved
// carries it, packed by Core's ShotWire), so this is presentation only; the server's own flight decides what the volley
// struck. A vortex set off in mid-flight carries on along the cut-short volley the server sends again (Detonate) and
// goes off where the server set it off (UR-T6). It never reaches into Core's state (UR-T65): it plays the events its owner's
// shot raises on the Presentation feed (ShotFired launches it, ShotCut cuts it short) and says what its own flight does
// there (a bounce, a landing) for the audio and anything else listening.
//
// U.RICO-T44: the shots are the original's sprites (GameArt): obj_bullet's spr_mortar turned along its flight, obj_pellet's
// spr_pellet, obj_laser's spr_raingun_ring, obj_rail_swirl's spr_railgun_swirl along the aim, obj_grenade's spr_grenade and
// obj_vortex's spr_vortex_device spinning 3 degrees a step (the other way after each bounce), all untinted as GameMaker's
// draw_self draws them; a shell landing plays obj_explosion's spr_smallexplosion (image_speed 0.5) and a grenade
// obj_grenade_explosion's spr_bigexplosion at three times its size, 70% opaque (image_speed 0.2). The coloured squares
// remain only as the fallback when the catalogue lacks a sprite.
using System.Collections.Generic;
using Ricochet.Core.Aim;
using Ricochet.Core.Arsenal;
using Ricochet.Core.Net;
using UnityEngine;
using Cmd = Ricochet.Core.Commands;

namespace Ricochet
{
    public sealed class ShotView : MonoBehaviour
    {
        private const float FlashSeconds = 0.6f;

        /// <summary>obj_grenade and obj_vortex turn this many degrees a step (grenade_create.gml's spin).</summary>
        private const float SpinPerStep = 3f;

        /// <summary>obj_grenade_explosion's image_xscale and image_yscale.</summary>
        private const float BigExplosionScale = 3f;

        /// <summary>obj_explosion's image_speed (obj_boom's 0.1 shares the sprite).</summary>
        public const float SmallExplosionSpeed = 0.5f;

        /// <summary>obj_grenade_explosion's image_speed.</summary>
        public const float BigExplosionSpeed = 0.2f;

        /// <summary>obj_grenade_explosion draws itself at this alpha.</summary>
        private const float BigExplosionAlpha = 0.7f;

        private readonly List<Part> _parts = new List<Part>();
        private VolleyFlight _flight;
        private SpriteRenderer _blast;
        private LineRenderer _blastRing;
        private bool _blastArt;
        private float _blastAge;
        private float _flashLeft;
        private Color _color = Color.white;
        private Player _owner;

        /// <summary>The volley being drawn (null before the first).</summary>
        public VolleyFlight Flight => _flight;

        /// <summary>True while any projectile is in the air.</summary>
        public bool Flying => _flight != null && !_flight.Landed;

        /// <summary>Raised when the whole volley has landed.</summary>
        public event System.Action<Volley> Landed;

        /// <summary>A new, unparented view of <paramref name="owner"/>'s shots (the player's own transform is scaled to its square); the player destroys it.</summary>
        public static ShotView Create(string name, Player owner)
        {
            var view = new GameObject(name).AddComponent<ShotView>();
            view._owner = owner;
            return view;
        }

        /// <summary>The GML sprite a weapon's projectile is drawn with.</summary>
        public static string SpriteOf(Weapon weapon)
        {
            switch (weapon)
            {
                case Weapon.Shotgun: return GmlSprites.Pellet;
                case Weapon.Laser: return GmlSprites.LaserPulse;
                case Weapon.Railgun: return GmlSprites.RailSwirl;
                case Weapon.Grenade: return GmlSprites.Grenade;
                case Weapon.Vortex: return GmlSprites.VortexDevice;
                default: return GmlSprites.Shell;
            }
        }

        /// <summary>How a weapon's sprite turns: along its flight (the shell, the rail swirl), spinning (grenade, vortex) or not at all.</summary>
        private static Turning TurningOf(Weapon weapon)
        {
            switch (weapon)
            {
                case Weapon.Cannon:
                case Weapon.Railgun:
                    return Turning.Heading;
                case Weapon.Grenade:
                case Weapon.Vortex:
                    return Turning.Spin;
                default:
                    return Turning.None;
            }
        }

        private void OnEnable()
        {
            Presentation.Raised += OnEvent;
        }

        private void OnDisable()
        {
            Presentation.Raised -= OnEvent;
        }

        // The events about this view's owner: a shot fired is drawn in their colour, a vortex set off cuts it short.
        private void OnEvent(Player who, Cmd.GameEvent e)
        {
            if (who != _owner)
            {
                return;
            }

            switch (e)
            {
                case Cmd.ShotFired fired:
                    Launch(fired.Volley, _owner.Color);
                    break;
                case Cmd.ShotCut cut:
                    Detonate(cut.Volley);
                    break;
            }
        }

        private void Launch(Volley volley, Color color)
        {
            EnsureBlast();
            _flight = new VolleyFlight(volley);
            _color = color;
            while (_parts.Count < volley.Paths.Count)
            {
                _parts.Add(new Part(transform, _parts.Count));
            }

            Sprite art = GameArt.Frame(SpriteOf(volley.Weapon));
            Turning turning = TurningOf(volley.Weapon);
            float size = volley.Weapon == Weapon.Shotgun ? 0.14f : volley.Weapon == Weapon.Grenade ? 0.3f : 0.22f;
            for (int i = 0; i < _parts.Count; i++)
            {
                Part part = _parts[i];
                bool used = i < volley.Paths.Count;
                part.Projectile.enabled = used;
                part.Flash.enabled = false;
                part.Trail.positionCount = 0;
                part.Points.Clear();
                part.Landed = !used;
                if (!used)
                {
                    continue;
                }

                if (art != null)
                {
                    part.Projectile.sprite = art;
                    part.Projectile.color = Color.white;
                    part.Projectile.transform.localScale = Vector3.one;
                }
                else
                {
                    part.Projectile.sprite = Shapes.SquareSprite;
                    part.Projectile.color = color;
                    part.Projectile.transform.localScale = new Vector3(size, size, 1f);
                }

                part.Turning = art != null ? turning : Turning.None;
                part.Spin = SpinPerStep;
                part.Angle = (float)volley.Paths[i].Heading;
                part.Projectile.transform.rotation = part.Turning == Turning.None ? Quaternion.identity : Quaternion.Euler(0f, 0f, part.Angle);

                float width = volley.Weapon == Weapon.Laser || volley.Weapon == Weapon.Railgun ? 0.11f : 0.07f;
                part.Trail.startWidth = width;
                part.Trail.endWidth = width;
                part.Trail.startColor = new Color(color.r, color.g, color.b, 0.15f);
                part.Trail.endColor = new Color(color.r, color.g, color.b, 0.8f);
                part.Points.Add(Arena.ToUnity(volley.Paths[i].Start));
                Lines.Set(part.Trail, part.Points);
                Place(part.Projectile.transform, volley.Paths[i].Start);
            }

            _blast.enabled = false;
            _blastRing.positionCount = 0;
            _flashLeft = 0f;
        }

        /// <summary>
        /// The volley in the air was set off early (a vortex, vortex_detonate.gml): it carries on from where it is along
        /// <paramref name="cut"/>, the volley cut short, and goes off where that ends.
        /// </summary>
        private void Detonate(Volley cut)
        {
            if (_flight == null || cut == null)
            {
                return;
            }

            double travelled = _flight.Flights.Count > 0 ? _flight.Flights[0].Travelled : 0;
            var flight = new VolleyFlight(cut);
            double upTo = System.Math.Min(travelled, cut.Main.Length * 0.999);
            if (upTo > 0)
            {
                flight.Advance(upTo / cut.Speed);
            }

            _flight = flight;
            for (int i = 0; i < _parts.Count; i++)
            {
                bool used = i < cut.Paths.Count;
                _parts[i].Landed = !used;
                _parts[i].Projectile.enabled = used;
            }
        }

        /// <summary>
        /// Adds a light for each projectile in the air (UR-T77: obj_bullet, obj_pellet, obj_laser, obj_grenade, obj_rail_swirl and
        /// obj_vortex all light obj_fogscreen's surface): its place and its lightSize (the pellet and the laser 0.5, the vortex 0.7).
        /// </summary>
        public void CollectLights(List<(Vector2 Spot, double Size)> lights)
        {
            if (_flight == null || _flight.Landed)
            {
                return;
            }

            Weapon weapon = _flight.Volley.Weapon;
            double size = weapon == Weapon.Shotgun || weapon == Weapon.Laser ? 0.5 : weapon == Weapon.Vortex ? 0.7 : 1.0;
            for (int i = 0; i < _parts.Count && i < _flight.Flights.Count; i++)
            {
                if (!_parts[i].Landed && _parts[i].Projectile.enabled)
                {
                    lights.Add((_parts[i].Projectile.transform.position, size));
                }
            }
        }

        private void Update()
        {
            if (_owner != null && Turns.PausedIn(_owner.Session))
            {
                return; // paused (UR-T43): the shot hangs where it is on every peer, as the server's own flight does
            }

            float dt = Time.deltaTime;
            if (_flight != null && !_flight.Landed)
            {
                for (int i = 0; i < _flight.Flights.Count; i++)
                {
                    ProjectileFlight flight = _flight.Flights[i];
                    Part part = _parts[i];
                    if (part.Landed)
                    {
                        continue;
                    }

                    int before = flight.Contacts;
                    int passed = flight.Advance(dt);
                    for (int c = 0; c < passed; c++)
                    {
                        Vec2 point = flight.Path.Contacts[before + c].Point;
                        part.Points.Add(Arena.ToUnity(point));
                        part.Spin = -part.Spin; // grenade_step.gml: a bounce turns the spin round
                        Presentation.Raise(_owner, new Cmd.Bounced(_owner.PlayerId, _owner.LastVolleyShot, before + c + 1, point));
                    }

                    Place(part.Projectile.transform, flight.Position);
                    Turn(part, flight, dt);
                    part.Points.Add(part.Projectile.transform.position);
                    Lines.Set(part.Trail, part.Points);
                    part.Points.RemoveAt(part.Points.Count - 1);

                    if (flight.Landed)
                    {
                        part.Landed = true;
                        part.Projectile.enabled = false;
                        if (!_flight.Volley.HasBlast)
                        {
                            StartFlash(part, flight.Path.Impact);
                        }
                    }
                }

                if (_flight.Landed)
                {
                    _flashLeft = FlashSeconds;
                    if (_flight.Volley.HasBlast)
                    {
                        StartBlast(_flight.Volley.BlastCenter);
                        Ring(_flight.Volley.BlastCenter, _flight.Volley.BlastRadius);
                    }

                    Presentation.Raise(_owner, new Cmd.ShotLanded(_owner.PlayerId, _owner.Seat, _owner.LastVolleyShot, _flight.Volley, null, false, -1));
                    Landed?.Invoke(_flight.Volley);
                }
            }
            else if (_flashLeft > 0f)
            {
                _flashLeft -= dt;
                float t = Mathf.Clamp01(_flashLeft / FlashSeconds);
                foreach (Part part in _parts)
                {
                    if (part.Flash.enabled && !part.FlashArt)
                    {
                        part.Flash.transform.localScale = Vector3.one * Mathf.Lerp(1.4f, 0.4f, t);
                        part.Flash.color = new Color(_color.r, _color.g, _color.b, t);
                    }

                    part.Trail.startColor = new Color(_color.r, _color.g, _color.b, 0.15f * t);
                    part.Trail.endColor = new Color(_color.r, _color.g, _color.b, 0.8f * t);
                }

                if (_blast.enabled && !_blastArt)
                {
                    float diameter = (float)(_flight.Volley.BlastRadius * 2.0) * Mathf.Lerp(1f, 0.6f, t);
                    _blast.transform.localScale = new Vector3(diameter, diameter, 1f);
                    _blast.color = new Color(1f, 0.6f, 0.2f, 0.45f * t);
                }

                _blastRing.startColor = _blastRing.endColor = new Color(1f, 1f, 1f, 0.7f * t);

                if (_flashLeft <= 0f)
                {
                    foreach (Part part in _parts)
                    {
                        if (!part.FlashArt)
                        {
                            part.Flash.enabled = false;
                        }

                        part.Trail.positionCount = 0;
                    }

                    if (!_blastArt)
                    {
                        _blast.enabled = false;
                    }

                    _blastRing.positionCount = 0;
                }
            }

            Animate(dt);
        }

        // The landing effects drawn from the original's sprites play their frames once at the room speed, then go.
        private void Animate(float dt)
        {
            foreach (Part part in _parts)
            {
                if (!part.Flash.enabled || !part.FlashArt)
                {
                    continue;
                }

                part.FlashAge += dt;
                if (part.FlashAge >= GameArt.Duration(GmlSprites.SmallExplosion, SmallExplosionSpeed))
                {
                    part.Flash.enabled = false;
                }
                else
                {
                    part.Flash.sprite = GameArt.FrameAt(GmlSprites.SmallExplosion, part.FlashAge, false, SmallExplosionSpeed);
                }
            }

            if (_blast != null && _blast.enabled && _blastArt)
            {
                _blastAge += dt;
                if (_blastAge >= GameArt.Duration(GmlSprites.BigExplosion, BigExplosionSpeed))
                {
                    _blast.enabled = false;
                }
                else
                {
                    _blast.sprite = GameArt.FrameAt(GmlSprites.BigExplosion, _blastAge, false, BigExplosionSpeed);
                }
            }
        }

        private void Turn(Part part, ProjectileFlight flight, float dt)
        {
            switch (part.Turning)
            {
                case Turning.Heading:
                    part.Angle = (float)flight.Heading;
                    break;
                case Turning.Spin:
                    part.Angle += part.Spin * dt * (GameArt.Catalogue != null ? GameArt.Catalogue.RoomSpeed : 60);
                    break;
                default:
                    return;
            }

            part.Projectile.transform.rotation = Quaternion.Euler(0f, 0f, part.Angle);
        }

        private void StartFlash(Part part, Vec2 impact)
        {
            part.Flash.enabled = true;
            part.FlashAge = 0f;
            Sprite art = GameArt.Frame(GmlSprites.SmallExplosion);
            part.FlashArt = art != null;
            if (part.FlashArt)
            {
                part.Flash.sprite = art;
                part.Flash.color = Color.white;
                part.Flash.transform.localScale = Vector3.one;
            }
            else
            {
                part.Flash.sprite = Shapes.SquareSprite;
                part.Flash.color = _color;
            }

            Place(part.Flash.transform, impact);
        }

        private void StartBlast(Vec2 center)
        {
            _blast.enabled = true;
            _blastAge = 0f;
            Sprite art = GameArt.Frame(GmlSprites.BigExplosion);
            _blastArt = art != null;
            if (_blastArt)
            {
                _blast.sprite = art;
                _blast.color = new Color(1f, 1f, 1f, BigExplosionAlpha);
                _blast.transform.localScale = new Vector3(BigExplosionScale, BigExplosionScale, 1f);
            }
            else
            {
                _blast.sprite = Shapes.SquareSprite;
                _blast.color = new Color(1f, 0.6f, 0.2f, 0.45f);
            }

            Place(_blast.transform, center);
        }

        // obj_grenade_explosion's draw_circle outline at the blast's full radius.
        private void Ring(Vec2 center, double radius)
        {
            const int Segments = 40;
            _blastRing.positionCount = Segments + 1;
            for (int i = 0; i <= Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                _blastRing.SetPosition(i, new Vector3((float)(center.X + radius * Mathf.Cos(a)), (float)(center.Y + radius * Mathf.Sin(a)), 0f));
            }
        }

        private void EnsureBlast()
        {
            if (_blast != null)
            {
                return;
            }

            var blast = new GameObject("Blast");
            blast.transform.SetParent(transform, false);
            _blast = Shapes.Square(blast, 1f, 3);
            _blast.enabled = false;
            _blastRing = Lines.Create(transform, "Blast ring", Color.white, 0.05f, 4);
        }

        private static void Place(Transform t, Vec2 point)
        {
            t.position = new Vector3((float)point.X, (float)point.Y, 0f);
        }

        private enum Turning
        {
            None,
            Heading,
            Spin,
        }

        // One projectile's drawing: the projectile, its trail, and its impact flash.
        private sealed class Part
        {
            public Part(Transform parent, int index)
            {
                var projectile = new GameObject("Projectile " + index);
                projectile.transform.SetParent(parent, false);
                Projectile = Shapes.Square(projectile, 0.22f, 5);

                var flash = new GameObject("Impact " + index);
                flash.transform.SetParent(parent, false);
                Flash = Shapes.Square(flash, 0.8f, 4);
                Flash.enabled = false;

                Trail = Lines.Create(parent, "Trail " + index, Color.white, 0.07f, 3);
            }

            public SpriteRenderer Projectile { get; }

            public SpriteRenderer Flash { get; }

            public LineRenderer Trail { get; }

            public List<Vector3> Points { get; } = new List<Vector3>();

            public bool Landed { get; set; }

            public Turning Turning { get; set; }

            public float Angle { get; set; }

            public float Spin { get; set; }

            public bool FlashArt { get; set; }

            public float FlashAge { get; set; }
        }
    }
}
