// A bare development build of the standalone Windows player (U.RICO-T71): no icon, product-settings polish or release
// checklist, just enough for Tools/play.ps1 to launch a playable Ricochet -- muted by default, so a CLI or agent starting
// the game never plays sound. Tools/run.ps1 runs this in batch mode:
// Unity -batchmode -quit -projectPath . -executeMethod Ricochet.Tooling.RicochetBuild.BuildWindows
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Ricochet.Tooling
{
    public static class RicochetBuild
    {
        public const string WindowsPath = "Build/Ricochet/Ricochet.exe";

        [MenuItem("Ricochet/Build Windows Player (dev)")]
        public static void BuildWindows()
        {
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(WindowsPath));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = WindowsPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });

            BuildSummary summary = report.summary;
            Debug.Log("[Ricochet] build: " + summary.result + ", " + summary.totalErrors + " errors, " + summary.totalSize + " bytes -> " + WindowsPath);
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
            }
        }
    }
}
