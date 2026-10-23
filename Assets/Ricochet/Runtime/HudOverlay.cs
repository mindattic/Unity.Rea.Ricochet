// The match HUD's MindAttic Components half (UR-T8), on every peer, read from the synced state only (Turns and each
// Player). Two cards on a canvas of their own:
//   The announcer (obj_announcer, obj_announcer_portrait): when Turns carries a new line, the personality's portrait
//   (their colour and monogram; the original had drawn profiles for two of its seven and a placeholder for the rest) and
//   name box slide in from the bottom left with the line, hold, and slide back out, easing as obj_announcer's
//   x += 1 + 0.1 * (onScreen - x) did each step.
//   The turn card (draw_weaponinfo, draw_iteminfo, draw_potential, draw_scores): whose turn it is, in their colour, with
//   their score, the weapon they carry (and a grenade's fuse), their item, and the combo the shot in the air would score.
//   In practice it shows the local player's own loadout. Everyone sees it, as everyone at the original's one screen did.
// The health bars, clock, turn order and the AI's thinking line stay MatchHud's IMGUI: MaProgressBar has no per-player
// colour, and a bar in each player's own colour is the point of them.
using System.Collections.Generic;
using MindAttic.Components.Unity;      // MaCanvas, MaPanel, MaText, PanelStyle, TextStyle
using Ricochet.Core;                   // Announcer, AnnouncerVoice, Announcement, ArsenalRules, Loadout, MatchPhase
using TMPro;                           // TextAlignmentOptions
using Unity.Multiplayer;               // NetSession
using UnityEngine;
using UnityEngine.UI;

namespace Ricochet
{
    public sealed class HudOverlay : MonoBehaviour
    {
        /// <summary>Seconds a line stays up once its card has slid in (the original held it while the recording played).</summary>
        public const float AnnounceSeconds = 4.5f;

        private const float AnnouncerWidth = 820f;
        private const float AnnouncerHeight = 150f;
        private const float TurnCardWidth = 560f;
        private const float Margin = 24f;
        private const float AboveAimLine = 72f;

        private MatchHud _hud;
        private Canvas _canvas;

        private RectTransform _announcerRoot;
        private Image _face;
        private MaText _initials;
        private MaText _announcerName;
        private MaText _announcerLine;
        private float _announcerX = -AnnouncerWidth - 40f;
        private Turns _lastTurns;
        private int _seenAnnounced;
        private float _shownUntil = -1f;

        private RectTransform _turnRoot;
        private MaText _turnTitle;
        private MaText _weaponText;
        private MaText _itemText;
        private MaText _comboText;
        private MaText _hintText;

        private readonly List<Player> _players = new List<Player>();

        /// <summary>Adds the Components half of <paramref name="hud"/>'s HUD to its GameObject.</summary>
        public static HudOverlay Create(MatchHud hud)
        {
            var overlay = hud.gameObject.AddComponent<HudOverlay>();
            overlay._hud = hud;
            return overlay;
        }

        /// <summary>True while the announcer's card is up with a line (sliding in or holding).</summary>
        public bool AnnouncerShowing => Time.time < _shownUntil;

        /// <summary>The name in the announcer's name box ("" before the first line).</summary>
        public string AnnouncerName => _announcerName != null ? _announcerName.Text : string.Empty;

        /// <summary>The announcer's last line, with the players' names in it ("" before the first).</summary>
        public string AnnouncerLine => _announcerLine != null ? _announcerLine.Text : string.Empty;

        /// <summary>True while the turn card is up.</summary>
        public bool TurnCardShowing => _turnRoot != null && _turnRoot.gameObject.activeSelf;

        /// <summary>Everything the turn card says, one line each (for tests and logs).</summary>
        public string TurnCardText => _turnRoot == null ? string.Empty
            : _turnTitle.Text + "\n" + _weaponText.Text + "\n" + _itemText.Text + "\n" + _comboText.Text;

        /// <summary>The canvas the cards are on (null until the first frame).</summary>
        public Canvas Canvas => _canvas;

        private void OnDestroy()
        {
            if (_canvas != null)
            {
                Destroy(_canvas.gameObject);
            }
        }

        private void Update()
        {
            NetSession session = _hud != null ? _hud.CurrentSession : null;
            if (session == null || !session.IsLive)
            {
                if (_canvas != null)
                {
                    _canvas.gameObject.SetActive(false);
                }

                return;
            }

            Build();
            _canvas.gameObject.SetActive(true);
            _players.Clear();
            _players.AddRange(Player.In(session));

            Turns turns = Turns.For(session);
            UpdateAnnouncer(turns);
            UpdateTurnCard(turns);
        }

        private void UpdateAnnouncer(Turns turns)
        {
            if (turns != _lastTurns)
            {
                _lastTurns = turns;
                _seenAnnounced = 0; // a new match's Turns: show its latest line (a welcome), however it arrived
            }

            if (turns != null && turns.Announced != _seenAnnounced)
            {
                _seenAnnounced = turns.Announced;
                AnnouncerVoice voice = Announcer.VoiceAt(turns.Voice);
                _announcerName.Text = voice.Name;
                _initials.Text = voice.Initials;
                _face.color = new Color32(voice.R, voice.G, voice.B, 255);
                _announcerLine.Text = Announcer.Text(turns.LastAnnouncement, NameOf);
                _shownUntil = Time.time + AnnounceSeconds;
            }

            if (turns == null)
            {
                _shownUntil = -1f;
            }

            // obj_announcer's step: ease towards on screen while the line plays, back off screen after.
            float target = AnnouncerShowing ? Margin : -AnnouncerWidth - 40f;
            float distance = target - _announcerX;
            float step = (60f + 6f * Mathf.Abs(distance)) * Time.unscaledDeltaTime;
            _announcerX = Mathf.Abs(distance) <= step ? target : _announcerX + Mathf.Sign(distance) * step;
            _announcerRoot.anchoredPosition = new Vector2(_announcerX, AboveAimLine);
            _announcerRoot.gameObject.SetActive(_announcerX > -AnnouncerWidth - 39f);
        }

        private void UpdateTurnCard(Turns turns)
        {
            Player subject = null;
            if (turns == null)
            {
                foreach (Player player in _players)
                {
                    if (player.IsLocalHuman)
                    {
                        subject = player;
                    }
                }
            }
            else if (turns.MatchPhase != MatchPhase.RoundOver && turns.MatchPhase != MatchPhase.Practice)
            {
                foreach (Player player in _players)
                {
                    if (player.Seat >= 0 && player.Seat == turns.Current)
                    {
                        subject = player;
                    }
                }
            }

            _turnRoot.gameObject.SetActive(subject != null);
            if (subject == null)
            {
                return;
            }

            Color color = subject.Color;
            _turnTitle.WithColor(color);
            _turnTitle.Text = turns == null
                ? "Practice: " + subject.DisplayName
                : subject.DisplayName + (subject.IsLocalHuman ? " (your turn)" : "'s turn") + "   Score " + subject.Score;
            _weaponText.Text = WeaponLine(subject.Loadout);
            _itemText.Text = ItemLine(subject.Loadout, subject.ExtraBounces);

            // draw_potential: what the shot in the air would score if it hit now (0 before it is fired).
            int bounces = subject.BouncesInFlight;
            int combo = bounces >= 0 ? Arena.MatchRules.Points(bounces, subject.SecondsInFlight) : 0;
            _comboText.Text = "Combo: " + combo;
            _hintText.Visible = subject.IsLocalHuman;
            _hintText.Text = "Q/E weapon   Z/C item   F use item   Space fire";
        }

        /// <summary>draw_weaponinfo: "Weapon: Grenade x3   Fuse 2.0 s".</summary>
        public static string WeaponLine(Loadout loadout)
        {
            if (loadout == null)
            {
                return "Weapon: -";
            }

            return "Weapon: " + ArsenalRules.Name(loadout.Weapon) + " x" + ArsenalRules.CountText(loadout.Count(loadout.Weapon))
                + (loadout.Weapon == Weapon.Grenade ? "   Fuse " + loadout.Fuse.ToString("0.0") + " s" : string.Empty);
        }

        /// <summary>draw_iteminfo: "Item: Teleporter x2   +1 bounce", or "Item: none".</summary>
        public static string ItemLine(Loadout loadout, int extraBounces)
        {
            string item = loadout != null && loadout.Has(loadout.Item)
                ? ArsenalRules.Name(loadout.Item) + " x" + ArsenalRules.CountText(loadout.Count(loadout.Item))
                : "none";
            return "Item: " + item + (extraBounces > 0 ? "   +" + extraBounces + " bounce" + (extraBounces == 1 ? string.Empty : "s") : string.Empty);
        }

        private string NameOf(int seat)
        {
            foreach (Player player in _players)
            {
                if (player.Seat == seat)
                {
                    return player.DisplayName;
                }
            }

            return null;
        }

        private void Build()
        {
            if (_canvas != null)
            {
                return;
            }

            // Below the menus (a MenuStack sorts at 100) and the kit's overlay; IMGUI draws over all of it.
            _canvas = MaCanvas.Create("Ricochet HUD", 10);
            _canvas.transform.SetParent(transform, false);


            // The announcer: portrait + name box + line, anchored bottom left, off screen until a line comes.
            _announcerRoot = Root("Announcer", new Vector2(0f, 0f), new Vector2(AnnouncerWidth, AnnouncerHeight));
            MaPanel card = MaPanel.Create(_announcerRoot, PanelStyle.Surface, "Card");
            Stretch(card.RectTransform, 0f);
            HorizontalLayoutGroup row = card.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 24f;
            row.padding = new RectOffset(20, 20, 16, 16);
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;

            MaPanel portrait = MaPanel.Create(card, PanelStyle.Raised, "Portrait");
            LayoutElement portraitSize = portrait.LayoutElement;
            portraitSize.minWidth = portraitSize.preferredWidth = 118f;
            portraitSize.minHeight = portraitSize.preferredHeight = 118f;
            portraitSize.flexibleWidth = portraitSize.flexibleHeight = 0f;
            _face = new GameObject("Face", typeof(RectTransform)).AddComponent<Image>();
            _face.gameObject.layer = 5; // UI
            _face.raycastTarget = false;
            _face.transform.SetParent(portrait.transform, false);
            Stretch((RectTransform)_face.transform, 6f);
            _initials = MaText.Create(portrait, string.Empty, TextStyle.Title, TextAlignmentOptions.Center).WithColor(Color.white);
            Stretch(_initials.RectTransform, 0f);

            var words = new GameObject("Name box", typeof(RectTransform)).GetComponent<RectTransform>();
            words.gameObject.layer = 5;
            words.SetParent(card.transform, false);
            VerticalLayoutGroup column = words.gameObject.AddComponent<VerticalLayoutGroup>();
            column.spacing = 6f;
            column.childAlignment = TextAnchor.MiddleLeft;
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            words.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            _announcerName = MaText.Create(words, string.Empty, TextStyle.Heading);
            _announcerLine = MaText.Create(words, string.Empty, TextStyle.Body);
            _announcerRoot.anchoredPosition = new Vector2(_announcerX, AboveAimLine);
            _announcerRoot.gameObject.SetActive(false);

            // The turn card, bottom right, as tall as its lines.
            _turnRoot = Root("Turn card", new Vector2(1f, 0f), new Vector2(TurnCardWidth, 0f));
            _turnRoot.anchoredPosition = new Vector2(-Margin, AboveAimLine);
            VerticalLayoutGroup fit = _turnRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            fit.childControlWidth = fit.childControlHeight = true;
            fit.childForceExpandWidth = true;
            fit.childForceExpandHeight = false;
            _turnRoot.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            MaPanel turnCard = MaPanel.Create(_turnRoot, PanelStyle.Surface, "Card");
            VerticalLayoutGroup lines = turnCard.gameObject.AddComponent<VerticalLayoutGroup>();
            lines.spacing = 6f;
            lines.padding = new RectOffset(24, 24, 16, 16);
            lines.childAlignment = TextAnchor.UpperLeft;
            lines.childControlWidth = lines.childControlHeight = true;
            lines.childForceExpandWidth = true;
            lines.childForceExpandHeight = false;
            _turnTitle = MaText.Create(turnCard, string.Empty, TextStyle.Heading);
            _weaponText = MaText.Create(turnCard, string.Empty, TextStyle.Body);
            _itemText = MaText.Create(turnCard, string.Empty, TextStyle.Body);
            _comboText = MaText.Create(turnCard, string.Empty, TextStyle.Muted);
            _hintText = MaText.Create(turnCard, string.Empty, TextStyle.Caption);
            _turnRoot.gameObject.SetActive(false);
        }

        // A card's root: a rect anchored and pivoted at a bottom corner that never takes the pointer.
        private RectTransform Root(string name, Vector2 corner, Vector2 size)
        {
            var root = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            root.gameObject.layer = 5;
            root.SetParent(_canvas.transform, false);
            root.anchorMin = root.anchorMax = root.pivot = corner;
            root.sizeDelta = size;
            CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            return root;
        }

        // Fills the parent, inset by the margin on every side.
        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            LayoutElement ignore = rect.GetComponent<LayoutElement>();
            if (ignore == null)
            {
                ignore = rect.gameObject.AddComponent<LayoutElement>();
            }

            ignore.ignoreLayout = true;
        }
    }
}
