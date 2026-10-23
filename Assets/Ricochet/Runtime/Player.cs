// The player: one per connection, spawned by the NetworkManager as its Player Prefab, standing at its seat's spot; or an
// AI opponent, the same thing spawned by the server under a key ("ai:1", from the Opponent prefab) when a match starts.
// The owner turns an aim and charges a power (AimState) and sees the shot's whole bounce path before firing; Fire
// sends the aim to the server, which is the one that decides (UR-LAW-1): it clamps the input, solves the shot with
// Core's BounceSolver, flies it, and knocks down whatever it lands on. Every peer replays the same flight from the
// synced shot inputs to draw it, so nothing per-frame goes over the wire. The name and colour live in the session's
// roster (NetRoster), which every peer has. In a match (UR-T4) the server gives each player a seat, refuses a shot
// out of turn, and keeps their health and whether they are still in the round; Arena runs the turns.
using System.Collections.Generic;
using Ricochet.Core;          // AimState, BounceSolver, ProjectileFlight, ShotPath, MatchPhase
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

        /// <summary>Health before the first match (Arena.MatchRules.MaxHealth; the server keeps it from then on).</summary>
        public const int StartHealth = 100;

        private static readonly List<Player> s_spawned = new List<Player>();

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

        // The match: the seat (turn order and spot; -1 for none), health, and whether still in the round.
        [Synced] private int _seat = -1;
        [Synced] private int _health = StartHealth;
        [Synced] private bool _inRound;

        private readonly AimState _aim = new AimState(StartHeading, StartPower); // owner only: the local, responsive aim
        private float _aimSentAt = -1f;
        private float _sentHeading = float.NaN;
        private float _sentPower = float.NaN;

        private ProjectileFlight _serverFlight; // server only: the shot it is flying, to land the impact
        private int _seenShots;                 // every peer: the last shot it has drawn
        private ShotPath _preview;              // owner only: the shot as it would fly right now

        private SpriteRenderer _body;
        private SpriteRenderer _barrel;
        private SpriteRenderer _turnMarker;
        private NetKey _key;
        private bool _keyLooked;
        private LineRenderer _previewLine;
        private ShotView _shotView;

        /// <summary>True for an AI opponent (spawned with a key by the server; a human's player object has none).</summary>
        public bool IsAi
        {
            get
            {
                if (!_keyLooked)
                {
                    _key = GetComponent<NetKey>();
                    _keyLooked = true;
                }

                return _key != null;
            }
        }

        /// <summary>A human's connection seat (the kit's), which orders the table when a match starts.</summary>
        public int KitSeat
        {
            get { return !IsAi && Owner.HasSeat ? Owner.SeatIndex : int.MaxValue; }
        }

        /// <summary>The name: the owner's, from the roster ("Player" until the roster has it), or "AI n".</summary>
        public string DisplayName
        {
            get
            {
                if (IsAi)
                {
                    string key = _key.Key ?? string.Empty;
                    return "AI " + (key.StartsWith(Arena.AiKeyPrefix, System.StringComparison.Ordinal) ? key.Substring(Arena.AiKeyPrefix.Length) : key);
                }

                return Roster(out NetPlayer player) ? player.DisplayName : "Player";
            }
        }

        /// <summary>The colour: the owner's, from the roster (their pick, or their seat's palette colour); an AI's seat colour.</summary>
        public Color32 Color
        {
            get
            {
                if (IsAi)
                {
                    return PlayerColors.ForSeat(Mathf.Max(0, Seat)).ToColor32();
                }

                return Roster(out NetPlayer player) ? player.UnityColor() : new Color32(255, 255, 255, 255);
            }
        }

        /// <summary>True when this is the player whose turn it is in a running match.</summary>
        public bool HasTurn
        {
            get
            {
                Turns turns = Turns.For(Session);
                return turns != null && Seat >= 0 && turns.Current == Seat
                    && (turns.MatchPhase == MatchPhase.Aiming || turns.MatchPhase == MatchPhase.ShotInFlight);
            }
        }

        /// <summary>Every spawned player <paramref name="session"/> has, humans and AI, in no particular order.</summary>
        public static IEnumerable<Player> In(NetSession session)
        {
            for (int i = 0; i < s_spawned.Count; i++)
            {
                Player player = s_spawned[i];
                if (player != null && player.IsSpawned && player.Session == session)
                {
                    yield return player;
                }
            }
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
            var marker = new GameObject("Turn marker");
            marker.transform.SetParent(transform, false);
            _turnMarker = Shapes.Square(marker, 1f, 2);
            marker.transform.localScale = new Vector3(0.3f, 0.3f, 1f);
            marker.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            marker.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            _turnMarker.color = new UnityEngine.Color(1f, 0.92f, 0.2f);
            _turnMarker.enabled = false;

            if (!s_spawned.Contains(this))
            {
                s_spawned.Add(this);
            }

            // Server: a human sits at their connection's seat in practice, or takes a free one mid-match (Arena). A player
            // object handed back to a reconnecting player keeps the seat it had. An AI's seat is set by Arena.
            if (IsServer && !IsAi && Seat < 0)
            {
                Arena arena = Arena.Current;
                if (arena != null && arena.MatchRunning)
                {
                    arena.Arrived(this);
                }
                else
                {
                    Seat = Owner.HasSeat ? Owner.SeatIndex : 0;
                }
            }

            if (IsServer && Position == Vector2.zero)
            {
                Position = Arena.SpawnPoint(Mathf.Max(0, Seat));
            }

            _seenShots = Shots; // a late joiner does not replay a shot fired before it arrived
            _shotView = ShotView.Create("Shot (" + name + ")");
            if (LocallyControlled)
            {
                _aim.Set(Heading, Power);
                _previewLine = Lines.Create(_shotView.transform, "Preview", new UnityEngine.Color(1f, 1f, 1f, 0.45f), 0.05f, 2);
            }
        }

        protected override void OnDespawned()
        {
            s_spawned.Remove(this);
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

        /// <summary>Server: fires for an AI seat (or a test), through the same checks as a client's Fire.</summary>
        public void ServerFire(float heading, float power)
        {
            if (IsServer)
            {
                OnFire(heading, power);
            }
        }

        // Server: fire. One shot in the air per player, and in a match only on your turn (Arena); the aim is clamped
        // like Aim's, then the shot is solved here.
        private void OnFire(float heading, float power)
        {
            if (_serverFlight != null && !_serverFlight.Landed)
            {
                return;
            }

            Arena arena = Arena.Current;
            if (arena != null && !arena.MayFire(this))
            {
                return;
            }

            OnAim(heading, power);
            ShotHeading = Heading;
            ShotPower = Power;
            Shots = Shots + 1;
            _seenShots = Shots; // launched here, not by Update, so a second Fire in the same frame finds it in the air
            LaunchShot();
            arena?.Fired(this);
        }

        // A human on their own machine: not a bot (its driver fires for it) and not an AI (Arena plays its turns).
        private bool LocallyControlled
        {
            get { return IsOwner && !IsAi && !NetBots.IsBot; }
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            transform.position = Position;

            if (LocallyControlled) // a bot's driver calls Fire itself (GameBot.cs); Arena fires for an AI
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
                if (_serverFlight.Landed)
                {
                    Arena.Current?.Landed(this, _serverFlight.Path); // a dummy knocked down, or a player hurt and the turn over
                }
            }

            float shownHeading = LocallyControlled ? (float)_aim.Heading : Heading;
            _barrel.transform.localRotation = Quaternion.Euler(0f, 0f, shownHeading);
            _barrel.transform.localPosition = Quaternion.Euler(0f, 0f, shownHeading) * new Vector3(0.45f, 0f, 0f);

            // Out of the round (no health left, or waiting for the next one): drawn faint.
            Color32 color = Color;
            color.a = Turns.For(Session) != null && !InRound ? (byte)60 : (byte)255;
            _body.color = color;
            _barrel.color = color;
            _turnMarker.enabled = HasTurn;
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

            bool mayFire = Turns.MayFire(Session, Seat);
            if (GameInput.Fire() && !ShotInFlight && mayFire)
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
                if (ShotInFlight || !mayFire)
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
            GUI.Label(label, DisplayName + (LocallyControlled ? " (you)" : string.Empty), centered);

            // In a match, a small health bar under the name (the roster's bars along the top are MatchHud's).
            if (Turns.For(Session) != null && Seat >= 0)
            {
                MatchHud.Bar(new Rect(screen.x - 30f, Screen.height - screen.y + 20f, 60f, 6f), Health / (float)Arena.MatchRules.MaxHealth, Color, InRound);
            }

            if (LocallyControlled)
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
