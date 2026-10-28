// The title's and the main menu's room picture (U.RICO-T52): bg_titlescreen over the whole room and obj_letter_R ... obj_letter_T
// drifting over it, stepped by Core's TitleLetters at the rooms' 180 steps a second (each letter bounces off the room's edges
// and shows its mirror letter while it moves left). The main menu's floor scrolls right a pixel a step, tiled; the title's
// stands still. It draws in room pixels (1920 x 1080, y down from the top left) inside the rect it is given.
using System.Collections.Generic;
using Ricochet.Core.Art;
using UnityEngine;
using UnityEngine.UI;

namespace Ricochet
{
    public sealed class TitleBackdrop : MonoBehaviour
    {
        /// <summary>The backdrop object's name, for tests.</summary>
        public const string ObjectName = "Title backdrop";

        /// <summary>The most steps one frame catches up (a quarter second), so a stall never runs the letters away.</summary>
        private const int MaxStepsPerFrame = TitleLetters.StepsPerSecond / 4;

        private readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();
        private RectTransform[] _floors;
        private double _owed;

        /// <summary>The letters' motion.</summary>
        public TitleLetters Model { get; private set; }

        /// <summary>The letters, R I C O C H E T in order (a letter whose art is missing is left out).</summary>
        public List<Image> Letters { get; } = new List<Image>();

        private readonly List<int> _letterIndex = new List<int>();

        /// <summary>
        /// Builds the backdrop of <paramref name="room"/> filling <paramref name="parent"/>; null without the room's
        /// background art.
        /// </summary>
        public static TitleBackdrop Create(Transform parent, LetterRoom room)
        {
            Sprite floor = GameArt.Background(MenuLooks.RoomBackground);
            if (floor == null)
            {
                return null;
            }

            var rect = new GameObject(ObjectName, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            rect.SetAsFirstSibling();
            Stretch(rect);
            var backdrop = rect.gameObject.AddComponent<TitleBackdrop>();
            backdrop.Model = new TitleLetters(room);

            // Two copies of the floor side by side, so a scrolling floor always covers the room.
            backdrop._floors = new RectTransform[2];
            for (int i = 0; i < 2; i++)
            {
                Image image = backdrop.Picture(MenuLooks.RoomBackground, floor, rect);
                Stretch(image.rectTransform);
                backdrop._floors[i] = image.rectTransform;
            }

            for (int i = 0; i < TitleLetters.Count; i++)
            {
                Sprite letter = backdrop.Letter(MenuLooks.TitleLetters[i]);
                if (letter == null)
                {
                    continue;
                }

                Image image = backdrop.Picture(MenuLooks.TitleLetters[i], letter, rect);
                RectTransform r = image.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
                r.pivot = new Vector2(0.5f, 0.5f); // the letter sprites' origin is their centre
                r.sizeDelta = letter.rect.size;
                backdrop.Letters.Add(image);
                backdrop._letterIndex.Add(i);
            }

            backdrop.Apply();
            return backdrop;
        }

        private void Update()
        {
            _owed += Time.unscaledDeltaTime * TitleLetters.StepsPerSecond;
            int steps = (int)_owed;
            if (steps <= 0)
            {
                return;
            }

            _owed -= steps;
            Model.Step(Mathf.Min(steps, MaxStepsPerFrame));
            Apply();
        }

        /// <summary>Puts the letters and the floor where the model has them now.</summary>
        public void Apply()
        {
            float shift = (float)(Model.FloorOffset % TitleLetters.RoomWidth);
            float width = (float)TitleLetters.RoomWidth;
            Place(_floors[0], shift);
            Place(_floors[1], shift - width);

            for (int k = 0; k < Letters.Count; k++)
            {
                int i = _letterIndex[k];
                Image image = Letters[k];
                image.rectTransform.anchoredPosition = new Vector2((float)Model.X(i), -(float)Model.Y(i));
                Sprite sprite = Letter(Model.Sprite(i));
                if (sprite != null && image.sprite != sprite)
                {
                    image.sprite = sprite;
                }
            }
        }

        private static void Place(RectTransform floor, float x)
        {
            floor.offsetMin = new Vector2(x, 0f);
            floor.offsetMax = new Vector2(x, 0f);
        }

        private Sprite Letter(string name)
        {
            if (!_sprites.TryGetValue(name, out Sprite sprite))
            {
                sprite = GameArt.Frame(name);
                _sprites[name] = sprite;
            }

            return sprite;
        }

        private Image Picture(string name, Sprite sprite, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
