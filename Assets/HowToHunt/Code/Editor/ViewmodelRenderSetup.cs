using System.Collections.Generic;
using System.Reflection;
using HowToHunt.Hunting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HowToHunt.Editor
{
    /// <summary>
    /// Held models (guns, tools, carried prey) are drawn after ambient occlusion (owner, 2026-11-25: the half-resolution
    /// SSAO's noise crawled over the gun in hand like TV snow). Their own layer (ViewmodelController.ViewmodelLayerName)
    /// is left out of every URP renderer's opaque pass and drawn by a RenderObjects pass at the same event as SSAO's
    /// after-opaque blend, listed after it, so it runs right after. The world keeps its AO. Idempotent.
    /// </summary>
    public static class ViewmodelRenderSetup
    {
        private const string PassName = "Viewmodel After AO";

        [MenuItem("How to Hunt/Set Up Viewmodel Rendering")]
        public static void Ensure()
        {
            int layer = EnsureLayer(ViewmodelController.ViewmodelLayerName);
            if (layer < 0) { Debug.LogWarning("[HowToHunt] No free layer for the viewmodel."); return; }
            foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
                if (data == null) continue;
                data.opaqueLayerMask &= ~(1 << layer);
                RenderObjects pass = null;
                foreach (ScriptableRendererFeature f in data.rendererFeatures)
                    if (f is RenderObjects ro && ro.name == PassName) pass = ro;
                if (pass == null)
                {
                    pass = ScriptableObject.CreateInstance<RenderObjects>();
                    pass.name = PassName;
                    AssetDatabase.AddObjectToAsset(pass, data);
                    data.rendererFeatures.Add(pass); // after SSAO: same event, so it runs after the AO blend
                    if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(pass, out string _, out long localId))
                    {
                        FieldInfo map = typeof(ScriptableRendererData).GetField("m_RendererFeatureMap", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (map?.GetValue(data) is List<long> ids) ids.Add(localId);
                    }
                }
                pass.settings.Event = RenderPassEvent.BeforeRenderingTransparents;
                pass.settings.filterSettings.RenderQueueType = RenderQueueType.Opaque;
                pass.settings.filterSettings.LayerMask = 1 << layer;
                pass.SetActive(true);
                EditorUtility.SetDirty(pass);
                EditorUtility.SetDirty(data);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>The layer's index, creating it in the first free user slot if needed (-1 if none is free).</summary>
        private static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0) return existing;
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tags.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue)) continue;
                slot.stringValue = name;
                tags.ApplyModifiedProperties();
                return i;
            }
            return -1;
        }
    }
}
