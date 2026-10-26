// Graphics and every Quality level must select a URP asset (U.RICO-T37), so the built-in pipeline never renders by accident.
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ricochet.EditorTests
{
    public class RenderSettingsTests
    {
        [Test]
        public void GraphicsAndEveryQualityLevel_SelectAUrpAsset()
        {
            Assert.That(GraphicsSettings.defaultRenderPipeline, Is.InstanceOf<UniversalRenderPipelineAsset>(), "Graphics");
            for (int level = 0; level < QualitySettings.names.Length; level++)
            {
                Assert.That(QualitySettings.GetRenderPipelineAssetAt(level), Is.InstanceOf<UniversalRenderPipelineAsset>(),
                    QualitySettings.names[level]);
            }
        }

        [Test]
        public void EveryTiersRenderer_Is2D_WithPostProcessData()
        {
            for (int level = 0; level < QualitySettings.names.Length; level++)
            {
                var asset = (UniversalRenderPipelineAsset)QualitySettings.GetRenderPipelineAssetAt(level);
                string path = UnityEditor.AssetDatabase.GetAssetPath(asset);
                var renderer = UnityEditor.AssetDatabase.LoadAssetAtPath<Renderer2DData>(path.Replace(".asset", "_Renderer2D.asset"));
                Assert.That(renderer, Is.Not.Null, QualitySettings.names[level]);
                Assert.That(new UnityEditor.SerializedObject(renderer).FindProperty("m_PostProcessData").objectReferenceValue, Is.Not.Null, QualitySettings.names[level]);
            }
        }

        [Test]
        public void AddingAGlow_LeavesAGlobalLight()
        {
            var parent = new GameObject("p");
            try
            {
                RenderStyle.Current.AddGlow(parent.transform, Color.cyan, 2f);
                Assert.That(Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None),
                    Has.Some.Matches<Light2D>(l => l.lightType == Light2D.LightType.Global));
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void TheRenderStyleIsUrp_AndAddsAGlow()
        {
            IRenderStyle style = RenderStyle.Current;
            Assert.That(style.Name, Is.EqualTo("URP 2D"));
            var parent = new GameObject("p");
            try
            {
                Assert.That(style.AddGlow(parent.transform, Color.cyan, 2f), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }
    }
}
