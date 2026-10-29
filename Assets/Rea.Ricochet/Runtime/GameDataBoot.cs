using System;
using System.IO;
using Ricochet.Core.Data;
using UnityEngine;

namespace Ricochet
{
    /// <summary>
    /// Points Core at the game's data files (UR-T35): StreamingAssets/Settings, which ships with the build as plain files.
    /// It runs before the first scene loads, ahead of anything that reads a rule, map or line. A bad file is logged with
    /// every problem (file, key and line) and the game stops there: the player quits, play mode ends in the editor.
    /// </summary>
    public static class GameDataBoot
    {
        /// <summary>The folder the game reads its data from: StreamingAssets/Settings.</summary>
        public static string Folder => Path.Combine(Application.streamingAssetsPath, "Settings");

        /// <summary>The folder the data was last loaded from at boot, or null if it has not loaded (or failed to).</summary>
        public static string LoadedFrom { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void UseStreamingAssets()
        {
            Boot(Folder, Stop);
        }

        /// <summary>
        /// Loads the data from <paramref name="folder"/> and makes it the game's; on a bad file logs every problem and
        /// calls <paramref name="stop"/>. Returns whether the data loaded.
        /// </summary>
        public static bool Boot(string folder, Action stop)
        {
            LoadedFrom = null;
            GameData.Use(new DirectorySettingsSource(folder));
            try
            {
                GameData unused = GameData.Current;
                LoadedFrom = folder;
                return true;
            }
            catch (SettingsException ex)
            {
                Debug.LogError(ex.Message);
                stop?.Invoke();
                return false;
            }
        }

        private static void Stop()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(1);
#endif
        }
    }
}
