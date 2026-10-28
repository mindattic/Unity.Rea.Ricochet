// Reads the keys through the Input System's RicochetControls asset (UR-T36: Active Input Handling is Input System
// Package, so these actions are the only input path left, rebindable through the Rebind controls screen and
// Unity.Shared profile instead of the legacy Input Manager). The bindings still land on the original's obj_game keys
// by default: A/D or Left/Right turn the tank and its aim, W/S drive it forward and back, F ends the turn, Ctrl uses
// the item, Space fires; Q/E cycle the weapon, Z/C the item, and 1/2/3 set a grenade's fuse (shorter, longer,
// longest). There is no power (U.RICO-T88): the original's Up/Down did nothing. Ping, Pause and Fullscreen are read here too, for UR-T29/UR-T43 to wire up; nothing here is about networking.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Ricochet
{
    internal static class GameInput
    {
        private static InputActionMap _cachedMap;
        private static readonly Dictionary<string, InputAction> Cache = new Dictionary<string, InputAction>();

        /// <summary>+1 turns the aim counterclockwise (left), -1 clockwise (right), 0 holds it.</summary>
        public static float Turn() => Mathf.Clamp(Act("Aim").ReadValue<float>() + OnScreenControls.Turn, -1f, 1f);

        /// <summary>+1 while driving forward (move_forward), -1 while driving backward (move_backward), 0 otherwise.</summary>
        public static int Drive() => Round(Act("Drive").ReadValue<float>() + OnScreenControls.Drive);

        /// <summary>True on the frame End Turn (next_player) is pressed.</summary>
        public static bool EndTurn() => Act("EndTurn").WasPressedThisFrame() || OnScreenControls.EndTurn.Take();

        /// <summary>True on the frame Fire is pressed, unless the click landed on a menu (obj_game never fired through the UI).</summary>
        public static bool Fire() => PressedOutsideUi(Act("Fire")) || OnScreenControls.Fire.Take();

        /// <summary>True on the frame Reroll is pressed: roll another random level (practice only).</summary>
        public static bool Reroll() => Act("Reroll").WasPressedThisFrame();

        /// <summary>The border step pressed this frame: -1 (PageUp, thinner), +1 (PageDown, thicker) or 0; each rolls the level again.</summary>
        public static int BorderStep() => Act("BorderThinner").WasPressedThisFrame() ? -1 : Act("BorderThicker").WasPressedThisFrame() ? 1 : 0;

        /// <summary>The weapon to cycle to this frame: -1 (previous), +1 (next) or 0.</summary>
        public static int CycleWeapon() => Step(Act("CycleWeapon"));

        /// <summary>The item to cycle to this frame: -1 (previous), +1 (next) or 0.</summary>
        public static int CycleItem() => Step(Act("CycleItem"));

        /// <summary>
        /// True on the frame Use Item (obj_game's vk_control: use the selected item) is pressed, unless the click
        /// landed on a menu.
        /// </summary>
        public static bool UseItem() => PressedOutsideUi(Act("UseItem")) || OnScreenControls.UseItem.Take();

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

        /// <summary>True on the frame Ping (ping_player: show every player's position) is pressed. Read by ReadyCard (UR-T29).</summary>
        public static bool Ping() => Act("Ping").WasPressedThisFrame();

        /// <summary>True on the frame Pause is pressed. Read by UR-T43's pause menu.</summary>
        public static bool Pause() => Act("Pause").WasPressedThisFrame();

        /// <summary>True on the frame Fullscreen is pressed. Read by MatchHud (UR-T29).</summary>
        public static bool Fullscreen() => Act("Fullscreen").WasPressedThisFrame();

        /// <summary>
        /// The heading the numpad snaps the aim to this frame, or null: obj_game's numpad keys set the barrel straight to
        /// 6 = 0, 9 = 45, 8 = 90, 7 = 135, 4 = 180, 1 = 225, 2 = 270, 3 = 315 degrees (counterclockwise from the right).
        /// Fixed keys, like the original's; they are not in the rebind list.
        /// </summary>
        public static double? AimSnap()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return null;
            }

            if (keyboard.numpad6Key.wasPressedThisFrame) return 0;
            if (keyboard.numpad9Key.wasPressedThisFrame) return 45;
            if (keyboard.numpad8Key.wasPressedThisFrame) return 90;
            if (keyboard.numpad7Key.wasPressedThisFrame) return 135;
            if (keyboard.numpad4Key.wasPressedThisFrame) return 180;
            if (keyboard.numpad1Key.wasPressedThisFrame) return 225;
            if (keyboard.numpad2Key.wasPressedThisFrame) return 270;
            if (keyboard.numpad3Key.wasPressedThisFrame) return 315;
            return null;
        }

        /// <summary>True on the frame numpad 5 is pressed: obj_game puts the aim back where it was before the last numpad snap.</summary>
        public static bool AimSnapBack() => Keyboard.current != null && Keyboard.current.numpad5Key.wasPressedThisFrame;

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
        // like a key), but the original stepped once per press: WasPressedThisFrame() is the Input System's own
        // per-frame transition (true only on the frame the axis crosses its press point), so reading it here - rather
        // than tracking a sign across calls ourselves - means two reads of CycleWeapon()/CycleItem() in the same
        // frame (one for the game, one for a HUD preview, say) agree, instead of the first consuming the step and
        // leaving the second with nothing.
        private static int Step(InputAction action) => action.WasPressedThisFrame() ? Math.Sign(action.ReadValue<float>()) : 0;

        private static int Round(float value) => value > 0.5f ? 1 : value < -0.5f ? -1 : 0;

        // Space and the mouse buttons double as Fire/UseItem; a click that lands on a menu button must not also
        // fire, the way obj_game's own input never read through its rooms' GUI.
        // Only a mouse press is blocked: the idle cursor resting over a HUD element must not swallow Space or a gamepad button.
        private static bool PressedOutsideUi(InputAction action) =>
            action.WasPressedThisFrame() && !(action.activeControl?.device is Mouse && PointerOverUi());

        private static bool PointerOverUi() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        /// <summary>Forgets the cached actions (tests only).</summary>
        internal static void ResetForTests()
        {
            _cachedMap = null;
            Cache.Clear();
            OnScreenControls.Reset();
        }
    }
}
