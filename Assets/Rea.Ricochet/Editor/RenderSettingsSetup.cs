// Creates the URP pipeline assets and 2D Renderer for the Low/Medium/High quality levels under Assets/Rea.Ricochet/Settings
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
        public const string Folder = "Assets/Rea.Ricochet/Settings";

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

        // CreateInstance leaves m_PostProcessData empty (Create > 2D Renderer fills it), and without it the 2D renderer
        // builds no post-process passes, so bloom would never draw.
        private static void FillPostProcessData(Renderer2DData renderer)
        {
            if (renderer == null)
            {
                return;
            }

            var so = new SerializedObject(renderer);
            if (so.FindProperty("m_PostProcessData").objectReferenceValue != null)
            {
                return;
            }

            var data = AssetDatabase.LoadAssetAtPath<PostProcessData>(PostProcessDataPath);
            so.FindProperty("m_PostProcessData").objectReferenceValue = data;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(renderer);
        }

        private const string PostProcessDataPath = "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";

        private static UniversalRenderPipelineAsset LoadOrCreate(string tier, int index)
        {
            string path = AssetPath(tier);
            var existing = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            string rendererPath = Folder + "/Ricochet_" + tier + "_Renderer2D.asset";
            if (existing != null)
            {
                // Repair an older asset made without post-process data.
                FillPostProcessData(AssetDatabase.LoadAssetAtPath<Renderer2DData>(rendererPath));
                return existing;
            }

            var renderer = ScriptableObject.CreateInstance<Renderer2DData>();
            AssetDatabase.CreateAsset(renderer, rendererPath);
            FillPostProcessData(renderer);
            UniversalRenderPipelineAsset asset = UniversalRenderPipelineAsset.Create(renderer);
            asset.renderScale = 1f;
            asset.msaaSampleCount = index == 0 ? 1 : index == 1 ? 2 : 4;
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
