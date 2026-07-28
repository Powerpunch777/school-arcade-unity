using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SchoolArcade.Editor.PixelLab.Tests
{
    internal sealed class PixelLabImportPolicyTests
    {
        private const string TemporaryAssetFolder = "Assets/Editor/PixelLab/Tests/__PixelLabImportTemp";

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TemporaryAssetFolder))
            {
                AssetDatabase.DeleteAsset(TemporaryAssetFolder);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        [Test]
        public void GeneratedAndExtractedPngsUseCandidateRuleOnly()
        {
            Assert.IsTrue(PixelLabPaths.IsGeneratedPng(
                "Assets/Art/Generated/PixelLab/hero/character_extracted/walk/north.png"));
            Assert.IsFalse(PixelLabPaths.IsGeneratedPng(
                "Assets/Resources/Art/Player/hero/walk/north.png"));
            Assert.IsFalse(PixelLabPaths.IsGeneratedPng(
                "Assets/Art/Generated/PixelLab/hero/character.zip"));
        }

        [Test]
        public void FileNamePpuMarkerHasHighestPriority()
        {
            PixelLabPpuDecision decision = PixelLabImportPolicy.DeterminePixelsPerUnit(
                "Assets/Art/Generated/PixelLab/hero/walk-ppu_24.png");

            Assert.AreEqual(24f, decision.Value);
            Assert.AreEqual(PixelLabPpuSource.FileName, decision.Source);
        }

        [Test]
        public void FileSpecificManifestPpuBeatsGlobalPpu()
        {
            const string json = @"{
              ""pixelsPerUnit"": 16,
              ""resources"": [
                { ""file"": ""walk/north.png"", ""ppu"": 32 },
                { ""file"": ""walk/south.png"", ""ppu"": 48 }
              ]
            }";

            PixelLabPpuDecision decision = PixelLabImportPolicy.SelectFromManifestJson(json, "walk/north.png");

            Assert.AreEqual(32f, decision.Value);
            Assert.AreEqual(PixelLabPpuSource.ManifestFile, decision.Source);
        }

        [Test]
        public void ConflictingManifestPpuFallsBackConservatively()
        {
            const string json = @"{
              ""groups"": [
                { ""ppu"": 24 },
                { ""ppu"": 32 }
              ]
            }";

            PixelLabPpuDecision decision = PixelLabImportPolicy.SelectFromManifestJson(json, "walk.png");

            Assert.AreEqual(PixelLabImportPolicy.DefaultPixelsPerUnit, decision.Value);
            StringAssert.Contains("conflicting", decision.Warning.ToLowerInvariant());
        }

        [Test]
        public void PixelArtSettingsAreAppliedWithoutDestroyingMultipleSpriteMode()
        {
            Directory.CreateDirectory(PixelLabPaths.AssetPathToFullPath(TemporaryAssetFolder));
            string textureAssetPath = TemporaryAssetFolder + "/sheet.png";
            string textureFullPath = PixelLabPaths.AssetPathToFullPath(textureAssetPath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels(new[] { Color.clear, Color.white, Color.red, Color.green });
                texture.Apply();
                File.WriteAllBytes(textureFullPath, texture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(textureAssetPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(textureAssetPath) as TextureImporter;
            Assert.IsNotNull(importer);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
            standalone.overridden = true;
            standalone.format = TextureImporterFormat.Automatic;
            standalone.textureCompression = TextureImporterCompression.Compressed;
            standalone.crunchedCompression = true;
            importer.SetPlatformTextureSettings(standalone);

            PixelLabTexturePostprocessor.ApplyPixelArtSettings(importer, 32f);
            importer.SaveAndReimport();
            importer = AssetImporter.GetAtPath(textureAssetPath) as TextureImporter;

            Assert.IsNotNull(importer);
            Assert.AreEqual(TextureImporterType.Sprite, importer.textureType);
            Assert.AreEqual(SpriteImportMode.Multiple, importer.spriteImportMode);
            Assert.AreEqual(32f, importer.spritePixelsPerUnit);
            Assert.AreEqual(FilterMode.Point, importer.filterMode);
            Assert.IsFalse(importer.mipmapEnabled);
            Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression);
            Assert.IsFalse(importer.crunchedCompression);
            Assert.AreEqual(TextureImporterAlphaSource.FromInput, importer.alphaSource);
            Assert.IsTrue(importer.alphaIsTransparency);
            Assert.AreEqual(TextureImporterNPOTScale.None, importer.npotScale);
            Assert.AreEqual(TextureWrapMode.Clamp, importer.wrapMode);
            standalone = importer.GetPlatformTextureSettings("Standalone");
            Assert.AreEqual(TextureImporterFormat.Automatic, standalone.format);
            Assert.AreEqual(TextureImporterCompression.Uncompressed, standalone.textureCompression);
            Assert.IsFalse(standalone.crunchedCompression);
        }
    }
}
