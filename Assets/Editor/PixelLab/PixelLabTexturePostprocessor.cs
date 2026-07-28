using UnityEditor;
using UnityEngine;

namespace SchoolArcade.Editor.PixelLab
{
    internal sealed class PixelLabTexturePostprocessor : AssetPostprocessor
    {
        private static readonly string[] CommonPlatformNames =
        {
            "Standalone", "Android", "iPhone", "WebGL", "Windows Store Apps", "Server"
        };

        private void OnPreprocessTexture()
        {
            if (!PixelLabPaths.IsGeneratedPng(assetPath))
            {
                return;
            }

            var textureImporter = assetImporter as TextureImporter;
            if (textureImporter == null)
            {
                Debug.LogError("PixelLab could not configure PNG importer: " + assetPath, assetImporter);
                return;
            }

            PixelLabPpuDecision ppu = PixelLabImportPolicy.DeterminePixelsPerUnit(assetPath);
            ApplyPixelArtSettings(textureImporter, ppu.Value);
            if (!string.IsNullOrEmpty(ppu.Warning))
            {
                Debug.LogWarning("PixelLab import warning for '" + assetPath + "': " + ppu.Warning, assetImporter);
            }
        }

        internal static void ApplyPixelArtSettings(TextureImporter importer, float pixelsPerUnit)
        {
            bool wasSprite = importer.textureType == TextureImporterType.Sprite;
            importer.textureType = TextureImporterType.Sprite;
            if (!wasSprite)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
            }

            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.compressionQuality = 100;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.sRGBTexture = true;

            foreach (string platformName in CommonPlatformNames)
            {
                TextureImporterPlatformSettings platform = importer.GetPlatformTextureSettings(platformName);
                if (!platform.overridden)
                {
                    continue;
                }

                platform.format = TextureImporterFormat.Automatic;
                platform.textureCompression = TextureImporterCompression.Uncompressed;
                platform.crunchedCompression = false;
                platform.compressionQuality = 100;
                importer.SetPlatformTextureSettings(platform);
            }
        }
    }
}
