// Draws one player's volley in the air: each projectile walking its flight (one shell, a beam, a railgun slug, a
// grenade, or a shotgun's fan of pellets), the trail behind it, and what it sets off where it lands. Every peer flies the
// volley the server solved (UR-T12: Player's ShotResolved carries it, packed by Core's ShotWire), so this is presentation
// only; the server's own flight decides what the volley struck. A vortex set off in mid-flight carries on along the
// cut-short volley the server sends again (Detonate) and goes off where the server set it off (UR-T6). It never reaches
// into Core's state (UR-T65): it plays the events its owner's shot raises on the Presentation feed (ShotFired launches it,
// ShotCut cuts it short) and says what its own flight does there (a bounce, a landing) for the audio and anything else
// listening.
//
// U.RICO-T44: the shots are the original's sprites (GameArt): obj_bullet's spr_mortar turned along its flight, obj_pellet's
// spr_pellet, obj_grenade's spr_grenade and obj_vortex's spr_vortex_device spinning 3 degrees a step (the other way after
// each bounce), all untinted as GameMaker's draw_self draws them; a grenade landing plays obj_grenade_explosion's
// spr_bigexplosion at three times its size, 70% opaque (image_speed 0.2). The coloured squares remain only as the fallback
// when the catalogue lacks a sprite.
//
// U.RICO-T47 (WeaponEffects, WeaponLooks): the laser is laser_draw's beam with obj_laser_pulse's rings riding it (no sprite
// at its head, as obj_laser's Draw drew none), running out once spent; the railgun is railgun_draw's rail with
// generate_swirl's spr_railgun_swirl every 56 px, fading at railFade; the cannon and shotgun, grenade and vortex keep the
// rebuild's trail in their player's colour. A shell that runs out of bounces plays obj_explosion's spr_smallexplosion
// where it stops (a pellet, a laser or a rail plays nothing there, as in the original); whoever a shot strikes gets
// obj_boom_normal's (hit.gml) on them. A grenade's blast is spr_bigexplosion under obj_grenade_explosion's circle growing
// 2 px a step, both gone when it reaches the blast radius. The vortex grows from a quarter of its size in flight; where it
// goes off it plays obj_boom (set off) or obj_explosion (spent) and vortex_step's implosion.
//
// U.RICO-T48: each of those blasts leaves its scorch mark on the level (FieldEffects): obj_scorchmark_small under a hit's
// obj_boom_normal, a spent shell's obj_explosion and a vortex's obj_boom or obj_explosion, obj_scorchmark under a grenade's.
using System.Collections.Generic;
using Ricochet.Core.Aim;
using Ricochet.Core.Arsenal;
using Ricochet.Core.Art;
using Ricochet.Core.Level;
using Ricochet.Core.Menu;
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

        private readonly List<Part> _parts = new List<Part>();
        private readonly List<Vector2> _struck = new List<Vector2>();
        private VolleyFlight _flight;
        private SpriteRenderer _blast;
        private bool _blastArt;
        private float _blastAge;
        private float _flashLeft;
        private Color _color = Color.white;
        private Player _owner;
        private bool _detonated;
        private LaserBeam _laser;
        private Rail _rail;
        private BlastRing _ring;
        private Implosion _implosion;
        private StormResult _storm;      // the storm walking the level's shapes while the implosion plays (U.RICO-T134)
        private Level _stormLevel;
        private VortexMode _heardMode = VortexMode.Done;

        /// <summary>The player whose shots it draws.</summary>
        public Player Owner => _owner;

        /// <summary>The volley being drawn (null before the first).</summary>
        public VolleyFlight Flight => _flight;

        /// <summary>True while any projectile is in the air.</summary>
        public bool Flying => _flight != null && !_flight.Landed;

        /// <summary>True while the laser's beam (or its pulses) is drawn.</summary>
        public bool LaserShowing => _laser != null && _laser.Showing;

        /// <summary>Pulses riding the laser's beam now.</summary>
        public int LaserPulses => _laser != null ? _laser.PulseCount : 0;

        /// <summary>True while the railgun's rail is drawn.</summary>
        public bool RailShowing => _rail != null && _rail.Showing;

        /// <summary>The rail's swirls drawn now.</summary>
        public int RailSwirls => _rail != null ? _rail.SwirlCount : 0;

        /// <summary>railAlpha now (0 with no rail).</summary>
        public float RailAlpha => _rail != null && _rail.Showing ? _rail.Alpha : 0f;

        /// <summary>True while obj_grenade_explosion's circle is drawn.</summary>
        public bool BlastShowing => _ring != null && _ring.Showing;

        /// <summary>The grenade circle's radius now, in units.</summary>
        public float BlastRadius => _ring != null ? _ring.Radius : 0f;

        /// <summary>True while a vortex's implosion is drawn.</summary>
        public bool Imploding => _implosion != null && _implosion.Showing;

        /// <summary>The implosion's mode (Done when there is none).</summary>
        public VortexMode ImplosionMode => _implosion != null ? _implosion.Mode : VortexMode.Done;

        /// <summary>The size the first projectile is drawn at (a vortex grows in flight).</summary>
        public float ProjectileScale => _parts.Count > 0 ? _parts[0].Projectile.transform.localScale.x : 0f;

        /// <summary>Raised when the whole volley has landed.</summary>
        public event System.Action<Volley> Landed;

        /// <summary>A new, unparented view of <paramref name="owner"/>'s shots (the player's own transform is scaled to its square); the player destroys it.</summary>
        public static ShotView Create(string name, Player owner)
        {
            var view = new GameObject(name).AddComponent<ShotView>();
            view._owner = owner;
            return view;
        }

        /// <summary>The GML sprite a weapon's projectile is drawn with (the laser's is its pulses', the railgun's its swirls').</summary>
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

        /// <summary>How a weapon's sprite turns: along its flight (the shell), spinning (grenade, vortex) or not at all.</summary>
        private static Turning TurningOf(Weapon weapon)
        {
            switch (weapon)
            {
                case Weapon.Cannon:
                    return Turning.Heading;
                case Weapon.Grenade:
                case Weapon.Vortex:
                    return Turning.Spin;
                default:
                    return Turning.None;
            }
        }

        // The laser and the railgun draw their own beam and rail where the others leave a trail.
        private static bool DrawsHead(Weapon weapon) => weapon != Weapon.Laser && weapon != Weapon.Railgun;

        private void OnEnable()
        {
            Presentation.Raised += OnEvent;
        }

        private void OnDisable()
        {
            Presentation.Raised -= OnEvent;
            HearImplosion(VortexMode.Done); // a view going lets go of its vortex's sounds
            EndStorm();
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
            EnsureEffects();
            _flight = new VolleyFlight(volley);
            _color = color;
            _detonated = false;
            while (_parts.Count < volley.Paths.Count)
            {
                _parts.Add(new Part(transform, _parts.Count));
            }

            Weapon weapon = volley.Weapon;
            bool head = DrawsHead(weapon);
            Sprite art = head ? GameArt.Frame(SpriteOf(weapon)) : null;
            Turning turning = TurningOf(weapon);
            float size = weapon == Weapon.Shotgun ? 0.14f : weapon == Weapon.Grenade ? 0.3f : 0.22f;
            for (int i = 0; i < _parts.Count; i++)
            {
                Part part = _parts[i];
                bool used = i < volley.Paths.Count;
                part.Projectile.enabled = used;
                part.Flash.enabled = false;
                part.Trail.positionCount = 0;
                part.Points.Clear();
                part.Landed = !used;
                part.Vortex = null;
                if (!used)
                {
                    continue;
                }

                if (!head)
                {
                    part.Projectile.sprite = null; // obj_laser's and obj_rail's own Draw draws no sprite at the head
                    part.Projectile.transform.localScale = Vector3.one;
                }
                else if (art != null)
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

                part.HasTrail = head;
                part.Turning = art != null ? turning : Turning.None;
                part.Spin = SpinPerStep;
                part.Angle = (float)volley.Paths[i].Heading;
                part.Projectile.transform.rotation = part.Turning == Turning.None ? Quaternion.identity : Quaternion.Euler(0f, 0f, part.Angle);
                if (weapon == Weapon.Vortex && art != null)
                {
                    part.Vortex = new Vortex(); // vortex_create: a quarter of its size, growing as it flies
                    part.VortexClock = 0f;
                    float scale = (float)part.Vortex.Scale;
                    part.Projectile.transform.localScale = new Vector3(scale, scale, 1f);
                }

                const float width = 0.07f;
                part.Trail.startWidth = width;
                part.Trail.endWidth = width;
                part.Trail.startColor = new Color(color.r, color.g, color.b, 0.15f);
                part.Trail.endColor = new Color(color.r, color.g, color.b, 0.8f);
                if (head)
                {
                    part.Points.Add(Arena.ToUnity(volley.Paths[i].Start));
                    Lines.Set(part.Trail, part.Points);
                }

                Place(part.Projectile.transform, volley.Paths[i].Start);
            }

            if (weapon == Weapon.Laser)
            {
                _laser.Launch(volley.Main);
            }
            else
            {
                _laser.Hide();
            }

            if (weapon == Weapon.Railgun)
            {
                _rail.Launch(volley.Main);
            }

            Struck(volley);
            _blast.enabled = false;
            _ring.Hide();
            EndStorm();
            _implosion.Hide();
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
            _detonated = true;
            Struck(cut);
            for (int i = 0; i < _parts.Count; i++)
            {
                bool used = i < cut.Paths.Count;
                _parts[i].Landed = !used;
                _parts[i].Projectile.enabled = used;
            }
        }

        // Where each player or dummy the volley strikes stands as it is fired (obj_boom_normal goes off on them, hit.gml).
        private void Struck(Volley volley)
        {
            _struck.Clear();
            if (volley.Strikes.Count == 0 || _owner == null)
            {
                return;
            }

            List<Target> targets = Arena.Targets(_owner.Session);
            foreach (Strike strike in volley.Strikes)
            {
                foreach (Target target in targets)
                {
                    if (target.Id == strike.TargetId)
                    {
                        _struck.Add(Arena.ToUnity(target.Center));
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Adds a light for each projectile in the air (UR-T77: obj_bullet, obj_pellet, obj_laser, obj_grenade, obj_rail_swirl and
        /// obj_vortex all light obj_fogscreen's surface): its place and its lightSize (the pellet and the laser 0.5, the vortex 0.7);
        /// every swirl of a rail still drawn (swirl_create's lightSize 1) and a vortex imploding (0.7).
        /// </summary>
        public void CollectLights(List<(Vector2 Spot, double Size)> lights)
        {
            if (_rail != null && _rail.Showing)
            {
                _rail.CollectLights(lights);
            }

            if (_implosion != null && _implosion.Showing && _flight != null)
            {
                lights.Add((Arena.ToUnity(_flight.Volley.BlastCenter), 0.7));
            }

            if (_flight == null || _flight.Landed)
            {
                return;
            }

            Weapon weapon = _flight.Volley.Weapon;
            if (weapon == Weapon.Railgun)
            {
                return; // its swirls light it
            }

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
                Fly(dt);
            }
            else if (_flashLeft > 0f)
            {
                FadeTrails(dt);
            }

            Effects(dt);
            Animate(dt);
        }

        private void Fly(float dt)
        {
            Volley volley = _flight.Volley;
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
                Grow(part, dt);
                if (part.HasTrail)
                {
                    part.Points.Add(part.Projectile.transform.position);
                    Lines.Set(part.Trail, part.Points);
                    part.Points.RemoveAt(part.Points.Count - 1);
                }

                if (i == 0 && volley.Weapon == Weapon.Laser)
                {
                    _laser.Fly(flight.Travelled);
                }
                else if (i == 0 && volley.Weapon == Weapon.Railgun)
                {
                    _rail.Reach(flight.Travelled);
                }

                if (flight.Landed)
                {
                    part.Landed = true;
                    part.Projectile.enabled = false;
                    if (volley.Weapon == Weapon.Cannon && flight.Path.End == ShotEndKind.Spent)
                    {
                        StartFlash(part, flight.Path.Impact); // bullet_step.gml: obj_explosion where the last bounce is spent
                        FieldEffects.ScorchAt(Arena.ToUnity(flight.Path.Impact), false); // and its obj_scorchmark_small (U.RICO-T48)
                    }

                    if (i == 0 && volley.Weapon == Weapon.Laser)
                    {
                        _laser.Spend();
                    }
                }
            }

            if (!_flight.Landed)
            {
                return;
            }

            _flashLeft = FlashSeconds;
            foreach (Vector2 spot in _struck)
            {
                SpriteBurst.Play(GmlSprites.SmallExplosion, spot, (float)WeaponLooks.HitFlashImageSpeed);
                FieldEffects.ScorchAt(spot, false); // obj_boom_normal's obj_scorchmark_small (U.RICO-T48)
            }

            if (volley.HasBlast && volley.Weapon == Weapon.Vortex)
            {
                Vector2 center = Arena.ToUnity(volley.BlastCenter);
                // vortex_detonate.gml's obj_boom where it is set off, vortex_step.gml's obj_explosion where it is spent; each
                // leaves obj_scorchmark_small.
                SpriteBurst.Play(GmlSprites.SmallExplosion, center, _detonated ? (float)WeaponLooks.DetonateFlashImageSpeed : SmallExplosionSpeed);
                FieldEffects.ScorchAt(center, false);
                _implosion.Start(center, WeaponLooks.ShotStepsPerSecond(Arena.Rules.Speed));
                _storm = volley.Storm.Shapes.Count > 0 ? volley.Storm : null;
                _stormLevel = Arena.Level;
                if (_storm != null)
                {
                    LevelView.KeepStorm(_storm, _stormLevel); // the server's level moved the shapes as the shot landed; so does this peer's
                }
            }
            else if (volley.HasBlast)
            {
                StartBlast(volley.BlastCenter);
                _ring.Start(Arena.ToUnity(volley.BlastCenter), volley.BlastRadius);
                FieldEffects.ScorchAt(Arena.ToUnity(volley.BlastCenter), true); // obj_grenade_explosion's obj_scorchmark
            }

            Presentation.Raise(_owner, new Cmd.ShotLanded(_owner.PlayerId, _owner.Seat, _owner.LastVolleyShot, volley, null, false, -1));
            Landed?.Invoke(volley);
        }

        // The trails, and the code-drawn flash and blast squares where the art is missing, fade after the landing.
        private void FadeTrails(float dt)
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

            if (_blast.enabled && !_blastArt && _flight != null)
            {
                float diameter = (float)(_flight.Volley.BlastRadius * 2.0) * Mathf.Lerp(1f, 0.6f, t);
                _blast.transform.localScale = new Vector3(diameter, diameter, 1f);
                _blast.color = new Color(1f, 0.6f, 0.2f, 0.45f * t);
            }

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
            }
        }

        // The laser running out, the rail fading, the grenade's circle and the vortex's implosion.
        private void Effects(float dt)
        {
            if (_laser == null)
            {
                return;
            }

            if (_laser.Showing && (_flight == null || _flight.Landed))
            {
                _laser.Fade(dt, _flight != null ? _flight.Volley.Speed : 1);
            }

            _rail.Step(dt);
            if (_ring.Showing && !_ring.Step(dt) && _blastArt)
            {
                _blast.enabled = false; // obj_grenade_explosion is gone once its circle reaches grenadeRadius
            }

            bool imploding = _implosion.Showing;
            _implosion.Step(dt);
            if (_storm != null)
            {
                // vortex_step.gml moves the level's shapes while it pulls and blows: walk them along the storm in step with the
                // implosion, and keep where they end up once it is over (the original never puts them back; U.RICO-T134).
                if (imploding && _implosion.Showing)
                {
                    LevelView.WalkStorm(_storm, _implosion.ElapsedSteps);
                }
                else if (imploding)
                {
                    EndStorm();
                }
            }

            HearImplosion(_implosion.Showing ? _implosion.Mode : VortexMode.Done);
        }

        // The storm is over (or cut short by the next shot): every shape it moved takes its final place and the level keeps it.
        private void EndStorm()
        {
            if (_storm == null)
            {
                return;
            }

            StormResult storm = _storm;
            _storm = null;
            LevelView.SettleStorm(storm, _stormLevel);
            _stormLevel = null;
        }

        // vortex_step.gml's sounds (U.RICO-T50): sfx_vortex_suck again each time it ends while it pulls in, stopped when it
        // blows out, and sfx_vortex_blow again each time it ends while it blows; each left to play out after.
        private void HearImplosion(VortexMode mode)
        {
            if (mode == _heardMode)
            {
                return;
            }

            if (_heardMode == VortexMode.Sucking)
            {
                GameAudio.Release(SoundCue.VortexSuck, this, stop: false);
            }
            else if (_heardMode == VortexMode.Blowing)
            {
                GameAudio.Release(SoundCue.VortexBlow, this, stop: false);
            }

            if (mode == VortexMode.Sucking)
            {
                GameAudio.Hold(SoundCue.VortexSuck, this);
            }
            else if (mode == VortexMode.Blowing)
            {
                GameAudio.Stop(SoundCue.VortexSuck);
                GameAudio.Hold(SoundCue.VortexBlow, this);
            }

            _heardMode = mode;
        }

        // The landing effects drawn from the original's sprites play their frames at the room speed.
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
                _blast.sprite = GameArt.FrameAt(GmlSprites.BigExplosion, _blastAge, false, BigExplosionSpeed); // its Animation End holds the last frame
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
                    part.Angle += part.Spin * dt * Steps.PerSecond;
                    break;
                default:
                    return;
            }

            part.Projectile.transform.rotation = Quaternion.Euler(0f, 0f, part.Angle);
        }

        // vortex_step.gml "flying": the device grows 0.01 a step to its full size.
        private static void Grow(Part part, float dt)
        {
            if (part.Vortex == null)
            {
                return;
            }

            part.VortexClock += dt * Steps.PerSecond;
            while (part.VortexClock >= 1f)
            {
                part.VortexClock -= 1f;
                part.Vortex.Step();
            }

            float scale = (float)part.Vortex.Scale;
            part.Projectile.transform.localScale = new Vector3(scale, scale, 1f);
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
                _blast.color = new Color(1f, 1f, 1f, (float)WeaponLooks.BlastAlpha);
                _blast.transform.localScale = new Vector3(BigExplosionScale, BigExplosionScale, 1f);
            }
            else
            {
                _blast.sprite = Shapes.SquareSprite;
                _blast.color = new Color(1f, 0.6f, 0.2f, 0.45f);
            }

            Place(_blast.transform, center);
        }

        private void EnsureEffects()
        {
            if (_blast != null)
            {
                return;
            }

            var blast = new GameObject("Blast");
            blast.transform.SetParent(transform, false);
            _blast = Shapes.Square(blast, 1f, 3);
            _blast.enabled = false;
            _laser = new LaserBeam(transform, 0);
            _rail = new Rail(transform);
            _ring = new BlastRing(transform);
            _implosion = new Implosion(transform);
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

            public bool HasTrail { get; set; }

            public Turning Turning { get; set; }

            public float Angle { get; set; }

            public float Spin { get; set; }

            public bool FlashArt { get; set; }

            public float FlashAge { get; set; }

            public Vortex Vortex { get; set; }

            public float VortexClock { get; set; }
        }
    }
}
