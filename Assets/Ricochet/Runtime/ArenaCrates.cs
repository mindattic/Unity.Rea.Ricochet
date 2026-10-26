// Arena, the crates: spawning and landing the keyed Crate objects the authority drops, and dropping one on demand.
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
        /// <summary>The crates on the level now (server only; empty outside a match).</summary>
        public IReadOnlyList<Drop> CratesOnField => _field != null ? _field.Drops : (IReadOnlyList<Drop>)Array.Empty<Drop>();

        /// <summary>Server: drops a crate of <paramref name="kind"/> at <paramref name="spot"/> now (a test, or a debug key; null outside a match).</summary>
        public Drop DropCrate(DropKind kind, Vector2 spot) => _authority.DropCrate(kind, ToCore(spot));

        private void SpawnCrate(Drop drop)
        {
            GameObject spawned = _session.Objects.Spawn(CratePrefab(), Crate.KeyFor(drop.Id), ToUnity(drop.Position));
            Crate crate = spawned.GetComponent<Crate>();
            crate.Kind = (int)drop.Kind;
            crate.Spot = ToUnity(drop.Spot);
            crate.Landed = drop.Landed;
        }

        private void LandCrate(Drop drop)
        {
            if (_session.Objects.TryGet(Crate.KeyFor(drop.Id), out Crate crate) && crate != null)
            {
                crate.Landed = true;
            }
        }

        private static GameObject CratePrefab() => LoadPrefab("Game/Crate");
    }
}
