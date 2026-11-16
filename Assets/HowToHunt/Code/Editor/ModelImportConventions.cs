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

        private const string KenneyNatureRoot = "Assets/HowToHunt/Art/ThirdParty/KenneyNatureKit/";

        /// <summary>
        /// Kenney Nature Kit colours come in washed out (pastel teal leaves, pink wood). Map its material names onto the
        /// art-direction A palette: warm greens, honey wood, soft grey stone.
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<string, string> KenneyColors = new System.Collections.Generic.Dictionary<string, string>
        {
            { "leafsGreen", "6FAE4C" }, { "leafsDark", "4F8C3F" }, { "leafsFall", "E39A3B" }, { "grass", "7EBC55" },
            { "wood", "A0703F" }, { "woodDark", "7C5232" }, { "woodBark", "8C5E3B" }, { "woodBarkDark", "6B442A" }, { "woodInner", "E4C28F" },
            { "stone", "AEB3B4" }, { "stoneDark", "8A9093" }, { "dirt", "A97B51" },
            { "colorPurple", "9D7CD8" }, { "colorRed", "E2574F" }, { "colorRedDark", "B9433D" }, { "colorYellow", "F3C94B" }, { "colorTan", "DDB68A" },
        };

        // After URP's FBX material description preprocessor (order 2), which would overwrite these colours.
        public override int GetPostprocessOrder() => 10;
        public override uint GetVersion() => 2;

        private void OnPreprocessMaterialDescription(UnityEditor.AssetImporters.MaterialDescription description, Material material, AnimationClip[] clips)
        {
            if (!assetPath.StartsWith(KenneyNatureRoot, System.StringComparison.Ordinal)) return;
            string name = string.IsNullOrEmpty(description.materialName) ? material.name : description.materialName;
            if (KenneyColors.TryGetValue(name, out string hex) && ColorUtility.TryParseHtmlString("#" + hex, out Color c))
            {
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", c);
                material.color = c;
            }
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.08f);
            material.enableInstancing = true;
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
