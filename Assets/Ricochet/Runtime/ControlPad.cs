// The clickable on-screen game controls (U.RICO-T29), the original's obj_btn_degreeleft / degreeright (turn the aim, held),
// obj_btn_moveforward / movebackward (drive, held), obj_btn_fire, obj_btn_useitem and obj_btn_endturn (one press each), which
// obj_mouse clicked on the game screen. A uGUI canvas of MindAttic Components labels on solid buttons, bottom right, on a canvas
// of its own (the match HUD's canvas never takes the pointer) that sorts with the HUD, under the announcer cards and every menu.
// The buttons only feed OnScreenControls; GameInput merges them with the keys, so every rule (your turn, a shot in the air,
// no shooting after driving) is PlayerAim's and the server's, same as for a key. It shows whenever the local human has a tank.
using MindAttic.Components.Unity; // MaCanvas, MaText, TextStyle
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

            public void OnPointerUp(PointerEventData eventData) => Release();

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
                if (Face != null)
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

        private void Update()
        {
            bool show = HasLocalTank();
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
        }

        private bool HasLocalTank()
        {
            foreach (Player player in Player.In(_hud.CurrentSession))
            {
                if (player.IsLocalHuman)
                {
                    return true;
                }
            }

            return false;
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

            MaText label = HudParts.Label(rect, TextStyle.Body, Color.white, 12f, 27f, TextAlignmentOptions.Center, bold: true);
            HudParts.Stretch(label.RectTransform, 4f);
            HudParts.Set(label, text);
        }
    }
}
