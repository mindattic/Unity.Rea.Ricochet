// U.RICO-T53, the room fades: the one black curtain (Core's RoomFade, the original's obj_fade and its ten per-room copies,
// spr_black over the room at depth -10000) on a canvas over everything, for every screen change. A screen change comes up
// out of black: a screen pushed or popped on the kit's main menu or in-game menu, the front end moving between the menus,
// the lobby and the game (obj_fade_mainmenu, obj_fade_game ...), and the endgame recap (obj_fade_endgame). Cover fades to
// black first and makes the change once it is black (obj_maproom's "fadein" before its room_goto), as leaving the map room
// for the game does. The change has the original's screen-change click: every obj_btn_* that went to another room played
// sfx_click; a change made with the pointer already had it from UiSounds, so the curtain plays it only for one made any
// other way (Enter, Esc, a pad). The curtain never takes a click, except while it is going to black.
using System;
using System.Collections.Generic;
using MindAttic.Components.Unity; // MenuStack, MaCanvas
using Ricochet.Core.Menu;
using Unity.Multiplayer.UI;       // NetFrontEnd, FrontEndState
using UnityEngine;
using UnityEngine.UI;

namespace Ricochet
{
    public sealed class ScreenFade : MonoBehaviour
    {
        /// <summary>The curtain's canvas order: over every menu, dialog and the title.</summary>
        public const int SortingOrder = 30000;

        /// <summary>The curtain's name, for tests.</summary>
        public const string CurtainName = "Room fade";

        private readonly RoomFade _fade = new RoomFade();
        private readonly HashSet<MenuStack> _watched = new HashSet<MenuStack>();
        private NetFrontEnd _frontEnd;
        private Image _curtain;
        private Action _then;
        private int _clickFrame = -1;

        /// <summary>The curtain in the scene (null before <see cref="Create"/>, and in a headless process).</summary>
        public static ScreenFade Instance { get; private set; }

        /// <summary>The curtain's state.</summary>
        public RoomFade Fade => _fade;

        /// <summary>The black image over the screen.</summary>
        public Image Curtain => _curtain;

        /// <summary>Creates the curtain once (none in a headless process: nothing is drawn there).</summary>
        public static ScreenFade Create()
        {
            if (Instance != null)
            {
                return Instance;
            }

            if (Application.isBatchMode && SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                return null;
            }

            Canvas canvas = MaCanvas.Create(CurtainName, SortingOrder);
            var fade = canvas.gameObject.AddComponent<ScreenFade>();
            var rect = new GameObject("Curtain", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            fade._curtain = rect.gameObject.AddComponent<Image>();
            fade._curtain.sprite = GameArt.Frame("spr_black"); // the copies' sprite; plain black without the art
            fade._curtain.type = Image.Type.Simple;
            fade.Show();
            return fade;
        }

        /// <summary>
        /// A new screen: up out of black, with the screen-change click when <paramref name="click"/> and the pointer did
        /// not already make it this frame (its press or the release a button acts on). Nothing without the curtain.
        /// </summary>
        public static void Open(bool click = false)
        {
            if (Instance == null)
            {
                return;
            }

            if (click)
            {
                Instance._clickFrame = Time.frameCount;
            }

            if (Instance._fade.Covering)
            {
                return; // already going to black: the change it is waiting on comes up out of black itself
            }

            Instance._fade.Open();

            Instance.Show();
        }

        /// <summary>
        /// To black, then <paramref name="then"/> (the screen change), then up out of black again; at once without the
        /// curtain. False (and <paramref name="then"/> not run) while a cover is already under way.
        /// </summary>
        public static bool Cover(Action then)
        {
            if (Instance == null)
            {
                then?.Invoke();
                return true;
            }

            if (Instance._fade.Covering)
            {
                return false;
            }

            Instance._then = then;
            Instance._fade.Cover();
            Instance.Show();
            return true;
        }

        /// <summary>Watches <paramref name="stack"/>: each change of its screen comes up out of black, with the click.</summary>
        public void Watch(MenuStack stack)
        {
            if (stack == null || !_watched.Add(stack))
            {
                return;
            }

            stack.ScreenChanged += OnScreenChanged;
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            foreach (MenuStack stack in _watched)
            {
                if (stack != null)
                {
                    stack.ScreenChanged -= OnScreenChanged;
                }
            }

            _watched.Clear();
            if (_frontEnd != null)
            {
                _frontEnd.StateChanged -= OnStateChanged;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnScreenChanged(MenuScreen screen)
        {
            if (screen != null && screen.Stack != null && screen.Stack.IsOpen)
            {
                Open(click: true);
            }
        }

        // The front end's rooms: the lobby, the game and the menus again each come up out of black (obj_fade_game,
        // obj_fade_mainmenu, obj_fade_multiplayer); joining and reconnecting keep the kit's busy dialog in view.
        private void OnStateChanged(FrontEndState state)
        {
            if (state == FrontEndState.Menu || state == FrontEndState.Lobby || state == FrontEndState.InGame)
            {
                Open();
            }
        }

        private void Update()
        {
            HookFrontEnd();
            if (_fade.Step(Time.unscaledDeltaTime))
            {
                // Black: make the change, then clear again over the new screen.
                Action then = _then;
                _then = null;
                _fade.Open();
                then?.Invoke();
            }

            Show();
        }

        // The click for a change made this frame, once UiSounds has had the pointer's press (its Update may run after the
        // event system's, which made the change).
        private void LateUpdate()
        {
            if (_clickFrame < 0)
            {
                return;
            }

            if (!UiSounds.PointerClickedOn(_clickFrame))
            {
                GameAudio.Play(SoundCue.Click);
            }

            _clickFrame = -1;
        }

        // The kit's main menu and in-game menu, and its rooms, once the front end has built them.
        private void HookFrontEnd()
        {
            NetFrontEnd frontEnd = NetFrontEnd.Instance;
            if (frontEnd != _frontEnd)
            {
                if (_frontEnd != null)
                {
                    _frontEnd.StateChanged -= OnStateChanged;
                }

                _frontEnd = frontEnd;
                if (_frontEnd != null)
                {
                    _frontEnd.StateChanged += OnStateChanged;
                }
            }

            if (_frontEnd != null && _frontEnd.HasMenus)
            {
                Watch(_frontEnd.MainMenu);
                Watch(_frontEnd.PauseMenu);
            }
        }

        private void Show()
        {
            if (_curtain == null)
            {
                return;
            }

            _curtain.color = new Color(0f, 0f, 0f, _fade.Alpha);
            _curtain.enabled = _fade.Alpha > 0f;
            _curtain.raycastTarget = _fade.Covering; // no clicks while it goes to black, before the change
        }
    }
}
