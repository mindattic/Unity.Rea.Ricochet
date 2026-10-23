// The title screen (UR-T9): the original's titlescreen room, the game's name in big letters before anything else, on a
// MindAttic Components screen of its own. While it is up the kit's front end keeps its menus away (MenusVisible false);
// Play (or Enter, the screen's first button) dismisses it and the kit's main menu (mainmenu) comes up. A headless process
// and a bot never show it, like the kit's own menus.
using System;
using MindAttic.Components.Unity; // MenuStack, MenuScreen, MaIcon
using MindAttic.Components.Unity.Core; // ButtonVariant
using Ricochet.Core.Menu;
using Unity.Multiplayer.UI;       // NetFrontEnd
using UnityEngine;

namespace Ricochet
{
    public sealed class TitleScreen : MonoBehaviour
    {
        private MenuStack _stack;
        private NetFrontEnd _frontEnd;

        /// <summary>Raised when the player leaves the title for the main menu.</summary>
        public event Action Dismissed;

        /// <summary>True while the title is up.</summary>
        public bool Showing => _stack != null && _stack.IsOpen;

        /// <summary>The title's screen.</summary>
        public MenuScreen Screen { get; private set; }

        /// <summary>
        /// Shows the title over <paramref name="frontEnd"/>'s menus (null: on its own, for a test); its main menu comes back
        /// when the title is dismissed.
        /// </summary>
        public static TitleScreen Show(NetFrontEnd frontEnd, string version = null)
        {
            var title = new GameObject("Title Screen").AddComponent<TitleScreen>();
            title._frontEnd = frontEnd;
            if (frontEnd != null)
            {
                frontEnd.MenusVisible = false;
            }

            title._stack = MenuStack.Create(null, backdrop: true, sortingOrder: 120);
            title._stack.EscapeEnabled = false;
            MenuScreen s = title._stack.CreateScreen(MenuFlow.Title(Room.Title).ToUpperInvariant(), MenuFlow.Tagline);
            s.ShowBackButton = false;
            s.AddButton("Play", title.Dismiss, ButtonVariant.Primary, MaIcon.Play);
            s.AddButton("Quit", Application.Quit);
            if (!string.IsNullOrEmpty(version))
            {
                s.AddText(version, TextStyle.Caption);
            }

            title.Screen = s;
            title._stack.Push(s);
            s.SelectFirst();
            return title;
        }

        /// <summary>To the main menu (titlescreen -> mainmenu).</summary>
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
