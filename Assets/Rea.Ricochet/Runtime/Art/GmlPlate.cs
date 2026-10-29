// One of the original's drawn controls (U.RICO-T78): its _over or _back sprite at half alpha under its face sprite, as every
// obj_btn_*, arrow, checkbox, input box and tiny game-screen button drew itself (Core's MenuLooks names them). A plate sits
// behind whatever control it dresses, never takes the pointer itself (unless asked to), and its owner says when the pointer
// is over it and whether a checkbox is ticked.
using Ricochet.Core.Art;
using UnityEngine;
using UnityEngine.UI;

namespace Ricochet
{
    [DisallowMultipleComponent]
    public sealed class GmlPlate : MonoBehaviour
    {
        /// <summary>The nine-slice border, in pixels, of the frames that stretch (spr_button, spr_inputbox).</summary>
        public const int FrameBorder = 12;

        private Plate _kind;
        private Image _back;
        private Image _face;
        private bool _stretch;
        private bool _over;
        private bool _checked;
        private bool _drawn;

        /// <summary>Which control it draws.</summary>
        public Plate Kind => _kind;

        /// <summary>The half-alpha sprite under the face.</summary>
        public Image Back => _back;

        /// <summary>The face sprite.</summary>
        public Image Face => _face;

        /// <summary>The GML name the back shows now.</summary>
        public string BackName => MenuLooks.Back(_kind, _over);

        /// <summary>The GML name the face shows now.</summary>
        public string FaceName => MenuLooks.Face(_kind, _checked);

        /// <summary>
        /// Adds a plate of <paramref name="kind"/> filling <paramref name="host"/>, behind its other children; null (and
        /// nothing added) when the catalogue lacks the sprite, so the control keeps its own look. A
        /// <paramref name="stretch"/> plate nine-slices its frame to the host's size, otherwise it keeps the sprite's
        /// proportions inside the host.
        /// </summary>
        public static GmlPlate Add(RectTransform host, Plate kind, bool stretch, bool raycast = false)
        {
            if (host == null || GameArt.Frame(MenuLooks.Face(kind)) == null)
            {
                return null;
            }

            var root = new GameObject("GML " + kind, typeof(RectTransform)).GetComponent<RectTransform>();
            root.gameObject.layer = host.gameObject.layer;
            root.SetParent(host, false);
            root.SetAsFirstSibling();
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            root.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            GmlPlate plate = root.gameObject.AddComponent<GmlPlate>();
            plate._kind = kind;
            plate._stretch = stretch;
            plate._back = plate.Layer("Back", root, false, new Color(1f, 1f, 1f, (float)MenuLooks.BackAlpha));
            plate._face = plate.Layer("Face", root, raycast, Color.white);
            plate.Draw();
            return plate;
        }

        /// <summary>The pointer is on the control (its Mouse Enter's showmsg / over).</summary>
        public bool Over
        {
            get => _over;
            set
            {
                if (_over != value || !_drawn)
                {
                    _over = value;
                    Draw();
                }
            }
        }

        /// <summary>A checkbox's tick.</summary>
        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked != value || !_drawn)
                {
                    _checked = value;
                    Draw();
                }
            }
        }

        private Image Layer(string name, RectTransform parent, bool raycast, Color color)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Image image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = raycast;
            image.color = color;
            image.preserveAspect = !_stretch;
            return image;
        }

        private void Draw()
        {
            _drawn = true;
            Show(_back, MenuLooks.Back(_kind, _over));
            Show(_face, MenuLooks.Face(_kind, _checked));
        }

        private void Show(Image image, string name)
        {
            Sprite sprite = _stretch ? GameArt.Sliced(name, FrameBorder) : GameArt.Frame(name);
            image.sprite = sprite;
            image.canvasRenderer.SetAlpha(sprite != null ? 1f : 0f); // a missing sprite draws nothing (a raycast face still takes the pointer)
            if (sprite == null)
            {
                return;
            }

            bool sliced = _stretch && sprite.border != Vector4.zero;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.fillCenter = true;
            // The frame's edge at its own pixel size: a canvas unit is a room pixel (the 1920x1080 reference), and Image
            // sizes a border by the sprite's pixels per unit against the canvas's reference 100.
            image.pixelsPerUnitMultiplier = sliced ? 100f / sprite.pixelsPerUnit : 1f;
        }
    }
}
