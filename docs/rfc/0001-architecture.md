---
codex: 1
project: Unity.Ricochet
code: UR
layer: rfc
status: draft
updated: 2026-10-21
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
   template's crates became keyed practice dummies to shoot at; power is the shot's range (the original had a
   fixed bullet speed and no power meter); a shot detonates on its fifth contact, as `bullet_step.gml` did.
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
   each Player's `[Synced]` seat/health; a late joiner sits the round out. AI seats fire a stand-in shot
   (`SimpleAi`, nearest reachable opponent) until UR-T7.
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
   shield wall and redirector belong with UR-T6's hazards and aim assist with UR-T7, so they are out of play (nobody
   starts with them, no crate gives them) until those land; a crate's roll gives the kind it names (the original's
   cannon roll gave a laser, its aim-assist roll a teleporter); a grenade's blast hurts everyone in it, not just the
   first player it touches.
5. **UR-T7**: AI — depends on UR-T3's bounce solver (reused for targeting) and UR-T4's turn loop.
6. **UR-T8**: announcer/HUD — presentation on top of whatever state exists by then.
7. **UR-T9**: full menu/room flow connecting everything built so far.
8. **UR-T10**: Unity.Shared profile wiring — can land any time after UR-T3's scaffold exists.
9. **UR-T11–UR-T14** (NET epic): host/join/lobby, then server-authoritative sync of the UR-T3/UR-T4 loop
   (the Core/Net split above is what makes this a wiring job, not a rewrite), then reconnect, then votes.

Single-player (UR-T3–UR-T10) is playable and testable before NET starts; NET's job is to put the same Core loop
on the wire, not to change it.
