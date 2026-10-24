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

            // The level is the same Core data on every peer; draw it. The match HUD draws once a match starts.
            LevelView.Create(Arena.Level);
            MatchHud.Create();

            // Server rules: dummies spawn when the session runs as a host or dedicated server, and the level is laid with its
            // hazards (UR-T6): electric arcs, laser gates and foliage.
            _arena = Arena.Serve(bootstrap.Session, dummyPrefab, layHazards: true);

            // Votes: NetVotes (beside the NetBootstrap) spawns the vote board; the front end shows the overlay in game. The
            // kick vote (UR-T14) is the kit's own with this game's line under it, in place of the one NetVotes adds.
            if (NetVotes.Instance != null)
            {
                NetVotes.Instance.AddKind(_arena.CreateRestartVote());
                NetVotes.Instance.AddKind(_arena.CreateMatchVote());
                NetVotes.Instance.AddKind(_arena.CreateKickVote());
            }

            // Sound (UR-T10): the volumes and mutes are the active MindAttic profile's (none on a headless peer).
            GameAudio.Create();

            NetFrontEnd frontEnd = NetFrontEnd.Instance;
            if (frontEnd == null)
            {
                return;
            }

            // The Settings screen: the kit's name and colour (read from and saved to the MindAttic profile, Unity.Shared,
            // since that package is installed), then the profile's audio settings, saved the moment they change.
            frontEnd.AddSettingsItems(AddAudioSettings);

            // The original's room graph (UR-T9): the title first, then the kit's main menu with the setup rooms (players,
            // then the map room, then into the game) and the controls, how-to-play and credits rooms off it; the same rooms
            // in the in-game menu, where the map room's button asks everyone by the match vote. The settings room's
            // announcer box sits under the kit's Settings, beside the name and colour.
            StartCoroutine(ShowTitle(frontEnd));

            frontEnd.AddScreen(MenuFlow.Title(Room.Players), s => Menus.BuildPlayers(s, _setup, frontEnd.Identity.DisplayName,
                frontEnd.OpenSettings, from => from.Stack.Push(MenuFlow.Title(Room.MapRoom), BuildMapRoom)), MenuPlacement.Both);
            frontEnd.AddScreen(MenuFlow.Title(Room.Controls), Menus.BuildControls, MenuPlacement.Both);
            // UR-T36: the Input System's RicochetControls asset, rebindable and saved to the Unity.Shared profile.
            frontEnd.AddScreen("Rebind controls", Menus.BuildRebind, MenuPlacement.Both);
            frontEnd.AddScreen(MenuFlow.Title(Room.HowToPlay), s => Menus.BuildHowToPlay(s), MenuPlacement.Both);
            frontEnd.AddScreen(MenuFlow.Title(Room.Credits), Menus.BuildCredits, MenuPlacement.MainMenu);
            frontEnd.AddSettingsItems(s => Menus.AddGameSettings(s, _setup));
            frontEnd.AddPauseMenuItems(s =>
            {
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
                audio.Apply(mix);
                GameAudio.Play(SoundCue.Bounce); // a click at the new volume
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
        private static IEnumerator ShowTitle(NetFrontEnd frontEnd)
        {
            yield return null;
            if (frontEnd != null && frontEnd.HasMenus && frontEnd.State == FrontEndState.Menu)
            {
                TitleScreen.Show(frontEnd, Application.version);
            }
        }

        // The map room, one step on from the players room: its button goes on. From the main menu that hosts a game with
        // the setup and opens the lobby (UR-T11: the join code, who is in, and the match), whose Start begins the match with
        // everyone who joined; in a game it is the match vote.
        private void BuildMapRoom(MenuScreen s)
        {
            NetFrontEnd frontEnd = NetFrontEnd.Instance;
            bool inGame = frontEnd != null && frontEnd.State == FrontEndState.InGame;
            Menus.BuildMap(s, _setup, inGame ? "Vote to start a match" : "Next: " + MenuFlow.Title(MenuFlow.Next(Room.MapRoom)),
                inGame ? (System.Action)StartMatchVote : HostFromMapRoom);
        }

        // titlescreen -> mainmenu -> players -> maproom -> lobby -> gamescreen: host with the setup; the lobby's Start plays it.
        private void HostFromMapRoom()
        {
            if (_lobby != null)
            {
                _ = _lobby.HostMatch(_setup);
            }
        }

        private void OnDestroy()
        {
            _lobby?.Dispose();
            _lobby = null;
        }

        private void StartMatchVote()
        {
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
