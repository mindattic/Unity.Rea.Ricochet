// The player: one per connection, spawned by the NetworkManager as its Player Prefab, standing at its seat's spot.
// The owner turns an aim and charges a power (AimState) and sees the shot's whole bounce path before firing; Fire
// sends the aim to the server, which is the one that decides (UR-LAW-1): it clamps the input, solves the shot with
// Core's BounceSolver, flies it, and knocks down whatever it lands on. Every peer replays the same flight from the
// synced shot inputs to draw it, so nothing per-frame goes over the wire. The name and colour live in the session's
// roster (NetRoster), which every peer has.
using Ricochet.Core;          // AimState, BounceSolver, ProjectileFlight, ShotPath
using Unity.Multiplayer;      // NetEntity, NetPlayer, NetRoster, NetBots
using Unity.Multiplayer.Core; // [ToServer], [Synced]
using UnityEngine;

namespace Ricochet
{
    public partial class Player : NetEntity
    {
        /// <summary>The aim a new player starts with: straight up, half power.</summary>
        public const float StartHeading = 90f;
        public const float StartPower = 0.5f;

        // client -> server: the owner's aim, so everyone sees which way the barrel points (at most 20 a second).
        [ToServer(RateLimit = 20)]
        public partial void Aim(float heading, float power);

        // client -> server: fire at this aim. The shot carries its own aim, so a dropped Aim never changes what flies.
        [ToServer(RateLimit = 4)]
        public partial void Fire(float heading, float power);

        // Only the server writes these; every peer, late joiners included, reads the same values.
        [Synced] private Vector2 _position;
        [Synced] private float _heading = StartHeading;
        [Synced] private float _power = StartPower;

        // The last shot: its aim, and a count that goes up by one per shot so every peer knows a new one was fired.
        [Synced] private int _shots;
        [Synced] private float _shotHeading;
        [Synced] private float _shotPower;

        private readonly AimState _aim = new AimState(StartHeading, StartPower); // owner only: the local, responsive aim
        private float _aimSentAt = -1f;
        private float _sentHeading = float.NaN;
        private float _sentPower = float.NaN;

        private ProjectileFlight _serverFlight; // server only: the shot it is flying, to land the impact
        private int _seenShots;                 // every peer: the last shot it has drawn
        private ShotPath _preview;              // owner only: the shot as it would fly right now

        private SpriteRenderer _body;
        private SpriteRenderer _barrel;
        private LineRenderer _previewLine;
        private ShotView _shotView;

        /// <summary>The owner's name, from the roster ("Player" until the roster has it).</summary>
        public string DisplayName
        {
            get { return Roster(out NetPlayer player) ? player.DisplayName : "Player"; }
        }

        /// <summary>The owner's colour, from the roster (their pick, or their seat's palette colour).</summary>
        public Color32 Color
        {
            get { return Roster(out NetPlayer player) ? player.UnityColor() : new Color32(255, 255, 255, 255); }
        }

        /// <summary>True while this player's last shot is still in the air on this peer.</summary>
        public bool ShotInFlight
        {
            get { return IsServer ? _serverFlight != null && !_serverFlight.Landed : _shotView != null && _shotView.Flying; }
        }

        /// <summary>The path of the shot this peer last saw fired (null before the first).</summary>
        public ShotPath LastShot { get; private set; }

        /// <summary>The owner's current preview: what would fly if they fired now (null on other peers).</summary>
        public ShotPath Preview => _preview;

        /// <summary>Solves a shot from this player's spot at <paramref name="heading"/> and <paramref name="power"/>, as things stand.</summary>
        public ShotPath Solve(float heading, float power)
        {
            return BounceSolver.Solve(Arena.ToCore(Position), heading, power, Arena.Level, Arena.Targets(Session), Arena.Rules);
        }

        protected override void OnSpawned()
        {
            _body = Shapes.Square(gameObject, 0.8f, 0);
            var barrel = new GameObject("Barrel");
            barrel.transform.SetParent(transform, false);
            _barrel = Shapes.Square(barrel, 1f, 1);
            barrel.transform.localScale = new Vector3(0.9f, 0.18f, 1f);

            if (IsServer && Position == Vector2.zero)
            {
                Position = Arena.SpawnPoint(Owner.HasSeat ? Owner.SeatIndex : 0);
            }

            _seenShots = Shots; // a late joiner does not replay a shot fired before it arrived
            _shotView = ShotView.Create("Shot (" + name + ")");
            if (IsOwner && !NetBots.IsBot)
            {
                _aim.Set(Heading, Power);
                _previewLine = Lines.Create(_shotView.transform, "Preview", new UnityEngine.Color(1f, 1f, 1f, 0.45f), 0.05f, 2);
            }
        }

        protected override void OnDespawned()
        {
            if (_shotView != null)
            {
                Destroy(_shotView.gameObject);
            }
        }

        // Server: a new barrel direction. Never trusted as-is: NaN or an out-of-range power would be garbage on every peer.
        private void OnAim(float heading, float power)
        {
            Heading = (float)Angles.Normalize(float.IsNaN(heading) ? Heading : heading);
            Power = (float)ShotRules.ClampPower(power);
        }

        // Server: fire. One shot in the air per player; the aim is clamped like Aim's, then the shot is solved here.
        private void OnFire(float heading, float power)
        {
            if (_serverFlight != null && !_serverFlight.Landed)
            {
                return;
            }

            OnAim(heading, power);
            ShotHeading = Heading;
            ShotPower = Power;
            Shots = Shots + 1;
            _seenShots = Shots; // launched here, not by Update, so a second Fire in the same frame finds it in the air
            LaunchShot();
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            transform.position = Position;

            if (IsOwner && !NetBots.IsBot) // a bot's driver calls Fire itself (GameBot.cs)
            {
                OwnerInput();
            }

            if (Shots != _seenShots)
            {
                _seenShots = Shots;
                LaunchShot();
            }

            if (IsServer && _serverFlight != null && !_serverFlight.Landed)
            {
                _serverFlight.Advance(Time.deltaTime);
                if (_serverFlight.Landed && _serverFlight.Path.End == ShotEndKind.Target)
                {
                    Arena.Current?.Knock(_serverFlight.Path.TargetId);
                }
            }

            float shownHeading = IsOwner && !NetBots.IsBot ? (float)_aim.Heading : Heading;
            _barrel.transform.localRotation = Quaternion.Euler(0f, 0f, shownHeading);
            _barrel.transform.localPosition = Quaternion.Euler(0f, 0f, shownHeading) * new Vector3(0.45f, 0f, 0f);
            _body.color = Color;
            _barrel.color = Color;
        }

        private void OwnerInput()
        {
            bool changed = _aim.Step(GameInput.Turn(), GameInput.Charge(), Time.deltaTime, Arena.Rules);
            float heading = (float)_aim.Heading;
            float power = (float)_aim.Power;
            if ((heading != _sentHeading || power != _sentPower) && Time.unscaledTime - _aimSentAt >= 0.05f)
            {
                _sentHeading = heading;
                _sentPower = power;
                _aimSentAt = Time.unscaledTime;
                Aim(heading, power);
            }

            if (GameInput.Fire() && !ShotInFlight)
            {
                Fire(heading, power);
            }

            // The preview: the whole path as it would fly now, hidden while the shot is in the air.
            if (changed || _preview == null || Time.frameCount % 10 == 0)
            {
                _preview = Solve(heading, power);
            }

            if (_previewLine != null)
            {
                if (ShotInFlight)
                {
                    _previewLine.positionCount = 0;
                }
                else
                {
                    _previewLine.positionCount = _preview.Points.Count + 1;
                    _previewLine.SetPosition(0, Position);
                    for (int i = 0; i < _preview.Points.Count; i++)
                    {
                        _previewLine.SetPosition(i + 1, Arena.ToUnity(_preview.Points[i]));
                    }
                }
            }
        }

        // Every peer: draw the shot the server just took. The server's own solve is the one that lands.
        private void LaunchShot()
        {
            ShotPath path = Solve(ShotHeading, ShotPower);
            LastShot = path;
            if (IsServer)
            {
                _serverFlight = new ProjectileFlight(path, Arena.Rules);
            }

            if (_shotView != null)
            {
                _shotView.Launch(path, Color);
            }
        }

        // The name under the square, and for the owner draw_bounceinfo's line: angle, power and "Bounces: n/max".
        // IMGUI keeps the template free of fonts and canvases; swap in MindAttic Components widgets with the HUD (UR-T8).
        private void OnGUI()
        {
            Camera camera = Camera.main;
            if (!IsSpawned || camera == null)
            {
                return;
            }

            Vector3 screen = camera.WorldToScreenPoint(transform.position + Vector3.down * 0.55f);
            var label = new Rect(screen.x - 80f, Screen.height - screen.y, 160f, 22f);
            var centered = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter };
            GUI.Label(label, DisplayName + (IsOwner ? " (you)" : string.Empty), centered);

            if (IsOwner && !NetBots.IsBot)
            {
                ShotPath shown = ShotInFlight && _shotView != null ? _shotView.Flight.Path : _preview;
                int bounces = ShotInFlight && _shotView != null ? _shotView.Flight.Contacts : shown != null ? shown.Contacts.Count : 0;
                string end = shown == null ? string.Empty
                    : shown.End == ShotEndKind.Target ? "  -> HIT"
                    : shown.End == ShotEndKind.Spent ? "  -> falls short" : string.Empty;
                GUI.Label(new Rect(12f, Screen.height - 34f, 600f, 24f),
                    "Angle " + Mathf.RoundToInt((float)_aim.Heading) + "°   Power " + Mathf.RoundToInt((float)_aim.Power * 100f) + "%   Bounces: "
                    + bounces + "/" + Arena.Rules.MaxBounces + end + "   (Left/Right aim, Up/Down power, Space fire)");
            }
        }

        private bool Roster(out NetPlayer player)
        {
            player = null;
            return Session != null && Session.Roster != null && Session.Roster.TryGet(OwnerClientId, out player);
        }
    }
}
