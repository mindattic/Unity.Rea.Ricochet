// Creates the URP pipeline assets and 2D Renderer for the Low/Medium/High quality levels under Assets/Ricochet/Settings
// and assigns them in Graphics and Quality settings (U.RICO-T37). Idempotent: reruns reuse the assets.
// Unity -batchmode -quit -projectPath . -executeMethod Ricochet.Tooling.RenderSettingsSetup.Apply
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ricochet.Tooling
{
    public static class RenderSettingsSetup
    {
        public const string Folder = "Assets/Ricochet/Settings";

        // Quality level name -> pipeline asset name. Very Low maps to Low's asset; Very High and Ultra to High's.
        private static readonly string[] Tiers = { "Low", "Medium", "High" };

        public static string AssetPath(string tier) { return Folder + "/Ricochet_" + tier + ".asset"; }

        [MenuItem("Ricochet/Set Up URP Render Settings")]
        public static void Apply()
        {
            Directory.CreateDirectory(Folder);
            UniversalRenderPipelineAsset[] assets = new UniversalRenderPipelineAsset[Tiers.Length];
            for (int i = 0; i < Tiers.Length; i++)
            {
                assets[i] = LoadOrCreate(Tiers[i], i);
            }

            GraphicsSettings.defaultRenderPipeline = assets[1];
            string[] names = QualitySettings.names;
            for (int level = 0; level < names.Length; level++)
            {
                int tier = names[level].Contains("Low") ? 0 : names[level] == "Medium" ? 1 : 2;
                QualitySettings.SetQualityLevel(level, false);
                QualitySettings.renderPipeline = assets[tier];
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Ricochet] URP assets assigned in Graphics and Quality settings.");
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }

        private static UniversalRenderPipelineAsset LoadOrCreate(string tier, int index)
        {
            string path = AssetPath(tier);
            var existing = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (existing != null)
            {
                return existing;
            }

            var renderer = ScriptableObject.CreateInstance<Renderer2DData>();
            AssetDatabase.CreateAsset(renderer, Folder + "/Ricochet_" + tier + "_Renderer2D.asset");
            UniversalRenderPipelineAsset asset = UniversalRenderPipelineAsset.Create(renderer);
            asset.renderScale = 1f;
            asset.msaaSampleCount = index == 0 ? 1 : index == 1 ? 2 : 4;
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
