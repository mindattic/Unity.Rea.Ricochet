// A level hazard on the wire (UR-T6): one per Core Hazard, which the server spawns under a key ("hazard:1", "hazard:2",
// ...) when its HazardField adds one (Arena) and despawns when it goes. Every peer reads them all back (Collect) to build
// the level a shot sees, so the preview, the AI and the server's solve bounce off and stop in the same things; only a
// laser gate's on/off ever changes after it spawns. Each kind draws itself:
//   Shield wall  obj_shieldwall's spr_shieldwall across the aim, untinted, playing at image_speed 1 and fading in 0.01 a
//                step to 0.9 (U.RICO-T47); without the art a hologram line in its owner's colour that fades in.
//   Electricity  draw_electricity.gml: a bolt redrawn every few frames through points a step apart along the arc, each
//                shaken off the line at random, with its glow circles as soft lights (IRenderStyle.AddGlow, draw_glow's
//                look) shaken half as far, between two pylons; a wider faint line for the glow where there are no lights.
//   Laser        check_source_laser.gml: as the gate comes on, the beam's end walks from the emitter toward the receiver
//                until it is there, drawn as laser_draw.gml draws a laser (U.RICO-T47); while off, only a faint guide line.
//   Foliage      a tuft (two crossed squares) of grass.
//   Breach       a dark gap over the border, its ends marked.
//   Shrunk       nothing of its own: the level shape a shrink ray struck (LevelView's) shrinks away (U.RICO-T90).
//   Redirector   obj_redirector's spr_redirector_arrow along the way it sends a shot, growing from 0.3 of its size 0.01 a
//                step (U.RICO-T47); without the art a rail redirector with a tick, as LevelView draws the level's own.
//                It stands (and plays sfx_redirector_grow) only once its flight (ItemFlights) lands (U.RICO-T79).
using System.Collections.Generic;
using Ricochet.Core.Aim;
using Ricochet.Core.Art;
using Ricochet.Core.Level;
using Unity.Multiplayer;      // NetEntity, NetSession
using Unity.Multiplayer.Core; // [Synced]
using UnityEngine;

namespace Ricochet
{
    public partial class LevelHazard : NetEntity
    {
        public const string KeyPrefix = "hazard:";

        private const float BoltStep = 0.25f;      // draw_electricity's quality
        private const float BoltShake = 0.14f;     // its shakiness
        private const float BoltRedraw = 0.05f;    // seconds between two bolts
        private const float BeamSpeed = 14f;       // units a second a laser's beam grows at (check_source_laser's dX, dY)
        private const float FadeInSeconds = 1.5f;  // a shield wall's fade in

        private static readonly List<LevelHazard> s_spawned = new List<LevelHazard>();
        private static readonly Color ArcColor = new Color(0.55f, 0.85f, 1f);
        private static readonly Color LaserColor = new Color(1f, 0.15f, 0.2f);
        private static readonly Color FoliageColor = new Color(0.25f, 0.7f, 0.25f);
        private static readonly Color RedirectorColor = new Color(1f, 0.55f, 0.15f);

        // Written by the server (Arena) as it spawns the hazard; _active again whenever a laser gate switches.
        [Synced] private int _hazardId;
        [Synced] private int _kind;
        [Synced] private Vector2 _a;
        [Synced] private Vector2 _b;
        [Synced] private float _radius;
        [Synced] private int _ownerSeat = -1;
        [Synced] private float _heading;
        [Synced] private bool _active = true;

        private LineRenderer _line;
        private LineRenderer _glow;
        private LineRenderer _tick;
        private SpriteRenderer _endA;
        private SpriteRenderer _endB;
        private SpriteRenderer _tuft;
        private SpriteRenderer _tuftCross;
        private SpriteRenderer _art;
        private readonly List<Transform> _sparks = new List<Transform>();
        private float _spawnedAt;
        private float _beam = 1f;      // a laser's beam: the share of the way from emitter to receiver it reaches
        private bool _wasActive = true;
        private float _nextBolt;
        private bool _growHeard;       // a redirector's sfx_redirector_grow has played (once its flight landed)
        private readonly List<Vector3> _points = new List<Vector3>();

        public HazardKind HazardKind => (HazardKind)Mathf.Clamp(Kind, 0, HazardRules.KindCount - 1);

        /// <summary>How far out from its emitter a laser gate's beam reaches on this peer now (0..1; 1 for the rest).</summary>
        public float Beam => _beam;

        /// <summary>
        /// True once this peer holds the hazard's values (the server writes them just after the spawn, so a peer can see
        /// the object a moment before them; hazard ids start at 1).
        /// </summary>
        public bool Ready => HazardId > 0;

        public static string KeyFor(int id) => KeyPrefix + id;

        /// <summary>Every hazard <paramref name="session"/> has spawned, as Core hazards (what a peer builds a shot's level from).</summary>
        public static List<Hazard> Collect(NetSession session)
        {
            var hazards = new List<Hazard>();
            foreach (LevelHazard hazard in In(session))
            {
                if (hazard.Ready)
                {
                    hazards.Add(hazard.ToCore());
                }
            }

            return hazards;
        }

        /// <summary>Every spawned hazard <paramref name="session"/> has.</summary>
        public static IEnumerable<LevelHazard> In(NetSession session)
        {
            for (int i = 0; i < s_spawned.Count; i++)
            {
                LevelHazard hazard = s_spawned[i];
                if (hazard != null && hazard.IsSpawned && hazard.Session == session)
                {
                    yield return hazard;
                }
            }
        }

        /// <summary>Server: copies a Core hazard onto this entity.</summary>
        public void Mirror(Hazard hazard)
        {
            if (!IsServer || hazard == null)
            {
                return;
            }

            HazardId = hazard.Id;
            Kind = (int)hazard.Kind;
            A = Arena.ToUnity(hazard.A);
            B = Arena.ToUnity(hazard.B);
            Radius = (float)hazard.Radius;
            OwnerSeat = hazard.Owner;
            Heading = (float)hazard.Heading;
            Active = hazard.Active;
        }

        /// <summary>This hazard as Core data.</summary>
        public Hazard ToCore()
        {
            return new Hazard(HazardId, HazardKind, Arena.ToCore(A), Arena.ToCore(B), Radius, OwnerSeat, Heading, Active);
        }

        protected override void OnSpawned()
        {
            if (!s_spawned.Contains(this))
            {
                s_spawned.Add(this);
            }

            _spawnedAt = Time.time;
            _wasActive = Active;
            _beam = 1f;
            Build();
            _growHeard = false;
            if (HazardKind == HazardKind.Redirector && !ItemFlights.RedirectorInFlight(-1))
            {
                GameAudio.Play(Ricochet.Core.Menu.SoundCue.RedirectorGrow); // redirector_detonate.gml: it stops and opens out
                _growHeard = true;
            }
        }

        protected override void OnDespawned()
        {
            s_spawned.Remove(this);
        }

        // What the drawing was built for: on the server the fields are written just after the spawn, so it is built again
        // once they hold the hazard (a client has them all when it spawns).
        private int _builtKind = -1;
        private Vector2 _builtA;
        private Vector2 _builtB;

        private void Build()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }

            _line = _glow = _tick = null;
            _endA = _endB = _tuft = _tuftCross = _art = null;
            _sparks.Clear();
            _builtKind = Kind;
            _builtA = A;
            _builtB = B;
            transform.position = Vector3.zero;
            switch (HazardKind)
            {
                case HazardKind.Electricity:
                    _line = Lines.Create(transform, "Bolt", ArcColor, 0.06f, -2);
                    BuildSparks();
                    if (_sparks.Count == 0)
                    {
                        // No 2D lights on this pipeline: draw_electricity's glow circles as a wide faint line instead.
                        _glow = Lines.Create(transform, "Glow", new Color(ArcColor.r, ArcColor.g, ArcColor.b, 0.18f), 0.34f, -3);
                    }

                    _endA = End("Pylon A", A, 0.3f, new Color(0.7f, 0.75f, 0.85f));
                    _endB = End("Pylon B", B, 0.3f, new Color(0.7f, 0.75f, 0.85f));
                    break;
                case HazardKind.Laser:
                    _glow = Lines.Create(transform, "Guide", new Color(LaserColor.r, LaserColor.g, LaserColor.b, 0.15f), 0.04f, -3);
                    Lines.Set(_glow, A, B);
                    // laser_draw.gml's beam: laser_color 4 px at 0.7 under a white 1 px core at 0.9 (U.RICO-T47).
                    _line = Steps.Line(transform, "Beam", Steps.Of(WeaponLooks.LaserColor, (float)WeaponLooks.LaserOuterAlpha), (float)WeaponLooks.LaserOuterWidth, -2);
                    _tick = Steps.Line(transform, "Beam core", new Color(1f, 1f, 1f, (float)WeaponLooks.LaserInnerAlpha), (float)WeaponLooks.LaserInnerWidth, -1);
                    _endA = End("Emitter", A, 0.28f, new Color(0.45f, 0.1f, 0.1f));
                    _endB = End("Receiver", B, 0.22f, new Color(0.45f, 0.1f, 0.1f));
                    break;
                case HazardKind.Foliage:
                {
                    var tuft = new GameObject("Tuft");
                    tuft.transform.SetParent(transform, false);
                    _tuft = Shapes.Square(tuft, Radius * 1.6f, -3);
                    tuft.transform.position = A;
                    var cross = new GameObject("Tuft cross");
                    cross.transform.SetParent(transform, false);
                    _tuftCross = Shapes.Square(cross, Radius * 1.6f, -3);
                    cross.transform.position = A;
                    cross.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
                    _tuft.color = FoliageColor;
                    _tuftCross.color = FoliageColor * 0.8f;
                    break;
                }

                case HazardKind.Breach:
                    _line = Lines.Create(transform, "Gap", new Color(0.05f, 0.05f, 0.07f), 0.22f, -3);
                    Lines.Set(_line, A, B);
                    _endA = End("Edge A", A, 0.2f, new Color(0.9f, 0.25f, 0.2f));
                    _endB = End("Edge B", B, 0.2f, new Color(0.9f, 0.25f, 0.2f));
                    break;
                case HazardKind.Redirector:
                {
                    // obj_redirector's spr_redirector_arrow along its heading (U.RICO-T47); the line and tick without the art.
                    _art = ArtSprite("Redirector", GmlSprites.RedirectorArrow, (A + B) * 0.5f, -3);
                    if (_art != null)
                    {
                        break;
                    }

                    _line = Lines.Create(transform, "Redirector", RedirectorColor, 0.18f, -3);
                    Lines.Set(_line, A, B);
                    Vector2 center = (A + B) * 0.5f;
                    Vector2 heading = Arena.ToUnity(Vec2.FromAngle(Heading));
                    _tick = Lines.Create(transform, "Heading", RedirectorColor, 0.06f, -3);
                    Lines.Set(_tick, center, center + heading * 0.6f);
                    break;
                }

                default: // a shield wall
                    // obj_shieldwall's spr_shieldwall across the aim, playing and fading in (U.RICO-T47); lines without the art.
                    _art = ArtSprite("Shield wall", GmlSprites.ShieldWall, (A + B) * 0.5f, -2);
                    if (_art != null)
                    {
                        Vector2 across = B - A;
                        _art.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(across.y, across.x) * Mathf.Rad2Deg - 90f);
                        _art.color = new Color(1f, 1f, 1f, 0f);
                        break;
                    }

                    _glow = Lines.Create(transform, "Glow", Color.white, 0.32f, -3);
                    Lines.Set(_glow, A, B);
                    _line = Lines.Create(transform, "Wall", Color.white, 0.12f, -2);
                    Lines.Set(_line, A, B);
                    break;
            }
        }

        /// <summary>The hazard drawn from the original's art (a shield wall, a placed redirector), or null.</summary>
        public SpriteRenderer Art => _art;

        // A GML sprite of the hazard at <paramref name="at"/>, or null when the catalogue lacks it.
        private SpriteRenderer ArtSprite(string childName, string sprite, Vector2 at, int order)
        {
            Sprite first = GameArt.Frame(sprite);
            if (first == null)
            {
                return null;
            }

            SpriteRenderer renderer = Steps.Sprite(transform, childName, order);
            renderer.sprite = first;
            renderer.transform.position = at;
            return renderer;
        }

        // draw_electricity's glow: a soft light (IRenderStyle.AddGlow, as draw_glow's circles) every other vertex of the arc.
        private void BuildSparks()
        {
            _sparks.Clear();
            float length = Vector2.Distance(A, B);
            for (float d = BoltStep; d < length; d += BoltStep * 2f)
            {
                var holder = new GameObject("Spark");
                holder.transform.SetParent(transform, false);
                if (RenderStyle.Current.AddGlow(holder.transform, ArcColor * 0.5f, BoltShake * 4f) == null)
                {
                    Destroy(holder);
                    _sparks.Clear();
                    return;
                }

                _sparks.Add(holder.transform);
            }
        }

        /// <summary>The soft lights along an electric arc (none on a pipeline without 2D lights).</summary>
        public int Sparks => _sparks.Count;

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (Kind != _builtKind || A != _builtA || B != _builtB)
            {
                _spawnedAt = Time.time;
                _wasActive = Active;
                Build();
            }

            switch (HazardKind)
            {
                case HazardKind.Electricity:
                    if (Time.time >= _nextBolt)
                    {
                        _nextBolt = Time.time + BoltRedraw;
                        Bolt();
                    }

                    break;
                case HazardKind.Laser:
                    if (Active && !_wasActive)
                    {
                        _beam = 0f; // switching on: the beam sets out from the emitter again
                    }

                    _wasActive = Active;
                    float length = Vector2.Distance(A, B);
                    _beam = length <= 0f ? 1f : Mathf.Min(1f, _beam + BeamSpeed * Time.deltaTime / length);
                    if (Active)
                    {
                        Lines.Set(_line, A, Vector2.Lerp(A, B, _beam));
                        Lines.Set(_tick, A, Vector2.Lerp(A, B, _beam));
                    }
                    else
                    {
                        _line.positionCount = 0;
                        _tick.positionCount = 0;
                    }

                    _endA.color = Active ? LaserColor : new Color(0.45f, 0.1f, 0.1f);
                    break;
                case HazardKind.Redirector:
                    // U.RICO-T79: it stands only once its flight (ItemFlights) has landed, and opens out from then.
                    bool flying = ItemFlights.RedirectorInFlight(OwnerSeat);
                    if (flying)
                    {
                        _spawnedAt = Time.time;
                    }
                    else if (!_growHeard)
                    {
                        _growHeard = true;
                        GameAudio.Play(Ricochet.Core.Menu.SoundCue.RedirectorGrow);
                    }

                    Show(!flying);
                    if (_art != null)
                    {
                        // redirector_detonate / redirector_step: from 0.3 of its size, growing 0.01 a step, along its heading.
                        float scale = (float)WeaponLooks.RedirectorScaleAt((Time.time - _spawnedAt) * Steps.PerSecond);
                        _art.transform.localScale = new Vector3(scale, scale, 1f);
                        _art.transform.rotation = Quaternion.Euler(0f, 0f, Heading);
                    }

                    break;
                case HazardKind.Shrunk:
                    // U.RICO-T90: the shape the shrink ray struck shrinks away (it is gone from the level already).
                    LevelView.ShrinkShape(OwnerSeat, (Time.time - _spawnedAt) * Steps.PerSecond);
                    break;
                case HazardKind.ShieldWall when _art != null:
                {
                    // obj_shieldwall: image_speed 1, image_alpha += 0.01 a step to 0.9, untinted.
                    float age = Time.time - _spawnedAt;
                    _art.sprite = GameArt.FrameAt(GmlSprites.ShieldWall, age, true, (float)WeaponLooks.ShieldWallImageSpeed);
                    _art.color = new Color(1f, 1f, 1f, (float)WeaponLooks.ShieldWallAlphaAt(age * Steps.PerSecond));
                    break;
                }

                case HazardKind.ShieldWall:
                {
                    float alpha = Mathf.Clamp01((Time.time - _spawnedAt) / FadeInSeconds) * 0.9f;
                    Color owner = OwnerColor();
                    Color tint = Color.Lerp(owner, new Color(0.6f, 0.95f, 1f), 0.5f);
                    float shimmer = 0.85f + 0.15f * Mathf.Sin(Time.time * 9f + HazardId);
                    _line.startColor = _line.endColor = new Color(tint.r, tint.g, tint.b, alpha * shimmer);
                    _glow.startColor = _glow.endColor = new Color(tint.r, tint.g, tint.b, alpha * 0.25f);
                    break;
                }
            }
        }

        /// <summary>False while what is drawn of it is hidden (a redirector waiting for its flight to land, U.RICO-T79).</summary>
        public bool Shown => _art != null ? _art.enabled : _line == null || _line.enabled;

        // Shows or hides what is drawn of this hazard (a redirector waiting for its flight to land).
        private void Show(bool shown)
        {
            if (_art != null)
            {
                _art.enabled = shown;
            }

            if (_line != null)
            {
                _line.enabled = shown;
            }

            if (_tick != null)
            {
                _tick.enabled = shown;
            }
        }

        // draw_electricity.gml: a vertex every BoltStep along the arc, each pushed off it by up to BoltShake at random.
        private void Bolt()
        {
            _points.Clear();
            Vector2 a = A;
            Vector2 b = B;
            float length = Vector2.Distance(a, b);
            Vector2 along = length > 0f ? (b - a) / length : Vector2.zero;
            _points.Add(a);
            for (float d = BoltStep; d < length; d += BoltStep)
            {
                _points.Add(a + along * d + Random.insideUnitCircle * BoltShake);
            }

            _points.Add(b);
            Lines.Set(_line, _points);
            for (int i = 1; i < _points.Count - 1; i++)
            {
                _points[i] = a + along * (i * BoltStep) + Random.insideUnitCircle * (BoltShake * 0.5f);
            }

            // The glow circles are shaken half as far, afresh each time (draw_electricity's second random offsets).
            for (int i = 0; i < _sparks.Count; i++)
            {
                _sparks[i].position = a + along * ((2 * i + 1) * BoltStep) + Random.insideUnitCircle * (BoltShake * 0.5f);
            }

            if (_glow != null)
            {
                Lines.Set(_glow, _points);
            }
        }

        private Color OwnerColor()
        {
            foreach (Player player in Player.In(Session))
            {
                if (player.Seat == OwnerSeat)
                {
                    return player.Color;
                }
            }

            return Color.white;
        }

        private SpriteRenderer End(string name, Vector2 at, float size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            SpriteRenderer square = Shapes.Square(go, size, -2);
            go.transform.position = at;
            square.color = color;
            return square;
        }
    }
}
