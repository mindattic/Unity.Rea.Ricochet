// The menu flow (UR-T9): the original's room graph, and what its screens say. The 2013 original (design reference only,
// UR-LAW-2) went titlescreen -> mainmenu -> names/players (the roster: names, colours, AI players and their skill) ->
// gamescreen (the match) -> endgame (the winner on a pedestal and a leaderboard), with settings,
// controls, howtoplay and credits each one button off the main menu and a Back button to it. Its multiplayer_hostorjoin/
// host/join rooms are not here: the kit's Host / Join by IP / Join by code screens replace them (BIBLE §3).
// The kit's host lobby (the join code and who is in, UR-T11) stands between the players room and the game as the Lobby room.
// Here every room but the game is a screen: the title is shown first, the kit's main menu is the main menu, and the
// setup rooms are screens on it and on the in-game menu (the setup is only proposed to the others by the match vote).
// The how-to-play slides, the controls and the credits are written for this game, in this project's own words, and are
// data (menu.json, UR-T35).
using System;
using System.Collections.Generic;

namespace Ricochet.Core.Menu
{
    using Ricochet.Core.Level;
    /// <summary>The original's rooms that this game keeps.</summary>
    public enum Room
    {
        Title,
        MainMenu,
        Players,
        Game,
        Endgame,
        Settings,
        Controls,
        HowToPlay,
        Credits,

        /// <summary>
        /// The host's lobby (UR-T11): the kit's lobby screen, with the join code (or the address) and who is in, and the
        /// match it will start. It stands where the original's multiplayer_host room did, between the players room and the game.
        /// </summary>
        Lobby,

        /// <summary>
        /// U.RICO-T52: the Start screen, the titlescreen room's setup panel (players, shapes, type, rotation, overlap, point
        /// goal, turn time, bounces) as the first setup screen: the title's Start and the main menu open it, its Next goes on to
        /// the players room.
        /// </summary>
        Setup,
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

    /// <summary>One how-to-play slide (obj_slide's slidetext): a heading and its lines, and the pictures on its frame.</summary>
    public sealed class Slide
    {
        public Slide(string title, params string[] lines)
            : this(title, lines, null)
        {
        }

        public Slide(string title, string[] lines, string[] images)
        {
            Title = title;
            Lines = lines ?? Array.Empty<string>();
            Images = images ?? Array.Empty<string>();
        }

        public string Title { get; }

        public IReadOnlyList<string> Lines { get; }

        /// <summary>U.RICO-T54: the GML sprites drawn on the slide's frame, in order (Core's SlidePicture lays them out).</summary>
        public IReadOnlyList<string> Images { get; }

        /// <summary>Everything on the slide, the heading first, a line each.</summary>
        public string Text => Title + "\n" + string.Join("\n", Lines);
    }

    public static class MenuFlow
    {
        /// <summary>What the title screen says under the name.</summary>
        public static string Tagline => Data.Menu.Tagline;

        private static Ricochet.Core.Data.GameData Data => Ricochet.Core.Data.GameData.Current;

        /// <summary>
        /// The room a room's main button goes on to: the title's Start (and the main menu's) opens the Start screen (U.RICO-T52,
        /// the titlescreen room's setup panel), then players (whose Start goes straight on to the game, obj_btn_start: no map room, every round is the random level) -> lobby -> game -> endgame -> mainmenu.
        /// </summary>
        public static Room Next(Room room)
        {
            switch (room)
            {
                case Room.Title:
                case Room.MainMenu:
                    return Room.Setup;
                case Room.Setup:
                    return Room.Players;
                case Room.Players:
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
        /// Where a room's Back button goes (obj_btn_back_*): the main menu for every room off it, the Start screen from the
        /// players room (one step back along the setup), and the title for the main
        /// menu itself. Back from the lobby ends the game being hosted, so it is the main menu too.
        /// </summary>
        public static Room Back(Room room)
        {
            switch (room)
            {
                case Room.MainMenu:
                    return Room.Title;
                case Room.Players:
                    return Room.Setup;
                case Room.Title:
                    return Room.Title;
                default:
                    return Room.MainMenu;
            }
        }

        /// <summary>The rooms the main menu opens directly (mainmenu's buttons), in its order.</summary>
        public static IReadOnlyList<Room> OffTheMainMenu => Data.Menu.OffTheMainMenu;

        /// <summary>A room's name on its button and at the top of its screen.</summary>
        public static string Title(Room room) => Data.Menu.Titles.TryGetValue(room, out string title) ? title : room.ToString();

        /// <summary>The controls screen (the original's keyboard-layout picture), as lines.</summary>
        public static IReadOnlyList<ControlBinding> Controls => Data.Menu.Controls;

        /// <summary>The how-to-play slides, in order.</summary>
        public static IReadOnlyList<Slide> Slides => Data.Menu.Slides;

        /// <summary>obj_btn_nextslide: the next slide, wrapping from the last to the first.</summary>
        public static int NextSlide(int slide) => (ClampSlide(slide) + 1) % Slides.Count;

        /// <summary>obj_btn_lastslide: the slide before, wrapping from the first to the last.</summary>
        public static int PreviousSlide(int slide) => (ClampSlide(slide) + Slides.Count - 1) % Slides.Count;

        /// <summary>obj_slide's "3/9" counter (slides counted from 1).</summary>
        public static string SlideCounter(int slide) => (ClampSlide(slide) + 1) + "/" + Slides.Count;

        public static int ClampSlide(int slide) => slide >= 0 && slide < Slides.Count ? slide : 0;

        /// <summary>The credits screen, a line each.</summary>
        public static IReadOnlyList<string> Credits => Data.Menu.Credits;

        /// <summary>
        /// U.RICO-T52: the names of the hover tips (menu.json's "tips": each control's message, which the original drew at the
        /// bottom of the room while the pointer was on it).
        /// </summary>
        public static readonly string[] TipNames =
        {
            "start", "quit", "players", "shapes", "type", "rotation", "overlap", "goal", "turnTime", "bounces", "defaults", "next",
        };

        /// <summary>The hover tip called <paramref name="name"/> (one of <see cref="TipNames"/>), or empty.</summary>
        public static string Tip(string name) => name != null && Data.Menu.Tips.TryGetValue(name, out string tip) ? tip : string.Empty;
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
