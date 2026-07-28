using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NUnit.Framework;

namespace SchoolArcade.Editor.PixelLab.Tests
{
    internal sealed class PixelLabZipExtractorTests
    {
        private string temporaryRoot;
        private string assetRoot;

        [SetUp]
        public void SetUp()
        {
            temporaryRoot = Path.Combine(Path.GetTempPath(), "PixelLabZipTests-" + Guid.NewGuid().ToString("N"));
            assetRoot = Path.Combine(temporaryRoot, "candidate");
            Directory.CreateDirectory(assetRoot);
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
        public void SupportedNestedFilesExtractInsideFixedDestination()
        {
            string zipPath = Path.Combine(assetRoot, "character.zip");
            CreateZip(zipPath,
                new ArchiveFile("directions/north/walk.png", new byte[] { 1, 2, 3 }),
                new ArchiveFile("metadata/animation.json", new byte[] { 4, 5 }));
            string destination = Path.Combine(assetRoot, "character_extracted");

            var extracted = PixelLabZipExtractor.ExtractArchive(zipPath, destination, assetRoot);

            Assert.AreEqual(2, extracted.Count);
            Assert.IsTrue(File.Exists(Path.Combine(destination, "directions", "north", "walk.png")));
            Assert.IsTrue(File.Exists(Path.Combine(destination, "metadata", "animation.json")));
            Assert.IsTrue(extracted.All(path => PixelLabPaths.IsFullPathInside(path, destination, false)));
        }

        [Test]
        public void TraversalEntryIsRejectedBeforeAnyFileIsWritten()
        {
            string zipPath = Path.Combine(assetRoot, "character.zip");
            CreateZip(zipPath, new ArchiveFile("../escape.png", new byte[] { 1 }));
            string destination = Path.Combine(assetRoot, "character_extracted");

            Assert.Throws<InvalidDataException>(() =>
                PixelLabZipExtractor.ExtractArchive(zipPath, destination, assetRoot));
            Assert.IsFalse(Directory.Exists(destination));
            Assert.IsFalse(File.Exists(Path.Combine(assetRoot, "escape.png")));
        }

        [Test]
        public void ScriptEntryIsRejectedAsNonInertContent()
        {
            string zipPath = Path.Combine(assetRoot, "character.zip");
            CreateZip(zipPath, new ArchiveFile("Editor/Surprise.cs", new byte[] { 1 }));
            string destination = Path.Combine(assetRoot, "character_extracted");

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                PixelLabZipExtractor.ExtractArchive(zipPath, destination, assetRoot));
            StringAssert.Contains("not allowed", exception.Message);
            Assert.IsFalse(Directory.Exists(destination));
        }

        [Test]
        public void ExistingDestinationIsNeverOverwritten()
        {
            string zipPath = Path.Combine(assetRoot, "character.zip");
            CreateZip(zipPath, new ArchiveFile("walk.png", new byte[] { 1 }));
            string destination = Path.Combine(assetRoot, "character_extracted");
            Directory.CreateDirectory(destination);
            string sentinel = Path.Combine(destination, "keep.txt");
            File.WriteAllText(sentinel, "keep");

            Assert.Throws<IOException>(() =>
                PixelLabZipExtractor.ExtractArchive(zipPath, destination, assetRoot));
            Assert.AreEqual("keep", File.ReadAllText(sentinel));
            Assert.IsFalse(File.Exists(Path.Combine(destination, "walk.png")));
        }

        [Test]
        public void DestinationOutsideAssetRootIsRejected()
        {
            string zipPath = Path.Combine(assetRoot, "character.zip");
            CreateZip(zipPath, new ArchiveFile("walk.png", new byte[] { 1 }));
            string destination = Path.Combine(temporaryRoot, "outside");

            Assert.Throws<InvalidOperationException>(() =>
                PixelLabZipExtractor.ExtractArchive(zipPath, destination, assetRoot));
            Assert.IsFalse(Directory.Exists(destination));
        }

        private static void CreateZip(string zipPath, params ArchiveFile[] files)
        {
            using (var stream = new FileStream(zipPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, false))
            {
                foreach (ArchiveFile file in files)
                {
                    ZipArchiveEntry entry = archive.CreateEntry(file.Path, CompressionLevel.Optimal);
                    using (Stream output = entry.Open())
                    {
                        output.Write(file.Content, 0, file.Content.Length);
                    }
                }
            }
        }

        private sealed class ArchiveFile
        {
            internal ArchiveFile(string path, byte[] content)
            {
                Path = path;
                Content = content;
            }

            internal string Path { get; private set; }
            internal byte[] Content { get; private set; }
        }
    }
}
