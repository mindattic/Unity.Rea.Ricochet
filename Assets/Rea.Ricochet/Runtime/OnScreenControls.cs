// The state the clickable on-screen game controls (ControlPad: obj_btn_fire, obj_btn_degreeleft/right, obj_btn_useitem,
// obj_btn_endturn, obj_btn_moveforward/backward, which obj_mouse clicked in the original) feed GameInput, next to the keys
// (U.RICO-T29). The turn and drive buttons are held (the original rotated and drove on mouse held, stopped on release);
// Fire, Use Item and End Turn are one press each, kept for the frame they were clicked in and the next, so a click that
// lands after the owner's input has already been read this frame is still seen, once.
using UnityEngine;

namespace Ricochet
{
    internal static class OnScreenControls
    {
        /// <summary>One click, readable (any number of times) for the frame it landed in or the next, but acted on once.</summary>
        internal sealed class Press
        {
            private int _clicked = -100;
            private int _read = -100;

            public void Click()
            {
                _clicked = Time.frameCount;
                _read = -100;
            }

            public bool Take()
            {
                int frame = Time.frameCount;
                if (frame > _clicked + 1)
                {
                    return false; // nobody was reading: the click is stale
                }

                if (_read >= _clicked && _read != frame)
                {
                    return false; // already acted on in an earlier frame
                }

                _read = frame;
                return true;
            }

            public void Clear()
            {
                _clicked = _read = -100;
            }
        }

        /// <summary>+1 while the turn-left button is held (aim counterclockwise), -1 while turn-right is, else 0.</summary>
        public static float Turn;

        /// <summary>+1 while move-forward is held, -1 while move-backward is, else 0.</summary>
        public static int Drive;

        public static readonly Press Fire = new Press();
        public static readonly Press UseItem = new Press();
        public static readonly Press EndTurn = new Press();

        /// <summary>Lets go of everything (the pad hid, or a test finished).</summary>
        public static void Reset()
        {
            Turn = 0f;
            Drive = 0;
            Fire.Clear();
            UseItem.Clear();
            EndTurn.Clear();
        }
    }
}
