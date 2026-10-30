# Ricochet: the game's Unity folder

Scaffolded by **MindAttic ▸ Multiplayer ▸ New Game…** (Unity.Multiplayer's game template, UR-T2/UR-T3), then built
into the finished game: turn-based, bounce-shot artillery for up to eight tanks, human or AI, turning the barrel and
banking the shot off the level's shapes and the border onto an opponent before they land one on you — a faithful
Unity 6 rebuild of the 2013 GameMaker 8 game *Ricochet*, with real networked multiplayer in place of passing one
keyboard around one machine. The parity record against the original is [docs/RECONSTRUCTION.md](../../docs/RECONSTRUCTION.md)
(section 7); the design is [RFC 0001](../../docs/rfc/0001-architecture.md); what the game is, its laws and its
deviations are [docs/BIBLE.md](../../docs/BIBLE.md).

Open `Game.unity`, press **Play**, click **Play** at the title, then **Start** on the Start screen (players, shapes,
rotation, overlap, goal, turn time, bounces) and **Next** through the players room (names, colours, each AI's own
skill and name) into the lobby, whose **Start** hosts the match (or **Host** on the main menu opens a free practice
lobby instead). A second copy (Multiplayer Play Mode, or a build) clicks **Join by IP** (`127.0.0.1`) or **Join by
code**. **A/D** (or Left/Right) turn the barrel, **W/S** drive (forfeiting the shot), **Space** (or left mouse) fires
— and, while a vortex device is in flight, sets it off there — **Q/E** cycle the weapon, **Z/C** the item, **Ctrl**
(or right mouse) uses the item, **1/2/3** set a grenade's fuse and **F** ends the turn. The numpad snaps the aim to
one of eight directions, **Alt** pings everyone, **PageUp/PageDown** thin or thicken the border and reroll the level
(**R** rerolls alone). The dotted line is the whole path the shot will bank through. There is no power: a shot flies
at the original's constant speed until its bounce limit runs out. **P**/**Pause** (or the right stick's press) pauses
the match (the host at once, anyone else by a vote); **Esc** opens the kit's in-game menu: **Invite players**,
**Settings** (every match rule, the announcer, sound), **Controls** (every binding can be rebound, kept in your
profile), **How to play**, **Credits**, and the votes to start, restart, pause (a non-host) and kick (never yourself,
the host or an AI). **F1** toggles fullscreen.

## A round and a match

Every round lays a fresh random level from the Start screen's shape count, kind, rotation and overlap (the original's
80 shape outlines), a random backdrop and track, and the fog of war if the host turned it on; every tank starts at a
random spot clear of the shapes. Up to eight tanks — humans in join order, then the AI — take seat order on a turn
clock (20 seconds by default, 5 to 30); only the seated tank whose turn it is may aim, drive and fire, and the turn
passes when its shot lands, it drives, or the clock runs out. A hit does the weapon's damage (100 by default,
configurable from 5/10/25/33/50/100) to a 100-health tank and scores the shooter 100 points, 25 a bounce and 5 a
tenth of a second the shot was in the air; the bars along the top are every seat's health, the outlined one has the
turn, and the line under the clock is the turn order of who's still standing. The last tank standing takes the round
and opens the next; a round that ends with a seat at the goal (500 points by default, 500 to 10000, or one round)
ends the match, and the endgame podium shows the top three with a vote for a rematch.

## Weapons, items and hazards

Six weapons (cannon, laser, shotgun, grenade, railgun, vortex) and six items (extra bounce, shield wall, shrink ray,
teleporter, aim assist, redirector) are cycled with Q/E and Z/C; weapon, item and health crates fall onto the level
during a round and go to whatever tank or shot reaches them. The vortex, set off in flight, pulls the level's shapes
in and flings them out hurting whatever they strike, swallowing shield walls, redirectors and foliage it reaches and
breaching any border side it reaches, which shots then fly out through for the rest of the round. An electric arc, a
laser gate that switches every other turn, and foliage a shot cuts down and stops in are built but laid only when the
scene asks for them (off by default, since the original never placed them). The aim-assist item turns a human's aim
onto the shot a fully skilled AI would take, drawn as a fading beam.

## AI, the announcer and your profile

AI opponents pick their shot as the original's `check_ai` did: the first whole degree whose straight line reaches an
opponent, else the soonest bank or railgun line, else a random degree, nudged by its own skill (0-10, set per AI with
its name on the players room). **Settings** also picks one of seven announcer personalities and how often they speak;
their portrait and name box slide in with a welcome, a cheer naming both tanks on a knock-out, and a heckle when a
shot hits nobody. Your name, colour, key bindings and sound (effects and music volume, each mutable) are your
MindAttic profile's (`Unity.Shared`, `Profiles.Current.Settings.Common`), read by the kit's front end and written
back when a screen that changes them closes; `GameAudio` plays the original's own sound effects (no synthesised
cues) at the profile's volume and re-reads it when the game window regains focus.

## Playing together

**Host**, **Join by IP** and **Join by code** on the main menu replace the original's hotseat rooms outright. Hosting
opens the kit's lobby: the join code (or, with no join-code service, the address to join at), the roster, and
**Start**, which waits until the lobby could seat two before playing the match everyone in it set up on the Start and
players screens. A dropped player keeps their seat for the kit's reconnect grace (60 seconds by default): their tank
stays put, marked away, their health, score and turn order held, and the turn waits with "Waiting for <name> to
reconnect (Ns)"; back in time with the same profile they resume the same turn, not back in time they drop out of the
round and the turn passes. A join code needs the project linked to Unity Relay (Edit > Project Settings > Services)
or a rendezvous URL on the NetBootstrap; without either, players join by IP.

## Layers (docs/rfc/0001-architecture.md, docs/BIBLE.md §6)

| Folder | What lives there |
|---|---|
| `Core/` (`Rea.Ricochet.Core`, no engine references) | Every rule as plain C#, grouped by domain: `Aim/` (`BounceSolver`, `ShotPath`, `ProjectileFlight`, `Geometry`, `AimState`: the original's `LineLineIntersection_X/_Y`, `lines_intersect` and `bullet_step`'s reflection, with no power since U.RICO-T88 — a shot flies at a constant speed until its bounce limit runs out); `Level/` (`LevelGenerator`, `ShapeLibrary`, `Hazards`/`HazardField`, the border and its breaches); `Match/` (`Match`, `MatchRules`, `Seating`, `MatchSetup`, `Announcer`); `Ai/` (`Ai`, `AiTurn`: the targeting sweep and its turn); `Arsenal/` (`Loadout`, `Arsenal`/`Volley`, `Drops`/`DropField`, `VortexStorm`); `Menu/` (`MenuFlow`, `Splash`, `RoomFade`, `Lobby`, `Podium`, `SetupMemory`, `CreditsRoll`); `Art/` (`WeaponLooks`, `HitLooks`, `ItemLooks`, `Backdrop`, `TankMotion`, `HudLooks`, the fonts' `SignedDistanceField`); `Net/` (`ShotWire`, `ShotOutcome`: a solved volley packed for the wire and unpacked, and the damage it dealt); `Commands/` (`GameAuthority`: every input is a command carrying a player id, returning ordered events — the one door every other layer calls through, plus `Commands`, `GameEvents`, `PlayerState`); `Data/` (`GameData`, `SettingsReader`: loads and validates the JSON in `Assets/StreamingAssets/Settings`). Tested without Unity: `dotnet test Core.slnx`. |
| `Runtime/` (`Rea.Ricochet.Runtime`) | The networked game and its drawing. `Player` (a `NetEntity`: `[ToServer]` aim/fire/drive/item calls, `[Synced]` aim, seat, health; the server runs every call through `GameAuthority` and plays its events; an AI seat is the same entity from the `Opponent` prefab), `Turns` (the keyed match state every peer reads: phase, whose turn, the clock, the round), `Dummy`/`Crate`/`LevelHazard` (keyed practice targets, falling crates and hazards every peer rebuilds the level from), `Arena`/`ArenaMatch`/`ArenaLevel`/`ArenaCrates`/`ArenaPause` (the server's rules: the level, the match and AI turns, drops, hazards, pause, and the start/restart/kick/pause votes), `Game`/`GameBot` (scene glue and the bot); presentation reads only synced state: `MatchHud` (its own uGUI, built in code — no IMGUI) with `HudOverlay`/`HudIcons`/`HudParts`/`TurnGuide` (the announcer's and turn cards), `EndgameRecap`/`EndgameFlashes` (the podium), `TankView`/`PlayerView`/`PlayerLabels`, `BackdropView`/`LevelView`/`ShotView`/`FieldEffects`/`ItemFlights`/`WeaponEffects`/`FogOfWar`, the rooms (`SplashScreen`, `TitleScreen`, `Menus`, `LobbyRoom`, `PauseMenu`), `SetupMemory`, `GameAudio`/`UiSounds`, `GameInput`/`InputBindings`/`ControlPad`/`OnScreenControls`/`GameCursor`, `RenderStyle`, `ScreenFade`, `Lines`/`Shapes`/`WorldText`/`MenuTip`. |
| `Editor/` | `RicochetBuild` (the Windows player build, `Tools/run.ps1`), `CoreBoundaryValidator` (keeps `Rea.Ricochet.Core` engine-free), `RenderSettingsSetup`, and the `Art`/`Fonts` importer presets and builders that read `gml-art.json`/`gml-fonts.json`. |
| `Art/`, `Fonts/`, `Audio/` | The original's sprites, backgrounds, fonts and sound descriptions imported by `Tools/import-gml-*.ps1` (Git LFS; each `gml-*.json` records every file's original path). |
| `Resources/Ricochet/` | `ArtCatalogue` (every imported asset by original name), the baked TextMeshPro fonts, music, the original's own sound effects and the announcers' recordings. |
| `Settings/` | The URP render pipeline assets (`Ricochet_High`/`Ricochet_Low` and their 2D renderers) `RenderSettingsSetup` wires up — not the game's rules and content, which are the validated JSON in `Assets/StreamingAssets/Settings/` (match, shot, arsenal, hazards, level, shapes, backdrop, announcer, menu), read by `Core/Data/GameData`. |
| `Tests/` | EditMode and PlayMode suites on the kit's `NetSessionTest`/`NetBotTest`: a full turn and shot round trip, every weapon and item, the AI's targeting and aim assist, every hazard, the announcer, the menu flow and endgame, the lobby and reconnects, the kick, pause and restart votes, the art and sound catalogues, and `HudNoImguiTests`/`CoreBoundaryTests` holding the two house rules (no IMGUI, Core stays engine-free). |

`GamePrefabs.asset` (this folder) and the `NetPrefabList` it holds are the player, AI opponent, turn-state, dummy,
crate and hazard prefabs the NetBootstrap registers. The wizard also wrote `mp-host.json` and
`.github/workflows/nightly.yml` at the project root, put `Game.unity` first in Build Settings and turned on Run In
Background.

## Tests

- Core: `dotnet test Core.slnx` from the project root. CI holds `Rea.Ricochet.Core`'s line coverage to a floor
  (`Tools/coverage-floor.ps1`).
- PlayMode: `powershell -File Tools/unity.ps1 -Log test-playmode -Extra "-runTests -testPlatform PlayMode -assemblyNames Rea.Ricochet.Tests"`.
- EditMode: `powershell -File Tools/unity.ps1 -Log test-editmode -Extra "-runTests -testPlatform EditMode -assemblyNames Rea.Ricochet.EditorTests"`.
- Bots end to end: `mp-host game smoke` (a Linux server and two bots under Docker Compose, run nightly by
  `.github/workflows/nightly.yml`).
