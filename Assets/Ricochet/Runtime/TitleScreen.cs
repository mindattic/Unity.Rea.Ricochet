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

            title._stack = MenuStack.Create(null, backdrop: true, sortingOrder: TitleSortingOrder);
            title._stack.EscapeEnabled = false;
            title._stack.gameObject.AddComponent<GmlNoRoom>(); // it draws its own room (AddRoom)
            MenuScreen s = title._stack.CreateScreen(MenuFlow.Title(Room.Title).ToUpperInvariant(), MenuFlow.Tagline);
            s.ShowBackButton = false;
            s.AddButton("Play", title.Dismiss, ButtonVariant.Primary, MaIcon.Play);
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

        /// <summary>The title room's picture under the screen (null without the art).</summary>
        public Canvas RoomCanvas { get; private set; }

        /// <summary>The title's letters, in order (empty without the art).</summary>
        public System.Collections.Generic.List<UnityEngine.UI.Image> Letters { get; } = new System.Collections.Generic.List<UnityEngine.UI.Image>();

        // U.RICO-T78: the titlescreen room's picture: bg_titlescreen over the whole screen and obj_letter_R ... obj_letter_T where
        // the room placed them (their origin, the letter's centre, at R 64,576 stepping 64 right and 32 up to T 512,352), under
        // the screen's card, in place of the kit's dimmed backdrop. The letters stand still here; their drift is U.RICO-T52's.
        private void AddRoom()
        {
            Sprite background = GameArt.Background(Ricochet.Core.Art.MenuLooks.RoomBackground);
            if (background == null)
            {
                return;
            }

            _stack.Backdrop = false;
            RoomCanvas = MaCanvas.Create("Title room", TitleSortingOrder - 1);
            RoomCanvas.transform.SetParent(transform, false);
            UnityEngine.UI.Image floor = Picture("bg_titlescreen", background, Vector2.zero, Vector2.one);
            RectTransform full = floor.rectTransform;
            full.offsetMin = full.offsetMax = Vector2.zero;

            string[] letters = Ricochet.Core.Art.MenuLooks.TitleLetters;
            for (int i = 0; i < letters.Length; i++)
            {
                Sprite letter = GameArt.Frame(letters[i]);
                if (letter == null)
                {
                    continue;
                }

                UnityEngine.UI.Image image = Picture(letters[i], letter, new Vector2(0f, 1f), new Vector2(0f, 1f));
                RectTransform rect = image.rectTransform;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = letter.rect.size;
                rect.anchoredPosition = new Vector2(64f + 64f * i, -(576f - 32f * i));
                Letters.Add(image);
            }
        }

        private UnityEngine.UI.Image Picture(string name, Sprite sprite, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = 5;
            rect.SetParent(RoomCanvas.transform, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            UnityEngine.UI.Image image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>The title's canvas order.</summary>
        public const int TitleSortingOrder = 120;

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
