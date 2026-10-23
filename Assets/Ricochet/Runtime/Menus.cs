// The original's rooms as MindAttic Components screens (UR-T9), filled from Core's MenuFlow, Maps and MatchSetup. Game
// adds them to the kit's front end: Players, Controls, How to play and Credits on the main menu and the in-game menu, the
// map room one step on from Players, and the game settings (the announcer) under the kit's Settings, beside the name and
// colour. Each builder only fills a screen it is handed, so a test can build one on a stack of its own.
//   Players (names/players rooms): who you play as (the kit's name and colour), how many AI opponents fill the empty
//     seats, how well they aim and how many aim assists everyone starts with; Next goes on to the map room.
//   Map (maproom): which map, what it is like, and the goal (one round, or first to 500 ... 10000 points); its button
//     starts the game: hosts one from the main menu, or asks the others by the match vote in a game.
//   Controls, How to play (obj_slide's slides with Last/Next and a "3/9" counter), Credits.
using System;
using System.Collections.Generic;
using MindAttic.Components.Unity; // MenuScreen, MaText, MaSelect, TextStyle
using MindAttic.Components.Unity.Core; // ButtonVariant
using Ricochet.Core;              // MenuFlow, Room, Maps, MatchSetup, MatchRules, Ai, ArsenalRules, Announcer
using UnityEngine;

namespace Ricochet
{
    public static class Menus
    {
        /// <summary>
        /// The players room: the local player's name (and a way to change it and the colour), then the AI opponents, their
        /// skill and the aim assists, and Next for the map room (<paramref name="next"/>, when given).
        /// </summary>
        public static void BuildPlayers(MenuScreen s, MatchSetup setup, string playingAs, Action changeNameAndColour, Action<MenuScreen> next)
        {
            s.Subtitle = "Up to " + MatchRules.MaxSeats + " seats: everyone who joins, then the AI opponents.";
            if (!string.IsNullOrEmpty(playingAs))
            {
                s.AddText("You play as " + playingAs + ".", TextStyle.Label);
            }

            if (changeNameAndColour != null)
            {
                s.AddButton("Name and colour", changeNameAndColour);
            }

            s.AddRange("AI opponents", 0, MatchRules.MaxSeats - 1, setup.AiCount, v => setup.AiCount = Mathf.RoundToInt(v), wholeNumbers: true);
            s.AddRange("AI skill", Ai.MinSkill, Ai.MaxSkill, setup.AiSkill, v => setup.AiSkill = Mathf.RoundToInt(v), wholeNumbers: true);
            s.AddRange("Aim assists each (10 = unlimited)", 0, ArsenalRules.Unlimited, setup.AimAssists, v => setup.AimAssists = Mathf.RoundToInt(v),
                wholeNumbers: true);
            if (next != null)
            {
                s.AddButton("Next: " + MenuFlow.Title(MenuFlow.Next(Room.Players)), () => next(s), ButtonVariant.Primary);
            }
        }

        /// <summary>
        /// The map room: the map (with its description) and the goal; <paramref name="start"/>, when given, is the button
        /// that goes on into the game, labelled <paramref name="startLabel"/>.
        /// </summary>
        public static void BuildMap(MenuScreen s, MatchSetup setup, string startLabel, Action start)
        {
            s.Subtitle = "Pick where to play and what it takes to win.";
            var names = new List<string>();
            foreach (MapInfo map in Maps.All)
            {
                names.Add(map.Name);
            }

            MaText description = null;
            s.AddSelect("Map", names, setup.Map, v =>
            {
                setup.Map = v;
                if (description != null)
                {
                    description.Text = Maps.At(setup.Map).Description;
                }
            });
            description = s.AddText(Maps.At(setup.Map).Description, TextStyle.Muted);

            var goals = new List<string>();
            var values = new List<int>();
            int goal = MatchSetup.OneRound;
            do
            {
                values.Add(goal);
                goals.Add(MatchSetup.DescribeGoal(goal));
                goal = MatchSetup.NextGoal(goal);
            }
            while (goal != MatchSetup.OneRound);

            s.AddSelect("Goal", goals, Math.Max(0, values.IndexOf(setup.Goal)), v => setup.Goal = values[Mathf.Clamp(v, 0, values.Count - 1)]);
            if (start != null)
            {
                s.AddButton(startLabel, start, ButtonVariant.Primary);
            }
        }

        /// <summary>The settings room's announcements box: the personality on the air, and how often they speak.</summary>
        public static void AddGameSettings(MenuScreen s, MatchSetup setup)
        {
            s.AddSection(MenuFlow.Title(Room.Settings));
            var voices = new List<string>();
            foreach (AnnouncerVoice voice in Announcer.Voices)
            {
                voices.Add(voice.Name);
            }

            s.AddSelect("Announcer", voices, setup.AnnouncerVoice, v => setup.AnnouncerVoice = v);
            s.AddRange("Announcements (0 off, 1 every time, 4 rarely)", 0, Announcer.MaxFrequency, setup.Announcements,
                v => setup.Announcements = Mathf.RoundToInt(v), wholeNumbers: true);
        }

        /// <summary>The controls room: a line a binding.</summary>
        public static void BuildControls(MenuScreen s)
        {
            foreach (ControlBinding binding in MenuFlow.Controls)
            {
                s.AddText(binding.Keys + "   " + binding.Action);
            }
        }

        /// <summary>The how-to-play room: one slide at a time, with its counter and Last / Next (wrapping).</summary>
        public static void BuildHowToPlay(MenuScreen s, int first = 0)
        {
            int slide = MenuFlow.ClampSlide(first);
            MaText counter = s.AddText(string.Empty, TextStyle.Caption);
            MaText title = s.AddText(string.Empty, TextStyle.Heading);
            MaText body = s.AddText(string.Empty);

            void Show()
            {
                Slide shown = MenuFlow.Slides[slide];
                counter.Text = MenuFlow.SlideCounter(slide);
                title.Text = shown.Title;
                body.Text = string.Join("\n\n", shown.Lines);
            }

            MaRow row = s.AddRow();
            row.AddButton("Last", () =>
            {
                slide = MenuFlow.PreviousSlide(slide);
                Show();
            });
            row.AddButton("Next", () =>
            {
                slide = MenuFlow.NextSlide(slide);
                Show();
            }, ButtonVariant.Primary);
            Show();
        }

        /// <summary>The credits room.</summary>
        public static void BuildCredits(MenuScreen s)
        {
            for (int i = 0; i < MenuFlow.Credits.Count; i++)
            {
                s.AddText(MenuFlow.Credits[i], i == 0 ? TextStyle.Heading : TextStyle.Body);
            }
        }
    }
}
