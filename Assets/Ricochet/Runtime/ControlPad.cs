// The clickable on-screen game controls (U.RICO-T29), the original's obj_btn_degreeleft / degreeright (turn the aim, held),
// obj_btn_moveforward / movebackward (drive, held), obj_btn_fire, obj_btn_useitem and obj_btn_endturn (one press each), which
// obj_mouse clicked on the game screen. A uGUI canvas of MindAttic Components labels on solid buttons, bottom right, on a canvas
// of its own (the match HUD's canvas never takes the pointer) that sorts with the HUD, under the announcer cards and every menu.
// The buttons only feed OnScreenControls; GameInput merges them with the keys, so every rule (your turn, a shot in the air,
// no shooting after driving) is PlayerAim's and the server's, same as for a key. It shows whenever the local human has a tank.
using MindAttic.Components.Unity; // MaCanvas, MaText, TextStyle
using Ricochet.Core.Art;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ricochet
{
    public sealed class ControlPad : MonoBehaviour
    {
        /// <summary>The pad's buttons, in the original's names.</summary>
        public enum Control { TurnLeft, TurnRight, Forward, Backward, Fire, UseItem, EndTurn }

        private const float Margin = 18f;
        private const float Square = 60f;
        private const float Gap = 6f;
        private const float WideWidth = 156f;
        private static readonly Color Idle = new Color(0f, 0f, 0f, 0.5f);
        private static readonly Color Hover = new Color(0.25f, 0.25f, 0.25f, 0.7f);
        private static readonly Color Down = new Color(0.9f, 0.8f, 0.2f, 0.85f);

        /// <summary>One button: reports its own press and release, and lets go when it is hidden or the pointer leaves.</summary>
        private sealed class PadButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
        {
            public Control Kind;
            public Image Face;
            public GmlPlate Plate; // U.RICO-T78: the original's sprite, when the art is there
            private bool _over;
            private bool _held;

            public void OnPointerDown(PointerEventData eventData)
            {
                if (eventData.button != PointerEventData.InputButton.Left)
                {
                    return;
                }

                _held = true;
                Apply(true);
                Paint();
            }

            public void OnPointerUp(PointerEventData eventData)
            {
                if (eventData.button == PointerEventData.InputButton.Left)
                {
                    Release(); // another button's release must not end a left-button hold
                }
            }

            public void OnPointerEnter(PointerEventData eventData)
            {
                _over = true;
                Paint();
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                _over = false;
                Release(); // the original stopped when the mouse left the button
            }

            private void OnDisable()
            {
                _over = false;
                Release();
            }

            private void Release()
            {
                if (_held)
                {
                    _held = false;
                    Apply(false);
                }

                Paint();
            }

            private void Paint()
            {
                if (Plate != null)
                {
                    // obj_btn_*'s Draw: the _over sprite while the mouse is on it (it is, while held), the _back one otherwise.
                    Plate.Over = _over || _held;
                }
                else if (Face != null)
                {
                    Face.color = _held ? Down : _over ? Hover : Idle;
                }
            }

            // Held buttons set a direction while down (and clear only their own on release); the others click once on the press.
            private void Apply(bool down)
            {
                switch (Kind)
                {
                    case Control.TurnLeft:
                        OnScreenControls.Turn = down ? 1f : (OnScreenControls.Turn > 0f ? 0f : OnScreenControls.Turn);
                        break;
                    case Control.TurnRight:
                        OnScreenControls.Turn = down ? -1f : (OnScreenControls.Turn < 0f ? 0f : OnScreenControls.Turn);
                        break;
                    case Control.Forward:
                        OnScreenControls.Drive = down ? 1 : (OnScreenControls.Drive > 0 ? 0 : OnScreenControls.Drive);
                        break;
                    case Control.Backward:
                        OnScreenControls.Drive = down ? -1 : (OnScreenControls.Drive < 0 ? 0 : OnScreenControls.Drive);
                        break;
                    case Control.Fire:
                        if (down) OnScreenControls.Fire.Click();
                        break;
                    case Control.UseItem:
                        if (down) OnScreenControls.UseItem.Click();
                        break;
                    case Control.EndTurn:
                        if (down) OnScreenControls.EndTurn.Click();
                        break;
                }
            }
        }

        private readonly PadButton[] _buttons = new PadButton[7];
        private MatchHud _hud;
        private Canvas _canvas;
        private MaText _angle;
        private Image _degree;

        /// <summary>Adds the pad to <paramref name="hud"/>'s GameObject.</summary>
        public static ControlPad Create(MatchHud hud)
        {
            var pad = hud.gameObject.AddComponent<ControlPad>();
            pad._hud = hud;
            return pad;
        }

        /// <summary>True while the pad is on screen.</summary>
        public bool Showing => _canvas != null && _canvas.gameObject.activeSelf;

        /// <summary>The pad's canvas (null until the first time it shows).</summary>
        public Canvas Canvas => _canvas;

        /// <summary>The button object for <paramref name="control"/>, for tests that press it with real pointer events (null before it is built).</summary>
        public GameObject ButtonObject(Control control) => _buttons[(int)control] != null ? _buttons[(int)control].gameObject : null;

        private void OnDestroy()
        {
            OnScreenControls.Reset();
            if (_canvas != null)
            {
                Destroy(_canvas.gameObject);
            }
        }

        /// <summary>obj_degree_gui's readout at the turn buttons: the local player's angle ("" while the pad is hidden).</summary>
        public string AngleText => Showing && _angle != null ? _angle.Tmp.text : string.Empty;

        /// <summary>True while spr_symbol_degree stands after that angle.</summary>
        public bool DegreeShowing => Showing && _degree != null && _degree.gameObject.activeSelf;

        private void Update()
        {
            Player local = LocalPlayer();
            bool show = local != null;
            if (show && _canvas == null)
            {
                Build();
            }

            if (_canvas != null && _canvas.gameObject.activeSelf != show)
            {
                _canvas.gameObject.SetActive(show); // OnDisable lets go of every held button
                if (!show)
                {
                    OnScreenControls.Reset();
                }
            }

            if (show)
            {
                // draw_angle's GUI line and obj_degree_gui (U.RICO-T49): the angle outlined 1 px over the turn buttons, the
                // degree sign at string_width / 2 + 20 from its left edge.
                HudParts.Set(_angle, HudLooks.AngleText(local.ShownHeading));
                float width = _angle.Tmp.preferredWidth;
                RectTransform sign = _degree.rectTransform;
                sign.anchoredPosition = new Vector2(_angle.RectTransform.anchoredPosition.x + (float)HudLooks.DegreeX(width), sign.anchoredPosition.y);
            }
        }

        private Player LocalPlayer()
        {
            foreach (Player player in Player.In(_hud.CurrentSession))
            {
                if (player.IsLocalHuman)
                {
                    return player;
                }
            }

            return null;
        }

        private void Build()
        {
            _canvas = MaCanvas.Create("Control pad canvas", MatchHud.SortingOrder);
            _canvas.transform.SetParent(transform, false);

            // Bottom right, as the original had them: the drive arrows at the edge, the turn arrows beside them, Fire / Use Item /
            // End Turn stacked to their left.
            float turnRight = Margin + Square + Gap;
            float turnLeft = turnRight + Square + Gap;
            float wideX = turnLeft + Square + Gap * 2f;
            Add(Control.Backward, "v", new Vector2(Square, Square), new Vector2(-Margin, Margin));
            Add(Control.Forward, "^", new Vector2(Square, Square), new Vector2(-Margin, Margin + Square + Gap));
            Add(Control.TurnRight, ">", new Vector2(Square, Square), new Vector2(-turnRight, Margin));
            Add(Control.TurnLeft, "<", new Vector2(Square, Square), new Vector2(-turnLeft, Margin));
            Add(Control.EndTurn, "End Turn", new Vector2(WideWidth, Square), new Vector2(-wideX, Margin));
            Add(Control.UseItem, "Use Item", new Vector2(WideWidth, Square), new Vector2(-wideX, Margin + Square + Gap));
            Add(Control.Fire, "Fire", new Vector2(WideWidth, Square), new Vector2(-wideX, Margin + (Square + Gap) * 2f));

            // The angle over the turn buttons, as obj_degree_gui sat by obj_btn_degreeleft; it never takes the pointer.
            float angleLeft = turnLeft + Square;
            float angleY = Margin + Square + Gap;
            _angle = HudParts.Label(_canvas.transform, TextStyle.Body, Color.white, 10f, GameArt.CanvasFontSize(GmlFonts.Score, 24f), TextAlignmentOptions.BottomLeft,
                gmlFont: GmlFonts.Score, outline: HudLooks.AngleOutline);
            RectTransform angle = _angle.RectTransform;
            angle.anchorMin = angle.anchorMax = angle.pivot = new Vector2(1f, 0f);
            angle.pivot = new Vector2(0f, 0f);
            angle.sizeDelta = new Vector2(Square * 2f, 30f);
            angle.anchoredPosition = new Vector2(-angleLeft + 4f, angleY);
            _angle.Tmp.overflowMode = TextOverflowModes.Overflow;
            _degree = HudParts.Solid("Degree", _canvas.transform, Color.white);
            _degree.sprite = GameArt.Frame(GmlSprites.Degree);
            RectTransform sign = _degree.rectTransform;
            sign.anchorMin = sign.anchorMax = new Vector2(1f, 0f);
            sign.pivot = new Vector2(0f, 1f);
            sign.sizeDelta = new Vector2(9f, 9f);
            sign.anchoredPosition = new Vector2(-angleLeft, angleY + 24f);
            _degree.gameObject.SetActive(_degree.sprite != null);
        }

        private void Add(Control control, string text, Vector2 size, Vector2 position)
        {
            RectTransform rect = HudParts.Rect(control.ToString(), _canvas.transform, new Vector2(1f, 0f), size, position);
            Image face = rect.gameObject.AddComponent<Image>();
            face.color = Idle;
            face.raycastTarget = true;
            PadButton button = rect.gameObject.AddComponent<PadButton>();
            button.Kind = control;
            button.Face = face;
            _buttons[(int)control] = button;

            // U.RICO-T78: obj_btn_degreeleft / degreeright (spr_tinyarrowleft / right), obj_btn_moveforward / movebackward
            // (spr_tinyarrowup / down) and obj_btn_fire / useitem / endturn (spr_tinybutton, the label in fnt_count), each
            // its _back or _over sprite at 0.5 under its face, kept to the sprite's proportions inside the pad's button.
            Plate kind = PlateOf(control);
            GmlPlate plate = GmlPlate.Add(rect, kind, stretch: false);
            if (plate != null)
            {
                face.color = Color.clear; // still the button's raycast target; the plate is its look
                button.Plate = plate;
                plate.Over = false;
                if (kind != Plate.TinyButton)
                {
                    return; // the arrow is the sprite
                }
            }

            MaText label = plate != null
                ? HudParts.Label(rect, TextStyle.Body, Color.white, 10f, GameArt.CanvasFontSize(MenuLooks.TinyFont, 16f), TextAlignmentOptions.Center,
                    gmlFont: MenuLooks.TinyFont)
                : HudParts.Label(rect, TextStyle.Body, Color.white, 12f, 27f, TextAlignmentOptions.Center, bold: true);
            HudParts.Stretch(label.RectTransform, 4f);
            HudParts.Set(label, text);
        }

        /// <summary>The original's sprite for each pad button.</summary>
        public static Plate PlateOf(Control control)
        {
            switch (control)
            {
                case Control.TurnLeft: return Plate.TinyArrowLeft;
                case Control.TurnRight: return Plate.TinyArrowRight;
                case Control.Forward: return Plate.TinyArrowUp;
                case Control.Backward: return Plate.TinyArrowDown;
                default: return Plate.TinyButton;
            }
        }

        /// <summary>The plate drawing <paramref name="control"/> (null without the art, or before the pad is built).</summary>
        public GmlPlate PlateFor(Control control) => _buttons[(int)control] != null ? _buttons[(int)control].Plate : null;
    }
}
