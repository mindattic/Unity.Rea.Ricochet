---
codex: 1
project: Unity.Ricochet
code: UR
layer: reconstruction
status: living
updated: 2026-10-23
---

# Unity.Ricochet: Reconstruction record (the REA of the original)
> What the 2013 GameMaker original contains, what the rebuild has of it, and the ordered list of tickets that closes the
> difference. Written by UR-T30 (the parity and standards audit, owner 2026-10-23). No GML is copied here
> ([UR-LAW-2](BIBLE.md#UR-LAW-2)); everything is described in this project's own words.

Source read: `R:\Projects\_PRE_2022\_PRE_2021\_PRE_2020\_PRE_2017\gmlRicochet\09-05-13_2.20am` (GameMaker 8.1 project,
"Beta 18"), compared with `main` at the commit that lands this file.

## 1. Inventory of the original

| Kind | Count | What |
|---|---|---|
| Objects | 469 | Game flow, 8 seats of buttons and labels, 16 each of polygon, square, triangle, circle and bar level shapes, weapons and projectiles, items, drops, hazards, effects, menu widgets |
| Scripts | 196 | Level and player setup, turn flow, weapons and items, AI, bounce maths, drawing helpers, announcer picks, config read and write |
| Rooms | 18 | titlescreen, mainmenu, names, players, maproom, gamescreen, endgame, settings, controls, howtoplay, credits, splashscreen, startup_credits, intro (empty), room0 (startup only), multiplayer_hostorjoin, multiplayer_host, multiplayer_join |
| Sprites | 315 | Tank, shots, effects, icons (weapon and item in normal, off and selected states), buttons, borders, shapes (16 each of five kinds), portraits, UI |
| Backgrounds | 64 | `bg_*` level floors in three groups, plus title and controls pictures |
| Sounds | 321 | 9 music tracks, about 70 effects, 7 announcers x (welcome, win, lose) x 10 recordings, plus 10 generic announcer lines of each kind |
| Fonts | 9 | impact, mono, score, score shadow, count, small, credits, font4, font5 |
| Data files | 4 | `config.ini` (every setting), `backup.ini` (defaults and eight default names), `names_f.ini` and `names_m.ini` (about 5,300 first names) |
| Extensions | 1 | Xin v1.1 (a Windows DLL helper; no behaviour to keep) |
| Timelines, paths, shaders | 0 | none |

### 1.1 How the original plays (the rules the rebuild must keep)

- **Round setup** (`randomize_level`, `setup_shapes`, `setup_players`, `setup_backgrounds`): a fresh level every round from
  the setup's polygon count (20), shape kind (abstract, triangles, bars, circles, squares), rotation (none, 45, 90, 360,
  random) and overlap (no or yes); every player at a random spot; a random background; weapons and items dealt from the
  settings; the turn clock and fuse reset; a random track starts; fog of war reset; then a Ready card.
- **A turn** (`obj_game`, `next_player`, `turn_countdown`): the seat whose turn it is turns the aim (A/D or arrows), drives
  the tank (W/S, which forfeits the shot), sets a grenade fuse (1/2/3), cycles weapon (Q/E) and item (Z/C), uses an item (F),
  fires (Space) or ends the turn; numpad keys snap the aim to the eight directions; Alt pings everyone; PageUp and PageDown
  resize the border and reroll; there is no power meter (bullet speed is a constant).
- **Weapons** (6): cannon, laser, shotgun, grenade, railgun, vortex. **Items** (6): extra bounce, shield wall, shrink ray,
  teleporter, aim assist, redirector. A count of 10 means unlimited. A laser sight item exists in the settings room only and is
  not usable in play (dormant).
- **Hits** (`hit`): damage per hit and bounce, score 100 + 25 a bounce + 5 a tenth of a second in flight; the last one standing
  wins the round; `check_endgame` ends the game at the goal (500 default).
- **Drops**: weapon, item and health crates and a random-weapon powerup fall on emitter timers and go to whoever's shot passes
  over them.
- **AI** (`check_ai`, `find_target_angle(_from_bounce)`, `predict_target_angle`): per-player skill, a thinking line, straight, bank
  and railgun shots, aim assist.
- **Announcer**: seven personalities, welcome, win and lose recordings, played with a portrait and name box.
- **Flow**: titlescreen (also the setup panel) -> mainmenu -> players (up to 8 seats, human or AI, per-seat skill, names) ->
  maproom -> gamescreen -> endgame, with settings, controls, howtoplay, credits off the main menu.

### 1.2 Dormant or dead in the original (recorded, not rebuilt)

`obj_sheep` and `create_random_sheep` (critters, commented out of `randomize_level`), `create_random_foliage`'s call (also
commented out; the rebuild keeps foliage as a hazard from UR-T6), the laser sight item, `obj_powerup_doublespeed`, `_shield`,
`_teleport`, `_extrabounce` (HUD stubs for a powerup system that never shipped), `object261`, `obl_lbl_announcer` (a typo duplicate), the debug keys (F12 debug on, F11 skip to endgame with random scores, F10 and F9 give
a weapon or an item, F8 trace every degree, numpad plus and minus game speed), `take_screenshot` (writes to `C:\`), the hotseat multiplayer rooms
(replaced by the kit, BIBLE section 3), `room0`, the empty `intro` and the campaign `maproom` no button reached.

## 2. What the rebuild has

| Original system | Rebuild | Where |
|---|---|---|
| Aim, bounce preview, shot flight (`aim`, `draw_potential`, `check_rail_intersection`, `lines_intersect`) | built, adds a power meter (deviation: the original had none) | `Core/Aim/` (`BounceSolver`, `ShotPath`, `AimState`, `ShotRules`) |
| Seats, health, turn order, clock, scoring, round and goal | built | `Core/Match/` (`Match`, `MatchRules`, `Seating`) |
| Six weapons, six items, counts, crates | built | `Core/Arsenal/` (`Loadout`, `Volley`, `Arsenal`, `Drops`) |
| Hazards: electricity, laser gate, foliage, shield wall, redirector, vortex, breaches | built | `Core/Level/` (`Hazards`, `Level`) |
| AI and aim assist | built (one skill for all AI, UR-T28) | `Core/Ai/` |
| Announcer (written lines, not recordings) | partial | `Core/Match/Announcer` |
| Room graph, how to play, controls, credits | built as MindAttic Components screens | `Core/Menu/MenuFlow`, `Runtime/Menus` |
| Four hand-laid maps | built (deviation: the original randomised the level, UR-T23) | `Core/Level/Maps` |
| Endgame recap | built as a list (the original had a podium) | `Runtime/EndgameRecap` |
| Commands and events: every input a command with a player id, one authority, ordered events (new, UR-T34) | built | `Core/Commands/` (`GameAuthority`, `PlayerState`, `Commands`, `GameEvents`) |
| Real multiplayer, votes, reconnect, profile | built (new, replaces the hotseat rooms) | `Core/Menu/` (`Lobby`, `VoteMenu`), `Core/Net/ShotWire`, kit |
| Sound | four synthesised cues, no recordings, no music | `Core/Menu/Sound`, `Runtime/GameAudio` |
| Art | none imported: shapes and IMGUI | `Runtime/*` |

## 3. Gaps against the original, and the ticket for each

Each row is its own story and ticket. Rows marked (open) were filed before this audit and are kept in the order below.

| Gap | Ticket |
|---|---|
| Unlimited count printed as `xinf` instead of the infinity sign | UR-T19 (open) |
| Vote overlay never shows on a joined client | UR-T20 (open) |
| Default players indistinguishable; no default or random names | UR-T21 (open) |
| Join by code needs Relay linked | UR-T22 (open, owner's Unity Cloud account) |
| Random levels use the original's 80 shapes (16 each of abstract, triangle, bar, circle, square outlines exported from the sprites' collision masks into `Core/Level/ShapeData`), picked as setup_shapes.gml does with the shape-type setting (five kinds plus random) and its none/45/90/360 rotation; the setup screen's controls for these settings come with UR-T41 | built (UR-T60) |
| Cannot drive the tank or end a turn early | UR-T24 (open) |
| Settings room's match rules missing | UR-T25 (open) |
| Fog of war missing | UR-T26 (open) |
| No music | UR-T27 (open) |
| One AI skill; no per-AI names | UR-T28 (open) |
| No mouse controls, numpad aim, ping, fullscreen | UR-T29 (open) |
| Setup not remembered (`config.ini`, defaults button) | UR-T41 |
| Ready card, turn-start ping, overlap-free placement before the clock | UR-T42 |
| Pause menu (Resume, Randomize, Music, Quit round) | UR-T43 |
| No art imported (315 sprites, 64 backgrounds, 9 fonts) | UR-T44 |
| Tank turret and hull art, tracks | UR-T45 |
| Backdrop: backgrounds, clouds, spotlights, lights, borders, shape art | UR-T46 |
| Weapon and hazard visuals | UR-T47 |
| Hit and knock-out effects, floating text, crate falls | UR-T48 |
| HUD icons, counts, tooltips, angle readout, turn arrow, guide | UR-T49 |
| Sound effects (only four synthesised cues) | UR-T50 |
| Announcer recordings and portraits | UR-T51 |
| Title and main menu presentation | UR-T52 |
| Room fades | UR-T53 |
| Controls and how-to-play art | UR-T54 |
| Credits roll | UR-T55 |
| Endgame podium | UR-T56 |
| Splash and startup credits | UR-T57 |
| Whole-game parity verification | UR-T58 |

## 4. Standards audit (Unity best practice and the shared standards)

| Standard | State on main | Ticket |
|---|---|---|
| Engine-free Core, `noEngineReferences`, tested outside Unity | met: `Ricochet.Core` asmdef, `dotnet test Core.slnx`, 18 test files | kept |
| Assembly names and one-way boundaries, an Editor assembly | met: `Ricochet.Runtime`, `Ricochet.Editor` (menus and validators), `Ricochet.Tests`, `Ricochet.EditorTests`; Core grouped by domain (Aim, Arsenal, Match, Level, Ai, Menu, Net) with `Ricochet.Core.*` sub-namespaces; `CoreBoundaryTests` fails if Core references anything beyond the BCL | kept |
| State changes only through commands that produce events | partial: player input is met, seat lifecycle, votes and HUD/audio events are not (see below); `Core/Commands/` (`GameAuthority` applies `AimCommand`, `SetPowerCommand`, `FireCommand`, `CycleWeaponCommand`, `CycleItemCommand`, `FuseCommand`, `UseItemCommand`, `DetonateCommand`, `DriveCommand`, `EndTurnCommand`, `RerollCommand`, `VoteCommand`, each carrying a player id, and returns ordered events); `Player`'s `[ToServer]` calls are those commands and `Arena` plays the events; gaps: `VoteCommand` is applied by the authority and tested but the kit's restart, match and kick votes still run on the kit's vote board, `Arena` still joins, drops, rejoins and removes match seats on `Match` directly, and `MatchHud`, `GameAudio` and `ShotView` do not yet subscribe to the events | follow-up |
| Content and tuning as validated data | gap: rules, maps, lines and slides are C# initialisers | UR-T35 |
| Input System | gap: Active Input Handling is the legacy manager, no actions asset, no rebinding | UR-T36 |
| URP behind a pipeline-agnostic layer | gap: no pipeline asset assigned in Graphics or Quality | UR-T37 |
| UI Toolkit or shared components, no IMGUI | gap: `MatchHud`, `Crate`, `Player` still use `OnGUI` | UR-T38 |
| Small focused components | gap: `Player.cs` 1174 lines, `Arena.cs` 975 | UR-T39 |
| Tests and CI | partial: Core and PlayMode tests exist, CI is the nightly smoke only | UR-T40 |
| Unity.Shared profiles and settings | partial: name, colour and audio read from the profile (UR-T10); the match setup is not stored | UR-T41 |
| Multiplayer-ready seams | met: Unity.Multiplayer kit, server authority, seats | kept |
| README and BIBLE | gap: README and BIBLE are slice-by-slice history | UR-T59 |
| Built in code | met for UI and level; scene and six prefabs are the kit's spawn templates | kept |

## 5. Build order

Build in this order and the repo reaches full parity and standards. Foundations come first so every later ticket lands on
the final architecture; rules next; then presentation, audio and screens; then the proof.

| # | Ticket | What |
|---|---|---|
| 1 | UR-T33 | Assembly names and boundaries |
| 2 | UR-T34 | Core commands and events |
| 3 | UR-T35 | Data-driven rules and content |
| 4 | UR-T36 | Input System |
| 5 | UR-T37 | URP |
| 6 | UR-T38 | Replace IMGUI |
| 7 | UR-T39 | Split Player and Arena |
| 8 | UR-T40 | Tests and CI |
| 9 | UR-T41 | Remember the setup |
| 10 | UR-T20 | Vote overlay on clients |
| 11 | UR-T19 | Infinity sign |
| 12 | UR-T21 | Distinct default colours and names |
| 13 | UR-T28 | Per-AI skill and names |
| 14 | UR-T25 | Settings room rules |
| 15 | UR-T23 | Randomised levels |
| 16 | UR-T42 | Ready card and turn-start ping |
| 17 | UR-T24 | Drive the tank, end the turn |
| 18 | UR-T26 | Fog of war |
| 19 | UR-T43 | Pause menu |
| 20 | UR-T29 | Mouse, numpad, ping, fullscreen |
| 21 | UR-T44 | Art import pipeline |
| 22 | UR-T45 | Tank visuals |
| 23 | UR-T46 | Level backdrop |
| 24 | UR-T47 | Weapon and hazard visuals |
| 25 | UR-T48 | Hit and knock-out effects |
| 26 | UR-T49 | HUD details |
| 27 | UR-T50 | Sound effects |
| 28 | UR-T27 | Music |
| 29 | UR-T51 | Announcer voices and portraits |
| 30 | UR-T52 | Title and main menu |
| 31 | UR-T53 | Room fades |
| 32 | UR-T54 | Controls and how-to-play art |
| 33 | UR-T55 | Credits roll |
| 34 | UR-T56 | Endgame podium |
| 35 | UR-T57 | Splash and startup credits |
| 36 | UR-T58 | Parity verification |
| 37 | UR-T59 | README and BIBLE final pass |

UR-T22 (Relay) depends on the owner's Unity Cloud account and sits outside the order. The queue priorities carry this order
(higher runs sooner).

## 6. Deviations that stay

The power meter (the original fixed the speed), a networked turn with server authority, real multiplayer in place of the
hotseat rooms, up to 16 seats (the original had 8 in its setup rooms and 16 bars), votes and reconnect, the kit's menus under
the original's screens, and pause as a host or vote action. Each is recorded in the BIBLE.
