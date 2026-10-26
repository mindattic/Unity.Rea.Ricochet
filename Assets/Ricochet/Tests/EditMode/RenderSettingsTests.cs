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
