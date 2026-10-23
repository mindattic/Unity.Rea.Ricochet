// Reads the aim keys with whichever input handling the project uses (Project Settings > Player > Active Input
// Handling): Left/Right or A/D turn the aim, Up/Down or W/S charge the power, Space fires; Q/E cycle the weapon, Z/C the
// item, F uses the item, and 1/2/3 set a grenade's fuse (shorter, longer, longest). Nothing here is about networking.
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
            return keyboard == null ? 0f : Axis(keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed, keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetAxisRaw("Vertical");
#else
            return 0f;
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

        /// <summary>True on the frame F (use the selected item) is pressed.</summary>
        public static bool UseItem()
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
