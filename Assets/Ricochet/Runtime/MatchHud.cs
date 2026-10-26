// The match HUD on every peer, read from the synced state only (Turns and each Player), built in code as a uGUI canvas
// of MindAttic Components labels (UR-T38; it was IMGUI): the turn clock at the top centre (draw_turntime.gml's yellow
// "SS.FF"), a health bar per seat in two rows of eight either side of it, each in the player's colour with their name and
// an AI tag (setup_players.gml's obj_healthbar row and draw_scores.gml's icons), the seat whose turn it is outlined, and
// under the clock the order the living seats play in from now. On an AI seat's turn, obj_ai_gui's line along the bottom:
// "AI 2 is plotting and scheming...", pulsing between half and full. Each slot also carries the seat's match score
// (draw_scores.gml's "name: score", UR-T8). The announcer's portrait and name box and the turn card are HudOverlay's
// widgets, on a canvas of their own. Once the game's goal is reached (UR-T9) the caption names the champion and the
// endgame recap (EndgameRecap) comes up. A seat whose player dropped reads "away" (UR-T13), and while the turn waits for
// them the line under the clock says so, with the seconds they have left to reconnect. The canvas sorts under the kit's
// menus and dialogs (UR-T18), so the Esc menu, the main menu and a modal simply cover it. It lays out on the kit's
// 1920x1080 reference and shrinks the bars, their gaps and the clock's own gap together (Seating.BarScale, UR-T17) when
// the canvas is narrower than the eight columns need, so all 16 seats stay on screen from 1280x720 up. The local
// player's own aim line and the vortex hint (Player.AimReadout, draw_bounceinfo and draw_vortexdetonate) run along the
// bottom, in practice too.
using System.Collections.Generic;
using System.Text;
using MindAttic.Components.Unity; // MaCanvas, MaText, TextStyle
using Ricochet.Core.Ai;
using Ricochet.Core.Match;
using Ricochet.Core.Menu;
using TMPro;                      // TextAlignmentOptions
using Unity.Multiplayer;          // NetBootstrap, NetSession
using UnityEngine;
using UnityEngine.UI;

namespace Ricochet
{
    public sealed class MatchHud : MonoBehaviour
    {
        // Design sizes in canvas units (the kit's 1920x1080 reference, 1.5x the old IMGUI pixels at 1280x720).
        private const float BaseBarWidth = 225f;
        private const float BarHeight = 15f;
        private const float SlotHeight = 51f;
        private const float NameLineHeight = 27f;
        private const float BaseBarGap = 12f;
        private const float BaseClockGap = 225f;
        private const float SidePadding = 60f;
        private const float TopMargin = 9f;
        private const float DefaultCanvasWidth = 1920f;

        /// <summary>The HUD's canvas order: above the world, below the announcer cards (10) and every menu (90 and up).</summary>
        public const int SortingOrder = 8;

        private static readonly Color Yellow = new Color(1f, 0.92f, 0.2f); // draw_turntime's c_yellow
        private static readonly Color Warning = new Color(1f, 0.2f, 0.2f);

        private sealed class Slot
        {
            public RectTransform Root;
            public Image Turn;
            public MaText Name;
            public MaText Score;
            public Image Back;
            public RectTransform Fill;
            public Image FillImage;
        }

        private readonly List<Player> _seated = new List<Player>();
        private readonly Dictionary<int, double> _shownHealth = new Dictionary<int, double>(); // UR-T67: each seat's drawn health, view-only
        private readonly StringBuilder _order = new StringBuilder();
        private readonly Slot[] _slots = new Slot[MatchRules.MaxSeats];
        private Canvas _canvas;
        private RectTransform _canvasRect;
        private RectTransform _matchRoot;
        private MaText _clock;
        private MaText _caption;
        private MaText _moveWarning;
        private MaText _thinking;
        private MaText _aim;
        private MaText _vortex;
        private CanvasGroup _thinkingFade;

        /// <summary>Adds the HUD to the scene; it draws whatever match <paramref name="session"/> (or the bootstrap's) is in.</summary>
        public static MatchHud Create(NetSession session = null)
        {
            var hud = new GameObject("Match HUD").AddComponent<MatchHud>();
            hud.Session = session;
            hud.Overlay = HudOverlay.Create(hud);
            hud.Recap = EndgameRecap.Create(hud);
            return hud;
        }

        /// <summary>The session read; null for NetBootstrap's.</summary>
        public NetSession Session { get; set; }

        /// <summary>The Components half: the announcer's card and the turn card.</summary>
        public HudOverlay Overlay { get; private set; }

        /// <summary>The endgame recap: the leaderboard once the game's goal is reached (UR-T9).</summary>
        public EndgameRecap Recap { get; private set; }

        /// <summary>The session drawn now: <see cref="Session"/>, or the bootstrap's.</summary>
        public NetSession CurrentSession => Session ?? (NetBootstrap.Instance != null ? NetBootstrap.Instance.Session : null);

        /// <summary>The HUD's canvas (null until there is a match or a local player to draw for).</summary>
        public Canvas Canvas => _canvas;

        /// <summary>How many seat slots are showing now (for tests and logs).</summary>
        public int SlotsShowing
        {
            get
            {
                int count = 0;
                foreach (Slot slot in _slots)
                {
                    if (slot != null && slot.Root.gameObject.activeSelf && _matchRoot.gameObject.activeSelf)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>Where slot <paramref name="index"/> sits now, in canvas units from the canvas's left edge (for tests).</summary>
        public bool TrySlotSpan(int index, out float left, out float right)
        {
            left = right = 0f;
            Slot slot = index >= 0 && index < _slots.Length ? _slots[index] : null;
            if (slot == null || !slot.Root.gameObject.activeSelf)
            {
                return false;
            }

            left = CanvasWidth / 2f + slot.Root.anchoredPosition.x;
            right = left + slot.Root.sizeDelta.x * slot.Root.localScale.x;
            return true;
        }

        /// <summary>The canvas's width in canvas units (1920 at 16:9, less on a narrower window).</summary>
        public float CanvasWidth => _canvasRect != null && _canvasRect.rect.width > 1f ? _canvasRect.rect.width : DefaultCanvasWidth;

        /// <summary>The clock's text now.</summary>
        public string ClockText => _clock != null ? _clock.Tmp.text : string.Empty;

        /// <summary>The caption's text now: the turn order, a result, or the wait for a dropped player.</summary>
        public string CaptionText => _caption != null ? _caption.Tmp.text : string.Empty;

        /// <summary>The local player's aim line now ("" when none shows).</summary>
        public string AimText => _aim != null && _aim.gameObject.activeSelf ? _aim.Tmp.text : string.Empty;

        private void OnDestroy()
        {
            if (_canvas != null)
            {
                Destroy(_canvas.gameObject);
            }
        }

        private void Update()
        {
            NetSession session = CurrentSession;
            Turns turns = Turns.For(session);
            Player local = null;
            _seated.Clear();
            foreach (Player player in Player.In(session))
            {
                if (player.Seat >= 0)
                {
                    _seated.Add(player);
                }

                if (local == null && player.IsLocalHuman)
                {
                    local = player;
                }
            }

            if (_canvas == null)
            {
                if (turns == null && local == null)
                {
                    return; // nothing to show yet: no canvas until there is
                }

                Build();
            }

            // The local player's own lines run in practice as well as in a match.
            string aim = local != null ? local.AimReadout : null;
            string vortex = local != null ? local.VortexHint : null;
            HudParts.Show(_aim, !string.IsNullOrEmpty(aim));
            if (!string.IsNullOrEmpty(aim))
            {
                HudParts.Set(_aim, aim);
            }

            HudParts.Show(_vortex, !string.IsNullOrEmpty(vortex));
            if (!string.IsNullOrEmpty(vortex))
            {
                HudParts.Set(_vortex, vortex);
            }

            HudParts.Show(_matchRoot, turns != null);
            if (turns == null)
            {
                return; // practice: no match, no roster HUD
            }

            _seated.Sort((a, b) => a.Seat.CompareTo(b.Seat));

            // UR-T17: shrink the bars, their gaps and the clock's own gap together so all eight columns fit the canvas.
            float scale = (float)Seating.BarScale(CanvasWidth, BaseBarWidth, BaseBarGap, BaseClockGap, SidePadding);
            float barWidth = BaseBarWidth * scale;
            float barGap = BaseBarGap * scale;
            float clockGap = BaseClockGap * scale;

            // The clock, or the round's result.
            bool over = turns.MatchPhase == MatchPhase.RoundOver || turns.MatchPhase == MatchPhase.GameOver;
            HudParts.Set(_clock, over ? "--.--" : Seating.FormatClock(turns.ShownTimeLeft));

            for (int i = 0; i < _slots.Length; i++)
            {
                bool used = i < _seated.Count;
                HudParts.Show(_slots[i].Root, used);
                if (used)
                {
                    UpdateSlot(_slots[i], i, _seated[i], turns, barWidth, barGap, clockGap, scale);
                }
            }

            HudParts.Set(_caption, Caption(turns));

            // draw_movewarning.gml: driving forfeits the turn's shot.
            bool warn = false;
            if (turns.HasMoved && turns.MatchPhase == MatchPhase.Aiming)
            {
                Player driver = Find(turns.Current);
                warn = driver != null && driver.IsLocalHuman;
            }

            HudParts.Show(_moveWarning, warn);

            Player thinking = turns.ThinkingLine >= 0 && turns.MatchPhase == MatchPhase.Aiming ? Find(turns.Current) : null;
            HudParts.Show(_thinking, thinking != null);
            if (thinking != null)
            {
                HudParts.Set(_thinking, thinking.DisplayName + Ai.ThinkingLine(turns.ThinkingLine));
                _thinkingFade.alpha = 0.5f + Mathf.PingPong(Time.time * 0.6f, 0.5f);
            }
        }

        private void UpdateSlot(Slot slot, int index, Player player, Turns turns, float barWidth, float barGap, float clockGap, float scale)
        {
            Seating.BarSlot(index, out int column, out int row);
            float x = (float)Seating.BarX(0, column, barWidth, barGap, clockGap);
            slot.Root.anchoredPosition = new Vector2(x, -(TopMargin + row * SlotHeight));
            slot.Root.localScale = new Vector3(scale, scale, 1f);
            Color color = player.Color;

            HudParts.Show(slot.Turn, player.HasTurn);

            string tag = player.IsAi ? "[AI] " : string.Empty;
            string state = player.InRound ? player.Health + (player.Away ? " away" : string.Empty) : turns.MatchPhase == MatchPhase.RoundOver || turns.MatchPhase == MatchPhase.GameOver || player.Health <= 0 ? "OUT" : "next round";
            slot.Name.Tmp.color = player.InRound ? color : new Color(color.r, color.g, color.b, 0.45f);
            HudParts.Set(slot.Name, tag + player.DisplayName + "  " + state);
            HudParts.Set(slot.Score, player.Score.ToString()); // draw_scores.gml

            // UR-T67: obj_healthbar eases its drawn value toward the real health instead of snapping to it.
            double shown = _shownHealth.TryGetValue(player.Seat, out double previous) ? previous : player.Health;
            shown = Seating.EaseHealth(shown, player.Health, Time.unscaledDeltaTime);
            _shownHealth[player.Seat] = shown;
            Color fill = color;
            fill.a = player.InRound ? 1f : 0.3f;
            slot.FillImage.color = fill;
            float fraction = Mathf.Clamp01((float)shown / Arena.MatchRules.MaxHealth);
            slot.FillImage.enabled = fraction > 0f; // an empty bar would be an inverted rect
            slot.Fill.anchorMax = new Vector2(fraction, 1f);
        }

        private string Caption(Turns turns)
        {
            if (turns.MatchPhase == MatchPhase.GameOver)
            {
                Player champion = Find(turns.ChampionSeat);
                return "Game over" + (champion != null ? ": " + champion.DisplayName + " wins the game!" : ".");
            }

            if (turns.MatchPhase == MatchPhase.RoundOver)
            {
                Player winner = Find(turns.Winner);
                return winner != null ? winner.DisplayName + " wins round " + turns.Round + "!" : "Nobody survived round " + turns.Round + ".";
            }

            // The turn waits for its dropped player to reconnect (UR-T13).
            if (turns.Waiting)
            {
                Player away = Find(turns.Current);
                return "Waiting for " + (away != null ? away.DisplayName : "a player") + " to reconnect (" + turns.WaitingSeconds + "s)";
            }

            // The living seats from the current one round the table: the turn order.
            _order.Clear().Append("Round ").Append(turns.Round).Append("   ").Append(MatchSetup.DescribeGoal(turns.GoalPoints)).Append("   Turn: ");
            int start = turns.Current;
            bool first = true;
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (Player player in _seated)
                {
                    bool inThisPass = pass == 0 ? player.Seat >= start : player.Seat < start;
                    if (!inThisPass || !player.InRound)
                    {
                        continue;
                    }

                    _order.Append(first ? string.Empty : "  >  ").Append(player.DisplayName);
                    first = false;
                }
            }

            return _order.ToString();
        }

        private Player Find(int seat)
        {
            foreach (Player player in _seated)
            {
                if (player.Seat == seat)
                {
                    return player;
                }
            }

            return null;
        }

        private void Build()
        {
            // Below the announcer cards (10) and the menus (a MenuStack sorts at 90 and up): they cover this, never the reverse.
            _canvas = MaCanvas.Create("Match HUD canvas", SortingOrder);
            _canvas.transform.SetParent(transform, false);
            _canvasRect = (RectTransform)_canvas.transform;

            // The match's own widgets share a root so practice hides them in one go.
            _matchRoot = HudParts.Rect("Match", _canvas.transform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            HudParts.Stretch(_matchRoot);

            _clock = HudParts.Label(_matchRoot, TextStyle.Title, Yellow, 20f, 39f, TextAlignmentOptions.Top, bold: true);
            Place(_clock.RectTransform, new Vector2(0.5f, 1f), new Vector2(210f, 51f), new Vector2(0f, -TopMargin + 3f));

            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i] = BuildSlot(i);
            }

            _caption = HudParts.Label(_matchRoot, TextStyle.Body, Color.white, 12f, 21f, TextAlignmentOptions.Top);
            Place(_caption.RectTransform, new Vector2(0.5f, 1f), new Vector2(1300f, 33f), new Vector2(0f, -(TopMargin + SlotHeight * 2f + 6f)));

            _moveWarning = HudParts.Label(_matchRoot, TextStyle.Body, Warning, 12f, 27f, TextAlignmentOptions.Center, bold: true);
            _moveWarning.Tmp.text = "You cannot move and shoot within the same turn. Press [F] to finish current turn";
            Place(_moveWarning.RectTransform, new Vector2(0.5f, 0f), new Vector2(1500f, 39f), new Vector2(0f, 93f));

            _thinking = HudParts.Label(_matchRoot, TextStyle.Body, Color.white, 12f, 27f, TextAlignmentOptions.Center, bold: true);
            Place(_thinking.RectTransform, new Vector2(0.5f, 0f), new Vector2(1500f, 39f), new Vector2(0f, 150f));
            _thinkingFade = _thinking.gameObject.AddComponent<CanvasGroup>();

            // The local player's own lines, outside the match root: they show in practice too.
            _aim = HudParts.Label(_canvas.transform, TextStyle.Body, Color.white, 11f, 24f, TextAlignmentOptions.MidlineLeft);
            Place(_aim.RectTransform, new Vector2(0f, 0f), new Vector2(1800f, 36f), new Vector2(18f, 12f));
            _vortex = HudParts.Label(_canvas.transform, TextStyle.Body, new Color(1f, 0.35f, 0.3f), 11f, 24f, TextAlignmentOptions.Center);
            Place(_vortex.RectTransform, new Vector2(0.5f, 0f), new Vector2(1500f, 36f), new Vector2(0f, 54f));

            // A HUD never takes the pointer, so it cannot swallow a click meant for a menu or the world.
            CanvasGroup group = _canvas.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            _aim.gameObject.SetActive(false);
            _vortex.gameObject.SetActive(false);
            _moveWarning.gameObject.SetActive(false);
            _thinking.gameObject.SetActive(false);
        }

        private Slot BuildSlot(int index)
        {
            var slot = new Slot();
            slot.Root = HudParts.Rect("Seat " + index, _matchRoot, new Vector2(0.5f, 1f), new Vector2(BaseBarWidth, SlotHeight), Vector2.zero);
            slot.Root.pivot = new Vector2(0f, 1f);
            slot.Turn = HudParts.Solid("Turn outline", slot.Root, new Color(1f, 0.92f, 0.2f, 0.35f)); // the seat on turn
            slot.Turn.rectTransform.anchorMin = Vector2.zero;
            slot.Turn.rectTransform.anchorMax = Vector2.one;
            slot.Turn.rectTransform.offsetMin = new Vector2(-5f, 1f);
            slot.Turn.rectTransform.offsetMax = new Vector2(5f, 3f);

            slot.Name = HudParts.Label(slot.Root, TextStyle.Body, Color.white, 10f, 20f, TextAlignmentOptions.TopLeft);
            Place(slot.Name.RectTransform, new Vector2(0f, 1f), new Vector2(BaseBarWidth - 45f, NameLineHeight), Vector2.zero);
            slot.Score = HudParts.Label(slot.Root, TextStyle.Body, new Color(1f, 1f, 1f, 0.85f), 10f, 20f, TextAlignmentOptions.TopRight, bold: true);
            Place(slot.Score.RectTransform, new Vector2(1f, 1f), new Vector2(42f, NameLineHeight), Vector2.zero);

            slot.Back = HudParts.Solid("Bar", slot.Root, new Color(0f, 0f, 0f, 0.6f));
            RectTransform back = slot.Back.rectTransform;
            Place(back, new Vector2(0f, 1f), new Vector2(BaseBarWidth, BarHeight), new Vector2(0f, -NameLineHeight + 3f));
            slot.FillImage = HudParts.Solid("Health", back, Color.white);
            slot.Fill = slot.FillImage.rectTransform;
            slot.Fill.anchorMin = Vector2.zero;
            slot.Fill.anchorMax = Vector2.one;
            slot.Fill.pivot = new Vector2(0f, 0.5f);
            slot.Fill.offsetMin = new Vector2(1.5f, 1.5f);
            slot.Fill.offsetMax = new Vector2(-1.5f, -1.5f);
            slot.Root.gameObject.SetActive(false);
            return slot;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }
    }
}
