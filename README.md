# Unity.Ricochet

Turn-based bounce-shot artillery for up to eight tanks, human or AI: turn your barrel, bank the shot off the level's
shapes and the border, and land it on an opponent before they land one on you. A faithful Unity 6 rebuild of the 2013
GameMaker 8 game *Ricochet*, with the one thing the original could never do: real networked multiplayer in place of
passing one keyboard around one machine.

![Unity](https://img.shields.io/badge/unity-6000.4-black) ![Netcode for GameObjects](https://img.shields.io/badge/netcode-2.x-blue) ![.NET](https://img.shields.io/badge/dotnet-10-512BD4) ![Status](https://img.shields.io/badge/status-complete-brightgreen)

## What it is

Every room and system of the original is rebuilt and checked against it (the parity record is
[docs/RECONSTRUCTION.md](docs/RECONSTRUCTION.md), section 7):

- **The startup and menus**: the studio splash and startup credits, the title with its drifting RICOCHET letters, the main
  menu, the Start screen (the title room's setup panel: players, shapes, rotation, overlap, goal, turn time, bounces), the
  players room (names, colours, AI opponents with their own skill and names), settings (the match rules, the announcer,
  sound), controls, how to play and the credits roll, every screen on the original's art and faded through its black curtain.
- **A round**: a fresh random level (the original's 80 shapes laid as `setup_shapes` did), every tank at a random spot, a
  random backdrop and track, fog of war if it is on, then a Ready card and a sonar ping before the clock runs.
- **A turn**: turn the barrel (a degree a step), drive (forfeiting the shot), set a grenade's fuse, cycle weapon and item, use
  the item, fire, or end the turn; a dotted preview shows where the shot will bank. There is no power: a shell flies at
  the original's constant speed until its bounces run out.
- **Six weapons and six items**: cannon, laser, shotgun, grenade, railgun and vortex; extra bounce, shield wall, shrink ray,
  teleporter, aim assist and redirector, with crates dropping onto the level during play.
- **Scoring and the end**: a hit scores 100, 25 a bounce and 5 a tenth of a second in the air; the last tank standing takes
  the round, and the first to the goal (500 by default, or one round) stands on the endgame podium.
- **AI opponents** that pick their shot as the original's `check_ai` did, nudged by their skill.
- **An announcer**: seven on-air personalities with their own recorded welcome, cheer and heckle lines and portraits.
- **The original's art, music and sound**: every sprite, background, font, track and effect imported from it.

New in this rebuild, because the original had no network layer: **Host**, **Join by IP** and **Join by code**, a lobby, the
server deciding every shot, votes (start, restart, kick, pause), a reconnect grace that holds a dropped player's seat, and a
MindAttic profile that keeps your name, colour, sound, key bindings and last match setup.

## Quick start

Prerequisites: Unity 6000.4.3f1 and the house kit cloned beside this repo (all siblings under `D:\Projects\MindAttic\`):

```powershell
git clone https://github.com/mindattic/Unity.Multiplayer
git clone https://github.com/mindattic/MindAttic.Components
git clone https://github.com/mindattic/Unity.Shared
```

`Packages/manifest.json` lists the three as local packages. Open the project in Unity Hub (the first open rebuilds
`Library/`), open `Assets/Ricochet/Game.unity` and press Play. From the title: **Start** > the Start screen > **Next** > the
players room > **Start** hosts the match and opens the lobby; its **Start** plays it. **Host** on the main menu opens a free
practice room. A second copy (Multiplayer Play Mode, or a build) joins with **Join by IP** (`127.0.0.1`) or **Join by code**
(a join code needs the project linked to Unity Relay or a rendezvous URL on the NetBootstrap; without one, join by IP).

### Controls

| Keys | Gamepad | Does |
|---|---|---|
| A / D, Left / Right | left stick | turn the barrel |
| W / S | left stick up / down | drive (forfeits the shot) |
| Space, left mouse | A | fire (and set off a vortex in flight) |
| Q / E | shoulders | cycle the weapon |
| Z / C | d-pad left / right | cycle the item |
| Ctrl, right mouse | B | use the item |
| 1 / 2 / 3 | X / Y / d-pad up | grenade fuse |
| F | d-pad down | end the turn |
| numpad | | snap the aim to one of eight directions |
| Alt | select | ping everyone |
| PageUp / PageDown | | thinner or thicker border, and reroll |
| P, Pause | right stick press | pause (the host at once, anyone else by a vote) |
| Esc | | the kit's in-game menu: invite, settings, votes |
| F1 | | fullscreen |

Every binding can be changed on the Controls screen; a rebind is kept in your profile.

### A built player

`powershell -File Tools/run.ps1` builds `Build/Ricochet/Ricochet.exe` in Unity batch mode (the Editor must be closed; the
log is `Logs/deploy.log`). `powershell -File Tools/play.ps1` launches it, building it first when missing or with `-Build`,
**muted by default** (`-muted` and `MINDATTIC_CLI_MUTED=1` for that run only, so a CLI or agent never plays sound; the
player's saved volumes are untouched). Add `-Sound` for audio.

## Tests

- Core (the rules, no Unity): `dotnet test Core.slnx`. CI holds `Ricochet.Core`'s line coverage to a floor
  (`Tools/coverage-floor.ps1`).
- EditMode and PlayMode: `powershell -File Tools/unity.ps1 -Log test-editmode -Extra "-runTests -testPlatform EditMode
  -assemblyNames Ricochet.EditorTests"`, and the same with `-testPlatform PlayMode -assemblyNames Ricochet.Tests`. The
  PlayMode suite plays real host and client sessions on the kit's `NetSessionTest` / `NetBotTest` fixtures.
- Bots end to end: `mp-host game smoke` (a server and two bots, as in `mp-host.json`; run nightly by
  `.github/workflows/nightly.yml`).
- `.github/workflows/ci.yml` runs Core with its coverage floor, the Unity suites (when the Unity licence secrets are set) and
  a Windows player build on every push and pull request.

## Architecture

Three layers, one direction ([RFC 0001](docs/rfc/0001-architecture.md) has the design):

- **Core** (`Assets/Ricochet/Core`, assembly `Ricochet.Core`, no engine references): every rule as plain C#, grouped by
  domain (`Aim`, `Arsenal`, `Level`, `Match`, `Ai`, `Menu`, `Art`, `Net`, `Data`, `Commands`). All play goes through
  `Commands/GameAuthority`: each input is a command carrying a player id, and it returns ordered events. Tuning and content
  are validated JSON in `Assets/StreamingAssets/Settings` (match, shot, arsenal, hazards, level, shapes, maps, announcer,
  menu, backdrop, registry), read by `Data/GameData`.
- **Runtime** (`Ricochet.Runtime`): the networked entities on Unity.Multiplayer (`Player`, `Turns`, `Crate`, `LevelHazard`,
  `Dummy`), the server's `Arena` that feeds commands to Core and plays its events, and the presentation, which only reads
  synced state and the `Presentation` event feed: the level, tanks, shots and effects, the HUD, the menus on MindAttic
  Components dressed in the original's art (`Runtime/Art`), audio, fades, splash, podium.
- **The house kit**: [Unity.Multiplayer](https://github.com/mindattic/Unity.Multiplayer) (netcode, front end, lobby, votes,
  reconnects), [MindAttic.Components](https://github.com/mindattic/MindAttic.Components) (UI widgets) and
  [Unity.Shared](https://github.com/mindattic/Unity.Shared) (profiles and settings). This project writes no RPC, menu
  framework or settings file of its own.

```text
Assets/Ricochet/
  Core/                engine-free rules (above)
  Runtime/             networked entities, the server's Arena, presentation; Art/ draws from the original's art
  Editor/              build, art and font importers, validators
  Art/, Fonts/, Audio/ the original's sprites, backgrounds, fonts and sound descriptions (Git LFS; gml-*.json say where each came from)
  Resources/Ricochet/  the ArtCatalogue, TextMeshPro fonts, music, effects and announcer recordings
  Tests/               EditMode and PlayMode suites
Assets/StreamingAssets/Settings/   the rules and content as data
Tests/                 dotnet projects compiling Core for `dotnet test Core.slnx`
Tools/                 run.ps1 (build), play.ps1 (launch muted), unity.ps1 (batch mode), import-gml-*.ps1
docs/
  BIBLE.md             what the game is and is not, the laws, the game as built, the deviations
  RECONSTRUCTION.md    the original's inventory and the parity record
  rfc/0001-architecture.md   the design
  USER_STORIES.md      generated from the ticket database (tickets db render)
```

## Deviations from the original

The rebuild keeps the original's play and fixes only its bugs. What differs, and why, is in the
[BIBLE, section 7](docs/BIBLE.md#UR-§7): mostly the network (real multiplayer, server authority, votes, reconnects and a
lobby instead of the hotseat rooms), the house kit and profile in place of `config.ini`, the modern input system with
rebinding and gamepads, and defects of the original that are fixed.

## Source material

The GameMaker original lives outside this repo and is read for reference only: no GML source is copied in
([UR-LAW-2](docs/BIBLE.md#UR-LAW-2)). Its art, fonts and sound are imported by `Tools/import-gml-art.ps1`,
`import-gml-fonts.ps1` and `import-gml-sound.ps1`, which record each file's original path; the PNGs and recordings are in Git
LFS (run `git lfs install` once). The ticket history is [docs/USER_STORIES.md](docs/USER_STORIES.md), or
`tickets ticket queue --repo Unity.Ricochet`.
