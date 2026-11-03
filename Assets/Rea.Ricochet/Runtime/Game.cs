// The game's one scene component: it draws the level, serves the rules (Arena) on the server, registers the votes the game offers, and
// adds the game's own rooms to the drop-in front end (UR-T9: the title, players, map, controls, how-to-play and credits rooms,
// Menus/TitleScreen; the endgame recap is the HUD's) and the match box on the host's lobby (UR-T11, LobbyRoom). The
// menus, Host / Join by IP / Join by code, the lobby's join code and roster, reconnect and the vote overlay are the kit's. Runs on every peer: a dedicated server and a bot run this scene too.
using System.Collections;
using MindAttic.Components.Unity; // MenuScreen
using Rea.Ricochet.Core.Level;
using Rea.Ricochet.Core.Match;
using Rea.Ricochet.Core.Menu;
using Unity.Multiplayer;    // NetBootstrap, NetVotes, NetSession
using Unity.Multiplayer.UI; // NetFrontEnd, MenuPlacement
using UnityEngine;
using UnityEngine.Serialization;

namespace Rea.Ricochet
{
    [DefaultExecutionOrder(-800)] // before NetBootstrap.Start, which may start a headless server at once
    public sealed class Game : MonoBehaviour
    {
        [Tooltip("The practice dummy prefab (NetworkObject + NetKey + Dummy), also in the scene's prefab list.")]
        [FormerlySerializedAs("cratePrefab")]
        [SerializeField] private GameObject dummyPrefab;

        private Arena _arena;
        private LobbyRoom _lobby;
        private readonly MatchSetup _setup = new MatchSetup(); // what the start-match vote proposes: the players, map and settings rooms' choices
        private MatchSetup _saved; // what the profile holds now (UR-T41), so an unchanged setup is not written again

        /// <summary>The host's lobby (null before Start, or without the front end).</summary>
        public LobbyRoom HostLobby => _lobby;

        /// <summary>The rules this scene serves (null before Start).</summary>
        public Arena Rules
        {
            get { return _arena; }
        }

        private void Start()
        {
            NetBootstrap bootstrap = NetBootstrap.Instance;
            if (bootstrap == null || bootstrap.Session == null)
            {
                Debug.LogError("[Game] No NetBootstrap in the scene: run MindAttic > Multiplayer > Set Up Scene.", this);
                return;
            }

            if (Camera.main != null)
            {
                RenderStyle.Current.ConfigureCamera(Camera.main);
            }

            // The level is the same Core data on every peer; draw it. The match HUD draws once a match starts.
            LevelView.Create(Arena.Level);
            // U.RICO-T46: the round's floor, clouds, spotlight, lights and border sprites, the same on every peer; the
            // clouds stop while the match is paused, as isPaused stopped them.
            NetSession session = bootstrap.Session;
            BackdropView.Create(() => Turns.PausedIn(session));
            MatchHud.Create();

            // Server rules: dummies spawn when the session runs as a host or dedicated server. The arcs, laser gates and foliage
            // (UR-T6) are laid only when hazards.json's layHazards asks (U.RICO-T91): the original laid none.
            _arena = Arena.Serve(bootstrap.Session, dummyPrefab, layHazards: Arena.HazardRules.LayHazards);

            // Votes: NetVotes (beside the NetBootstrap) spawns the vote board; the front end shows the overlay in game. The
            // kick vote (UR-T14) is the kit's own with this game's line under it, in place of the one NetVotes adds.
            if (NetVotes.Instance != null)
            {
                NetVotes.Instance.AddKind(_arena.CreateRestartVote());
                NetVotes.Instance.AddKind(_arena.CreateMatchVote());
                NetVotes.Instance.AddKind(_arena.CreateKickVote());
                // The pause menu (UR-T43): a guest's pause and resume, and everyone's quit round, ask everyone.
                NetVotes.Instance.AddKind(_arena.CreatePauseVote());
                NetVotes.Instance.AddKind(_arena.CreateResumeVote());
                NetVotes.Instance.AddKind(_arena.CreateQuitRoundVote());
            }

            // Sound (UR-T10): the volumes and mutes are the active MindAttic profile's (none on a headless peer).
            GameAudio.Create();

            // U.RICO-T53: the one black curtain every screen change fades through (obj_fade and its ten copies).
            ScreenFade.Create();

            // U.RICO-T54: obj_mouse's spr_mouse as the pointer, over everything, in every room.
            GameCursor.Create();

            // U.RICO-T78: every menu control in the original's spr_button / checkbox / input box / arrow art and fonts.
            GmlMenuSkin.Ensure();

            NetFrontEnd frontEnd = NetFrontEnd.Instance;
            if (frontEnd == null)
            {
                return;
            }

            // UR-T41: the setup starts where the last session left it (the original's read_config), from the profile.
            SetupMemory.Load(_setup);
            // U.RICO-T101: no map room, every round is the random level (a saved older choice is dropped). U.RICO-T118: only when
            // maps.json switches the optional map room on does a remembered hand-laid map of it stay chosen.
            if (!(Maps.MapRoomEnabled && Maps.At(_setup.Map).MapRoomOnly))
            {
                _setup.Map = Maps.RandomIndex;
            }
            _saved = _setup.Clone();

            // The Settings screen: the kit's name and colour (read from and saved to the MindAttic profile, Unity.Shared,
            // since that package is installed), then the profile's audio settings, saved the moment they change.
            frontEnd.AddSettingsItems(AddAudioSettings);

            // The original's room graph (UR-T9): the title first, then the kit's main menu with the setup rooms (the Start
            // screen, players, whose Start goes into the game) and the controls, how-to-play and credits rooms off it; the same rooms
            // in the in-game menu, where the players room's button asks everyone by the match vote. The settings room's
            // announcer box sits under the kit's Settings, beside the name and colour.
            StartCoroutine(ShowTitle(frontEnd));

            // U.RICO-T146: -autowalk scripts the same rooms U.RICO-T58 checked by hand and screenshots each one.
            if (Autowalk.Requested)
            {
                StartCoroutine(RunAutowalk(frontEnd));
            }

            // U.RICO-T52: the main menu's Start opens the Start screen (the title room's setup panel), whose Next is the
            // players room; in a game the players room stands on its own (the match vote follows it).
            frontEnd.AddScreen(MenuFlow.Title(Room.Setup), BuildSetupRoom, MenuPlacement.MainMenu);
            frontEnd.AddScreen(MenuFlow.Title(Room.Players), BuildPlayersRoom, MenuPlacement.PauseMenu);
            frontEnd.AddScreen(MenuFlow.Title(Room.Controls), Menus.BuildControls, MenuPlacement.Both);
            // UR-T36: the Input System's RicochetControls asset, rebindable and saved to the Unity.Shared profile.
            frontEnd.AddScreen("Rebind controls", Menus.BuildRebind, MenuPlacement.Both);
            frontEnd.AddScreen(MenuFlow.Title(Room.HowToPlay), s => Menus.BuildHowToPlay(s), MenuPlacement.Both);
            frontEnd.AddScreen(MenuFlow.Title(Room.Credits), Menus.BuildCredits, MenuPlacement.MainMenu);
            frontEnd.AddSettingsItems(s =>
            {
                RememberOnHide(s);
                Menus.AddGameSettings(s, _setup);
            });
            frontEnd.AddPauseMenuItems(s =>
            {
                // UR-T43: the Esc menu stays the kit's; this pauses the match (the host at once, anyone else by a vote).
                s.AddButton(VoteMenu.PauseButton, () =>
                {
                    BackToTheGame();
                    PauseMenu.TogglePause(frontEnd.Session);
                });
                s.AddButton("Vote to start a match", StartMatchVote);
                s.AddButton(VoteMenu.RestartButton, StartRestartVote);
            });
            // The kick room (UR-T14): built afresh each time it opens, from who is in the game now.
            frontEnd.AddScreen(VoteMenu.KickRoom, s => Menus.BuildKick(s, KickRoster(frontEnd.Session), StartKickVote), MenuPlacement.PauseMenu);
            // The host's lobby (UR-T11): the kit's join code and roster, with the match Start will begin.
            _lobby = LobbyRoom.Attach(frontEnd, this, setup => _arena != null && _arena.StartMatch(setup));
            frontEnd.GameStarted += () => Debug.Log("[Game] In the game as " + frontEnd.Session.Identity.DisplayName + ".");
            frontEnd.Left += why => Debug.Log("[Game] Back at the main menu: " + why + ".");
        }

        // The profile's music and effect volumes (0-100) and mutes: heard at once while the screen is open, written back to
        // the profile's settings.json when it closes (once, not on every step of a dragged slider).
        private static void AddAudioSettings(MindAttic.Components.Unity.MenuScreen s)
        {
            GameAudio audio = GameAudio.Instance;
            if (audio == null)
            {
                return;
            }

            audio.Reload();
            AudioMix mix = audio.Mix;
            bool changed = false;
            System.Action<System.Action<AudioMix>> change = edit =>
            {
                edit(mix);
                changed = true;
                audio.Apply(mix); // (UiSounds plays the slider's sfx_select)
            };

            s.AddRange("Sound effects volume", 0, 100, Mathf.Round(mix.SfxVolume * 100f), v => change(m => m.SfxVolume = v / 100f), wholeNumbers: true);
            s.AddToggle("Mute sound effects", mix.MuteSfx, v => change(m => m.MuteSfx = v));
            s.AddRange("Music volume", 0, 100, Mathf.Round(mix.MusicVolume * 100f), v => change(m => m.MusicVolume = v / 100f), wholeNumbers: true);
            s.AddToggle("Mute music", mix.MuteMusic, v => change(m => m.MuteMusic = v));
            s.Hidden += () =>
            {
                if (changed && audio != null)
                {
                    changed = false;
                    audio.Save(mix);
                }
            };
        }

        // The title comes up over the kit's main menu once the front end has built its menus (it builds none in a headless
        // process, a bot or a session already running, and then there is no title either).
        // Its Start opens the Start screen over the main menu.
        private IEnumerator ShowTitle(NetFrontEnd frontEnd)
        {
            yield return null;
            if (frontEnd != null && frontEnd.HasMenus && frontEnd.State == FrontEndState.Menu)
            {
                // U.RICO-T57: the splash and the startup credits first (the menus hidden until the title is done with them).
                frontEnd.MenusVisible = false;
                SplashScreen.Show(() => ShowTitleNow(frontEnd));
            }
        }

        private void ShowTitleNow(NetFrontEnd frontEnd)
        {
            if (frontEnd == null || frontEnd.State != FrontEndState.Menu)
            {
                if (frontEnd != null)
                {
                    frontEnd.MenusVisible = true;
                }

                return;
            }

            TitleScreen.Show(frontEnd, Application.version, () =>
            {
                if (frontEnd.MainMenu != null)
                {
                    frontEnd.MainMenu.Push(MenuFlow.Title(Room.Setup), BuildSetupRoom);
                }
            });
        }

        // U.RICO-T52: the Start screen, then the players room.
        private void BuildSetupRoom(MenuScreen s)
        {
            RememberOnHide(s);
            Menus.BuildSetup(s, _setup, from => from.Stack.Push(MenuFlow.Title(Room.Players), BuildPlayersRoom), ResetSetup);
        }

        // The players room: its Start goes straight on to the game (obj_btn_start). From the main menu that hosts a game with the
        // setup and opens the lobby (UR-T11: the join code, who is in, and the match), whose Start begins the match with everyone
        // who joined; in a game it is the match vote. There is no map room: every round is the random level.
        // U.RICO-T118 (spike, never the default): while maps.json's "mapRoom" is on, the players room's button is Next to the
        // optional map room instead, and the map room's button does what Start does.
        private void BuildPlayersRoom(MenuScreen s)
        {
            NetFrontEnd frontEnd = NetFrontEnd.Instance;
            RememberOnHide(s);
            string playingAs = frontEnd != null ? frontEnd.Identity.DisplayName : null;
            System.Action changeNameAndColour = frontEnd != null ? (System.Action)frontEnd.OpenSettings : null;
            if (MenuFlow.Next(Room.Players) == Room.MapRoom)
            {
                Menus.BuildPlayers(s, _setup, playingAs, changeNameAndColour, from => from.Stack.Push(MenuFlow.Title(Room.MapRoom), BuildMapRoom), ResetSetup,
                    "Next: " + MenuFlow.Title(Room.MapRoom));
                return;
            }

            bool inGame = frontEnd != null && frontEnd.State == FrontEndState.InGame;
            Menus.BuildPlayers(s, _setup, playingAs, changeNameAndColour,
                from => (inGame ? (System.Action)StartMatchVote : HostFromStart)(), ResetSetup, inGame ? "Vote to start a match" : Menus.StartLabel);
        }

        // U.RICO-T118: the optional map room, one step on from the players room (only while the map room is on): the random arena
        // or a hand-laid map, then Start, which hosts from the main menu or asks everyone by the match vote in a game.
        private void BuildMapRoom(MenuScreen s)
        {
            NetFrontEnd frontEnd = NetFrontEnd.Instance;
            RememberOnHide(s);
            bool inGame = frontEnd != null && frontEnd.State == FrontEndState.InGame;
            Menus.BuildMap(s, _setup, inGame ? "Vote to start a match" : Menus.StartLabel, inGame ? (System.Action)StartMatchVote : HostFromStart, ResetSetup);
        }

        // UR-T41: a setup screen saves the setup to the profile as it closes (once, not on every step of a slider), the way
        // the original rewrote config.ini on every change. The method group is the same delegate every time, so a screen
        // rebuilt in place never subscribes twice.
        private void RememberOnHide(MenuScreen s)
        {
            s.Hidden -= RememberSetup;
            s.Hidden += RememberSetup;
        }

        // Saves the setup when it differs from what the profile already holds.
        private void RememberSetup()
        {
            if (_saved != null && _setup.SameAs(_saved))
            {
                return;
            }

            if (SetupMemory.Save(_setup))
            {
                _saved = _setup.Clone();
            }
        }

        // The setup screens' Reset to defaults (the original's Default button): every choice back to the defaults, saved.
        private void ResetSetup()
        {
            if (SetupMemory.ResetToDefaults(_setup))
            {
                _saved = _setup.Clone();
            }
        }

        // titlescreen -> Start screen -> players (Start) -> lobby -> gamescreen: host with the setup; the lobby's Start plays it.
        private void HostFromStart()
        {
            RememberSetup();
            if (_lobby == null)
            {
                return;
            }

            // U.RICO-T53/T101: Start fades to black before it goes on (obj_btn_start's curtain went to "fadein" and its
            // room_goto came once it was black); the lobby then comes up out of black.
            ScreenFade.Cover(() =>
            {
                if (_lobby != null)
                {
                    _ = _lobby.HostMatch(_setup);
                }
            });
        }

        private void Update()
        {
            // U.RICO-T52, obj_jukebox: in the menus, track after track (the match plays its own).
            NetFrontEnd frontEnd = NetFrontEnd.Instance;
            // U.RICO-T57: not while the splash plays sfx_tech or the startup credits hold the silence.
            if (frontEnd != null && frontEnd.HasMenus && frontEnd.State == FrontEndState.Menu && GameAudio.Instance != null && !SplashScreen.Showing)
            {
                GameAudio.Instance.Jukebox();
            }
        }

        private void OnDestroy()
        {
            _lobby?.Dispose();
            _lobby = null;
        }

        private void StartMatchVote()
        {
            RememberSetup();
            if (NetVotes.Instance != null && NetVotes.Instance.Board != null)
            {
                NetVotes.Instance.Board.StartVote(Arena.MatchVote, _setup.AiCount, _setup.Format());
                BackToTheGame();
            }
        }

        private static void StartRestartVote()
        {
            if (NetVotes.Instance != null && NetVotes.Instance.Board != null)
            {
                NetVotes.Instance.Board.StartVote(Arena.RestartVote);
                BackToTheGame();
            }
        }

        private static void StartKickVote(ulong clientId)
        {
            if (NetVotes.Instance != null && NetVotes.Instance.Board != null)
            {
                NetVotes.Instance.Board.StartKickVote(clientId);
                BackToTheGame();
            }
        }

        // Closes the in-game menu, so the vote overlay (shown in game, under the menus) is in view.
        private static void BackToTheGame()
        {
            NetFrontEnd frontEnd = NetFrontEnd.Instance;
            if (frontEnd != null && frontEnd.PauseMenu != null)
            {
                frontEnd.PauseMenu.Close();
            }
        }

        // U.RICO-T146: scripted input that walks every room once, in the same order U.RICO-T58 checked by hand, and has
        // Autowalk screenshot each one. Only runs behind -autowalk; every wait has a timeout so a stuck session logs a
        // warning and moves on instead of hanging the player.
        private IEnumerator RunAutowalk(NetFrontEnd frontEnd)
        {
            yield return WaitFor(() => SplashScreen.Showing, 10f, "the splash");
            Autowalk.Capture("splash");
            yield return WaitFor(() => !SplashScreen.Showing, 15f, "the splash to finish");
            yield return null;
            Autowalk.Capture("title");

            frontEnd.MainMenu.Push(MenuFlow.Title(Room.Setup), BuildSetupRoom);
            yield return null;
            Autowalk.Capture("setup");
            frontEnd.MainMenu.Push(MenuFlow.Title(Room.Players), BuildPlayersRoom);
            yield return null;
            Autowalk.Capture("players");
            frontEnd.MainMenu.Pop(); // players
            frontEnd.MainMenu.Pop(); // setup
            yield return null;

            yield return CaptureOffMenuRoom(frontEnd, MenuFlow.Title(Room.Controls), Menus.BuildControls, "controls");
            yield return CaptureOffMenuRoom(frontEnd, "Rebind controls", Menus.BuildRebind, "rebind-controls");
            yield return CaptureOffMenuRoom(frontEnd, MenuFlow.Title(Room.HowToPlay), s => Menus.BuildHowToPlay(s), "how-to-play");
            yield return CaptureOffMenuRoom(frontEnd, MenuFlow.Title(Room.Credits), Menus.BuildCredits, "credits");

            frontEnd.OpenSettings();
            yield return null;
            Autowalk.Capture("settings");
            frontEnd.MainMenu.Pop();
            yield return null;

            while (frontEnd.MainMenu.Current != null && frontEnd.MainMenu.Current != frontEnd.MainMenu.Root)
            {
                frontEnd.MainMenu.Pop();
                yield return null;
            }

            Autowalk.Capture("main-menu");

            // A short, host-alone practice match (enough AI to finish without a second player) for the lobby, match,
            // pause and endgame rooms U.RICO-T58's manual pass also checked. A throwaway clone of _setup, so this never
            // writes the inflated AI count or goal back to the player's saved profile (RememberSetup only fires from a
            // screen's own Hidden event, which this skips on purpose).
            if (_lobby != null)
            {
                MatchSetup practice = _setup.Clone();
                practice.AiCount = System.Math.Max(practice.AiCount, 3);
                practice.Goal = MatchSetup.OneRound;
                _ = _lobby.HostMatch(practice);
            }

            yield return WaitFor(() => frontEnd.State == FrontEndState.Lobby, 20f, "the lobby");
            yield return null;
            Autowalk.Capture("lobby");

            if (_arena != null && _lobby != null && _lobby.Pending != null)
            {
                yield return WaitFor(() => _lobby.CanStart, 10f, "enough seats to start");
                _arena.StartMatch(_lobby.Pending);
            }

            NetSession session = frontEnd.Session;
            yield return WaitFor(() => Turns.For(session) != null && Turns.For(session).MatchPhase == MatchPhase.Aiming, 45f, "the match to begin");
            Autowalk.Capture("match");

            if (Turns.For(session) != null)
            {
                PauseMenu.TogglePause(session);
                yield return WaitFor(() => Turns.PausedIn(session), 10f, "the pause");
                Autowalk.Capture("pause");
                PauseMenu.TogglePause(session);
            }

            yield return WaitFor(() => Turns.For(session) != null && Turns.For(session).MatchPhase == MatchPhase.GameOver, 180f, "the match to end");
            Autowalk.Capture("endgame");

            Debug.Log("[Autowalk] Done: screenshots in " + Autowalk.Folder);
        }

        // Opens an off-menu room (controls, rebind, how to play, credits), screenshots it and pops it, the way a user
        // visiting it from the main menu and backing out would.
        private static IEnumerator CaptureOffMenuRoom(NetFrontEnd frontEnd, string title, System.Action<MenuScreen> build, string room)
        {
            frontEnd.MainMenu.Push(title, build);
            yield return null;
            Autowalk.Capture(room);
            frontEnd.MainMenu.Pop();
            yield return null;
        }

        // Waits for <paramref name="condition"/>, up to <paramref name="timeoutSeconds"/>; logs and moves on instead of
        // hanging the walk when it is never met (a build without the expected flow, or a match that never starts).
        private static IEnumerator WaitFor(System.Func<bool> condition, float timeoutSeconds, string what)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (!condition())
            {
                Debug.LogWarning("[Autowalk] Timed out waiting for " + what + ".");
            }
        }

        // The kit's roster as the kick room sees it.
        private static System.Collections.Generic.IEnumerable<VoteMember> KickRoster(NetSession session)
        {
            if (session == null || session.Roster == null)
            {
                yield break;
            }

            foreach (Unity.Multiplayer.Core.NetPlayer player in session.Roster.All)
            {
                yield return new VoteMember(player.ClientId, player.DisplayName, player.IsHost, player.IsLocal, player.IsConnected);
            }
        }
    }
}
