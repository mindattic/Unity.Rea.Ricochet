// U.RICO-T54: the controls room's keyboard picture (obj_keyboardlayout's spr_keyboardlayout) and mouse (obj_mouse's
// spr_mouse) redrawn for this game's bindings as they are now. It sits on the room's picture (Menus.AddPicture's, whose
// place and size GmlRoomLayout keeps at the original's 960, 512 and 1814 x 972), hides the original image under it and
// draws Core's KeyboardPicture over the same frame: every key a black cap, the bound ones in their action's colour, a
// line from each action's leftmost key to its name, and the mouse with each bound button coloured. It draws itself again
// whenever a binding changes (the rebind screen, Reset to defaults) and each time the screen shows.
using System.Collections.Generic;
using Ricochet.Core.Art;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Ricochet
{
    public sealed class KeyboardPictureView : MonoBehaviour
    {
        /// <summary>The name of a key's cap (followed by its control, "Key q"), for tests.</summary>
        public const string KeyPrefix = "Key ";

        private static readonly Color CapEdge = new Color(0.82f, 0.82f, 0.82f, 1f);
        private static readonly Color CapFace = new Color(0.04f, 0.04f, 0.04f, 1f);

        private RectTransform _rect;
        private bool _dirty = true;

        /// <summary>What it last drew.</summary>
        public KeyboardDrawing Drawing { get; private set; }

        /// <summary>Puts the picture on <paramref name="picture"/> (the room's spr_keyboardlayout image), hiding that image.</summary>
        public static KeyboardPictureView Attach(Image picture)
        {
            if (picture == null)
            {
                return null;
            }

            picture.color = new Color(1f, 1f, 1f, 0f); // the original is kept (the room's picture) but not drawn
            var view = picture.gameObject.AddComponent<KeyboardPictureView>();
            view._rect = (RectTransform)picture.transform;
            view.Redraw();
            return view;
        }

        /// <summary>The bindings as the Input System has them now: the gameplay map's every control.</summary>
        public static List<BoundControl> CurrentBindings()
        {
            var bound = new List<BoundControl>();
            InputActionMap map = InputBindings.Gameplay;
            if (map == null)
            {
                return bound;
            }

            foreach (InputAction action in map.actions)
            {
                foreach (InputBinding binding in action.bindings)
                {
                    if (!binding.isComposite)
                    {
                        bound.Add(new BoundControl(action.name, binding.effectivePath));
                    }
                }
            }

            return bound;
        }

        /// <summary>Draws the picture for the bindings as they are now.</summary>
        public void Redraw()
        {
            _dirty = false;
            if (_rect == null)
            {
                _rect = (RectTransform)transform;
            }

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }

            Drawing = KeyboardPicture.Draw(CurrentBindings());
            TMP_FontAsset font = GameArt.Font(MenuLooks.ButtonFont);

            foreach (Callout callout in Drawing.Callouts)
            {
                Color colour = ToColor(callout.Colour);
                double top = System.Math.Min(callout.LineFromY, callout.LaneY);
                double bottom = System.Math.Max(callout.LineFromY, callout.LaneY);
                Box("Line " + callout.Group, new PictureRect(callout.LineX - 1.5, top, 3, bottom - top + 1.5), colour);
                PictureRect label = callout.Label;
                double from = callout.RightAligned ? label.Right : callout.LineX;
                double to = callout.RightAligned ? callout.LineX : label.X;
                Box("Line to " + callout.Group, new PictureRect(from, callout.LaneY - 1.5, to - from, 3), colour);
                Label("Name " + callout.Group, label, callout.Caption, colour, callout.RightAligned ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft,
                    28f, font);
            }

            foreach (KeyCap key in KeyboardPicture.Keys)
            {
                string group = Drawing.GroupOf(key.Control);
                Box(KeyPrefix + key.Control, key.Rect, CapEdge);
                PictureRect r = key.Rect;
                var face = new PictureRect(r.X + 3, r.Y + 3, r.Width - 6, r.Height - 6);
                Box("Face " + key.Control, face, group != null ? ToColor(KeyboardPicture.Colour(group)) : CapFace);
                Label("Cap " + key.Control, face, key.Label, group != null ? Color.black : Color.white, TextAlignmentOptions.Center, 26f, font);
            }

            DrawMouse(font);
        }

        private void DrawMouse(TMP_FontAsset font)
        {
            PictureRect body = KeyboardPicture.Mouse;
            Box("Mouse", body, CapEdge);
            Box("Mouse face", new PictureRect(body.X + 3, body.Y + 3, body.Width - 6, body.Height - 6), CapFace);
            foreach ((string button, PictureRect rect) in new[] { ("leftButton", KeyboardPicture.MouseLeft), ("rightButton", KeyboardPicture.MouseRight) })
            {
                string group = Drawing.MouseGroupOf(button);
                Box("Mouse " + button, new PictureRect(rect.X + 3, rect.Y + 3, rect.Width - 6, rect.Height - 6),
                    group != null ? ToColor(KeyboardPicture.Colour(group)) : CapFace);
            }

            Box("Mouse wheel", new PictureRect(body.CentreX - 4, body.Y + 14, 8, 26), CapEdge);
            Sprite pointer = GameArt.Frame(GameCursor.Sprite);
            if (pointer != null)
            {
                Image arrow = Box("Mouse pointer", new PictureRect(body.Right - 18, body.Bottom - 25, 18, 25), Color.white);
                arrow.sprite = pointer;
            }

            foreach (MouseCaption caption in Drawing.MouseCaptions)
            {
                Label("Mouse name " + caption.Button, caption.Label, caption.Caption, ToColor(caption.Colour), TextAlignmentOptions.MidlineRight, 28f, font);
            }
        }

        private void OnEnable()
        {
            InputSystem.onActionChange += OnActionChange;
            if (Drawing != null)
            {
                _dirty = true; // the screen is showing again: a rebind may have come in while it was away
            }
        }

        private void OnDisable()
        {
            InputSystem.onActionChange -= OnActionChange;
        }

        private void OnActionChange(object _, InputActionChange change)
        {
            if (change == InputActionChange.BoundControlsChanged)
            {
                _dirty = true;
            }
        }

        private void LateUpdate()
        {
            if (_dirty)
            {
                Redraw();
            }
        }

        // A child covering rect (in the 1814 x 972 picture's pixels, y down), anchored so it scales with the picture.
        private RectTransform Place(string name, PictureRect r)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = gameObject.layer;
            rect.SetParent(transform, false);
            float w = (float)KeyboardPicture.Width;
            float h = (float)KeyboardPicture.Height;
            rect.anchorMin = new Vector2((float)r.X / w, 1f - (float)r.Bottom / h);
            rect.anchorMax = new Vector2((float)r.Right / w, 1f - (float)r.Y / h);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private Image Box(string name, PictureRect r, Color colour)
        {
            Image image = Place(name, r).gameObject.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        private void Label(string name, PictureRect r, string text, Color colour, TextAlignmentOptions alignment, float size, TMP_FontAsset font)
        {
            TextMeshProUGUI label = Place(name, r).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null)
            {
                label.font = font;
            }

            label.text = text;
            label.color = colour;
            label.alignment = alignment;
            label.enableAutoSizing = true;
            label.fontSizeMin = 8f;
            label.fontSizeMax = size;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
        }

        private static Color ToColor(uint rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
