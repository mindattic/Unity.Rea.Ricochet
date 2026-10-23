# Ricochet: the game's Unity folder

Scaffolded by **MindAttic ▸ Multiplayer ▸ New Game…** (Unity.Multiplayer's game template, UR-T3), then built into the
first playable slice of Ricochet: aim, preview the bounce path, fire, and watch the shot bank off the border, the
mirror walls and the rail redirectors onto a practice dummy; then matches (UR-T4): up to 16 seats, humans and AI,
taking turns against a 20-second clock until one is left standing; weapons, items and crates (UR-T5); and AI
opponents that think, aim (straight, bank or railgun shots, as well as their skill allows) and fire, with the
aim-assist item turning a human's aim onto the shot they would take (UR-T7).

Open `Game.unity`, press **Play**, click **Play** on the title screen and **Host** on the main menu, then **Start** in the lobby
(or **Players** > **Next: Map** > **Next: Lobby** > **Start** to host a match). A second copy (Multiplayer Play Mode, or a build) clicks **Join by
IP** (`127.0.0.1`) or **Join by code**. **Left/Right** (or A/D) turn your aim, **Up/Down** (or W/S) set the power,
**Space** fires (and, while your vortex device is in the air, sets it off); **Q/E** cycle the weapon, **Z/C** the item,
**F** uses the item and **1/2/3** set a grenade's fuse. The faint line is the whole path the shot will fly; the bottom-left line reads the angle, the power
and `Bounces: n/5`. **Esc** opens the in-game menu: **Invite players**, **Settings** (name and colour, and the announcer),
**Players** (**AI opponents** 0-15, **AI skill** 0-10, **Aim assists each** 0 none to 10 unlimited, then **Next: Map**: the map
and the goal, and **Vote to start a match**), **Controls**, **How to play**, **Vote to start a match** and **Vote to restart** (the
match from round one, a rematch once a game is over, or every dummy back up in practice).

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

The announcer and HUD (UR-T8): **Settings** also picks the **Announcer** (Kurt Sly, Rye Wheatley, Sid Steinburger, Vlad, Matt
Baxter, Doctor or Haggis McKnight) and **Announcements** (0 off, 1 every time, 2/3/4 one time in 3/6/9) that the start-match
vote carries. Their portrait and name box slide in at the bottom left with a line: a welcome when the match starts, a cheer
naming both players when a shot knocks someone out, a heckle naming the shooter when a shot hits nobody (each of the ten
lines of a kind once before any repeats). A hit on an opponent scores the shooter 100 points, 25 a bounce and 5 for every
tenth of a second the shot was in the air (a pellet its share); scores last the whole match and show on each seat's bar.
The card at the bottom right shows everyone whose turn it is, their score, weapon (and a grenade's fuse), item and the
combo the shot in the air would score (in practice, your own loadout).

The profile (UR-T10): the name and colour you play as, and your sound settings, are your MindAttic profile's
(Unity.Shared, `Profiles.Current.Settings.Common`: `displayName`, `color`, `sfxVolume`, `musicVolume`, `muteSfx`,
`muteMusic`). The kit's front end reads the name and colour from it and sends them with the connection; **Settings** shows
them, saved back to the profile, with the game's **Sound effects volume**, **Mute sound effects**, **Music volume** and
**Mute music** below, heard at once and written to the profile's `settings.json` when the screen closes. `GameAudio` plays
a shot's fire, bounces and landing (and a blast) at the effect volume, synthesised by Core's `Tones` (no audio files),
and re-reads the profile when the game window regains focus.

The menu flow (UR-T9) follows the original's rooms: the title screen (**Play**, **Quit**), then the kit's main menu (**Host**,
**Join by IP**, **Join by code**, **Settings**) with **Players**, **Controls**, **How to play** (slides, **Last**/**Next**) and
**Credits** off it. **Players** goes on to the map room: **Map** (Practice Range, Crossfire, Pillars or Funnel: the same border,
seats and laid hazards, different walls and redirectors) and **Goal** (one round, or first to 500 ... 10000 points; 500 by
default), whose **Next: Lobby** hosts a game and opens the lobby. In a game the map room's button is the match vote, which
carries the map and the goal. When a round ends with someone at the goal (after the round's result), the game is over: nobody
fires, and every peer's endgame recap shows "<champion> wins!" over the leaderboard (every seat by score, 1st, 2nd ...), with
**Vote for a rematch**, **Main menu** and **Close**; a new match or a rematch takes it away.

Playing together (UR-T11) replaces the original's hotseat `multiplayer_hostorjoin`/`multiplayer_host`/`multiplayer_join` rooms
with the kit's front end: **Host**, **Join by IP** (an address, `:port` optional) and **Join by code** on the main menu. Hosting
(**Host**, or the map room's **Next: Lobby**) opens the kit's lobby: the join code with Copy (or, with no join-code service, the
address to join at), the players in it (name, colour, Host tag, remove), **Leave** and **Start**. `LobbyRoom` adds the game's
**Match** box: the map and the goal, how the sixteen seats fill as people join ("2 players and 3 AI opponents: 5 of 16 seats",
Core's `Lobby`), the AI and the announcer, and how the others get in; from the map room **Start** waits until the match could seat
two, and then plays it with everyone in the lobby. Hosting from **Host** opens the same lobby for free practice. A client goes
straight into the game when it joins (a later joiner plays from the next round), and **Esc** > **Invite players** shows the code
again. A join code needs the project linked to Unity Relay (Edit > Project Settings > Services), or a rendezvous URL on the
NetBootstrap (`server/Rendezvous` in Unity.Multiplayer); without either the host is told so and players join by IP.

Reconnects (UR-T13): a player whose connection drops keeps their seat for the kit's reconnect grace (`NetConfig.ReconnectGraceSeconds`,
60 seconds by default). Their player stays where it stood, marked away (nobody plays it, not the host either), and in a match their
seat keeps its health and score and stays in the turn order; when it is their turn the clock stops and the line under it reads
"Waiting for <name> to reconnect (Ns)". Back in time, with the same profile, they get the same player and seat and resume the same
turn with the time it had left. Not back in time, they leave: out of the round, and the turn passes.

## Layers (docs/rfc/0001-architecture.md)

| Folder | What lives there |
|---|---|
| `Core/` (`Ricochet.Core`, no engine references) | The rules as plain C#: `Geometry` (the original's `LineLineIntersection_X/_Y` and `lines_intersect`, the reflection rule from `bullet_step`), `Level` (border, mirror walls, redirectors), `BounceSolver` (the shot's whole path, and `FindHeading`, the original's `find_target_angle`), `ShotPath`, `ProjectileFlight`, `AimState`, `ShotRules`; and the match: `Match` (seats, turn order, turn clock, health, rounds; the original's `next_player`, `turn_countdown`, `hit` and `check_endgame`), `MatchRules`, `Seating` (sixteen spots, the HUD's bar slots, the `SS.FF` clock), `MatchSetup` (AI count, AI skill, aim assists: the start-match vote's payload); the AI (UR-T7): `Ai` (the targeting sweep, the original's `check_ai` and `find_target_angle(_from_bounce)`: straight, bank, then railgun shots, nudged by skill; and aim assist) and `AiTurn` (think, plan, turn the barrel, fire); and the arsenal (UR-T5): `ArsenalRules` and `Loadout` (counts, selection, fuse; the original's `cycle_weapon/item_left/right`), `Arsenal`/`Volley` (what each weapon and item does), `Drops` (`DropField`, `Pickups`: the random crates); and the hazards (UR-T6): `Hazards` (`HazardField`, `HazardRules`: the shield wall, electric arc, laser gate, foliage, breach and placed redirector, the level a shot sees with them on it, a vortex's implosion), with `Level`'s barriers and breaches and `BounceSolver` stopping at the one and escaping through the other; and the announcer (UR-T8): `Announcer` (the seven personalities, the announcements setting's odds, the welcome/win/lose lines drawn without repeats) with `MatchSetup` carrying the choice, and `MatchRules.Points`/`Match`'s per-seat score; and the menu flow (UR-T9): `Maps` (the map room's four levels), `MenuFlow` (the room graph, the controls, the how-to-play slides, the credits), `Recap` (the endgame leaderboard), `MatchSetup`'s goal and map, and `Match`'s goal, `GameOver` phase and champion; and sound (UR-T10): `Sound` (`AudioMix`, the profile's volumes and mutes; `Tones`, the synthesised effects); and the lobby (UR-T11): `Lobby` (how the seats fill, whether a match can start, the lobby's match box), with `MenuFlow`'s `Lobby` room between the map room and the game; and the shot on the wire (UR-T12): `ShotWire` (a solved volley packed exactly for a `[ToClients]` call, and unpacked) and `ShotOutcome` (the damage a landed volley did to each seat); and reconnects (UR-T13): `Match.Drop`/`Rejoin`, a seat `Away` whose turn waits for it. Tested without Unity: `dotnet test Core.slnx`. |
| `Runtime/` (`Ricochet`) | The networked game and its drawing. `Player` (a `NetEntity`: `[ToServer] Aim` and `Fire`, `[Synced]` aim, last shot, seat, health and whether still in the round; the server solves and flies the shot, refuses one out of turn, and lands it, and sends every client the volley it solved and, in a match, the damage it dealt (`[ToClients] ShotResolved` and `ShotResult`), so every peer draws the server's bounce and floats the same damage; an AI seat is the same entity from the `Opponent` prefab), `Turns` (the keyed match state every peer reads: phase, whose turn, the clock, the round), `Dummy` (keyed practice targets), `Crate` (a keyed falling crate), `LevelHazard` (a keyed hazard every peer rebuilds the shot's level from, drawing its arc, beam, wall, tuft, breach or redirector), `Arena` (the server's rules: the level, the match and its AI turns, the start-match and restart votes, a dropped player's held seat), `Game` (scene glue), `GameBot` (the bot), and presentation: `MatchHud` (IMGUI bars, scores, clock, turn order) with `HudOverlay` (MindAttic Components: the announcer's card and the turn card) and `EndgameRecap` (the endgame room), the rooms (`TitleScreen`, and `Menus`: players, map, game settings, controls, how-to-play and credits screens on the kit's front end), `LobbyRoom` (the match box on the kit's host lobby, and the map room's setup carried through it to Start), `LevelView` (redrawn when a match is on another map), `ShotView`, `Lines`, `Shapes`, `GameInput`. |
| `Tests/` | PlayMode tests on the kit's `NetSessionTest`/`NetBotTest` (aim round trip and clamping, a shot landing on a dummy with the client flying the server's path, one shot in the air at a time, dummies by key, the restart vote, two bots each banking a shot onto their dummy; and in `TurnTests` a match seating two players and an AI, a shot out of turn refused, the turn passing on a landed shot and on the clock, a hit's damage seen by everyone, the client flying the server's own bounce and getting the server's damage for it, the AI taking its turn; and in `WeaponTests` cycling the weapon and item and setting the fuse, an extra bounce and a teleport, a grenade spent and its blast seen by everyone, a crate collected by a shot; and in `AiOpponentTests` a fully skilled AI's "is thinking..." line on the client and its hit on an opponent, a client's aim assist turning her aim onto the host and her shot landing, and a match with no aim assist; and in `HazardTests` a shield wall stopping another player's shot and shattering, a placed redirector, a vortex set off in mid-flight swallowing a wall and breaching the border a later shot escapes through, and a laid level's arc stopping a shot while its laser gate switches every turn; and in `AnnouncerTests` the welcome in the chosen voice on the client with its HUD's announcer and turn cards, a heckle for a shot at nobody, a cheer and the shooter's score for a knock-out, and silence with the announcer off; and in `ProfileTests` a profile's name and colour reaching every peer's player, and its volumes applied and a change saved to its settings.json; and in `MenuFlowTests` a one-round game ending for everyone with the champion and the recap on the client and nobody firing until a rematch, a match on another map played there by every peer, and the title, players, map room and how-to-play screens' buttons; and in `LobbyTests` two of the kit's front ends: hosting from the map room into the lobby with the join code, the roster and the match box holding Start back, a client joining by that code and the match starting on the chosen map with both seated, and Host's practice lobby joined by IP; and in `ReconnectTests` a client dropping on her own turn, the turn waiting with her seat and health held, and her reconnecting to the same turn and firing, and a seat given up after the grace) and EditMode checks of the scene and prefabs. |

`Resources/Game/*.prefab` and `GamePrefabs.asset` are the player, AI opponent, turn-state, dummy, crate and hazard prefabs and the
`NetPrefabList` the NetBootstrap registers. The wizard also wrote `mp-host.json` and `.github/workflows/nightly.yml` at the project root,
put `Game.unity` first in Build Settings and turned on Run In Background.

## Tests

- Core: `dotnet test Core.slnx` from the project root.
- PlayMode: `Unity -batchmode -projectPath . -runTests -testPlatform PlayMode -assemblyNames Ricochet.Tests`.
- EditMode: `Unity -batchmode -projectPath . -runTests -testPlatform EditMode -assemblyNames Ricochet.EditorTests`.
- Bots end to end: `mp-host game smoke` (a Linux server and two bots under Docker Compose).
