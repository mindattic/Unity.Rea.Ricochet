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
            string json = _asset.SaveBindingOverridesAsJson();
            GameSettings().Set(ProfileKey, json);
            return Profiles.Current.TrySaveSettings();
        }

        /// <summary>Clears every rebind back to the asset's own bindings, and saves that.</summary>
        public static bool ResetToDefaults()
        {
            EnsureLoaded();
            foreach (InputActionMap map in _asset.actionMaps)
            {
                map.RemoveAllBindingOverrides();
            }

            return Save();
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
                UnityEngine.Object.DestroyImmediate(_asset);
            }

            _asset = null;
            _map = null;
        }
    }
}
