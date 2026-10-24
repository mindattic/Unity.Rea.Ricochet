# MindAttic project agent entrypoint

Read the shared protocol at ..\mindattic-agent-standard\AGENTS.md and this project's own
documentation. Common prompt commands are implemented by the shared runner; do not add a second
copy under a provider-specific command folder.

## Rebuild standard (owner 2026-10-23)

This repo upgrades an old prototype to Unity. The gameplay is kept exactly; the code is not. Everything is
rebuilt as cutting-edge, best-practice Unity code design and organisation. Unity.SpaceGod and Unity.GridGame
are the reference architectures: read their AGENTS.md, README and docs/BIBLE.md before designing anything here.

1. **REA the original in its totality first.** Every object, script, room, sprite, sound and data file is
   reverse-engineered and recorded in docs/RECONSTRUCTION.md before it is rebuilt. Gameplay matches the original;
   where the rebuild deviates, the BIBLE says so and why.
   **Broken things are fixed.** Where the original's source is plainly broken (a random that is not random, a draw
   call commented out, a feature that can never be reached, sound that ships switched off), the rebuild plays the way
   the game was clearly meant to, not the way the bug made it, and the BIBLE records each one as a fixed defect.
   Everything that is not broken matches the original exactly: never substitute or approximate a working behaviour.
2. **Core is pure C#.** The rules live in an engine-free Core assembly (`noEngineReferences`, no `UnityEngine`),
   tested with `dotnet test` outside Unity. State changes only through commands that produce events;
   presentation reads state and plays events, and never mutates the game.
3. **Assemblies have hard boundaries.** Core, Runtime (MonoBehaviours, presentation), Editor and Tests are separate
   asmdefs with one-way references.
4. **Content and tuning are data.** Levels, items, enemies, text and every tuning number live in data files or a
   settings registry, validated on load, not hard-coded in behaviour.
5. **Built in code, not by hand in the Editor.** Scenes, UI and objects are composed in code (or generated), so
   nothing depends on Inspector wiring the owner has to click together.
6. **Modern Unity only.** Unity 6, URP behind a pipeline-agnostic layer, the Input System, UI Toolkit or the shared
   MindAttic components, Unity.Shared profiles and settings, multiplayer-ready seams (commands carrying a player id,
   one authority, no global current-player singleton).
7. **Done means verified by play, not asserted.** Unity 6000.4.3f1 runs headless here, so every ticket that touches
   Unity-side code or assets runs a headless import/compile of its own worktree (`-batchmode -quit -projectPath <worktree>`,
   never touching an editor the owner has open), runs the EditMode (and, where relevant, PlayMode) tests it touches, and
   commits any `.meta` files Unity writes, before it lands. Landing Unity code that was never compiled is not allowed. Core tests green, a build that launches, and the feature seen
   working (screenshots or a scripted run) before a ticket closes.
8. **Finite.** Once every story from the reconstruction is built, the repo is a complete, polished proof of concept
   of the original, and is done.

9. **Online-ready.** This game is built on the shared Unity.Multiplayer kit so an online version can be set up: one authority holds the state, players are NetEntities with seats, and local play goes through the same path. Read Unity.Multiplayer's docs/articles before touching networking.\n