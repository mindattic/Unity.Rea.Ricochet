// The original's rooms as MindAttic Components screens (UR-T9), filled from Core's MenuFlow, Maps and MatchSetup. Game
// adds them to the kit's front end: Players, Controls, How to play and Credits on the main menu and the in-game menu, the
// map room one step on from Players, and the game settings (the announcer) under the kit's Settings, beside the name and
// colour. Each builder only fills a screen it is handed, so a test can build one on a stack of its own.
//   Players (names/players rooms): who you play as (the kit's name and colour), how many AI opponents fill the empty
//     seats, how well they aim and how many aim assists everyone starts with; Next goes on to the map room.
//   Map (maproom): which map, what it is like, and the goal (one round, or first to 500 ... 10000 points); its button
//     starts the game: hosts one from the main menu, or asks the others by the match vote in a game.
//   Players and Map both carry a Reset to defaults (UR-T41, the original's Default button) when Game gives them a reset:
//     the whole setup back to the defaults and saved, the screen rebuilt to show it.
//   Vote to kick (UR-T14, in game only): a button for each player who may be voted out.
//   Controls, How to play (obj_slide's slides with Last/Next and a "3/9" counter), Credits.
//   Rebind controls (UR-T36): one row per key, mouse button or gamepad control the RicochetControls asset binds;
//     clicking a row's button starts an interactive rebind, saved to the active Unity.Shared profile at once.
using System;
using System.Collections.Generic;
using MindAttic.Components.Unity; // MenuScreen, MaText, MaSelect, TextStyle
using MindAttic.Components.Unity.Core; // ButtonVariant
using Ricochet.Core.Ai;
using Ricochet.Core.Arsenal;
using Ricochet.Core.Level;
using Ricochet.Core.Match;
using Ricochet.Core.Menu;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ricochet
{
    public static class Menus
    {
        /// <summary>
        /// The players room: the local player's name (and a way to change it and the colour), then the AI opponents, their
        /// skill and the aim assists, and Next for the map room (<paramref name="next"/>, when given).
        /// </summary>
        public static void BuildPlayers(MenuScreen s, MatchSetup setup, string playingAs, Action changeNameAndColour, Action<MenuScreen> next,
            Action resetSetup = null)
        {
            s.Subtitle = "Up to " + MatchRules.MaxSeats + " seats: everyone who joins, then the AI opponents.";
            AddResetButton(s, resetSetup, () => BuildPlayers(s, setup, playingAs, changeNameAndColour, next, resetSetup));
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
        public static void BuildMap(MenuScreen s, MatchSetup setup, string startLabel, Action start, Action resetSetup = null)
        {
            s.Subtitle = "Pick where to play and what it takes to win.";
            AddResetButton(s, resetSetup, () => BuildMap(s, setup, startLabel, start, resetSetup));
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

        /// <summary>The label of the setup screens' Default button (UR-T41, the original's obj_btn_default).</summary>
        public const string ResetSetupLabel = "Reset to defaults";

        // UR-T41: the setup screens' Default button, when the screen is given a reset: it puts every choice of the setup
        // (players, map and settings rooms alike) back to the defaults, then rebuilds the screen so it shows them.
        private static void AddResetButton(MenuScreen s, Action resetSetup, Action rebuild)
        {
            if (resetSetup == null)
            {
                return;
            }

            s.AddButton(ResetSetupLabel, () =>
            {
                resetSetup();
                s.Clear();
                rebuild();
            }, ButtonVariant.Danger);
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

        /// <summary>
        /// The rebind screen (UR-T36): one row per control the RicochetControls asset binds (a composite action's
        /// keyboard half and its gamepad half as separate rows, since each is its own control to press), the button
        /// showing what triggers it now and starting an interactive rebind on click, and a Reset to defaults. A
        /// change is saved to the active Unity.Shared profile the moment it completes, so it is kept for every
        /// session (<see cref="InputBindings"/>).
        /// </summary>
        // The one rebind in progress, if any: StartRebind cancels it before starting another (two rows pending at
        // once would race for which one re-enables the map), and leaving the screen any way at all - Back, closing
        // the in-game menu, navigating elsewhere - cancels it too, so the Gameplay map can never come back disabled.
        private static InputActionRebindingExtensions.RebindingOperation _activeRebind;

        public static void BuildRebind(MenuScreen s)
        {
            s.Hidden -= CancelActiveRebind; // BuildRebind re-runs itself (Reset to defaults), so guard against a
            s.Hidden += CancelActiveRebind; // second subscription to the same screen's Hidden event.

            s.Subtitle = "Click a control, then press, click or move the one you want instead. Esc cancels.";
            s.AddButton("Reset to defaults", () =>
            {
                InputBindings.ResetToDefaults();
                s.Clear();
                BuildRebind(s);
            }, ButtonVariant.Danger);

            foreach (InputAction action in InputBindings.Gameplay.actions)
            {
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    if (action.bindings[i].isComposite)
                    {
                        continue; // the composite's own entry carries no control; its parts (below) are what rebinds
                    }

                    int bindingIndex = i;
                    MaRow row = s.AddRow(equalWidths: false);
                    row.AddText(action.name, TextStyle.Label);
                    row.AddFlexibleSpace();
                    MaButton button = null;
                    button = row.AddButton(DescribeBinding(action, bindingIndex), () => StartRebind(action, bindingIndex, button));
                }
            }
        }

        private static string DescribeBinding(InputAction action, int bindingIndex) =>
            action.bindings[bindingIndex].ToDisplayString(InputBinding.DisplayStringOptions.DontIncludeInteractions);

        // Cancelling fires the pending operation's own OnCancel callback (synchronously), which restores its
        // button's label and re-enables its map - so this alone is enough to undo whatever StartRebind did.
        private static void CancelActiveRebind() => _activeRebind?.Cancel();

        private static void StartRebind(InputAction action, int bindingIndex, MaButton button)
        {
            CancelActiveRebind();

            InputActionMap map = action.actionMap;
            map.Disable();
            button.Label = "Press a control...";
            _activeRebind = action.PerformInteractiveRebinding(bindingIndex)
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnComplete(op =>
                {
                    _activeRebind = null;
                    op.Dispose();
                    InputBindings.Save();
                    button.Label = DescribeBinding(action, bindingIndex);
                    map.Enable();
                })
                .OnCancel(op =>
                {
                    _activeRebind = null;
                    op.Dispose();
                    button.Label = DescribeBinding(action, bindingIndex);
                    map.Enable();
                })
                .Start();
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

        /// <summary>
        /// The kick room (UR-T14), off the in-game menu: a button for each player this peer may vote to kick (Core's
        /// <see cref="VoteMenu.KickTargets"/>: not themselves, not the host, not an AI), each starting that vote through
        /// <paramref name="startKick"/> with their client id; or a line saying there is nobody.
        /// </summary>
        public static void BuildKick(MenuScreen s, IEnumerable<VoteMember> roster, Action<ulong> startKick)
        {
            IReadOnlyList<VoteMember> targets = VoteMenu.KickTargets(roster);
            s.Subtitle = targets.Count == 0 ? VoteMenu.NobodyToKick : VoteMenu.KickSubtitle;
            foreach (VoteMember target in targets)
            {
                ulong clientId = target.ClientId;
                s.AddButton(VoteMenu.KickButton(target.Name), () => startKick?.Invoke(clientId), ButtonVariant.Danger);
            }
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
