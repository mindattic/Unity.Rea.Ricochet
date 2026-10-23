# Unity.Ricochet

Turn-based bounce-shot artillery for up to 16 players: pick an angle and a power, bank your shot off walls and
redirectors, and land it on an opponent before they land one on you. Rebuilt in Unity 6 from a 2013 GameMaker
original that could only ever pass the keyboard around one machine; this version adds the real networked
multiplayer GML never could.

![Unity](https://img.shields.io/badge/unity-6000.4-black) ![Netcode for GameObjects](https://img.shields.io/badge/netcode-2.x-blue) ![.NET](https://img.shields.io/badge/dotnet-10-512BD4) ![Status](https://img.shields.io/badge/status-playable%20slice-yellow)

## Why

- The 2013 original (`gmlRicochet`, GameMaker 8) is hotseat-only: one PC, one keyboard, players taking turns at
  the same screen. GML of that era had no real network layer, so "multiplayer" meant passing the keyboard.
- The bounce-aiming mechanic — fire at an angle, watch the shot ricochet off borders and redirectors toward a
  target, same idea as the arcade game *Ricochet* — is the game worth keeping. Everything around it (engine,
  input, rendering, netcode) is being rebuilt from scratch.
- Built on the house kit ([Unity.Multiplayer](https://github.com/mindattic/Unity.Multiplayer),
  [MindAttic.Components](https://github.com/mindattic/MindAttic.Components),
  [Unity.Shared](https://github.com/mindattic/Unity.Shared)) instead of hand-rolled networking, menus or player
  profiles, the same way [Unity.Cursory](https://github.com/mindattic/Unity.Cursory) and
  [Unity.SpaceGod](https://github.com/mindattic/Unity.SpaceGod) are.

## What's being rebuilt

From the original `Ricochet.project.gmx` (`objects/`, `scripts/`, `rooms/`):

- **Turn-based aim-and-fire**: an angle, a power meter and a bounce-count limit per shot; a potential-path
  preview (`draw_potential`, `draw_bounceinfo`) traces the ricochet off level borders and rail redirectors
  before the player commits.
- **Up to 16 players**, human or AI, each with a health bar (`obj_bar1`…`obj_bar16`), a turn-order/turn-timer
  HUD (`draw_turntime`) and a configurable AI count and aim-assist level.
- **Weapons and items**: a cycling weapon/item loadout (`cycle_weapon`, `cycle_item`), grenades with a fuse
  timer, a vortex detonation, a destructible shield wall, electricity and laser hazards, random weapon/item/health
  drops onto the level.
- **An announcer**: portrait + name box commentary (`obj_announcer`, `obj_announcer_portrait`,
  `obj_box_announcements`) between turns, with a selectable announcer and announcer-line cycling.
- **Menus and flow**: splash → title → main menu → player setup (names, colours, AI count) → map/room select →
  game → endgame recap, plus controls/settings/how-to-play/credits screens.

What is **not** being carried over as-is: the GameMaker hotseat "multiplayer" rooms
(`multiplayer_host`/`multiplayer_join`/`multiplayer_hostorjoin`). Real network play (host, join by IP, join by
code, reconnect to your seat) replaces them, from Unity.Multiplayer.

See [docs/BIBLE.md](docs/BIBLE.md) for what this project is and is not, and
[docs/rfc/0001-architecture.md](docs/rfc/0001-architecture.md) for the design and build order.

## Status

The Unity project is scaffolded from Unity.Multiplayer's New Game wizard and the first slice is playable (UR-T3):
aim with an angle and a power, see the whole bounce path before firing (off the border, mirror walls and rail
redirectors, up to the five-contact limit), fire, and watch the server-authoritative shot fly and knock down a
practice dummy, hosted or joined over the network. Matches are in (UR-T4): up to 16 seats of humans and AI
opponents taking turns against a 20-second clock, with health bars and the turn order along the top. Weapons and
items are in (UR-T5): cycle the cannon, laser, shotgun, fused grenade and railgun (Q/E) and the extra bounce,
teleporter and shrink ray (Z/C, F to use), and shoot across the crates that fall during a match for a random weapon,
item or health. AI opponents are in (UR-T7): pick how many, how well they aim and how many aim assists everyone
starts with before voting to start a match; an AI thinks out loud, then takes a straight, bank or railgun shot, and
the aim-assist item (Z/C, F) turns your aim onto a shot that reaches an opponent. Level hazards are in (UR-T6): an electric arc and a laser gate (on every other turn) that
stop a shot, tufts of foliage a shot cuts down, a shield wall only its owner's shots pass, a redirector you set
down, and a vortex you set off in mid-flight with Space that swallows what it reaches and tears breaches in the
border that shots fly out through. The announcer and HUD are in (UR-T8): pick one of the original's seven on-air
personalities and how often they speak; their portrait and name box slide in to welcome everyone, cheer a knock-out and
heckle a shot that hits nobody, a hit on an opponent scores points (100, 25 a bounce, more the longer it flew) that last
the whole match, and a card shows everyone whose turn it is with their score, weapon, item and the combo in the air.
Your profile is in (UR-T10): your name, colour and sound settings (effect and music volume, each with a mute) come from
your MindAttic profile (Unity.Shared), so you set them once under Settings and every later session, and every other
MindAttic game, keeps them; a shot's fire, bounces and landing play at the profile's effect volume.
The menu flow is in (UR-T9): a title screen, then the main menu with the original's rooms off it: Players (your name and
colour, the AI opponents, their skill and the aim assists), then the map room (four maps and the goal: one round, or first
to 500 to 10000 points) and Start; Game settings (the announcer), Controls, How to play (slides) and Credits. A game ends
when a round finishes with someone at the goal, and everyone sees the endgame recap: the champion and the leaderboard, with
a rematch vote. See
[docs/USER_STORIES.md](docs/USER_STORIES.md) (generated by `tickets db render --repo Unity.Ricochet`) for the open
ticket backlog, or `tickets ticket queue --repo Unity.Ricochet`.

## Quick start

Prerequisites: Unity 6000.4.3f1, and the kit's three repos cloned side by side with this one (all siblings under
`D:\Projects\MindAttic\`):

```powershell
git clone https://github.com/mindattic/Unity.Multiplayer
git clone https://github.com/mindattic/MindAttic.Components
git clone https://github.com/mindattic/Unity.Shared
```

`Packages/manifest.json` already lists the three as local packages. Open the project in Unity Hub; the first
open regenerates `Library/` (not committed) from `Packages/` and `ProjectSettings/`. Open `Assets/Ricochet/Game.unity`,
press Play, click Play on the title, then Host (free practice) or Players > Next: Map > Start (a match against AI): Left/Right
aim, Up/Down set the power, Space fires.

Tests: `dotnet test Core.slnx` runs the bounce solver's unit tests without Unity; the PlayMode and EditMode suites
are listed in [Assets/Ricochet/README.md](Assets/Ricochet/README.md).

## Project layout

```text
Assets/Ricochet/     the game (see its README.md)
  Core/              engine-free rules: geometry, level, bounce solver, shot path and flight, aim, match and turns,
                     loadout and arsenal, drops, hazards, announcer and scoring, maps, the room graph and the recap
  Runtime/           networked entities (Player, Dummy, Turns, Crate, LevelHazard), server rules (Arena), scene glue, bot,
                     HUD (IMGUI bars and clock, MindAttic Components announcer and turn cards, the endgame recap), the
                     title and the rooms' screens (Menus), presentation
  Tests/             PlayMode and EditMode tests on the kit's test fixtures
Tests/               dotnet projects compiling Assets/Ricochet/Core for `dotnet test Core.slnx`
Packages/            manifest.json: the house kit (Unity.Multiplayer, MindAttic.Components, Unity.Shared) + Unity packages
docs/
  BIBLE.md           what this project is, is not, and its laws
  rfc/0001-architecture.md   design and build order
  USER_STORIES.md    generated ticket log (tickets db render)
```

## Source material

The GameMaker original lives outside this repo, at
`R:\Projects\_PRE_2022\_PRE_2021\_PRE_2020\_PRE_2017\gmlRicochet\09-05-13_2.20am` (last touched 2013-09-05), read
for design reference only — no GML source is copied into this repo.
