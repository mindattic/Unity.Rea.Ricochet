// The game's one scene component: it draws the level, serves the rules (Arena) on the server, registers the votes the game offers, and
// adds the game's own rooms to the drop-in front end (UR-T9: the title, players, map, controls, how-to-play and credits rooms,
// Menus/TitleScreen; the endgame recap is the HUD's) and the match box on the host's lobby (UR-T11, LobbyRoom). The
// menus, Host / Join by IP / Join by code, the lobby's join code and roster, reconnect and the vote overlay are the kit's. Runs on every peer: a dedicated server and a bot run this scene too.
using System.Collections;
using MindAttic.Components.Unity; // MenuScreen
using Ricochet.Core.Level;
using Ricochet.Core.Match;
using Ricochet.Core.Menu;
using Unity.Multiplayer;    // NetBootstrap, NetVotes, NetSession
using Unity.Multiplayer.UI; // NetFrontEnd, MenuPlacement
using UnityEngine;
using UnityEngine.Serialization;

namespace Ricochet
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
            _setup.Map = Maps.RandomIndex; // U.RICO-T101: no map room, every round is the random level (a saved older choice is dropped)
            _saved = _setup.Clone();

            // The Settings screen: the kit's name and colour (read from and saved to the MindAttic profile, Unity.Shared,
            // since that package is installed), then the profile's audio settings, saved the moment they change.
            frontEnd.AddSettingsItems(AddAudioSettings);

            // The original's room graph (UR-T9): the title first, then the kit's main menu with the setup rooms (the Start
            // screen, players, whose Start goes into the game) and the controls, how-to-play and credits rooms off it; the same rooms
            // in the in-game menu, where the players room's button asks everyone by the match vote. The settings room's
            // announcer box sits under the kit's Settings, beside the name and colour.
            StartCoroutine(ShowTitle(frontEnd));

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
                TitleScreen.Show(frontEnd, Application.version, () =>
                {
                    if (frontEnd.MainMenu != null)
                    {
                        frontEnd.MainMenu.Push(MenuFlow.Title(Room.Setup), BuildSetupRoom);
                    }
                });
            }
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
        private void BuildPlayersRoom(MenuScreen s)
        {
            NetFrontEnd frontEnd = NetFrontEnd.Instance;
            RememberOnHide(s);
            bool inGame = frontEnd != null && frontEnd.State == FrontEndState.InGame;
            Menus.BuildPlayers(s, _setup, frontEnd != null ? frontEnd.Identity.DisplayName : null, frontEnd != null ? (System.Action)frontEnd.OpenSettings : null,
                from => (inGame ? (System.Action)StartMatchVote : HostFromStart)(), ResetSetup, inGame ? "Vote to start a match" : Menus.StartLabel);
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
            if (frontEnd != null && frontEnd.HasMenus && frontEnd.State == FrontEndState.Menu && GameAudio.Instance != null)
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
