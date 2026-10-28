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
- The bounce-aiming mechanic (an angle, the original's constant bullet speed with no power, a shot that flies until
  its bounces run out, and a preview of its ricochet off borders and redirectors before you commit, U.RICO-T88) is the one thing this project preserves faithfully from the GML original.
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
angle and fire intent as a `[ToServer]` call; it never computes or asserts the resolved path or the
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
  a count of 10 meaning unlimited, three of every weapon each round, the cannon included (config.ini countcannon = 3; each shot spends one, and a spent cannon cycles right like any weapon; a player with nothing left cannot fire and the clock runs out, as the original; the setup screen sets the cannon like the rest: obj_btn_cannon_more/less, 0 to 10, 10 unlimited); the laser, shotgun, fused grenade
  (1/2/3 set a 0.5–6 s fuse; it passes through players and blasts everyone near where it goes off) and railgun; the
  extra bounce, teleporter and shrink ray; and weapon/item/health/powerup crates that fall during a match on
  obj_drop_emitter's random timers and go to whoever's shot passes over one (UR-T5; `Core/Loadout.cs`, `Volley.cs`,
  `Drops.cs` tested by `dotnet test Core.slnx`, the wire by `Tests/PlayMode/WeaponTests.cs`).
- ✅ Shotgun (U.RICO-T94, as shoot_shotgun.gml, pellet_create.gml and hit.gml): `pelletCount` = 12 pellets, each starting 40 + 1..30 px
  (px x 22/1920 units) out along the aim at a whole-degree random spread of +/- `shotgunSpread` = 4, each with its own bounce
  limit (0..maxBounces, or 2..maxBounces when that is 2 or more; Extra Bounce adds to it). The first pellet to strike does a
  full hit and every other pellet is removed where it is that moment, so a volley strikes at most once (the rebuild had five
  evenly spaced pellets at 0.35 of a hit each; `pelletDamage` is gone from arsenal.json). The dice are `GameAuthority`'s seeded
  shot stream, drawn only when the shot is fired; the solved paths travel on `ShotWire` unchanged (no version bump), and a
  preview (no dice) is one pellet along the aim.
- ✅ AI opponents and aim assist: the start-match vote carries the AI count, one AI skill (0–10) and the aim assists
  each player starts with (0 = none, 10 = unlimited; obj_btn_ai_*/obj_btn_aimassist_*); on its turn an AI shows one
  of check_ai's thirty "is thinking..." lines to every peer and decides at once, as the original did (U.RICO-T98):
  the first whole degree from 0 whose straight line reaches a living opponent before the first shape
  (find_target_angle), else the whole degree whose tracer (shoot_tracer: a shell off the shapes and the border only)
  reaches one soonest, else a random whole degree; it nudges that by up to 10 − skill whole degrees (default skill 10,
  config.ini), turns its barrel onto it at a player's turn rate and fires a cannon shell that spends nothing
  (obj_check_ai calls shoot_bullet); the aim-assist item turns a human's aim onto the shot a
  fully skilled AI would take and draws it as a fading beam (UR-T7; `Core/Ai.cs`, `MatchSetup.cs` tested by
  `dotnet test Core.slnx`, the wire by `Tests/PlayMode/AiOpponentTests.cs`).
- ✅ Level hazards: an electric arc and a laser gate (on every other turn) that stop a shot, foliage a shot cuts down (U.RICO-T91: none is laid by default, as the original never created obj_tesla, never called draw_electricity or check_source_laser, and had randomize_level's foliage commented out; the code stays and `hazards.json`'s `layHazards: true` lays them every round),
  the shield wall item (only its owner's shots pass; anyone else's shatters it; it decays after 20–120 s), the
  redirector item, and the vortex device set off in mid-flight (vortex_detonate.gml). Corrected by U.RICO-T96 to match
  vortex_step.gml: the vortex hurts no one itself and touches no hazard or border; it pulls the level's shapes within 200 px
  (2.2917 units) in at 1 px a step and flings them out at 10 px a step, and a tank is hurt (once) only when a flung shape
  strikes it (`Core/Arsenal/VortexStorm.cs`; shapes are the walls grouped by shared corners; the shapes stay where the storm leaves them, as in
  the original, which never puts a moved shape back: U.RICO-T134 has `VortexStorm.Play` give every moved shape's pull and fling
  (`Volley.Storm`, carried by `ShotWire`, version 3 since U.RICO-T141), the server's level keep them when the shot lands (`Level.Apply`), and each
  peer's `LevelView` walk the shapes along it in step with the implosion (`WalkStorm`, 250 steps in the pull starts, 800 the
  fling) and keep them in its own level when it ends (`SettleStorm`); U.RICO-T141 turns them as vortex_step.gml did
  (`image_angle -= 1` each step of the pull and the wait, 550 degrees clockwise in all, none while blowing, about the shape's box
  centre, `StormShape.Pivot`, which the pull carries along; `Level.Apply` leaves the walls at the turned pose and `ShotWire` version 3
  carries the pivot); not modelled: a client that joins after a storm lays the round's shapes unmoved until the next round; the flight no longer stops on a tank it meets: obj_vortex has no collision event with a tank, so it flies through to its landing point, U.RICO-T135) (UR-T6;
  `Core/Hazards.cs` with `Level`'s barriers and breaches tested by `dotnet test Core.slnx`, the wire by
  `Tests/PlayMode/HazardTests.cs`).
- ✅ Announcer and HUD: the start-match vote carries one of the original's seven on-air personalities and how often they
  speak (0 off, 1 every time, 2–4 one time in 3/6/9); their portrait and name box slide in with a welcome at the match's
  start, a cheer for a knock-out and a heckle for a shot that hits nobody, ten lines a kind drawn without repeats, each
  played as the personality's own recording with its written line as the caption (U.RICO-T51, below); a hit on an opponent scores the shooter 100 + 25 a bounce + 5 a tenth of a second in
  the air for the whole match (hit.gml/draw_potential.gml), shown on each seat's bar; a turn card shows everyone whose
  turn it is with their score, weapon, item and combo (draw_weaponinfo/draw_iteminfo). The cards are MindAttic Components
  widgets; the per-colour health bars are MatchHud's own uGUI (UR-T8, IMGUI until UR-T38; `Core/Announcer.cs`, `MatchRules.Points` and `Match`'s scores
  tested by `dotnet test Core.slnx`, the wire and HUD by `Tests/PlayMode/AnnouncerTests.cs`).
- ✅ Menu flow and rooms: the original's room graph (titlescreen → mainmenu → players → gamescreen → endgame,
  with settings, controls, howtoplay and credits off the main menu) as a title screen and MindAttic Components screens on the
  kit's front end: Players (name and colour, AI count, skill, aim assists), (no map room, U.RICO-T101: the goal, one round or first to
  500–10000 points, check_endgame/obj_btn_goal_*, is on the Start screen), the
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
  title → main menu → Players → Start → lobby → Start plays a match against an AI to its goal, the endgame recap
  shows the champion and leaderboard, Main menu leads back, and a rematch vote replays it; Settings, Controls, How to
  play and Credits open and close; a second build joins by IP into practice and plays the vote-started match. hit.gml's
  damage (default weapon_damage 100, chosen from 5/10/25/33/50/100; U.RICO-T92), check_endgame's goal and the score-ranked recap match the source. Gaps filed as fix tickets: the build's
  `UR-T3` branding (UR-T16), the HUD at narrow widths and over menus (UR-T17, UR-T18, UR-T19), the vote overlay never
  showing on a client (UR-T20), identical default names and colours (UR-T21), join by code without Relay (UR-T22),
  randomised levels and spots (UR-T23), driving and ending a turn (UR-T24), the settings room's match rules (UR-T25),
  fog of war (UR-T26), music (UR-T27), per-AI skill and names (UR-T28), mouse and numpad controls (UR-T29), and in the
  kit, bots joining from a player build (UM-T76) and a second copy taking over the host's player (UM-T77). Left out on
  purpose: the hotseat and multiplayer rooms (§3) and the original's unused rooms (`room0`, the empty `intro`, and the
  campaign `maproom` no button reached). The studio splash and startup credits, then left out too, are restored by
  U.RICO-T57 (Splash and startup credits).
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

Deviations: (1) the original lights the surface with `draw_set_blend_mode(bm_src_color)` (factors src colour and inverse src colour)
on a white sprite: taken literally that paints the sprite's whole square white (white src colour, factor 0 on the fog) and
leaves the alpha at a*a + 1 - a, so fog that is almost opaque everywhere, never a reveal. That is plainly broken, so this
fixes it to what was clearly meant: each stamp's alpha (the sprite's falloff times lightStrength) is composited onto the fog's
opacity (overlaps add, nothing re-fogs). Only tanks still in the round light it (a knocked-out tank's obj_player is gone). (2) obj_powerup crates (the random-drops option) are not lit, as the original's fogscreen does not list them.
(3) Networked: the original had one screen for one hot-seat viewer, so every peer keeps and draws its own surface from the
same inputs (the synced players, shots and crates); nothing about the fog is sent. As in the original, every tank is a
light, so tanks are never hidden, only the ground and drops not yet reached (this replaces UR-T26's invented 5-unit
circle around the local tank only).

## Pause menu (U.RICO-T43)

Source: `obj_pausemenu`, `obj_btn_resume`, `obj_btn_randomize`, `obj_btn_music`, `obj_btn_quit_round`, `isPaused`.

- The original's Esc set `isPaused`, which froze the clock, the clouds and every shot, and drew "Paused" with Resume,
  Randomize (`randomize_level`, only while `!hasFired`), Next Track and Setup (back to the players room).
- Here Core's `GameAuthority.SetPaused` pauses a running match: the turn clock, the Ready hold, crates, hazards, shots and
  drives wait, and every command but votes and seat joins and leaves is refused (`Tests/Core.Tests/PauseTests.cs`). `Turns`
  carries the pause to every peer, so each peer's clock, Ready card, input and shot flight stop too, and the AI waits.
- The pause menu (`Runtime/PauseMenu.cs`, under the HUD) is up on every peer while paused: Resume, Randomize the level
  (`GameAuthority.RerollRound`: a fresh random level, fresh spots, crates, hazards and loadouts, the current turn started
  over with its Ready card), Music and Vote to quit the round (`GameAuthority.QuitMatch`).
- `Tests/PlayMode/PauseMenuTests.cs` plays it on a host and a client.

Deviations: (1) Networked, so pause is the host's at once and anyone else's a vote (pause and resume votes), and it freezes
every peer. (2) The Pause key is P, Pause or the right stick's press, and the in-game menu's "Pause the game": Esc stays the
kit's in-game menu (invite, settings, votes, leave). (3) Randomize is the host's alone, still only before the round's first
shot. (4) The music button is an on/off toggle of the player's own profile mute (`GameAudio.SetMusicMuted`), not Next Track
(music is a random pick per round and turn, UR-T27, so there is no track to step to); under the CLI session mute the game stays silent and only the player's
own values are saved. (5) Quit round is a vote, and it ends the match back to practice with everyone on the in-game menu
(whose Players and Map rooms start the next one), not the setup screen.

## Art and fonts (U.RICO-T44)

Source: every `sprites/*.sprite.gmx`, `background/*.background.gmx` and `fonts/*.font.gmx` of the 2013 project (the copy
docs/RECONSTRUCTION.md was read from; the 2018 re-save's images are byte-identical), `scripts/draw_text_outline.gml`.

- `Tools/import-gml-art.ps1` mirrors all 314 sprites (473 frames) and the 62 backgrounds that have an image into
  `Assets/Ricochet/Art`, each in its GameMaker group (`Unlisted` for the 39 sprites and 12 backgrounds the project file
  no longer lists), and writes `Art/gml-art.json`: the source data, the original files (`originalPath`, relative to the
  project folder), whether the project lists it, and the image_speed its object's Create sets. `bg_redslate` is skipped:
  its image is missing from the original.
- `Editor/Art/GmlArtImporter` is the importer preset, read from that file: single sprites with the pivot on the GML
  origin at 1920 px = 22 units (`ShapeLibrary.UnitsPerPixel`), full-rect, bilinear, uncompressed, no mipmaps;
  backgrounds pivot top left, tile where they tiled, and are compressed at high quality. `GmlArtBuilder` adds a Sprite
  Atlas V2 per top group and fills `Resources/Ricochet/ArtCatalogue`, which records every asset with its original path.
- `Tools/import-gml-fonts.ps1` mirrors the nine fonts' baked glyph atlases and the sprite font sheet (`spr_spritefont`,
  coloured ASCII 33-126 on a ruled 40x51 grid) into `Assets/Ricochet/Fonts` with `gml-fonts.json`. Only GameMaker's
  rasterised glyphs are imported, never Impact, Verdana, Monotype Corsiva or Arial themselves (not redistributable).
- `Editor/Fonts/GmlFontBuilder` makes each a TextMeshPro font: the glyph cells repacked with clear gaps, sampled up to
  about 64 texels a line (`GmlFontMetrics.SamplingScale`), turned into a signed distance field (Core's
  `SignedDistanceField`), with GameMaker's metrics (cell top on the line top, `shift` and `offset` as advance and bearing,
  `string_height("M")` lines) at that scale, so a label at the font.gmx size draws at the original's pixel size. Each has
  draw_text_outline's materials beside its own: white face, black outline 1 and 2 px wide (the original calls it with 2 on
  its buttons, labels and the Ready card, 1 on the aim readout and the name over the tank), the face dilated back so the
  glyph itself is untouched. The sprite font keeps its colours on an RGBA atlas under TextMeshPro's Sprite shader.
- `Runtime/Art/GameArt` is presentation's one door to it (by GML name; null when missing, so a view keeps its code-drawn
  fallback) and `GmlFontLabel` keeps a themed MaText in its GML font. Swapped so far: the shots (`spr_mortar` and
  `spr_railgun_swirl` turned along the flight, `spr_pellet`, `spr_raingun_ring`, `spr_grenade` and `spr_vortex_device`
  spinning 3 degrees a step and turning back at each bounce), a shell's `spr_smallexplosion` (obj_explosion, image_speed
  0.5) and a grenade's `spr_bigexplosion` (three times its size at 70%, image_speed 0.2); the crates' fall and land
  sprites with their kind's icon at 70% 34 px above, bobbing 5 px a pixel every five steps once landed; the match HUD,
  aim readout and world labels in `fnt_score`; the Ready card in `fnt_impact` and `fnt_mono` with their outlines.
- `Tests/EditMode/ArtCatalogueTests` checks the catalogue, the preset, the fonts, their outlines and their metrics;
  `Tests/Core.Tests/GmlFontTests` the glyph table and the distance field; `Tests/PlayMode/ArtShowcaseTests` (explicit,
  needs graphics) saves screenshots of the art in a match to `Screenshots/`.
- Git LFS holds the PNGs (`.gitattributes`); the .meta files, the JSON and the generated .asset files are text.

Deviations: (1) draw_text_outline draws the text nine (or 25) times at whole-pixel offsets, a square outline; the rebuild
draws one SDF outline, whose corners are round. (2) The shots are no longer tinted in their player's colour (the rebuild
did that with its squares; GameMaker's draw_self draws them as they are): the trail keeps the colour. (3) The grenade and
vortex spin at 3 degrees per 60-a-second step, where the original spun 3 degrees per step at whatever `shootFPS` the
machine reached during a shot. (4) The HUD labels keep the rebuild's layout and fit their text to its boxes in the
original's faces, rather than drawing at the original's sizes in its places. (5) U.RICO-T45 (tanks, tracks: done, see
Tanks), U.RICO-T46 (backgrounds, clouds, borders, shapes: done, see Level backdrop), U.RICO-T47 (the rail's swirls, the
laser, the vortex's implosion, the items on the level: done, see Weapon and hazard visuals), U.RICO-T48 (scorch marks,
glass, flame, the player explosion), U.RICO-T49 (the HUD's icons, health bars, turn arrow), U.RICO-T51 (the announcers'
portraits and speaker), U.RICO-T79 (the items in flight, the endgame flashes) and U.RICO-T78 (the menus, the pad and the
rooms' pictures: done, see Menus and pictures) swap the other sprites.

## Tanks (U.RICO-T45)

Source: `objects/obj_player` (the turret, spr_tank_top, depth 0), `objects/obj_bottom` (the hull, spr_tank_bottom,
depth 1), `objects/obj_tracks` and `sprites/spr_tracks` (depth 2), `scripts/setup_players.gml`, `align_bottom_to_top.gml`,
`spawn_tracks.gml`, `move_forward.gml` / `move_backward.gml`.

- `Runtime/TankView` draws each player as the original did: spr_tank_bottom under spr_tank_top at the original's pixel
  size, both tinted the seat's colour (setup_players' `image_blend = pColor[i]`), faint
  out of the round as the squares were. The turret is on the aim (the owner's own responsive aim, everyone else's synced
  one). The hull starts on the turret's angle (`bottom[i].image_angle = player[i].image_angle`) and, while the tank
  drives, turns a degree a step the short way toward the turret (align_bottom_to_top, from move_forward / move_backward).
- While it drives, a spr_tracks print (4x64, untinted) is laid under the hull at the hull's angle on the tenth step and
  every fifth step after (obj_bottom's `trackTime = 10`, spawn_tracks' `trackTime = 5`; the count is not reset when the
  tank stops). Each print holds at alpha 0.7 for its 400-step alarm, then loses 0.003 a step and is destroyed once it
  reaches 0 (635 steps, about 10.6 s at 60). Prints stay where they were laid; a new level sweeps them away, as the
  original's room change did.
- Core's `TankMotion` / `TrackLayer` hold the step rules (tested in `Tests/Core.Tests/TankMotionTests`); the view runs
  them at the room speed (`ArtCatalogue.RoomSpeed`). "Driving" is the authority's held drive key (`PlayerState.DriveDirection`,
  synced as `Player.TankMoving`), the original's `isMoving`, so a tank pushing against a wall still turns its hull and
  lays prints, as in the original. The name over the tank stays `PlayerLabels`' world label (U.RICO-T38, in fnt_score
  since U.RICO-T44); the turn marker over the tank is spr_turnarrow since U.RICO-T49 (HUD details).
- Without the art (an empty catalogue) the player keeps its code-drawn square and barrel.

Fixed defect: `isMoving` is a global and spawn_tracks runs in every obj_bottom's Step, so in the original every tank on
the board laid prints under itself while the current one drove, piling them under the tanks standing still. Only the
driving tank lays tracks in the rebuild.

Not carried: `obj_head` (spr_head) and `spr_torso` are a leftover the project file no longer lists: a person-shaped body
from before the tanks (obj_head followed `playerX`/`playerY`/`playerPreSwagger`, which nothing sets). They stay imported
under `Art/Sprites/Unlisted` and are drawn by nothing.

## Level backdrop (U.RICO-T46)

Source: `scripts/setup_backgrounds.gml` (called by `randomize_level`), `rooms/gamescreen` (its tiled `bg_dirt`, eight
cloud instances and four border instances), `objects/obj_cloud_parent` and `obj_cloud1-4`, `obj_spotlight`, `obj_lights`
and `scripts/draw_light.gml`, `scripts/draw_borders.gml` (obj_game's Draw) with `spr_top_border`, `spr_bottom_border`,
`spr_left_border`, `spr_right_border`, `scripts/setup_shapes.gml` and the eighty `obj_polygon/triangle/bar/circle/square1-16`.

- `Assets/StreamingAssets/Settings/backdrop.json` holds it all as data (validated by `GameData`): the three floor groups
  (bg_group1 outdoor 16, bg_group2 tiles and patterns 16, bg_group3 skins and stucco 5; `bg_potato` is in two, as in the
  original), the eight clouds where gamescreen put them, the cloud alpha (0.15), speed (a pixel a step) and wrap range, the
  spotlight's place and pulse, the lights' count, radius and colour ranges, and the border sprites with draw_borders'
  `border` (32).
- Core's `Backdrop.Plan` rolls a round's backdrop the way the original did: one floor from each group, then one of the
  three groups (so each group is a third of the rounds, and a bg_group3 floor is likelier than any one bg_group1 floor),
  then obj_lights' lights: x, y, a radius of 50 to 300 and each of red, green and blue 32 to 196. Its seed is
  `Backdrop.SeedFor(LevelSeed, LevelVersion)`, which every peer already holds (Arena, synced since UR-T23), so all peers
  draw the same floor and lights without anything new on the wire, and the version moves every round and reroll, so a
  hand-laid map gets a new floor each round too. `Backdrop.DriftCloud` and `SpotlightPulse` are the clouds' and the
  spotlight's step rules (`Tests/Core.Tests/BackdropTests`).
- `Runtime/BackdropView` (made by `Game` beside `LevelView`) draws, on every peer: the floor tiled at its own pixel size
  under everything; the eight clouds at 0.15 drifting left a pixel a step at the room speed and, once wholly off the left,
  back in at the right edge at a height rolled from -100 to room_height - 100, standing still while the match is paused
  (isPaused stopped them); `spr_spotlight` at 960, 712 with its alpha swinging 0.05 to 0.15 by 0.001 a step; the ten
  lights as 2D point lights through `IRenderStyle.AddGlow` (draw_light's circle from the colour at the centre to black at
  the edge, brightening what is under it; none on the built-in pipeline); and the four border sprites, sorted over the fog
  of war as draw_borders (depth -1999) was over the fogscreen (depth -999). The original room is centred on the level
  (1920 px = 22 units); the clouds and spotlight are clipped to the framed area as the room's view clipped them.
- `LevelView` draws each shape of a random level as its sprite (`GmlSprites.Shape`: spr_polygon1-16, spr_triangle1-16,
  spr_bar1-16, spr_circle1-16, spr_square1-16) at the origin and turn `RoundLayout.Shapes` records, in the sprite's own
  colours (the shapes carry their colour in their art; obj_polygon's image_blend stays white), over the clouds (depth -2)
  and under the tanks; its walls are the outline of that sprite's collision mask (UR-T60). A shape whose sprite is missing
  falls back to its outline, and a hand-laid map's walls stay lines. The arena's edge is draw_borders' red line.
- `Tests/PlayMode/BackdropTests` plays it; `ArtCatalogueTests` checks every name backdrop.json and the shapes use is in the
  art; `ArtShowcaseTests` saves `backdrop-practice` and `backdrop-random` screenshots.

Deviations: (1) The rebuild's arena is the whole of what was the original's room, so the border sprites frame it from
outside (the sides 32 px, the top and bottom 64 px, stretched a little to meet at the corners) rather than covering the
room's edges inside it, and the floor reaches under the frame. (2) obj_spotlight stood only in the splashscreen room; the
story puts it over the level, at the place and pulse it had there. (3) The lights are URP 2D point lights, which brighten
what they fall on, where draw_light blended a gradient circle over the drawn room.

Fixed defects: obj_lights is placed in no room and created by nothing, so the original never drew its lights; the rebuild
does, as the object was clearly meant to. Its loop runs to `i <= lightCount`, eleven lights for a count of ten, and rolls
each light's y over `room_width`, putting nearly half of them below the room; the rebuild lays ten, over the room's height.
gamescreen's `obj_border_top` shows `spr_bottom_border` (a copy-paste slip; draw_borders draws `spr_top_border` there),
and the top frame here is `spr_top_border`.

Border (U.RICO-T106): obj_game's keys 33 (PageUp) and 34 (PageDown) step `border` down or up by 32 (32 to 256) and call
`randomize_level` again; R rerolls only. `LevelSettings.Border` carries it, `LevelSeeds` packs its step into the round's seed (so
every peer lays the same walls from the seed alone; the dice are now the low 18 bits of a packed seed), and the authority keeps it
between rounds. `LevelGenerator.Inset` pulls the playfield in by the extra border pixels on x and twice that on y, as the original did;
the 32 px stock border is the whole arena, with its frame outside as before, and `BackdropView` grows the frame inward to meet the
smaller level. In a match the page keys reroll the round only before its first shot (the pause menu's Randomize rule).
Deviation: the vertical inset is clamped so the playfield keeps 6 of the arena's 12 units of height to seat every player; the
laid hazards and practice dummies keep their fixed places, so a thick border can leave one outside the playfield.

Not carried: `obj_ground` (placed nowhere: a surface that would subtract `spr_polygon1` at 0, 0 once for every shape, an
unfinished idea) and `obj_hex` (a hex sprite placed only in `room0`, the startup room the rebuild does not carry). `bg_redslate` is in no group, and its
image is missing from the original, so nothing is lost there.

## Weapon and hazard visuals (U.RICO-T47)

Source: `scripts/draw_beam.gml`, `draw_dottedline.gml`, `laser_create/step/draw.gml`, `new_laser.gml`, `pulse_step.gml`,
`shoot_railgun.gml`, `railgun_step/draw.gml`, `generate_swirl.gml`, `swirl_create.gml`, `grenade_create/step.gml`,
`grenade_countdown.gml`, `draw_weaponinfo.gml`, `draw_grenadefuse.gml`, `drawgui_fuse.gml`, `draw_fuse.gml`,
`vortex_create/step/draw.gml`, `vortex_detonate.gml`, `draw_vortexdetonate.gml`, `draw_electricity.gml`, `draw_glow.gml`,
`redirector_step.gml`, `redirector_detonate.gml`, `hit.gml`, and the objects `obj_laser`, `obj_laser_pulse`,
`obj_laser_connector`, `obj_rail`, `obj_rail_swirl`, `obj_rail_ring`, `obj_grenade`, `obj_grenade_explosion`, `obj_vortex`,
`obj_vortex_child`, `obj_boom`, `obj_boom_normal`, `obj_explosion`, `obj_shieldwall`, `obj_redirector`, `obj_aimassist`,
`obj_lasersight`, `obj_shotgun_blast`, `obj_pellet`, `obj_tracer`, `obj_tesla`, `obj_shrinkray(_child)`, `obj_teleporter`,
`obj_flash`, `obj_flashemitter`.

- Core's `WeaponLooks` and `Vortex` hold every number and step rule (`Tests/Core.Tests/WeaponLooksTests`); the views run them
  at the room speed (60 steps a second), as U.RICO-T44 settled for the spins.
- **Preview and aim assist** (`DottedBeam`): draw_beam's dotted line, 6 px dashes with 6 px gaps, black and 4 px wide under
  white and 2 px wide, along every leg of the path from the muzzle (each leg its own draw_dottedline, so the dashes start
  afresh at a corner; the last dash is drawn whole, as the original's loop does). The preview is at full alpha; the aim
  assist's fades out over its 5.5 s (obj_aimassist's 0.003 a step).
- **Laser** (`LaserBeam`): no sprite at the head (obj_laser's Draw is laser_draw alone): laser_color (255, 0, 63) 4 px at 0.7
  under white 1 px at 0.9 from the muzzle to the head through every bounce (new_laser's segments). obj_laser_pulse's
  spr_raingun_ring is sent from the muzzle 40-60 steps in and every 25-75 after (obj_laser's alarm), riding the beam at the
  head's speed turned to its heading. Once the last bounce is spent the beam's back end runs after the head at five times its
  speed (laser_step's `originX += dX * 5`) until it is gone, and the pulses fade 0.02 a step. The rebuild flies the laser at a shell's speed too (shoot_laser.gml, U.RICO-T105), so the pulses keep the spacing the original's head covered in those steps.
- **Railgun** (`Rail`): no sprite at the slug: railgun_draw's railColor (200, 255, 200) line 9 px at 0.1 and 1 px at railAlpha,
  and a spr_railgun_swirl every 56 px from the muzzle (generate_swirl) tinted railColor at railAlpha, each lighting the fog
  (swirl_create's lightSize 1). railAlpha starts at 0.8 and loses 0.005 a step: the rail is gone 160 steps (2.7 s) after it
  is fired. The original's rail was there at once; the rebuild's slug flies at eight times a shell's speed and the rail is
  drawn as far as it has reached.
- **Grenade**: spr_grenade spinning (U.RICO-T44); the turn card's weapon line counts the fuse down to the hundredth while it
  flies (draw_weaponinfo's `fuseLeft / shootFPS`, grenade_countdown), and draw_grenadefuse's red hint ("You can adjust the
  grenade fuse by pressing [1-3]", the rebuild's keys) shows while the owner holds it on their turn. obj_grenade_explosion is
  spr_bigexplosion at three times its size at 0.7 (U.RICO-T44) under a white circle at 0.7 growing 2 px a step; both are gone
  when the circle reaches grenadeRadius (75 steps), so the sprite is cut at its sixteenth frame, as in the original.
  Grenade rules (U.RICO-T95, T136): it bounces off a living tank (obj_grenade's obj_player collision: move_towards_point at -bulletSpeed, so it leaves
  away from the tank's centre at full speed; not a bounce, and the tank is not hurt by it). Its blast grows 2 px a step to grenadeRadius = 150 px
  (1.71875 units, 150 * UnitsPerPixel) and hurts only the first player the circle reaches (check_grenade_intersection sets the explosion's collision
  after one hit): the player reached in the fewest 2 px steps, the lower seat on a tie. The thrower can be that player.
- **Vortex** (`Implosion`): spr_vortex_device grows from a quarter of its size 0.01 a step in flight. Where it goes off it
  plays obj_boom (set off, image_speed 0.1) or obj_explosion (spent, 0.5), then vortex_step's modes: spr_vortex at 0.5 growing
  from nothing 0.002 a step to its full size while turning a degree a step backwards, obj_vortex_child at its size over it
  turning 4 a step forwards from 90, 300 steps waiting, then shrinking 0.01 a step while vortex_draw's white circle at 0.7
  spreads 10 px a step, and 300 more steps of circle. It lights the fog at 0.7 while it shows.
- **Shots landing**: a shell that runs out of bounces plays obj_explosion where it stops (bullet_step); a pellet, a laser and
  a rail play nothing there (pellet_step destroys the pellet, laser_step's explosion is commented out). Whoever a shot strikes
  gets obj_boom_normal's spr_smallexplosion (image_speed 0.5) on them as it lands, on every peer (hit.gml; `SpriteBurst`).
- **Shield wall**: spr_shieldwall across the aim, untinted, playing at image_speed 1 and fading in 0.01 a step to 0.9.
  **Redirector**: spr_redirector_arrow along the way it sends a shot, from 0.3 of its size growing 0.01 a step.
- **Electric arc**: draw_electricity's bolt, redrawn every few frames through points shaken off the line, with its glow
  circles as soft lights through `IRenderStyle.AddGlow` (draw_glow's circles, the same code) every other vertex, shaken half
  as far. **Laser gate**: drawn as laser_draw draws a laser.
- `Tests/PlayMode/WeaponLookTests` plays the laser, rail, vortex, grenade circle, hit flash and preview on a client;
  `HazardTests` checks the wall's and redirector's art; `ArtShowcaseTests.TheWeaponsAndHazards` saves `U.RICO-T47-*`
  screenshots (preview, laser, laser running out, railgun, vortex in flight, imploding and blown, grenade circle, items,
  hazards).

Deviations: (1) The vortex's implosion runs at the shot's step rate (`WeaponLooks.ShotStepsPerSecond`: the steps a shell at
the rebuild's speed takes to cover bulletSpeed's 3 px, about 524 a second), not at 60: the original ran it at shootFPS
(1020 asked, whatever the machine reached) during the shot, and at 60 its 1,200 steps would last 20 s, well into the next
turn; it lasts about 2.3 s. (2) The cannon, shotgun, grenade and vortex keep the rebuild's trail in the player's colour
(U.RICO-T44's deviation 2); the laser and railgun draw the original's beam and rail instead. (3) The preview draws the whole
bounce path in draw_beam's style; the original drew draw_beam only for the aim assist and the laser sight (a straight beam
to the first wall) and had no path preview (RECONSTRUCTION's aim row). (4) draw_electricity drew additively; the rebuild's
bolt is alpha-blended and its glow is 2D point lights (none, and a faint wide line instead, on the built-in pipeline).
(5) The redirector keeps the rebuild's turn per turn (UR-T25), drawn at its heading, rather than spinning a degree a step.

Not carried: `obj_laser_connector` and `obj_rail_ring` (created nowhere: their only creations are commented out; the
connector would have drawn laser_draw's look, which the beam already is), `drawgui_fuse` and `draw_fuse` (called by nothing;
draw_weaponinfo's countdown is the one the original showed), `draw_glow` as its own call (its only call, in railgun_draw, is
commented out and malformed; railgun_draw's 9 px line is that glow), `obj_tesla` (no sprite and no events), `obj_tracer` (the
AI's invisible probe, drawn only in debug) and `obj_shotgun_blast` (a controller with nothing to draw). The items in flight
(obj_teleporter, obj_shrinkray, a redirector before it stops) and the endgame room's camera flashes (obj_flash,
obj_flashemitter: the endgame room's only use of them) are U.RICO-T79's (below); the shield wall's glass is U.RICO-T48's.

## Hit and knock-out effects (U.RICO-T48)

Source: `scripts/hit.gml`, `show_float_text.gml`, `show_float_text_ext.gml`, `show_float_text_color.gml`, `spawn_glass.gml`,
`use_item.gml`, `add_random_weapon/item/health.gml`, `clear_objects.gml`, `railgun_step.gml`, `globals.gml` (darkgrey, brown),
and the objects `obj_boom`, `obj_boom_normal`, `obj_explosion`, `obj_grenade_explosion`, `obj_playerexplosion`, `obj_flame`,
`obj_scorchmark`, `obj_scorchmark_small`, `obj_glass`, `obj_shieldwall`, `obj_float_text`, `obj_hitscore`,
`obj_powerup_explosion`, `obj_powerup`, `obj_weapondrop`, `obj_itemdrop`, `obj_healthdrop`.

- Core's `HitLooks` holds every number and step rule (`Tests/Core.Tests/HitLooksTests`); the views run them at the room
  speed. A crate and a hazard say how they left the level (`DropEnd`, `HazardEnd`), and the authority raises `CrateGone` (taken
  or burst) and `ShieldWallShattered` (struck, swallowed or decayed; nothing for one swept away with the round), which the
  server sends every peer once (`Player.Flourish`) and each raises on the Presentation feed. A shot's outcome carries the
  points it scored (`ShotOutcome.Points`), so every peer floats them when its own flight lands.
- **Floating text** (`FloatText`, obj_float_text): fnt_score outlined 1 px in black, drawn 10 px over its spot, rising a
  pixel a step and losing its fadespeed of alpha a step (0.008, gone in 125 steps; 0.01 from the _ext and _color callers);
  flashing text moves through white, red, yellow, blue, blue, white, one a step (colorCycle 1-5 and 0, where 5 matches no case
  and keeps the blue). hit.gml floats "+N pts" over the shooter (flashing) and "-N" in red over whoever it struck; a crate's
  "Laser x 1 collected" in white (health's "+N" in green) over its collector; use_item's "Bounce +1" (flashing) over whoever
  used an extra bounce. They replace the rebuild's earlier name-label pickup and damage figures (UR-T38, UR-T12).
- **Scorch marks** (`FieldEffects`): obj_scorchmark_small under obj_boom_normal (a hit), obj_explosion (a shell spent, a
  vortex spent) and obj_boom (a vortex set off); obj_scorchmark under obj_grenade_explosion, obj_playerexplosion and
  obj_powerup_explosion. Each lies under the tracks and tanks, losing 0.0001 of its alpha a step (10,000 steps).
- **Knock-out** (obj_playerexplosion): spr_bigexplosion at image_speed 0.5 holding its last frame (image_index 19),
  sfx_explode, a big scorch mark, and 30 steps in obj_flame: an ef_smokeup every step, each in its own dark grey (0-40 a
  channel), 5 px over the spot, until the round goes. Every peer plays it from the server's result once its own flight lands.
- **Shield wall breaking** (obj_shieldwall, spawn_glass): ef_ring and ef_firework in white and 20-30 shards of spr_glass1-5,
  each scattered up to 20 px either way, turning -2..2 degrees and moving -5..5 px a step either way, losing 0.02 of its alpha
  a step (50 steps); sfx_shatter when a shot (or a vortex) broke it, silent when it decayed.
- **Crates**: falling, ef_smokeup in darkgrey (32, 32, 32) 25 px over them every step; landing, ef_firework in brown
  (165, 42, 42) 10 px under them and sfx_crash; obj_powerup, landed, ef_spark in white or orange 30 px over itself (less its
  hover) every step; the others' icon keeps U.RICO-T44's hover. Taken, an ef_ring 25 px over where it stood; left too long,
  obj_powerup_explosion (spr_bigexplosion at 0.5, gone at its end, sfx_explode, a big scorch mark).
- **The round going** (a new round, a reroll, the session ending: the level's version, map or seed changing) sweeps the
  scorch marks, the held blasts and their smoke, the glass and the floating text away, as clear_objects did.
- Sounds play through `GameAudio`, which honours the profile's and the session's mute: sfx_shatter, sfx_crash and
  sfx_explode as the original's recordings (U.RICO-T50, "Sound effects" below).
- `Tests/PlayMode/HitEffectTests` plays the knock-out (points, damage, blast, smoke), a scorch mark swept by a reroll, a
  shield wall's glass, "Bounce +1" and a crate's smoke, landing, ring and burst on a client; `ArtShowcaseTests.
  TheHitAndKnockOutEffects` saves `U.RICO-T48-*` screenshots (glass, crates falling, landing and sparking, knock-out and its
  smoke).

Deviations: (1) GameMaker's runner drew ef_ring, ef_firework, ef_smokeup and ef_spark, not the game, so `GmlParticles` draws
the rebuild's reading of them (`HitLooks.Look`: a ring spreading 8, 12 or 20 px a step for 12 steps; 20-50 sparks of 4 px
flying apart for 15-30 steps; a soft puff 24 px and growing, rising 1-1.5 px a step for 30-40 steps; a 10 px flare shrinking
away in 8-12 steps), tinted as the original asked. (2) "+N pts" floats once per volley with the points it scored; hit.gml
floated it per hit, and since it destroyed the volley's other pellets on the first, a volley rarely made two. (3) The
knock-out blast is drawn at the knocked-out tank; hit.gml made it at the striking bullet, which was touching the tank.
(4) A shield wall a railgun breaks plays sfx_shatter too (railgun_step's shatter was silent); one a vortex swallows breaks
like one a shot struck (obj_shieldwall's vortex collision). (5) Two crates collected by one shot float only the last's text
(the synced pickup count's last value). (6) sfx_flyover, sfx_powerup and sfx_powerup_activate are U.RICO-T50's ("Sound effects").

Fixed defects: obj_glass rolled one of five but drew spr_glass1 for two of the rolls and spr_glass5 for none, though the
sprite shipped; each of the five is drawn. Not carried: obj_hitscore (created nowhere; obj_float_text is the same text).

## Item flights and endgame flashes (U.RICO-T79, the items' play matched in U.RICO-T80)

Source: `scripts/use_item.gml`, `shoot_teleporter.gml`, `teleport_player.gml`, `shoot_shrinkray.gml`, `shoot_redirector.gml`,
`redirector_step.gml`, `redirector_detonate.gml`, `globals.gml` (bulletSpeed, lime), the objects `obj_teleporter`,
`obj_shrinkray`, `obj_shrinkray_child`, `obj_redirector`, `obj_flash`, `obj_flashemitter`, and the `endgame` room.

- Core's `ItemLooks` holds every number and step rule (`Tests/Core.Tests/ItemLooksTests`). The authority flies a teleporter,
  a shrink ray or a redirector before it takes effect and `ItemUsed.Flight` carries that path; the server draws it and sends it
  to every client (`Player.ItemFlown`, packed by `ShotWire.EncodeFlight`), so every peer flies the server's own path.
  `ItemFlights` draws them at the room speed, each at its multiple of the rebuild's shot speed, and a new level sweeps them away.
- **The items' play** (U.RICO-T80): each item that flies takes effect when its flight arrives, as the original's did. The
  authority keeps every item in the air and moves it on each frame at the same speed the peers draw it
  (`GameAuthority.AdvanceItems`, called by the shooter's `Player` beside its shot's flight): a teleporter puts its shooter down
  the frame it arrives (teleport_player), a shrink ray strikes then. The turn's end and a new level destroy every item still in
  the air without effect (next_player, randomize_level: `ItemFlightsCleared`, sent to every peer). The teleporter flies on
  through tanks and hazards, turned by a standing redirector, until it first touches a shape or the border, and teleports its
  shooter there, Len = 40 px back along its heading (obj_teleporter's Step and collision; `Arsenal.Teleport`). The redirector
  flies on at bulletSpeed * 2, bouncing off the border and the shapes only (redirector_step), until its owner presses the item
  key again: that press stops it where it is and it opens out there (redirector_detonate; `RedirectorStopped`, sent to every
  peer, ends its drawn flight). One redirector a turn (hasFiredRedirector). Fixed defect: the original's use_item charged that
  second press as well as the launch (and shoot_redirector charged the launch twice, even an unlimited count), and refused the
  press once the count was spent, so the last redirector could never be stopped; the rebuild charges one redirector once, as it
  sets off, and always obeys the press that stops it.
- **Teleporter** (obj_teleporter): spr_teleporter's six frames at image_speed 1, at bulletSpeed * 2, its image_angle set to the
  aim once and never turned. teleport_player: a small white ef_ring where the shooter stood as it sets off; where the shooter
  lands, a medium ef_ring in white and one in the shooter's colour and a small white ef_firework.
- **Shrink ray** (obj_shrinkray): at bulletSpeed * 4, an ef_spark (small, lime: globals.gml's make_color_rgb(128, 255, 128))
  every step where it is; it passes through tanks and hazards and stops on the first shape or border piece it touches (its
  only collision is obj_polygon_parent), a medium lime ef_firework and sfx_shrink there. The shape it struck shrinks away
  (U.RICO-T90: obj_polygon_parent's shrinking, image_xscale 0.01 smaller a step, image_blend a random one of c_blue, c_red,
  c_orange and c_yellow each step, destroyed at 0; Core's `ItemLooks.ShapeScaleAt`, `LevelView.ShrinkShape`) and is gone from
  the level: a `Shrunk` hazard (`HazardKind.Shrunk`, the shape and its range of walls) that every peer builds the level
  without (`HazardField.Build`, `Level.WithoutWalls`), until the next round. A border piece it strikes shrinks under
  draw_borders' sprites (drawn over it at depth -1999) and the border still bounces by position, so nothing changes. It
  never shrinks a tank: the rebuild's earlier shrunken tank (`ArsenalRules.ShrinkScale`) is gone. Its spr_shrinkball is never drawn: obj_shrinkray's
  Draw event is `//draw_self();` (so its 20-degree-a-step turn shows nowhere), and shoot_shrinkray's obj_shrinkray_child (the same
  ball turning the other way) is commented out and never created. The rebuild draws the ray as the original did, by its sparks.
- **Redirector** (obj_redirector, "flying"): spr_redirector_arrow at bulletSpeed * 2, from 0.15 of its size growing 0.01 a step
  to 0.30, turning 3 degrees a step and set back to its heading at every bounce. The standing redirector (`LevelHazard`) is
  hidden until the flight lands and only then opens out from 0.3 (redirector_detonate) and plays sfx_redirector_grow.
- **Endgame flashes** (obj_flashemitter, obj_flash): while the endgame recap is up (`EndgameRecap`), one obj_flash a step at
  irandom_range(0, 1920), irandom_range(0, 1080) in the room; one in the lower half and the middle third across (the pedestal)
  is destroyed at once; the rest play spr_flash's six frames once at image_speed 1 and go (`EndgameFlashes`, on a canvas of its
  own just over the recap, taking no clicks). They stop when the recap closes.
- `Tests/PlayMode/ItemFlightTests` flies each item on a host and a client (sprite, angle, size, sparks, rings, the waiting
  redirector) and runs the flashes.

Deviations: (0) The struck shape leaves the level for shots at once, while the original's shrinking shape still stood in the
way, smaller each step, for the 100 steps it took to go: a shot is solved whole when fired, against the level as it stands.
(1) A flight is a path the authority solves when the item is used, so it can cross the wire whole: an item's path
is solved for 60 seconds of flight (`ItemLooks.RedirectorMaxSeconds`, the longest turn), and a redirector that flies all of it
unstopped is gone with nothing placed, as the original's was when its turn ended. (2) The flashes are drawn over the rebuild's
endgame room (the recap, UR-T9, drawn as the original's podium since U.RICO-T56).

Not carried: shoot_*'s ef_smoke at the muzzle (the weapons' launch puffs are not carried either), obj_shrinkray_child (never
created), and the shrink ray's own sprite (never drawn).

## HUD details (U.RICO-T49)

Source: `scripts/setup_weapons.gml`, `setup_items.gml`, `draw_angle.gml`, `draw_scores.gml`, `draw_turntime.gml`,
`draw_weaponinfo.gml`, `draw_iteminfo.gml`, `gui_draw.gml`, `setup_players.gml`, `next_player.gml`,
`mouseover_message_draw.gml`, `mouseover_powerup_draw.gml`, `draw_text_outline(_color).gml`, `globals.gml` (border, maxhp),
the gamescreen room's instances, and the objects `obj_icon_weapon_cannon/laser/shotgun/grenade/railgun/vortex`,
`obj_item_extrabounce/shieldwall/shrinkray/teleporter/aimassist/redirector` (and the unplaced `obj_item_extrabounces`,
`obj_item_lasersight`), `obj_lbl_count_*`, `obj_infinity`, `obj_weapon_icon`, `obj_hover_icon`, `obj_displaybox`,
`obj_degree`, `obj_degree_gui`, `obj_turnarrow`, `obj_guide`, `obj_indicator_extrabounce`, `obj_healthbar`.

- Core's `HudLooks` holds every position and rule (`Tests/Core.Tests/HudLooksTests`); positions are the original room's
  pixels, which the HUD canvas's 1920x1080 reference matches one for one, and `GameArt.PixelsToUnits` in the world.
- **Icons** (`HudIcons`, obj_icon_weapon_* / obj_item_*): weapons on the row at room_height - border*2, items on
  room_height - border, the n-th at border*(n+1) + 10n (32, 74, 116, 158, 200, 242). setup_weapons / setup_items: the _off
  sprite at 0.2 with none carried, the plain sprite at 1 carried, the _selected sprite for the one in hand; over a carried
  one its count in fnt_count outlined 2 px at x + 28, y + 15, or spr_infinity at x + 28, y + 24 for an unlimited one
  (obj_infinity). They show the current player's loadout (player[current]); in practice the local player's. A click on one
  (Left Pressed) selects it for the local player on their own turn, through `SelectWeaponCommand` / `SelectItemCommand`
  (refused with none carried or a shot in the air, as `MayAct` refuses cycling). On a canvas of their own that takes the
  pointer, sorting with the HUD under the announcer cards and every menu.
- **Hover text**: hovering an icon says "Name: what it does" in mouseover_message_draw's white fnt_score, from arsenal.json's
  `about` (a deviation, below).
- **Angle** (`TurnGuide`, draw_angle / obj_degree): while the turn runs (Aiming or a shot in the air: isReady && turnStarted),
  the current player's angle in whole degrees in fnt_score outlined 1 px, its top left at the tank's x and 36 px under it
  while it aims up (image_angle < 180), 60 px over it otherwise; spr_symbol_degree at string_width / 2 + 20 from its left
  edge. obj_degree_gui: the local player's angle the same way over the pad's turn buttons (`ControlPad`), as it sat by
  obj_btn_degreeleft.
- **Guide** (`TurnGuide`, obj_guide): while the turn runs and its player has not driven, circles of 60 and 61 px in their
  colour at 0.7 and of 59 and 62 px in white at 0.4 round the tank, and a tick along the aim from 49 to 71 px, 3 px wide in
  white, under one from 50 to 70 px, 1 px wide in their colour.
- **Turn arrow** (obj_turnarrow): spr_turnarrow's six frames at image_speed 0.2 in the column left of the current seat's
  slot, where the original drew it at x - sprite_width beside the score; and over the current tank, turned to point down at
  it, in place of the rebuild's yellow diamond (which stays when the art is missing).
- **Seat slots** (`MatchHud`, draw_scores / obj_healthbar): draw_scores' spr_icon_player or spr_icon_computer, the name in
  the player's colour and the score; under them obj_healthbar's 171x25 bar: spr_healthbar_back at 0.25,
  spr_healthbar_fill in the player's colour scaled to the health shown (image_xscale), spr_healthbar over them and
  "shown/max" in fnt_count outlined 1 px, centred, 4 px down. The shown health keeps U.RICO-T67's easing. Code-drawn
  rects and an "[AI]" tag when the art is missing.
- **Extra-bounce indicator** (obj_indicator_extrabounce): spr_indicator_extrabounce just left of the angle readout while the
  current player has an extra bounce on this turn.
- The aim line (draw_bounceinfo, U.RICO-T44) moved right of the icons, to x 290 where draw_weaponinfo wrote.
- `Tests/PlayMode/HudDetailTests` plays the icons' sprites, counts and infinity sign, a click on someone else's turn
  ignored and on Ann's own selecting the grenade and the shield wall, the hover text, the slots' health text, art bar and
  arrow, the tank's arrow, guide and angle with its degree sign, and the pad's angle; `ArtShowcaseTests.TheHudDetails`
  saves `U.RICO-T49-*` screenshots (the HUD, the current tank close up, a hover).

Deviations: (1) The icons' hover text is the rebuild's: the original's icons had no Mouse Enter (mouseover_message_draw
served the setup room's labels and buttons), and it drew at room_height - 30, where the rebuild's aim line runs, so it sits
just over the icons. (2) A slot keeps the rebuild's layout (U.RICO-T17's two rows of eight that shrink to fit, the turn
outline, the score and "away" / "OUT" / "next round"); the original's single row of 200 px columns held at most eight.
(3) The turn arrow is drawn over the tank as well as beside the slot (the original had only the slot's), replacing the
rebuild's diamond there. (4) A click plays sfx_flick on a carried icon and sfx_disabled on one with none (U.RICO-T50).

Fixed defect: obj_indicator_extrabounce was never created (no instance in any room, no instance_create), so its sprite
never showed; it is drawn while an extra bounce is on, at the angle readout it was meant to follow (its Step set only its y,
to draw_angle's).

Not carried: `obj_lbl_count_*` (the setup room's counts with spr_infinity_large; the setup is the kit's front end),
`obj_displaybox` (obj_multiplayer_host's name boxes; the kit's lobby), `obj_weapon_icon` and `obj_infinity` (created
nowhere; the icons draw spr_infinity themselves), `obj_item_extrabounces` and `obj_item_lasersight` (not in the room; the
laser sight is dormant), `mouseover_powerup_draw` (the dormant powerup HUD stubs, RECONSTRUCTION 1.2). `obj_hover_icon` is
the crates' floating icon (U.RICO-T44's). `obj_bar1-16`, `obj_circle1-16` and `obj_square1-16` are the random level's
shapes (`GmlSprites.Shape`, U.RICO-T46), not HUD markers.

## Menus and pictures (U.RICO-T78)

Source: every `obj_btn_*`, `obj_btn_*_less/_more/_last/_next`, `obj_checkbox_*`, `obj_inputbox*`, `obj_lbl_*`, `obj_pausemenu`,
`obj_btn_fire/useitem/endturn`, `obj_btn_degreeleft/degreeright`, `obj_btn_moveforward/movebackward`,
`obj_keyboardlayout`, `obj_slide`, `obj_creditsimage`, `obj_letter_*`, and the
rooms `titlescreen`, `controls`, `howtoplay`, `credits` and `gamescreen` (U.RICO-T84: and `mainmenu`, `settings`,
`players`, `names`, `maproom` and `multiplayer_*` for the rooms' places and background).

- Core's `MenuLooks` names every sprite and font and holds the rules (`Tests/Core.Tests/MenuLooksTests`). Every one of these
  controls drew itself the same way: at alpha 0.5 its `_over` sprite while the mouse was on it or its `_back` sprite
  otherwise, then its face sprite at 1 (`GmlPlate`).
- **Menus** (`GmlMenuSkin`, one watcher, as `UiSounds` is, that dresses every MindAttic Components control as it appears,
  whichever screen built it: the kit's front end, the game's rooms, the lobby, the pause menu, the endgame recap): a button
  is spr_button over spr_button_back / spr_button_over (nine-sliced at 12 px to the kit's width), its label in fnt_mono
  outlined 2 px, white; a switch is spr_checkbox_unchecked / _checked over spr_checkbox_back / _over, its text in fnt_score
  outlined 2 px; a text field is spr_inputbox over spr_inputbox_back (no hover), its text in fnt_score; a slider's or
  dropdown's caption (obj_lbl_*) is fnt_mono outlined 2 px, its value fnt_score, with spr_arrowleft / spr_arrowright either
  side stepping it one notch a click and wrapping past either end, as every less / more arrow did, with sfx_select; section
  headings are fnt_mono outlined 2 px; a screen's title is fnt_impact outlined 2 px (the rooms' titles), the pause menu's
  "Paused" fnt_mono outlined 2 px (obj_pausemenu). The keyboard's focus draws as the mouse over did.
- **Pad** (`ControlPad`): the turn arrows spr_tinyarrowleft / right, the drive arrows spr_tinyarrowup / down, Fire, Use Item
  and End Turn spr_tinybutton with the label in fnt_count (not outlined), each over its _back / _over sprite, in its own
  proportions inside the pad's button.
- **Pictures**: the title is on bg_titlescreen with obj_letter_R ... obj_letter_T where the titlescreen room placed them;
  the controls room shows spr_keyboardlayout, the how-to-play room obj_slide's spr_howtoplay1 between its counter and its
  text, and the credits room spr_creditsimage.
- **Controls and how-to-play art** (U.RICO-T54; source `obj_Controls`, `obj_keyboardlayout`, `obj_mouse`, `obj_slide`,
  `obj_btn_nextslide` / `_lastslide`, `obj_howtoplay`, `obj_howtoplayimage`): the keyboard picture is redrawn for this
  game's controls (Core's `KeyboardPicture`, drawn by `KeyboardPictureView` over the room's spr_keyboardlayout image, which
  keeps its place and size but is no longer drawn): a full keyboard (main block, navigation block, number pad), every key
  bound in the Input System's Gameplay map as it is now (after the rebind screen) coloured for its action, and each action's
  name at the end of a line from its leftmost key, above or below the keyboard in lanes that keep the names apart, as the
  original's were; the three grenade fuses are one name, the border's thinner and thicker one; Esc (Go back) and the
  number pad's quick angles are drawn though no binding names them. A mouse beside the number pad has its bound buttons
  coloured and named (left Fire, right Use item by default). A rebind or Reset to defaults redraws it at once. The
  gamepad's bindings are not drawn (the original had no pad; the rebind screen lists them). obj_mouse drew spr_mouse
  (18 x 25, tip at its top left) at the pointer in every room, so `GameCursor` draws it over everything (the room fade
  too) at the room's scale, hiding the system's arrow while the window has focus. Each how-to-play slide carries its own
  pictures (menu.json's `images`, every slide must list at least one; Core's `SlidePicture` lays up to four in a row, two
  rows past that, on the black 640 x 480 frame): the game's own sprites for what it explains (the player and AI icons,
  the weapons' and items' icons, the crates, the hazards, the announcers' portraits). The original drew one blank frame
  for all ten slides; there are eleven here, with the counter showing "n/11". `Tests/Core.Tests/KeyboardPictureTests`.
- **Rooms** (U.RICO-T84, `GmlRoomLayout`, attached by the same sweep): every menu room (mainmenu, settings, players, names,
  controls, howtoplay, credits, maproom, the multiplayer rooms) was 1920 x 1080 on bg_titlescreen with each object's sprite
  centred on its place, so every menu screen now stands on bg_titlescreen (not over the live arena) with its card dissolved
  and its controls at the rooms' places and sprite sizes (Core's `RoomLayout`, `Tests/Core.Tests/RoomLayoutTests`): the
  title at (border, border) = (32, 32); obj_btn_back_* at (128, 1024); the way on (obj_btn_start / _setup / _players, the
  screen's Primary button) at (1792, 1024); obj_btn_default (Reset to defaults) at (1792, 928); a screen of buttons only
  (the main menu) as mainmenu's column at x 960, 96 apart, centred on y 544 (five at 352 ... 736); a slider or dropdown row
  with its arrows at spr_arrowleft / right's own 89 x 64; a switch with its 64 px checkbox before its text (obj_checkbox_*);
  obj_keyboardlayout at (960, 512), obj_creditsimage at (960, 576), obj_slide at (896, 576) with its counter above and its
  text below at the slide's left edge, and obj_btn_lastslide / _nextslide drawn as spr_arrowleft / right at (512, 576) and
  (1280, 576) (`GmlMenuSkin.MakeArrow`). The title screen (it draws its own room), the vote's pause menu and the kit's
  in-game and endgame menus (over the game, as obj_pausemenu and the endgame's bg_stadium room were) keep their look
  (`GmlNoRoom`, or a stack's closable root).
- `Tests/PlayMode/MenuArtTests` checks the dressing, the arrows' wrap, the title room, the pictures and the rooms' places
  and background; `MenuScreenTests` that the slide's arrows still turn it; `ControlPadTests` the pad's sprites;
  `ArtCatalogueTests` that every name resolves;
  `ArtShowcaseTests.TheMenusAndPictures` saves `U.RICO-T78-*` screenshots (title, settings, controls, how to play, credits,
  a match, paused).

Deviations: (1) The rebuild's screens hold controls the original's rooms had no object for (the kit's name and colour, the
lobby's code, the opponents' list, the settings' extra rules), so those flow down the room as the players room's rows did
(from y 272, 16 px apart, a new column where the room runs out, up to four columns, left of a picture that leaves room);
a button keeps the kit's width when its label is wider than spr_button's 226 px (nine-sliced, at most 420); the kit's
slider or dropdown stays between the arrows. A screen too long even for four columns (the settings, which the original
spread over its own hand-placed grid) keeps the kit's card, on bg_titlescreen. The controls room's binding lines (the
rebuild's, for its remappable bindings) are kept but not drawn, under the keyboard picture as the room had only the picture;
the credits room's lines roll over spr_creditsimage (U.RICO-T55, Credits roll, below). (2) The kit's own back icon is hidden on a taken screen in favour
of the room's Back button at (128, 1024); the title, the vote's pause menu and the kit's in-game and endgame menus are not
re-laid (they draw over the game, or their own room). (3) The title's letters drift as obj_letter_parent moved them and the
main menu repeats them (U.RICO-T52, below). The announcer's portrait and speaker are U.RICO-T51's (Announcer voices and
portraits).

Not carried: spr_pausemenu and spr_pausemenu_back (obj_pausemenu's draws of them are commented out), spr_label and
spr_label_small (only the labels' hover masks; never drawn), the picture labels spr_lbl_* and picture buttons spr_btn_start,
_quit, _settings, _controls, _howtoplay, _default and _credits (no object or script draws them), spr_back (obj_button_back is
placed in no room), spr_howtoplay (obj_howtoplayimage is placed in no room), spr_btn_config and spr_btn_add / _remove with
spr_checkbox_player / _ai (the title's setup panel and the players room's seats: the kit's front end and U.RICO-T52). The items
in flight (spr_teleporter, spr_shrinkball) and the endgame's spr_flash are U.RICO-T79's (Item flights and endgame flashes).

## Title and main menu (U.RICO-T52)

- **Letters** (Core's `TitleLetters`, `Runtime/Art/TitleBackdrop`): obj_letter_R, _I, _C, _O, _C2, _H, _E and _T, children
  of obj_letter_parent, start at hspeed 1 and vspeed -1; each step a letter past a room edge (x < 0 or > 1920, y < 0 or >
  1080) has that speed negated, then moves; moving left it shows its mirror letter (R and T, I and E, C and H, O and the
  second C swap), moving right its own. The titlescreen and mainmenu rooms ran at 180 steps a second; the title placed letter
  i at (64 + 64 i, 576 - 32 i), the main menu at (128 + 64 i, 416 - 32 i). Nothing in the original dropped them in: they
  drift from where the room placed them.
- **Floor**: both rooms are bg_titlescreen; the main menu's scrolls right a pixel a step (tiled), the title's stands still.
- **Title** (obj_titlescreen): stops any music as it opens; Esc quits; Start goes on to the Start screen, over the main menu
  (its Back is the main menu); Quit quits. The original also switched to full screen on Start; not carried (the window
  setting is the player's).
- **Music** (obj_jukebox, on the titlescreen and mainmenu rooms): whenever no track plays, play_random_music starts a random
  one of the five, once through (`GameAudio.Jukebox`, run while the front end is in its menus); the match plays its own.
- **Start screen** (the titlescreen room's setup panel, `Room.Setup`, `Menus.BuildSetup`): players (the AI opponents),
  shapes (5 to 40 in fives), type (abstract, triangles, bars, circles, squares, random), rotation (none, 45, 90, 360),
  overlap, goal, turn time (5 to 30 s) and bounces, Default, and Next to the players room. The control under the pointer
  shows the original's message on the line at the bottom (menu.json's `tips`). It is the main menu's first button, in
  place of Players (now its Next). The level settings are saved with the setup, travel in the match vote, and ride in each
  random round's seed (`LevelSeeds`: a seed of 0 or more is the stock settings, so old seeds lay the same rounds; a
  negative seed packs the shapes, type, rotation and overlap above its dice), so every peer lays the same round.
- `Tests/Core.Tests/TitleAndStartTests` holds the letters, the seeds and the setup's level settings to the original;
  `MenuFlowTests` the room graph; PlayMode `MenuScreenTests` the title's Start and the Start screen, `MenuArtTests` the
  letters' drift.

## Credits roll (U.RICO-T55)

Source: `obj_credits`, `obj_creditsimage`, `obj_btn_back_credits`, `spr_creditsimage`, `fnt_credits` and the `credits` room.

- **The original**: the credits room (1920 x 1080 on bg_titlescreen) held obj_creditsimage, which centred spr_creditsimage
  (486 x 714, origin 243, 357) with every name already painted into the picture; obj_btn_back_credits at (128, 1024), its
  "Main Menu" label in fnt_mono outlined 2 px, back to the main menu with sfx_click; and obj_credits, which only sized the
  view and toggled full screen on F1. Nothing scrolled and no credits text was drawn: fnt_credits (Verdana 10) is on disk
  but not in the project, and nothing sets it.
- **The rebuild** (Core's `CreditsRoll`, `Runtime/Art/CreditsRollView`, `Menus.BuildCredits`): the picture stands where
  the room put it (`RoomLayout.Credits`) and this project's own credits roll up over it, masked to the picture's rect: from
  just under its bottom edge until the last line has left its top, then again, restarting each time the screen opens, on
  unscaled time. menu.json's `credits` is a list of sections (a heading and its lines) written in this project's own words,
  crediting the original's maker, its idea, voices, music, art, sound effects and GameMaker, then the house kit
  (Unity.Multiplayer, MindAttic Components, Unity.Shared); `creditsScrollSpeed` is 60 room pixels a second, a pixel a step
  at the rooms' 60 steps (the pace the menu rooms' floors scrolled). Headings are fnt_mono outlined 2 px as the rooms'
  captions; lines fnt_credits outlined 1 px, white, centred. Without the picture's art the credits are a plain list.
  The room's Back is U.RICO-T84's at (128, 1024).
- `Tests/Core.Tests/CreditsRollTests` (the roll's place over time, its period and wrap), `GameDataTests` (the sections and
  speed load).

Deviations: (1) The roll itself: the original's credits were the still picture; the ticket's credits scroll over it. (2) The
picture is drawn at alpha 0.35 under the roll so its painted names do not fight the moving text; it still names every
freesound.org contributor, which the roll points to rather than repeats. (3) fnt_credits was never drawn by the original, so
it has no size to match: the lines draw it at twice its Verdana 10 face to read at the room's size.

## Endgame podium (U.RICO-T56)

Source: `obj_endgame`, `obj_pedastal`, `obj_pedastool`, `obj_winner_top`, `obj_winner_bottom`, `obj_flashemitter`,
`obj_flash`, `obj_btn_back_endgame`, `obj_fade_endgame`, `obj_win`, `check_endgame.gml`, `hit.gml`, `music_victory`,
`sfx_applause`, `sfx_woot` and the `endgame` room.

- **The original**: check_endgame (called when obj_win's "Wins!" card has faded) goes to the endgame room once a seat's score
  reaches the goal. The room is 1920 x 1080 at 60 steps a second on bg_stadium, tiled. obj_endgame's create stops the music
  and plays music_victory once (`audio_play_music(music_victory, 0)`), centres obj_pedastal (spr_pedastal, 278 x 247, origin
  139, 123) at (960, 864), sorts every seat by score (highest first) and stands the first three on it as obj_winner_bottom's
  hull (spr_tank_bottom, depth 1) under obj_winner_top's turret (spr_tank_top, depth 0), both blended in the player's colour:
  first at (960, 786), second at (871, 831) to the left, third at (1049, 880) to the right, each turret at image_angle 270
  (facing down). Its step spins the champion's turret alone: spinAngle starts at 90 and loses a degree a step. Its draw puts
  "Wins!" (fnt_impact, white, outlined 2 px) at the room's middle, the champion's name (fnt_mono, their colour, outlined 2 px
  black) a border (32 px) above it, and the leaderboard at the top: "Rank" / "Player" / "Score" in fnt_score at y 100 (left
  edge x 760, centre 960, right edge 1160) and a row a seat 30 px apart from y 130, "1st" ... "8th", the name and the score
  (a negative score shown as 0), in each seat's colour. obj_flashemitter's flashes go off everywhere but the pedestal
  (U.RICO-T79's), obj_fade_endgame brings it out of black (U.RICO-T53's), and obj_btn_back_endgame at (128, 1024), "Main
  Menu" with "Return to Title Screen" on hover, stops the music and goes to the main menu. obj_pedastool shares
  spr_pedastal but is placed nowhere; obj_win is the gamescreen's round card, not the podium's.
- **The rebuild** (Core's `Podium`, `Runtime/EndgameRecap`, `GameAudio`): on every peer, once the synced turn state says
  `GameOver`, the endgame room comes up on its own canvas over the game, the 1920 x 1080 room scaled whole into the screen
  on bg_stadium tiled across it (the room's silver without the art), every place, size, origin, angle, font and colour as
  above (`Podium.Spot`, `Podium.Angle`, `Podium.RowY`; the ranking is the existing `Recap.Rank`, at most eight seats,
  U.RICO-T100). The champion's turret turns a degree a step on the room's 60 steps. `GameAudio` stops the turn's track and
  plays music_victory once through as the game ends, and a rematch picks a new track. The Main Menu button stands at
  (128, 1024) and leaves the game; the vote for a rematch stands at (1792, 1024) (the rooms' way on), Close at (1792, 928).
  The flashes and the fade in are the existing `EndgameFlashes` and `ScreenFade`.
- `Tests/Core.Tests/PodiumTests` (the places, the spin, the rows, the track and the cheer); PlayMode `MenuFlowTests` (the
  recap's headline and lines on a client).

Fixed defects: (1) sfx_woot is in the original's folder and played by nothing; the podium is where it belongs, so it plays
with sfx_applause (the round's last knock-out's applause in hit.gml, heard again by the crowd at the podium) as the room
comes up. Deviations: (1) the vote for a rematch and Close are the rebuild's (the original had only Main Menu); Esc does not
go to the settings room and F1 does not toggle full screen (the window setting is the player's). (2) No confetti: the
original's endgame has none (its only celebration is obj_flashemitter's camera flashes), so none is invented. (3) The
champion's turret is drawn at 270 until the first step and at 89 from it, as the original's did; the jump is under the fade's
black.

## Room fades (U.RICO-T53)

- **One curtain** (Core's `RoomFade`, `Runtime/ScreenFade`): the original gave every room its own copy of one object
  (obj_fade, obj_fade_game, _mainmenu, _maproom, _settings, _controls, _credits, _endgame, _howtoplay, _multiplayer,
  _splashscreen): spr_black over the room at depth -10000, created at image_alpha 1 in mode "fadeout", its alpha stepping
  0.01 a step to clear and the mode going "off"; a room that left by fading to black first (obj_maproom, obj_startup_credits,
  obj_lotus_card_picture) set it to "fadein", and the room_goto came once it was black. The copies differ only in where that
  room_goto went, so the rebuild has one curtain on its own canvas over everything (sorting order 30000): `Open` brings a
  screen up out of black, `Cover` fades to black, makes the change, then opens.
- **Length**: 100 steps of 0.01 at the rooms' 60 steps a second, about 1.67 s, as data (menu.json's `fadeSeconds`), on
  unscaled time so a paused match does not hold it.
- **Which changes fade**: every screen pushed or popped on the kit's main menu and in-game menu (the setup rooms, settings,
  controls, how to play, credits, kick), the front end moving between the menus, the lobby and the game, and the endgame
  recap opening (obj_fade_endgame). Players' Start covers to black before it hosts (obj_btn_start's "fadein"; U.RICO-T53, T101), and the
  lobby comes up out of black. The curtain takes clicks only while it is going to black, so nothing is pressed twice
  before the change; clearing, it never blocks the new screen (the original's curtain sat over a room whose buttons still
  worked).
- **Click**: every obj_btn_* that changed rooms played sfx_click on Left Pressed. A change made with the pointer already has
  it from `UiSounds` (its press, or the release a uGUI button acts on), so the curtain plays sfx_click only for one made
  another way (Enter, Esc, a pad). sfx_select stays the less / more steppers' (U.RICO-T50).
- The splash and startup credits (obj_fade_splashscreen, obj_startup_credits, obj_lotus_card_picture; U.RICO-T57) open
  out of black and leave through this same `ScreenFade.Cover`. Deviation: the kit's busy dialog while
  joining or reconnecting comes up without a fade.

## Splash and startup credits (U.RICO-T57)

Source: the splashscreen and startup_credits rooms; object_splashscreen, obj_lotus_card, obj_lotus_card_picture,
obj_lotus_card_top, obj_line_emitter, obj_static_line, obj_spotlight, obj_startup_credits, obj_typewriter,
obj_creditsimage, obj_fade_splashscreen and obj_fade_credits.

- **Order**: the game opens on the splash, then the startup credits, then the title (`Game.ShowTitle`,
  `Runtime/SplashScreen`, Core's `Core/Menu/Splash.cs`). Each room leaves through the black curtain (Room fades). The front
  end's menus stay hidden, the jukebox waits and the pointer is hidden (`GameCursor.HiddenByRoom`) until the title is up.
  A headless process and a bot skip both rooms.
- **Splash**: on silver, sfx_tech loops as the room's music; spr_lotus_card_noborder stands at the room's middle with a
  second copy trembling a pixel every 2 steps toward a spot up to 6 px off; spr_spotlight pulses at (960, 712); spr_beach
  comes up on the card 0.008 alpha every 5 steps with sfx_startup, flickering (hidden, nudged up to 4 px, now and then
  spr_ryan, now and then one of sfx_static1-6) less as more of it shows, while faint black lines (0.3 alpha) fall down the
  card; spr_lotus_card_frame frames it; "A game by" (fnt_score) and "Ryan DeBraal" (fnt_impact), white outlined 2 px,
  under it. 200 steps after the picture is whole the room goes on.
- **Startup credits**: the music stops; spr_creditsimage at (960, 576) and a red note outlined in black at room_height - 60,
  typed a character every 2 steps with sfx_type_quiet on every second one. After 600 steps, or on a click, a pad's button
  or any key but F1 and F12, it goes on to the title.
- Fixed defects: (1) the original never reached the splash: the game started on startup_credits, so the splash was dead;
  the rebuild shows it first. (2) obj_lotus_card_picture waited for a key before its picture started to show, and the same
  key finished it, so the reveal was never seen; here it starts as the room opens and the first key or click finishes it
  (stopping sfx_startup). (3) The flicker's rarity was checked from alpha > 0.5 down, so the rarer settings (> 0.6 ... > 0.9)
  were never reached; the rebuild checks the rarest first.
- Deviations: the startup credits' note is this project's own words, typed out by the typewriter; the original warned of
  GameMaker trouble with a wireless Xbox 360 controller, which the rebuild does not have. Left out: obj_typewriter_intro
  (the empty intro room's, never reached), and F1 (window) and F12 (game_restart) on the credits room, which are only
  passed over, not acted on. room0 and intro hold nothing and are not rebuilt.

## Music (U.RICO-T27)

Source: `sound/music_*.sound.gmx` and `sound/audio/music_*.mp3`, `play_random_music.gml`, `play_next_track.gml`, `obj_jukebox`,
`randomize_level.gml`, `next_player.gml`.

- `Tools/import-gml-sound.ps1` also mirrors the music (eight mp3s, in Git LFS; music_spacefighter names no recording) into
  `Resources/Ricochet/Music`, each with a .meta that sets the importer to Streaming, and lists them under `music` in
  `gml-sound.json`. Core's `MusicBook` holds the tracks, play_random_music.gml's five (phatsketch, gocart, rocket,
  latinindustries, cutandrun by irandom_range(1,5)) and the volume (the profile's music volume, 0 when Music is muted or
  the session is muted).
- `GameAudio` watches the synced turn state on every peer: each new round (randomize_level) and each new turn (next_player)
  of a match picks a random one of the five and plays it on the looping music source, which carries the Settings Music volume
  and Mute music (and the pause menu's Music toggle, U.RICO-T43). Under a session mute the pick is made at volume 0 and
  nothing is saved. Tests read `CurrentTrack`, `MusicRequests` and the source's volume.
- Deviations: the original's `audio_play_music` calls are commented out in play_random_music.gml (so the shipped game
  played no music; the port plays it, as the ticket asks); its track restarts on every turn (a new pick, as the script's
  intent), looping; music_victory plays once through as a game ends (U.RICO-T56, Endgame podium); music_inaheartbeat and
  music_windsong are imported but, as in the original, never picked. A turn is noticed by the seat or round changing, so a
  turn passing back to the same seat (only one seat left taking turns) keeps its track; the original would have picked anew.
  The track stops when the match ends.

## Sound effects (U.RICO-T50)

Source: the original's `sound/sfx_*.sound.gmx` and `sound/audio/` (the 71 non-voice definitions; the announcers' voice

lines are U.RICO-T51's, the music_* tracks U.RICO-T27's, below), and every `audio_play_sound` / `audio_is_playing` /
`audio_stop_sound` in its scripts and objects.

- `Tools/import-gml-sound.ps1` mirrors each definition's recording (its `<data>`, a .wav for all but sfx_tech's .mp3) into
  `Assets/Ricochet/Resources/Ricochet/Sound/<name>`, in Git LFS, and records each one's definition, recording, bytes,
  GameMaker volume, kind and preload in `Assets/Ricochet/Audio/gml-sound.json`. 70 recordings; sfx_bounce_shotgun3 names
  no file and is listed as skipped. Every definition's volume is 1, so none is scaled.
- Core's `SoundCue` is one cue per recording (70) and `SoundBook` maps each to its recording and says which moment plays
  which, at the profile's effect volume, 0 when the player muted effects or the session is muted (U.RICO-T71).
  `GameAudio` loads the recordings from Resources and plays them; it records every cue it chose with the volume it gave
  (`Requests`, `LastCue`, `LastVolume`), so tests check what would be heard without hearing it.
- **Shots** (every peer, from the Presentation feed): the fire sound per weapon (shoot_bullet / laser / shotgun / grenade /
  railgun / vortex: sfx_fire_cannon, _laser, _shotgun, _grenade, _railgun, sfx_vortex_shoot); each contact a projectile
  passes, its own bounce (sfx_bounce_cannon for a shell and a vortex, _laser, _grenade, a pellet's sfx_bounce_shotgun1 or 2),
  sfx_bend off a rail redirector, nothing for a rail (railgun_step plays none); sfx_explode where it lands for whoever it
  struck (obj_boom_normal), a shell's last bounce (obj_explosion), a grenade (obj_grenade_explosion) and a vortex (obj_boom /
  obj_explosion). The laser's sfx_loop_laser starts once sfx_fire_laser ends and sounds again each time it ends until the
  laser lands; the grenade's sfx_grenade_beep sounds again each time it ends while it flies; the vortex's sfx_vortex_suck
  sounds while it pulls in and stops when sfx_vortex_blow starts as it blows out (vortex_step's modes).
- **Items and the round** (the server's flourish, every peer): the item's own sound as it is used (use_item: sfx_powerup_activate
  for an extra bounce, sfx_hologram for a shield wall going up, sfx_shrinkray_shoot, sfx_teleporter_shoot, sfx_zoom for an aim
  assist, sfx_redirector_shoot), sfx_shrink as a shrink ray strikes a shape or the border, sfx_teleport as a teleporter puts its tank down,
  sfx_powerup for a crate collected, sfx_beep on each of the turn clock's last four seconds, sfx_sigh for a turn that ran out
  and a knock-out mid-round, sfx_applause for the knock-out that ends the round (in place of its sigh, as hit.gml did). A
  redirector placed plays sfx_redirector_grow as it opens out (redirector_detonate).
- **Crates**: sfx_flyover as one starts to fall, stopped as it lands with sfx_crash; sfx_explode as one bursts.
- **Shield walls**: sfx_shatter when a shot breaks one (U.RICO-T48).
- **The Ready card**: sfx_switch as it goes up, sfx_sonar_ping as the tank is pinged unless it is still ringing (U.RICO-T42).
- **The owner's keys** (that peer only, as the original's were): sfx_engine_turnon as a drive key goes down, sfx_engine
  again each time it ends while it is held, sfx_engine_shutoff on release once the engine ran (play_movement_sound,
  stop_moving; Core's `EngineSound`); sfx_turn again each time it ends while the barrel turns and the tank stands, stopped
  on release (rotate_left / rotate_right); sfx_type for a numpad snap; sfx_flick for a weapon or item cycled; sfx_fuse1, 2
  and 3 for the fuse keys; sfx_disabled for firing a weapon or using an item with none left, or using one after firing.
- **The HUD icons**: a click plays sfx_flick on a carried weapon or item, sfx_disabled on one with none (obj_icon_* /
  obj_item_*'s Left Pressed).
- **The menus** (`UiSounds`): sfx_flick as the pointer comes over a control that can be pressed (the buttons' Mouse Enter),
  and on a press sfx_click for a button or toggle, sfx_select for a slider (the setup's less / more / last / next steppers),
  sfx_disabled for one that cannot be pressed.
- `Tests/Core.Tests/SoundTests` checks the 70 cues against the imported files and the manifest, and every mapping;
  `Tests/PlayMode/SoundEffectTests` checks every recording loads, every cue plays at volume 0 under a session mute with
  settings.json untouched, the profile's volume and mute, a shot's fire, bounces, loops and landing from the feed, a held
  cue, the round's applause in place of a sigh, and the menus' sounds.

Deviations: (1) A sound the original kept going with "if (!audio_is_playing(x)) audio_play_sound(x)" every step (the engine,
the turn, the laser's loop, the grenade's beep, the vortex) plays on a voice of its own whose length `GameAudio` keeps, so
it answers the same muted as heard. (2) The engine and turn sounds are the owner's own peer's, as the original's single
screen was; a shot's, an item's and the round's are every peer's. (3) The menus are the kit's (U.RICO-T9), so their sounds
come from one watcher over every Selectable instead of each obj_btn_*'s events; a slider stands for a stepper's pair of
less / more buttons and plays sfx_select once per press. A teleporter's and a shrink ray's effect sounds (sfx_teleport,
sfx_shrink) play when their flights arrive, as the original's did (U.RICO-T80); sfx_redirector_grow when its owner stops the
redirector and the standing one opens out.

Fixed defect: pellet_step's `choose(sfx_bounce_shotgun1, sfx_bounce_shotgun2, sfx_bounce_shotgun3)` picked sfx_bounce_shotgun3
a third of the time, and that definition names no recording, so a third of the pellet bounces were silent; the rebuild picks
between the two that exist.

Not played (no moment of the rebuild makes them yet): sfx_hologram_pass (a shot passing through its owner's own shield wall,
which leaves no contact on the rebuild's path), sfx_hacktivated (obj_game's debug key), sfx_blip (obj_selector), sfx_whoosh
(nothing in the splash or startup credits plays it; their sfx_tech, sfx_startup, sfx_static1-6 and sfx_type_quiet play
since U.RICO-T57), sfx_type for the name boxes
(the kit's lobby). sfx_bounce, sfx_bounce_shotgun (sfx_bounce_shotgun1's bytes),
sfx_dummy, sfx_fire, sfx_shoot_vortex, sfx_static and sfx_fuse4-6 are in the original's folder but played
nowhere; each is imported and has its cue. sfx_woot, also played nowhere in the original, cheers the endgame podium with
sfx_applause (U.RICO-T56, Endgame podium).

## Announcer voices and portraits (U.RICO-T51)

Source: the original's `sound/sfx_{sly,rye,sid,vlad,baxter,doctor,scottish}_{welcome,win,lose}1..10.sound.gmx`,
play_random_announcement, random_announcement_{welcome,win,lose}, random_<key>_<kind>, random_{welcome,win,lose}_number,
obj_announcer, obj_announcer_portrait, obj_speaker and the spr_profile_* sprites.

- After a shot lands the next turn waits obj_endturn's 50 steps (`MatchRules.EndTurnSeconds`), re-armed for another 50
  while the announcement said as it landed is still playing: the server holds the turn for the fewest whole alarms that
  outlast the recording (`Match.HoldTurnEnd`, the clip's length whether or not anyone hears it; U.RICO-T107).
- The match starts with Matt Baxter on the air, as the shipped config.ini and the Default button set it (globals.gml's
  "sly" never survived read_config; U.RICO-T108).
- The 210 voice lines (seven personalities x three kinds x ten) are imported by `Tools/import-gml-sound.ps1` into
  `Resources/Ricochet/Voice/<name>` (Git LFS) and listed under `voices` in `Assets/Ricochet/Audio/gml-sound.json`. Each is
  the .ogg the original's `sound/audio` holds beside the definition's .wav (the same take: the .wavs are 162 MB that Unity
  would re-encode to Vorbis on import anyway, the .oggs 6 MB).
- The line Core's `Announcer` draws (the existing no-repeat deck) picks the recording: `Announcer.Recording` gives
  `sfx_<key>_<kind><line + 1>`. When `HudOverlay` slides the card in, `GameAudio.Announce` plays it on a source of its own
  at the effect volume, first stopping a line still playing (play_random_announcement's audio_stop_sound). The card holds
  while it is heard (obj_announcer's audio_is_playing), with obj_speaker's spr_speaker beside the portrait flicking to a
  random frame every 5-10 steps. The written line stays on the card as the caption; muted (the player's effects mute or a
  session mute) nothing is heard and the card holds the caption for 4.5 s.
- Portraits are the original's own, named in `announcer.json` (`portrait`): spr_profile_sly, spr_profile_rye,
  spr_profile_sid, and spr_profile_dummy shared by Vlad, Matt Baxter, Doctor and Haggis McKnight, drawn as they are. The
  colour and monogram show only if the art is missing.
- Tested by `Tests/Core.Tests/AnnouncerTests.cs` (recording names, the card's hold, the portraits) and
  `Tests/PlayMode/AnnouncerTests.cs` (the card's portrait and the recording asked for, on a client).

Fixed defects: (1) five definitions name no recording (sfx_sid_lose3, sfx_sid_lose4, sfx_sly_lose4, sfx_sly_lose5,
sfx_vlad_lose4), so those lines played silence; each is filled from the voice actor's own take in the project's
`sfx/announcers` folder (NN-Loss-3.wav, sly_lose4.wav, ...), whose takes are byte-identical to the numbered recordings
wherever both exist. (2) Sid Steinburger has a drawn portrait, spr_profile_sid, that play_random_announcement never used
(it set spr_profile_dummy for him); the rebuild shows it.

Deviation: random_sly_* and random_rye_* drew with irandom_range (repeats possible) where the other five used the
no-repeat random_*_number scripts; every personality uses the no-repeat deck, as the rebuild already did. The eighth set
in the folder, sfx_announce_*, is not imported: no script or object plays it.
U.RICO-T101 (parity audit, port faithfully): Players' Start goes straight on to the game (obj_btn_start). The map room and the four
invented hand-laid maps (Crossfire, Pillars, Funnel and the menu's map picker) are out of the flow: `Room.MapRoom` and `Menus.BuildMap`
are gone, the Start button hosts through `ScreenFade.Cover` as the map room's Next did, the goal lives on the Start screen, and
every round is the random level (randomize_level); `Game` resets a remembered profile map to the random index on load. maps.json
keeps only the practice range, as a test fixture, and the random arena.
