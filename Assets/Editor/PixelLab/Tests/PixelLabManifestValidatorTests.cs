using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace SchoolArcade.Editor.PixelLab.Tests
{
    internal sealed class PixelLabManifestValidatorTests
    {
        private string temporaryRoot;

        [SetUp]
        public void SetUp()
        {
            temporaryRoot = Path.Combine(Path.GetTempPath(), "PixelLabManifestTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, true);
            }
        }

        [Test]
        public void NestedIdsAndDeclaredFilesAreValidated()
        {
            Directory.CreateDirectory(Path.Combine(temporaryRoot, "walk"));
            File.WriteAllBytes(Path.Combine(temporaryRoot, "walk", "north.png"), new byte[] { 1, 2, 3 });
            File.WriteAllBytes(Path.Combine(temporaryRoot, "character.zip"), new byte[] { 4, 5, 6 });
            const string json = @"{
              ""asset"": { ""id"": ""asset-123"" },
              ""operation"": {
                ""id"": ""operation-456"",
                ""resources"": [
                  {
                    ""id"": ""resource-789"",
                    ""files"": [
                      { ""path"": ""walk/north.png"" },
                      { ""path"": ""character.zip"" }
                    ]
                  }
                ]
              }
            }";

            PixelLabManifestReport report = PixelLabManifestValidator.ValidateContents(
                "Assets/TestAsset/asset-manifest.json",
                temporaryRoot,
                json);

            Assert.IsTrue(report.IsValid, string.Join("\n", report.Issues.Select(issue => issue.Message)));
            CollectionAssert.AreEqual(new[] { "asset-123" }, report.AssetIds);
            CollectionAssert.AreEqual(new[] { "operation-456" }, report.OperationIds);
            CollectionAssert.AreEqual(new[] { "resource-789" }, report.ResourceIds);
            Assert.AreEqual(2, report.Files.Count);
            Assert.IsTrue(report.Files.All(file => file.Exists));
        }

        [Test]
        public void SnakeCaseIdsRemainCompatibleWithSchemaChanges()
        {
            File.WriteAllBytes(Path.Combine(temporaryRoot, "idle.png"), new byte[] { 1 });
            const string json = @"{
              ""pixel_lab_asset_id"": ""asset-a"",
              ""operation_id"": ""operation-b"",
              ""resource_ids"": [""resource-c""],
              ""generated_files"": [""idle.png""]
            }";

            PixelLabManifestReport report = PixelLabManifestValidator.ValidateContents(
                "Assets/TestAsset/asset-manifest.json",
                temporaryRoot,
                json);

            Assert.IsTrue(report.IsValid, string.Join("\n", report.Issues.Select(issue => issue.Message)));
            CollectionAssert.Contains(report.AssetIds, "asset-a");
            CollectionAssert.Contains(report.OperationIds, "operation-b");
            CollectionAssert.Contains(report.ResourceIds, "resource-c");
        }

        [Test]
        public void TraversalInDeclaredFileIsAnError()
        {
            const string json = @"{
              ""assetId"": ""asset-a"",
              ""operationId"": ""operation-b"",
              ""resourceId"": ""resource-c"",
              ""files"": [""../outside.png""]
            }";

            PixelLabManifestReport report = PixelLabManifestValidator.ValidateContents(
                "Assets/TestAsset/asset-manifest.json",
                temporaryRoot,
                json);

            Assert.IsTrue(report.HasErrors);
            Assert.IsTrue(report.Issues.Any(issue => issue.Code == "file-path-traversal"));
        }

        [Test]
        public void MissingContractFieldsAreReportedClearly()
        {
            PixelLabManifestReport report = PixelLabManifestValidator.ValidateContents(
                "Assets/TestAsset/asset-manifest.json",
                temporaryRoot,
                "{ \"prompt\": \"hero\" }");

            Assert.IsTrue(report.HasErrors);
            Assert.IsTrue(report.Issues.Any(issue => issue.Code == "missing-asset-id"));
            Assert.IsTrue(report.Issues.Any(issue => issue.Code == "missing-operation-id"));
            Assert.IsTrue(report.Issues.Any(issue => issue.Code == "missing-resource-id"));
            Assert.IsTrue(report.Issues.Any(issue => issue.Code == "missing-file-list"));
        }

        [Test]
        public void RemoteOnlyFileListDoesNotSatisfyLocalArtifactContract()
        {
            const string json = @"{
              ""assetId"": ""asset-a"",
              ""operationId"": ""operation-b"",
              ""resourceId"": ""resource-c"",
              ""files"": [""https://example.invalid/hero.png""]
            }";

            PixelLabManifestReport report = PixelLabManifestValidator.ValidateContents(
                "Assets/TestAsset/asset-manifest.json",
                temporaryRoot,
                json);

            Assert.IsTrue(report.HasErrors);
            Assert.IsTrue(report.Issues.Any(issue => issue.Code == "remote-file-reference"));
            Assert.IsTrue(report.Issues.Any(issue => issue.Code == "missing-local-file-list"));
        }

        [Test]
        public void DuplicateJsonKeysAreRejectedInsteadOfSilentlyOverwritten()
        {
            PixelLabManifestReport report = PixelLabManifestValidator.ValidateContents(
                "Assets/TestAsset/asset-manifest.json",
                temporaryRoot,
                "{ \"assetId\": \"one\", \"assetId\": \"two\" }");

            Assert.IsTrue(report.HasErrors);
            Assert.IsTrue(report.Issues.Any(issue => issue.Code == "invalid-json"));
        }
    }
}
