// Arena, the pause menu (UR-T43: obj_pausemenu, obj_btn_resume, obj_btn_randomize, obj_btn_quit_round, isPaused). The original
// froze its one machine's game; here the server's authority pauses the match for every peer (Turns mirrors it), so every
// clock, Ready card, AI and shot waits. The host pauses, resumes and randomizes at once (it runs this Arena); anyone else asks
// everyone by the pause and resume votes, and quitting the round is always a vote. Randomize lays the round again on a fresh
// random level (the level generator's) before its first shot; Quit round stops the match and goes back to practice.
using Ricochet.Core.Menu;
using Unity.Multiplayer;      // NetSession
using Unity.Multiplayer.Core; // VoteKind

namespace Ricochet
{
    public sealed partial class Arena
    {
        public const string PauseVote = "pause";
        public const string ResumeVote = "resume";
        public const string QuitRoundVote = "quitround";

        /// <summary>True while the match is paused (server only).</summary>
        public bool Paused => _authority.Paused;

        /// <summary>The pause menu's Randomize may run now: a turn has begun and nobody has fired this round (server only).</summary>
        public bool MayRerollRound => _authority.MayRerollRound;

        /// <summary>True when this arena serves <paramref name="session"/> (its host's pause menu acts on it directly).</summary>
        public bool Serves(NetSession session) => session != null && _session == session;

        /// <summary>Server: pauses or resumes the match for everyone. False when nothing changed (no match, or already so).</summary>
        public bool SetPaused(bool paused)
        {
            bool changed = _authority.SetPaused(paused).Count > 0;
            Mirror();
            return changed;
        }

        /// <summary>
        /// Server: the pause menu's Randomize: unpaused, the round is laid again on a fresh random level and the current turn
        /// starts over with its Ready card. False (and nothing changes) once anyone has fired this round.
        /// </summary>
        public bool RerollRound()
        {
            bool rolled = _authority.RerollRound().Count > 0;
            Mirror();
            return rolled;
        }

        /// <summary>
        /// Server: Quit round: the match stops, the AI opponents leave, the practice dummies stand back up and the level's
        /// hazards are laid again; every peer's pause menu sends its player to the in-game menu (the original's players room).
        /// </summary>
        public bool QuitRound()
        {
            if (_authority.QuitMatch().Count == 0)
            {
                return false;
            }

            _aiTurn = null;
            RemoveAi();
            _humanSeats.Clear();
            if (!_dummiesUp)
            {
                SpawnDummies();
            }

            ResetHazards();
            if (_turns != null && _turns.IsSpawned)
            {
                _turns.CountQuit();
            }

            Mirror();
            return true;
        }

        /// <summary>The pause vote: anyone but the host asks everyone to pause.</summary>
        public VoteKind CreatePauseVote() => new VoteKind(PauseVote, VoteMenu.PauseTitle)
        {
            Description = _ => VoteMenu.PauseDescription,
            Check = _ => _authority.Paused ? VoteMenu.AlreadyPaused : !_authority.MayPause ? VoteMenu.NothingToPause : null,
            Passed = vote => RouteVote(vote, _ => SetPaused(true)),
        };

        /// <summary>The resume vote: anyone but the host asks everyone to carry on.</summary>
        public VoteKind CreateResumeVote() => new VoteKind(ResumeVote, VoteMenu.ResumeTitle)
        {
            Description = _ => VoteMenu.ResumeDescription,
            Check = _ => _authority.Paused ? null : VoteMenu.NotPaused,
            Passed = vote => RouteVote(vote, _ => SetPaused(false)),
        };

        /// <summary>The quit-round vote (obj_btn_quit_round): everyone, the host included, asks everyone.</summary>
        public VoteKind CreateQuitRoundVote() => new VoteKind(QuitRoundVote, VoteMenu.QuitRoundTitle)
        {
            Description = _ => VoteMenu.QuitRoundDescription,
            Check = _ => MatchRunning ? null : VoteMenu.NothingToQuit,
            Passed = vote => RouteVote(vote, _ => QuitRound()),
        };
    }
}
