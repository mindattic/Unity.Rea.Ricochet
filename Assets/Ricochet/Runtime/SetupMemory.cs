// The remembered setup (UR-T41): the original wrote datafiles/config.ini on every change (write_config, write_settings)
// and read it back on start (read_config, recall_settings); its Default button (obj_btn_default) put backup.ini's values
// back. Here the lobby's setup (Core's MatchSetup: the players room's AI, skill and aim assists, the map room's map and
// goal, and the settings room's announcer) is kept in the active Unity.Shared profile's settings.json, in the "game"
// block beside InputBindings' inputBindingOverrides, as a "matchSetup" object of named whole numbers (MatchSetup.ToRecord).
// Only the game block is written, so a session mute (Profiles.SessionMuted) never touches the stored audio settings.
using System;
using System.Collections.Generic;
using Ricochet.Core.Match;
using Unity.Shared;
using Unity.Shared.Core;
using Unity.Shared.Core.Json;
using UnityEngine;

namespace Ricochet
{
    internal static class SetupMemory
    {
        /// <summary>The game block's key the setup is stored under.</summary>
        public const string ProfileKey = "matchSetup";

        /// <summary>
        /// Never reads or writes the real profile store while set (tests), the same switch as
        /// <see cref="InputBindings.SkipProfileForTests"/>.
        /// </summary>
        internal static bool SkipProfileForTests;

        /// <summary>
        /// Fills <paramref name="setup"/> from the active profile (the defaults where nothing is saved). False when nothing
        /// could be read; <paramref name="setup"/> is then left as it was.
        /// </summary>
        public static bool Load(MatchSetup setup)
        {
            if (setup == null)
            {
                throw new ArgumentNullException(nameof(setup));
            }

            if (SkipProfileForTests)
            {
                return false;
            }

            try
            {
                setup.CopyFrom(Read(GameSettings()));
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[SetupMemory] Could not read the saved setup (" + ex.Message + "); using the defaults.");
                return false;
            }
        }

        /// <summary>Writes <paramref name="setup"/> into the active profile. False when the profile could not be written.</summary>
        public static bool Save(MatchSetup setup)
        {
            if (setup == null)
            {
                throw new ArgumentNullException(nameof(setup));
            }

            if (SkipProfileForTests)
            {
                return true;
            }

            try
            {
                Write(GameSettings(), setup);
                return Profiles.Current.TrySaveSettings();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[SetupMemory] Could not save the setup: " + ex.Message);
                return false;
            }
        }

        /// <summary>The Default button: every choice back to the game's defaults, saved.</summary>
        public static bool ResetToDefaults(MatchSetup setup)
        {
            if (setup == null)
            {
                throw new ArgumentNullException(nameof(setup));
            }

            setup.ResetToDefaults();
            return Save(setup);
        }

        /// <summary>The setup stored in <paramref name="game"/> (a profile's game block); the defaults where it holds none.</summary>
        internal static MatchSetup Read(JsonObject game)
        {
            JsonObject stored = game?.GetObject(ProfileKey);
            if (stored == null)
            {
                return new MatchSetup();
            }

            var record = new List<KeyValuePair<string, int>>();
            foreach (KeyValuePair<string, object> entry in stored)
            {
                if (entry.Value is JsonNumber) // a hand-edited 3.5 rounds; anything not a number keeps its default
                {
                    double value = Math.Round(stored.GetDouble(entry.Key), MidpointRounding.AwayFromZero);
                    record.Add(new KeyValuePair<string, int>(entry.Key, (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, value))));
                }
            }

            return MatchSetup.FromRecord(record);
        }

        /// <summary>Stores <paramref name="setup"/> in <paramref name="game"/> (a profile's game block), replacing what was there.</summary>
        internal static void Write(JsonObject game, MatchSetup setup)
        {
            var stored = new JsonObject();
            foreach (KeyValuePair<string, int> entry in setup.ToRecord())
            {
                stored.Set(entry.Key, JsonNumber.From(entry.Value));
            }

            game.Set(ProfileKey, stored);
        }

        // The profile's free-form "game" block, where every Ricochet-specific profile setting lives.
        private static JsonObject GameSettings()
        {
            ProfilesBootstrap.Initialize();
            return Profiles.Current.Settings.Game;
        }
    }
}
