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
| Sprites | 314 | Tank, shots, effects, icons (weapon and item in normal, off and selected states), buttons, borders, shapes (16 each of five kinds), portraits, UI. 275 are in `Ricochet.project.gmx`; 39 are on disk only (left_border, spr_spritefont, spr_player, spr_simple1-5, ...). Earlier drafts said 315 |
| Backgrounds | 63 | `bg_*` level floors in three groups, plus title and controls pictures. 50 are in the project; `bg_redslate` names an image the project never had. Earlier drafts said 64 |
| Sounds | 321 | 9 music tracks, about 70 effects, 7 announcers x (welcome, win, lose) x 10 recordings, plus 10 generic announcer lines of each kind |
| Fonts | 9 | impact, mono, score, score shadow, count, small, credits, font4, font5 (impact, mono, score, count, small in the project), plus `spr_spritefont`, a sheet of coloured ASCII 33-126 glyphs no code loads |
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
| Art | imported whole (U.RICO-T44): every sprite, background and font, catalogued with its original path; shots, crates, HUD text, tanks (U.RICO-T45), the level backdrop and shapes (U.RICO-T46), the weapons, items and hazards (U.RICO-T47) and the hit and knock-out effects (U.RICO-T48) drawn from it, the rest still code-drawn | `Art/`, `Fonts/`, `Resources/Ricochet/ArtCatalogue`, `Runtime/Art/` |

## 3. Gaps against the original, and the ticket for each

Each row is its own story and ticket. Rows marked (open) were filed before this audit and are kept in the order below.

| Gap | Ticket |
|---|---|
| Unlimited count printed as `xinf` instead of the infinity sign | UR-T19 (open) |
| Vote overlay never shows on a joined client | UR-T20 (open) |
| Default players indistinguishable; no default or random names | UR-T21 (open) |
| Join by code needs Relay linked | UR-T22 (open, owner's Unity Cloud account) |
| Random levels use the original's 80 shapes (16 each of abstract, triangle, bar, circle, square outlines exported from the sprites' collision masks into `StreamingAssets/Settings/shapes.json`), picked as setup_shapes.gml does with the shape-type setting (five kinds plus random) and its none/45/90/360 rotation; the setup screen's controls for these settings come with UR-T41 | built (UR-T60) |
| Cannot drive the tank or end a turn early | UR-T24 (open) |
| Settings room's match rules missing | UR-T25 (built: `MatchSetup` holds the turn time, bounces, damage, each weapon and item box and count, the drop boxes and pause_redirector; `ApplyTo` gives them to MatchRules/ShotRules/ArsenalRules/DropRules/HazardRules at match start) |
| Fog of war missing | UR-T26 (built) |
| No music | UR-T27 (open) |
| One AI skill; no per-AI names | UR-T28 (built: `MatchSetup` keeps a skill and a name index per AI (`SkillOf`/`NameIndexOf`; `AiNames` is the list, -1 is "AI n"), with Default and Random names buttons on the Players room's opponents screen; names ride to clients on `Turns`) |
| No mouse controls, numpad aim, ping, fullscreen | UR-T29 (done: ControlPad on-screen buttons, numpad 8-way aim snap, Alt pings every player, F1 fullscreen) |
| Setup not remembered (`config.ini`, defaults button) | UR-T41 (built: `MatchSetup.ToRecord`/`FromRecord`, `SetupMemory` keeps it in the profile's game block as `matchSetup`, saved as a setup screen closes or a match starts, loaded on start; Players and Map carry Reset to defaults. Per-AI skills and names (UR-T28) joined the record as `skill1..15`/`name1..15`, and an older save's single `skill` still sets every AI; the match rules (UR-T25: turn time, bounces, damage, each weapon and item box and count, the drop boxes, pause_redirector) joined it as `turn`, `bounces`, `damage`, `pause`, `wpn0..5`, `wpnct0..5`, `item0..5`, `itemct0..5`, `drop0..3`, and the start-match vote carries those that differ from the defaults) |
| Ready card, turn-start ping, overlap-free placement before the clock | UR-T42 (built: every turn opens in `MatchPhase.Ready` for `MatchRules.ReadySeconds`, obj_ready's 80-step hold plus its 50-step fade at 60 a second (`readyHoldSeconds`/`readyFadeSeconds` in match.json), with the clock full and still and nobody able to fire, drive or end the turn, the AI included; `ReadyCard` shows "Ready", the name in the player's colour and the name over the tank, with the switch sound, then pings the tank (sonar sound, a white and a coloured ring) as the clock starts. The round's level and spots are rolled clear of overlaps before the first turn, so check_isready's wait has nothing left to wait for. obj_checkready1-8 are the players room's unused "Ready" checkboxes (their click sets hasCannon) and are not carried. The two sounds are synthesised until UR-T50) |
| Pause menu (Resume, Randomize, Music, Quit round) | UR-T43 (built: the host pauses at once and anyone else by a vote, freezing every peer's clock, Ready card, shots and AI; Randomize is the host's before the round's first shot; Music toggles the profile's mute; Quit round is a vote back to practice. Pause is on P / Pause / right stick, Esc stays the kit's menu; see the BIBLE's Pause menu deviations) |
| No art imported (314 sprites, 63 backgrounds, 9 fonts and the sprite font) | UR-T44 (built: `Tools/import-gml-art.ps1` and `import-gml-fonts.ps1` mirror them with `gml-art.json` / `gml-fonts.json`; `GmlArtImporter`, `GmlArtBuilder` and `GmlFontBuilder` make the sprites, atlases, TextMeshPro SDF fonts and `ArtCatalogue`; shots, crates, the HUD, the Ready card and the world labels draw from it; the remaining swaps are U.RICO-T45, U.RICO-T46 and the follow-up) |
| Tank turret and hull art, tracks | UR-T45 (built: `TankView` draws spr_tank_bottom under spr_tank_top in the seat colour, the turret on the aim, the hull turning a degree a step after it while the tank drives (align_bottom_to_top), and spr_tracks prints every five steps of driving (spawn_tracks, the first ten steps in) holding at 0.7 for 400 steps then fading 0.003 a step (obj_tracks); Core's `TankMotion` holds the step rules. obj_head, spr_head and spr_torso are an unlisted leftover (a person-shaped "head" that followed `playerPreSwagger`) and are not carried; only the driving tank lays tracks, see the BIBLE's Tanks section) |
| Backdrop: backgrounds, clouds, spotlights, lights, borders, shape art | UR-T46 (built: `backdrop.json` holds setup_backgrounds' three floor groups, the eight clouds, the spotlight, obj_lights' ranges and the border sprites; Core's `Backdrop.Plan` rolls the round's floor and ten lights from the level's seed and version so every peer agrees; `BackdropView` tiles the floor, drifts the clouds at 0.15, pulses the spotlight 0.05 to 0.15, lights the ten through `IRenderStyle.AddGlow` and frames the arena with the border sprites over the fog; `LevelView` draws a random level's shapes as their sprites from `RoundLayout.Shapes`. obj_lights was never placed in the original (fixed: drawn, ten of them, over the room's height); obj_ground and obj_hex are not carried; see the BIBLE's Level backdrop) |
| Weapon and hazard visuals | UR-T47 (built: Core's `WeaponLooks` and `Vortex` hold the step rules; the preview and aim assist in draw_beam's dots (`DottedBeam`), the laser as laser_draw's beam with obj_laser_pulse's rings running out once spent, the railgun as railgun_draw's rail with generate_swirl's swirls fading at railFade, obj_grenade_explosion's circle, the vortex growing in flight and imploding through vortex_step's modes with obj_vortex_child, obj_boom_normal on whoever a shot strikes, obj_explosion only where a shell's bounces run out, the shield wall and redirector as their sprites, the arc's glow as soft lights, the grenade's fuse counting down and draw_grenadefuse's hint. obj_laser_connector, obj_rail_ring, drawgui_fuse, draw_fuse, obj_tesla are dead in the original and not carried; the items in flight and the endgame flashes (obj_flash, obj_flashemitter) are U.RICO-T79 (the shield wall's glass is U.RICO-T48's); see the BIBLE's Weapon and hazard visuals) |
| Hit and knock-out effects, floating text, crate falls | UR-T48 (built: Core's `HitLooks` holds the rules; `FloatText` is obj_float_text ("+N pts" flashing over the shooter, the damage in red, the crates' and the extra bounce's texts), `FieldEffects` lays obj_scorchmark(_small) under every blast, plays obj_playerexplosion holding its last frame with obj_flame's smoke, obj_powerup_explosion, a collected crate's ring and spawn_glass's shards when a shield wall breaks (with sfx_shatter when struck), all swept away with the round; `GmlParticles` draws the rebuild's reading of GameMaker's ef_ring, ef_firework, ef_smokeup and ef_spark; the crates smoke as they fall, land with a firework and sfx_crash, and the powerup sparks; the server sends every peer what broke (`CrateGone`, `ShieldWallShattered`) and the points a shot scored. spr_glass5 is drawn (fixed defect); obj_hitscore is dead and not carried; see the BIBLE's Hit and knock-out effects) |
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
| State changes only through commands that produce events | met: `Core/Commands/` (`GameAuthority` applies `AimCommand`, `SetPowerCommand`, `FireCommand`, `CycleWeaponCommand`, `CycleItemCommand`, `FuseCommand`, `UseItemCommand`, `DetonateCommand`, `DriveCommand`, `EndTurnCommand`, `RerollCommand`, `VoteCommand`, and the seat lifecycle `JoinSeatCommand`, `DropSeatCommand`, `RejoinSeatCommand`, `LeaveSeatCommand`, each carrying a player id, and returns ordered events); `Player`'s `[ToServer]` calls are those commands and `Arena` plays the events and joins, drops, rejoins and removes seats only by command; the kit counts the restart, match and kick ballots (its overlay is the kit's, UM-T78) and `Arena` replays them as `VoteCommand`s so Core's `VotePassed` carries the effect out; `ShotView` and `GameAudio` subscribe to the `Presentation` feed of those events, and `MatchHud` draws only the synced `Turns` and `Player` copies, so presentation never reads or changes Core state | UR-T34, UR-T65 |
| Content and tuning as validated data | met: `Assets/StreamingAssets/Settings/*.json` (match, shot, hazards, arsenal, level, shapes, maps, announcer, menu, registry) loaded by `Core/Data/GameData` through an engine-free JSON parser and `ISettingsSource`, every problem reported with file, key and line; `Tests/Core.Tests/GameDataTests.cs` loads every shipped file | UR-T35 |
| Input System | gap: Active Input Handling is the legacy manager, no actions asset, no rebinding | UR-T36 |
| URP behind a pipeline-agnostic layer | gap: no pipeline asset assigned in Graphics or Quality | UR-T37 |
| UI Toolkit or shared components, no IMGUI | done: `MatchHud` is a uGUI canvas of Components labels, `Player`/`Crate` labels are world TextMeshPro (`PlayerLabels`, `WorldText`); no `OnGUI` remains | UR-T38 |
| Small focused components | gap: `Player.cs` 1174 lines, `Arena.cs` 975 | UR-T39 |
| Tests and CI | partial: Core and PlayMode tests exist, CI is the nightly smoke only | UR-T40 |
| Unity.Shared profiles and settings | met: name, colour and audio read from the profile (UR-T10), rebinds (UR-T36) and the match setup (UR-T41) kept in its game block | UR-T41 |
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
