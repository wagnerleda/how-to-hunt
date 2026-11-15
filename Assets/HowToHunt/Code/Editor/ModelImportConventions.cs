using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HowToHunt.Editor
{
    /// <summary>
    /// Import conventions for models under Assets/HowToHunt/Art/Models (see docs/ASSETS.md):
    /// meters, file scale, axis conversion baked, flat normals kept, generic rig, clips named after the Blender
    /// action (prefix "Rig|" removed); clips named Idle/Flap/Walk/Run/Loop* loop. Materials whose name contains
    /// "Wing", "Leaf" or "DoubleSided" render both faces.
    /// </summary>
    public sealed class ModelImportConventions : AssetPostprocessor
    {
        private const string ModelsRoot = "Assets/HowToHunt/Art/Models/";
        private static readonly string[] LoopingClips = { "Idle", "Flap", "Walk", "Run", "Fly", "Swim" };

        private bool InScope => assetPath.StartsWith(ModelsRoot, System.StringComparison.Ordinal);

        private void OnPreprocessModel()
        {
            if (!InScope) return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }

        private void OnPreprocessAnimation()
        {
            if (!InScope) return;
            var importer = (ModelImporter)assetImporter;
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                string source = string.IsNullOrEmpty(clip.takeName) ? clip.name : clip.takeName;
                int bar = source.LastIndexOf('|');
                clip.name = bar >= 0 ? source.Substring(bar + 1) : source;
                bool loop = LoopingClips.Contains(clip.name) || clip.name.StartsWith("Loop", System.StringComparison.Ordinal);
                clip.loopTime = loop;
                clip.loopPose = loop;
            }
            importer.clipAnimations = clips;
        }

        private void OnPostprocessMaterial(Material material)
        {
            if (!InScope) return;
            string n = material.name;
            if (n.Contains("Wing") || n.Contains("Leaf") || n.Contains("DoubleSided"))
            {
                if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
                material.doubleSidedGI = true;
            }
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.15f);
            material.enableInstancing = true;
        }
    }
}
