# Ricochet: the game's Unity folder

Scaffolded by **MindAttic ▸ Multiplayer ▸ New Game…** (Unity.Multiplayer's game template, UR-T3), then built into the
first playable slice of Ricochet: aim, preview the bounce path, fire, and watch the shot bank off the border, the
mirror walls and the rail redirectors onto a practice dummy; then matches (UR-T4): up to 16 seats, humans and AI,
taking turns against a 20-second clock until one is left standing; weapons, items and crates (UR-T5); and AI
opponents that think, aim (straight, bank or railgun shots, as well as their skill allows) and fire, with the
aim-assist item turning a human's aim onto the shot they would take (UR-T7).

Open `Game.unity`, press **Play** and click **Host**. A second copy (Multiplayer Play Mode, or a build) clicks **Join by
IP** (`127.0.0.1`) or **Join by code**. **Left/Right** (or A/D) turn your aim, **Up/Down** (or W/S) set the power,
**Space** fires (and, while your vortex device is in the air, sets it off); **Q/E** cycle the weapon, **Z/C** the item,
**F** uses the item and **1/2/3** set a grenade's fuse. The faint line is the whole path the shot will fly; the bottom-left line reads the angle, the power
and `Bounces: n/5`. **Esc** opens the in-game menu: **Invite players**, **Settings** (name and colour), **AI
opponents** (0-15), **AI skill** (0-10), **Aim assists each** (0 none, 10 unlimited) and **Vote to start a match**, and **Vote to restart** (the match from round one, or every dummy
back up in practice).

In a match the dummies go, everyone takes a seat (the humans in join order, then the AI) and plays in seat order: only
the seat whose turn it is may fire, the yellow clock at the top gives them 20 seconds, and the turn passes when their
shot lands or the clock runs out. A hit costs 10 health plus 2 a bounce (+/-1), or 10 a bounce if your own shot comes
back to you; at 0 you are out. The bars along the top (two rows of eight around the clock) are every seat's health,
the outlined one has the turn, and the line under the clock is the order the players still standing play in. The
last one standing wins the round and opens the next. Someone who joins mid-round plays from the next one.

Level hazards (UR-T6): the level is laid, in practice and at the start of every round, with a blue electric arc (top left)
and a red laser gate (top right) that stop any shot touching them, the gate only on every other turn, and a few tufts of
grass a shot cuts down and stops in. The shield wall item stands a wall a unit in front of you, across your aim: your own
shots pass through it, anyone else's shatters it (it decays by itself after 20-120 seconds). The redirector item flies like
a shell and becomes a redirector where it stops, sending shots on the way it flew. The vortex device flies like a shell;
press Space again while it is in the air to set it off there: it hurts everyone in its pull, swallows the shield walls,
redirectors and grass it reaches, and tears a breach in any border side it reaches, which shots fly out through for the
rest of the round.

## Layers (docs/rfc/0001-architecture.md)

| Folder | What lives there |
|---|---|
| `Core/` (`Ricochet.Core`, no engine references) | The rules as plain C#: `Geometry` (the original's `LineLineIntersection_X/_Y` and `lines_intersect`, the reflection rule from `bullet_step`), `Level` (border, mirror walls, redirectors), `BounceSolver` (the shot's whole path, and `FindHeading`, the original's `find_target_angle`), `ShotPath`, `ProjectileFlight`, `AimState`, `ShotRules`; and the match: `Match` (seats, turn order, turn clock, health, rounds; the original's `next_player`, `turn_countdown`, `hit` and `check_endgame`), `MatchRules`, `Seating` (sixteen spots, the HUD's bar slots, the `SS.FF` clock), `MatchSetup` (AI count, AI skill, aim assists: the start-match vote's payload); the AI (UR-T7): `Ai` (the targeting sweep, the original's `check_ai` and `find_target_angle(_from_bounce)`: straight, bank, then railgun shots, nudged by skill; and aim assist) and `AiTurn` (think, plan, turn the barrel, fire); and the arsenal (UR-T5): `ArsenalRules` and `Loadout` (counts, selection, fuse; the original's `cycle_weapon/item_left/right`), `Arsenal`/`Volley` (what each weapon and item does), `Drops` (`DropField`, `Pickups`: the random crates); and the hazards (UR-T6): `Hazards` (`HazardField`, `HazardRules`: the shield wall, electric arc, laser gate, foliage, breach and placed redirector, the level a shot sees with them on it, a vortex's implosion), with `Level`'s barriers and breaches and `BounceSolver` stopping at the one and escaping through the other. Tested without Unity: `dotnet test Core.slnx`. |
| `Runtime/` (`Ricochet`) | The networked game and its drawing. `Player` (a `NetEntity`: `[ToServer] Aim` and `Fire`, `[Synced]` aim, last shot, seat, health and whether still in the round; the server solves and flies the shot, refuses one out of turn, and lands it, every peer replays the same flight to draw it; an AI seat is the same entity from the `Opponent` prefab), `Turns` (the keyed match state every peer reads: phase, whose turn, the clock, the round), `Dummy` (keyed practice targets), `Crate` (a keyed falling crate), `LevelHazard` (a keyed hazard every peer rebuilds the shot's level from, drawing its arc, beam, wall, tuft, breach or redirector), `Arena` (the server's rules: the level, the match and its AI turns, the start-match and restart votes), `Game` (scene glue), `GameBot` (the bot), and presentation: `MatchHud`, `LevelView`, `ShotView`, `Lines`, `Shapes`, `GameInput`. |
| `Tests/` | PlayMode tests on the kit's `NetSessionTest`/`NetBotTest` (aim round trip and clamping, a shot landing on a dummy with the client flying the server's path, one shot in the air at a time, dummies by key, the restart vote, two bots each banking a shot onto their dummy; and in `TurnTests` a match seating two players and an AI, a shot out of turn refused, the turn passing on a landed shot and on the clock, a hit's damage seen by everyone, the AI taking its turn; and in `WeaponTests` cycling the weapon and item and setting the fuse, an extra bounce and a teleport, a grenade spent and its blast seen by everyone, a crate collected by a shot; and in `AiOpponentTests` a fully skilled AI's "is thinking..." line on the client and its hit on an opponent, a client's aim assist turning her aim onto the host and her shot landing, and a match with no aim assist; and in `HazardTests` a shield wall stopping another player's shot and shattering, a placed redirector, a vortex set off in mid-flight swallowing a wall and breaching the border a later shot escapes through, and a laid level's arc stopping a shot while its laser gate switches every turn) and EditMode checks of the scene and prefabs. |

`Resources/Game/*.prefab` and `GamePrefabs.asset` are the player, AI opponent, turn-state, dummy, crate and hazard prefabs and the
`NetPrefabList` the NetBootstrap registers. The wizard also wrote `mp-host.json` and `.github/workflows/nightly.yml` at the project root,
put `Game.unity` first in Build Settings and turned on Run In Background.

## Tests

- Core: `dotnet test Core.slnx` from the project root.
- PlayMode: `Unity -batchmode -projectPath . -runTests -testPlatform PlayMode -assemblyNames Ricochet.Tests`.
- EditMode: `Unity -batchmode -projectPath . -runTests -testPlatform EditMode -assemblyNames Ricochet.EditorTests`.
- Bots end to end: `mp-host game smoke` (a Linux server and two bots under Docker Compose).
