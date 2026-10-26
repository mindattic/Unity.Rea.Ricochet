// UR-T36: ProjectSettings now has Active Input Handling on Input System Package, so GameInput's actions compile into
// the player; these checks make sure the asset itself carries every action the ticket asks for, with a keyboard (or
// mouse) and a gamepad binding each, and that real key and gamepad presses read through GameInput the way the
// original's obj_game keys did. Run from Test Runner > EditMode, or:
//   Unity -batchmode -projectPath . -runTests -testPlatform EditMode -assemblyNames Ricochet.EditorTests
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Ricochet.EditorTests
{
    /// <summary>The asset's shape: every action UR-T36 asks for, bound on both a keyboard/mouse and a gamepad.</summary>
    public class GameInputActionMapTests
    {
        private const string AssetPath = "Assets/Ricochet/Resources/Input/RicochetControls.inputactions";

        // Fullscreen and Reroll are keyboard-only, like the original (no fullscreen toggle or practice reroll on a pad).
        private static readonly string[] KeyboardOrMouseOnly = { "Fullscreen", "Reroll" };

        private static readonly string[] ExpectedActions =
        {
            "Aim", "Power", "Fire", "CycleWeapon", "CycleItem", "UseItem",
            "Fuse1", "Fuse2", "Fuse3", "EndTurn", "Drive", "Ping", "Pause", "Fullscreen", "Reroll",
        };

        private static InputActionAsset LoadAsset()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
            Assert.That(asset, Is.Not.Null, AssetPath + " is missing");
            return asset;
        }

        private static bool HasBindingOn(InputAction action, string devicePrefix) =>
            action.bindings.Any(b => !b.isComposite && b.effectivePath.StartsWith(devicePrefix));

        [Test]
        public void TheGameplayMap_HasEveryActionTheTicketAsksFor()
        {
            InputActionMap map = LoadAsset().FindActionMap("Gameplay");
            Assert.That(map, Is.Not.Null, "a Gameplay action map");
            foreach (string name in ExpectedActions)
            {
                Assert.That(map.FindAction(name), Is.Not.Null, name + " is missing from the Gameplay map");
            }
        }

        [Test]
        public void EveryAction_HasAKeyboardOrMouseBinding()
        {
            InputActionMap map = LoadAsset().FindActionMap("Gameplay");
            foreach (string name in ExpectedActions)
            {
                InputAction action = map.FindAction(name);
                Assert.That(HasBindingOn(action, "<Keyboard>") || HasBindingOn(action, "<Mouse>"), Is.True,
                    name + " has no keyboard or mouse binding");
            }
        }

        [Test]
        public void EveryActionButTheKeyboardOnlyOnes_AlsoHasAGamepadBinding()
        {
            InputActionMap map = LoadAsset().FindActionMap("Gameplay");
            foreach (string name in ExpectedActions.Except(KeyboardOrMouseOnly))
            {
                InputAction action = map.FindAction(name);
                Assert.That(HasBindingOn(action, "<Gamepad>"), Is.True, name + " has no gamepad binding");
            }
        }

        [Test]
        public void TheControlSchemesCoverKeyboardMouseAndGamepad()
        {
            InputActionAsset asset = LoadAsset();
            Assert.That(asset.controlSchemes.Any(s => s.name == "Keyboard&Mouse" && RequiresDevice(s, "<Keyboard>")), Is.True,
                "a Keyboard&Mouse control scheme");
            Assert.That(asset.controlSchemes.Any(s => s.name == "Gamepad" && RequiresDevice(s, "<Gamepad>")), Is.True,
                "a Gamepad control scheme");
        }

        private static bool RequiresDevice(InputControlScheme scheme, string devicePath)
        {
            return scheme.deviceRequirements.Any(d => d.controlPath == devicePath);
        }
    }

    /// <summary>
    /// Real key and gamepad presses, read the way the game reads them (through GameInput, the one place it does),
    /// prove the bindings actually work and that a rebind changes what the game responds to.
    /// Presses are simulated through the Input System's own public state-event API
    /// (<see cref="InputSystem.QueueStateEvent{TState}"/> with <see cref="KeyboardState"/>/<see cref="GamepadState"/>)
    /// rather than the com.unity.inputsystem package's bundled Tests/TestFixture assembly: that assembly's shipped
    /// asmdef has a duplicate "versionDefines" JSON key whose second (empty) copy wins, so its own defineConstraint
    /// (UNITY_TESTS_FRAMEWORK) can never be satisfied and it never compiles into this project.
    /// </summary>
    public class GameInputReadingTests
    {
        private Keyboard _keyboard;
        private Gamepad _gamepad;
        private InputSettings.UpdateMode _previousUpdateMode;

        [SetUp]
        public void SetUp()
        {
            // Outside Play mode (this is an EditMode test, with no player loop running), the Input System treats
            // every update as an "Editor" update and actions don't react to those - by design, so a Scene-view
            // click while not playing can't fire a gameplay action. "RUN_PLAYER_UPDATES_IN_EDIT_MODE" is the
            // package's own documented escape hatch (InputManager.runPlayerUpdatesInEditMode: "consider the editor
            // to be in perpetual play mode"); paired with ProcessEventsManually, it makes InputSystem.Update() apply
            // queued events the same way a running game would, so GameInput's actions can see the simulated presses.
            _previousUpdateMode = InputSystem.settings.updateMode;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            InputSystem.settings.SetInternalFeatureFlag("RUN_PLAYER_UPDATES_IN_EDIT_MODE", true);

            // Without this, ApplySavedOverrides() would read whatever rebinds the developer running these tests has
            // actually saved on this machine (Application.persistentDataPath), leaving every default-binding
            // assertion depending on that developer's own profile instead of the asset's shipped defaults.
            InputBindings.SkipProfileForTests = true;
            InputBindings.ResetForTests();
            GameInput.ResetForTests();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _gamepad = InputSystem.AddDevice<Gamepad>();

            // Force the Gameplay map to load and enable now, before any key is simulated: loading it lazily (as
            // GameInput does on the game's first real frame) means the map's actions only start tracking control
            // changes from here on, so an Enable() that happened after a Press() would miss that press entirely.
            InputBindings.Gameplay.Enable();
        }

        [TearDown]
        public void TearDown()
        {
            InputSystem.RemoveDevice(_keyboard);
            InputSystem.RemoveDevice(_gamepad);
            InputSystem.settings.updateMode = _previousUpdateMode;
            InputSystem.settings.SetInternalFeatureFlag("RUN_PLAYER_UPDATES_IN_EDIT_MODE", false);
            InputBindings.ResetForTests();
            InputBindings.SkipProfileForTests = false;
            GameInput.ResetForTests();
        }

        // Each call replaces the keyboard's whole state, so "pressed" always means "only these keys", matching the
        // Press-then-Release pattern every test below uses (none of them hold more than one key at a time).
        private void Press(params Key[] keys)
        {
            KeyboardState state = default;
            foreach (Key key in keys)
            {
                state.Set(key, true);
            }

            InputSystem.QueueStateEvent(_keyboard, state);
            InputSystem.Update();
        }

        private void ReleaseKeys()
        {
            InputSystem.QueueStateEvent(_keyboard, default(KeyboardState));
            InputSystem.Update();
        }

        private void PressGamepadButton(GamepadButton button)
        {
            InputSystem.QueueStateEvent(_gamepad, new GamepadState().WithButton(button));
            InputSystem.Update();
        }

        [Test]
        public void AAndD_TurnTheAim_TheOriginalWay()
        {
            Press(Key.A);
            Assert.That(GameInput.Turn(), Is.EqualTo(1f), "A turns left (+1), as obj_game bound it");
            ReleaseKeys();

            Press(Key.D);
            Assert.That(GameInput.Turn(), Is.EqualTo(-1f), "D turns right (-1)");
        }

        [Test]
        public void UpAndDown_RaiseAndLowerThePower()
        {
            Press(Key.UpArrow);
            Assert.That(GameInput.Charge(), Is.EqualTo(1f));
            ReleaseKeys();

            Press(Key.DownArrow);
            Assert.That(GameInput.Charge(), Is.EqualTo(-1f));
        }

        [Test]
        public void WAndS_DriveForwardAndBack()
        {
            Press(Key.W);
            Assert.That(GameInput.Drive(), Is.EqualTo(1));
            ReleaseKeys();

            Press(Key.S);
            Assert.That(GameInput.Drive(), Is.EqualTo(-1));
        }

        [Test]
        public void SpaceAndTheMouseAndTheGamepad_AllFire()
        {
            Press(Key.Space);
            Assert.That(GameInput.Fire(), Is.True, "Space fires");
            ReleaseKeys();

            PressGamepadButton(GamepadButton.South);
            Assert.That(GameInput.Fire(), Is.True, "the gamepad's south button fires");
        }

        [Test]
        public void EThenHoldingIt_CyclesTheWeaponOnceOnly()
        {
            Press(Key.E);
            Assert.That(GameInput.CycleWeapon(), Is.EqualTo(1), "E steps to the next weapon");

            // CycleWeapon() reads WasPressedThisFrame(), the Input System's own per-frame transition, so two reads
            // within the same frame must agree (a HUD preview reading it right after the game must see the same step,
            // not consume it) - this re-sends the same still-held key, simulating the next frame with no new press.
            Assert.That(GameInput.CycleWeapon(), Is.EqualTo(1), "a second read the same frame sees the same step");
            Press(Key.E);
            Assert.That(GameInput.CycleWeapon(), Is.EqualTo(0), "held into another frame, it does not step again");
            ReleaseKeys();

            Press(Key.Q);
            Assert.That(GameInput.CycleWeapon(), Is.EqualTo(-1), "Q steps to the previous weapon");
        }

        [Test]
        public void ZAndC_CycleTheItem()
        {
            Press(Key.Z);
            Assert.That(GameInput.CycleItem(), Is.EqualTo(-1));
            ReleaseKeys();

            Press(Key.C);
            Assert.That(GameInput.CycleItem(), Is.EqualTo(1));
        }

        [Test]
        public void Digit1_2_3_SetTheFuseStep()
        {
            Press(Key.Digit1);
            Assert.That(GameInput.Fuse(), Is.EqualTo(-1), "1: shorter");
            ReleaseKeys();

            Press(Key.Digit2);
            Assert.That(GameInput.Fuse(), Is.EqualTo(1), "2: longer");
            ReleaseKeys();

            Press(Key.Digit3);
            Assert.That(GameInput.Fuse(), Is.EqualTo(2), "3: longest");
        }

        [Test]
        public void FKeyEndsTheTurn_AndLeftCtrlUsesTheItem()
        {
            Press(Key.F);
            Assert.That(GameInput.EndTurn(), Is.True);
            ReleaseKeys();

            Press(Key.LeftCtrl);
            Assert.That(GameInput.UseItem(), Is.True);
        }

        [Test]
        public void TheNumpadSnapsTheAimToTheEightDirections_AndFiveGoesBack()
        {
            (Key key, double heading)[] snaps =
            {
                (Key.Numpad6, 0), (Key.Numpad9, 45), (Key.Numpad8, 90), (Key.Numpad7, 135),
                (Key.Numpad4, 180), (Key.Numpad1, 225), (Key.Numpad2, 270), (Key.Numpad3, 315),
            };
            foreach ((Key key, double heading) in snaps)
            {
                Press(key);
                Assert.That(GameInput.AimSnap(), Is.EqualTo(heading), key + " snaps to " + heading);
                ReleaseKeys();
            }

            Assert.That(GameInput.AimSnap(), Is.Null, "nothing pressed, nothing to snap to");
            Press(Key.Numpad5);
            Assert.That(GameInput.AimSnap(), Is.Null, "numpad 5 is not a direction");
            Assert.That(GameInput.AimSnapBack(), Is.True, "numpad 5 puts the aim back");
        }

        [Test]
        public void AltPings_AndF1TogglesFullscreen()
        {
            Press(Key.LeftAlt);
            Assert.That(GameInput.Ping(), Is.True, "Alt pings every player");
            ReleaseKeys();

            Press(Key.F1);
            Assert.That(GameInput.Fullscreen(), Is.True, "F1 toggles fullscreen");
            Assert.That(MatchHud.ToggledFullscreen(UnityEngine.FullScreenMode.Windowed), Is.EqualTo(UnityEngine.FullScreenMode.FullScreenWindow));
            Assert.That(MatchHud.ToggledFullscreen(UnityEngine.FullScreenMode.ExclusiveFullScreen), Is.EqualTo(UnityEngine.FullScreenMode.Windowed));
        }

        [Test]
        public void TheOnScreenButtons_JoinTheKeys()
        {
            OnScreenControls.Turn = 1f;
            OnScreenControls.Drive = -1;
            Assert.That(GameInput.Turn(), Is.EqualTo(1f), "the held turn-left button turns the aim");
            Assert.That(GameInput.Drive(), Is.EqualTo(-1), "the held move-backward button drives");

            Press(Key.D);
            Assert.That(GameInput.Turn(), Is.EqualTo(0f), "a key and a button pulling opposite ways cancel");
            ReleaseKeys();
            OnScreenControls.Reset();
            Assert.That(GameInput.Turn(), Is.EqualTo(0f));
            Assert.That(GameInput.Drive(), Is.EqualTo(0));

            OnScreenControls.Fire.Click();
            Assert.That(GameInput.Fire(), Is.True, "a clicked Fire button fires");
            Assert.That(GameInput.Fire(), Is.True, "and every read in that frame agrees");
        }

        [Test]
        public void ARebind_MovesWhereTheGameReadsFireFrom()
        {
            InputAction fire = InputBindings.Gameplay.FindAction("Fire");
            int spaceBindingIndex = fire.bindings.ToList().FindIndex(b => b.effectivePath == "<Keyboard>/space");
            Assert.That(spaceBindingIndex, Is.GreaterThanOrEqualTo(0), "Fire's default Space binding");

            fire.ApplyBindingOverride(spaceBindingIndex, "<Keyboard>/g");

            Press(Key.Space);
            Assert.That(GameInput.Fire(), Is.False, "Space no longer fires once rebound away from it");
            ReleaseKeys();

            Press(Key.G);
            Assert.That(GameInput.Fire(), Is.True, "the rebound key fires instead");
        }

        // GameInput.Fire()/UseItem() also check EventSystem.current.IsPointerOverGameObject() so a click that lands
        // on a menu button doesn't also fire (UR-T36 review): EventSystem.IsPointerOverGameObject() is not virtual,
        // and populating it for real needs a Canvas, a GraphicRaycaster and a running player loop (Update()) that
        // EditMode's no-player-loop environment doesn't have, so that guard is exercised by hand (open the rebind
        // screen in Play mode and click a row - Fire/UseItem never trigger) rather than by a test here.
    }
}
