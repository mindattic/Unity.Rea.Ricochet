// Reads the keys through the Input System's RicochetControls asset (UR-T36: Active Input Handling is Input System
// Package, so these actions are the only input path left, rebindable through the Rebind controls screen and
// Unity.Shared profile instead of the legacy Input Manager). The bindings still land on the original's obj_game keys
// by default: A/D or Left/Right turn the tank and its aim, W/S drive it forward and back, F ends the turn, Ctrl uses
// the item, Space fires; Q/E cycle the weapon, Z/C the item, and 1/2/3 set a grenade's fuse (shorter, longer,
// longest). The original had no power meter, so the rebuild's power stays on Up/Down alone (W/S were the drive
// keys). Ping, Pause and Fullscreen are read here too, for UR-T29/UR-T43 to wire up; nothing here is about networking.
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace Ricochet
{
    internal static class GameInput
    {
        private static int _cycleWeaponSign;
        private static int _cycleItemSign;
        private static InputActionMap _cachedMap;
        private static readonly Dictionary<string, InputAction> Cache = new Dictionary<string, InputAction>();

        /// <summary>+1 turns the aim counterclockwise (left), -1 clockwise (right), 0 holds it.</summary>
        public static float Turn() => Act("Aim").ReadValue<float>();

        /// <summary>+1 raises the power, -1 lowers it, 0 holds it.</summary>
        public static float Charge() => Act("Power").ReadValue<float>();

        /// <summary>+1 while driving forward (move_forward), -1 while driving backward (move_backward), 0 otherwise.</summary>
        public static int Drive() => Round(Act("Drive").ReadValue<float>());

        /// <summary>True on the frame End Turn (next_player) is pressed.</summary>
        public static bool EndTurn() => Act("EndTurn").WasPressedThisFrame();

        /// <summary>True on the frame Fire is pressed.</summary>
        public static bool Fire() => Act("Fire").WasPressedThisFrame();

        /// <summary>True on the frame Reroll is pressed: roll another random level (practice only).</summary>
        public static bool Reroll() => Act("Reroll").WasPressedThisFrame();

        /// <summary>The weapon to cycle to this frame: -1 (previous), +1 (next) or 0.</summary>
        public static int CycleWeapon() => Edge(ref _cycleWeaponSign, Act("CycleWeapon").ReadValue<float>());

        /// <summary>The item to cycle to this frame: -1 (previous), +1 (next) or 0.</summary>
        public static int CycleItem() => Edge(ref _cycleItemSign, Act("CycleItem").ReadValue<float>());

        /// <summary>True on the frame Use Item (obj_game's vk_control: use the selected item) is pressed.</summary>
        public static bool UseItem() => Act("UseItem").WasPressedThisFrame();

        /// <summary>True on the frame the shorten-fuse key (obj_game's 1) is pressed.</summary>
        public static bool Fuse1() => Act("Fuse1").WasPressedThisFrame();

        /// <summary>True on the frame the lengthen-fuse key (obj_game's 2) is pressed.</summary>
        public static bool Fuse2() => Act("Fuse2").WasPressedThisFrame();

        /// <summary>True on the frame the max-fuse key (obj_game's 3) is pressed.</summary>
        public static bool Fuse3() => Act("Fuse3").WasPressedThisFrame();

        /// <summary>The grenade fuse key pressed this frame: -1 shorter, +1 longer, +2 longest, 0 none.</summary>
        public static int Fuse()
        {
            if (Fuse1())
            {
                return -1;
            }

            if (Fuse2())
            {
                return 1;
            }

            return Fuse3() ? 2 : 0;
        }

        /// <summary>True on the frame Ping (ping_player: show every player's position) is pressed. Read by UR-T29.</summary>
        public static bool Ping() => Act("Ping").WasPressedThisFrame();

        /// <summary>True on the frame Pause is pressed. Read by UR-T43's pause menu.</summary>
        public static bool Pause() => Act("Pause").WasPressedThisFrame();

        /// <summary>True on the frame Fullscreen is pressed. Read by UR-T29.</summary>
        public static bool Fullscreen() => Act("Fullscreen").WasPressedThisFrame();

        // The named action, cached by name; the cache is thrown away whenever InputBindings hands back a different
        // map instance (a rebind-screen reset in a test re-creates it) so a stale InputAction is never read from.
        private static InputAction Act(string name)
        {
            InputActionMap map = InputBindings.Gameplay;
            if (!ReferenceEquals(map, _cachedMap))
            {
                _cachedMap = map;
                Cache.Clear();
            }

            if (!Cache.TryGetValue(name, out InputAction action))
            {
                action = map.FindAction(name, throwIfNotFound: true);
                Cache[name] = action;
            }

            return action;
        }

        // CycleWeapon/CycleItem are bound as a continuous -1..1 axis (so a held gamepad shoulder button still reads
        // like a key), but the original stepped once per press: this turns a 0 -> +-1 transition into one step, and
        // nothing while the axis stays away from zero or sits there.
        private static int Edge(ref int previousSign, float raw)
        {
            int sign = raw > 0.5f ? 1 : raw < -0.5f ? -1 : 0;
            int step = sign != 0 && sign != previousSign ? sign : 0;
            previousSign = sign;
            return step;
        }

        private static int Round(float value) => value > 0.5f ? 1 : value < -0.5f ? -1 : 0;

        /// <summary>Forgets the last frame's cycle-key state and the cached actions (tests only).</summary>
        internal static void ResetForTests()
        {
            _cycleWeaponSign = 0;
            _cycleItemSign = 0;
            _cachedMap = null;
            Cache.Clear();
        }
    }
}
