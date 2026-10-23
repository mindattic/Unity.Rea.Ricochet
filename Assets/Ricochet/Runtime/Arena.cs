// The game's rules on the server, in a plain class so a test (or a bot test) can serve them on its own session.
// It holds the level every shot bounces in (Core's Level.Default, the same on every peer, so nothing about it goes
// over the wire), spawns the keyed practice dummies shots are fired at, knocks one down when a shot lands on it, and
// offers a "restart" vote that stands them all back up. Turns, seats and health (UR-T4) build on this.
using System;
using System.Collections.Generic;
using Ricochet.Core;         // Level, ShotRules, Vec2, Target
using Unity.Multiplayer;      // NetSession, NetKey
using Unity.Multiplayer.Core; // VoteKind
using UnityEngine;

namespace Ricochet
{
    public sealed class Arena
    {
        public const int DummyCount = 3;
        public const string DummyKeyPrefix = "dummy:";
        public const string RestartVote = "restart";

        /// <summary>A dummy's hit radius (its square is drawn 0.8 wide).</summary>
        public const float DummyRadius = 0.45f;

        /// <summary>The level every peer plays in.</summary>
        public static readonly Level Level = Level.Default();

        /// <summary>The shot rules every peer plays by.</summary>
        public static readonly ShotRules Rules = ShotRules.Default;

        private static readonly Vector2[] DummyStarts = { new Vector2(-7f, 1f), new Vector2(7f, -3f), new Vector2(0f, 3.5f) };

        private static readonly float[] SeatColumns = { 0f, -4f, 4f, -8f, 8f };

        private readonly NetSession _session;
        private readonly GameObject _dummyPrefab;

        public Arena(NetSession session, GameObject dummyPrefab)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _dummyPrefab = dummyPrefab ?? throw new ArgumentNullException(nameof(dummyPrefab));
        }

        /// <summary>The arena the server is running, for Player to land shots on (null on a client).</summary>
        public static Arena Current { get; private set; }

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

        /// <summary>Where a seat's player stands: five columns along the bottom of the level, rows stacked up from there.</summary>
        public static Vector2 SpawnPoint(int seat)
        {
            seat = Math.Max(0, seat);
            return new Vector2(SeatColumns[seat % SeatColumns.Length], -4.5f + 1.5f * (seat / SeatColumns.Length % 3));
        }

        public static Vec2 ToCore(Vector2 v) => new Vec2(v.x, v.y);

        public static Vector2 ToUnity(Vec2 v) => new Vector2((float)v.X, (float)v.Y);

        /// <summary>What a shot fired now can hit: every standing dummy <paramref name="session"/> knows about.</summary>
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
        }

        /// <summary>Despawns every dummy and spawns it again, standing, under the same key, in the same frame.</summary>
        public void ResetDummies()
        {
            for (int i = 0; i < DummyCount; i++)
            {
                _session.Objects.Despawn(DummyKey(i));
            }

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

        /// <summary>The vote this game offers: stand every dummy back up. Registered on NetVotes by Game.cs.</summary>
        public VoteKind CreateRestartVote() => new VoteKind(RestartVote, "Restart the round?")
        {
            Description = _ => "Every practice dummy stands back up.",
            Passed = _ => ResetDummies(),
        };
    }
}
