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
// pickup text, the small health bar (PlayerLabels) and, for its owner, draw_bounceinfo's aim line (MatchHud reads it).
// The profile (UR-T10): a human's name and colour are the ones their MindAttic profile (Unity.Shared) holds; the kit's
// front end sends them with the connection, so they reach the roster this reads. A shot's fire, bounces and landing
// play through GameAudio at the profile's effect volume.
// Reconnects (UR-T13): a dropped human's Player waits with their seat (the kit hands it to the server, then back to them
// when they reconnect in time), marked Away: nobody plays it meanwhile, not even a host it now belongs to, and its name
// and colour stay theirs because the roster is read by the connection seat it was spawned for, not by its current owner.
// Commands and events (UR-T34): every [ToServer] call above is turned, on the server, into a Core command carrying this
// player's id (AimCommand, FireCommand, UseItemCommand, DriveCommand ...) that the one GameAuthority applies; the aim, the
// loadout, the tank and the shot in the air are the authority's PlayerState, and what this object syncs to the peers is a
// copy of it (SyncFromAuthority). The authority answers with events, which Present plays: the shot drawn and sent, the result
// sent. A Player never changes the game another way, and never asks who "the current player" is.
using System.Collections.Generic;
using Ricochet.Core.Ai;
using Ricochet.Core.Aim;
using Ricochet.Core.Arsenal;
using Ricochet.Core.Level;
using Ricochet.Core.Match;
using Ricochet.Core.Menu;
using Ricochet.Core.Net;
using Unity.Multiplayer;      // NetEntity, NetPlayer, NetRoster, NetBots
using Unity.Multiplayer.Core; // [ToServer], [Synced]
using UnityEngine;
using Cmd = Ricochet.Core.Commands;

namespace Ricochet
{
    public partial class Player : NetEntity
    {
        /// <summary>The aim a new player starts with: straight up, half power.</summary>
        public const float StartHeading = 90f;
        public const float StartPower = 0.5f;

        /// <summary>Health before the first match (Arena.MatchRules.MaxHealth; the server keeps it from then on).</summary>
        public const int StartHealth = 100;

        private float _driveSentAt;

        private static readonly List<Player> s_spawned = new List<Player>();

        private NetKey _key;
        private bool _keyLooked;

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

        /// <summary>For an AI opponent, its number from 1 (the "n" of its "ai:n" key); 0 for a human.</summary>
        public int AiNumber
        {
            get
            {
                string key = _key.Key ?? string.Empty;
                return IsAi && key.StartsWith(Arena.AiKeyPrefix, System.StringComparison.Ordinal)
                    && int.TryParse(key.Substring(Arena.AiKeyPrefix.Length), out int number) ? number : 0;
            }
        }

        private string RawName()
        {
            if (IsAi)
            {
                string key = _key.Key ?? string.Empty;
                if (key.StartsWith(Arena.AiKeyPrefix, System.StringComparison.Ordinal) && int.TryParse(key.Substring(Arena.AiKeyPrefix.Length), out int number))
                {
                    Turns turns = Turns.For(Session);
                    return turns != null ? turns.AiName(number) : "AI " + number; // the match's names ride on Turns
                }

                return "AI " + key;
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
                    && (turns.MatchPhase == MatchPhase.Ready || turns.MatchPhase == MatchPhase.Aiming || turns.MatchPhase == MatchPhase.ShotInFlight);
            }
        }

        /// <summary>The hit radius: a seat's full radius, less after a shrink ray.</summary>
        public double Radius => Seating.PlayerRadius * Mathf.Clamp(Scale, 0.05f, 1f);

        /// <summary>Adds a light for each of this player's projectiles in the air (UR-T77's fog of war).</summary>
        public void CollectShotLights(List<(Vector2 Spot, double Size)> lights)
        {
            if (_shotView != null)
            {
                _shotView.CollectLights(lights);
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

        protected override void OnSpawned()
        {
            // The original's tank (U.RICO-T45), or the code-drawn square and barrel when the art is missing. The same ones
            // again when the kit respawns this object on a reconnect.
            _tank = TankView.Create(transform, name, BodyScale);
            if (_tank == null)
            {
                _body = Shapes.Square(gameObject, BodyScale, 0);
                GameObject barrel = Child("Barrel");
                _barrel = Shapes.Square(barrel, 1f, 1);
                barrel.transform.localScale = new Vector3(0.9f, 0.18f, 1f);
            }

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
                    AssignSeat(Owner.HasSeat ? Owner.SeatIndex : 0);
                }
            }

            if (IsServer && Position == Vector2.zero)
            {
                Place(Arena.SpawnPoint(Mathf.Max(0, Seat)));
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
            FieldEffects.Ensure(); // the level's marks listen for the server's flourishes from now on (U.RICO-T48)
            _seenPickups = PickupCount;
            _seenExtraBounces = ExtraBounces;
            _seenAssists = Assists;
            _adoptedAssists = Assists;
            _shotView = ShotView.Create("Shot (" + name + ")", this);
            _shotView.Landed += _ =>
            {
                if (_pendingOutcome != null) // the server's result came in while this peer was still flying the shot
                {
                    ShotOutcome outcome = _pendingOutcome;
                    _pendingOutcome = null;
                    ShowOutcome(outcome);
                }
            };
            // obj_aimassist's beam and the preview, both in draw_beam's dotted style (U.RICO-T47).
            _assistBeam = DottedBeam.Create(_shotView.transform, "Aim assist", 3);
            if (LocallyControlled)
            {
                _aim.Set(Heading, Power);
                _previewBeam = DottedBeam.Create(_shotView.transform, "Preview", 2);
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
            if (_labels != null)
            {
                _labels.Destroy();
                _labels = null;
            }

            if (_registered != null)
            {
                _registered.RemovePlayer(_playerId); // back from its synced values if the kit respawns it on a reconnect
                _registered = null;
                _state = null;
            }

            if (_shotView != null)
            {
                Destroy(_shotView.gameObject);
            }

            if (_tank != null)
            {
                _tank.DestroyTracks();
            }
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
            transform.localScale = new Vector3(Scale, Scale, 1f) * BodyScale;

            // A client in a process of its own lays the server's level (a host's peers share its Arena).
            if (!IsServer && Arena.Current == null && LevelVersion > 0)
            {
                Arena.AdoptLayout(LevelMap, LevelSeed, LevelVersion);
            }

            if (LocallyControlled) // a bot's driver calls Fire itself (GameBot.cs); Arena fires for an AI
            {
                OwnerInput();
            }

            // A shot reaches a client as the server's ShotResolved (OnShotResolved), never solved again here: the synced
            // Shots count only tells a late joiner which shots were fired before it arrived.

            ShowSyncedFloats(); // a crate collected, an extra bounce used (U.RICO-T48)
            DrawAssist();
            RefreshLabels();

            if (IsServer)
            {
                DriveStep();
                FlightStep();
                bool moving = State.DriveDirection != 0;
                if (TankMoving != moving)
                {
                    TankMoving = moving;
                }
            }

            DrawTank();
        }

        /// <summary>True for the human playing on this machine (not a bot, not an AI).</summary>
        public bool IsLocalHuman => IsSpawned && LocallyControlled;

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
