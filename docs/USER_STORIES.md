---
codex: 1
project: Unity.Ricochet
code: UR
layer: stories
status: living
updated: 2026-10-21
---
<!-- GENERATED from MindAttic.Tickets/data/Tickets.db by `tickets db render`. Do not edit: this file is rewritten from the database; change stories and tickets with the tickets CLI. -->

# Unity.Ricochet — User Stories
> ✅ done (shipped & tested) · 🟡 partial · ⬜ planned. Every ✅ cites the test.

## Priority backlog
_Nothing recorded yet._


## Epic CORE — Single-player core rebuild
- **UR-US-CORE1 ⬜** As a player, I aim with an angle and power meter and see my shot's likely ricochet path off borders and redirectors before I fire, the way the 2013 original's aim/bounce-preview did.
- **UR-US-CORE2 ⬜** As a host, I set up to 16 players (any mix of human and AI) with names, colours and per-player health, and play proceeds through a visible turn order with a turn timer.
- **UR-US-CORE3 ⬜** As a player, I cycle through a weapon and item loadout, throw fused grenades, and pick up random weapon/item/health drops spawned on the level.
- **UR-US-CORE4 ⬜** As a player, I contend with level hazards carried over from the original: a destructible shield wall, a vortex detonation, electricity and laser obstacles, and destructible borders/foliage.
- **UR-US-CORE5 ⬜** As a player, I can fill empty seats with AI opponents whose count and aim-assist level I control before the match starts.
- **UR-US-CORE6 ⬜** As a player, I see announcer commentary (portrait + name box) between turns and a HUD showing scores, turn time, and the current weapon/item.
- **UR-US-CORE7 ⬜** As a player, I move through title, player setup, map/room select, settings, controls, how-to-play and credits screens, and see an endgame recap, matching the original's room flow.
- **UR-US-CORE8 ⬜** As a player, my display name, colour and audio settings come from my MindAttic profile (Unity.Shared) instead of being re-entered every session.


## Epic NET — Real multiplayer
- **UR-US-NET1 ⬜** As a player, I host a match (listen server or dedicated) or join one by IP or a short join code, through the Unity.Multiplayer drop-in front end and lobby.
- **UR-US-NET2 ⬜** As a player, turn order, aiming inputs, shot resolution and damage are server-authoritative and replicated to every client, replacing the original's single-PC hotseat turn-passing.
- **UR-US-NET3 ⬜** As a player who drops mid-match, my seat and health are held for the reconnect grace period and I resume on the same turn if I reconnect in time.
- **UR-US-NET4 ⬜** As a player, I can start a restart or kick vote through NetVotes with the same overlay the house front end provides.

## Tickets
> Permanent log of every change and fix — this repo's ticket system. Rows are append-only: never
> delete or reword one; only move its Status (open → closed / wont-do) and fill in Ref. Status is
> a word, not a glyph, so tickets don't count toward the story index. Keep this section last.

| ID | Date | Type | Status | Summary | Ref |
|---|---|---|---|---|---|
| UR-T1 | 2026-10-21 | docs | closed | Start the permanent ticket log in the ticket database | — |
| UR-T2 | 2026-10-21 | chore | closed | Create Unity.Ricochet repo (mindattic/Unity.Ricochet, tagged wip); scaffold README, AGENTS/CLAUDE pointers, .gitignore, Packages/manifest.json referencing the house kit (Unity.Multiplayer, MindAttic.Components, Unity.Shared), and docs/BIBLE.md + docs/rfc/0001-architecture.md capturing the GML Ricochet port plan | repo scaffold commit |
| UR-T3 | 2026-10-21 | feature | open | Run the Unity.Multiplayer New Game wizard to scaffold the project (NetBootstrap/NetFrontEnd/NetVotes scene, package manifest), then build core aim-and-fire: angle/power input, bounce-path preview off borders and rail redirectors (porting the line-line intersection and bounce-prediction math from aim.gml/draw_potential.gml/draw_bounceinfo.gml/check_rail_intersection.gml), and projectile flight/impact | UR-US-CORE1 |
| UR-T4 | 2026-10-21 | feature | open | Player roster and turn system: up to 16 seats (human/AI mix), per-player health bars, visible turn order and a turn timer, porting the obj_bar1..16 / obj_controls / draw_turntime design | UR-US-CORE2 |
| UR-T5 | 2026-10-21 | feature | open | Weapons and items: cycling weapon/item loadout, fused grenades, and random weapon/item/health/powerup drops, porting cycle_weapon(_left/right)/cycle_item_left/right and add_*drop/add_random_* scripts | UR-US-CORE3 |
| UR-T6 | 2026-10-21 | feature | open | Level hazards: destructible shield wall, vortex detonation, electricity and laser obstacles, destructible borders/foliage, porting destroy_shieldwall/detonate_vortex/draw_electricity/check_source_laser/create_random_foliage | UR-US-CORE4 |
| UR-T7 | 2026-10-21 | feature | open | AI opponents: configurable AI count and aim-assist level, targeting and fire decisions, porting check_ai.gml/obj_CheckAI/obj_ai_gui/find_target_angle(_from_bounce) and the aimassist mechanic | UR-US-CORE5 |
| UR-T8 | 2026-10-21 | feature | open | Announcer and HUD: portrait + name-box commentary between turns, score/turn/weapon/item info overlays, porting obj_announcer(_portrait)/obj_box_*/draw_scores/draw_weaponinfo/draw_iteminfo | UR-US-CORE6 |
| UR-T9 | 2026-10-21 | feature | open | Menu flow and rooms: title, player setup (names/colours/AI count), map/room select, settings, controls, how-to-play, credits, endgame recap, porting the room graph (titlescreen -> mainmenu -> names/players -> maproom -> gamescreen -> endgame) | UR-US-CORE7 |
| UR-T10 | 2026-10-21 | feature | open | Wire player display name, colour and audio settings to the active Unity.Shared profile instead of re-entering them each session | UR-US-CORE8 |
| UR-T11 | 2026-10-21 | feature | open | Multiplayer host/join/lobby: Unity.Multiplayer NetFrontEnd for Host, Join by IP, Join by code, and a lobby showing the join code and roster, replacing the GML hotseat multiplayer_host/join/hostorjoin rooms | UR-US-NET1 |
| UR-T12 | 2026-10-21 | feature | open | Server-authoritative turn and shot sync: turn order, aim/fire intent as [ToServer] RPCs, resolved shot path and damage as [Synced] state replicated via [ToClients], so every client sees the same bounce and the same result | UR-US-NET2 |
| UR-T13 | 2026-10-21 | feature | open | Reconnect handling for a turn-based match: a dropped player's seat and health are held for the reconnect grace period and resume on the same turn if they reconnect in time | UR-US-NET3 |
| UR-T14 | 2026-10-21 | feature | open | Restart/kick votes through NetVotes with the house front end's vote overlay | UR-US-NET4 |
