// The one InputActions asset the game reads through (UR-T36): Resources/Input/RicochetControls, loaded once as a
// private copy so a rebind never touches the asset on disk. Its Gameplay map is enabled for the lifetime of the
// process; GameInput reads actions off it every frame, and the rebind screen (Menus.BuildRebind) changes a binding
// through it. A rebind is saved into the active Unity.Shared profile's settings.json (the "game" block's
// inputBindingOverrides, the project's own data, same as GameAudio's volumes live in the shared "common" block) so it
// is kept from session to session and follows the profile, not the machine.
using System;
using Unity.Shared;
using Unity.Shared.Core;
using Unity.Shared.Core.Json;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ricochet
{
    internal static class InputBindings
    {
        /// <summary>Where the asset lives under a Resources folder (no scene or inspector reference needed).</summary>
        public const string ResourcePath = "Input/RicochetControls";

        /// <summary>The one action map the asset defines.</summary>
        public const string ActionMapName = "Gameplay";

        private const string ProfileKey = "inputBindingOverrides";

        private static InputActionAsset _asset;
        private static InputActionMap _map;

        /// <summary>
        /// Skips reading (and never writes) the real Unity.Shared profile store while set. EditMode tests set this
        /// before touching <see cref="Gameplay"/>: without it, they would load whatever the developer running them
        /// has actually saved on this machine (<c>Application.persistentDataPath</c>, per <c>ProfilesBootstrap</c>),
        /// which could leave every default-binding assertion depending on that developer's own rebinds.
        /// </summary>
        internal static bool SkipProfileForTests;

        /// <summary>The loaded asset (a private, instantiated copy), created and enabled on first use.</summary>
        public static InputActionAsset Asset
        {
            get
            {
                EnsureLoaded();
                return _asset;
            }
        }

        /// <summary>The Gameplay action map, enabled.</summary>
        public static InputActionMap Gameplay
        {
            get
            {
                EnsureLoaded();
                return _map;
            }
        }

        private static void EnsureLoaded()
        {
            if (_asset != null)
            {
                return;
            }

            InputActionAsset source = Resources.Load<InputActionAsset>(ResourcePath);
            if (source == null)
            {
                throw new InvalidOperationException("Resources/" + ResourcePath + ".inputactions is missing.");
            }

            _asset = UnityEngine.Object.Instantiate(source);
            _asset.name = source.name;
            _map = _asset.FindActionMap(ActionMapName, throwIfNotFound: true);
            ApplySavedOverrides();
            _map.Enable();
        }

        /// <summary>Re-reads the active profile's saved overrides (a fresh profile, or another MindAttic game) and applies them.</summary>
        public static void ApplySavedOverrides()
        {
            EnsureLoaded();
            if (SkipProfileForTests)
            {
                return;
            }

            // Always starts from the asset's own bindings, not whatever was on it a moment ago: a profile with no
            // saved overrides (fresh, or one switched to from a profile that had some) must mean "use the defaults",
            // not "keep whatever the previous profile left on this instance".
            RemoveAllOverrides();

            string json = ReadProfileJson();
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            try
            {
                _asset.LoadBindingOverridesFromJson(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[InputBindings] Could not read the saved bindings (" + ex.Message + "); using the defaults.");
            }
        }

        /// <summary>Saves every binding override now on the asset into the active profile. False when the profile could not be written.</summary>
        public static bool Save()
        {
            EnsureLoaded();
            if (SkipProfileForTests)
            {
                return true;
            }

            string json = _asset.SaveBindingOverridesAsJson();
            GameSettings().Set(ProfileKey, json);
            return Profiles.Current.TrySaveSettings();
        }

        /// <summary>Clears every rebind back to the asset's own bindings, and saves that.</summary>
        public static bool ResetToDefaults()
        {
            EnsureLoaded();
            RemoveAllOverrides();
            return Save();
        }

        private static void RemoveAllOverrides()
        {
            foreach (InputActionMap map in _asset.actionMaps)
            {
                map.RemoveAllBindingOverrides();
            }
        }

        private static string ReadProfileJson()
        {
            return GameSettings().GetString(ProfileKey);
        }

        // The profile's free-form "game" block (Unity.Shared.Core.ProfileSettings.Game), the same place every
        // Ricochet-specific profile setting lives.
        private static JsonObject GameSettings()
        {
            ProfilesBootstrap.Initialize();
            return Profiles.Current.Settings.Game;
        }

        /// <summary>Forgets the loaded asset, so the next use loads and applies the profile afresh (tests only).</summary>
        internal static void ResetForTests()
        {
            if (_asset != null)
            {
                _asset.Disable(); // otherwise its actions stay registered in the Input System's global action state
                UnityEngine.Object.DestroyImmediate(_asset);
            }

            _asset = null;
            _map = null;
        }
    }
}
