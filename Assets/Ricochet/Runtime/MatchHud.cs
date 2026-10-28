// The match HUD on every peer, read from the synced state only (Turns and each Player), built in code as a uGUI canvas
// of MindAttic Components labels (UR-T38; it was IMGUI): the turn clock at the top centre (draw_turntime.gml's yellow
// "SS.FF"), a health bar per seat in two rows of eight either side of it, each in the player's colour with their name and
// an AI tag (setup_players.gml's obj_healthbar row and draw_scores.gml's icons), the seat whose turn it is outlined, and
// under the clock the order the living seats play in from now. On an AI seat's turn, obj_ai_gui's line along the bottom:
// "AI 2 is plotting and scheming...", pulsing between half and full. Each slot also carries the seat's match score
// (draw_scores.gml's "name: score", UR-T8). The announcer's portrait and name box and the turn card are HudOverlay's
// widgets, on a canvas of their own; the turn-start Ready card and ping are ReadyCard's (UR-T42). Once the game's goal is reached (UR-T9) the caption names the champion and the
// endgame recap (EndgameRecap) comes up. A seat whose player dropped reads "away" (UR-T13), and while the turn waits for
// them the line under the clock says so, with the seconds they have left to reconnect. The canvas sorts under the kit's
// menus and dialogs (UR-T18), so the Esc menu, the main menu and a modal simply cover it. It lays out on the kit's
// 1920x1080 reference and shrinks the bars, their gaps and the clock's own gap together (Seating.BarScale, UR-T17) when
// the canvas is narrower than the eight columns need, so all 8 seats stay on screen from 1280x720 up. The local
// player's own aim line and the vortex hint (Player.AimReadout, draw_bounceinfo and draw_vortexdetonate) run along the
// bottom, in practice too. U.RICO-T49: each slot's bar is obj_healthbar's art with its "shown/max", the AI tag is
// draw_scores' spr_icon_computer (spr_icon_player for a human), and obj_turnarrow stands beside the current seat; the weapon
// and item icons are HudIcons and the guide, angle and extra-bounce indicator at the current tank are TurnGuide.
using System.Collections.Generic;
using System.Text;
using MindAttic.Components.Unity; // MaCanvas, MaText, TextStyle
using Ricochet.Core.Ai;
using Ricochet.Core.Art;
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
        // Design sizes in canvas units (the kit's 1920x1080 reference, which is the original room pixel for pixel). U.RICO-T49:
        // a slot is obj_turnarrow's 32 px column, then draw_scores' 16 px human or computer mark and "name  score" over
        // obj_healthbar's 171x25 bar (spr_healthbar_back, _fill in the player's colour, spr_healthbar, "shown/max").
        private const float ArrowSpace = 32f;
        private const float BarWidth = 171f;
        private const float BaseBarWidth = ArrowSpace + BarWidth;
        private const float BarHeight = 25f;
        private const float MarkSize = 16f;
        private const float SlotHeight = 54f;
        private const float NameLineHeight = 27f;
        private const float BaseBarGap = 12f;
        private const float BaseClockGap = 225f;
        private const float SidePadding = 60f;
        private const float TopMargin = 9f;
        private const float DefaultCanvasWidth = 1920f;
        private const float AimLineLeft = 290f;

        /// <summary>The HUD's canvas order: above the world, below the announcer cards (10) and every menu (90 and up).</summary>
        public const int SortingOrder = 8;

        private static readonly Color Yellow = new Color(1f, 0.92f, 0.2f); // draw_turntime's c_yellow
        private static readonly Color Warning = new Color(1f, 0.2f, 0.2f);

        private sealed class Slot
        {
            public RectTransform Root;
            public Image Turn;
            public Image Arrow;
            public Image Mark;
            public MaText Name;
            public MaText Score;
            public Image Back;
            public RectTransform Fill;
            public Image FillImage;
            public Image Frame;
            public MaText Health;
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
            hud.Ready = ReadyCard.Create(hud);
            hud.Fog = FogOfWar.Create(hud);
            hud.Pause = PauseMenu.Create(hud);
            hud.Pad = ControlPad.Create(hud);
            hud.Icons = HudIcons.Create(hud);
            hud.Guide = TurnGuide.Create(hud);
            return hud;
        }

        /// <summary>The weapon and item icons along the bottom left (U.RICO-T49).</summary>
        public HudIcons Icons { get; private set; }

        /// <summary>obj_guide's rings, draw_angle's readout and the extra-bounce indicator at the current tank (U.RICO-T49).</summary>
        public TurnGuide Guide { get; private set; }

        /// <summary>The health text in seat slot <paramref name="index"/> ("" when it is not showing), obj_healthbar's "shown/max".</summary>
        public string HealthTextOf(int index)
        {
            Slot slot = index >= 0 && index < _slots.Length ? _slots[index] : null;
            return slot != null && slot.Root.gameObject.activeSelf ? slot.Health.Tmp.text : string.Empty;
        }

        /// <summary>True while seat slot <paramref name="index"/> shows obj_turnarrow beside it.</summary>
        public bool ArrowShowingAt(int index)
        {
            Slot slot = index >= 0 && index < _slots.Length ? _slots[index] : null;
            return slot != null && slot.Root.gameObject.activeSelf && slot.Arrow.gameObject.activeSelf;
        }

        /// <summary>True when seat slot <paramref name="index"/> draws its bar from the original's spr_healthbar art.</summary>
        public bool BarFromArtAt(int index)
        {
            Slot slot = index >= 0 && index < _slots.Length ? _slots[index] : null;
            return slot != null && slot.Frame.sprite != null && slot.FillImage.sprite != null;
        }

        /// <summary>The clickable on-screen controls: turn, drive, Fire, Use Item and End Turn (UR-T29).</summary>
        public ControlPad Pad { get; private set; }

        /// <summary>The window mode F1 switches to from the given one (the original's window_set_fullscreen(!window_get_fullscreen())).</summary>
        public static FullScreenMode ToggledFullscreen(FullScreenMode mode) =>
            mode == FullScreenMode.Windowed ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;

        /// <summary>The pause menu (UR-T43): up on every peer while the match is paused.</summary>
        public PauseMenu Pause { get; private set; }

        /// <summary>The fog of war that hides the arena around the local player's tank (UR-T26).</summary>
        public FogOfWar Fog { get; private set; }

        /// <summary>The turn-start Ready card and ping (UR-T42).</summary>
        public ReadyCard Ready { get; private set; }

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
            if (GameInput.Fullscreen())
            {
                Screen.fullScreenMode = ToggledFullscreen(Screen.fullScreenMode); // F1
            }

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
            string vortex = local != null ? local.VortexHint ?? local.GrenadeHint : null; // draw_vortexdetonate / draw_grenadefuse
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

            // obj_turnarrow beside the current player's score (spr_turnarrow's six frames at image_speed 0.2), U.RICO-T49.
            Sprite arrow = player.HasTurn ? GameArt.FrameAt(GmlSprites.TurnArrow, Time.time, true, (float)HudLooks.TurnArrowImageSpeed) : null;
            HudParts.Show(slot.Arrow, arrow != null);
            if (arrow != null)
            {
                slot.Arrow.sprite = arrow;
            }

            // draw_scores: spr_icon_computer for an AI, spr_icon_player for a human (the "[AI]" tag when the art is missing).
            Sprite mark = GameArt.Frame(player.IsAi ? GmlSprites.IconComputer : GmlSprites.IconPlayer);
            slot.Mark.sprite = mark;
            HudParts.Show(slot.Mark, mark != null);
            string tag = player.IsAi && mark == null ? "[AI] " : string.Empty;
            string state = player.InRound ? (player.Away ? "away" : string.Empty) : turns.MatchPhase == MatchPhase.RoundOver || turns.MatchPhase == MatchPhase.GameOver || player.Health <= 0 ? "OUT" : "next round";
            slot.Name.Tmp.color = player.InRound ? color : new Color(color.r, color.g, color.b, 0.45f);
            HudParts.Set(slot.Name, tag + player.DisplayName + (state.Length > 0 ? "  " + state : string.Empty));
            HudParts.Set(slot.Score, player.Score.ToString()); // draw_scores.gml

            // UR-T67: obj_healthbar eases its drawn value toward the real health instead of snapping to it.
            double shown = _shownHealth.TryGetValue(player.Seat, out double previous) ? previous : player.Health;
            shown = Seating.EaseHealth(shown, player.Health, Time.unscaledDeltaTime);
            _shownHealth[player.Seat] = shown;
            Color fill = color;
            fill.a = player.InRound ? 1f : 0.3f;
            slot.FillImage.color = fill;
            int max = Arena.MatchRules.MaxHealth;
            float fraction = (float)HudLooks.HealthFill(shown, max);
            slot.FillImage.enabled = fraction > 0f; // an empty bar would be an inverted rect
            slot.Fill.anchorMax = new Vector2(fraction, 1f);
            HudParts.Set(slot.Health, HudLooks.HealthText(Mathf.RoundToInt((float)shown), max));
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

            _clock = HudParts.Label(_matchRoot, TextStyle.Title, Yellow, 20f, 39f, TextAlignmentOptions.Top, bold: true, gmlFont: GmlFonts.Score);
            Place(_clock.RectTransform, new Vector2(0.5f, 1f), new Vector2(210f, 51f), new Vector2(0f, -TopMargin + 3f));

            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i] = BuildSlot(i);
            }

            _caption = HudParts.Label(_matchRoot, TextStyle.Body, Color.white, 12f, 21f, TextAlignmentOptions.Top, gmlFont: GmlFonts.Score);
            Place(_caption.RectTransform, new Vector2(0.5f, 1f), new Vector2(1300f, 33f), new Vector2(0f, -(TopMargin + SlotHeight * 2f + 6f)));

            _moveWarning = HudParts.Label(_matchRoot, TextStyle.Body, Warning, 12f, 27f, TextAlignmentOptions.Center, bold: true, gmlFont: GmlFonts.Score);
            _moveWarning.Tmp.text = "You cannot move and shoot within the same turn. Press [F] to finish current turn";
            Place(_moveWarning.RectTransform, new Vector2(0.5f, 0f), new Vector2(1500f, 39f), new Vector2(0f, 93f));

            _thinking = HudParts.Label(_matchRoot, TextStyle.Body, Color.white, 12f, 27f, TextAlignmentOptions.Center, bold: true, gmlFont: GmlFonts.Score);
            Place(_thinking.RectTransform, new Vector2(0.5f, 0f), new Vector2(1500f, 39f), new Vector2(0f, 150f));
            _thinkingFade = _thinking.gameObject.AddComponent<CanvasGroup>();

            // The local player's own lines, outside the match root: they show in practice too.
            _aim = HudParts.Label(_canvas.transform, TextStyle.Body, Color.white, 11f, 24f, TextAlignmentOptions.MidlineLeft, gmlFont: GmlFonts.Score, outline: 1);
            // Right of the weapon and item icons (U.RICO-T49), where draw_weaponinfo wrote at x 290, and clear of the pad.
            Place(_aim.RectTransform, new Vector2(0f, 0f), new Vector2(1180f, 36f), new Vector2(AimLineLeft, 12f));
            _vortex = HudParts.Label(_canvas.transform, TextStyle.Body, new Color(1f, 0.35f, 0.3f), 11f, 24f, TextAlignmentOptions.Center, gmlFont: GmlFonts.Score);
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
            slot.Turn.rectTransform.offsetMin = new Vector2(ArrowSpace - 5f, 1f);
            slot.Turn.rectTransform.offsetMax = new Vector2(5f, 3f);

            // obj_turnarrow (32x32) in the column left of the mark, as the original drew it at x - sprite_width.
            slot.Arrow = HudParts.Solid("Turn arrow", slot.Root, Color.white);
            Place(slot.Arrow.rectTransform, new Vector2(0f, 1f), new Vector2(ArrowSpace, ArrowSpace), new Vector2(0f, 2f));
            slot.Arrow.gameObject.SetActive(false);

            // draw_scores' 16x16 mark, then the name and the score.
            slot.Mark = HudParts.Solid("Mark", slot.Root, Color.white);
            Place(slot.Mark.rectTransform, new Vector2(0f, 1f), new Vector2(MarkSize, MarkSize), new Vector2(ArrowSpace, -4f));
            float nameLeft = ArrowSpace + MarkSize + 3f;
            slot.Name = HudParts.Label(slot.Root, TextStyle.Body, Color.white, 10f, 20f, TextAlignmentOptions.TopLeft, gmlFont: GmlFonts.Score);
            Place(slot.Name.RectTransform, new Vector2(0f, 1f), new Vector2(BaseBarWidth - nameLeft - 40f, NameLineHeight), new Vector2(nameLeft, 0f));
            slot.Score = HudParts.Label(slot.Root, TextStyle.Body, new Color(1f, 1f, 1f, 0.85f), 10f, 20f, TextAlignmentOptions.TopRight, bold: true, gmlFont: GmlFonts.Score);
            Place(slot.Score.RectTransform, new Vector2(1f, 1f), new Vector2(38f, NameLineHeight), Vector2.zero);

            // obj_healthbar: spr_healthbar_back at 0.25, spr_healthbar_fill in the player's colour scaled to the health shown
            // (image_xscale), spr_healthbar over them, and "shown/max" in fnt_count outlined 1 px, centred, 4 px down. Code-drawn
            // rects when the art is missing.
            Sprite backArt = GameArt.Frame(GmlSprites.HealthBarBack);
            Sprite fillArt = GameArt.Frame(GmlSprites.HealthBarFill);
            Sprite frameArt = GameArt.Frame(GmlSprites.HealthBar);
            slot.Back = HudParts.Solid("Bar", slot.Root, backArt != null ? new Color(1f, 1f, 1f, (float)HudLooks.HealthBackAlpha) : new Color(0f, 0f, 0f, 0.6f));
            slot.Back.sprite = backArt;
            RectTransform back = slot.Back.rectTransform;
            Place(back, new Vector2(0f, 1f), new Vector2(BarWidth, BarHeight), new Vector2(ArrowSpace, -NameLineHeight));
            var fillBox = HudParts.Rect("Fill box", slot.Root, new Vector2(0f, 1f), new Vector2(BarWidth, BarHeight), new Vector2(ArrowSpace, -NameLineHeight));
            slot.FillImage = HudParts.Solid("Health", fillBox, Color.white);
            slot.FillImage.sprite = fillArt;
            slot.Fill = slot.FillImage.rectTransform;
            slot.Fill.anchorMin = Vector2.zero;
            slot.Fill.anchorMax = Vector2.one;
            slot.Fill.pivot = new Vector2(0f, 0.5f);
            float inset = fillArt != null ? 0f : 1.5f;
            slot.Fill.offsetMin = new Vector2(inset, inset);
            slot.Fill.offsetMax = new Vector2(-inset, -inset);
            slot.Frame = HudParts.Solid("Frame", slot.Root, Color.white);
            slot.Frame.sprite = frameArt;
            Place(slot.Frame.rectTransform, new Vector2(0f, 1f), new Vector2(BarWidth, BarHeight), new Vector2(ArrowSpace, -NameLineHeight));
            slot.Frame.enabled = frameArt != null;
            slot.Health = HudParts.Label(slot.Root, TextStyle.Body, Color.white, 8f, GameArt.CanvasFontSize(GmlFonts.Count, 16f), TextAlignmentOptions.Top,
                gmlFont: GmlFonts.Count, outline: HudLooks.HealthOutline);
            Place(slot.Health.RectTransform, new Vector2(0f, 1f), new Vector2(BarWidth, BarHeight), new Vector2(ArrowSpace, -NameLineHeight - HudLooks.HealthTextOffsetY));
            slot.Health.Tmp.overflowMode = TextOverflowModes.Overflow;
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
