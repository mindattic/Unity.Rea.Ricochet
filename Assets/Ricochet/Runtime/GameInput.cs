// Reads the keys with whichever input handling the project uses (Project Settings > Player > Active Input Handling),
// bound as obj_game's keyboard events were: A/D or Left/Right turn the tank and its aim, W/S drive it forward and back,
// F ends the turn, Ctrl uses the item, Space fires; Q/E cycle the weapon, Z/C the item, and 1/2/3 set a grenade's fuse
// (shorter, longer, longest). The original had no power meter, so the rebuild's power stays on Up/Down alone (W/S were
// the drive keys). Nothing here is about networking.
#if ENABLE_INPUT_SYSTEM && GAME_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine;

namespace Ricochet
{
    internal static class GameInput
    {
        /// <summary>+1 turns the aim counterclockwise (left), -1 clockwise (right), 0 holds it.</summary>
        public static float Turn()
        {
#if ENABLE_INPUT_SYSTEM && GAME_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard == null ? 0f : Axis(keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed, keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return -Input.GetAxisRaw("Horizontal");
#else
            return 0f;
#endif
        }

        /// <summary>+1 raises the power, -1 lowers it, 0 holds it.</summary>
        public static float Charge()
        {
#if ENABLE_INPUT_SYSTEM && GAME_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard == null ? 0f : Axis(keyboard.upArrowKey.isPressed, keyboard.downArrowKey.isPressed);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return (Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
#else
            return 0f;
#endif
        }

        /// <summary>+1 while W is held (move_forward), -1 while S is held (move_backward), 0 otherwise.</summary>
        public static int Drive()
        {
#if ENABLE_INPUT_SYSTEM && GAME_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard == null ? 0 : (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0);
#else
            return 0;
#endif
        }

        /// <summary>True on the frame F is pressed (next_player: end the turn).</summary>
        public static bool EndTurn()
        {
#if ENABLE_INPUT_SYSTEM && GAME_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.fKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.F);
#else
            return false;
#endif
        }

        /// <summary>True on the frame Space is pressed.</summary>
        public static bool Fire()
        {
#if ENABLE_INPUT_SYSTEM && GAME_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Space);
#else
            return false;
#endif
        }

        /// <summary>True on the frame R, PageUp or PageDown is pressed: roll another random level (practice only).</summary>
        public static bool Reroll()
        {
#if ENABLE_INPUT_SYSTEM && GAME_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && (keyboard.rKey.wasPressedThisFrame || keyboard.pageUpKey.wasPressedThisFrame || keyboard.pageDownKey.wasPressedThisFrame);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.PageUp) || Input.GetKeyDown(KeyCode.PageDown);
#else
            return false;
#endif
        }

        /// <summary>The weapon to cycle to this frame: -1 (Q, left), +1 (E, right) or 0.</summary>
        public static int CycleWeapon()
        {
#if ENABLE_INPUT_SYSTEM && GAME_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard == null ? 0 : (keyboard.eKey.wasPressedThisFrame ? 1 : 0) - (keyboard.qKey.wasPressedThisFrame ? 1 : 0);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return (Input.GetKeyDown(KeyCode.E) ? 1 : 0) - (Input.GetKeyDown(KeyCode.Q) ? 1 : 0);
#else
            return 0;
#endif
        }

        /// <summary>The item to cycle to this frame: -1 (Z, left), +1 (C, right) or 0.</summary>
        public static int CycleItem()
        {
#if ENABLE_INPUT_SYSTEM && GAME_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard == null ? 0 : (keyboard.cKey.wasPressedThisFrame ? 1 : 0) - (keyboard.zKey.wasPressedThisFrame ? 1 : 0);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return (Input.GetKeyDown(KeyCode.C) ? 1 : 0) - (Input.GetKeyDown(KeyCode.Z) ? 1 : 0);
#else
            return 0;
#endif
        }

        /// <summary>True on the frame Ctrl (obj_game's vk_control: use the selected item) is pressed.</summary>
        public static bool UseItem()
        {
#if ENABLE_INPUT_SYSTEM && GAME_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && (keyboard.leftCtrlKey.wasPressedThisFrame || keyboard.rightCtrlKey.wasPressedThisFrame);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl);
#else
            return false;
#endif
        }

        /// <summary>The grenade fuse key pressed this frame (obj_game's 1, 2, 3): -1 shorter, +1 longer, +2 longest, 0 none.</summary>
        public static int Fuse()
        {
#if ENABLE_INPUT_SYSTEM && GAME_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return 0;
            }

            return keyboard.digit1Key.wasPressedThisFrame ? -1 : keyboard.digit2Key.wasPressedThisFrame ? 1 : keyboard.digit3Key.wasPressedThisFrame ? 2 : 0;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Alpha1) ? -1 : Input.GetKeyDown(KeyCode.Alpha2) ? 1 : Input.GetKeyDown(KeyCode.Alpha3) ? 2 : 0;
#else
            return 0;
#endif
        }

        private static float Axis(bool positive, bool negative)
        {
            return (positive ? 1f : 0f) - (negative ? 1f : 0f);
        }
    }
}
