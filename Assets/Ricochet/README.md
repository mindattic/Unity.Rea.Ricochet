# Ricochet: the game's Unity folder

Scaffolded by **MindAttic ▸ Multiplayer ▸ New Game…** (Unity.Multiplayer's game template, UR-T3), then built into the
first playable slice of Ricochet: aim, preview the bounce path, fire, and watch the shot bank off the border, the
mirror walls and the rail redirectors onto a practice dummy. Turns, seats and health come next (UR-T4).

Open `Game.unity`, press **Play** and click **Host**. A second copy (Multiplayer Play Mode, or a build) clicks **Join by
IP** (`127.0.0.1`) or **Join by code**. **Left/Right** (or A/D) turn your aim, **Up/Down** (or W/S) set the power,
**Space** fires. The faint line is the whole path the shot will fly; the bottom-left line reads the angle, the power
and `Bounces: n/5`. **Esc** opens the in-game menu: **Invite players**, **Settings** (name and colour), **Vote to
restart** (stands every dummy back up).

## Layers (docs/rfc/0001-architecture.md)

| Folder | What lives there |
|---|---|
| `Core/` (`Ricochet.Core`, no engine references) | The rules as plain C#: `Geometry` (the original's `LineLineIntersection_X/_Y` and `lines_intersect`, the reflection rule from `bullet_step`), `Level` (border, mirror walls, redirectors), `BounceSolver` (the shot's whole path, and `FindHeading`, the original's `find_target_angle`), `ShotPath`, `ProjectileFlight`, `AimState`, `ShotRules`. Tested without Unity: `dotnet test Core.slnx`. |
| `Runtime/` (`Ricochet`) | The networked game and its drawing. `Player` (a `NetEntity`: `[ToServer] Aim` and `Fire`, `[Synced]` aim and last shot; the server solves and flies the shot and knocks down what it lands on, every peer replays the same flight to draw it), `Dummy` (keyed practice targets), `Arena` (the server's rules, the level and the restart vote), `Game` (scene glue), `GameBot` (the bot), and presentation: `LevelView`, `ShotView`, `Lines`, `Shapes`, `GameInput`. |
| `Tests/` | PlayMode tests on the kit's `NetSessionTest`/`NetBotTest` (aim round trip and clamping, a shot landing on a dummy with the client flying the server's path, one shot in the air at a time, dummies by key, the restart vote, two bots each banking a shot onto their dummy) and EditMode checks of the scene and prefabs. |

`Resources/Game/*.prefab` and `GamePrefabs.asset` are the player and dummy prefabs and the `NetPrefabList` the
NetBootstrap registers. The wizard also wrote `mp-host.json` and `.github/workflows/nightly.yml` at the project root,
put `Game.unity` first in Build Settings and turned on Run In Background.

## Tests

- Core: `dotnet test Core.slnx` from the project root.
- PlayMode: `Unity -batchmode -projectPath . -runTests -testPlatform PlayMode -assemblyNames Ricochet.Tests`.
- EditMode: `Unity -batchmode -projectPath . -runTests -testPlatform EditMode -assemblyNames Ricochet.EditorTests`.
- Bots end to end: `mp-host game smoke` (a Linux server and two bots under Docker Compose).
