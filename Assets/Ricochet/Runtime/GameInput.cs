// Reads the aim keys with whichever input handling the project uses (Project Settings > Player > Active Input
// Handling): Left/Right or A/D turn the aim, Up/Down or W/S charge the power, Space fires. Nothing here is about
// networking.
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

        private static float Axis(bool positive, bool negative)
        {
            return (positive ? 1f : 0f) - (negative ? 1f : 0f);
        }
    }
}
