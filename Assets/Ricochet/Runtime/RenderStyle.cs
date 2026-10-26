// The one seam for pipeline-specific drawing (U.RICO-T37): the game renders through URP's 2D Renderer, but the
// post effects (bloom for glows) and 2D lights (the original's draw_light) live behind IRenderStyle so no other
// code touches a pipeline type, and the built-in pipeline still works (a no-op style) if no URP asset is assigned.
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ricochet
{
    public interface IRenderStyle
    {
        string Name { get; }

        /// <summary>Sets <paramref name="camera"/> up for this pipeline (post processing, bloom).</summary>
        void ConfigureCamera(Camera camera);

        /// <summary>A soft light of <paramref name="radius"/> world units under <paramref name="parent"/>, or null when the pipeline has none.</summary>
        GameObject AddGlow(Transform parent, Color color, float radius);
    }

    public static class RenderStyle
    {
        private static readonly IRenderStyle s_builtIn = new BuiltInRenderStyle();
        private static readonly IRenderStyle s_urp = new UrpRenderStyle();

        /// <summary>The style matching the pipeline Graphics/Quality settings currently select.</summary>
        public static IRenderStyle Current
        {
            get { return GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset ? s_urp : s_builtIn; }
        }

        private sealed class BuiltInRenderStyle : IRenderStyle
        {
            public string Name { get { return "Built-in"; } }
            public void ConfigureCamera(Camera camera) { }
            public GameObject AddGlow(Transform parent, Color color, float radius) { return null; }
        }

        private sealed class UrpRenderStyle : IRenderStyle
        {
            private const float BloomIntensity = 0.6f;
            private GameObject _volume;

            public string Name { get { return "URP 2D"; } }

            public void ConfigureCamera(Camera camera)
            {
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                if (_volume != null)
                {
                    return;
                }

                _volume = new GameObject("RenderStyle Volume");
                Object.DontDestroyOnLoad(_volume);
                var volume = _volume.AddComponent<Volume>();
                volume.isGlobal = true;
                VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
                Bloom bloom = profile.Add<Bloom>(true);
                bloom.intensity.Override(BloomIntensity);
                bloom.threshold.Override(0.9f);
                volume.sharedProfile = profile;
            }

            public GameObject AddGlow(Transform parent, Color color, float radius)
            {
                var go = new GameObject("Glow");
                go.transform.SetParent(parent, false);
                var light = go.AddComponent<Light2D>();
                light.lightType = Light2D.LightType.Point;
                light.color = color;
                light.pointLightOuterRadius = radius;
                light.intensity = 0.8f;
                return go;
            }
        }
    }
}
