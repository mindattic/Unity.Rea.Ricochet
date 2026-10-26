// The pause menu (U.RICO-T43: obj_pausemenu, obj_btn_resume, obj_btn_randomize, obj_btn_music, obj_btn_quit_round, isPaused): a
// host and one client, Ann. The host's pause freezes every peer's turn clock (and the shots and the AI) and puts the menu up on
// her client; the AI waits out a pause; Ann's pause and resume are votes; Randomize is the host's alone and only before the
// round's first shot; Quit round is a vote that ends the match back to practice on every peer; and the Music toggle, under the
// CLI session mute, stays silent and saves only the player's own values.
// The pause rules themselves are tested without Unity: dotnet test Core.slnx (Tests/Core.Tests/PauseTests.cs).
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Ricochet.Core.Match;
using Unity.Multiplayer;
using Unity.Multiplayer.Core;
using Unity.Multiplayer.Testing;
using Unity.Shared;
using Unity.Shared.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ricochet.Tests
{
    public class PauseMenuTests : NetSessionTest
    {
        private double _endTurnSeconds;
        private double _aiThinkSeconds;
        private MatchHud _hud;

        public PauseMenuTests() : base(Topology.Host, clients: 1) { }

        protected override void OnSessionsCreated()
        {
            SetPlayerPrefab(GamePrefabs.Player);
            RegisterPrefab(GamePrefabs.Dummy);
            RegisterPrefab(GamePrefabs.Opponent);
            RegisterPrefab(GamePrefabs.Turns);
            RegisterPrefab(GamePrefabs.Crate);
            foreach (NetSession session in Sessions)
            {
                VoteBoard.RegisterPrefab(session);                // what NetVotes does in the scene
            }

            Client.Identity = new PlayerIdentity("ann-token", "Ann", new PlayerColor(230, 80, 60));
        }

        [SetUp]
        public void KeepTheRules()
        {
            _endTurnSeconds = Arena.MatchRules.EndTurnSeconds;
            _aiThinkSeconds = Arena.MatchRules.AiThinkSeconds;
            TestMatches.WithoutReadyCard();
        }

        [TearDown]
        public void PutTheRulesBack()
        {
            if (_hud != null)
            {
                Object.Destroy(_hud.gameObject);
                _hud = null;
            }

            Arena.MatchRules.EndTurnSeconds = _endTurnSeconds;
            Arena.MatchRules.AiThinkSeconds = _aiThinkSeconds;
            TestMatches.StockReadyCard();
        }

        [UnityTest]
        public IEnumerator TheHostsPause_FreezesEveryPeersClockAndShots_AndPutsTheMenuUpOnEveryPeer()
        {
            Arena arena = null;
            Player host = null, ann = null;
            VoteBoard board = null;
            yield return Begin((a, b) => { arena = a; board = b; }, (h, c) => { host = h; ann = c; });
            _hud = MatchHud.Create(Client);
            Assert.That(arena.StartMatch(TestMatches.OnThePracticeRange()), Is.True);
            Turns turns = null;
            yield return WaitUntil(() => (turns = Turns.For(Client)) != null && turns.MatchPhase == MatchPhase.Aiming && turns.Current == host.Seat,
                "the client never saw the host's turn");
            Assert.That(_hud.Pause.Showing, Is.False);

            Assert.That(PauseMenu.Pause(Server), Is.True, "the host pauses at once");
            Assert.That(arena.Paused, Is.True);
            Assert.That(board.IsVoteOpen, Is.False, "no vote for the host");
            yield return WaitUntil(() => turns.IsPaused, "the client never saw the pause");
            yield return WaitUntil(() => _hud.Pause.Showing, "the pause menu never came up on the client");
            Assert.That(_hud.Pause.OffersRandomize, Is.False, "Randomize is the host's");

            double serverClock = arena.Match.TimeLeft;
            float shownClock = turns.ShownTimeLeft;
            host.ServerFire(0f, 0f);                              // refused while paused
            yield return new WaitForSeconds(1f);
            Assert.That(arena.Match.TimeLeft, Is.EqualTo(serverClock).Within(1e-6), "the server's clock waits");
            Assert.That(turns.ShownTimeLeft, Is.EqualTo(shownClock).Within(0.01f), "the client's clock waits");
            Assert.That(host.Shots, Is.EqualTo(0), "nobody fires while paused");
            Assert.That(Turns.MayFire(Server, host.Seat), Is.False);

            Assert.That(PauseMenu.Resume(Server), Is.True, "the host resumes at once");
            yield return WaitUntil(() => !turns.IsPaused, "the client never saw the game resume");
            yield return WaitUntil(() => !_hud.Pause.Showing, "the pause menu stayed up");
            yield return WaitUntil(() => arena.Match.TimeLeft < serverClock - 0.3, "the clock never ran again");
            host.ServerFire(0f, 0f);
            yield return WaitUntil(() => host.Shots == 1, "the host could not fire after the pause");
        }

        [UnityTest]
        public IEnumerator TheAiWaitsOutAPause()
        {
            Arena.MatchRules.EndTurnSeconds = 0.2;
            Arena.MatchRules.AiThinkSeconds = 1.0;
            Arena arena = null;
            Player host = null, ann = null;
            yield return Begin((a, _) => arena = a, (h, c) => { host = h; ann = c; });
            Assert.That(arena.StartMatch(TestMatches.OnThePracticeRange(aiCount: 1)), Is.True);
            Player ai = arena.PlayerAt(2);
            Assert.That(ai.IsAi, Is.True);

            host.ServerFire(0f, 0f);                              // a short shot at nobody
            yield return WaitUntil(() => arena.Match.Current == ann.Seat && arena.Match.Phase == MatchPhase.Aiming, "the turn never passed to Ann", 10f);
            ann.ServerFire(0f, 0f);
            yield return WaitUntil(() => arena.Match.Current == ai.Seat && arena.Match.Phase == MatchPhase.Aiming, "the turn never passed to the AI", 10f);
            Assert.That(arena.SetPaused(true), Is.True);          // before it has finished thinking

            yield return new WaitForSeconds(2.5f);
            Assert.That(ai.Shots, Is.EqualTo(0), "the AI fired through the pause");
            Assert.That(arena.Match.Current, Is.EqualTo(ai.Seat), "the AI's turn is still on");

            Assert.That(arena.SetPaused(false), Is.True);
            yield return WaitUntil(() => ai.Shots >= 1, "the AI never fired after the pause", 15f);
        }

        [UnityTest]
        public IEnumerator AGuestsPauseAndResume_AreVotes()
        {
            Arena arena = null;
            VoteBoard board = null;
            yield return Begin((a, b) => { arena = a; board = b; }, (_, __) => { });
            Assert.That(arena.StartMatch(TestMatches.OnThePracticeRange()), Is.True);
            Turns turns = null;
            yield return WaitUntil(() => (turns = Turns.For(Client)) != null && turns.MatchPhase == MatchPhase.Aiming, "the client never got the match");
            yield return WaitUntil(() => VoteBoard.For(Client) != null && VoteBoard.For(Client).IsSpawned, "the client never got the vote board");

            Assert.That(PauseMenu.Pause(Client), Is.True, "Ann asks everyone");
            yield return WaitUntil(() => board.IsVoteOpen, "the pause vote never opened");
            Assert.That(arena.Paused, Is.False, "not until the vote passes");
            board.CastVote(true);                                 // the host says Yes too: 2 of 2
            yield return WaitUntil(() => turns.IsPaused, "the pause vote passed but the client never saw the pause");
            Assert.That(arena.Paused, Is.True);
            yield return WaitUntil(() => !board.IsVoteOpen, "the pause vote stayed open");

            Assert.That(PauseMenu.Resume(Client), Is.True, "Ann asks everyone to carry on");
            yield return WaitUntil(() => board.IsVoteOpen, "the resume vote never opened");
            Assert.That(arena.Paused, Is.True, "not until the vote passes");
            board.CastVote(true);
            yield return WaitUntil(() => !turns.IsPaused, "the resume vote passed but the client stayed paused");
            Assert.That(arena.Paused, Is.False);
        }

        [UnityTest]
        public IEnumerator RandomizeIsTheHostsAlone_AndOnlyBeforeTheRoundsFirstShot()
        {
            Arena arena = null;
            Player host = null;
            yield return Begin((a, _) => arena = a, (h, __) => host = h);
            Assert.That(arena.StartMatch(new MatchSetup { AiCount = 0 }), Is.True);
            Turns turns = null;
            yield return WaitUntil(() => (turns = Turns.For(Client)) != null && turns.MatchPhase == MatchPhase.Aiming, "the client never got the match");
            Assert.That(arena.SetPaused(true), Is.True);
            Assert.That(arena.MayRerollRound, Is.True);

            int version = Arena.LevelVersion;
            Assert.That(PauseMenu.Randomize(Client), Is.False, "Ann is not the host");
            Assert.That(Arena.LevelVersion, Is.EqualTo(version));

            Assert.That(PauseMenu.Randomize(Server), Is.True);
            Assert.That(Arena.LevelVersion, Is.GreaterThan(version), "a fresh level");
            Assert.That(arena.Paused, Is.False, "Randomize resumes, as obj_btn_randomize did");
            Assert.That(arena.Match.Current, Is.EqualTo(host.Seat), "the turn starts over");
            yield return WaitUntil(() => !turns.IsPaused, "the client stayed paused");

            yield return WaitUntil(() => arena.Match.Phase == MatchPhase.Aiming, "the turn never started over");
            host.ServerFire(0f, 0f);
            yield return WaitUntil(() => host.Shots == 1, "the host never fired");
            Assert.That(arena.MayRerollRound, Is.False, "not once anyone has fired");
            version = Arena.LevelVersion;
            Assert.That(PauseMenu.Randomize(Server), Is.False);
            Assert.That(Arena.LevelVersion, Is.EqualTo(version));
        }

        [UnityTest]
        public IEnumerator QuitRoundIsAVote_ThatEndsTheMatchBackToPracticeOnEveryPeer()
        {
            Arena arena = null;
            VoteBoard board = null;
            yield return Begin((a, b) => { arena = a; board = b; }, (_, __) => { });
            _hud = MatchHud.Create(Client);
            Assert.That(arena.StartMatch(TestMatches.OnThePracticeRange()), Is.True);
            Turns turns = null;
            yield return WaitUntil(() => (turns = Turns.For(Client)) != null && turns.MatchPhase == MatchPhase.Aiming, "the client never got the match");
            Assert.That(arena.SetPaused(true), Is.True);
            yield return WaitUntil(() => _hud.Pause.Showing, "the pause menu never came up on the client");
            VoteBoard onClient = null;
            yield return WaitUntil(() => (onClient = VoteBoard.For(Client)) != null && onClient.IsSpawned, "the client never got the vote board");

            Assert.That(PauseMenu.QuitRound(Server), Is.True, "the host asks everyone too");
            yield return WaitUntil(() => onClient.IsVoteOpen, "the client never saw the quit vote");
            Assert.That(arena.MatchRunning, Is.True, "not until the vote passes");
            onClient.CastVote(true);                              // Ann's Yes and the host's: 2 of 2

            yield return WaitUntil(() => !arena.MatchRunning, "the quit vote never ended the match");
            Assert.That(arena.Paused, Is.False);
            yield return WaitUntil(() => turns.MatchPhase == MatchPhase.Practice, "the client never went back to practice");
            yield return WaitUntil(() => _hud.Pause.QuitsSeen == 1, "the client's pause menu never saw the round quit");
            yield return WaitUntil(() => !_hud.Pause.Showing, "the pause menu stayed up after the round was quit");
            Dummy dummy = null;
            yield return WaitUntil(() => Client.Objects.TryGet(Arena.DummyKey(0), out dummy) && !dummy.Down, "the practice dummies never stood back up");
        }

        // Starts both sessions, serves the arena and the pause menu's votes, and waits for both players.
        private IEnumerator Begin(System.Action<Arena, VoteBoard> served, System.Action<Player, Player> players)
        {
            yield return StartAndJoinAll();
            Arena arena = Arena.Serve(Server, GamePrefabs.Dummy);
            VoteBoard board = VoteBoard.Spawn(Server);
            board.AddKind(arena.CreatePauseVote());
            board.AddKind(arena.CreateResumeVote());
            board.AddKind(arena.CreateQuitRoundVote());
            var host = WaitForSpawn<Player>(Server, owner: Server.LocalClientId);
            yield return host;
            var ann = WaitForSpawn<Player>(Server, owner: Client.LocalClientId);
            yield return ann;
            yield return WaitUntil(() => Player.In(Client).Count() == 2, "the client never saw both players");
            served(arena, board);
            players(host.Result, ann.Result);
        }
    }

    /// <summary>
    /// The pause menu's Music toggle (obj_btn_music) is the player's own profile mute: under the CLI session mute (U.RICO-T71)
    /// the game stays silent whatever it says, and what is saved is the player's choice with their other values, never the
    /// mute's forced zeros.
    /// </summary>
    public class PauseMenuMusicTests
    {
        private string _root;
        private GameAudio _audio;
        private float _listenerVolume;

        [SetUp]
        public void AFreshProfile_MutedForTheSession()
        {
            _listenerVolume = AudioListener.volume;
            _root = TempProfiles.Use();
            CommonSettings common = Profiles.Current.Settings.Common;
            common.SfxVolume = 0.4f;
            common.MusicVolume = 0.5f;
            common.MuteMusic = false;
            common.MuteSfx = false;
            Profiles.Current.SaveSettings();
            Profiles.SessionMuted = true;
        }

        [TearDown]
        public void PutTheProfilesBack()
        {
            Profiles.SessionMuted = false;
            AudioListener.volume = _listenerVolume;
            if (_audio != null)
            {
                Object.Destroy(_audio.gameObject);
            }

            TempProfiles.Release(_root);
        }

        [UnityTest]
        public IEnumerator TheMusicToggle_IsSilentUnderTheSessionMute_AndNeverSavesTheMutesValues()
        {
            _audio = GameAudio.Create();
            yield return null;
            Assert.That(_audio.MusicOn, Is.True, "the player's own setting, not the session mute's");

            Assert.That(PauseMenu.SetMusic(false), Is.True, "saved");
            Assert.That(_audio.MusicOn, Is.False);
            Assert.That(_audio.Music.volume, Is.EqualTo(0f));

            Assert.That(PauseMenu.SetMusic(true), Is.True, "saved");
            Assert.That(_audio.MusicOn, Is.True);
            Assert.That(_audio.Music.volume, Is.EqualTo(0f), "still silent this session");
            Assert.That(AudioListener.volume, Is.EqualTo(0f));

            Assert.That(PauseMenu.SetMusic(false), Is.True);
            Profiles.SessionMuted = false;
            CommonSettings reread = Profiles.Current.ReloadSettings().Common;
            Assert.That(reread.MuteMusic, Is.True, "the toggle's choice");
            Assert.That(reread.MusicVolume, Is.EqualTo(0.5f).Within(1e-4), "not the mute's 0");
            Assert.That(reread.SfxVolume, Is.EqualTo(0.4f).Within(1e-4), "not the mute's 0");
            Assert.That(reread.MuteSfx, Is.False, "not the mute's true");
        }
    }
}
