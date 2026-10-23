// The menu flow (UR-T9): the original's room graph, and what its screens say. The 2013 original (design reference only,
// UR-LAW-2) went titlescreen -> mainmenu -> names/players (the roster: names, colours, AI players and their skill) ->
// maproom (the level) -> gamescreen (the match) -> endgame (the winner on a pedestal and a leaderboard), with settings,
// controls, howtoplay and credits each one button off the main menu and a Back button to it. Its multiplayer_hostorjoin/
// host/join rooms are not here: the kit's Host / Join by IP / Join by code screens replace them (BIBLE §3).
// The kit's host lobby (the join code and who is in, UR-T11) stands between the map room and the game as the Lobby room.
// Here every room but the game is a screen: the title is shown first, the kit's main menu is the main menu, and the
// setup rooms are screens on it and on the in-game menu (the setup is only proposed to the others by the match vote).
// The how-to-play slides, the controls and the credits are written for this game, in this project's own words.
using System;
using System.Collections.Generic;

namespace Ricochet.Core
{
    /// <summary>The original's rooms that this game keeps.</summary>
    public enum Room
    {
        Title,
        MainMenu,
        Players,
        MapRoom,
        Game,
        Endgame,
        Settings,
        Controls,
        HowToPlay,
        Credits,

        /// <summary>
        /// The host's lobby (UR-T11): the kit's lobby screen, with the join code (or the address) and who is in, and the
        /// match it will start. It stands where the original's multiplayer_host room did, between the map room and the game.
        /// </summary>
        Lobby,
    }

    /// <summary>A line on the controls screen: the keys, and what they do.</summary>
    public readonly struct ControlBinding
    {
        public ControlBinding(string keys, string action)
        {
            Keys = keys;
            Action = action;
        }

        public string Keys { get; }

        public string Action { get; }

        public override string ToString() => Keys + ": " + Action;
    }

    /// <summary>One how-to-play slide (obj_slide's slidetext): a heading and its lines.</summary>
    public sealed class Slide
    {
        public Slide(string title, params string[] lines)
        {
            Title = title;
            Lines = lines ?? Array.Empty<string>();
        }

        public string Title { get; }

        public IReadOnlyList<string> Lines { get; }

        /// <summary>Everything on the slide, the heading first, a line each.</summary>
        public string Text => Title + "\n" + string.Join("\n", Lines);
    }

    public static class MenuFlow
    {
        /// <summary>What the title screen says under the name.</summary>
        public const string Tagline = "Bank it off the walls. Land it on your friends.";

        /// <summary>The room a room's main button goes on to: the original's titlescreen -> mainmenu -> ... -> endgame -> mainmenu.</summary>
        public static Room Next(Room room)
        {
            switch (room)
            {
                case Room.Title:
                    return Room.MainMenu;
                case Room.MainMenu:
                    return Room.Players;
                case Room.Players:
                    return Room.MapRoom;
                case Room.MapRoom:
                    return Room.Lobby;
                case Room.Lobby:
                    return Room.Game;
                case Room.Game:
                    return Room.Endgame;
                default:
                    return Room.MainMenu;
            }
        }

        /// <summary>
        /// Where a room's Back button goes (obj_btn_back_*): the main menu for every room off it, the players room from the
        /// map room (one step back along the setup), and the title for the main menu itself. Back from the lobby ends the
        /// game being hosted, so it is the main menu too.
        /// </summary>
        public static Room Back(Room room)
        {
            switch (room)
            {
                case Room.MainMenu:
                    return Room.Title;
                case Room.MapRoom:
                    return Room.Players;
                case Room.Title:
                    return Room.Title;
                default:
                    return Room.MainMenu;
            }
        }

        /// <summary>The rooms the main menu opens directly (mainmenu's buttons), in its order.</summary>
        public static IReadOnlyList<Room> OffTheMainMenu { get; } = new[] { Room.Players, Room.Settings, Room.Controls, Room.HowToPlay, Room.Credits };

        /// <summary>A room's name on its button and at the top of its screen.</summary>
        public static string Title(Room room)
        {
            switch (room)
            {
                case Room.Title:
                    return "Ricochet";
                case Room.MainMenu:
                    return "Main menu";
                case Room.Players:
                    return "Players";
                case Room.MapRoom:
                    return "Map";
                case Room.Game:
                    return "Game";
                case Room.Endgame:
                    return "Game over";
                case Room.Settings:
                    return "Game settings";
                case Room.Controls:
                    return "Controls";
                case Room.HowToPlay:
                    return "How to play";
                case Room.Credits:
                    return "Credits";
                case Room.Lobby:
                    return "Lobby";
                default:
                    return room.ToString();
            }
        }

        /// <summary>The controls screen (the original's keyboard-layout picture), as lines.</summary>
        public static IReadOnlyList<ControlBinding> Controls { get; } = new[]
        {
            new ControlBinding("Left / Right (A / D)", "Turn your aim"),
            new ControlBinding("Up / Down (W / S)", "Set the power"),
            new ControlBinding("Space", "Fire (again, while your vortex device flies: set it off)"),
            new ControlBinding("Q / E", "Cycle your weapon"),
            new ControlBinding("Z / C", "Cycle your item"),
            new ControlBinding("F", "Use your item"),
            new ControlBinding("1 / 2 / 3", "Grenade fuse: shorter, longer, longest"),
            new ControlBinding("Esc", "The in-game menu: players, map, settings, votes, invite, leave"),
        };

        /// <summary>The how-to-play slides, in order.</summary>
        public static IReadOnlyList<Slide> Slides { get; } = new[]
        {
            new Slide("Introduction",
                "Ricochet is a game of angles: every player has a cannon, and the shortest way to an opponent is rarely a straight line.",
                "The faint line in front of you is the whole path your shot will fly, bounce by bounce."),
            new Slide("Attacking",
                "Players take turns firing at each other. Bank a shot off the border and the white walls, and the orange redirectors send it along their arrows.",
                "A shot goes off on its fifth contact. Every bounce before a hit adds to the damage, and to the points it scores.",
                "Careful: a shot that comes back to you hurts you, ten health a bounce."),
            new Slide("Time limit",
                "Each turn has a 20-second clock at the top of the screen. Fire before it runs out, or the turn passes to the next player."),
            new Slide("Rounds and the goal",
                "A round goes on until one player is left standing. A hit on an opponent scores 100 points, 25 more for each bounce and 5 for every tenth of a second the shot was in the air.",
                "Scores last the whole game. Once a round ends with someone at the goal (500 points unless you change it), the game is over and the top score wins."),
            new Slide("Crates",
                "Crates fall into the arena during a round. Shoot across one to collect it: W a weapon, I an item, + some health, ? a mystery weapon."),
            new Slide("Weapons",
                "Q and E cycle your weapon. You carry three of each, and the cannon never runs out.",
                "Cannon: one shell. Laser: the full range, three times as fast. Shotgun: a fan of five pellets.",
                "Grenade: passes through players and goes off when its fuse runs out (1, 2 and 3 set it). Railgun: straight through the walls, hitting everyone on the line.",
                "Vortex device: press Space again while it flies to set it off; it hurts everyone in its pull, swallows the shield walls, redirectors and grass it reaches and tears a breach in the border."),
            new Slide("Items",
                "Z and C cycle your item, F uses it before you fire.",
                "Extra bounce: one more contact for this turn's shot. Shield wall: a wall in front of you only your own shots pass.",
                "Shrink ray: shrinks the first player it reaches. Teleporter: moves you to where it lands.",
                "Aim assist: turns your aim onto a shot that reaches an opponent. Redirector: sets a redirector down where it lands."),
            new Slide("Hazards",
                "The blue electric arc stops any shot that touches it. The red laser gate does too, but only on every other turn.",
                "A shot cuts down the first tuft of grass it touches, and stops there."),
            new Slide("Opponents and the announcer",
                "AI opponents fill the empty seats: choose how many and how well they aim on the Players screen. On its turn an AI thinks out loud, then looks for a straight shot, a bank shot or a railgun shot.",
                "The announcer welcomes everyone, cheers a knock-out and heckles a shot that hits nobody. Choose who, and how often, under Game settings."),
            new Slide("Playing together",
                "Everyone plays on their own computer. One player hosts: Players, then the map, then Start opens the lobby with a join code to share (or the address to join at).",
                "The others click Join by code and type it in, or Join by IP with the host's address. When everyone is in the lobby, the host presses Start and the match begins.",
                "Someone who joins later plays from the next round; Esc > Invite players shows the code again."),
            new Slide("Your profile",
                "Your name, colour and sound volumes come from your MindAttic profile, so you set them once: change them under Settings and every session, and every MindAttic game, keeps them."),
        };

        /// <summary>obj_btn_nextslide: the next slide, wrapping from the last to the first.</summary>
        public static int NextSlide(int slide) => (ClampSlide(slide) + 1) % Slides.Count;

        /// <summary>obj_btn_lastslide: the slide before, wrapping from the first to the last.</summary>
        public static int PreviousSlide(int slide) => (ClampSlide(slide) + Slides.Count - 1) % Slides.Count;

        /// <summary>obj_slide's "3/9" counter (slides counted from 1).</summary>
        public static string SlideCounter(int slide) => (ClampSlide(slide) + 1) + "/" + Slides.Count;

        public static int ClampSlide(int slide) => slide >= 0 && slide < Slides.Count ? slide : 0;

        /// <summary>The credits screen, a line each.</summary>
        public static IReadOnlyList<string> Credits { get; } = new[]
        {
            "Ricochet",
            "Based on the 2013 GameMaker game of the same name.",
            "Rebuilt in Unity 6 by MindAttic, with real network play in place of the original's one shared keyboard.",
            "Networking, lobby, join codes and votes: Unity.Multiplayer.",
            "Menus and HUD cards: MindAttic Components.",
            "Player profiles: Unity.Shared.",
            "Thanks for playing.",
        };
    }

    /// <summary>One line of the endgame recap.</summary>
    public readonly struct RecapEntry
    {
        public RecapEntry(int seat, string name, int score, bool isAi = false)
        {
            Seat = seat;
            Name = name ?? string.Empty;
            Score = score;
            IsAi = isAi;
        }

        public int Seat { get; }

        public string Name { get; }

        /// <summary>The score shown: never below zero (the endgame room showed a negative score as 0).</summary>
        public int Score { get; }

        public bool IsAi { get; }
    }

    /// <summary>
    /// The endgame room's leaderboard (obj_endgame): every seat ranked by score, highest first and the lower seat first on a
    /// tie, with its place ("1st", "2nd" ...); the first is the champion, whose name goes over "Wins!".
    /// </summary>
    public static class Recap
    {
        /// <summary>The entries ranked: score high to low, then seat low to high. Negative scores count as 0.</summary>
        public static List<RecapEntry> Rank(IEnumerable<RecapEntry> entries)
        {
            var ranked = new List<RecapEntry>();
            if (entries != null)
            {
                foreach (RecapEntry entry in entries)
                {
                    ranked.Add(new RecapEntry(entry.Seat, entry.Name, Math.Max(0, entry.Score), entry.IsAi));
                }
            }

            ranked.Sort((a, b) => a.Score != b.Score ? b.Score.CompareTo(a.Score) : a.Seat.CompareTo(b.Seat));
            return ranked;
        }

        /// <summary>"1st", "2nd", "3rd", "4th" ... "11th", "12th", "13th" ... "21st" for a place counted from 1.</summary>
        public static string Place(int place)
        {
            if (place <= 0)
            {
                return "-";
            }

            int tens = place % 100;
            string suffix = tens >= 11 && tens <= 13 ? "th"
                : (place % 10) == 1 ? "st"
                : (place % 10) == 2 ? "nd"
                : (place % 10) == 3 ? "rd"
                : "th";
            return place + suffix;
        }

        /// <summary>"Ann wins!" (the champion's name over the pedestal), or "Nobody wins." for an empty table.</summary>
        public static string Headline(IReadOnlyList<RecapEntry> ranked)
        {
            return ranked == null || ranked.Count == 0 ? "Nobody wins." : ranked[0].Name + " wins!";
        }

        /// <summary>A leaderboard line: "1st  Ann  740" (with an AI tag).</summary>
        public static string Line(int place, RecapEntry entry)
        {
            return Place(place) + "   " + (entry.IsAi ? "[AI] " : string.Empty) + entry.Name + "   " + entry.Score;
        }
    }
}
