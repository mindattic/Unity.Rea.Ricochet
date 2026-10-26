---
codex: 1
project: Unity.Ricochet
code: UR
layer: bible
status: living
updated: 2026-10-23
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
  `Tests/PlayMode/TurnTests.cs`).
- ✅ Weapons and items: a per-player loadout of six weapons and six items cycled left/right (cycle_weapon/item_*.gml),
  a count of 10 meaning unlimited, the cannon unlimited, three of the rest each round; the laser, shotgun, fused grenade
  (1/2/3 set a 0.5–6 s fuse; it passes through players and blasts everyone near where it goes off) and railgun; the
  extra bounce, teleporter and shrink ray; and weapon/item/health/powerup crates that fall during a match on
  obj_drop_emitter's random timers and go to whoever's shot passes over one (UR-T5; `Core/Loadout.cs`, `Volley.cs`,
  `Drops.cs` tested by `dotnet test Core.slnx`, the wire by `Tests/PlayMode/WeaponTests.cs`).
- ✅ AI opponents and aim assist: the start-match vote carries the AI count, one AI skill (0–10) and the aim assists
  each player starts with (0 = none, 10 = unlimited; obj_btn_ai_*/obj_btn_aimassist_*); on its turn an AI shows one
  of check_ai's thirty "is thinking..." lines to every peer, looks for a straight shot, then a bank shot, then a
  railgun shot through the walls (find_target_angle(_from_bounce), on the preview's own solver), nudges its aim by up
  to 10 − skill degrees, turns its barrel onto it and fires; the aim-assist item turns a human's aim onto the shot a
  fully skilled AI would take and draws it as a fading beam (UR-T7; `Core/Ai.cs`, `MatchSetup.cs` tested by
  `dotnet test Core.slnx`, the wire by `Tests/PlayMode/AiOpponentTests.cs`).
- ✅ Level hazards: an electric arc and a laser gate (on every other turn) that stop a shot, foliage a shot cuts down,
  the shield wall item (only its owner's shots pass; anyone else's shatters it; it decays after 20–120 s), the
  redirector item, and the vortex device set off in mid-flight (vortex_detonate.gml), which hurts everyone in its pull,
  swallows the destructible hazards it reaches and breaches the border it reaches, so later shots fly out (UR-T6;
  `Core/Hazards.cs` with `Level`'s barriers and breaches tested by `dotnet test Core.slnx`, the wire by
  `Tests/PlayMode/HazardTests.cs`).
- ✅ Announcer and HUD: the start-match vote carries one of the original's seven on-air personalities and how often they
  speak (0 off, 1 every time, 2–4 one time in 3/6/9); their portrait and name box slide in with a welcome at the match's
  start, a cheer for a knock-out and a heckle for a shot that hits nobody, ten written lines a kind drawn without repeats
  (the original's were recordings); a hit on an opponent scores the shooter 100 + 25 a bounce + 5 a tenth of a second in
  the air for the whole match (hit.gml/draw_potential.gml), shown on each seat's bar; a turn card shows everyone whose
  turn it is with their score, weapon, item and combo (draw_weaponinfo/draw_iteminfo). The cards are MindAttic Components
  widgets; the per-colour health bars are MatchHud's own uGUI (UR-T8, IMGUI until UR-T38; `Core/Announcer.cs`, `MatchRules.Points` and `Match`'s scores
  tested by `dotnet test Core.slnx`, the wire and HUD by `Tests/PlayMode/AnnouncerTests.cs`).
- ✅ Menu flow and rooms: the original's room graph (titlescreen → mainmenu → players → maproom → gamescreen → endgame,
  with settings, controls, howtoplay and credits off the main menu) as a title screen and MindAttic Components screens on the
  kit's front end: Players (name and colour, AI count, skill, aim assists), the map room (four hand-laid maps on the same
  border, seats and laid hazards, and the goal: one round or first to 500–10000 points, check_endgame/obj_btn_goal_*), the
  announcer under Settings, Controls, How to play (obj_slide's slides) and Credits; the match vote carries the map and goal,
  every peer builds the map's level from its index (synced on `Turns`), and once a round ends with the goal reached the match
  is over (`GameOver`, nobody fires) and every peer's endgame recap shows the champion and the leaderboard (obj_endgame) with
  a rematch vote (UR-T9; `Core/MenuFlow.cs`, `Maps.cs`, `Match`'s goal tested by `dotnet test Core.slnx`, the wire and the
  screens by `Tests/PlayMode/MenuFlowTests.cs`).
- ✅ Server-authoritative shot sync: the server sends every client the volley it solved (every projectile's points,
  contacts and end, and whom it strikes; packed exactly, doubles as doubles) and, once it lands in a match, the damage each
  seat took and who it put out, both as `[ToClients]` calls on the shooter's `Player`; no client solves a shot again, so
  every peer draws the same bounce and the struck players float the same damage (UR-T12; `Core/ShotWire.cs` tested by
  `dotnet test Core.slnx`, the wire by `Tests/PlayMode/TurnTests.cs`).
- ✅ Votes: the original had none (whoever held the one keyboard restarted or quit); the kit's NetVotes runs them and its
  vote overlay asks everyone else Yes or No with the tally and the clock, passing on two thirds. The in-game menu offers
  the match vote, **Vote to restart** (the match from round one, a rematch once it is over, the dummies in practice) and
  **Vote to kick**, a room listing everyone who may be voted out (not yourself, not the host, never an AI); the kick is the
  kit's own kind with the game's lines, and passing disconnects them, frees their seat at once and drops it out of the
  round, passing the turn if it was theirs (UR-T14; `Core/VoteMenu.cs` tested by `dotnet test Core.slnx`, the wire by
  `Tests/PlayMode/KickVoteTests.cs`).
- 🟡 End-to-end port check (UR-T15, 2026-10-23): the Windows player builds (`-buildWindows64Player`) and launches;
  title → main menu → Players → map room → lobby → Start plays a match against an AI to its goal, the endgame recap
  shows the champion and leaderboard, Main menu leads back, and a rematch vote replays it; Settings, Controls, How to
  play and Credits open and close; a second build joins by IP into practice and plays the vote-started match. hit.gml's
  damage, check_endgame's goal and the score-ranked recap match the source. Gaps filed as fix tickets: the build's
  `UR-T3` branding (UR-T16), the HUD at narrow widths and over menus (UR-T17, UR-T18, UR-T19), the vote overlay never
  showing on a client (UR-T20), identical default names and colours (UR-T21), join by code without Relay (UR-T22),
  randomised levels and spots (UR-T23), driving and ending a turn (UR-T24), the settings room's match rules (UR-T25),
  fog of war (UR-T26), music (UR-T27), per-AI skill and names (UR-T28), mouse and numpad controls (UR-T29), and in the
  kit, bots joining from a player build (UM-T76) and a second copy taking over the host's player (UM-T77). Left out on
  purpose: the hotseat and multiplayer rooms (§3), the studio splash and startup credits (the title screen stands in),
  and the original's unused rooms (`room0`, the empty `intro`, and the campaign `maproom` no button reached).
- ⬜ Anything not above is ticketed and unstarted. See
  [USER_STORIES.md](USER_STORIES.md) or `tickets ticket queue --repo Unity.Ricochet`.

## Fog of war (UR-T26, ported exactly in UR-T77)

Source: `obj_fogscreen`, `reset_fogofwar.gml`, `randomize_level.gml` (obj_fog and obj_fogofwar_emitter are the retired
per-32-pixel-cell version of the same idea; reset_fogofwar only makes the fogscreen, so they are not ported).

- The `fogofwar` rule is the host's setting (Settings toggle, synced through Turns' packed rules, as before).
- `reset_fogofwar` runs at the end of `randomize_level`, so every new level or round gets a new fogscreen. Here
  `FogOfWar` (Runtime) makes a fresh `FogSurface` (Core) whenever the level or the round changes, or the rule comes on.
- The fogscreen's surface is `room_width/16` by `room_height/16` (surfScale 1/16), cleared black once at creation and never
  again, so a revealed spot stays revealed for the level. It is drawn over the whole arena scaled up (point filtered, as
  draw_surface_ext's default), above everything (depth -999).
- Every 10 steps (alarm[0], 60 a second; the first stamp is in the first draw) it stamps `spr_lightsource` (200 px, radius
  100 room pixels, soft falloff) at each light's place scaled by its `lightSize`, strength 1: every tank (1), bullet (1),
  pellet (0.5), laser (0.5), grenade (1), rail swirl (1), vortex (0.7), and each landed weapon, item and health drop (1).
  Here a room pixel is `ShapeLibrary.UnitsPerPixel`; the falloff table is read off the sprite's alpha.

Deviations: (1) the original lights the surface with GameMaker's `bm_src_color` blend; this reveals by compositing each
stamp's alpha onto the fog's opacity (overlaps add, nothing re-fogs), the evident intent, since the blend maths could not be
run here. (2) obj_powerup crates (the random-drops option) are not lit, as the original's fogscreen does not list them.
(3) Networked: the original had one screen for one hot-seat viewer, so every peer keeps and draws its own surface from the
same inputs (the synced players, shots and crates); nothing about the fog is sent. As in the original, every tank is a
light, so tanks are never hidden, only the ground and drops not yet reached (this replaces UR-T26's invented 5-unit
circle around the local tank only).
