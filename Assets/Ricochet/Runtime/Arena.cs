// The game's rules on the server, in a plain class so a test (or a bot test) can serve them on its own session.
// It holds the level every shot bounces in (Core's Level.Default, the same on every peer, so nothing about it goes
// over the wire), spawns the keyed practice dummies shots are fired at before a match, and runs the match (UR-T4): up
// to sixteen seats, the connected humans plus AI opponents it spawns, taking turns on Core's Match with its turn
// clock and health, mirrored to every peer through the Turns object and each Player. It offers two votes: start a
// match (with a number of AI opponents) and restart (the round's health, or the practice dummies).
using System;
using System.Collections.Generic;
using Ricochet.Core;         // Level, ShotRules, Vec2, Target, Match, Seating, SimpleAi
using Unity.Multiplayer;      // NetSession, NetKey
using Unity.Multiplayer.Core; // VoteKind
using UnityEngine;

namespace Ricochet
{
    public sealed class Arena
    {
        public const int DummyCount = 3;
        public const string DummyKeyPrefix = "dummy:";
        public const string SeatKeyPrefix = "seat:";
        public const string AiKeyPrefix = "ai:";
        public const string RestartVote = "restart";
        public const string MatchVote = "match";

        /// <summary>A dummy's hit radius (its square is drawn 0.8 wide).</summary>
        public const float DummyRadius = 0.45f;

        /// <summary>The level every peer plays in.</summary>
        public static readonly Level Level = Level.Default();

        /// <summary>The shot rules every peer plays by.</summary>
        public static readonly ShotRules Rules = ShotRules.Default;

        /// <summary>The match rules every peer plays by.</summary>
        public static readonly MatchRules MatchRules = MatchRules.Default;

        private static readonly Vector2[] DummyStarts = { new Vector2(-7f, 1f), new Vector2(7f, -3f), new Vector2(0f, 3.5f) };

        private readonly NetSession _session;
        private readonly GameObject _dummyPrefab;
        private readonly System.Random _aiDice = new System.Random();
        private Match _match;
        private Turns _turns;
        private readonly Dictionary<int, int> _humanSeats = new Dictionary<int, int>(); // kit seat -> match seat
        private int _aiCount;
        private bool _dummiesUp;

        public Arena(NetSession session, GameObject dummyPrefab)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _dummyPrefab = dummyPrefab ?? throw new ArgumentNullException(nameof(dummyPrefab));
        }

        /// <summary>The arena the server is running, for Player to land shots on (null on a client).</summary>
        public static Arena Current { get; private set; }

        /// <summary>The match being played (null before the first one starts). Server only.</summary>
        public Match Match => _match;

        /// <summary>The AI opponents the current match was started with.</summary>
        public int AiCount => _aiCount;

        /// <summary>Serves the rules on <paramref name="session"/>: dummies spawn as soon as it runs as a server.</summary>
        public static Arena Serve(NetSession session, GameObject dummyPrefab)
        {
            var arena = new Arena(session, dummyPrefab);
            session.Started += () =>
            {
                if (session.IsServer)
                {
                    Current = arena;
                    arena.SpawnDummies();
                }
            };
            session.SeatReleased += seat => arena.SeatReleased(seat.Index);
            session.Ended += _ =>
            {
                if (Current == arena)
                {
                    Current = null;
                }
            };
            if (session.IsServer && session.IsLive)
            {
                Current = arena;
                arena.SpawnDummies();
            }

            return arena;
        }

        public static string DummyKey(int index) => DummyKeyPrefix + index;

        public static int DummyIndex(string key)
        {
            int index;
            return key != null && key.StartsWith(DummyKeyPrefix, StringComparison.Ordinal)
                && int.TryParse(key.Substring(DummyKeyPrefix.Length), out index) ? index : 0;
        }

        public static Vector2 DummyStart(int index) => DummyStarts[((index % DummyStarts.Length) + DummyStarts.Length) % DummyStarts.Length];

        /// <summary>Where a seat's player stands (Core's sixteen spots; the first five are along the bottom).</summary>
        public static Vector2 SpawnPoint(int seat) => ToUnity(Seating.SpawnPoint(Math.Max(0, seat)));

        /// <summary>The target id a seated player is hit as ("seat:3").</summary>
        public static string SeatTarget(int seat) => SeatKeyPrefix + seat;

        /// <summary>The seat a target id names, or -1 when it is not a player.</summary>
        public static int SeatOf(string targetId)
        {
            int seat;
            return targetId != null && targetId.StartsWith(SeatKeyPrefix, StringComparison.Ordinal)
                && int.TryParse(targetId.Substring(SeatKeyPrefix.Length), out seat) ? seat : -1;
        }

        public static Vec2 ToCore(Vector2 v) => new Vec2(v.x, v.y);

        public static Vector2 ToUnity(Vec2 v) => new Vector2((float)v.X, (float)v.Y);

        /// <summary>
        /// What a shot fired now can hit: every standing dummy <paramref name="session"/> knows about and, during a match,
        /// every player still in the round (the shooter too: a shot that banks back hurts, as hit.gml's self-hit did).
        /// </summary>
        public static List<Target> Targets(NetSession session)
        {
            var targets = new List<Target>();
            if (session == null || session.Objects == null)
            {
                return targets;
            }

            foreach (NetKey key in session.Objects.All)
            {
                Dummy dummy = key != null ? key.GetComponent<Dummy>() : null;
                if (dummy != null && dummy.IsSpawned && !dummy.Down)
                {
                    targets.Add(new Target(dummy.Key, ToCore(dummy.Position), DummyRadius));
                }
            }

            if (Turns.For(session) != null)
            {
                foreach (Player player in Player.In(session))
                {
                    if (player.InRound && player.Seat >= 0)
                    {
                        targets.Add(new Target(SeatTarget(player.Seat), ToCore(player.Position), Seating.PlayerRadius));
                    }
                }
            }

            return targets;
        }

        /// <summary>Spawns every dummy at its start, keyed dummy:0, dummy:1, ... (server only).</summary>
        public void SpawnDummies()
        {
            for (int i = 0; i < DummyCount; i++)
            {
                GameObject spawned = _session.Objects.Spawn(_dummyPrefab, DummyKey(i), DummyStart(i));
                spawned.GetComponent<Dummy>().Position = DummyStart(i);
            }

            _dummiesUp = true;
        }

        /// <summary>Despawns every dummy and spawns it again, standing, under the same key, in the same frame.</summary>
        public void ResetDummies()
        {
            RemoveDummies();
            SpawnDummies();
        }

        /// <summary>A shot landed on <paramref name="key"/>: knock it down (server only). False when there is no such dummy.</summary>
        public bool Knock(string key)
        {
            if (key == null || !_session.Objects.TryGet(key, out Dummy dummy) || dummy == null)
            {
                return false;
            }

            dummy.Down = true;
            return true;
        }

        /// <summary>
        /// Starts a match (server only): every connected player takes a seat in join order, then
        /// <paramref name="aiCount"/> AI opponents fill the next seats, up to sixteen in all; the practice dummies are
        /// cleared away and seat 0 takes the first turn. A match already running starts over.
        /// </summary>
        /// <returns>False (and nothing changes) when fewer than two seats would play.</returns>
        public bool StartMatch(int aiCount)
        {
            var humans = new List<Player>();
            foreach (Player player in Player.In(_session))
            {
                if (!player.IsAi)
                {
                    humans.Add(player);
                }
            }

            humans.Sort((a, b) => a.KitSeat.CompareTo(b.KitSeat));
            int seated = Math.Min(humans.Count, MatchRules.MaxSeats);
            aiCount = Math.Max(0, Math.Min(aiCount, MatchRules.MaxSeats - seated));
            if (seated + aiCount < 2)
            {
                return false;
            }

            RemoveAi();
            if (_dummiesUp)
            {
                RemoveDummies();
            }

            _aiCount = aiCount;
            _match = new Match(MatchRules);
            _humanSeats.Clear();
            for (int i = 0; i < humans.Count; i++)
            {
                Player human = humans[i];
                int seat = i < seated ? i : -1; // a seventeenth human watches
                human.Seat = seat;
                if (seat >= 0)
                {
                    _match.Join(seat, SeatKind.Human, human.DisplayName);
                    _humanSeats[human.KitSeat] = seat;
                    human.Position = SpawnPoint(seat);
                }
            }

            GameObject opponentPrefab = OpponentPrefab();
            for (int i = 0; i < aiCount; i++)
            {
                int seat = seated + i;
                GameObject spawned = _session.Objects.Spawn(opponentPrefab, AiKeyPrefix + (i + 1), SpawnPoint(seat));
                Player ai = spawned.GetComponent<Player>();
                ai.Seat = seat;
                ai.Position = SpawnPoint(seat);
                _match.Join(seat, SeatKind.Ai, ai.DisplayName);
            }

            _turns = Turns.For(_session);
            if (_turns == null)
            {
                _turns = _session.Objects.Spawn(TurnsPrefab(), Turns.Key).GetComponent<Turns>();
            }

            _turns.Arena = this;
            _match.Start();
            Mirror();
            return true;
        }

        /// <summary>Server: may <paramref name="player"/> fire now? Always in practice; in a match, only on their turn.</summary>
        public bool MayFire(Player player)
        {
            if (_match == null || !_match.IsRunning)
            {
                return true;
            }

            return player != null && player.Seat >= 0 && _match.MayFire(player.Seat);
        }

        /// <summary>Server: <paramref name="player"/> fired (the turn clock stops until it lands).</summary>
        public void Fired(Player player)
        {
            if (_match != null && _match.IsRunning && player != null)
            {
                _match.Fire(player.Seat);
                Mirror();
            }
        }

        /// <summary>Server: <paramref name="player"/>'s shot landed: knock down a dummy, or hurt a player and end the turn.</summary>
        public void Landed(Player player, ShotPath path)
        {
            if (path == null)
            {
                return;
            }

            if (path.End == ShotEndKind.Target && path.TargetId.StartsWith(DummyKeyPrefix, StringComparison.Ordinal))
            {
                Knock(path.TargetId);
            }

            if (_match != null && _match.IsRunning && player != null)
            {
                int hit = path.End == ShotEndKind.Target ? SeatOf(path.TargetId) : -1;
                _match.ShotLanded(player.Seat, hit, path.Bounces);
                Mirror();
            }
        }

        /// <summary>True while a match is being played (server only).</summary>
        public bool MatchRunning => _match != null && _match.IsRunning;

        /// <summary>
        /// Server: a player arrived while a match is running: they take the lowest free seat and play from the next
        /// round (a seventeenth watches). Before a match, Player seats itself by its connection's seat.
        /// </summary>
        public void Arrived(Player player)
        {
            if (!MatchRunning || player == null || player.IsAi)
            {
                return;
            }

            int seat = _match.FreeSeat();
            player.Seat = seat;
            if (seat >= 0)
            {
                _match.Join(seat, SeatKind.Human, player.DisplayName);
                _humanSeats[player.KitSeat] = seat;
                player.Position = SpawnPoint(seat);
            }

            Mirror();
        }

        /// <summary>
        /// Server: the kit released a connection's seat (the player left, or did not come back in time): their match seat
        /// drops out, passing the turn if it was theirs. A dropped player whose seat is only held keeps it.
        /// </summary>
        public void SeatReleased(int kitSeat)
        {
            if (_match != null && _humanSeats.TryGetValue(kitSeat, out int seat))
            {
                _humanSeats.Remove(kitSeat);
                _match.Leave(seat);
                Mirror();
            }
        }

        /// <summary>Server: runs the match clock (called by Turns each frame) and plays the AI seats' turns.</summary>
        public void Tick(float seconds)
        {
            if (_match == null)
            {
                return;
            }

            _match.Tick(seconds);
            if (_match.Phase == MatchPhase.Aiming && _match.TryGetSeat(_match.Current, out MatchSeat seat) && seat.Kind == SeatKind.Ai
                && _match.Rules.TurnSeconds - _match.TimeLeft >= _match.Rules.AiThinkSeconds)
            {
                Player ai = PlayerAt(seat.Index);
                if (ai != null && !ai.ShotInFlight)
                {
                    double heading = SimpleAi.ChooseHeading(ToCore(ai.Position), SeatTarget(seat.Index), Level, Targets(_session), Rules, _aiDice, out _);
                    ai.ServerFire((float)heading, (float)SimpleAi.Power);
                }
            }

            Mirror();
        }

        /// <summary>The player sitting in <paramref name="seat"/>, or null.</summary>
        public Player PlayerAt(int seat)
        {
            foreach (Player player in Player.In(_session))
            {
                if (player.Seat == seat)
                {
                    return player;
                }
            }

            return null;
        }

        /// <summary>The vote this game offers: restart the round (in a match) or stand every dummy back up.</summary>
        public VoteKind CreateRestartVote() => new VoteKind(RestartVote, "Restart the round?")
        {
            Description = _ => _match != null && _match.IsRunning ? "Everyone back to full health; the match starts over." : "Every practice dummy stands back up.",
            Passed = _ =>
            {
                if (_match != null && _match.IsRunning)
                {
                    StartMatch(_aiCount);
                }
                else
                {
                    ResetDummies();
                }
            },
        };

        /// <summary>The vote that starts a match; its number is how many AI opponents to add.</summary>
        public VoteKind CreateMatchVote() => new VoteKind(MatchVote, r => "Start a match with " + r.Number + " AI opponent" + (r.Number == 1 ? "?" : "s?"))
        {
            Description = _ => "Everyone takes a seat and plays in turn; the last one standing wins the round.",
            Check = r => HumanCount() + Math.Max(0, r.Number) < 2 ? "A match needs two players: add an AI opponent." : null,
            Passed = v => StartMatch((int)v.Number),
        };

        private int HumanCount()
        {
            int count = 0;
            foreach (Player player in Player.In(_session))
            {
                count += player.IsAi ? 0 : 1;
            }

            return count;
        }

        private void Mirror()
        {
            if (_match == null)
            {
                return;
            }

            if (_turns != null && _turns.IsSpawned)
            {
                _turns.Mirror(_match);
            }

            foreach (Player player in Player.In(_session))
            {
                if (player.Seat >= 0 && _match.TryGetSeat(player.Seat, out MatchSeat seat))
                {
                    player.Health = seat.Health;
                    player.InRound = seat.Alive;
                }
                else
                {
                    player.InRound = false;
                }
            }
        }

        private void RemoveDummies()
        {
            for (int i = 0; i < DummyCount; i++)
            {
                _session.Objects.Despawn(DummyKey(i));
            }

            _dummiesUp = false;
        }

        private void RemoveAi()
        {
            for (int i = 1; i <= MatchRules.MaxSeats; i++)
            {
                _session.Objects.Despawn(AiKeyPrefix + i);
            }
        }

        private static GameObject OpponentPrefab() => LoadPrefab("Game/Opponent");

        private static GameObject TurnsPrefab() => LoadPrefab("Game/Turns");

        private static GameObject LoadPrefab(string path)
        {
            GameObject prefab = Resources.Load<GameObject>(path);
            if (prefab == null)
            {
                throw new InvalidOperationException("Resources/" + path + ".prefab is missing.");
            }

            return prefab;
        }
    }
}
