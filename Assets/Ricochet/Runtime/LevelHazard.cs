// A level hazard on the wire (UR-T6): one per Core Hazard, which the server spawns under a key ("hazard:1", "hazard:2",
// ...) when its HazardField adds one (Arena) and despawns when it goes. Every peer reads them all back (Collect) to build
// the level a shot sees, so the preview, the AI and the server's solve bounce off and stop in the same things; only a
// laser gate's on/off ever changes after it spawns. Each kind draws itself:
//   Shield wall  a hologram line in its owner's colour that fades in (obj_shieldwall's image_alpha += 0.01 to 0.9).
//   Electricity  draw_electricity.gml: a bolt redrawn every few frames through points a step apart along the arc, each
//                shaken off the line at random, over a wider faint glow, between two pylons.
//   Laser        check_source_laser.gml: as the gate comes on, the beam's end walks from the emitter toward the receiver
//                until it is there; while off, only a faint guide line between the two.
//   Foliage      a tuft (two crossed squares) of grass.
//   Breach       a dark gap over the border, its ends marked.
//   Redirector   a rail redirector with a tick pointing the way it sends a shot, as LevelView draws the level's own.
using System.Collections.Generic;
using Ricochet.Core;          // Hazard, HazardKind
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
        private float _spawnedAt;
        private float _beam = 1f;      // a laser's beam: the share of the way from emitter to receiver it reaches
        private bool _wasActive = true;
        private float _nextBolt;
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
            _endA = _endB = _tuft = _tuftCross = null;
            _builtKind = Kind;
            _builtA = A;
            _builtB = B;
            transform.position = Vector3.zero;
            switch (HazardKind)
            {
                case HazardKind.Electricity:
                    _glow = Lines.Create(transform, "Glow", new Color(ArcColor.r, ArcColor.g, ArcColor.b, 0.18f), 0.34f, -3);
                    _line = Lines.Create(transform, "Bolt", ArcColor, 0.06f, -2);
                    _endA = End("Pylon A", A, 0.3f, new Color(0.7f, 0.75f, 0.85f));
                    _endB = End("Pylon B", B, 0.3f, new Color(0.7f, 0.75f, 0.85f));
                    break;
                case HazardKind.Laser:
                    _glow = Lines.Create(transform, "Guide", new Color(LaserColor.r, LaserColor.g, LaserColor.b, 0.15f), 0.04f, -3);
                    Lines.Set(_glow, A, B);
                    _line = Lines.Create(transform, "Beam", LaserColor, 0.09f, -2);
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
                    _line = Lines.Create(transform, "Redirector", RedirectorColor, 0.18f, -3);
                    Lines.Set(_line, A, B);
                    Vector2 center = (A + B) * 0.5f;
                    Vector2 heading = Arena.ToUnity(Vec2.FromAngle(Heading));
                    _tick = Lines.Create(transform, "Heading", RedirectorColor, 0.06f, -3);
                    Lines.Set(_tick, center, center + heading * 0.6f);
                    break;
                }

                default: // a shield wall
                    _glow = Lines.Create(transform, "Glow", Color.white, 0.32f, -3);
                    Lines.Set(_glow, A, B);
                    _line = Lines.Create(transform, "Wall", Color.white, 0.12f, -2);
                    Lines.Set(_line, A, B);
                    break;
            }
        }

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
                        float flicker = 0.8f + 0.2f * Mathf.Sin(Time.time * 40f);
                        _line.startColor = _line.endColor = new Color(LaserColor.r, LaserColor.g, LaserColor.b, flicker);
                    }
                    else
                    {
                        _line.positionCount = 0;
                    }

                    _endA.color = Active ? LaserColor : new Color(0.45f, 0.1f, 0.1f);
                    break;
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

            Lines.Set(_glow, _points);
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
