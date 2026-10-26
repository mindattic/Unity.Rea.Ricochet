// The pause menu (UR-T43: obj_pausemenu, obj_btn_resume, obj_btn_randomize, obj_btn_music, obj_btn_quit_round, isPaused) on
// every peer, read from the synced state only (Turns). The original's Esc froze its one machine's game (the clock, the clouds,
// the shots) under "Paused" with Resume, Randomize, Next Track and Setup. Online the kit's Esc menu stays (invite, settings,
// votes, leave), so the Pause key (P or Pause, the right stick's press, or the in-game menu's "Pause the game") pauses: the
// host pauses at once, anyone else asks everyone by a vote, and the server's pause freezes every peer's clock, Ready card, AI
// and shots. While it is paused this menu is up on every peer: Resume (the host's at once, anyone else's a vote), Randomize the
// level (the host's, before the round's first shot: a fresh random level, the turn started over), Music (the player's own
// profile mute, through GameAudio, which keeps a session mute silent) and Quit round (always a vote; the match ends and
// everyone goes to the in-game menu, as the original's Setup button went to the players room). A MindAttic Components
// screen over the HUD, under the endgame recap, the kit's menus and the vote overlay (so a vote can be answered from it).
using MindAttic.Components.Unity;       // MenuStack, MenuScreen
using MindAttic.Components.Unity.Core; // ButtonVariant
using Ricochet.Core.Match;
using Ricochet.Core.Menu;
using Unity.Multiplayer;                // NetSession, VoteBoard, NetBots
using Unity.Multiplayer.UI;             // NetFrontEnd, FrontEndState
using UnityEngine;

namespace Ricochet
{
    public sealed class PauseMenu : MonoBehaviour
    {
        /// <summary>The menu's canvas order: over the HUD's cards (10), under the endgame recap (90), the kit's menus and votes.</summary>
        public const int SortingOrder = 89;

        private MatchHud _hud;
        private MenuStack _stack;
        private MenuScreen _screen;
        private Turns _lastTurns;
        private int _lastQuits;
        private int _built = -1;

        /// <summary>Adds the pause menu to <paramref name="hud"/>'s GameObject; it follows whatever match the HUD's session plays.</summary>
        public static PauseMenu Create(MatchHud hud)
        {
            var menu = hud.gameObject.AddComponent<PauseMenu>();
            menu._hud = hud;
            return menu;
        }

        /// <summary>True while the menu is up.</summary>
        public bool Showing => _stack != null && _stack.IsOpen && _stack.gameObject.activeInHierarchy;

        /// <summary>The menu's screen (null until the first pause).</summary>
        public MenuScreen Screen => _screen;

        /// <summary>True when the menu offers Randomize now (the host, before the round's first shot).</summary>
        public bool OffersRandomize { get; private set; }

        /// <summary>How many quit rounds this peer has seen (each sends its player to the in-game menu).</summary>
        public int QuitsSeen { get; private set; }

        /// <summary>True while a match is being played and not over: what may be paused.</summary>
        public static bool InMatch(Turns turns) =>
            turns != null && turns.MatchPhase != MatchPhase.Practice && turns.MatchPhase != MatchPhase.GameOver;

        /// <summary>The Pause key: pauses, or resumes when paused (the host at once, anyone else by a vote).</summary>
        public static bool TogglePause(NetSession session)
        {
            Turns turns = LiveTurns(session);
            if (!InMatch(turns))
            {
                return false;
            }

            return turns.IsPaused ? Resume(session) : Pause(session);
        }

        /// <summary>Pauses the match: the host at once, anyone else starts the pause vote.</summary>
        public static bool Pause(NetSession session)
        {
            Arena arena = HostArena(session);
            return arena != null ? arena.SetPaused(true) : StartVote(session, Arena.PauseVote);
        }

        /// <summary>obj_btn_resume: the host at once, anyone else starts the resume vote.</summary>
        public static bool Resume(NetSession session)
        {
            Arena arena = HostArena(session);
            return arena != null ? arena.SetPaused(false) : StartVote(session, Arena.ResumeVote);
        }

        /// <summary>obj_btn_randomize: the host only, before the round's first shot.</summary>
        public static bool Randomize(NetSession session)
        {
            Arena arena = HostArena(session);
            return arena != null && arena.RerollRound();
        }

        /// <summary>obj_btn_quit_round: always a vote, the host's too.</summary>
        public static bool QuitRound(NetSession session) => StartVote(session, Arena.QuitRoundVote);

        /// <summary>obj_btn_music: this player's music on or off (their profile's mute; a session mute keeps it silent).</summary>
        public static bool SetMusic(bool on)
        {
            GameAudio audio = GameAudio.Instance;
            return audio != null && audio.SetMusicMuted(!on);
        }

        // The arena this peer serves, when it is the host of the session (null on a client).
        private static Arena HostArena(NetSession session)
        {
            Arena arena = Arena.Current;
            return session != null && session.IsServer && arena != null && arena.Serves(session) ? arena : null;
        }

        private static bool StartVote(NetSession session, string kind)
        {
            VoteBoard board = session != null ? VoteBoard.For(session) : null;
            if (board == null || !board.IsSpawned)
            {
                return false;
            }

            board.StartVote(kind);
            return true;
        }

        private static Turns LiveTurns(NetSession session) => session != null && session.IsLive ? Turns.For(session) : null;

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
            Turns turns = LiveTurns(session);
            if (turns != _lastTurns)
            {
                _lastTurns = turns;
                _lastQuits = turns != null ? turns.QuitCount : 0;
            }

            if (turns != null && turns.QuitCount != _lastQuits)
            {
                _lastQuits = turns.QuitCount;
                QuitsSeen++;
                ToTheInGameMenu();
            }

            if (PausePressed(session))
            {
                TogglePause(session);
            }

            if (InMatch(turns) && turns.IsPaused)
            {
                Show(session);
            }
            else if (Showing)
            {
                _stack.Close();
            }
        }

        // The Pause key, read only by a peer with a player at the keyboard, and not while the kit's in-game menu is open.
        private static bool PausePressed(NetSession session)
        {
            if (session == null || !session.IsClient || NetBots.IsBot)
            {
                return false;
            }

            NetFrontEnd frontEnd = NetFrontEnd.Instance;
            if (frontEnd != null && frontEnd.PauseMenu != null && frontEnd.PauseMenu.IsOpen)
            {
                return false;
            }

            return GameInput.Pause();
        }

        private void Show(NetSession session)
        {
            Arena host = HostArena(session);
            bool randomize = host != null && host.MayRerollRound;
            int built = (host != null ? 1 : 0) | (randomize ? 2 : 0);
            if (_stack == null)
            {
                _stack = MenuStack.Create(null, backdrop: true, sortingOrder: SortingOrder);
                _stack.name = "Pause menu";
                _stack.EscapeEnabled = false; // Esc stays the kit's in-game menu; only Resume (or a vote) closes this one
                _stack.AllowCloseRoot = false;
                _screen = _stack.CreateScreen(VoteMenu.PausedTitle);
                _stack.Push(_screen);
                // obj_pausemenu drew "Paused" in fnt_mono outlined 2 px (its spr_pausemenu and spr_pausemenu_back draws are
                // commented out, so no panel sprite either; U.RICO-T78). The buttons are dressed by GmlMenuSkin.
                TMPro.TMP_Text title = GmlMenuSkin.TitleOf(_screen);
                if (title != null)
                {
                    GmlFontLabel.Attach(title, Ricochet.Core.Art.MenuLooks.ButtonFont, Ricochet.Core.Art.MenuLooks.ButtonOutline);
                }
            }

            if (built != _built || !Showing)
            {
                _built = built;
                Build(session, host != null, randomize);
            }

            if (!Showing)
            {
                _stack.Open();
                _screen.MarkNavigationDirty();
            }
        }

        private void Build(NetSession session, bool host, bool randomize)
        {
            OffersRandomize = randomize;
            _screen.Clear();
            _screen.Title = VoteMenu.PausedTitle;
            _screen.Subtitle = host ? VoteMenu.PausedForHost : VoteMenu.PausedForGuest;
            _screen.AddButton(host ? VoteMenu.ResumeButton : VoteMenu.ResumeVoteButton, () => Resume(session), ButtonVariant.Primary);
            if (randomize)
            {
                _screen.AddButton(VoteMenu.RandomizeButton, () => Randomize(session));
            }

            GameAudio audio = GameAudio.Instance;
            _screen.AddToggle(VoteMenu.MusicToggle, audio == null || audio.MusicOn, on => SetMusic(on));
            _screen.AddButton(VoteMenu.QuitRoundButton, () => QuitRound(session), ButtonVariant.Danger);
            _screen.MarkNavigationDirty();
        }

        // obj_btn_quit_round's room_goto(players): the round is over for everyone, so each player lands on the in-game menu,
        // whose Players and Map rooms start the next match.
        private static void ToTheInGameMenu()
        {
            NetFrontEnd frontEnd = NetFrontEnd.Instance;
            if (frontEnd != null && frontEnd.HasMenus && frontEnd.State == FrontEndState.InGame && frontEnd.PauseMenu != null && !frontEnd.PauseMenu.IsOpen)
            {
                frontEnd.PauseMenu.Open();
            }
        }
    }
}
