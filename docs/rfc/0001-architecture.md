---
codex: 1
project: Unity.Ricochet
code: UR
layer: rfc
status: draft
updated: 2026-10-23
---

# RFC 0001 — Architecture and build order

## Source material

The original is a GameMaker 8 (`.gmx`) project at
`R:\Projects\_PRE_2022\_PRE_2021\_PRE_2020\_PRE_2017\gmlRicochet\09-05-13_2.20am` (last touched 2013-09-05):
`Ricochet.project.gmx` plus `objects/` (~90 `.object.gmx` files), `scripts/` (~150 `.gml` files) and `rooms/`
(17 `.room.gmx` files). It is read for design reference only (UR-LAW-2); nothing from it is copied into this
repo. The inventory below is from directory listings and filenames, not a full read of every script — treat
specifics as the starting hypothesis each ticket verifies against, not settled fact.

## What the original does, as best read from its file names

- **Aiming/physics** (`aim.gml`, `LineLineIntersection_X/Y.gml`, `check_rail_intersection.gml`,
  `check_rail_redirector_intersection.gml`, `draw_potential.gml`, `draw_bounceinfo.gml`, `find_target_angle(_from_bounce).gml`,
  `bullet_create/step/polygon_collision.gml`): an angle/power shot, a predicted bounce path drawn before firing,
  rail and redirector collision, and the actual projectile step/impact.
- **Players and turns** (`obj_Controls`, `obj_bar1`..`obj_bar16`, `obj_back_players`, `check_isready.gml`,
  `check_rotate_clockwise.gml`, `draw_turntime.gml`, `clear_scores.gml`): up to 16 seats with health bars, a
  turn order and a turn timer.
- **Weapons and items** (`cycle_weapon(_left/right).gml`, `cycle_item_left/right.gml`, `fire_weapon.gml`,
  `add_weapondrop/itemdrop/healthdrop/powerup.gml`, `add_random_weapon/item/health.gml`, `draw_weaponinfo.gml`,
  `draw_iteminfo.gml`, `obj_box_weapons`, `obj_box_items`): a cycling loadout, grenades
  (`draw_fuse.gml`/`draw_grenadefuse.gml`/`drawgui_fuse.gml`), and random drops onto the level.
- **Hazards** (`destroy_shieldwall.gml`, `detonate_vortex.gml`/`draw_vortexdetonate.gml`, `draw_electricity.gml`,
  `check_source_laser.gml`, `create_random_foliage/sheep.gml`, `obj_border_top/bottom/left/right`, `obj_boom`/
  `obj_boom_normal`): a destructible shield wall, a vortex, electricity and laser obstacles, destructible
  borders and incidental level dressing.
- **AI** (`check_ai.gml`, `obj_CheckAI`, `obj_ai_gui`, `obj_aimassist`, `obj_btn_ai_less/more`,
  `obj_btn_aimassist_less/more`): a configurable AI player count and an aim-assist level, feeding the same
  `find_target_angle(_from_bounce)` targeting math as the preview.
- **Announcer/HUD** (`obj_announcer`, `obj_announcer_portrait`, `obj_box_announcements/character/environment/
  names`, `draw_scores.gml`, `draw_text_outline(_color).gml`, `obj_Gui`, `obj_btn_announcer_*`): portrait +
  name-box commentary between turns, cycled/selected, and score/info overlays.
- **Flow** (`rooms/`): `splashscreen → intro → titlescreen → mainmenu → names/players (roster setup) →
  maproom (level select) → gamescreen (the match) → endgame`, with `settings`, `controls`, `howtoplay`,
  `credits` reachable from the menu, and `multiplayer_hostorjoin/host/join` as the GML hotseat flow this
  project replaces (UR-LAW-3 §3) rather than ports.

## Architecture {#architecture}

Three layers, server-authoritative per UR-LAW-1:

```
Assets/Ricochet/Core/        Engine-adjacent game/turn/physics state: TurnOrder, Shot (angle, power, bounce path),
                              BounceSolver (ports LineLineIntersection + rail/redirector checks), Loadout, Hazards.
                              Plain C#, no UnityEngine where practical, so the bounce solver and turn rules are
                              unit-testable (dotnet test) the way Unity.Shared's core is.
                                  ▲
Assets/Ricochet/Net/          NetEntity subclasses wrapping Core state for the wire: PlayerEntity ([Synced] health,
                              seat, loadout), ShotEntity ([ToServer] Aim/Fire, [ToClients] ShotResolved), ArenaEntity
                              (hazards, drops). Only this layer imports Unity.Multiplayer, per UM-LAW-2's own rule
                              applied one level up: Core never imports Unity.Netcode either.
                                  ▲
Assets/Ricochet/Presentation/  MonoBehaviours reading Core/Net state: sprites, bounce-path line renderer, HUD
                              (MindAttic Components widgets), announcer, menus/rooms as scenes, AI's visual
                              aim-assist overlay. No gameplay decisions live here.
```

Scenes mirror the original's room graph where it still applies (`Title`, `Roster`, `MapSelect`, `Game`,
`Endgame`, `Settings`, `Controls`, `HowToPlay`, `Credits`), driven by Unity.Multiplayer's `NetSceneFlow`; there
is no `MultiplayerHostOrJoin` scene; `NetFrontEnd`'s Host/Join screens cover that job.

## Build order

1. **UR-T2** (done): repo, docs, package manifest.
2. **UR-T3** (done): run the Unity.Multiplayer *New Game* wizard to get a working scaffold (`NetBootstrap`,
   `NetFrontEnd`, `NetVotes`, one networked player), then build the Core bounce solver and aim/fire loop on top
   of it, single-player-against-nothing first (no AI, no turns yet) so the physics is provably right
   (unit tests on BounceSolver) before anything else depends on it.
   As built: Core lives in `Assets/Ricochet/Core` (`Ricochet.Core`, tested by `dotnet test Core.slnx`); the
   template's crates became keyed practice dummies to shoot at; there is no power, as the original had a fixed
   bullet speed and no power meter (U.RICO-T88 took out the range-by-power the rebuild once added); a shot detonates on its fifth contact, as `bullet_step.gml` did.
   The server solves and flies every shot and lands the impact; clients replay the same path from the synced aim.
3. **UR-T4** (done): turn order and the 16-seat roster with health bars — the loop that calls into UR-T3's shots.
   As built: the name guess above was half wrong. `obj_bar1`..`obj_bar16` are level obstacles (children of
   `obj_polygon_parent`) and `obj_Controls` is the Controls screen; the roster and turns really live in
   `setup_players.gml` (players, `obj_healthbar` row), `next_player.gml`, `turn_countdown.gml`/`reset_timer.gml`/
   `draw_turntime.gml`, `hit.gml` and `check_endgame.gml`. Ported as Core `Match` (seats, turns in seat order skipping
   the dead, a 20 s clock that beeps its last four seconds and passes the turn, hit.gml's damage of 10 + 2 a bounce
   +/- 1 to an opponent or 10 a bounce to yourself out of 100 health, the last one standing wins and opens the next
   round) and `Seating` (sixteen fixed spots, the HUD's two rows of eight bars around the clock). A match starts by
   vote with N AI opponents (`Opponent` prefab, keyed `ai:n`); the server mirrors it through a keyed `Turns` object and
   each Player's `[Synced]` seat/health; a late joiner sits the round out. AI seats fired a stand-in shot
   (nearest reachable opponent) until UR-T7.
4. **UR-T5/UR-T6**: weapons/items and hazards — content on top of the turn loop, independent of each other.
   UR-T5 (done), as built: Core `Loadout` (counts, selection, fuse; cycle_weapon/item_left/right, the spend rule of
   fire_weapon/use_item with 10 = unlimited, packed into two ints for the wire), `Arsenal.Fire` solving each weapon's
   `Volley` (cannon; laser at full range and triple speed; a five-pellet shotgun fan, each pellet a share of a hit; a
   grenade that passes through players and blasts on its fuse, 0.5–6 s by keys 1/2/3, or its bounce limit; a railgun
   straight through walls hitting everyone on the line), the items (extra bounce, teleporter, shrink ray), and
   `DropField`/`Pickups` (obj_drop_emitter's 20–120 s timer per kind, crates falling to a random spot and lasting
   20–120 s, collected by a shot passing over, add_random_weapon/item/health). The server keeps each Player's loadout
   and mirrors it; the client cycles, fuses and uses items through `[ToServer]` calls checked like a shot; crates are
   keyed `Crate` objects. Departures: the cannon is unlimited (an empty-handed player could only time out); the vortex,
   shield wall and redirector belong with UR-T6's hazards, so they are out of play (nobody starts with them, no crate
   gives them) until those land; a crate's roll gives the kind it names (the original's
   cannon roll gave a laser, its aim-assist roll a teleporter); a grenade's blast hurts everyone in it, not just the
   first player it touches.
   UR-T6 (done), as built: Core `HazardField` (`Hazards.cs`) holds the level's hazards and `Level` gained barriers (a shot
   stops where it touches one: `ShotEndKind.Absorbed`) and breaches (a shot reaching the border there leaves the level:
   `Escaped`). The shield wall item (spawn_shieldwall.gml: a unit ahead, across the aim, 2.2 long; its owner's shots pass,
   anyone else's shatters it; it decays after 20–120 s), the redirector item (shoot_redirector.gml, set down where its
   flight stops), and the vortex device set off by firing again while it flies (vortex_detonate.gml: `Arsenal.Detonate`
   cuts the volley short; the server syncs the distance so every peer cuts its own flight there) came into play. The
   original's implosion dragged every polygon in reach about, the border pieces included; here it swallows the
   destructible hazards it reaches and breaches each border side it reaches along the chord it cuts, until the round
   ends. draw_electricity.gml and check_source_laser.gml were never called in the original; they became an electric
   arc between pylons that stops any shot and a laser gate that does while it is on, switching every turn (its beam
   sweeping out from the emitter as it comes on), and create_random_foliage.gml's tufts (commented out of
   randomize_level.gml) became a few each round that a shot cuts down and stops in. A shot stopped by a hazard strikes
   nothing; a grenade or vortex stopped by one is lost without going off (obj_shieldwall destroyed them). The server
   mirrors each hazard as a keyed `LevelHazard`; every peer builds the level a shot sees from them (`Arena.LevelFor`),
   so the preview, the AI and the server agree. The arcs, gates and foliage are laid only when the scene asks
   (`Arena.Serve(..., layHazards: true)`, as `Game` does), so the other tests' shots fly in the bare level.
5. **UR-T7** (done): AI — depends on UR-T3's bounce solver (reused for targeting) and UR-T4's turn loop.
   As built: Core `MatchSetup` (AI count, one AI skill 0–10 for every AI seat where the original had a pSkill per
   player, and the aim assists each player starts with, 0–10 with 0 taking the item out of play and 10 unlimited;
   the vote's number and text), `Ai` (one half-degree sweep of the preview's solver finds every heading that reaches
   an opponent: a straight shot beats a bank shot, which beats a railgun shot through the walls when the AI carries
   one, then the nearest opponent and the middle of the widest run of headings; skill nudges it by up to 10 − skill
   whole degrees; nothing in reach fires blind) and `AiTurn` (think, plan once, turn the barrel at 180°/s or faster
   when the clock is short, fire). Arena plays an AI seat's turn through the same server calls a human's goes
   through; `Turns` carries the "is thinking..." line every HUD pulses along the bottom (obj_ai_gui). Aim assist,
   whose original was only a fading straight beam along the aim (the preview already shows more), is the AI's
   targeting at full skill offered to a human: it turns their aim onto a shot that reaches an opponent  and every peer draws that path as a beam that fades over 5.5 s or goes with the
   shot. Departures: the railgun choice is new (the original AI only fired bullets); the original took the first
   degree that touched a target, often a graze.
6. **UR-T8** (done): announcer/HUD — presentation on top of whatever state exists by then.
   As built: the announcer was voice-only in the original: seven personalities (obl_lbl_announcer's names, two with drawn
   profiles), ten recordings a moment, played on a welcome (obj_game's create), a win (hit.gml, when a shot puts an
   opponent out) and a lose (obj_explosion, a shot that went off at its bounce limit without hitting anyone), as often as
   the "announcements" setting (0 off, 1 always, 2/3/4 one in 3/6/9; the welcome always) allowed, each recording once
   before repeats (random_win_number); obj_announcer slid the portrait in while the sound played. Core `Announcer` keeps
   those rules with written lines naming the players; `MatchSetup` carries the personality and setting in the
   start-match vote; Arena says the lines (`Turns` mirrors the last one and a count) and every peer's `HudOverlay` slides a
   MindAttic Components card in with the portrait (the personality's colour and monogram) and name box. Scores, which the
   original kept (pScore += point_combo on every opponent hit: points_kill 100, points_bounce 25, points_flighttime 5 per
   100 steps at 1020 fps), are `MatchRules.Points` and `Match`'s per-seat `Score` (a pellet scores its share; reset when a
   match starts, kept across rounds), mirrored on each `Player`. draw_weaponinfo/draw_iteminfo/draw_potential became the
   turn card: everyone sees the current seat's weapon, item and combo, as everyone at the one screen did. The health bars,
   clock and turn order are MatchHud's own uGUI, built in code (MaProgressBar has no per-player colour; UR-T38 replaced the IMGUI). Departures: no audio (the recordings are the
   original's), and the turn does not wait for a line to finish as check_endgame waited for the recording; the score
   goal that ended a game (goal = 500) belongs with UR-T9's endgame.
7. **UR-T9** (done): full menu/room flow connecting everything built so far.
   As built: no scenes, and no `NetSceneFlow`: the game is one scene, and every room but the game itself is a screen. Core
   `MenuFlow` holds the room graph (`Next`: title → main menu → players → map room → game → endgame → main menu; `Back`: the
   main menu from every room off it, the players room from the map room), the controls, the how-to-play slides (obj_slide's
   Last/Next, wrapping, with its "3/9" counter; rewritten for this game) and the credits; `Recap` ranks the endgame's
   leaderboard (score high to low, the lower seat on a tie, obj_endgame's ds_grid_sort) with its places and "X wins!".
   `TitleScreen` keeps the kit's menus hidden until Play; `Menus` fills the Players, map room, controls, how-to-play and
   credits screens added to the kit's main and in-game menus (`NetFrontEnd.AddScreen`), and the announcer under its Settings.
   The map room: the original's was a world map with one campaign mission, and each round's level came from
   randomize_level.gml's polygon/shape/rotation/overlap settings; here Core `Maps` is four hand-laid levels on the same 22 x 12
   border, with walls and redirectors kept clear of the sixteen seats, the dummies and the laid arcs and gates (a test checks
   it, and that every seat can reach someone). `Arena.Level` became the current map's level (`Arena.UseMap`), every peer
   following the index `Turns` carries; `LevelView` redraws on a change. The goal (globals.gml's 500; obj_btn_goal_less/more's
   steps of 500 to 10000, 1 meaning one round) rides in `MatchSetup` with the map; `Match.Goal` ends the match after the round
   in which it is reached (the round's result first), in a new `GameOver` phase with the top score as `Champion`; nobody fires
   until a new match or the restart vote's rematch. The endgame recap (`EndgameRecap`, on every peer's HUD) lists every seat
   with its place, name and score. Departures: the Start on the map room hosts a game and starts the match at once (the
   original's start went straight to the game screen on the one PC); in a game it is the match vote; the original's endgame
   pedestal, victory music and fades are not here; settings the original had that this game has no rule for yet (turn time,
   weapon counts, drop frequencies, fog of war) are not offered.
8. **UR-T10**: Unity.Shared profile wiring — can land any time after UR-T3's scaffold exists.
9. **UR-T11–UR-T14** (NET epic): host/join/lobby, then server-authoritative sync of the UR-T3/UR-T4 loop
   (the Core/Net split above is what makes this a wiring job, not a rewrite), then reconnect, then votes.
   UR-T12 (done), as built: turn order, aim and fire intent were already the server's (`Turns`, `[ToServer] Aim`/`Fire`,
   `MayFire`); what changed is that a client no longer solves a shot again from the synced aim in its own copy of the level
   (hazards, players and crates that may lag the server's by a frame). The server packs the volley it solved with Core
   `ShotWire` (every path's origin, aim, end, points and contacts, the strikes, the blast; doubles as doubles, so a client's
   points are the server's to the bit; anything else decodes to nothing) and sends it as `Player`'s `[ToClients]
   ShotResolved(shot, numbers, ids)`, again for the same shot with the cut-short volley when a vortex is set off. Once a
   match shot lands, `Arena` gathers Core `ShotOutcome` (the damage each struck seat took, summed per seat, and who went
   out) and sends it as `[ToClients] ShotResult`; every peer floats the damage over the struck players when its own flight
   lands. The `[Synced]` shot count stays, so a late joiner skips shots fired before it arrived; health stays `[Synced]`.

Single-player (UR-T3–UR-T10) is playable and testable before NET starts; NET's job is to put the same Core loop
on the wire, not to change it.
