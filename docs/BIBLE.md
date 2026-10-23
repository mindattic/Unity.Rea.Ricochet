---
codex: 1
project: Unity.Ricochet
code: UR
layer: bible
status: living
updated: 2026-10-21
---

# Unity.Ricochet — Project Bible
> Single source of truth for what Unity.Ricochet IS, is NOT, and the rules that keep it coherent.
> README says how to use it; [RFC 0001](rfc/0001-architecture.md) holds the design and build order; this says how to think about the system.

Status legend: ✅ done (verified by a test/build) · 🟡 partial · ⬜ planned · `living`.

## 1. The one sentence {#UR-§1}
Unity.Ricochet is a Unity 6 rebuild of the 2013 GameMaker game *Ricochet* — turn-based, bounce-shot artillery for
up to 16 players, human or AI — with real server-authoritative network multiplayer in place of the original's
single-machine hotseat.

## 2. The product promise {#UR-§2}
- The bounce-aiming mechanic (angle, power, a preview of the shot's ricochet off borders and redirectors before
  you commit) is the one thing this project preserves faithfully from the GML original.
- Everything else the original did on one PC with one keyboard — turn order, AI opponents, weapons, items,
  hazards, the announcer, menus — is rebuilt as a real Unity game, not ported script-for-script.
- Multiplayer is real: host or join by IP or a short code, play from separate machines, reconnect to your seat
  if you drop, exactly what GameMaker 8 of 2013 had no way to do.
- Built on the house kit — [Unity.Multiplayer](https://github.com/mindattic/Unity.Multiplayer),
  [MindAttic.Components](https://github.com/mindattic/MindAttic.Components),
  [Unity.Shared](https://github.com/mindattic/Unity.Shared) — never hand-rolled netcode, menus or profiles.

## 3. What it is NOT {#UR-§3}
- NOT a literal GML-to-C# transliteration. The original's `objects/*.gmx` and `scripts/*.gml` are design
  reference only (read from `R:\Projects\_PRE_2022\_PRE_2021\_PRE_2020\_PRE_2017\gmlRicochet\09-05-13_2.20am`);
  no GML source is copied into this repo, and the implementation is native Unity (MonoBehaviour/NetEntity,
  C#, the house kit), not a transpile.
- NOT client-authoritative, anywhere shot resolution, damage or turn order is decided. See [UR-LAW-1](#UR-LAW-1).
- NOT carrying forward the GML hotseat "multiplayer" rooms (`multiplayer_host`/`multiplayer_join`/
  `multiplayer_hostorjoin`) — those exist only because GML had no network layer; Unity.Multiplayer's real
  networking replaces them outright, not alongside them.

## 4. Architecture canon {#UR-§4}
The full layout and build order live in [RFC 0001 § Architecture](rfc/0001-architecture.md#architecture) and are
not repeated here ([UR-LAW-4](#UR-LAW-4)). The shape in one line: a turn-based game loop and bounce-physics core
that is engine-adjacent but testable, a thin Unity presentation layer (sprites/VFX/UI reading that core), and
the house kit doing every bit of networking, menus and profile I/O — this project never writes its own RPC,
menu or settings-file code.

## 5. The Laws {#UR-§5}

Unity.Ricochet **inherits the org-wide house rules** in `../../MindAttic.HouseRules.md`. Of note here:
HOUSE-LAW-1 (whole-number versioning), HOUSE-LAW-8 (done is verified, not asserted), HOUSE-LAW-15 (don't wait on
a full local test run before landing — the orchestrator's background run catches the rest).

Project-specific laws:

### UR-LAW-1 — The server owns the shot {#UR-LAW-1}
Turn order, aim/fire intent, bounce-path resolution and damage are decided once, on the server (the host in a
listen-server match), per [UM-LAW-1](https://github.com/mindattic/Unity.Multiplayer). A client sends its aim
angle, power and fire intent as a `[ToServer]` call; it never computes or asserts the resolved path or the
damage itself. A single-player match against AI runs the same server-authoritative loop locally (the host is
also the only player), so there is one code path, not two.

### UR-LAW-2 — No GML source in this repo {#UR-LAW-2}
The original `gmlRicochet` project is read for design reference from its location outside this repo and never
copied, vendored or committed here, in whole or in part — not `.gmx` object/room files, not `.gml` scripts.
Ported behavior is re-described in tickets and code comments in this project's own terms.

### UR-LAW-3 — Everything networked goes through the kit {#UR-LAW-3}
No hand-written `NetworkVariable`, RPC, menu widget or settings-file reader. Synced state and RPCs use
Unity.Multiplayer's `[Synced]`/`[ToServer]`/`[ToClients]`/`[ToOwner]`/`[ToClient]`; menus and HUD widgets come
from MindAttic Components; player identity, colour and settings come from Unity.Shared's `Profiles.Current`.

### UR-LAW-4 — One home per fact {#UR-LAW-4}
Design and build order live in RFC 0001; laws live here; story and ticket status live in
[USER_STORIES.md](USER_STORIES.md) (generated — never hand-edited, see `tickets db render`/`sync`). A second
copy of any of these is a bug.

## 6. Verified state {#UR-§6}
- ✅ Repo created (`mindattic/Unity.Ricochet`, tagged `wip`), scaffolded with README, AGENTS/CLAUDE forwarders,
  `.gitignore`, `Packages/manifest.json` (the house kit as local packages), this Bible and RFC 0001 (UR-T2).
- ✅ The Unity project and the first playable slice: aim, bounce preview and a server-solved shot (UR-T3).
- ✅ Matches: up to 16 seats (the connected humans plus AI opponents), turns in seat order with a 20-second turn
  clock, health and damage per hit.gml, the last one standing wins the round, and a HUD of per-seat health bars,
  the clock and the turn order (UR-T4; `Core/Match.cs` tested by `dotnet test Core.slnx`, the wire by
  `Tests/PlayMode/TurnTests.cs`). The AI seats play a stand-in turn until UR-T7.
- ⬜ Everything else — weapons, hazards, the real AI, announcer/HUD, menus, profiles (UR-T5–UR-T10) and
  multiplayer polish (UR-T11–UR-T14) — is ticketed and unstarted. See
  [USER_STORIES.md](USER_STORIES.md) or `tickets ticket queue --repo Unity.Ricochet`.
