// The game's one scene component: it draws the level, serves the rules (Arena) on the server, registers the votes the game offers, and
// adds the game's own items to the drop-in front end. The menus, lobby, join codes, reconnect, roster and vote overlay
// are the kit's; this is the only glue a game writes. Runs on every peer: a dedicated server and a bot run this scene too.
using Unity.Multiplayer;    // NetBootstrap, NetVotes
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

            // The level is the same Core data on every peer; draw it.
            LevelView.Create(Arena.Level);

            // Server rules: dummies spawn when the session runs as a host or dedicated server.
            _arena = Arena.Serve(bootstrap.Session, dummyPrefab);

            // Votes: NetVotes (beside the NetBootstrap) spawns the vote board; the front end shows the overlay in game.
            if (NetVotes.Instance != null)
            {
                NetVotes.Instance.AddKind(_arena.CreateRestartVote());
            }

            NetFrontEnd frontEnd = NetFrontEnd.Instance;
            if (frontEnd == null)
            {
                return;
            }

            frontEnd.AddScreen("How to play", s =>
            {
                s.AddText("Left/Right (or A/D) turn your aim, Up/Down (or W/S) set the power, Space fires.");
                s.AddText("The faint line shows where the shot will bounce: off the border, off the white walls, and along "
                    + "the orange redirectors' arrows. It explodes on its fifth contact. Knock the dummies down.");
                s.AddText("Esc opens the menu: invite players, change your name and colour, or vote to restart.");
            }, MenuPlacement.Both);

            frontEnd.AddPauseMenuItems(s => s.AddButton("Vote to restart", StartRestartVote));
            frontEnd.GameStarted += () => Debug.Log("[Game] In the game as " + frontEnd.Session.Identity.DisplayName + ".");
            frontEnd.Left += why => Debug.Log("[Game] Back at the main menu: " + why + ".");
        }

        private static void StartRestartVote()
        {
            if (NetVotes.Instance != null && NetVotes.Instance.Board != null)
            {
                NetVotes.Instance.Board.StartVote(Arena.RestartVote);
            }
        }
    }
}
