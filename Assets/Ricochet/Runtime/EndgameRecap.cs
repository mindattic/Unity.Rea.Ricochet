// The endgame room (UR-T9) on every peer, read from the synced state only (Turns and each Player): once the game's goal is
// reached (MatchPhase.GameOver) a MindAttic Components screen comes up over the game with the champion's name over
// "wins!" and the leaderboard, every seat ranked by score (obj_endgame's ds_grid_sort of pScore and its "1st Ann 740"
// rows, in each player's colour on the original's pedestal; here a line each). Its buttons are the endgame room's way out
// and then some: vote for a rematch (the restart vote: the same players, map and goal from round one), the main menu
// (leave the game), or close it to look at the level. It comes up again for the next game that ends. While it is up the
// endgame room's camera flashes go off over it (EndgameFlashes, U.RICO-T79).
using System.Collections.Generic;
using MindAttic.Components.Unity;       // MenuStack, MenuScreen, MaText, TextStyle
using MindAttic.Components.Unity.Core; // ButtonVariant
using Ricochet.Core.Level;
using Ricochet.Core.Match;
using Ricochet.Core.Menu;
using Unity.Multiplayer;                // NetSession, NetVotes
using Unity.Multiplayer.UI;             // NetFrontEnd
using UnityEngine;

namespace Ricochet
{
    public sealed class EndgameRecap : MonoBehaviour
    {
        private MatchHud _hud;
        private EndgameFlashes _flashes;
        private MenuStack _stack;
        private MenuScreen _screen;
        private MatchPhase _lastPhase = MatchPhase.Practice;
        private Turns _lastTurns;
        private readonly List<string> _lines = new List<string>();

        /// <summary>Adds the recap to <paramref name="hud"/>'s GameObject; it shows whatever game the HUD's session ends.</summary>
        public static EndgameRecap Create(MatchHud hud)
        {
            var recap = hud.gameObject.AddComponent<EndgameRecap>();
            recap._hud = hud;
            recap._flashes = hud.gameObject.AddComponent<EndgameFlashes>(); // U.RICO-T79: obj_flashemitter's camera flashes
            return recap;
        }

        /// <summary>True while the recap is up.</summary>
        public bool Showing => _stack != null && _stack.IsOpen && _stack.gameObject.activeInHierarchy;

        /// <summary>"Ann wins!" ("" before a game has ended).</summary>
        public string Headline { get; private set; } = string.Empty;

        /// <summary>The leaderboard, a line each, first place first.</summary>
        public IReadOnlyList<string> Lines => _lines;

        /// <summary>The recap's screen (null until a game has ended).</summary>
        public MenuScreen Screen => _screen;

        /// <summary>The endgame room's camera flashes, thrown while the recap is up.</summary>
        public EndgameFlashes Flashes => _flashes;

        /// <summary>Closes the recap (the game stays over until a new match starts).</summary>
        public void Close()
        {
            if (_flashes != null)
            {
                _flashes.End();
            }

            if (_stack != null)
            {
                _stack.Close();
            }
        }

        private void OnDestroy()
        {
            if (_stack != null)
            {
                Destroy(_stack.transform.root.gameObject);
            }
        }

        private void Update()
        {
            NetSession session = _hud != null ? _hud.CurrentSession : null;
            Turns turns = session != null && session.IsLive ? Turns.For(session) : null;
            MatchPhase phase = turns != null ? turns.MatchPhase : MatchPhase.Practice;
            if (turns != _lastTurns)
            {
                _lastTurns = turns;
                _lastPhase = MatchPhase.Practice;
            }

            if (phase == MatchPhase.GameOver && _lastPhase != MatchPhase.GameOver)
            {
                Show(session, turns);
            }
            else if (phase != MatchPhase.GameOver && Showing)
            {
                Close(); // a rematch or a new match started
            }

            _lastPhase = phase;
        }

        private void Show(NetSession session, Turns turns)
        {
            var entries = new List<RecapEntry>();
            var colours = new Dictionary<int, Color>();
            foreach (Player player in Player.In(session))
            {
                if (player.Seat >= 0)
                {
                    entries.Add(new RecapEntry(player.Seat, player.DisplayName, player.Score, player.IsAi));
                    colours[player.Seat] = player.Color;
                }
            }

            List<RecapEntry> ranked = Recap.Rank(entries);
            Headline = Recap.Headline(ranked);
            _lines.Clear();
            for (int i = 0; i < ranked.Count; i++)
            {
                _lines.Add(Recap.Line(i + 1, ranked[i]));
            }

            if (_stack == null)
            {
                // Over the HUD's cards (10), under the kit's menus and dialogs.
                _stack = MenuStack.Create(null, backdrop: true, sortingOrder: 90);
                _stack.name = "Endgame";
                _stack.AllowCloseRoot = true;
                _screen = _stack.CreateScreen(MenuFlow.Title(Room.Endgame));
                _stack.Push(_screen);
            }

            _screen.Clear();
            _screen.Title = Headline;
            _screen.Subtitle = Maps.At(turns.MapIndex).Name + ", " + MatchSetup.DescribeGoal(turns.GoalPoints).ToLowerInvariant() + ", "
                + turns.Round + " round" + (turns.Round == 1 ? string.Empty : "s");
            for (int i = 0; i < ranked.Count; i++)
            {
                MaText line = _screen.AddText(_lines[i], i == 0 ? TextStyle.Heading : TextStyle.Body);
                if (colours.TryGetValue(ranked[i].Seat, out Color colour))
                {
                    line.WithColor(colour);
                }
            }

            _screen.AddSpace();
            if (NetVotes.Instance != null && NetVotes.Instance.Board != null)
            {
                _screen.AddButton("Vote for a rematch", () =>
                {
                    NetVotes.Instance.Board.StartVote(Arena.RestartVote);
                    Close();
                }, ButtonVariant.Primary);
            }

            if (NetFrontEnd.Instance != null && NetFrontEnd.Instance.HasMenus)
            {
                _screen.AddButton(MenuFlow.Title(MenuFlow.Next(Room.Endgame)), () =>
                {
                    Close();
                    NetFrontEnd.Instance.ConfirmLeave();
                });
            }

            _screen.AddButton("Close", Close);
            _stack.Open();
            _screen.MarkNavigationDirty();
            if (_flashes != null)
            {
                _flashes.Begin();
            }
        }
    }
}
