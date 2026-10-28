// U.RICO-T54, the game's pointer: the original's obj_mouse drew spr_mouse (18 x 25, its tip at the top left) at the mouse
// in every room, over everything, in place of the system's arrow. Here one image on a canvas over every menu, dialog and
// the room fade follows the pointer, at the room's scale (1080 lines); the system's arrow is hidden while it shows, and
// comes back when the window loses focus, when there is no mouse, or without the art.
using MindAttic.Components.Unity; // MaCanvas
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Ricochet
{
    public sealed class GameCursor : MonoBehaviour
    {
        /// <summary>obj_mouse's sprite.</summary>
        public const string Sprite = "spr_mouse";

        /// <summary>The pointer's canvas order: over the room fade.</summary>
        public const int SortingOrder = ScreenFade.SortingOrder + 1;

        /// <summary>The pointer's name, for tests.</summary>
        public const string PointerName = "Game cursor";

        /// <summary>The room height the sprite's pixels are drawn at.</summary>
        private const float RoomHeight = 1080f;

        private Canvas _canvas;
        private Image _pointer;

        /// <summary>The pointer in the scene (null before <see cref="Create"/>, in a headless process or without the art).</summary>
        public static GameCursor Instance { get; private set; }

        /// <summary>The pointer's image.</summary>
        public Image Pointer => _pointer;

        /// <summary>Creates the pointer once (none in a headless process, nor without spr_mouse).</summary>
        public static GameCursor Create()
        {
            if (Instance != null)
            {
                return Instance;
            }

            if (Application.isBatchMode && SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                return null;
            }

            UnityEngine.Sprite sprite = GameArt.Frame(Sprite);
            if (sprite == null)
            {
                return null;
            }

            Canvas canvas = MaCanvas.Create(PointerName, SortingOrder);
            var cursor = canvas.gameObject.AddComponent<GameCursor>();
            cursor._canvas = canvas;
            var rect = new GameObject("Pointer", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0f, 1f); // the tip, at the sprite's top left
            cursor._pointer = rect.gameObject.AddComponent<Image>();
            cursor._pointer.sprite = sprite;
            cursor._pointer.raycastTarget = false;
            cursor._pointer.enabled = false;
            return cursor;
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            Cursor.visible = true;
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void LateUpdate()
        {
            Mouse mouse = Mouse.current;
            bool show = _pointer != null && mouse != null && Application.isFocused;
            if (show)
            {
                float scale = _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
                float pixels = Screen.height / RoomHeight / scale;
                Rect frame = _pointer.sprite.rect;
                var rect = (RectTransform)_pointer.transform;
                rect.sizeDelta = new Vector2(frame.width, frame.height) * pixels;
                rect.anchoredPosition = mouse.position.ReadValue() / scale;
                rect.SetAsLastSibling();
            }

            if (_pointer != null)
            {
                _pointer.enabled = show;
            }

            Cursor.visible = !show;
        }
    }
}
