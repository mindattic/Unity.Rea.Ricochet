using System.IO;
using Ricochet.Core.Data;
using UnityEngine;

namespace Ricochet
{
    /// <summary>
    /// Points Core at the game's data files (UR-T35): StreamingAssets/Settings, which ships with the build as plain files.
    /// It runs before the first scene loads, ahead of anything that reads a rule, map or line, and a bad file stops the
    /// game there with every problem listed (file, key and line).
    /// </summary>
    internal static class GameDataBoot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void UseStreamingAssets()
        {
            GameData.Use(new DirectorySettingsSource(Path.Combine(Application.streamingAssetsPath, "Settings")));
            try
            {
                GameData unused = GameData.Current;
            }
            catch (SettingsException ex)
            {
                Debug.LogError(ex.Message);
                throw;
            }
        }
    }
}
