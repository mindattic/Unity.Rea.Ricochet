// The player: one per connection, spawned by the NetworkManager as its Player Prefab, standing at its seat's spot; or an
// AI opponent, the same thing spawned by the server under a key ("ai:1", from the Opponent prefab) when a match starts.
// The owner turns an aim and charges a power (AimState) and sees the shot's whole bounce path before firing; Fire
// sends the aim to the server, which is the one that decides (UR-LAW-1): it clamps the input, solves the shot with
// Core's BounceSolver, flies it, and knocks down whatever it lands on. It sends every client the volley it solved
// (UR-T12: ShotResolved, packed by Core's ShotWire), so every peer draws the server's own bounce rather than solving it
// again in a level that may not match the server's that frame, and once it lands in a match what it did (ShotResult:
// Core's ShotOutcome, the damage each seat took), which the struck players float over themselves; nothing per-frame
// goes over the wire. The name and colour live in the session's
// roster (NetRoster), which every peer has. In a match (UR-T4) the server gives each player a seat, refuses a shot
// out of turn, and keeps their health and whether they are still in the round; Arena runs the turns.
// Weapons and items (UR-T5): the server keeps each player's Loadout (Core) and mirrors it packed into two [Synced] ints;
// the owner cycles the weapon (Q/E) and item (Z/C), sets a grenade's fuse (1/2/3) and uses the item (F) through
// [ToServer] calls the server checks like a shot (only on your turn, before you fire). A shot fires the selected weapon
// (Core's Arsenal: cannon, laser, shotgun, grenade, railgun) and spends one in a match (practice is free fire); its
// weapon, fuse and extra bounces are synced with the aim so every peer flies the same volley. The items: an extra
// bounce for this turn's shot, a teleporter that moves you to where it lands, and a shrink ray that shrinks the first
// player it reaches for the rest of the round. Aim assist (UR-T7) is the AI's targeting offered to a human: the server
// turns their aim onto the shot a fully skilled AI would take (Core's Ai.Assist), the owner's aim follows, and every
// peer draws that path as a fading beam (obj_aimassist) until the shot is fired. An AI seat aims (ServerAim), picks its
// weapon (ServerSelectWeapon) and fires through the same server calls; Arena plays its turn.
// Level hazards (UR-T6): every shot, preview and item flight is solved in the level as this player's shot sees it
// (Arena.LevelFor: the hazards on it, less this player's own shield walls). The shield wall item stands a wall in front
// of you and the redirector item sets a redirector down where it lands (both Arena's HazardField); fire again while your
// vortex is in the air to set it off there (Detonate, vortex_detonate.gml): the server cuts the volley short and syncs
// the distance it went off at, so every peer cuts its own flight there too.
// The HUD (UR-T8): the server mirrors each player's match score; the weapon and item the player whose turn it is carries
// are shown to everyone by MatchHud's turn card (draw_weaponinfo / draw_iteminfo), so this draws only the name, the
// pickup text, the small health bar and, for its owner, draw_bounceinfo's aim line.
// The profile (UR-T10): a human's name and colour are the ones their MindAttic profile (Unity.Shared) holds; the kit's
// front end sends them with the connection, so they reach the roster this reads. A shot's fire, bounces and landing
// play through GameAudio at the profile's effect volume.
// Reconnects (UR-T13): a dropped human's Player waits with their seat (the kit hands it to the server, then back to them
// when they reconnect in time), marked Away: nobody plays it meanwhile, not even a host it now belongs to, and its name
// and colour stay theirs because the roster is read by the connection seat it was spawned for, not by its current owner.
using System.Collections.Generic;
using Ricochet.Core;          // AimState, Arsenal, Loadout, Volley, VolleyFlight, ShotPath, MatchPhase, Ai
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

        // client -> server: fire the selected weapon at this aim. The shot carries its own aim, so a dropped Aim never
        // changes what flies.
        [ToServer(RateLimit = 4)]
        public partial void Fire(float heading, float power);

        // client -> server: select the next weapon (+1, cycle_weapon_right) or the one before (-1, cycle_weapon_left).
        [ToServer(RateLimit = 10)]
        public partial void CycleWeapon(int direction);

        // client -> server: select the next item (+1) or the one before (-1).
        [ToServer(RateLimit = 10)]
        public partial void CycleItem(int direction);

        // client -> server: the grenade fuse keys: -1 shorter, +1 longer, +2 the longest.
        [ToServer(RateLimit = 10)]
        public partial void Fuse(int change);

        // client -> server: use the selected item at this aim (a teleporter and a shrink ray fly along it).
        [ToServer(RateLimit = 4)]
        public partial void UseItem(float heading, float power);

        // client -> server: set off this player's vortex where it is now (vortex_detonate.gml: fire again after firing it).
        [ToServer(RateLimit = 4)]
        public partial void Detonate();

        // client -> server (UR-T23): R, PageUp or PageDown in practice: roll another random level (randomize_level.gml).
        [ToServer(RateLimit = 2)]
        public partial void Reroll();

        // server -> every client (UR-T12): the volley the server solved for shot number `shot` (Core's ShotWire), so every
        // peer flies the server's own bounce. Sent again, for the same shot, with the volley cut short when a vortex is set
        // off in the air.
        [ToClients]
        public partial void ShotResolved(int shot, double[] numbers, string ids);

        // server -> every client (UR-T12): what shot number `shot` did in a match once it landed (Core's ShotOutcome): the
        // damage each seat it struck took, and the seats it put out of the round.
        [ToClients]
        public partial void ShotResult(int shot, int[] seats, int[] damage, int[] knockedOut);

        // The level in play (UR-T23): the map, the seed its random round was built from, and a count that goes up by one per
        // change, so a peer adopts the newest and every one lays the identical round (Arena.AdoptLayout). Every Player
        // carries the same three, so a late joiner learns the level whichever Player it sees first, practice included.
        [Synced] private int _levelMap;
        [Synced] private int _levelSeed;
        [Synced] private int _levelVersion;

        // Only the server writes these; every peer, late joiners included, reads the same values.
        [Synced] private Vector2 _position;
        [Synced] private float _heading = StartHeading;
        [Synced] private float _power = StartPower;

        // The last shot: its aim, weapon, fuse and extra bounces, and a count that goes up by one per shot so every peer
        // knows a new one was fired.
        [Synced] private int _shots;
        [Synced] private float _shotHeading;
        [Synced] private float _shotPower;
        [Synced] private int _shotWeapon;
        [Synced] private float _shotFuse = 2f;
        [Synced] private int _shotExtraBounces;

        // How far from the muzzle the last shot (a vortex) was set off; 0 when it was not.
        [Synced] private float _shotDetonation;

        // The match: the seat (turn order and spot; -1 for none), health, and whether still in the round.
        [Synced] private int _seat = -1;
        [Synced] private int _health = StartHealth;
        [Synced] private bool _inRound;

        // Reconnects (UR-T13): the kit seat a human's connection sits in (kept while they are away, when the server owns
        // this object), and whether their connection has dropped and the seat is held for them.
        [Synced] private int _connectionSeat = -1;
        [Synced] private bool _away;

        // Points scored this match (UR-T8: hit.gml's pScore), carried from round to round.
        [Synced] private int _score;

        // The loadout (Loadout.PackWeapons / PackItems; 0 until the server first writes it), the extra bounces used this
        // turn, and the size a shrink ray left this player at (1 = full size).
        [Synced] private int _weapons;
        [Synced] private int _items;
        [Synced] private int _extraBounces;
        [Synced] private float _scale = 1f;

        // The last crate collected (kind * 1000 + what it gave: a weapon or item index, or the health added), and a count
        // that goes up by one per crate so every peer can float its "Laser x 1 collected" over the player.
        [Synced] private int _pickupCount;
        [Synced] private int _lastPickup;

        // The last aim assist: the aim the server turned this player onto, and a count that goes up by one per use so the
        // owner's aim follows it and every peer draws its beam.
        [Synced] private int _assists;
        [Synced] private float _assistHeading;
        [Synced] private float _assistPower;

        private readonly AimState _aim = new AimState(StartHeading, StartPower); // owner only: the local, responsive aim
        private float _aimSentAt = -1f;
        private float _sentHeading = float.NaN;
        private float _sentPower = float.NaN;

        private Loadout _loadout;                 // server only: the real loadout
        private Loadout _shownLoadout;            // every other peer: the synced one, unpacked
        private int _shownWeapons = -1;
        private int _shownItems = -1;

        private const float PickupSeconds = 2.5f;

        /// <summary>Seconds an aim-assist beam takes to fade (obj_aimassist lost 0.003 of its alpha a frame at 60).</summary>
        public const float AssistSeconds = 5.5f;

        private int _seenAssists;        // every peer: the last assist whose beam it drew
        private int _adoptedAssists;     // owner: the last assist its local aim followed
        private float _assistShownUntil = -1f;
        private LineRenderer _assistLine;
        private int _seenPickups;
        private float _pickupShownUntil = -1f;

        private VolleyFlight _serverFlight; // server only: the volley it is flying, to land the impact
        private int _seenShots;             // every peer: the last shot it has drawn
        private Volley _preview;            // owner only: the shot as it would fly right now

        private const float HurtSeconds = 1.6f;
        private ShotOutcome _pendingOutcome;  // a client: the server's result for the shot this peer is still flying
        private int _hurt;                    // every peer: the damage the last shot that struck this player did
        private float _hurtShownUntil = -1f;

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

        /// <summary>
        /// A human's connection seat (the kit's), which orders the table when a match starts: the one this player object
        /// was spawned for, kept while they are away (UR-T13).
        /// </summary>
        public int KitSeat
        {
            get
            {
                if (IsAi)
                {
                    return int.MaxValue;
                }

                return ConnectionSeat >= 0 ? ConnectionSeat : Owner.HasSeat ? Owner.SeatIndex : int.MaxValue;
            }
        }

        /// <summary>The name: the owner's, from the roster ("Player" until the roster has it), or "AI n"; a name another
        /// player in the match also has gets a number ("Default 2", UR-T21).</summary>
        public string DisplayName => Look().Name;

        /// <summary>The colour: the owner's, from the roster (their pick, or their seat's palette colour); an AI's seat colour;
        /// a colour another player also has is swapped for one nobody has (UR-T21).</summary>
        public Color32 Color
        {
            get
            {
                int rgb = Look().Rgb;
                return new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
            }
        }

        private string RawName()
        {
            if (IsAi)
            {
                string key = _key.Key ?? string.Empty;
                return "AI " + (key.StartsWith(Arena.AiKeyPrefix, System.StringComparison.Ordinal) ? key.Substring(Arena.AiKeyPrefix.Length) : key);
            }

            return Roster(out NetPlayer player) ? player.DisplayName : "Player";
        }

        private int RawRgb()
        {
            Color32 c = IsAi ? PlayerColors.ForSeat(Mathf.Max(0, Seat)).ToColor32()
                : Roster(out NetPlayer player) ? player.UnityColor() : new Color32(255, 255, 255, 255);
            return (c.r << 16) | (c.g << 8) | c.b;
        }

        // The match's players resolved apart, once a frame per session: every peer sorts by the same keys.
        private static NetSession s_lookSession;
        private static int s_lookFrame = -1;
        private static int s_lookCount = -1;
        private static readonly Dictionary<Player, PlayerLook> s_looks = new Dictionary<Player, PlayerLook>();

        private PlayerLook Look()
        {
            int count = s_spawned.Count;
            if (s_lookFrame != Time.frameCount || s_lookSession != Session || s_lookCount != count)
            {
                var all = new List<Player>(In(Session));
                all.Sort((x, y) =>
                {
                    int c = x.KitSeat.CompareTo(y.KitSeat);
                    if (c == 0)
                    {
                        c = x.Seat.CompareTo(y.Seat);
                    }

                    return c != 0 ? c : x.OwnerClientId.CompareTo(y.OwnerClientId);
                });
                var wanted = new List<PlayerLook>(all.Count);
                foreach (Player p in all)
                {
                    wanted.Add(new PlayerLook(p.RawName(), p.RawRgb()));
                }

                PlayerLook[] resolved = PlayerLooks.Resolve(wanted);
                s_looks.Clear();
                for (int i = 0; i < all.Count; i++)
                {
                    s_looks[all[i]] = resolved[i];
                }

                s_lookFrame = Time.frameCount;
                s_lookSession = Session;
                s_lookCount = count;
            }

            return s_looks.TryGetValue(this, out PlayerLook look) ? look : new PlayerLook(RawName(), RawRgb());
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

        /// <summary>The hit radius: a seat's full radius, less after a shrink ray.</summary>
        public double Radius => Seating.PlayerRadius * Mathf.Clamp(Scale, 0.05f, 1f);

        /// <summary>
        /// What this player carries: on the server the real loadout; on every other peer the synced copy (read-only in
        /// spirit: changing it changes nothing but this peer's view).
        /// </summary>
        public Loadout Loadout
        {
            get
            {
                if (IsServer)
                {
                    return ServerLoadout;
                }

                if (_shownLoadout == null || Weapons != _shownWeapons || Items != _shownItems)
                {
                    _shownLoadout = _shownLoadout ?? new Loadout(Arena.ArsenalRules);
                    if (Weapons == 0)
                    {
                        _shownLoadout.Reset();
                    }
                    else
                    {
                        _shownLoadout.UnpackFrom(Weapons, Items);
                    }

                    _shownWeapons = Weapons;
                    _shownItems = Items;
                }

                return _shownLoadout;
            }
        }

        private Loadout ServerLoadout
        {
            get { return _loadout ?? (_loadout = new Loadout(Arena.ArsenalRules)); }
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

        /// <summary>Server: how far the shot in the air (its first projectile) has flown from the muzzle; 0 with none.</summary>
        public double ShotTravelled
        {
            get { return IsServer && _serverFlight != null && _serverFlight.Flights.Count > 0 ? _serverFlight.Flights[0].Travelled : 0; }
        }

        /// <summary>The main path of the shot this peer last saw fired (null before the first).</summary>
        public ShotPath LastShot => LastVolley != null ? LastVolley.Main : null;

        /// <summary>The whole volley this peer last saw fired (null before the first).</summary>
        public Volley LastVolley { get; private set; }

        /// <summary>The shot number <see cref="LastVolley"/> is (the server's <see cref="Shots"/> count when it was fired).</summary>
        public int LastVolleyShot { get; private set; }

        /// <summary>What this player's last landed shot did in a match, as the server sent it (null before the first).</summary>
        public ShotOutcome LastOutcome { get; private set; }

        /// <summary>The shot number <see cref="LastOutcome"/> is about.</summary>
        public int LastOutcomeShot { get; private set; }

        /// <summary>True while this peer is drawing an aim-assist beam for this player.</summary>
        public bool AssistShown => _assistShownUntil > Time.time;

        /// <summary>The owner's current preview: what would fly if they fired now (null on other peers).</summary>
        public Volley Preview => _preview;

        /// <summary>Solves a cannon shot from this player's spot at <paramref name="heading"/> and <paramref name="power"/>, as things stand.</summary>
        public ShotPath Solve(float heading, float power)
        {
            return SolveVolley(Weapon.Cannon, heading, power, Arena.ArsenalRules.DefaultFuse, 0).Main;
        }

        /// <summary>Solves what <paramref name="weapon"/> would do from this player's spot, as things stand.</summary>
        public Volley SolveVolley(Weapon weapon, float heading, float power, double fuse, int extraBounces)
        {
            return Arsenal.Fire(weapon, Arena.ToCore(Position), heading, power, fuse, extraBounces, ShotLevel, Arena.Targets(Session),
                Arena.Rules, Arena.ArsenalRules);
        }

        /// <summary>The level this player's shots fly in now: the hazards on it, less their own shield walls (UR-T6).</summary>
        public Level ShotLevel => Arena.LevelFor(Session, Seat);

        protected override void OnSpawned()
        {
            _body = Shapes.Square(gameObject, 0.8f, 0);
            GameObject barrel = Child("Barrel"); // the same ones again when the kit respawns this object on a reconnect
            _barrel = Shapes.Square(barrel, 1f, 1);
            barrel.transform.localScale = new Vector3(0.9f, 0.18f, 1f);
            GameObject marker = Child("Turn marker");
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

            // The connection seat this object is for: its owner's now (a first spawn, or the kit handing it back on a
            // reconnect), so it still names them after a drop, when the server owns it.
            if (IsServer && !IsAi && Owner.HasSeat)
            {
                ConnectionSeat = Owner.SeatIndex;
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

            if (IsServer && Arena.LevelVersion > 0)
            {
                MirrorLevel(Arena.MapIndex, Arena.LevelSeed, Arena.LevelVersion);
            }

            if (IsServer)
            {
                MirrorLoadout();
            }

            _seenShots = Shots; // a late joiner does not replay a shot fired before it arrived
            _seenPickups = PickupCount;
            _seenAssists = Assists;
            _adoptedAssists = Assists;
            _shotView = ShotView.Create("Shot (" + name + ")");
            _shotView.Bounced += _ => GameAudio.Play(SoundCue.Bounce);
            _shotView.Landed += volley =>
            {
                GameAudio.Play(volley.HasBlast ? SoundCue.Blast : SoundCue.Land);
                if (_pendingOutcome != null) // the server's result came in while this peer was still flying the shot
                {
                    ShotOutcome outcome = _pendingOutcome;
                    _pendingOutcome = null;
                    ShowOutcome(outcome);
                }
            };
            _assistLine = Lines.Create(_shotView.transform, "Aim assist", new UnityEngine.Color(0.55f, 0.95f, 1f, 1f), 0.08f, 3);
            if (LocallyControlled)
            {
                _aim.Set(Heading, Power);
                _previewLine = Lines.Create(_shotView.transform, "Preview", new UnityEngine.Color(1f, 1f, 1f, 0.45f), 0.05f, 2);
            }
        }

        private GameObject Child(string childName)
        {
            Transform existing = transform.Find(childName);
            if (existing != null)
            {
                return existing.gameObject;
            }

            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            return child;
        }

        protected override void OnDespawned()
        {
            s_spawned.Remove(this);
            if (_shotView != null)
            {
                Destroy(_shotView.gameObject);
            }
        }

        /// <summary>Server: copies the loadout out to the synced ints every peer reads.</summary>
        public void MirrorLoadout()
        {
            if (!IsServer)
            {
                return;
            }

            Weapons = ServerLoadout.PackWeapons();
            Items = ServerLoadout.PackItems();
        }

        /// <summary>Server: this player collected a crate of <paramref name="kind"/> that gave <paramref name="value"/>.</summary>
        public void Collected(DropKind kind, int value)
        {
            if (!IsServer)
            {
                return;
            }

            LastPickup = (int)kind * 1000 + Mathf.Clamp(value, 0, 999);
            PickupCount = PickupCount + 1;
        }

        /// <summary>
        /// add_random_weapon / item / health's floating text for a pickup code: "Grenade x 1 collected",
        /// "Teleporter x 1 collected", "+7".
        /// </summary>
        public static string PickupText(int code)
        {
            var kind = (DropKind)Mathf.Clamp(code / 1000, 0, DropRules.KindCount - 1);
            int value = code % 1000;
            switch (kind)
            {
                case DropKind.Health:
                    return "+" + value;
                case DropKind.Item:
                    return ArsenalRules.Name((Item)Mathf.Clamp(value, 0, ArsenalRules.Kinds - 1)) + " x 1 collected";
                default:
                    return ArsenalRules.Name((Weapon)Mathf.Clamp(value, 0, ArsenalRules.Kinds - 1)) + " x 1 collected";
            }
        }

        /// <summary>Server: a fresh round: the starting loadout, full size, no extra bounces.</summary>
        public void ResetForRound()
        {
            if (!IsServer)
            {
                return;
            }

            ServerLoadout.Reset();
            MirrorLoadout();
            Scale = 1f;
            ExtraBounces = 0;
        }

        // Server: a new barrel direction. Never trusted as-is: NaN or an out-of-range power would be garbage on every peer.
        private void OnAim(float heading, float power)
        {
            Heading = (float)Angles.Normalize(float.IsNaN(heading) ? Heading : heading);
            Power = (float)ShotRules.ClampPower(power);
        }

        /// <summary>Server: turns the barrel for an AI seat (or a test), clamped like a client's Aim.</summary>
        public void ServerAim(float heading, float power)
        {
            if (IsServer)
            {
                OnAim(heading, power);
            }
        }

        /// <summary>
        /// Server: cycles right until <paramref name="weapon"/> is selected, for an AI seat (or a test). False when the
        /// player has none of it (or may not cycle now); the selection is then wherever the cycling stopped.
        /// </summary>
        public bool ServerSelectWeapon(Weapon weapon)
        {
            if (!IsServer)
            {
                return false;
            }

            for (int i = 0; i < ArsenalRules.Kinds && ServerLoadout.Weapon != weapon; i++)
            {
                OnCycleWeapon(1);
            }

            return ServerLoadout.Weapon == weapon;
        }

        /// <summary>Server: fires for an AI seat (or a test), through the same checks as a client's Fire.</summary>
        public void ServerFire(float heading, float power)
        {
            if (IsServer)
            {
                OnFire(heading, power);
            }
        }

        /// <summary>Server: cycles the weapon for an AI seat (or a test), through the same checks as a client's.</summary>
        public void ServerCycleWeapon(int direction)
        {
            if (IsServer)
            {
                OnCycleWeapon(direction);
            }
        }

        /// <summary>Server: sets off this player's vortex for a test, through the same checks as a client's.</summary>
        public void ServerDetonate()
        {
            if (IsServer)
            {
                OnDetonate();
            }
        }

        /// <summary>Server: uses the selected item for an AI seat (or a test), through the same checks as a client's.</summary>
        public void ServerUseItem(float heading, float power)
        {
            if (IsServer)
            {
                OnUseItem(heading, power);
            }
        }

        // Server: may this player cycle, set the fuse or use an item now? As for a shot: their turn (or practice), and
        // nothing of theirs in the air (cycle_weapon_left's "turnStarted && !hasFired").
        private bool MayAct()
        {
            if (_serverFlight != null && !_serverFlight.Landed)
            {
                return false;
            }

            Arena arena = Arena.Current;
            return arena == null || arena.MayFire(this);
        }

        // Practice is free fire: nothing is spent.
        private static bool Practising
        {
            get
            {
                Arena arena = Arena.Current;
                return arena == null || !arena.MatchRunning;
            }
        }

        // Server: fire. One volley in the air per player, and in a match only on your turn (Arena) and only with a weapon
        // you have (fire_weapon.gml); the aim is clamped like Aim's, then the volley is solved here.
        private void OnFire(float heading, float power)
        {
            if (!MayAct())
            {
                return;
            }

            Loadout loadout = ServerLoadout;
            Weapon weapon;
            if (Practising)
            {
                if (!loadout.Has(loadout.Weapon))
                {
                    return;
                }

                weapon = loadout.Weapon;
            }
            else
            {
                Weapon? fired = loadout.Fire();
                if (fired == null)
                {
                    return;
                }

                weapon = fired.Value;
            }

            OnAim(heading, power);
            ShotHeading = Heading;
            ShotPower = Power;
            ShotWeapon = (int)weapon;
            ShotFuse = (float)loadout.Fuse;
            ShotExtraBounces = ExtraBounces;
            ShotDetonation = 0f;
            ExtraBounces = 0;
            MirrorLoadout();
            Shots = Shots + 1;
            _seenShots = Shots; // a second Fire in the same frame finds this one in the air
            LaunchShot();
            Arena.Current?.Fired(this);
        }

        /// <summary>Server: writes the level in play for the clients to adopt (see <see cref="Arena.AdoptLayout"/>).</summary>
        internal void MirrorLevel(int map, int seed, int version)
        {
            if (!IsServer)
            {
                return;
            }

            LevelMap = map;
            LevelSeed = seed;
            LevelVersion = version;
        }

        // Server: R / PageUp / PageDown in practice: a new random level, everyone to fresh spots. Ignored in a match.
        private void OnReroll()
        {
            Arena.Current?.RerollLevel();
        }

        // Server: vortex_detonate.gml. Only this player's own vortex, only while it is still in the air, and only once: the
        // volley is cut short where it is now and lands at once (the implosion strikes, swallows and breaches there).
        private void OnDetonate()
        {
            if (_serverFlight == null || _serverFlight.Landed || _serverFlight.Volley.Weapon != Weapon.Vortex || ShotDetonation > 0f)
            {
                return;
            }

            float at = Mathf.Max(0.001f, (float)_serverFlight.Flights[0].Travelled);
            Volley cut = Arsenal.Detonate(_serverFlight.Volley, at, Arena.Targets(Session), Arena.ArsenalRules);
            if (ReferenceEquals(cut, _serverFlight.Volley))
            {
                return;
            }

            ShotDetonation = at;
            LastVolley = cut;
            if (_shotView != null)
            {
                _shotView.Detonate(cut);
            }

            SendResolved(Shots, cut); // every client cuts its flight at the server's own cut, striking whom it struck
            _serverFlight = new VolleyFlight(cut);
            _serverFlight.Advance(cut.Duration + 1.0);
            Arena.Current?.Landed(this, cut);
        }

        private void OnCycleWeapon(int direction)
        {
            if (MayAct() && ServerLoadout.CycleWeapon(System.Math.Sign(direction)))
            {
                MirrorLoadout();
            }
        }

        private void OnCycleItem(int direction)
        {
            if (MayAct() && ServerLoadout.CycleItem(System.Math.Sign(direction)))
            {
                MirrorLoadout();
            }
        }

        private void OnFuse(int change)
        {
            if (!MayAct())
            {
                return;
            }

            Loadout loadout = ServerLoadout;
            if (change < 0)
            {
                loadout.ShortenFuse();
            }
            else if (change == 1)
            {
                loadout.LengthenFuse();
            }
            else if (change >= 2)
            {
                loadout.MaxOutFuse();
            }

            MirrorLoadout();
        }

        // Server: use_item.gml. The selected item, if carried and in play, before this turn's shot.
        private void OnUseItem(float heading, float power)
        {
            if (!MayAct())
            {
                return;
            }

            Loadout loadout = ServerLoadout;
            Item item = loadout.Item;
            if (!Arena.ArsenalRules.InPlay(item) || !loadout.Has(item))
            {
                return;
            }

            if (!Practising)
            {
                loadout.Use();
            }

            OnAim(heading, power);
            switch (item)
            {
                case Item.ExtraBounce:
                    ExtraBounces = ExtraBounces + 1;
                    break;
                case Item.Teleporter:
                    Vec2 spot = Arsenal.Teleport(Arena.ToCore(Position), Heading, Power, ShotLevel, Arena.Rules, Radius, out _);
                    Position = Arena.ToUnity(spot);
                    break;
                case Item.ShieldWall:
                    Arena.Current?.PlaceShieldWall(this); // spawn_shieldwall.gml
                    break;
                case Item.Redirector:
                    Vec2 placed = Arsenal.PlaceRedirector(Arena.ToCore(Position), Heading, Power, ShotLevel, Arena.Rules, out double redirectorHeading, out _);
                    Arena.Current?.PlaceRedirector(this, placed, redirectorHeading);
                    break;
                case Item.ShrinkRay:
                    string hit = Arsenal.ShrinkRay(Arena.ToCore(Position), Heading, Arena.SeatTarget(Seat), ShotLevel, Arena.Targets(Session),
                        Arena.Rules, out _);
                    int seat = Arena.SeatOf(hit);
                    Arena arena = Arena.Current;
                    Player shrunk = seat >= 0 && arena != null ? arena.PlayerAt(seat) : null;
                    if (shrunk != null)
                    {
                        shrunk.Scale = Mathf.Min(shrunk.Scale, (float)Arena.ArsenalRules.ShrinkScale);
                    }

                    break;
                case Item.AimAssist:
                    // obj_aimassist, with the AI's targeting: onto the shot that reaches an opponent, if any does.
                    AiPlan assist = Ai.Assist(Arena.ToCore(Position), Arena.SeatTarget(Seat), ShotLevel, Arena.Targets(Session), Power, Arena.Rules);
                    if (assist != null)
                    {
                        OnAim((float)assist.Heading, (float)assist.Power);
                    }

                    AssistHeading = Heading;
                    AssistPower = Power;
                    Assists = Assists + 1;
                    break;
            }

            MirrorLoadout();
        }

        // A human on their own machine: not a bot (its driver fires for it) and not an AI (Arena plays its turns). A dropped
        // player's object belongs to the server while their seat is held, so a host owns it then, but does not play it.
        private bool LocallyControlled
        {
            get { return IsOwner && !IsAi && !NetBots.IsBot && !(IsServer && Away); }
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            transform.position = Position;
            transform.localScale = new Vector3(Scale, Scale, 1f) * 0.8f;

            // A client in a process of its own lays the server's level (a host's peers share its Arena).
            if (!IsServer && Arena.Current == null && LevelVersion > 0)
            {
                Arena.AdoptLayout(LevelMap, LevelSeed, LevelVersion);
            }

            if (LocallyControlled && GameInput.Reroll())
            {
                Reroll();
            }

            if (LocallyControlled) // a bot's driver calls Fire itself (GameBot.cs); Arena fires for an AI
            {
                OwnerInput();
            }

            // A shot reaches a client as the server's ShotResolved (OnShotResolved), never solved again here: the synced
            // Shots count only tells a late joiner which shots were fired before it arrived.

            if (PickupCount != _seenPickups)
            {
                _seenPickups = PickupCount;
                _pickupShownUntil = Time.time + PickupSeconds;
            }

            DrawAssist();

            if (IsServer && _serverFlight != null && !_serverFlight.Landed)
            {
                _serverFlight.Advance(Time.deltaTime);
                if (_serverFlight.Landed)
                {
                    Arena.Current?.Landed(this, _serverFlight.Volley); // dummies knocked down, players hurt, crates collected, the turn over
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
            // An aim assist the server just applied: the local aim follows it.
            bool assisted = Assists != _adoptedAssists;
            if (assisted)
            {
                _adoptedAssists = Assists;
                _aim.Set(AssistHeading, AssistPower);
            }

            bool changed = _aim.Step(GameInput.Turn(), GameInput.Charge(), Time.deltaTime, Arena.Rules) || assisted;
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
            bool mayAct = mayFire && !ShotInFlight;
            int cycleWeapon = GameInput.CycleWeapon();
            if (cycleWeapon != 0 && mayAct)
            {
                CycleWeapon(cycleWeapon);
            }

            int cycleItem = GameInput.CycleItem();
            if (cycleItem != 0 && mayAct)
            {
                CycleItem(cycleItem);
            }

            int fuse = GameInput.Fuse();
            if (fuse != 0 && mayAct && Loadout.Weapon == Weapon.Grenade)
            {
                Fuse(fuse);
            }

            if (GameInput.UseItem() && mayAct)
            {
                UseItem(heading, power);
            }

            if (GameInput.Fire() && mayAct)
            {
                Fire(heading, power);
            }
            else if (GameInput.Fire() && VortexInFlight)
            {
                Detonate(); // vortex_detonate.gml: fire again to set it off
            }

            // The preview: the whole path of the selected weapon as it would fly now, hidden while the shot is in the air.
            Loadout loadout = Loadout;
            if (changed || _preview == null || Time.frameCount % 10 == 0 || _preview.Weapon != loadout.Weapon)
            {
                _preview = SolveVolley(loadout.Weapon, heading, power, loadout.Fuse, ExtraBounces);
            }

            if (_previewLine != null)
            {
                if (ShotInFlight || !mayFire)
                {
                    _previewLine.positionCount = 0;
                }
                else
                {
                    ShotPath main = _preview.Main;
                    _previewLine.positionCount = main.Points.Count + 1;
                    _previewLine.SetPosition(0, Position);
                    for (int i = 0; i < main.Points.Count; i++)
                    {
                        _previewLine.SetPosition(i + 1, Arena.ToUnity(main.Points[i]));
                    }
                }
            }
        }

        // Every peer: obj_aimassist's beam along the assisted path, fading out, gone once the shot is fired.
        private void DrawAssist()
        {
            if (_assistLine == null)
            {
                return;
            }

            if (Assists != _seenAssists)
            {
                _seenAssists = Assists;
                _assistShownUntil = Time.time + AssistSeconds;
                ShotPath path = SolveVolley(Weapon.Cannon, AssistHeading, AssistPower, Arena.ArsenalRules.DefaultFuse, ExtraBounces).Main;
                _assistLine.positionCount = path.Points.Count + 1;
                _assistLine.SetPosition(0, Position);
                for (int i = 0; i < path.Points.Count; i++)
                {
                    _assistLine.SetPosition(i + 1, Arena.ToUnity(path.Points[i]));
                }
            }

            if (!AssistShown)
            {
                _assistLine.positionCount = 0;
                return;
            }

            float alpha = Mathf.Clamp01((_assistShownUntil - Time.time) / AssistSeconds);
            var color = new UnityEngine.Color(0.55f, 0.95f, 1f, alpha);
            _assistLine.startColor = color;
            _assistLine.endColor = color;
        }

        // This player's own vortex is in the air and may still be set off.
        private bool VortexInFlight
        {
            get { return ShotInFlight && LastVolley != null && LastVolley.Weapon == Weapon.Vortex && LastVolley.HasBlast && ShotDetonation <= 0f; }
        }

        // Server: solve the volley just fired, fly it, and send every client what was solved (UR-T12). The server's own solve
        // is the one that lands; no client works it out again.
        private void LaunchShot()
        {
            Volley volley = SolveVolley((Weapon)Mathf.Clamp(ShotWeapon, 0, ArsenalRules.Kinds - 1), ShotHeading, ShotPower, ShotFuse, ShotExtraBounces);
            _serverFlight = new VolleyFlight(volley);
            Draw(Shots, volley);
            SendResolved(Shots, volley);
        }

        // Server: ShotResolved for shot number `shot`.
        private void SendResolved(int shot, Volley volley)
        {
            ShotWire.Encode(volley, out double[] numbers, out string ids);
            ShotResolved(shot, numbers, ids ?? string.Empty);
        }

        // Every peer: draw a volley this player fired: its flight, the fire sound, and the aim-assist beam gone with it.
        private void Draw(int shot, Volley volley)
        {
            _assistShownUntil = -1f; // fire_weapon.gml: the aim-assist beam goes with the shot
            _pendingOutcome = null;
            LastVolley = volley;
            LastVolleyShot = shot;
            if (_shotView != null)
            {
                _shotView.Launch(volley, Color);
            }

            GameAudio.Play(SoundCue.Fire); // UR-T10: at the profile's effect volume
        }

        // A client: the server solved shot number `shot`. A new shot is drawn as the server solved it; the same shot again is
        // its vortex set off in the air, cut where the server cut it. (A host's own peer is the server: it drew it already.)
        private void OnShotResolved(int shot, double[] numbers, string ids)
        {
            if (IsServer)
            {
                return;
            }

            Volley volley = ShotWire.Decode(numbers, ids);
            if (volley == null)
            {
                Debug.LogWarning("[Ricochet] " + name + ": shot " + shot + " came over unreadable; nothing drawn.");
                return;
            }

            if (shot > _seenShots)
            {
                _seenShots = shot;
                Draw(shot, volley);
            }
            else if (shot == LastVolleyShot && LastVolley != null && volley.Main.End == ShotEndKind.Detonated)
            {
                LastVolley = volley;
                if (_shotView != null)
                {
                    _shotView.Detonate(volley);
                }
            }
        }

        /// <summary>
        /// Server: what this player's volley did in a match once it landed (Arena.Landed). Every peer gets it: the struck
        /// players float the damage they took over themselves once that peer's own flight lands.
        /// </summary>
        public void ServerResult(ShotOutcome outcome)
        {
            if (!IsServer || outcome == null)
            {
                return;
            }

            LastOutcome = outcome;
            LastOutcomeShot = Shots;
            outcome.Encode(out int[] seats, out int[] damage, out int[] knockedOut);
            ShotResult(Shots, seats, damage, knockedOut);
        }

        // Every peer (the host's included): the server's result for shot number `shot`.
        private void OnShotResult(int shot, int[] seats, int[] damage, int[] knockedOut)
        {
            ShotOutcome outcome = ShotOutcome.Decode(seats, damage, knockedOut, global::Ricochet.Core.MatchRules.MaxSeats);
            if (outcome == null)
            {
                Debug.LogWarning("[Ricochet] " + name + ": the result of shot " + shot + " came over unreadable.");
                return;
            }

            LastOutcome = outcome;
            LastOutcomeShot = shot;
            if (_shotView != null && _shotView.Flying && shot == LastVolleyShot)
            {
                _pendingOutcome = outcome; // shown when this peer's flight lands, not before the shot gets there
            }
            else
            {
                ShowOutcome(outcome);
            }
        }

        // Every peer: each player the shot struck floats the damage it took.
        private void ShowOutcome(ShotOutcome outcome)
        {
            foreach (Player player in In(Session))
            {
                int damage = player.Seat >= 0 ? outcome.DamageTo(player.Seat) : 0;
                if (damage > 0)
                {
                    player.ShowHurt(damage);
                }
            }
        }

        private void ShowHurt(int damage)
        {
            _hurt = damage;
            _hurtShownUntil = Time.time + HurtSeconds;
        }

        /// <summary>True for the human playing on this machine (not a bot, not an AI).</summary>
        public bool IsLocalHuman => IsSpawned && LocallyControlled;

        /// <summary>
        /// The number of bounces this player's shot has made so far on this peer while it flies (draw_potential's combo
        /// grows with them), or -1 when nothing of theirs is in the air.
        /// </summary>
        public int BouncesInFlight => ShotInFlight && _shotView != null && _shotView.Flight != null && _shotView.Flight.Flights.Count > 0
            ? _shotView.Flight.Flights[0].Contacts
            : -1;

        /// <summary>Seconds this player's shot has been in the air on this peer (0 when nothing is).</summary>
        public double SecondsInFlight => ShotInFlight && _shotView != null && _shotView.Flight != null && _shotView.Flight.Flights.Count > 0 && LastVolley != null
            ? _shotView.Flight.Flights[0].Travelled / LastVolley.Speed
            : 0;

        // The name under the square, and for the owner draw_bounceinfo's line (angle, power, "Bounces: n/max"). The weapon
        // and item are MatchHud's turn card (Components widgets); this stays IMGUI, drawn at each player's spot.
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
            GUI.Label(label, DisplayName + (LocallyControlled ? " (you)" : Away ? " (reconnecting...)" : string.Empty), centered);

            // show_float_text: what the last crate gave, rising over the player for a moment.
            if (Time.time < _pickupShownUntil)
            {
                float rise = (1f - (_pickupShownUntil - Time.time) / PickupSeconds) * 30f;
                GUI.Label(new Rect(screen.x - 120f, Screen.height - screen.y - 70f - rise, 240f, 22f), PickupText(LastPickup), centered);
            }

            // The server's result for a shot that struck this player (UR-T12): the damage it took, rising and fading.
            if (Time.time < _hurtShownUntil)
            {
                float left = (_hurtShownUntil - Time.time) / HurtSeconds;
                var hurt = new GUIStyle(centered) { fontStyle = FontStyle.Bold };
                hurt.normal.textColor = new UnityEngine.Color(1f, 0.3f, 0.25f, Mathf.Clamp01(left * 2f));
                GUI.Label(new Rect(screen.x - 60f, Screen.height - screen.y - 50f - (1f - left) * 24f, 120f, 22f), "-" + _hurt, hurt);
            }

            // In a match, a small health bar under the name (the roster's bars along the top are MatchHud's).
            if (Turns.For(Session) != null && Seat >= 0)
            {
                MatchHud.Bar(new Rect(screen.x - 30f, Screen.height - screen.y + 20f, 60f, 6f), Health / (float)Arena.MatchRules.MaxHealth, Color, InRound);
            }

            if (LocallyControlled)
            {
                bool flying = ShotInFlight && _shotView != null && _shotView.Flight != null;
                ShotPath shown = flying ? _shotView.Flight.Volley.Main : _preview != null ? _preview.Main : null;
                int bounces = flying ? _shotView.Flight.Flights[0].Contacts : shown != null ? shown.Contacts.Count : 0;
                string end = shown == null ? string.Empty
                    : shown.End == ShotEndKind.Absorbed ? "  -> blocked"
                    : shown.End == ShotEndKind.Escaped ? "  -> out through the breach"
                    : _preview != null && _preview.HasBlast ? string.Empty
                    : shown.End == ShotEndKind.Target ? "  -> HIT"
                    : shown.End == ShotEndKind.Spent ? "  -> falls short" : string.Empty;
                GUI.Label(new Rect(12f, Screen.height - 34f, 900f, 24f),
                    "Angle " + Mathf.RoundToInt((float)_aim.Heading) + "°   Power " + Mathf.RoundToInt((float)_aim.Power * 100f) + "%   Bounces: "
                    + bounces + "/" + (Arena.Rules.MaxBounces + ExtraBounces) + end + "   (Left/Right aim, Up/Down power, Space fire)");

                Loadout loadout = Loadout;

                // draw_vortexdetonate.gml: tell the player how to set the vortex off.
                if ((loadout.Weapon == Weapon.Vortex && Turns.MayFire(Session, Seat)) || VortexInFlight)
                {
                    var hint = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter };
                    hint.normal.textColor = new UnityEngine.Color(1f, 0.35f, 0.3f);
                    GUI.Label(new Rect(0f, Screen.height - 28f, Screen.width, 24f),
                        VortexInFlight ? "Press [SPACE] to detonate the Vortex Device" : "After you fire, press [SPACE] to detonate the Vortex Device", hint);
                }
            }
        }

        private bool Roster(out NetPlayer player)
        {
            player = null;
            if (Session == null || Session.Roster == null)
            {
                return false;
            }

            // By the connection seat when it is known (it outlives a drop); otherwise by the owner's connection.
            return (ConnectionSeat >= 0 && Session.Roster.TryGetBySeat(ConnectionSeat, out player)) || Session.Roster.TryGet(OwnerClientId, out player);
        }
    }
}
