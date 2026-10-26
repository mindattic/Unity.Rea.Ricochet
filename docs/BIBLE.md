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
(there is no music to step through until UR-T27); under the CLI session mute the game stays silent and only the player's
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
laser, the vortex's implosion, the items on the level: done, see Weapon and hazard visuals) and U.RICO-T78 (the rest) swap
the other sprites.

## Tanks (U.RICO-T45)

Source: `objects/obj_player` (the turret, spr_tank_top, depth 0), `objects/obj_bottom` (the hull, spr_tank_bottom,
depth 1), `objects/obj_tracks` and `sprites/spr_tracks` (depth 2), `scripts/setup_players.gml`, `align_bottom_to_top.gml`,
`spawn_tracks.gml`, `move_forward.gml` / `move_backward.gml`.

- `Runtime/TankView` draws each player as the original did: spr_tank_bottom under spr_tank_top at the original's pixel
  size (a shrink ray still shrinks it), both tinted the seat's colour (setup_players' `image_blend = pColor[i]`), faint
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
  speed (laser_step's `originX += dX * 5`) until it is gone, and the pulses fade 0.02 a step. The rebuild flies the laser at
  three times a shell's speed, so the pulses are spaced by the distance the original's head covered in those steps (3 px a
  step), which keeps their spacing.
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
(obj_teleporter, obj_shrinkray and its child, a redirector before it stops) and the endgame room's camera flashes (obj_flash,
obj_flashemitter: the endgame room's only use of them) are U.RICO-T79; the shield wall's glass is U.RICO-T48's (below).

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
- Sounds play through `GameAudio`, which honours the profile's and the session's mute: sfx_shatter and sfx_crash as the
  synthesised `Shatter` and `Crash` cues, sfx_explode as `Blast`, until U.RICO-T50's recordings.
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
(the synced pickup count's last value). (6) sfx_flyover, sfx_powerup and sfx_powerup_activate wait for U.RICO-T50.

Fixed defects: obj_glass rolled one of five but drew spr_glass1 for two of the rolls and spr_glass5 for none, though the
sprite shipped; each of the five is drawn. Not carried: obj_hitscore (created nowhere; obj_float_text is the same text).

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
rebuild's diamond there. (4) sfx_flick and sfx_disabled on a click wait for U.RICO-T50.

Fixed defect: obj_indicator_extrabounce was never created (no instance in any room, no instance_create), so its sprite
never showed; it is drawn while an extra bounce is on, at the angle readout it was meant to follow (its Step set only its y,
to draw_angle's).

Not carried: `obj_lbl_count_*` (the setup room's counts with spr_infinity_large; the setup is the kit's front end),
`obj_displaybox` (obj_multiplayer_host's name boxes; the kit's lobby), `obj_weapon_icon` and `obj_infinity` (created
nowhere; the icons draw spr_infinity themselves), `obj_item_extrabounces` and `obj_item_lasersight` (not in the room; the
laser sight is dormant), `mouseover_powerup_draw` (the dormant powerup HUD stubs, RECONSTRUCTION 1.2). `obj_hover_icon` is
the crates' floating icon (U.RICO-T44's). `obj_bar1-16`, `obj_circle1-16` and `obj_square1-16` are the random level's
shapes (`GmlSprites.Shape`, U.RICO-T46), not HUD markers.
