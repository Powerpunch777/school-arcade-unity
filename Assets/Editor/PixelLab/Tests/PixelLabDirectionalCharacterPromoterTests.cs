using System;
using System.IO;
using System.IO.Compression;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SchoolArcade.Editor.PixelLab.Tests
{
    internal sealed class PixelLabDirectionalCharacterPromoterTests
    {
        private const string CandidateFolder = "Assets/Art/Generated/PixelLab/__promotion-test";
        private const string DestinationFolder = "Assets/Resources/Art/__PromotionTest";

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(CandidateFolder);
            AssetDatabase.DeleteAsset(DestinationFolder);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        [Test]
        public void PromotesEightCenteredDirectionsAndRecordsApproval()
        {
            Directory.CreateDirectory(PixelLabPaths.AssetPathToFullPath(CandidateFolder));
            var fileEntries = new string[PixelLabDirectionalCharacterPromoter.DirectionOrder.Length];
            for (int index = 0; index < PixelLabDirectionalCharacterPromoter.DirectionOrder.Length; index++)
            {
                string direction = PixelLabDirectionalCharacterPromoter.DirectionOrder[index];
                string assetPath = CandidateFolder + "/candidate-" + direction + ".png";
                fileEntries[index] = "\"" + assetPath + "\"";
                WriteFixture(assetPath, index);
            }

            string manifestPath = CandidateFolder + "/asset-manifest.json";
            File.WriteAllText(PixelLabPaths.AssetPathToFullPath(manifestPath),
                "{\n"
                + "  \"provider\": \"pixel_lab\",\n"
                + "  \"operation\": \"create_character_v3\",\n"
                + "  \"resources\": { \"characterId\": \"character-approved\" },\n"
                + "  \"request\": { \"input\": { \"imageSize\": { \"width\": 64, \"height\": 64 } } },\n"
                + "  \"files\": [" + string.Join(",", fileEntries) + "]\n"
                + "}\n");

            PixelLabCharacterPromotionResult result = PixelLabDirectionalCharacterPromoter.Promote(
                manifestPath,
                DestinationFolder + "/hero_idle",
                false);

            Assert.AreEqual("character-approved", result.CharacterId);
            Assert.AreEqual(8, result.DestinationAssetPaths.Length);
            for (int index = 0; index < result.DestinationAssetPaths.Length; index++)
            {
                Texture2D promoted = LoadPng(result.DestinationAssetPaths[index]);
                try
                {
                    Assert.AreEqual(64, promoted.width);
                    Assert.AreEqual(64, promoted.height);
                    Assert.Greater(promoted.GetPixel(2 + index, 3).a, 0.9f);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(promoted);
                }
            }
            string approval = File.ReadAllText(PixelLabPaths.AssetPathToFullPath(result.ApprovalAssetPath));
            StringAssert.Contains("character-approved", approval);
            StringAssert.Contains("candidate-south.png", approval);
        }

        [Test]
        public void PromotesExplicitZipWalkFramesWithLosslessGlobalEvenCrop()
        {
            Directory.CreateDirectory(PixelLabPaths.AssetPathToFullPath(CandidateFolder));
            string zipAssetPath = CandidateFolder + "/walk-bundle.zip";
            string zipFullPath = PixelLabPaths.AssetPathToFullPath(zipAssetPath);
            WriteWalkZip(zipFullPath);
            string manifestPath = CandidateFolder + "/asset-manifest.json";
            File.WriteAllText(PixelLabPaths.AssetPathToFullPath(manifestPath),
                "{\"provider\":\"pixel_lab\",\"operation\":\"animate_character\","
                + "\"resources\":{\"characterId\":\"walk-character\"},\"jobIds\":[\"j1\",\"j2\",\"j3\",\"j4\",\"j5\",\"j6\",\"j7\",\"j8\"],"
                + "\"request\":{\"input\":{\"frameCount\":6,\"directions\":[\"south\",\"south-east\",\"east\",\"north-east\",\"north\",\"north-west\",\"west\",\"south-west\"]}},"
                + "\"delivery\":{\"animation\":{\"storedFrameCount\":7,\"referenceFrameIndex\":0,\"frameCountSemantics\":\"generated_frames_excluding_reference\",\"playableFrameIndices\":[1,2,3,4,5,6]}}}");

            PixelLabCharacterPromotionResult result = PixelLabDirectionalCharacterPromoter.PromoteWalkAnimation(
                manifestPath, zipAssetPath, DestinationFolder + "/leon_walk", false);

            Assert.AreEqual(66, result.CellWidth);
            Assert.AreEqual(8, result.DestinationAssetPaths.Length);
            foreach (string assetPath in result.DestinationAssetPaths)
            {
                Texture2D promoted = LoadPng(assetPath);
                try { Assert.AreEqual(396, promoted.width); Assert.AreEqual(66, promoted.height); }
                finally { UnityEngine.Object.DestroyImmediate(promoted); }
                TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                Assert.IsNotNull(importer);
                Assert.AreEqual(66f, importer.spritePixelsPerUnit);
            }
            string approval = File.ReadAllText(PixelLabPaths.AssetPathToFullPath(result.ApprovalAssetPath));
            StringAssert.Contains("sourceZipSha256", approval);
            StringAssert.Contains("frame_006.png", approval);
            StringAssert.DoesNotContain("frame_000.png", approval);
            StringAssert.Contains("\"x\": 30", approval);
        }

        private static void WriteWalkZip(string zipFullPath)
        {
            using (ZipArchive archive = ZipFile.Open(zipFullPath, ZipArchiveMode.Create))
            {
                foreach (string direction in PixelLabDirectionalCharacterPromoter.DirectionOrder)
                for (int frame = 1; frame <= PixelLabDirectionalCharacterPromoter.WalkFrameCount; frame++)
                {
                    string entryName = "Leon_Style_Anchor_B/animations/walk/" + direction + "/frame_" + frame.ToString("D3") + ".png";
                    ZipArchiveEntry entry = archive.CreateEntry(entryName);
                    using (Stream stream = entry.Open())
                    {
                        Texture2D texture = new Texture2D(124, 124, TextureFormat.RGBA32, false, false);
                        try
                        {
                            Color32[] pixels = new Color32[124 * 124];
                            pixels[31 * 124 + 30] = new Color32(255, 0, 0, 255);
                            pixels[93 * 124 + 95] = new Color32(0, 255, 0, 255);
                            texture.SetPixels32(pixels);
                            texture.Apply(false, false);
                            byte[] png = texture.EncodeToPNG();
                            stream.Write(png, 0, png.Length);
                        }
                        finally { UnityEngine.Object.DestroyImmediate(texture); }
                    }
                }
            }
        }

        private static void WriteFixture(string assetPath, int index)
        {
            var texture = new Texture2D(124, 124, TextureFormat.RGBA32, false, false);
            try
            {
                var pixels = new Color32[124 * 124];
                pixels[(30 + 3) * 124 + (30 + 2 + index)] = new Color32((byte)(20 + index), 80, 160, 255);
                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                File.WriteAllBytes(PixelLabPaths.AssetPathToFullPath(assetPath), texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static Texture2D LoadPng(string assetPath)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            Assert.IsTrue(ImageConversion.LoadImage(texture, File.ReadAllBytes(PixelLabPaths.AssetPathToFullPath(assetPath)), false));
            return texture;
        }
    }
}
