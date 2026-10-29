// The endgame room (UR-T9, the podium U.RICO-T56) on every peer, read from the synced state only (Turns and each Player):
// once the game's goal is reached (MatchPhase.GameOver) obj_endgame's room comes up over the game, as Core's Podium lays
// it out: bg_stadium tiled, obj_pedastal at the middle with the first three of the ranking on it as their tanks in their
// colours (first at the centre, second left, third right; the champion's turret spinning a degree a step), "Wins!" under
// the champion's name, and the leaderboard at the top, every seat ranked by score in its colour (obj_endgame's
// ds_grid_sort of pScore and its "1st Ann 740" rows). Its buttons are the endgame room's way out and then some: the main
// menu (obj_btn_back_endgame at (128, 1024): leave the game), a vote for a rematch (the restart vote: the same players,
// map and goal from round one), and Close, to look at the level. As it comes up the crowd cheers (sfx_woot and
// sfx_applause), GameAudio plays music_victory once through, obj_flashemitter's camera flashes go off over it
// (EndgameFlashes, U.RICO-T79) and it fades in out of black (obj_fade_endgame, U.RICO-T53). It comes up again for the
// next game that ends. Presentation only.
using System.Collections.Generic;
using MindAttic.Components.Unity;       // MaCanvas, MaButton
using MindAttic.Components.Unity.Core; // ButtonVariant
using Ricochet.Core.Art;
using Ricochet.Core.Match;
using Ricochet.Core.Menu;
using TMPro;
using Unity.Multiplayer;                // NetSession, NetVotes
using Unity.Multiplayer.UI;             // NetFrontEnd
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ricochet
{
    public sealed class EndgameRecap : MonoBehaviour
    {
        /// <summary>Over the HUD's cards (10), under the flashes (91) and the kit's menus and dialogs.</summary>
        public const int SortingOrder = 90;

        /// <summary>The names of the objects it adds, for tests.</summary>
        public const string CanvasName = "Endgame";
        public const string RoomName = "Endgame room";

        private MatchHud _hud;
        private EndgameFlashes _flashes;
        private Canvas _canvas;
        private RectTransform _canvasRect;
        private RectTransform _room;
        private Image _backdrop;
        private RectTransform _content;
        private MatchPhase _lastPhase = MatchPhase.Practice;
        private Turns _lastTurns;
        private float _clock;
        private long _steps;
        private readonly List<string> _lines = new List<string>();
        private readonly List<Image> _turrets = new List<Image>();
        private readonly List<MaButton> _buttons = new List<MaButton>();
        private MaButton _rematch;

        /// <summary>Adds the recap to <paramref name="hud"/>'s GameObject; it shows whatever game the HUD's session ends.</summary>
        public static EndgameRecap Create(MatchHud hud)
        {
            var recap = hud.gameObject.AddComponent<EndgameRecap>();
            recap._hud = hud;
            recap._flashes = hud.gameObject.AddComponent<EndgameFlashes>(); // U.RICO-T79: obj_flashemitter's camera flashes
            return recap;
        }

        /// <summary>True while the recap is up.</summary>
        public bool Showing => _canvas != null && _canvas.gameObject.activeInHierarchy;

        /// <summary>"Ann wins!" ("" before a game has ended).</summary>
        public string Headline { get; private set; } = string.Empty;

        /// <summary>The leaderboard, a line each, first place first.</summary>
        public IReadOnlyList<string> Lines => _lines;

        /// <summary>The endgame room's camera flashes, thrown while the recap is up.</summary>
        public EndgameFlashes Flashes => _flashes;

        /// <summary>The room's canvas (null until a game has ended).</summary>
        public Canvas Canvas => _canvas;

        /// <summary>The turrets on the podium, first place first (at most three).</summary>
        public IReadOnlyList<Image> Turrets => _turrets;

        /// <summary>The room's buttons (Main Menu, then the rematch vote when there is one, then Close).</summary>
        public IReadOnlyList<MaButton> Buttons => _buttons;

        /// <summary>Whole room steps since the recap came up (the champion's spin).</summary>
        public long Steps => _steps;

        /// <summary>Closes the recap (the game stays over until a new match starts).</summary>
        public void Close()
        {
            if (_flashes != null)
            {
                _flashes.End();
            }

            if (_canvas != null)
            {
                _canvas.gameObject.SetActive(false);
            }
        }

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
            Turns turns = session != null && session.IsLive ? Turns.For(session) : null;
            MatchPhase phase = turns != null ? turns.MatchPhase : MatchPhase.Practice;
            if (turns != _lastTurns)
            {
                _lastTurns = turns;
                _lastPhase = MatchPhase.Practice;
            }

            if (phase == MatchPhase.GameOver && _lastPhase != MatchPhase.GameOver)
            {
                Show(session, turns);
            }
            else if (phase != MatchPhase.GameOver && Showing)
            {
                Close(); // a rematch or a new match started
            }

            _lastPhase = phase;
            if (Showing)
            {
                Fit();
                Spin();
            }
        }

        // obj_endgame's Step: spinAngle -= 1; firstplace.image_angle = spinAngle.
        private void Spin()
        {
            _clock += Time.deltaTime * Podium.StepsPerSecond;
            while (_clock >= 1f)
            {
                _clock -= 1f;
                _steps++;
            }

            for (int place = 0; place < _turrets.Count; place++)
            {
                _turrets[place].rectTransform.localRotation = Quaternion.Euler(0f, 0f, (float)Podium.Angle(place, _steps));
            }
        }

        // The 1920 x 1080 room scaled whole into the screen, centred, with bg_stadium tiled over all of it.
        private void Fit()
        {
            if (_canvasRect == null || _room == null)
            {
                return;
            }

            Rect area = _canvasRect.rect;
            float scale = Mathf.Min(area.width / Podium.RoomWidth, area.height / Podium.RoomHeight);
            if (scale <= 0f || float.IsNaN(scale))
            {
                return;
            }

            _room.localScale = new Vector3(scale, scale, 1f);
            if (_backdrop != null && _backdrop.sprite != null)
            {
                // A tile is the background's own size in room pixels at the room's scale.
                _backdrop.pixelsPerUnitMultiplier = 100f / _backdrop.sprite.pixelsPerUnit / scale;
            }
        }

        private void Show(NetSession session, Turns turns)
        {
            var entries = new List<RecapEntry>();
            var colours = new Dictionary<int, Color>();
            foreach (Player player in Player.In(session))
            {
                if (player.Seat >= 0)
                {
                    entries.Add(new RecapEntry(player.Seat, player.DisplayName, player.Score, player.IsAi));
                    colours[player.Seat] = player.Color;
                }
            }

            List<RecapEntry> ranked = Recap.Rank(entries);
            Headline = Recap.Headline(ranked);
            _lines.Clear();
            for (int i = 0; i < ranked.Count; i++)
            {
                _lines.Add(Recap.Line(i + 1, ranked[i]));
            }

            EnsureRoom();
            Build(ranked, colours);
            _canvas.gameObject.SetActive(true);
            _clock = 0f;
            _steps = 0;
            Fit();
            Spin();
            SelectFirstButton();

            foreach (SoundCue cue in Podium.Cheer)
            {
                GameAudio.Play(cue); // the crowd as the podium comes up (the BIBLE's Endgame podium)
            }

            ScreenFade.Open(); // U.RICO-T53: the endgame room comes up out of black (obj_fade_endgame)
            if (_flashes != null)
            {
                _flashes.Begin();
            }
        }

        private void EnsureRoom()
        {
            if (_canvas != null)
            {
                return;
            }

            _canvas = MaCanvas.Create(CanvasName, SortingOrder);
            _canvas.transform.SetParent(transform, false);
            _canvasRect = (RectTransform)_canvas.transform;

            // The room's colour, then bg_stadium tiled over the whole screen (it also takes the clicks meant for the game).
            Image colour = NewImage("Endgame room colour", _canvasRect, null);
            Stretch(colour.rectTransform);
            colour.color = new Color32(0xC0, 0xC0, 0xC0, 0xFF);
            colour.raycastTarget = true;
            Sprite stadium = GameArt.Background(Podium.Background);
            if (stadium != null)
            {
                _backdrop = NewImage("Endgame room background", _canvasRect, stadium);
                Stretch(_backdrop.rectTransform);
                _backdrop.type = Image.Type.Tiled;
                _backdrop.preserveAspect = false;
            }

            _room = new GameObject(RoomName, typeof(RectTransform)).GetComponent<RectTransform>();
            _room.gameObject.layer = _canvas.gameObject.layer;
            _room.SetParent(_canvasRect, false);
            _room.anchorMin = _room.anchorMax = new Vector2(0.5f, 0.5f);
            _room.pivot = new Vector2(0.5f, 0.5f);
            _room.sizeDelta = new Vector2(Podium.RoomWidth, Podium.RoomHeight);
            _room.anchoredPosition = Vector2.zero;
        }

        private void Build(List<RecapEntry> ranked, Dictionary<int, Color> colours)
        {
            if (_content != null)
            {
                Destroy(_content.gameObject);
            }

            _turrets.Clear();
            _buttons.Clear();
            _rematch = null;
            _content = new GameObject("Endgame podium", typeof(RectTransform)).GetComponent<RectTransform>();
            _content.gameObject.layer = _room.gameObject.layer;
            _content.SetParent(_room, false);
            Stretch(_content);

            // Depth order: obj_pedastal (100) under every obj_winner_bottom (1) under every obj_winner_top (0), then the text.
            AddSprite("Pedestal", Podium.PedestalSprite, Podium.Pedestal, Color.white, 0f);
            int standing = Podium.OnPodium(ranked.Count);
            for (int place = 0; place < standing; place++)
            {
                AddSprite("Hull " + Recap.Place(place + 1), GmlSprites.TankBottom, Podium.Spot(place), ColourOf(ranked[place], colours), 0f);
            }

            for (int place = 0; place < standing; place++)
            {
                Image turret = AddSprite("Turret " + Recap.Place(place + 1), GmlSprites.TankTop, Podium.Spot(place),
                    ColourOf(ranked[place], colours), (float)Podium.TurretAngle);
                if (turret != null)
                {
                    _turrets.Add(turret);
                }
            }

            // "Wins!" with the champion's name a border above it.
            AddText(Podium.WinsText, GmlFonts.Impact, 2, Podium.Wins.X, Podium.Wins.Y, TextAlignmentOptions.Top, Color.white, 48f);
            if (ranked.Count > 0)
            {
                AddText(ranked[0].Name, GmlFonts.Mono, 2, Podium.ChampionName.X, Podium.ChampionName.Y, TextAlignmentOptions.Top,
                    ColourOf(ranked[0], colours), 32f);
            }

            // The leaderboard: the headings, then a row a seat (at most eight, U.RICO-T100).
            AddRow(Podium.HeaderY, Podium.RankHeading, Podium.PlayerHeading, Podium.ScoreHeading, Color.white);
            for (int i = 0; i < ranked.Count; i++)
            {
                RecapEntry entry = ranked[i];
                AddRow(Podium.RowY(i), Recap.Place(i + 1), (entry.IsAi ? "[AI] " : string.Empty) + entry.Name, entry.Score.ToString(),
                    ColourOf(entry, colours));
            }

            // The way out.
            if (NetFrontEnd.Instance != null && NetFrontEnd.Instance.HasMenus)
            {
                AddButton(Podium.MainMenuLabel, Podium.MainMenuButton, ButtonVariant.Secondary, () =>
                {
                    Close();
                    NetFrontEnd.Instance.ConfirmLeave();
                });
            }

            if (NetVotes.Instance != null && NetVotes.Instance.Board != null)
            {
                _rematch = AddButton(Podium.RematchLabel, Podium.RematchButton, ButtonVariant.Primary, () =>
                {
                    NetVotes.Instance.Board.StartVote(Arena.RestartVote);
                    Close();
                });
            }

            AddButton(Podium.CloseLabel, Podium.CloseButton, ButtonVariant.Secondary, Close);
        }

        private static Color ColourOf(RecapEntry entry, Dictionary<int, Color> colours)
        {
            return colours.TryGetValue(entry.Seat, out Color colour) ? colour : Color.white;
        }

        private Image AddSprite(string name, string sprite, RoomSpot spot, Color tint, float angle)
        {
            Sprite frame = GameArt.Frame(sprite);
            if (frame == null)
            {
                return null; // no art: the podium is left out and the text and buttons stay
            }

            Image image = NewImage(name, _content, frame);
            image.color = tint; // image_blend
            RectTransform rect = image.rectTransform;
            PlaceAt(rect, spot.X, spot.Y);
            rect.sizeDelta = frame.rect.size;
            rect.pivot = new Vector2(frame.pivot.x / frame.rect.width, frame.pivot.y / frame.rect.height); // the sprite's origin
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            return image;
        }

        private void AddRow(int y, string rank, string player, string score, Color colour)
        {
            AddText(rank, GmlFonts.Score, 0, Podium.RankX, y, TextAlignmentOptions.TopLeft, colour, 24f);
            AddText(player, GmlFonts.Score, 0, Podium.PlayerX, y, TextAlignmentOptions.Top, colour, 24f);
            AddText(score, GmlFonts.Score, 0, Podium.ScoreX, y, TextAlignmentOptions.TopRight, colour, 24f);
        }

        // draw_text at (x, y) with valign top and the halign the alignment says: the text's top edge on y, its left edge,
        // centre or right edge on x.
        private TextMeshProUGUI AddText(string text, string font, int outline, int x, int y, TextAlignmentOptions alignment,
            Color colour, float fallbackSize)
        {
            var rect = new GameObject("Text: " + text, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = _content.gameObject.layer;
            rect.SetParent(_content, false);
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            GameArt.ApplyFont(label, font, outline);
            label.text = text;
            label.fontSize = GameArt.CanvasFontSize(font, fallbackSize);
            label.color = colour;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            PlaceAt(rect, x, y);
            float pivotX = alignment == TextAlignmentOptions.TopLeft ? 0f : alignment == TextAlignmentOptions.TopRight ? 1f : 0.5f;
            rect.pivot = new Vector2(pivotX, 1f);
            rect.sizeDelta = new Vector2(600f, label.fontSize * 1.5f);
            return label;
        }

        private MaButton AddButton(string label, RoomSpot spot, ButtonVariant variant, System.Action onClick)
        {
            MaButton button = MaButton.Create(_content, label, onClick, variant);
            RectTransform rect = (RectTransform)button.transform;
            PlaceAt(rect, spot.X, spot.Y);
            rect.pivot = new Vector2(0.5f, 0.5f);
            Sprite face = GameArt.Frame(MenuLooks.Face(Plate.Button));
            rect.sizeDelta = face != null ? new Vector2(Mathf.Max(face.rect.width, 226f), face.rect.height) : new Vector2(320f, 64f);
            _buttons.Add(button);
            return button;
        }

        private void SelectFirstButton()
        {
            if (EventSystem.current == null || _buttons.Count == 0)
            {
                return;
            }

            // The pad and keyboard start on the rematch vote when there is one, else the first button.
            MaButton first = _rematch != null ? _rematch : _buttons[0];
            EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        // The room's pixel (x, y), y growing downward, inside the 1920 x 1080 room.
        private static void PlaceAt(RectTransform rect, int x, int y)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Image NewImage(string name, RectTransform parent, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            Image image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }
    }
}
