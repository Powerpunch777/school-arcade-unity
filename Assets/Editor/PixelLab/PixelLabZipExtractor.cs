using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEditor;

namespace SchoolArcade.Editor.PixelLab
{
    internal sealed class PixelLabZipExtractionResult
    {
        internal PixelLabZipExtractionResult(string destinationAssetPath, IReadOnlyList<string> extractedAssetPaths)
        {
            DestinationAssetPath = destinationAssetPath;
            ExtractedAssetPaths = extractedAssetPaths;
        }

        internal string DestinationAssetPath { get; private set; }
        internal IReadOnlyList<string> ExtractedAssetPaths { get; private set; }
    }

    internal static class PixelLabZipExtractor
    {
        internal const int MaximumEntries = 4096;
        internal const long MaximumEntryBytes = 128L * 1024L * 1024L;
        internal const long MaximumTotalBytes = 512L * 1024L * 1024L;

        private static readonly HashSet<string> AllowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".json", ".txt", ".md"
        };

        internal static string GetDestinationAssetPath(string zipAssetPath)
        {
            string normalized = PixelLabPaths.NormalizeAssetPath(zipAssetPath);
            string parent = PixelLabPaths.NormalizeAssetPath(Path.GetDirectoryName(normalized));
            return parent + "/" + Path.GetFileNameWithoutExtension(normalized) + "_extracted";
        }

        internal static PixelLabZipExtractionResult ExtractProjectArchive(string zipAssetPath)
        {
            string normalized = PixelLabPaths.NormalizeAssetPath(zipAssetPath);
            if (!PixelLabPaths.IsGeneratedZip(normalized))
            {
                throw new InvalidOperationException(
                    "Select a .zip file inside '" + PixelLabPaths.CandidateRoot + "'.");
            }

            string assetRoot;
            if (!PixelLabPaths.TryGetAssetRoot(normalized, out assetRoot)
                || string.Equals(assetRoot, normalized, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The ZIP must be inside a PixelLab asset subfolder: " + PixelLabPaths.CandidateRoot + "/<asset>/.");
            }

            string zipFullPath = PixelLabPaths.AssetPathToFullPath(normalized);
            string allowedRootFullPath = PixelLabPaths.AssetPathToFullPath(assetRoot);
            if (!File.Exists(zipFullPath))
            {
                throw new FileNotFoundException("Selected PixelLab ZIP does not exist.", normalized);
            }

            if (!Directory.Exists(allowedRootFullPath))
            {
                throw new DirectoryNotFoundException("PixelLab asset folder does not exist: " + assetRoot);
            }

            string destinationAssetPath = GetDestinationAssetPath(normalized);
            string destinationFullPath = PixelLabPaths.AssetPathToFullPath(destinationAssetPath);
            IReadOnlyList<string> extractedFullPaths = ExtractArchive(
                zipFullPath,
                destinationFullPath,
                allowedRootFullPath);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var extractedAssetPaths = new List<string>();
            foreach (string fullPath in extractedFullPaths)
            {
                string extractedAssetPath;
                if (PixelLabPaths.TryFullPathToAssetPath(fullPath, out extractedAssetPath))
                {
                    extractedAssetPaths.Add(extractedAssetPath);
                }
            }

            return new PixelLabZipExtractionResult(destinationAssetPath, extractedAssetPaths);
        }

        internal static IReadOnlyList<string> ExtractArchive(
            string zipFullPath,
            string destinationFullPath,
            string allowedRootFullPath)
        {
            string archive = Path.GetFullPath(zipFullPath);
            string destination = Path.GetFullPath(destinationFullPath);
            string allowedRoot = Path.GetFullPath(allowedRootFullPath);
            if (!File.Exists(archive))
            {
                throw new FileNotFoundException("PixelLab ZIP does not exist.", archive);
            }

            if (!PixelLabPaths.IsFullPathInside(archive, allowedRoot, false))
            {
                throw new InvalidOperationException("ZIP is outside the allowed PixelLab asset folder.");
            }

            if (!PixelLabPaths.IsFullPathInside(destination, allowedRoot, false))
            {
                throw new InvalidOperationException("Extraction destination is outside the allowed PixelLab asset folder.");
            }

            if (Directory.Exists(destination) || File.Exists(destination))
            {
                throw new IOException(
                    "Extraction destination already exists. Review or remove it before extracting again: " + destination);
            }

            using (var archiveStream = new FileStream(archive, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var zip = new ZipArchive(archiveStream, ZipArchiveMode.Read, false))
            {
                List<ExtractionEntry> entries = Preflight(zip, destination);
                if (entries.Count == 0)
                {
                    throw new InvalidDataException("PixelLab ZIP contains no supported files.");
                }

                bool destinationCreated = false;
                var extracted = new List<string>();
                try
                {
                    Directory.CreateDirectory(destination);
                    destinationCreated = true;
                    foreach (ExtractionEntry entry in entries)
                    {
                        string parent = Path.GetDirectoryName(entry.TargetFullPath);
                        if (!string.IsNullOrEmpty(parent))
                        {
                            Directory.CreateDirectory(parent);
                        }

                        using (Stream input = entry.ArchiveEntry.Open())
                        using (var output = new FileStream(entry.TargetFullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        {
                            CopyEntry(input, output, entry.ArchiveEntry.Length, entry.ArchiveEntry.FullName);
                        }

                        extracted.Add(entry.TargetFullPath);
                    }

                    return extracted;
                }
                catch (Exception exception)
                {
                    if (destinationCreated && PixelLabPaths.IsFullPathInside(destination, allowedRoot, false))
                    {
                        try
                        {
                            Directory.Delete(destination, true);
                        }
                        catch (Exception cleanupException)
                        {
                            throw new IOException(
                                "ZIP extraction failed and the partial destination could not be removed. "
                                + "Extraction error: " + exception.Message
                                + " Cleanup error: " + cleanupException.Message,
                                exception);
                        }
                    }

                    throw new IOException("ZIP extraction failed: " + exception.Message, exception);
                }
            }
        }

        private static List<ExtractionEntry> Preflight(ZipArchive archive, string destinationFullPath)
        {
            if (archive.Entries.Count > MaximumEntries)
            {
                throw new InvalidDataException(
                    "ZIP contains " + archive.Entries.Count + " entries; the safety limit is " + MaximumEntries + ".");
            }

            var results = new List<ExtractionEntry>();
            var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long totalBytes = 0L;
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string entryName = entry.FullName ?? string.Empty;
                ValidateEntryName(entryName);
                if (IsLink(entry))
                {
                    throw new InvalidDataException("ZIP links are not allowed: " + entryName);
                }

                bool isDirectory = entryName.EndsWith("/", StringComparison.Ordinal)
                    || entryName.EndsWith("\\", StringComparison.Ordinal);
                if (isDirectory)
                {
                    continue;
                }

                string extension = Path.GetExtension(entryName);
                if (!AllowedExtensions.Contains(extension))
                {
                    throw new InvalidDataException(
                        "ZIP entry type is not allowed: " + entryName
                        + ". Allowed file types are PNG, JSON, TXT, and MD.");
                }

                if (entry.Length > MaximumEntryBytes)
                {
                    throw new InvalidDataException(
                        "ZIP entry exceeds the 128 MiB per-file limit: " + entryName);
                }

                checked
                {
                    totalBytes += entry.Length;
                }

                if (totalBytes > MaximumTotalBytes)
                {
                    throw new InvalidDataException("ZIP exceeds the 512 MiB total extraction limit.");
                }

                string normalizedEntry = entryName.Replace('/', Path.DirectorySeparatorChar)
                    .Replace('\\', Path.DirectorySeparatorChar);
                string target = Path.GetFullPath(Path.Combine(destinationFullPath, normalizedEntry));
                if (!PixelLabPaths.IsFullPathInside(target, destinationFullPath, false))
                {
                    throw new InvalidDataException("ZIP entry escapes the destination folder: " + entryName);
                }

                if (!targets.Add(target))
                {
                    throw new InvalidDataException("ZIP contains duplicate output paths: " + entryName);
                }

                results.Add(new ExtractionEntry(entry, target));
            }

            return results;
        }

        private static void ValidateEntryName(string entryName)
        {
            if (string.IsNullOrWhiteSpace(entryName))
            {
                throw new InvalidDataException("ZIP contains an entry with an empty name.");
            }

            if (entryName.IndexOf('\0') >= 0
                || entryName.StartsWith("/", StringComparison.Ordinal)
                || entryName.StartsWith("\\", StringComparison.Ordinal)
                || Path.IsPathRooted(entryName)
                || (entryName.Length >= 2 && char.IsLetter(entryName[0]) && entryName[1] == ':')
                || entryName.IndexOf(':') >= 0)
            {
                throw new InvalidDataException("ZIP entry must use a relative path: " + entryName);
            }

            string trimmed = entryName.TrimEnd('/', '\\');
            string[] segments = trimmed.Replace('\\', '/').Split('/');
            foreach (string segment in segments)
            {
                if (segment.Length == 0
                    || string.Equals(segment, ".", StringComparison.Ordinal)
                    || string.Equals(segment, "..", StringComparison.Ordinal))
                {
                    throw new InvalidDataException("ZIP entry contains an unsafe path segment: " + entryName);
                }
            }

            if (segments.Length > 64)
            {
                throw new InvalidDataException("ZIP entry path is nested more than 64 folders deep: " + entryName);
            }
        }

        private static bool IsLink(ZipArchiveEntry entry)
        {
            int unixFileType = (entry.ExternalAttributes >> 16) & 0xF000;
            bool unixLink = unixFileType == 0xA000;
            bool windowsReparsePoint = (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0;
            return unixLink || windowsReparsePoint;
        }

        private static void CopyEntry(Stream input, Stream output, long expectedLength, string entryName)
        {
            var buffer = new byte[81920];
            long copied = 0L;
            while (true)
            {
                int read = input.Read(buffer, 0, buffer.Length);
                if (read == 0)
                {
                    break;
                }

                copied += read;
                if (copied > MaximumEntryBytes || copied > expectedLength)
                {
                    throw new InvalidDataException("ZIP entry expanded beyond its declared size: " + entryName);
                }

                output.Write(buffer, 0, read);
            }

            if (copied != expectedLength)
            {
                throw new InvalidDataException("ZIP entry length does not match its directory record: " + entryName);
            }
        }

        private sealed class ExtractionEntry
        {
            internal ExtractionEntry(ZipArchiveEntry archiveEntry, string targetFullPath)
            {
                ArchiveEntry = archiveEntry;
                TargetFullPath = targetFullPath;
            }

            internal ZipArchiveEntry ArchiveEntry { get; private set; }
            internal string TargetFullPath { get; private set; }
        }
    }
}
