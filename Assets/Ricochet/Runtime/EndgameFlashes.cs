// The endgame room's camera flashes (U.RICO-T79, docs/BIBLE.md "Item flights and endgame flashes"): while the endgame
// recap is up, obj_flashemitter's obj_flash every step at a random spot over the 1920x1080 room, none on the pedestal (the
// lower half's middle third, where obj_flash destroys itself at once; Core's ItemLooks.NextFlash), each playing spr_flash's
// six frames once at image_speed 1 and gone at its Animation End. They are drawn on a canvas of their own over the recap
// (which sorts at 90) and take no clicks. Presentation only, on every peer.
using System.Collections.Generic;
using MindAttic.Components.Unity; // MaCanvas
using Ricochet.Core.Art;
using UnityEngine;
using UnityEngine.UI;

namespace Ricochet
{
    public sealed class EndgameFlashes : MonoBehaviour
    {
        public const string FlashSprite = "spr_flash";

        /// <summary>Just over the endgame recap's menu stack (90), under the kit's dialogs.</summary>
        public const int SortingOrder = 91;

        private readonly List<Flash> _live = new List<Flash>();
        private readonly Stack<Image> _pool = new Stack<Image>();
        private readonly System.Random _dice = new System.Random();
        private Canvas _canvas;
        private RectTransform _room;
        private float _clock;
        private bool _running;

        /// <summary>Flashes showing now (for tests).</summary>
        public int Showing => _live.Count;

        /// <summary>Flashes thrown since it started, kept or not (for tests): one a step.</summary>
        public int Thrown { get; private set; }

        /// <summary>True while it throws flashes.</summary>
        public bool Running => _running;

        /// <summary>The canvas they are drawn on (null before the first start).</summary>
        public Canvas Canvas => _canvas;

        /// <summary>Every flash showing now, in room pixels (y growing downward), for tests.</summary>
        public IEnumerable<Vector2Int> Spots
        {
            get
            {
                foreach (Flash flash in _live)
                {
                    yield return flash.Spot;
                }
            }
        }

        /// <summary>obj_flashemitter switched on: a flash every step from now.</summary>
        public void Begin()
        {
            if (_canvas == null)
            {
                _canvas = MaCanvas.Create("Endgame flashes", SortingOrder);
                _canvas.transform.SetParent(transform, false);
                GraphicRaycaster raycaster = _canvas.GetComponent<GraphicRaycaster>();
                if (raycaster != null)
                {
                    raycaster.enabled = false; // the recap's buttons under it take the clicks
                }

                _room = (RectTransform)_canvas.transform;
            }

            _canvas.gameObject.SetActive(true);
            _running = true;
            _clock = 0f;
        }

        /// <summary>The endgame room left: every flash goes.</summary>
        public void End()
        {
            _running = false;
            foreach (Flash flash in _live)
            {
                Recycle(flash.Image);
            }

            _live.Clear();
            if (_canvas != null)
            {
                _canvas.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (!_running)
            {
                return;
            }

            float steps = Time.deltaTime * Steps.PerSecond;
            float duration = GameArt.Duration(FlashSprite, (float)ItemLooks.FlashImageSpeed);
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                Flash flash = _live[i];
                flash.Age += Time.deltaTime;
                if (flash.Age >= duration)
                {
                    Recycle(flash.Image);
                    _live.RemoveAt(i);
                    continue;
                }

                flash.Image.sprite = GameArt.FrameAt(FlashSprite, flash.Age, false, (float)ItemLooks.FlashImageSpeed);
            }

            // obj_flashemitter's Step: instance_create(0, 0, obj_flash), one a step.
            _clock += steps;
            while (_clock >= 1f)
            {
                _clock -= 1f;
                for (int n = 0; n < ItemLooks.FlashesPerStep; n++)
                {
                    Thrown++;
                    if (ItemLooks.NextFlash(_dice, out int x, out int y) && duration > 0f)
                    {
                        Add(x, y);
                    }
                }
            }
        }

        private void Add(int x, int y)
        {
            Sprite first = GameArt.Frame(FlashSprite);
            if (first == null)
            {
                return;
            }

            Image image = _pool.Count > 0 ? _pool.Pop() : NewImage();
            image.gameObject.SetActive(true);
            image.sprite = first;
            RectTransform rect = image.rectTransform;
            // The room's pixel (x, y) wherever the canvas is scaled to: anchored there, the sprite's origin on it.
            var at = new Vector2(x / (float)ItemLooks.RoomWidth, 1f - y / (float)ItemLooks.RoomHeight);
            rect.anchorMin = at;
            rect.anchorMax = at;
            rect.sizeDelta = first.rect.size;
            rect.pivot = new Vector2(first.pivot.x / first.rect.width, first.pivot.y / first.rect.height);
            rect.anchoredPosition = Vector2.zero;
            _live.Add(new Flash { Image = image, Spot = new Vector2Int(x, y) });
        }

        private Image NewImage()
        {
            var go = new GameObject("Flash", typeof(RectTransform));
            go.transform.SetParent(_room, false);
            Image image = go.AddComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            return image;
        }

        private void Recycle(Image image)
        {
            if (image == null)
            {
                return;
            }

            image.gameObject.SetActive(false);
            _pool.Push(image);
        }

        private sealed class Flash
        {
            public Image Image;
            public Vector2Int Spot;
            public float Age;
        }
    }
}
