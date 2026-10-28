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
- **A turn** (`obj_game`, `next_player`, `turn_countdown`): the seat whose turn it is turns the aim (A/D or arrows; rotSpeed, a degree a step: 60 a second), drives
  the tank (W/S, moveSpeed, a pixel a step: about 0.69 units a second; it forfeits the shot), sets a grenade fuse (1/2/3), cycles weapon (Q/E) and item (Z/C), uses an item (F),
  fires (Space) or ends the turn; numpad keys snap the aim to the eight directions; Alt pings everyone; PageUp and PageDown
  resize the border and reroll; there is no power meter (bullet speed is a constant).
- **Weapons** (6): cannon, laser, shotgun, grenade, railgun, vortex. **Items** (6): extra bounce, shield wall, shrink ray,
  teleporter, aim assist, redirector. A count of 10 means unlimited. A laser sight item exists in the settings room only and is
  not usable in play (dormant).
- **Hits** (`hit`): damage per hit and bounce, score 100 + 25 a bounce + 5 a tenth of a second in flight; the last one standing
  wins the round; `check_endgame` ends the game at the goal (500 default).
- **Drops**: weapon, item and health crates and a random-weapon powerup fall on emitter timers and go to whoever's shot passes
  over them.
- **AI** (`check_ai`, `find_target_angle(_from_bounce)`, `predict_target_angle`): per-player skill (default 10), a thinking line, the
  first straight degree, else the soonest tracer, else a random degree; always a cannon shell; aim assist.
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
| Aim, bounce preview, shot flight (`aim`, `draw_potential`, `check_rail_intersection`, `lines_intersect`) | built; no power meter, as the original (U.RICO-T88 took out the one the rebuild had added): a shot flies until its bounces run out | `Core/Aim/` (`BounceSolver`, `ShotPath`, `AimState`, `ShotRules`) |
| Seats, health, turn order, clock, scoring, round and goal | built | `Core/Match/` (`Match`, `MatchRules`, `Seating`) |
| Six weapons, six items, counts, crates | built | `Core/Arsenal/` (`Loadout`, `Volley`, `Arsenal`, `Drops`) |
| Hazards: electricity, laser gate, foliage, shield wall, redirector, vortex, breaches | built | `Core/Level/` (`Hazards`, `Level`) |
| AI and aim assist | built (per-AI skill, UR-T28; the original's targeting, turning and cannon-only shot, U.RICO-T98) | `Core/Ai/` |
| Announcer (recordings, written lines as captions, the original's portraits) | built (U.RICO-T51) | `Core/Match/Announcer`, `Runtime/HudOverlay`, `Runtime/GameAudio` |
| Room graph, how to play, controls, credits | built as MindAttic Components screens | `Core/Menu/MenuFlow`, `Runtime/Menus` |
| Four hand-laid maps | built (deviation: the original randomised the level, UR-T23) | `Core/Level/Maps` |
| Endgame recap | built as the original's podium room (U.RICO-T56) | `Core/Menu/Podium`, `Runtime/EndgameRecap` |
| Commands and events: every input a command with a player id, one authority, ordered events (new, UR-T34) | built | `Core/Commands/` (`GameAuthority`, `PlayerState`, `Commands`, `GameEvents`) |
| Real multiplayer, votes, reconnect, profile | built (new, replaces the hotseat rooms) | `Core/Menu/` (`Lobby`, `VoteMenu`), `Core/Net/ShotWire`, kit |
| Sound | the original's 70 non-voice recordings, one `SoundCue` each (U.RICO-T50); the announcers' 210 voice lines (U.RICO-T51) | `Core/Menu/Sound`, `Runtime/GameAudio`, `Runtime/UiSounds` |
| Art | imported whole (U.RICO-T44): every sprite, background and font, catalogued with its original path; shots, crates, HUD text, tanks (U.RICO-T45), the level backdrop and shapes (U.RICO-T46), the weapons, items and hazards (U.RICO-T47), the hit and knock-out effects (U.RICO-T48), the HUD details (U.RICO-T49), the announcers' portraits (U.RICO-T51), the items in flight and the endgame flashes (U.RICO-T79) and the menus, the pad and the rooms' pictures (U.RICO-T78) drawn from it, the menu screens on bg_titlescreen with their controls at the rooms' places (U.RICO-T84) | `Art/`, `Fonts/`, `Resources/Ricochet/ArtCatalogue`, `Runtime/Art/` |

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
| No music | UR-T27 (built: eight streamed tracks, a random one each round and turn, on the Settings music volume and mute) |
| One AI skill; no per-AI names | UR-T28 (built: `MatchSetup` keeps a skill and a name index per AI (`SkillOf`/`NameIndexOf`; `AiNames` is the list, -1 is "AI n"), with Default and Random names buttons on the Players room's opponents screen; names ride to clients on `Turns`) |
| No mouse controls, numpad aim, ping, fullscreen | UR-T29 (done: ControlPad on-screen buttons, numpad 8-way aim snap, Alt pings every player, F1 fullscreen) |
| Setup not remembered (`config.ini`, defaults button) | UR-T41 (built: `MatchSetup.ToRecord`/`FromRecord`, `SetupMemory` keeps it in the profile's game block as `matchSetup`, saved as a setup screen closes or a match starts, loaded on start; Players and Map carry Reset to defaults. Per-AI skills and names (UR-T28) joined the record as `skill1..15`/`name1..15`, and an older save's single `skill` still sets every AI; the match rules (UR-T25: turn time, bounces, damage, each weapon and item box and count, the drop boxes, pause_redirector) joined it as `turn`, `bounces`, `damage`, `pause`, `wpn0..5`, `wpnct0..5`, `item0..5`, `itemct0..5`, `drop0..3`, and the start-match vote carries those that differ from the defaults) |
| Ready card, turn-start ping, overlap-free placement before the clock | UR-T42 (built: every turn opens in `MatchPhase.Ready` for `MatchRules.ReadySeconds`, obj_ready's 80-step hold plus its 50-step fade at 60 a second (`readyHoldSeconds`/`readyFadeSeconds` in match.json), with the clock full and still and nobody able to fire, drive or end the turn, the AI included; `ReadyCard` shows "Ready", the name in the player's colour and the name over the tank, with sfx_switch, then pings the tank (sfx_sonar_ping, a white and a coloured ring) as the clock starts. The round's level and spots are rolled clear of overlaps before the first turn, so check_isready's wait has nothing left to wait for. obj_checkready1-8 are the players room's unused "Ready" checkboxes (their click sets hasCannon) and are not carried. The two sounds are the original's recordings since UR-T50) |
| Pause menu (Resume, Randomize, Music, Quit round) | UR-T43 (built: the host pauses at once and anyone else by a vote, freezing every peer's clock, Ready card, shots and AI; Randomize is the host's before the round's first shot; Music toggles the profile's mute; Quit round is a vote back to practice. Pause is on P / Pause / right stick, Esc stays the kit's menu; see the BIBLE's Pause menu deviations) |
| No art imported (314 sprites, 63 backgrounds, 9 fonts and the sprite font) | UR-T44 (built: `Tools/import-gml-art.ps1` and `import-gml-fonts.ps1` mirror them with `gml-art.json` / `gml-fonts.json`; `GmlArtImporter`, `GmlArtBuilder` and `GmlFontBuilder` make the sprites, atlases, TextMeshPro SDF fonts and `ArtCatalogue`; shots, crates, the HUD, the Ready card and the world labels draw from it; the remaining swaps are U.RICO-T45, U.RICO-T46 and the follow-up) |
| Tank turret and hull art, tracks | UR-T45 (built: `TankView` draws spr_tank_bottom under spr_tank_top in the seat colour, the turret on the aim, the hull turning a degree a step after it while the tank drives (align_bottom_to_top), and spr_tracks prints every five steps of driving (spawn_tracks, the first ten steps in) holding at 0.7 for 400 steps then fading 0.003 a step (obj_tracks); Core's `TankMotion` holds the step rules. obj_head, spr_head and spr_torso are an unlisted leftover (a person-shaped "head" that followed `playerPreSwagger`) and are not carried; only the driving tank lays tracks, see the BIBLE's Tanks section) |
| Backdrop: backgrounds, clouds, spotlights, lights, borders, shape art | UR-T46 (built: `backdrop.json` holds setup_backgrounds' three floor groups, the eight clouds, the spotlight, obj_lights' ranges and the border sprites; Core's `Backdrop.Plan` rolls the round's floor and ten lights from the level's seed and version so every peer agrees; `BackdropView` tiles the floor, drifts the clouds at 0.15, pulses the spotlight 0.05 to 0.15, lights the ten through `IRenderStyle.AddGlow` and frames the arena with the border sprites over the fog; `LevelView` draws a random level's shapes as their sprites from `RoundLayout.Shapes`. obj_lights was never placed in the original (fixed: drawn, ten of them, over the room's height); obj_ground and obj_hex are not carried; see the BIBLE's Level backdrop) |
| Weapon and hazard visuals | UR-T47 (built: Core's `WeaponLooks` and `Vortex` hold the step rules; the preview and aim assist in draw_beam's dots (`DottedBeam`), the laser as laser_draw's beam with obj_laser_pulse's rings running out once spent, the railgun as railgun_draw's rail with generate_swirl's swirls fading at railFade, obj_grenade_explosion's circle, the vortex growing in flight and imploding through vortex_step's modes with obj_vortex_child, obj_boom_normal on whoever a shot strikes, obj_explosion only where a shell's bounces run out, the shield wall and redirector as their sprites, the arc's glow as soft lights, the grenade's fuse counting down and draw_grenadefuse's hint. obj_laser_connector, obj_rail_ring, drawgui_fuse, draw_fuse, obj_tesla are dead in the original and not carried; the items in flight and the endgame flashes (obj_flash, obj_flashemitter) are built in UR-T79 (the shield wall's glass is U.RICO-T48's); see the BIBLE's Weapon and hazard visuals) |
| Hit and knock-out effects, floating text, crate falls | UR-T48 (built: Core's `HitLooks` holds the rules; `FloatText` is obj_float_text ("+N pts" flashing over the shooter, the damage in red, the crates' and the extra bounce's texts), `FieldEffects` lays obj_scorchmark(_small) under every blast, plays obj_playerexplosion holding its last frame with obj_flame's smoke, obj_powerup_explosion, a collected crate's ring and spawn_glass's shards when a shield wall breaks (with sfx_shatter when struck), all swept away with the round; `GmlParticles` draws the rebuild's reading of GameMaker's ef_ring, ef_firework, ef_smokeup and ef_spark; the crates smoke as they fall, land with a firework and sfx_crash, and the powerup sparks; the server sends every peer what broke (`CrateGone`, `ShieldWallShattered`) and the points a shot scored. spr_glass5 is drawn (fixed defect); obj_hitscore is dead and not carried; see the BIBLE's Hit and knock-out effects) |
| Item flights and endgame flashes | UR-T79 (built: Core's `ItemLooks` holds the rules and `ItemUsed` carries the item's flight, sent to every client by `Player.ItemFlown` (`ShotWire.EncodeFlight`); `ItemFlights` draws obj_teleporter's spr_teleporter at twice bulletSpeed turned to the aim with teleport_player's rings and firework, obj_shrinkray at four times with an ef_spark in lime every step and a lime firework on whoever it strikes (its spr_shrinkball and obj_shrinkray_child are never drawn in the original: the Draw is commented out and the child never created), and obj_redirector flying at twice from 0.15 of its size to 0.30 turning 3 degrees a step, the standing redirector waiting for it to land; `EndgameFlashes` is obj_flashemitter's obj_flash every step over the endgame recap, none on the pedestal. The shield wall's glass is UR-T48's; see the BIBLE's Item flights and endgame flashes); U.RICO-T80 matched the items' play: each takes effect when its flight arrives (`GameAuthority.AdvanceItems`), the teleporter teleports at its first shape or border, the redirector flies until its owner presses again, items in the air are gone at the turn's end; U.RICO-T90: the shrink ray passes through tanks and shrinks away the first shape it strikes, gone from the level (`HazardKind.Shrunk`) |
| HUD icons, counts, tooltips, angle readout, turn arrow, guide | UR-T49 (built: Core's `HudLooks` holds the rules; `HudIcons` draws obj_icon_weapon_* and obj_item_* in their off, plain and selected sprites with fnt_count counts and spr_infinity for unlimited, selects on a click (`SelectWeaponCommand` / `SelectItemCommand`) and says what each does on hover (arsenal.json's `about`); `MatchHud`'s slots draw obj_healthbar's three sprites with "shown/max", draw_scores' human and computer marks and obj_turnarrow; `TurnGuide` draws obj_guide's rings and tick, draw_angle's readout with spr_symbol_degree and the extra-bounce indicator (never created in the original: fixed); the pad shows obj_degree_gui's angle; the turn marker over the tank is spr_turnarrow. obj_lbl_count_*, obj_displaybox, obj_weapon_icon, obj_infinity and mouseover_powerup_draw are not carried; see the BIBLE's HUD details) |
| Sound effects (only four synthesised cues) | UR-T50 (built: `Tools/import-gml-sound.ps1` mirrors the 70 non-voice sfx_* recordings into `Resources/Ricochet/Sound` with `Audio/gml-sound.json`; Core's `SoundCue` (one per recording) and `SoundBook` say which moment plays which and at what volume; `GameAudio` plays the shots' fire, bounces, loops and landings from the feed, the items, crates collected, countdown, timeouts, knock-outs and the round's applause from the server's flourish, and the views the crates' flyover and crash, the vortex's suck and blow, the redirector's grow, the HUD icons' flick and disabled and the owner's engine, turn, fuse, snap, cycle and disabled; `UiSounds` the menus' flick, click and select. sfx_bounce_shotgun3 names no file, so pellets bounced silently a third of the time (fixed); see the BIBLE's Sound effects) |
| Announcer recordings and portraits | UR-T51 (built: `Tools/import-gml-sound.ps1` mirrors the seven personalities' 210 voice lines into `Resources/Ricochet/Voice` (each definition's .ogg sibling; `voices` in `Audio/gml-sound.json`); the line Core's `Announcer` draws without repeats picks `sfx_<key>_<kind><n>` (`Announcer.Recording`), which `GameAudio.Announce` plays as `HudOverlay` slides the card in, stopping one still playing; the card holds while it is heard with obj_speaker's spr_speaker flickering beside the portrait, the written line staying as the caption (and alone, 4.5 s, when muted); portraits are the original's (`announcer.json`'s `portrait`: spr_profile_sly, _rye, _sid, and spr_profile_dummy for the other four). Five definitions name no recording (sfx_sid_lose3/4, sfx_sly_lose4/5, sfx_vlad_lose4) and are filled from the actors' takes in `sfx/announcers` (fixed); spr_profile_sid was drawn but never shown (fixed); sfx_announce_* is played by nothing and not imported; see the BIBLE's Announcer voices and portraits) |
| Title and main menu presentation | UR-T52 (built: Core's `TitleLetters` steps obj_letter_parent's eight letters at the rooms' 180 steps a second, a pixel right and up a step from where titlescreen and mainmenu placed them, turned back past the 1920 x 1080 room's edges and showing the mirror letter moving left; `TitleBackdrop` draws them over bg_titlescreen on the title and, through `GmlRoomLayout`, the main menu, whose floor scrolls right a pixel a step; the title stops the music and Esc quits as obj_titlescreen did, and `GameAudio.Jukebox` is obj_jukebox, track after unlooped track in the menus; the titlescreen room's setup panel is the Start screen (`Room.Setup`, `Menus.BuildSetup`: players, shapes, type, rotation, overlap, goal, turn time, bounces, Default and each row's hover message from menu.json's `tips`), opened by the title's Start and the main menu's first button, Next going on to the players room; its level settings ride in each random round's seed (`LevelSeeds`), so every peer lays the same round. Nothing in the original dropped the letters in, so they drift from the start; the fullscreen switch on Start is not carried (the window setting is the player's); see the BIBLE's Title and main menu) |
| Room fades | UR-T53 (built: Core's `RoomFade` is obj_fade and its ten per-room copies as one black curtain, out of black at 0.01 a step (menu.json's `fadeSeconds`, about 1.67 s) and, where a room left that way, to black before the change; `Runtime/ScreenFade` lays it over everything and opens it on every menu screen change, every front-end move between the menus, lobby and game, and the endgame recap, and covers the map room's way into the lobby; the screen-change sfx_click comes from `UiSounds` for a pointer and from the curtain otherwise. The splash and startup credits' fades come with them in UR-T57, through the same curtain; see the BIBLE's Room fades) |
| Controls and how-to-play art | UR-T54 |
| Credits roll | UR-T55 (built: the original's credits room only showed spr_creditsimage, every name painted into it, with obj_creditsimage centring it and obj_credits sizing the view; nothing moved and fnt_credits was never set. Core's `CreditsRoll` and menu.json's `credits` (sections, each a heading and its lines, in this project's own words, crediting the original's people and the house kit) and `creditsScrollSpeed` (60 room px a second, a pixel a step); `Runtime/Art/CreditsRollView` rolls them up over the dimmed picture inside its own rect, headings in fnt_mono, lines in fnt_credits, and starts over each pass; see the BIBLE's Credits roll) |
| Endgame podium | UR-T56 (built: Core's `Podium` holds obj_endgame's room: obj_pedastal at (960, 864), the first three as their tanks in their colours at (960, 786), (871, 831) and (1049, 880), turrets at 270 and the champion's spinning a degree a step from 90, "Wins!" and the name, the leaderboard at y 100 and rows 30 apart, obj_btn_back_endgame at (128, 1024), music_victory once through; `Runtime/EndgameRecap` draws it on bg_stadium with the existing flashes and fade, `GameAudio` plays the track, and sfx_woot (played nowhere in the original, fixed) cheers with sfx_applause; the original has no confetti; see the BIBLE's Endgame podium) |
| Splash and startup credits | UR-T57 |
| Whole-game parity verification | UR-T58 |

## 4. Standards audit (Unity best practice and the shared standards)

| Standard | State on main | Ticket |
|---|---|---|
| Engine-free Core, `noEngineReferences`, tested outside Unity | met: `Ricochet.Core` asmdef, `dotnet test Core.slnx`, 18 test files | kept |
| Assembly names and one-way boundaries, an Editor assembly | met: `Ricochet.Runtime`, `Ricochet.Editor` (menus and validators), `Ricochet.Tests`, `Ricochet.EditorTests`; Core grouped by domain (Aim, Arsenal, Match, Level, Ai, Menu, Net) with `Ricochet.Core.*` sub-namespaces; `CoreBoundaryTests` fails if Core references anything beyond the BCL | kept |
| State changes only through commands that produce events | met: `Core/Commands/` (`GameAuthority` applies `AimCommand`, `FireCommand`, `CycleWeaponCommand`, `CycleItemCommand`, `FuseCommand`, `UseItemCommand`, `DetonateCommand`, `DriveCommand`, `EndTurnCommand`, `RerollCommand`, `VoteCommand`, and the seat lifecycle `JoinSeatCommand`, `DropSeatCommand`, `RejoinSeatCommand`, `LeaveSeatCommand`, each carrying a player id, and returns ordered events); `Player`'s `[ToServer]` calls are those commands and `Arena` plays the events and joins, drops, rejoins and removes seats only by command; the kit counts the restart, match and kick ballots (its overlay is the kit's, UM-T78) and `Arena` replays them as `VoteCommand`s so Core's `VotePassed` carries the effect out; `ShotView` and `GameAudio` subscribe to the `Presentation` feed of those events, and `MatchHud` draws only the synced `Turns` and `Player` copies, so presentation never reads or changes Core state | UR-T34, UR-T65 |
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

A networked turn with server authority, real multiplayer in place of the
hotseat rooms, up to 16 seats (the original had 8 in its setup rooms and 16 bars), votes and reconnect, the kit's menus under
the original's screens, and pause as a host or vote action. Each is recorded in the BIBLE.
