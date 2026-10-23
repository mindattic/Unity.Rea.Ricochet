// The host's lobby (UR-T11), in place of the original's multiplayer_hostorjoin/host/join rooms. The kit's front end
// (Unity.Multiplayer NetFrontEnd) does the networking part: Host, Join by IP and Join by code on the main menu, and for
// the host a lobby with the join code (or the address to join at), the live roster (names, colours, Host tag, remove) and
// Leave / Start. This adds the game's part to that lobby screen: a Match box saying what Start will begin (the map and the
// goal, how the sixteen seats fill as people join, the AI and the announcer, from Core's Lobby), with Start held back while
// a match could not seat two; and it carries the map room's setup through the lobby, so the match starts with everyone
// who joined when the host presses Start. Hosting from the main menu's Host button (no setup chosen) opens the same lobby
// for free practice. Clients go straight into the game when they join, as the kit does.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MindAttic.Components.Unity; // MenuScreen, MaText, MaButton, MaRow
using Ricochet.Core.Match;
using Ricochet.Core.Menu;
using Unity.Multiplayer;          // NetSession
using Unity.Multiplayer.Core;     // NetPlayer, JoinCode, Disconnection
using Unity.Multiplayer.UI;       // NetFrontEnd, FrontEndState
using UnityEngine;

namespace Ricochet
{
    public sealed class LobbyRoom : IDisposable
    {
        private readonly NetFrontEnd _frontEnd;
        private readonly MonoBehaviour _runner;
        private readonly Func<MatchSetup, bool> _startMatch;
        private NetSession _session;
        private MatchSetup _pending;
        private MenuScreen _screen;
        private MaText _seats;
        private readonly List<MaText> _lines = new List<MaText>();
        private bool _disposed;

        private LobbyRoom(NetFrontEnd frontEnd, MonoBehaviour runner, Func<MatchSetup, bool> startMatch)
        {
            _frontEnd = frontEnd;
            _runner = runner;
            _startMatch = startMatch;
            _frontEnd.StateChanged += OnStateChanged;
            _frontEnd.GameStarted += OnGameStarted;
            _frontEnd.Left += OnLeft;
            HookSession();
        }

        /// <summary>
        /// The lobby on <paramref name="frontEnd"/>: <paramref name="runner"/> runs its coroutines, and
        /// <paramref name="startMatch"/> (the server's Arena.StartMatch) starts the match a <see cref="HostMatch"/> carried
        /// once the host presses Start.
        /// </summary>
        public static LobbyRoom Attach(NetFrontEnd frontEnd, MonoBehaviour runner, Func<MatchSetup, bool> startMatch)
        {
            if (frontEnd == null)
            {
                throw new ArgumentNullException(nameof(frontEnd));
            }

            if (runner == null)
            {
                throw new ArgumentNullException(nameof(runner));
            }

            return new LobbyRoom(frontEnd, runner, startMatch ?? throw new ArgumentNullException(nameof(startMatch)));
        }

        /// <summary>The match Start will begin: the map room's setup, while hosting from it; null for free practice.</summary>
        public MatchSetup Pending => _pending;

        /// <summary>The lobby screen this box is on (null when no lobby is showing).</summary>
        public MenuScreen Screen => _screen != null && _frontEnd != null && _frontEnd.State == FrontEndState.Lobby ? _screen : null;

        /// <summary>What the Match box says, a line each (empty when no lobby is showing).</summary>
        public IReadOnlyList<string> Lines => Screen == null ? Array.Empty<string>() : _lines.Where(t => t != null).Select(t => t.Text).ToArray();

        /// <summary>People in the lobby who would take a seat (the host included; a held seat does not count).</summary>
        public int People => _session == null ? 0 : _session.Roster.All.Count(p => p.IsConnected);

        /// <summary>True when Start would begin a match now (always, for free practice).</summary>
        public bool CanStart => _pending == null || Lobby.CanStart(People, _pending.AiCount);

        /// <summary>
        /// The map room's Start: hosts a game carrying <paramref name="setup"/>, opening the lobby (the join code and who
        /// is in); the host's Start there begins the match with everyone who joined. When the front end is set to skip
        /// the lobby, the match begins at once.
        /// </summary>
        /// <returns>True when the game is hosted.</returns>
        public async Task<bool> HostMatch(MatchSetup setup)
        {
            if (_frontEnd.State != FrontEndState.Menu)
            {
                return false;
            }

            _pending = (setup ?? new MatchSetup()).Clone();
            bool hosted = await _frontEnd.HostAsync();
            if (!hosted)
            {
                _pending = null;
            }

            return hosted;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            UnhookSession();
            if (_frontEnd != null)
            {
                _frontEnd.StateChanged -= OnStateChanged;
                _frontEnd.GameStarted -= OnGameStarted;
                _frontEnd.Left -= OnLeft;
            }
        }

        // ---- The front end's events -----------------------------------------------------------------------------------------

        private void OnStateChanged(FrontEndState state)
        {
            HookSession();
            if (state == FrontEndState.Lobby)
            {
                Later(Refresh); // the kit builds the lobby screen just after it changes state
            }
            else
            {
                Forget();
            }
        }

        // The host pressed Start (or there is no lobby): the setup the lobby carried starts as soon as the host has a player.
        private void OnGameStarted()
        {
            MatchSetup setup = _pending;
            _pending = null;
            Forget();
            if (setup != null && _session != null && _session.IsServer && Alive)
            {
                _runner.StartCoroutine(StartWhenSeated(setup));
            }
        }

        private void OnLeft(Disconnection why)
        {
            _pending = null;
            Forget();
        }

        private void OnPlayersChanged(NetPlayer player)
        {
            Refresh();
        }

        // A code published after the lobby opened makes the kit refill the screen, clearing this box: put it back.
        private void OnJoinCodePublished(JoinCode code)
        {
            Later(Refresh);
        }

        // ---- The Match box ----------------------------------------------------------------------------------------------------

        /// <summary>Adds the Match box to the lobby showing (once; again after the kit refilled it) and brings it up to date.</summary>
        public void Refresh()
        {
            if (_disposed || _frontEnd == null || _frontEnd.State != FrontEndState.Lobby || _frontEnd.MainMenu == null)
            {
                return;
            }

            MenuScreen lobby = _frontEnd.MainMenu.Current;
            if (lobby == null || lobby.Title != MenuFlow.Title(Room.Lobby))
            {
                return;
            }

            if (lobby != _screen || _seats == null)
            {
                Build(lobby);
            }

            if (_pending != null)
            {
                _seats.Text = Lobby.SeatLine(People, _pending.AiCount);
            }

            MaButton start = StartButton(lobby);
            if (start != null)
            {
                start.Interactable = CanStart;
            }
        }

        private void Build(MenuScreen lobby)
        {
            _screen = lobby;
            _lines.Clear();
            lobby.AddSection("Match");
            if (_pending == null)
            {
                _seats = Line(lobby, Lobby.PracticeLine, TextStyle.Body);
            }
            else
            {
                IReadOnlyList<string> plan = Lobby.Plan(_pending, People);
                Line(lobby, plan[0], TextStyle.Label);
                _seats = Line(lobby, plan[1], TextStyle.Body);
                for (int i = 2; i < plan.Count; i++)
                {
                    Line(lobby, plan[i], TextStyle.Muted);
                }
            }

            Line(lobby, Lobby.JoinLine, TextStyle.Muted);

            // Leave / Start stay at the bottom, under the box.
            MaRow buttons = lobby.GetComponentsInChildren<MaRow>(true).FirstOrDefault(r => r.GetComponentsInChildren<MaButton>(true).Any(b => b.Label == "Start"));
            if (buttons != null)
            {
                buttons.transform.SetAsLastSibling();
            }

            lobby.MarkNavigationDirty();
        }

        private MaText Line(MenuScreen lobby, string text, TextStyle style)
        {
            MaText line = lobby.AddText(text, style);
            _lines.Add(line);
            return line;
        }

        private static MaButton StartButton(MenuScreen lobby)
        {
            return lobby.GetComponentsInChildren<MaButton>(true).FirstOrDefault(b => b.Label == "Start");
        }

        private void Forget()
        {
            _screen = null;
            _seats = null;
            _lines.Clear();
        }

        // ---- Plumbing ----------------------------------------------------------------------------------------------------------

        private bool Alive => !_disposed && _runner != null && _runner.isActiveAndEnabled;

        private IEnumerator StartWhenSeated(MatchSetup setup)
        {
            // A player spawns a frame or so after its connection (the host's own, straight after the host starts): wait
            // until everyone in the lobby has one, so the match seats them all.
            for (int frame = 0; frame < 300 && _session != null && _session.IsServer && Humans(_session) < Math.Max(1, People); frame++)
            {
                yield return null;
            }

            if (_session == null || !_session.IsServer)
            {
                yield break;
            }

            if (!_startMatch(setup))
            {
                Debug.Log("[Lobby] A match needs two players: add an AI opponent on the Players screen, or wait for someone to join.");
            }
        }

        private static int Humans(NetSession session) => Player.In(session).Count(p => !p.IsAi);

        private void Later(Action action)
        {
            if (Alive)
            {
                _runner.StartCoroutine(NextFrame(action));
            }
        }

        private static IEnumerator NextFrame(Action action)
        {
            yield return null;
            action();
        }

        private void HookSession()
        {
            NetSession session = _frontEnd != null ? _frontEnd.Session : null;
            if (session == _session)
            {
                return;
            }

            UnhookSession();
            _session = session;
            if (_session != null)
            {
                _session.Roster.Joined += OnPlayersChanged;
                _session.Roster.Left += OnPlayersChanged;
                _session.JoinCodePublished += OnJoinCodePublished;
            }
        }

        private void UnhookSession()
        {
            if (_session == null)
            {
                return;
            }

            _session.Roster.Joined -= OnPlayersChanged;
            _session.Roster.Left -= OnPlayersChanged;
            _session.JoinCodePublished -= OnJoinCodePublished;
            _session = null;
        }
    }
}
