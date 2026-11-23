using System.Collections.Generic;
using HowToHunt.Foundation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HowToHunt.Core
{
    /// <summary>
    /// Graphics options that switch URP renderer features. r_ssao turns ambient occlusion (soft contact shadows) on or
    /// off; it runs after the opaques from a depth copy at half resolution, so it never makes the scene draw twice.
    /// Renderer features live in assets: the editor puts them back as they were when play mode ends.
    /// </summary>
    public static class RenderQuality
    {
        public static readonly BoolCvar Ssao = new BoolCvar("r_ssao", true, "Ambient occlusion (soft contact shadows). Off is faster.", CvarFlags.Archive);

        private const string SsaoFeature = "ScreenSpaceAmbientOcclusion"; // URP's type is internal: matched by name
        private static readonly Dictionary<ScriptableRendererFeature, bool> Original = new Dictionary<ScriptableRendererFeature, bool>();

        public static void Register(ConsoleSystem console)
        {
            console.Register(Ssao);
            Ssao.Changed += _ => Apply();
            Application.quitting += Restore;
            Apply();
        }

        private static void Apply()
        {
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)) return;
            foreach (ScriptableRendererData data in urp.rendererDataList)
            {
                if (data == null) continue;
                foreach (ScriptableRendererFeature feature in data.rendererFeatures)
                {
                    if (feature == null || feature.GetType().Name != SsaoFeature) continue;
                    if (!Original.ContainsKey(feature)) Original[feature] = feature.isActive;
                    if (feature.isActive != Ssao.Value) feature.SetActive(Ssao.Value);
                }
            }
        }

        private static void Restore()
        {
            foreach (KeyValuePair<ScriptableRendererFeature, bool> kv in Original)
                if (kv.Key != null && kv.Key.isActive != kv.Value) kv.Key.SetActive(kv.Value);
            Original.Clear();
        }
    }
}
