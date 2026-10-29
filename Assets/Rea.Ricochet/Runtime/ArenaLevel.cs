// Arena, the level: the round's layout every peer plays in, targets and the level a shot sees, the hazards and the practice dummies.
// (U.RICO-T39: split out of Arena.cs, no behaviour change.)
using System;
using System.Collections.Generic;
using Ricochet.Core.Ai;
using Ricochet.Core.Aim;
using Ricochet.Core.Arsenal;
using Ricochet.Core.Level;
using Ricochet.Core.Match;
using Ricochet.Core.Menu;
using Ricochet.Core.Net;
using Unity.Multiplayer;      // NetSession, NetKey
using Unity.Multiplayer.Core; // VoteKind
using UnityEngine;
using Cmd = Ricochet.Core.Commands;

namespace Ricochet
{
    public sealed partial class Arena
    {
        /// <summary>
        /// The level every peer plays in: the current map's (UR-T9). The first map (the practice range) until a match is
        /// started on another; each peer follows the map Turns carries.
        /// </summary>
        public static Level Level => s_layout.Level;

        // The round being played: the map's level and where each seat stands (a random map's come from LevelSeed, UR-T23).
        private static RoundLayout s_layout = Maps.At(0).Layout(0);

        /// <summary>The round <see cref="Level"/> belongs to: its seat spots and, on a random map, the shapes it laid (U.RICO-T46 draws them).</summary>
        public static RoundLayout Layout => s_layout;

        /// <summary>The map <see cref="Level"/> was built from (an index into Maps.All).</summary>
        public static int MapIndex { get; private set; }

        /// <summary>The seed the current round's random level was built from (0 on a hand-laid map).</summary>
        public static int LevelSeed { get; private set; }

        /// <summary>Counts the server's level changes, so a client adopts the newest and never an older one (0 before any).</summary>
        public static int LevelVersion { get; private set; }

        /// <summary>Raised on this peer when <see cref="Level"/> changes to another map's (the level view redraws).</summary>
        public static event Action<Level> LevelChanged;

        /// <summary>Plays on map <paramref name="index"/> from now on, on this peer (the server's match start, a client's Turns).</summary>
        public static void UseMap(int index) => UseLayout(index, 0);

        /// <summary>
        /// Plays map <paramref name="index"/>'s round for <paramref name="seed"/> from now on, on this peer: a random map's
        /// level and seat spots are built from the seed, so every peer lays the same round (UR-T23).
        /// </summary>
        public static void UseLayout(int index, int seed)
        {
            index = Maps.Clamp(index);
            if (index == MapIndex && seed == LevelSeed)
            {
                return;
            }

            MapIndex = index;
            LevelSeed = seed;
            s_layout = Maps.At(index).Layout(seed);
            LevelChanged?.Invoke(Level);
        }

        /// <summary>Back to the practice range and no level changes seen yet (a session starting or ending).</summary>
        private static void ResetLayout()
        {
            LevelVersion = 0;
            UseLayout(0, 0);
        }

        /// <summary>Client: the server's level (map, seed and version, as Player carries them): adopted when newer than this peer's.</summary>
        internal static void AdoptLayout(int index, int seed, int version)
        {
            if (version > LevelVersion)
            {
                LevelVersion = version;
                UseLayout(index, seed);
            }
        }

        /// <summary>The hazards on the level now (server only).</summary>
        public IReadOnlyList<Hazard> HazardsOnField => _hazards.Hazards;

        /// <summary>
        /// What a shot fired now can hit: every standing dummy <paramref name="session"/> knows about and, during a match,
        /// every player still in the round (the shooter too: a shot that banks back hurts, as hit.gml's self-hit did).
        /// </summary>
        public static List<Target> Targets(NetSession session)
        {
            List<Target> targets = Dummies(session);
            if (session == null || session.Objects == null)
            {
                return targets;
            }

            if (Turns.For(session) != null)
            {
                foreach (Player player in Player.In(session))
                {
                    if (player.InRound && player.Seat >= 0)
                    {
                        targets.Add(new Target(SeatTarget(player.Seat), ToCore(player.Position), player.Radius));
                    }
                }
            }

            return targets;
        }

        /// <summary>A target for every standing practice dummy <paramref name="session"/> knows about.</summary>
        public static List<Target> Dummies(NetSession session)
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

        /// <summary>
        /// The level <paramref name="shooter"/>'s shot sees in <paramref name="session"/> now: the level with every hazard
        /// on it that stops their shot (not their own shield walls), the breaches and the placed redirectors. The server
        /// reads its own HazardField; every other peer builds it from the LevelHazard objects it has (the same hazards).
        /// </summary>
        public static Level LevelFor(NetSession session, int shooter)
        {
            Arena current = Current;
            if (current != null && current._session == session)
            {
                return current._authority.LevelFor(shooter);
            }

            return session != null ? HazardField.Build(Level, LevelHazard.Collect(session), shooter) : Level;
        }

        /// <summary>
        /// Server: every hazard goes, and the level is laid again when <see cref="LaysHazards"/> (the arcs and gates, and
        /// create_random_foliage's tufts kept clear of every seat's spot, every player and the practice dummies).
        /// </summary>
        public void ResetHazards() => _authority.ResetHazards();

        private void SpawnHazard(Hazard hazard)
        {
            GameObject spawned = _session.Objects.Spawn(HazardPrefab(), LevelHazard.KeyFor(hazard.Id), Vector2.zero);
            spawned.GetComponent<LevelHazard>().Mirror(hazard);
        }

        private void MirrorHazard(Hazard hazard)
        {
            if (_session.Objects.TryGet(LevelHazard.KeyFor(hazard.Id), out LevelHazard entity) && entity != null)
            {
                entity.Mirror(hazard);
            }
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

        // The server laid a level (a match's map, a new round, a reroll): this peer plays on it and every player's clients learn it.
        private void AdoptServerLayout(Cmd.LayoutChanged layout)
        {
            UseLayout(layout.Map, layout.Seed);
            if (!layout.Rolled)
            {
                return;
            }

            LevelVersion = layout.Version;
            foreach (Player player in Player.In(_session))
            {
                player.MirrorLevel(MapIndex, LevelSeed, LevelVersion);
            }
        }

        /// <summary>
        /// Server, practice only (R, PageUp and PageDown in the original): rolls another random level, stands every player at
        /// a fresh spot and lays the practice dummies and hazards again. False during a match, whose rounds roll their own.
        /// Players ask for it with a command; this is for a test or a debug key.
        /// </summary>
        public bool RerollLevel() => _authority.Reroll().Count > 0;

        private void RemoveDummies()
        {
            for (int i = 0; i < DummyCount; i++)
            {
                _session.Objects.Despawn(DummyKey(i));
            }

            _dummiesUp = false;
        }

        private static GameObject HazardPrefab() => LoadPrefab("Game/Hazard");
    }
}
