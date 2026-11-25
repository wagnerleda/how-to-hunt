using UnityEditor;
using UnityEngine;

namespace HowToHunt.Editor
{
    /// <summary>
    /// Import rules for the owner's weapon drop-ins (Assets/HowToHunt/Art/Weapons/&lt;gun&gt;/) and the imported textures
    /// under Art/Models: models in metres with no cameras, lights or animation; textures capped at 1024 px (they come
    /// in at 2-4K, far more than a first-person gun needs) and normal maps detected by name. The original files stay
    /// untouched, so this can be raised later without re-downloading anything.
    /// </summary>
    public sealed class WeaponImportConventions : AssetPostprocessor
    {
        private const string WeaponsRoot = "Assets/HowToHunt/Art/Weapons/";
        private const string ModelsRoot = "Assets/HowToHunt/Art/Models/";
        private const int MaxTexture = 1024;

        public override uint GetVersion() => 1;

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(WeaponsRoot, System.StringComparison.Ordinal)) return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.isReadable = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }

        /// <summary>Weapon sounds load decompressed: short, played often, and readable when the content build trims them.</summary>
        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(SoundLibraryBuilder.WeaponsRoot, System.StringComparison.Ordinal)) return;
            var importer = (AudioImporter)assetImporter;
            AudioImporterSampleSettings s = importer.defaultSampleSettings;
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.7f;
            s.preloadAudioData = true;
            importer.defaultSampleSettings = s;
        }

        private void OnPreprocessTexture()
        {
            bool weapons = assetPath.StartsWith(WeaponsRoot, System.StringComparison.Ordinal);
            bool models = assetPath.StartsWith(ModelsRoot, System.StringComparison.Ordinal);
            if (!weapons && !models) return;
            var importer = (TextureImporter)assetImporter;
            importer.maxTextureSize = Mathf.Min(importer.maxTextureSize, MaxTexture);
            string name = System.IO.Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
            if (name.Contains("normal") || name.EndsWith("_nor") || name.Contains("_nrm")) importer.textureType = TextureImporterType.NormalMap;
            bool data = name.Contains("metal") || name.Contains("rough") || name.Contains("_ao") || name.Contains("orm") || name.Contains("height") || name.Contains("aoroumet");
            if (data && importer.textureType != TextureImporterType.NormalMap) importer.sRGBTexture = false;
        }
    }
}
