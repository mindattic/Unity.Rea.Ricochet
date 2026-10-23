// The game's one scene component: it draws the level, serves the rules (Arena) on the server, registers the votes the game offers, and
// adds the game's own items to the drop-in front end. The menus, lobby, join codes, reconnect, roster and vote overlay
// are the kit's; this is the only glue a game writes. Runs on every peer: a dedicated server and a bot run this scene too.
using Ricochet.Core;        // MatchRules, MatchSetup, Ai, ArsenalRules, Announcer
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
        private readonly MatchSetup _setup = new MatchSetup(); // what the start-match vote proposes (obj_btn_ai_*, obj_btn_aimassist_*)

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

            // Votes: NetVotes (beside the NetBootstrap) spawns the vote board; the front end shows the overlay in game.
            if (NetVotes.Instance != null)
            {
                NetVotes.Instance.AddKind(_arena.CreateRestartVote());
                NetVotes.Instance.AddKind(_arena.CreateMatchVote());
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
                s.AddText("Esc opens the menu: invite players, change your name and colour, pick how many AI opponents to add "
                    + "(up to 16 seats in all) and vote to start a match.");
                s.AddText("In a match you play in turn: the yellow clock at the top is your 20 seconds to fire, the bars are "
                    + "everyone's health, and the line under the clock is the order the players still standing take their turns. "
                    + "A shot hurts more for every bounce (and hurts you, if it comes back). The last one standing wins the round.");
                s.AddText("Weapons: Q/E cycle the cannon (unlimited), laser (full range, fast), shotgun (a fan of five pellets), "
                    + "grenade (passes through players and goes off when its fuse runs out: 1 shorter, 2 longer, 3 longest) and "
                    + "railgun (straight through walls, hitting everyone on the line). You start each round with three of each.");
                s.AddText("Items: Z/C cycle, F uses: an extra bounce for this turn's shot, a teleporter that moves you to where it "
                    + "lands, a shrink ray that shrinks the first player it reaches, and aim assist, which turns your aim onto a "
                    + "shot that reaches an opponent (at your power if it can, else at full) and lights its path. Crates fall "
                    + "during a match: shoot across one to collect it (W a weapon, I an item, + health, ? a mystery weapon).");
                s.AddText("AI opponents fill the empty seats. Before you vote to start a match, pick how many, how well they aim "
                    + "(skill 10 fires exactly where it means to; each step down nudges its aim up to a degree more either way) "
                    + "and how many aim assists everyone starts with (0 for none, 10 for unlimited). On its turn an AI thinks, "
                    + "looks for a straight shot, then a bank shot, then a railgun shot through the walls, and fires.");
                s.AddText("The vortex device flies like a shell: press Space again while it is in the air to set it off. It hurts "
                    + "everyone in its pull, swallows shield walls, redirectors and foliage, and tears a breach in any border it "
                    + "reaches; shots fly out through a breach instead of bouncing. The shield wall item stands a wall in front of "
                    + "you that only your own shots pass; anyone else's shatters it. The redirector item sets a redirector down "
                    + "where it lands, sending shots on the way it flew.");
                s.AddText("The announcer (pick who and how often before you vote to start a match) welcomes everyone, cheers a "
                    + "shot that knocks someone out and heckles one that hits nobody. A hit on an opponent scores 100 points, 25 "
                    + "more a bounce and 5 for every tenth of a second the shot was in the air; scores last the whole match. The "
                    + "card at the bottom right shows whose turn it is, their score, weapon, item and the combo the shot in the "
                    + "air would score.");
                s.AddText("Hazards: the blue electric arc stops any shot that touches it; the red laser gate does too, but it is "
                    + "only on every other turn; a shot cuts down the first tuft of grass it touches and stops there.");
            }, MenuPlacement.Both);

            frontEnd.AddPauseMenuItems(s =>
            {
                s.AddRange("AI opponents", 0, MatchRules.MaxSeats - 1, _setup.AiCount, v => _setup.AiCount = Mathf.RoundToInt(v), wholeNumbers: true);
                s.AddRange("AI skill", Ai.MinSkill, Ai.MaxSkill, _setup.AiSkill, v => _setup.AiSkill = Mathf.RoundToInt(v), wholeNumbers: true);
                s.AddRange("Aim assists each (10 = unlimited)", 0, ArsenalRules.Unlimited, _setup.AimAssists, v => _setup.AimAssists = Mathf.RoundToInt(v),
                    wholeNumbers: true);
                // The settings screen's announcements box (UR-T8): the personality, and how often it speaks.
                var voices = new System.Collections.Generic.List<string>();
                foreach (AnnouncerVoice voice in Announcer.Voices)
                {
                    voices.Add(voice.Name);
                }

                s.AddSelect("Announcer", voices, _setup.AnnouncerVoice, v => _setup.AnnouncerVoice = v);
                s.AddRange("Announcements (0 off, 1 every time, 4 rarely)", 0, Announcer.MaxFrequency, _setup.Announcements,
                    v => _setup.Announcements = Mathf.RoundToInt(v), wholeNumbers: true);
                s.AddButton("Vote to start a match", StartMatchVote);
                s.AddButton("Vote to restart", StartRestartVote);
            });
            frontEnd.GameStarted += () => Debug.Log("[Game] In the game as " + frontEnd.Session.Identity.DisplayName + ".");
            frontEnd.Left += why => Debug.Log("[Game] Back at the main menu: " + why + ".");
        }

        private void StartMatchVote()
        {
            if (NetVotes.Instance != null && NetVotes.Instance.Board != null)
            {
                NetVotes.Instance.Board.StartVote(Arena.MatchVote, _setup.AiCount, _setup.Format());
            }
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
