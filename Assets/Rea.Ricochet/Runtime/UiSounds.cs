// The menus' sounds (U.RICO-T50), as the original's buttons made them: every obj_btn_* and obj_checkbox_* played sfx_flick
// when the pointer came over it (Mouse Enter), and on Left Pressed sfx_click (a button, a checkbox) or sfx_select (the
// settings' less / more / last / next steppers, which the rebuild's sliders are); one that could not be pressed played
// sfx_disabled (obj_btn_addplayer, obj_btn_removeplayer at their limits). The rebuild's menus are MindAttic Components
// (uGUI Selectables), so one watcher beside GameAudio hears them all, whichever screen built them, instead of every
// AddButton call site playing its own. The HUD's weapon and item icons are not Selectables and play their own (HudIcons).
using System.Collections.Generic;
using Ricochet.Core.Menu;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Ricochet
{
    public sealed class UiSounds : MonoBehaviour
    {
        private readonly List<RaycastResult> _hits = new List<RaycastResult>();
        private PointerEventData _pointer;
        private EventSystem _events;
        private Selectable _hovered;

        /// <summary>The control under the pointer now (null for none), for tests.</summary>
        public Selectable Hovered => _hovered;

        /// <summary>
        /// The frame a pointer last pressed a control and played its cue (-1 for never): a screen change that frame has had
        /// its click already (ScreenFade, U.RICO-T53).
        /// </summary>
        public static int PressedFrame { get; private set; } = -1;

        /// <summary>
        /// The frame the pointer was let go after a press that played its cue (-1 for never): uGUI buttons act on release,
        /// so a screen change that frame has had its click too (U.RICO-T53).
        /// </summary>
        public static int ReleasedFrame { get; private set; } = -1;

        private static bool _pressCued;

        /// <summary>True when the pointer's press or release on <paramref name="frame"/> already made the click.</summary>
        public static bool PointerClickedOn(int frame)
        {
            return frame >= 0 && (frame == PressedFrame || frame == ReleasedFrame);
        }

        /// <summary>
        /// The cue pressing <paramref name="control"/> plays: sfx_disabled when it cannot be pressed, sfx_select for a
        /// slider (the original's steppers), sfx_click for anything else (a button, a toggle); null for none.
        /// </summary>
        public static SoundCue? PressCue(Selectable control)
        {
            if (control == null)
            {
                return null;
            }

            if (!control.IsInteractable())
            {
                return SoundCue.Disabled;
            }

            // The original's less / more arrows (GmlStepper, U.RICO-T78) played sfx_select, as the sliders they step do.
            return control is Slider || control is Scrollbar || control.GetComponent<GmlStepper>() != null ? SoundCue.Select : SoundCue.Click;
        }

        /// <summary>The cue the pointer coming over <paramref name="control"/> plays: sfx_flick when it can be pressed.</summary>
        public static SoundCue? HoverCue(Selectable control)
        {
            return control != null && control.IsInteractable() && !(control is Scrollbar) ? SoundCue.Flick : (SoundCue?)null;
        }

        /// <summary>What the pointer is over, and whether it was pressed this frame: the sounds for both.</summary>
        public void Observe(Selectable under, bool pressed, bool released = false)
        {
            if (released && _pressCued)
            {
                ReleasedFrame = Time.frameCount; // U.RICO-T53: the button acts now; its click was the press's
                _pressCued = false;
            }

            if (under != _hovered)
            {
                _hovered = under;
                GameAudio.Play(HoverCue(under));
            }

            if (pressed)
            {
                SoundCue? cue = PressCue(under);
                _pressCued = cue != null;
                if (cue != null)
                {
                    PressedFrame = Time.frameCount; // U.RICO-T53: this press is the screen change's click, if it changes one
                }

                GameAudio.Play(cue);
            }
        }

        private void Update()
        {
            Pointer pointer = Pointer.current;
            EventSystem events = EventSystem.current;
            if (pointer == null || events == null)
            {
                return;
            }

            if (_pointer == null || _events != events)
            {
                _pointer = new PointerEventData(events);
                _events = events;
            }

            _pointer.position = pointer.position.ReadValue();
            _hits.Clear();
            events.RaycastAll(_pointer, _hits);
            Selectable under = null;
            foreach (RaycastResult hit in _hits)
            {
                if (hit.gameObject == null)
                {
                    continue;
                }

                under = hit.gameObject.GetComponentInParent<Selectable>();
                break; // the topmost thing the pointer is over decides, as a click would
            }

            Observe(under, pointer.press.wasPressedThisFrame, pointer.press.wasReleasedThisFrame);
        }
    }
}
