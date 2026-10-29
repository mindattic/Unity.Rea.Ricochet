// The original's menu look on the rebuild's menus (U.RICO-T78). The menus are MindAttic Components screens (the kit's front
// end, the game's own rooms, the lobby, the pause menu), so, as UiSounds hears every one of them from one watcher, this one
// watcher dresses every one of their controls as it appears, whichever screen built it:
//   a button (obj_btn_*): spr_button_back, or spr_button_over while the pointer is on it, at 0.5 under spr_button, the label
//     in fnt_mono outlined 2 px (draw_text_outline), white;
//   a switch (obj_checkbox_*): spr_checkbox_back / _over at 0.5 under spr_checkbox_unchecked or _checked, the text in
//     fnt_score outlined 2 px;
//   a text field (obj_inputbox*): spr_inputbox_back at 0.5 under spr_inputbox, the text in fnt_score;
//   a labelled slider or dropdown (obj_lbl_* with its less / more arrows): the caption in fnt_mono outlined 2 px, the value in
//     fnt_score, and spr_arrowleft / spr_arrowright either side stepping it one notch, wrapping past either end;
//   a screen's title in fnt_impact outlined 2 px (the rooms' titles) and its section headings in fnt_mono outlined 2 px.
// Each control keeps its own behaviour, focus ring, navigation and layout: only its face changes. Without the art (an empty
// catalogue) nothing is dressed and the theme's look stays.
using System.Collections.Generic;
using MindAttic.Components.Unity; // MaButton, MaToggle, MaTextbox, MaFormRow, MaRange, MaSelect, MenuScreen, MaText
using MindAttic.Components.Unity.Core; // ControlState
using Ricochet.Core.Art;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ricochet
{
    public sealed class GmlMenuSkin : MonoBehaviour
    {
        /// <summary>Seconds between looks for new controls.</summary>
        public const float SweepSeconds = 0.1f;

        private static GmlMenuSkin s_instance;
        private float _next;

        /// <summary>The watcher in this scene, made on first call.</summary>
        public static GmlMenuSkin Ensure()
        {
            if (s_instance == null)
            {
                s_instance = new GameObject("GML menu skin").AddComponent<GmlMenuSkin>();
            }

            return s_instance;
        }

        private void Update()
        {
            if (Time.unscaledTime < _next)
            {
                return;
            }

            _next = Time.unscaledTime + SweepSeconds;
            Sweep();
        }

        /// <summary>Dresses every control on screen that is not dressed yet; returns how many it dressed.</summary>
        public static int Sweep()
        {
            if (GameArt.Frame(MenuLooks.Face(Plate.Button)) == null)
            {
                return 0; // no art: the theme's look stays
            }

            int dressed = 0;
            foreach (MaButton button in FindObjectsByType<MaButton>(FindObjectsSortMode.None))
            {
                dressed += Dress(button) ? 1 : 0;
            }

            foreach (MaToggle toggle in FindObjectsByType<MaToggle>(FindObjectsSortMode.None))
            {
                dressed += Dress(toggle) ? 1 : 0;
            }

            foreach (MaTextbox textbox in FindObjectsByType<MaTextbox>(FindObjectsSortMode.None))
            {
                dressed += Dress(textbox) ? 1 : 0;
            }

            foreach (MaFormRow row in FindObjectsByType<MaFormRow>(FindObjectsSortMode.None))
            {
                dressed += Dress(row) ? 1 : 0;
            }

            foreach (MenuScreen screen in FindObjectsByType<MenuScreen>(FindObjectsSortMode.None))
            {
                DressText(screen);
                GmlRoomLayout.Attach(screen); // the room's background and the controls at the room's places (U.RICO-T84)
            }

            return dressed;
        }

        /// <summary>obj_btn_*: spr_button over its back, the label in fnt_mono outlined 2 px. An icon-only button is left as it is.</summary>
        public static bool Dress(MaButton button)
        {
            if (button == null || button is MaIconButton || button.GetComponent<GmlControlSkin>() != null)
            {
                return false;
            }

            GmlPlate plate = GmlPlate.Add(button.RectTransform, Plate.Button, stretch: true);
            if (plate == null)
            {
                return false;
            }

            GmlControlSkin skin = button.gameObject.AddComponent<GmlControlSkin>();
            skin.Init(plate, button.GetComponent<MaStateRelay>(), button.LabelText, button.Surface.Fill, button.Surface.Border);
            skin.Selected = () => button.Selected;
            if (button.LabelText != null)
            {
                GmlFontLabel.Attach(button.LabelText, MenuLooks.ButtonFont, MenuLooks.ButtonOutline);
            }

            return true;
        }

        /// <summary>
        /// A how-to-play slide's Last / Next (obj_btn_lastslide / _nextslide): spr_arrowleft or spr_arrowright over its back in
        /// place of the button's face, its label kept but not drawn. Replaces any button dressing it already has.
        /// </summary>
        public static bool MakeArrow(MaButton button, bool last)
        {
            if (button == null)
            {
                return false;
            }

            GmlControlSkin old = button.GetComponent<GmlControlSkin>();
            if (old != null)
            {
                if (old.Plate != null)
                {
                    DestroyImmediate(old.Plate.gameObject);
                }

                DestroyImmediate(old);
            }

            GmlPlate plate = GmlPlate.Add(button.RectTransform, last ? Plate.ArrowLeft : Plate.ArrowRight, stretch: false);
            if (plate == null)
            {
                return false;
            }

            button.gameObject.AddComponent<GmlSlideArrow>().Last = last;
            GmlControlSkin skin = button.gameObject.AddComponent<GmlControlSkin>();
            skin.Init(plate, button.GetComponent<MaStateRelay>(), null, button.Surface.Fill, button.Surface.Border);
            skin.Selected = () => button.Selected;
            if (button.LabelText != null)
            {
                button.LabelText.alpha = 0f;
            }

            return true;
        }

        /// <summary>obj_checkbox_*: the checkbox in place of the switch's track and knob, the text in fnt_score outlined 2 px.</summary>
        public static bool Dress(MaToggle toggle)
        {
            if (toggle == null || toggle.GetComponent<GmlControlSkin>() != null)
            {
                return false;
            }

            Transform track = Find(toggle.transform, "Track");
            if (track == null)
            {
                return false;
            }

            GmlPlate plate = GmlPlate.Add((RectTransform)track, Plate.Checkbox, stretch: false);
            if (plate == null)
            {
                return false;
            }

            // The checkbox is square, so the track's 68x38 pill becomes a 48 px square (64 px at three quarters, the
            // control's height being the kit's).
            LayoutElement size = track.GetComponent<LayoutElement>();
            if (size != null)
            {
                size.minWidth = size.preferredWidth = size.minHeight = size.preferredHeight = CheckboxSize;
            }

            var hidden = new List<Graphic> { track.GetComponent<Image>() };
            Transform border = track.Find("Border");
            Transform knob = track.Find("Knob");
            if (border != null) hidden.Add(border.GetComponent<Image>());
            if (knob != null) hidden.Add(knob.GetComponent<Image>());

            TMP_Text label = null;
            foreach (TextMeshProUGUI text in toggle.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                label = text;
                break;
            }

            GmlControlSkin skin = toggle.gameObject.AddComponent<GmlControlSkin>();
            skin.Init(plate, toggle.GetComponent<MaStateRelay>(), label, hidden.ToArray());
            skin.Ticked = () => toggle.Value;
            if (label != null)
            {
                GmlFontLabel.Attach(label, MenuLooks.CheckboxFont, MenuLooks.CheckboxOutline);
            }

            return true;
        }

        /// <summary>The checkbox's side on the kit's rows.</summary>
        public const float CheckboxSize = 48f;

        /// <summary>obj_inputbox*: spr_inputbox over spr_inputbox_back, the text in fnt_score; its caption in fnt_mono outlined 2 px.</summary>
        public static bool Dress(MaTextbox textbox)
        {
            if (textbox == null || textbox.GetComponent<GmlControlSkin>() != null)
            {
                return false;
            }

            Transform field = Find(textbox.transform, "Field");
            MaSurface surface = field != null ? field.GetComponent<MaSurface>() : null;
            if (surface == null)
            {
                return false;
            }

            GmlPlate plate = GmlPlate.Add((RectTransform)field, Plate.InputBox, stretch: true);
            if (plate == null)
            {
                return false;
            }

            GmlControlSkin skin = textbox.gameObject.AddComponent<GmlControlSkin>();
            skin.Init(plate, field.GetComponent<MaStateRelay>(), null, surface.Fill, surface.Border);
            foreach (TextMeshProUGUI text in textbox.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (text.gameObject.name == "Caption")
                {
                    GmlFontLabel.Attach(text, MenuLooks.ButtonFont, MenuLooks.ButtonOutline);
                }
                else
                {
                    GmlFontLabel.Attach(text, MenuLooks.FieldFont);
                }
            }

            return true;
        }

        /// <summary>
        /// obj_lbl_*: the caption in fnt_mono outlined 2 px and the value in fnt_score; a slider or dropdown gets the less / more
        /// arrows either side (spr_arrowleft, spr_arrowright), one notch a click, wrapping.
        /// </summary>
        public static bool Dress(MaFormRow row)
        {
            if (row == null || row.GetComponent<GmlRowSkin>() != null)
            {
                return false;
            }

            row.gameObject.AddComponent<GmlRowSkin>();
            foreach (MaText text in row.GetComponentsInChildren<MaText>(true))
            {
                if (text.transform.parent != row.transform)
                {
                    continue;
                }

                if (text == row.ValueText)
                {
                    GmlFontLabel.Attach(text.Tmp, MenuLooks.FieldFont);
                }
                else if (text.Tmp.text == row.Label)
                {
                    text.WithColor(Color.white); // draw_text_outline's white face
                    GmlFontLabel.Attach(text.Tmp, MenuLooks.ButtonFont, MenuLooks.ButtonOutline);
                }
            }

            if (row.Control is MaRange range)
            {
                GmlStepper.AddPair(row, range.RectTransform, delta =>
                {
                    if (range.Interactable)
                    {
                        range.Value = (float)MenuLooks.Step(range.Value, delta * MenuLooks.Notch(range.Min, range.Max, range.Slider.wholeNumbers), range.Min, range.Max);
                    }
                });
            }
            else if (row.Control is MaSelect select)
            {
                GmlStepper.AddPair(row, select.RectTransform, delta =>
                {
                    if (select.Interactable && select.Count > 0)
                    {
                        select.Value = (int)MenuLooks.Step(select.Value, delta, 0, select.Count - 1);
                    }
                });
            }

            return true;
        }

        /// <summary>A screen's title in fnt_impact outlined 2 px (unless its owner set one) and its section headings in fnt_mono outlined 2 px.</summary>
        public static void DressText(MenuScreen screen)
        {
            if (screen == null)
            {
                return;
            }

            TMP_Text title = TitleOf(screen);
            if (title != null && title.GetComponent<GmlFontLabel>() == null)
            {
                GmlFontLabel.Attach(title, MenuLooks.TitleFont, MenuLooks.TitleOutline);
            }

            foreach (Transform child in screen.Items)
            {
                MaText text = child.GetComponent<MaText>();
                if (text != null && text.Style == TextStyle.Section && text.GetComponent<GmlFontLabel>() == null)
                {
                    text.WithColor(Color.white); // draw_text_outline's white face
                    GmlFontLabel.Attach(text.Tmp, MenuLooks.ButtonFont, MenuLooks.ButtonOutline);
                }
            }
        }

        /// <summary>The title of <paramref name="screen"/> (the header's text), or null.</summary>
        public static TMP_Text TitleOf(MenuScreen screen)
        {
            Transform header = screen != null && screen.Panel != null ? screen.Panel.Find("Header") : null;
            if (header == null)
            {
                return null;
            }

            foreach (Transform child in header)
            {
                MaText text = child.GetComponent<MaText>();
                if (text != null)
                {
                    return text.Tmp;
                }
            }

            return null;
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }

            return null;
        }
    }

    /// <summary>Marks a form row the skin has dressed.</summary>
    [DisallowMultipleComponent]
    public sealed class GmlRowSkin : MonoBehaviour
    {
    }

    /// <summary>
    /// Keeps a dressed control's plate in step with it: over while the pointer (or the keyboard's focus) is on it or it is
    /// the chosen one, ticked while a switch is on; the theme's own fill hidden, and the label white (half white when it
    /// cannot be used), as draw_text_outline drew it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GmlControlSkin : MonoBehaviour
    {
        private static readonly Color Disabled = new Color(1f, 1f, 1f, 0.5f);

        private GmlPlate _plate;
        private MaStateRelay _relay;
        private TMP_Text _label;
        private Graphic[] _hidden;

        /// <summary>True when the control is the chosen one (a vote's answer): drawn as if the pointer were on it.</summary>
        public System.Func<bool> Selected;

        /// <summary>A switch's value: the checkbox's tick.</summary>
        public System.Func<bool> Ticked;

        /// <summary>The plate drawn behind the control.</summary>
        public GmlPlate Plate => _plate;

        internal void Init(GmlPlate plate, MaStateRelay relay, TMP_Text label, params Graphic[] hidden)
        {
            _plate = plate;
            _relay = relay;
            _label = label;
            _hidden = hidden;
            LateUpdate();
        }

        private void LateUpdate()
        {
            if (_plate == null)
            {
                return;
            }

            ControlState state = _relay != null ? _relay.State : ControlState.Normal;
            _plate.Over = state == ControlState.Hover || state == ControlState.Pressed || (Selected != null && Selected());
            if (Ticked != null)
            {
                _plate.Checked = Ticked();
            }

            if (_hidden != null)
            {
                foreach (Graphic graphic in _hidden)
                {
                    if (graphic != null && graphic.canvasRenderer.GetAlpha() > 0f)
                    {
                        graphic.canvasRenderer.SetAlpha(0f); // the theme's fill and border: the plate is the body now
                    }
                }
            }

            if (_label != null)
            {
                Color color = state == ControlState.Disabled ? Disabled : Color.white;
                if (_label.color != color)
                {
                    _label.color = color;
                }

                if ((_label.fontStyle & FontStyles.Bold) != 0)
                {
                    _label.fontStyle &= ~FontStyles.Bold; // the theme bolds a primary button; GameMaker drew every label as baked
                }
            }
        }
    }

    /// <summary>
    /// A less / more arrow (obj_btn_*_less / _more / _last / _next): spr_arrowleft or spr_arrowright over its back, stepping its
    /// row's setting one notch a click. A uGUI Button, so UiSounds hears it (sfx_select, as the arrows played), kept out of
    /// keyboard navigation: Left and Right already step the setting it sits beside.
    /// </summary>
    public sealed class GmlStepper : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        /// <summary>An arrow's size on the kit's rows: spr_arrowleft's 89x64 at half.</summary>
        public static readonly Vector2 Size = new Vector2(44.5f, 32f);

        private GmlPlate _plate;

        /// <summary>-1 for the less arrow, +1 for the more arrow.</summary>
        public int Direction { get; private set; }

        /// <summary>The arrow's button.</summary>
        public Button Button { get; private set; }

        /// <summary>Puts a less arrow before <paramref name="control"/> and a more arrow after it, each calling <paramref name="step"/> with -1 or +1.</summary>
        public static void AddPair(MaFormRow row, RectTransform control, System.Action<int> step)
        {
            Add(row.transform, control.GetSiblingIndex(), -1, step);
            Add(row.transform, control.GetSiblingIndex() + 1, 1, step);
        }

        private static GmlStepper Add(Transform row, int index, int direction, System.Action<int> step)
        {
            var rect = new GameObject(direction < 0 ? "GML less" : "GML more", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = row.gameObject.layer;
            rect.SetParent(row, false);
            rect.SetSiblingIndex(index);
            LayoutElement size = rect.gameObject.AddComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = Size.x;
            size.minHeight = size.preferredHeight = Size.y;
            size.flexibleWidth = 0f;

            GmlPlate plate = GmlPlate.Add(rect, direction < 0 ? Plate.ArrowLeft : Plate.ArrowRight, stretch: false, raycast: true);
            GmlStepper stepper = rect.gameObject.AddComponent<GmlStepper>();
            stepper._plate = plate;
            stepper.Direction = direction;
            stepper.Button = rect.gameObject.AddComponent<Button>();
            stepper.Button.transition = Selectable.Transition.None;
            stepper.Button.navigation = new Navigation { mode = Navigation.Mode.None };
            stepper.Button.targetGraphic = plate != null ? plate.Face : null;
            stepper.Button.onClick.AddListener(() => step(direction));
            return stepper;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_plate != null) _plate.Over = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_plate != null) _plate.Over = false;
        }

        private void OnDisable()
        {
            if (_plate != null) _plate.Over = false;
        }
    }
}
