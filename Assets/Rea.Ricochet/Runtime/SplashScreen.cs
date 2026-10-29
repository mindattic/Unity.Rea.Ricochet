// U.RICO-T57: the splash and the startup credits, the original's splashscreen and startup_credits rooms, before the title.
// Core's SplashRoom, LotusCard, SplashReveal and StartupCredits say what each room step holds; this draws it on a canvas
// of its own (the 1920 x 1080 room fitted to the window, on the rooms' silver), plays the sounds they ask for, and takes
// the keys and clicks: on the splash the first finishes the picture, on the credits any key but F1 and F12 (or a click,
// or a pad's button) goes on. Each room leaves through the black curtain (ScreenFade.Cover: obj_fade_splashscreen,
// obj_fade_credits), and the title comes up out of black after. While it is up the front end's menus stay hidden, the
// jukebox waits (Game) and the pointer is hidden (GameCursor.HiddenByRoom). A headless process and a bot never show it.
using System;
using System.Collections.Generic;
using MindAttic.Components.Unity; // MaCanvas
using Ricochet.Core.Art;
using Ricochet.Core.Menu;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace Ricochet
{
    public sealed class SplashScreen : MonoBehaviour
    {
        /// <summary>The rooms' canvas order: over the menus and the title, under the curtain and the pointer.</summary>
        public const int SortingOrder = ScreenFade.SortingOrder - 10;

        /// <summary>The canvas's name, for tests.</summary>
        public const string CanvasName = "Splash";

        private enum Stage
        {
            Splash,
            Credits,
            Done,
        }

        private readonly System.Random _dice = new System.Random();
        private readonly List<SoundCue> _sounds = new List<SoundCue>();
        private readonly List<Image> _lineImages = new List<Image>();
        private Action _done;
        private Stage _stage;
        private bool _leaving;
        private double _clock;
        private RectTransform _room;
        private RectTransform _splashRoot;
        private RectTransform _creditsRoot;

        private LotusCard _card;
        private SplashReveal _reveal;
        private SpotlightPulse _pulse;
        private Image _spotlight;
        private Image _trembling;
        private Image _picture;
        private RectTransform _linesRoot;

        private StartupCredits _credits;
        private TextMeshProUGUI _note;

        /// <summary>The rooms on screen now (null when none).</summary>
        public static SplashScreen Instance { get; private set; }

        /// <summary>True while the splash or the startup credits are up.</summary>
        public static bool Showing => Instance != null && Instance._stage != Stage.Done;

        /// <summary>True while the startup credits room is the one up.</summary>
        public bool OnCredits => _stage == Stage.Credits;

        /// <summary>The splash's picture, the startup credits' state (for tests).</summary>
        public SplashReveal Reveal => _reveal;

        public StartupCredits Credits => _credits;

        /// <summary>
        /// Shows the splash, then the startup credits, then runs <paramref name="done"/> (the title). Without a screen to
        /// draw on (a headless process) <paramref name="done"/> runs at once and null is returned.
        /// </summary>
        public static SplashScreen Show(Action done)
        {
            if (Application.isBatchMode && SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                done?.Invoke();
                return null;
            }

            if (Instance != null)
            {
                Destroy(Instance.gameObject);
            }

            Canvas canvas = MaCanvas.Create(CanvasName, SortingOrder);
            var splash = canvas.gameObject.AddComponent<SplashScreen>();
            splash._done = done;
            splash.Build(canvas);
            return splash;
        }

        /// <summary>Goes on from the room up now as a key or click would (the splash's first press finishes its picture).</summary>
        public void Press()
        {
            if (_stage == Stage.Splash)
            {
                if (_reveal.Press())
                {
                    GameAudio.Stop(SplashRoom.Startup); // obj_lotus_card_picture: audio_stop_sound(sfx_startup)
                }
            }
            else if (_stage == Stage.Credits)
            {
                _credits.Press();
            }
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                GameCursor.HiddenByRoom = false;
            }
        }

        private void Build(Canvas canvas)
        {
            // The room: 1920 x 1080 about the canvas's middle, scaled to fit (Fit), on silver; its children placed in room
            // pixels from the top left (Place).
            var roomObject = new GameObject("Room", typeof(RectTransform));
            _room = (RectTransform)roomObject.transform;
            _room.SetParent(canvas.transform, false);
            _room.anchorMin = _room.anchorMax = new Vector2(0.5f, 0.5f);
            _room.pivot = new Vector2(0.5f, 0.5f);
            _room.sizeDelta = new Vector2(SplashRoom.RoomWidth, SplashRoom.RoomHeight);

            // The window round the room shows the room's colour too (no black bars on another shape of window).
            Image silver = Fill(canvas.transform, "Room colour", ToColor(SplashRoom.RoomColourRgb));
            silver.transform.SetAsFirstSibling();
            silver.raycastTarget = true; // the rooms take the clicks, nothing under them

            _splashRoot = Group("Splashscreen");
            _creditsRoot = Group("Startup credits");
            _creditsRoot.gameObject.SetActive(false);

            // splashscreen: the spotlight, the card and its trembling copy, the picture, the falling lines, the frame, the words.
            _card = new LotusCard(_dice);
            _reveal = new SplashReveal(_dice);
            _pulse = new SpotlightPulse(SplashRoom.SpotlightMinAlpha, SplashRoom.SpotlightMaxAlpha, SplashRoom.SpotlightRate);
            _spotlight = Placed(_splashRoot, "Spotlight", SplashRoom.SpotlightSprite, SplashRoom.Spotlight.X, SplashRoom.Spotlight.Y);
            Placed(_splashRoot, "Lotus card", SplashRoom.CardSprite, SplashRoom.Middle.X, SplashRoom.Middle.Y);
            _trembling = Placed(_splashRoot, "Lotus card (trembling)", SplashRoom.CardSprite, SplashRoom.Middle.X, SplashRoom.Middle.Y);
            _picture = Placed(_splashRoot, "Lotus card picture", SplashRoom.PictureSprite, SplashRoom.Middle.X, SplashRoom.Middle.Y);
            _linesRoot = Group("Static lines", _splashRoot);
            Placed(_splashRoot, "Lotus card top", SplashRoom.FrameSprite, SplashRoom.Middle.X, SplashRoom.Middle.Y);
            Words(_splashRoot, "By line", SplashRoom.ByLine, GmlFonts.Score, Color.white, SplashRoom.ByLineY);
            Words(_splashRoot, "Author line", SplashRoom.AuthorLine, GmlFonts.Impact, Color.white, SplashRoom.AuthorLineY);

            // startup_credits: the credits picture and the note, typed out.
            _credits = new StartupCredits();
            Placed(_creditsRoot, "Credits image", SplashRoom.CreditsImage, SplashRoom.CreditsImageSpot.X, SplashRoom.CreditsImageSpot.Y);
            _note = Words(_creditsRoot, "Note", _credits.Typing.Text, GmlFonts.Score, ToColor(SplashRoom.NoteColourRgb), SplashRoom.NoteY);
            _note.maxVisibleCharacters = 0;

            // object_splashscreen: sfx_tech as the room's music, looping; no pointer in either room.
            GameCursor.HiddenByRoom = true;
            if (GameAudio.Instance != null)
            {
                GameAudio.Instance.PlayMusicCue(SplashRoom.Music, true);
            }

            _stage = Stage.Splash;
            Fit();
            Draw();
            ScreenFade.Open();
        }

        private void Update()
        {
            if (_stage == Stage.Done)
            {
                return;
            }

            Fit();
            if (!_leaving && Pressed(out string key))
            {
                if (_stage == Stage.Credits)
                {
                    _credits.Press(key);
                }
                else
                {
                    Press();
                }
            }

            int steps = SplashRoom.Steps(ref _clock, Time.unscaledDeltaTime);
            for (int i = 0; i < steps; i++)
            {
                StepRoom();
            }

            Draw();
            if (!_leaving && (_stage == Stage.Splash ? _reveal.Leaving : _credits.Leaving))
            {
                Leave();
            }
        }

        private void StepRoom()
        {
            if (_stage == Stage.Splash)
            {
                _card.Step();
                _pulse.Step();
                _sounds.Clear();
                _reveal.Step(_sounds);
                foreach (SoundCue cue in _sounds)
                {
                    GameAudio.Play(cue);
                }
            }
            else if (_stage == Stage.Credits)
            {
                if (_credits.Step())
                {
                    GameAudio.Play(SoundCue.TypeQuiet);
                }
            }
        }

        // To black, then the next room (or the title) up out of black.
        private void Leave()
        {
            _leaving = true;
            ScreenFade.Cover(() =>
            {
                _leaving = false;
                if (_stage == Stage.Splash)
                {
                    // startup_credits: audio_stop_music, and its own room.
                    if (GameAudio.Instance != null)
                    {
                        GameAudio.Instance.StopMusic();
                    }

                    GameAudio.Stop(SplashRoom.Startup);
                    _splashRoot.gameObject.SetActive(false);
                    _creditsRoot.gameObject.SetActive(true);
                    _stage = Stage.Credits;
                    _clock = 0;
                    Draw();
                }
                else
                {
                    _stage = Stage.Done;
                    GameCursor.HiddenByRoom = false;
                    Action done = _done;
                    _done = null;
                    Destroy(gameObject);
                    done?.Invoke();
                }
            });
        }

        // A key, a mouse button or a pad's button pressed this frame; key is the key's name (null for a click or a button).
        private static bool Pressed(out string key)
        {
            key = null;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
            {
                foreach (KeyControl control in keyboard.allKeys)
                {
                    if (control != null && control.wasPressedThisFrame)
                    {
                        key = control.keyCode.ToString();
                        return true;
                    }
                }

                return true;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame))
            {
                return true;
            }

            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                foreach (InputControl control in pad.allControls)
                {
                    if (control is UnityEngine.InputSystem.Controls.ButtonControl button && !button.synthetic && button.wasPressedThisFrame)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void Draw()
        {
            if (_stage == Stage.Splash)
            {
                SetAlpha(_spotlight, (float)_pulse.Alpha);
                Move(_trembling, _card.X, _card.Y);
                if (_picture != null)
                {
                    Sprite frame = GameArt.Frame(_reveal.Sprite);
                    if (frame != null && _picture.sprite != frame)
                    {
                        _picture.sprite = frame;
                    }

                    _picture.enabled = _reveal.Visible && _reveal.Alpha > 0;
                    SetAlpha(_picture, (float)_reveal.Alpha);
                    Move(_picture, SplashRoom.Middle.X + _reveal.OffsetX, SplashRoom.Middle.Y + _reveal.OffsetY);
                }

                DrawLines();
            }
            else if (_stage == Stage.Credits && _note != null)
            {
                _note.maxVisibleCharacters = _credits.Typing.Shown;
            }
        }

        // obj_static_line: a black line the card's width at 0.3 alpha (draw_line in the default colour).
        private void DrawLines()
        {
            IReadOnlyList<StaticLine> lines = _reveal.Lines;
            while (_lineImages.Count < lines.Count)
            {
                Image line = Fill(_linesRoot, "Static line", new Color(0f, 0f, 0f, (float)SplashReveal.LineAlpha));
                RectTransform rect = line.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(SplashReveal.CardWidth, 1f);
                _lineImages.Add(line);
            }

            for (int i = 0; i < _lineImages.Count; i++)
            {
                Image line = _lineImages[i];
                bool shown = i < lines.Count;
                line.enabled = shown;
                if (shown)
                {
                    line.rectTransform.anchoredPosition = new Vector2(SplashRoom.Middle.X, -(float)lines[i].Y);
                }
            }
        }

        // The room scaled to fit the canvas, the whole room in view.
        private void Fit()
        {
            var canvasRect = (RectTransform)transform;
            Vector2 size = canvasRect.rect.size;
            if (size.x <= 0f || size.y <= 0f)
            {
                return;
            }

            float scale = Mathf.Min(size.x / SplashRoom.RoomWidth, size.y / SplashRoom.RoomHeight);
            _room.localScale = new Vector3(scale, scale, 1f);
        }

        private RectTransform Group(string name, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent != null ? parent : _room, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Image Fill(Transform parent, string name, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = go.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        // GML sprite <name> at room pixel (x, y), by its origin, at its own size (nothing drawn when the art is missing).
        private static Image Placed(Transform parent, string name, string sprite, int x, int y)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            Image image = go.AddComponent<Image>();
            image.raycastTarget = false;
            Sprite art = GameArt.Frame(sprite);
            if (art == null)
            {
                image.enabled = false;
                return image;
            }

            image.sprite = art;
            Vector2 pixels = art.rect.size;
            rect.sizeDelta = pixels;
            rect.pivot = new Vector2(art.pivot.x / pixels.x, art.pivot.y / pixels.y);
            Move(image, x, y);
            return image;
        }

        private static void Move(Image image, int x, int y)
        {
            if (image != null)
            {
                image.rectTransform.anchoredPosition = new Vector2(x, -y);
            }
        }

        private static void SetAlpha(Image image, float alpha)
        {
            if (image != null)
            {
                Color c = image.color;
                c.a = Mathf.Clamp01(alpha);
                image.color = c;
            }
        }

        // A line of words centred on the room, its top at room y (draw_text with fa_center, fa_top), outlined 2 px in black.
        private TextMeshProUGUI Words(Transform parent, string name, string text, string font, Color colour, int top)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(SplashRoom.RoomWidth, SplashRoom.RoomHeight - top);
            rect.anchoredPosition = new Vector2(SplashRoom.RoomWidth / 2f, -top);
            TextMeshProUGUI words = go.AddComponent<TextMeshProUGUI>();
            words.raycastTarget = false;
            if (!GameArt.ApplyFont(words, font, SplashRoom.Outline))
            {
                words.outlineWidth = 0.2f;
                words.outlineColor = Color.black;
            }

            words.fontSize = GameArt.CanvasFontSize(font, 32f);
            words.enableWordWrapping = false;
            words.overflowMode = TextOverflowModes.Overflow;
            words.alignment = TextAlignmentOptions.Top;
            words.color = colour;
            words.text = text;

            // A note longer than a line keeps its last line on the room: its block ends where a one-line note would.
            int extraLines = text.Split('\n').Length - 1;
            if (extraLines > 0)
            {
                rect.anchoredPosition += new Vector2(0f, extraLines * words.fontSize * 1.2f);
            }

            return words;
        }

        private static Color ToColor(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }
    }
}
