// The title screen (UR-T9, U.RICO-T52): the original's titlescreen room, the game's name in big letters before anything else,
// on a MindAttic Components screen of its own. While it is up the kit's front end keeps its menus away (MenusVisible false).
// As the original's obj_titlescreen did, it stops any music as it opens (the jukebox then plays the menus' tracks, Game) and
// Esc quits; its Start (Enter, the screen's first button) goes on to the Start screen, the titlescreen room's setup panel,
// over the kit's main menu (Back from there is the main menu), and Quit quits. The original also switched to full screen on
// Start; that is not carried: the window setting is the player's. A headless process and a bot never show it.
using System;
using MindAttic.Components.Unity; // MenuStack, MenuScreen, MaIcon
using MindAttic.Components.Unity.Core; // ButtonVariant
using Ricochet.Core.Art;
using Ricochet.Core.Menu;
using Unity.Multiplayer.UI;       // NetFrontEnd
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ricochet
{
    public sealed class TitleScreen : MonoBehaviour
    {
        /// <summary>The title's Start button's label.</summary>
        public const string StartLabel = "Start";

        private MenuStack _stack;
        private NetFrontEnd _frontEnd;
        private Action _start;

        /// <summary>Raised when the player leaves the title (Start), before the Start screen opens.</summary>
        public event Action Dismissed;

        /// <summary>True while the title is up.</summary>
        public bool Showing => _stack != null && _stack.IsOpen;

        /// <summary>The title's screen.</summary>
        public MenuScreen Screen { get; private set; }

        /// <summary>The titlescreen room's picture: the floor and the drifting letters (null without the art).</summary>
        public TitleBackdrop Backdrop { get; private set; }

        /// <summary>The title room's canvas, under the screen (null without the art).</summary>
        public Canvas RoomCanvas { get; private set; }

        /// <summary>The title's letters, in order (empty without the art).</summary>
        public System.Collections.Generic.List<UnityEngine.UI.Image> Letters =>
            Backdrop != null ? Backdrop.Letters : new System.Collections.Generic.List<UnityEngine.UI.Image>();

        /// <summary>
        /// Shows the title over <paramref name="frontEnd"/>'s menus (null: on its own, for a test); its main menu comes back
        /// when the title is dismissed, and <paramref name="start"/> (the Start screen's opening) runs after.
        /// </summary>
        public static TitleScreen Show(NetFrontEnd frontEnd, string version = null, Action start = null)
        {
            var title = new GameObject("Title Screen").AddComponent<TitleScreen>();
            title._frontEnd = frontEnd;
            title._start = start;
            if (frontEnd != null)
            {
                frontEnd.MenusVisible = false;
            }

            if (GameAudio.Instance != null)
            {
                GameAudio.Instance.StopMusic(); // obj_titlescreen's create: audio_stop_music
            }

            title._stack = MenuStack.Create(null, backdrop: true, sortingOrder: TitleSortingOrder);
            title._stack.EscapeEnabled = false;
            title._stack.gameObject.AddComponent<GmlNoRoom>(); // it draws its own room (AddRoom)
            MenuScreen s = title._stack.CreateScreen(MenuFlow.Title(Room.Title).ToUpperInvariant(), MenuFlow.Tagline);
            s.ShowBackButton = false;
            s.AddButton(StartLabel, title.Dismiss, ButtonVariant.Primary, MaIcon.Play);
            s.AddButton("Quit", Application.Quit);
            if (!string.IsNullOrEmpty(version))
            {
                s.AddText(version, TextStyle.Caption);
            }

            title.Screen = s;
            title.AddRoom();
            title._stack.Push(s);
            s.SelectFirst();
            return title;
        }

        // The titlescreen room's picture under the screen's card, in place of the kit's dimmed backdrop: bg_titlescreen and
        // obj_letter_R ... obj_letter_T drifting from where the room placed them (TitleBackdrop).
        private void AddRoom()
        {
            if (GameArt.Background(MenuLooks.RoomBackground) == null)
            {
                return;
            }

            _stack.Backdrop = false;
            RoomCanvas = MaCanvas.Create("Title room", TitleSortingOrder - 1);
            RoomCanvas.transform.SetParent(transform, false);
            Backdrop = TitleBackdrop.Create(RoomCanvas.transform, LetterRoom.Title);
        }

        /// <summary>The title's canvas order.</summary>
        public const int TitleSortingOrder = 120;

        // obj_titlescreen: Esc ends the game.
        private void Update()
        {
            if (Showing && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Application.Quit();
            }
        }

        /// <summary>Start: to the kit's main menu, and on to the Start screen.</summary>
        public void Dismiss()
        {
            if (!Showing)
            {
                return;
            }

            _stack.Close();
            if (_frontEnd != null)
            {
                _frontEnd.MenusVisible = true;
            }

            Dismissed?.Invoke();
            _start?.Invoke();
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_stack != null && _stack.transform.root != transform.root)
            {
                Destroy(_stack.transform.root.gameObject);
            }
        }
    }
}
