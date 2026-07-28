using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace SchoolArcade.Editor.PixelLab
{
    internal enum PixelLabIssueSeverity
    {
        Info,
        Warning,
        Error
    }

    internal sealed class PixelLabValidationIssue
    {
        internal PixelLabValidationIssue(PixelLabIssueSeverity severity, string code, string message)
        {
            Severity = severity;
            Code = code;
            Message = message;
        }

        internal PixelLabIssueSeverity Severity { get; private set; }
        internal string Code { get; private set; }
        internal string Message { get; private set; }
    }

    internal sealed class PixelLabManifestFile
    {
        internal PixelLabManifestFile(string declaredPath)
        {
            DeclaredPath = declaredPath;
        }

        internal string DeclaredPath { get; private set; }
        internal string AssetPath { get; set; }
        internal bool Exists { get; set; }
        internal bool IsRemote { get; set; }
    }

    internal sealed class PixelLabManifestReport
    {
        internal PixelLabManifestReport(string manifestAssetPath)
        {
            ManifestAssetPath = PixelLabPaths.NormalizeAssetPath(manifestAssetPath);
            AssetDirectoryPath = PixelLabPaths.NormalizeAssetPath(Path.GetDirectoryName(ManifestAssetPath));
        }

        internal string ManifestAssetPath { get; private set; }
        internal string AssetDirectoryPath { get; private set; }
        internal List<string> AssetIds { get; } = new List<string>();
        internal List<string> OperationIds { get; } = new List<string>();
        internal List<string> ResourceIds { get; } = new List<string>();
        internal List<PixelLabManifestFile> Files { get; } = new List<PixelLabManifestFile>();
        internal List<PixelLabValidationIssue> Issues { get; } = new List<PixelLabValidationIssue>();

        internal bool HasErrors
        {
            get { return Issues.Any(issue => issue.Severity == PixelLabIssueSeverity.Error); }
        }

        internal bool IsValid
        {
            get { return !HasErrors; }
        }

        internal void AddIssue(PixelLabIssueSeverity severity, string code, string message)
        {
            Issues.Add(new PixelLabValidationIssue(severity, code, message));
        }
    }

    internal static class PixelLabManifestValidator
    {
        internal const long MaxManifestBytes = 4L * 1024L * 1024L;

        private static readonly HashSet<string> FileLeafKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "file", "filename", "filepath", "path", "relativepath", "localpath",
            "outputfile", "artifactfile", "archive", "archivefile", "imagefile"
        };

        private static readonly HashSet<string> FileContainerKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "files", "filelist", "outputs", "outputfiles", "artifacts", "artifactfiles",
            "archives", "images", "generatedfiles"
        };

        internal static PixelLabManifestReport ValidateAssetManifest(string manifestAssetPath)
        {
            string normalized = PixelLabPaths.NormalizeAssetPath(manifestAssetPath);
            var earlyReport = new PixelLabManifestReport(normalized);
            if (!PixelLabPaths.IsUnderAssetPath(normalized, PixelLabPaths.CandidateRoot, false))
            {
                earlyReport.AddIssue(
                    PixelLabIssueSeverity.Error,
                    "manifest-outside-candidates",
                    "Manifest must be inside '" + PixelLabPaths.CandidateRoot + "'.");
                return earlyReport;
            }

            if (!string.Equals(Path.GetFileName(normalized), PixelLabPaths.ManifestFileName, StringComparison.OrdinalIgnoreCase))
            {
                earlyReport.AddIssue(
                    PixelLabIssueSeverity.Error,
                    "unexpected-manifest-name",
                    "Expected a file named '" + PixelLabPaths.ManifestFileName + "'.");
                return earlyReport;
            }

            string fullPath;
            try
            {
                fullPath = PixelLabPaths.AssetPathToFullPath(normalized);
            }
            catch (Exception exception)
            {
                earlyReport.AddIssue(PixelLabIssueSeverity.Error, "invalid-manifest-path", exception.Message);
                return earlyReport;
            }

            if (!File.Exists(fullPath))
            {
                earlyReport.AddIssue(
                    PixelLabIssueSeverity.Error,
                    "manifest-missing",
                    "Manifest file does not exist: " + normalized);
                return earlyReport;
            }

            var info = new FileInfo(fullPath);
            if (info.Length > MaxManifestBytes)
            {
                earlyReport.AddIssue(
                    PixelLabIssueSeverity.Error,
                    "manifest-too-large",
                    "Manifest is larger than the 4 MiB safety limit.");
                return earlyReport;
            }

            try
            {
                string json = File.ReadAllText(fullPath, new UTF8Encoding(false, true));
                return ValidateContents(normalized, Path.GetDirectoryName(fullPath), json);
            }
            catch (Exception exception)
            {
                earlyReport.AddIssue(
                    PixelLabIssueSeverity.Error,
                    "manifest-read-failed",
                    "Could not read manifest: " + exception.Message);
                return earlyReport;
            }
        }

        internal static PixelLabManifestReport ValidateContents(
            string manifestAssetPath,
            string assetDirectoryFullPath,
            string json)
        {
            var report = new PixelLabManifestReport(manifestAssetPath);
            PixelLabJsonValue root;
            string parseError;
            if (!PixelLabJson.TryParse(json, out root, out parseError))
            {
                report.AddIssue(PixelLabIssueSeverity.Error, "invalid-json", "Manifest JSON is invalid: " + parseError);
                return report;
            }

            if (root.Kind != PixelLabJsonKind.Object)
            {
                report.AddIssue(
                    PixelLabIssueSeverity.Error,
                    "invalid-root",
                    "Manifest root must be a JSON object, not " + root.Kind.ToString().ToLowerInvariant() + ".");
            }

            var raw = new RawManifestData();
            Walk(root, new List<string>(), "$", raw, report);
            report.AssetIds.AddRange(ValidateIds("asset", raw.AssetIds, report));
            report.OperationIds.AddRange(ValidateIds("operation", raw.OperationIds, report));
            report.ResourceIds.AddRange(ValidateIds("resource", raw.ResourceIds, report));
            ValidateFileReferences(assetDirectoryFullPath, raw.FileReferences, report);
            return report;
        }

        private static void Walk(
            PixelLabJsonValue value,
            List<string> context,
            string pointer,
            RawManifestData raw,
            PixelLabManifestReport report)
        {
            if (value == null)
            {
                return;
            }

            if (value.Kind == PixelLabJsonKind.Object)
            {
                foreach (KeyValuePair<string, PixelLabJsonValue> property in value.ObjectValue)
                {
                    string normalizedKey = PixelLabImportPolicy.NormalizeKey(property.Key);
                    string propertyPointer = pointer + "." + property.Key;
                    IdCategory category = ClassifyIdKey(normalizedKey, context);
                    if (category != IdCategory.None)
                    {
                        AddIds(property.Value, category, propertyPointer, raw, report);
                    }

                    if (PixelLabImportPolicy.IsPpuKey(property.Key))
                    {
                        float ignored;
                        if (!PixelLabImportPolicy.TryReadPpuValue(property.Value, out ignored))
                        {
                            report.AddIssue(
                                PixelLabIssueSeverity.Warning,
                                "invalid-ppu",
                                "PPU at " + propertyPointer + " must be a number from 1 through 4096; it will be ignored.");
                        }
                    }

                    if (FileLeafKeys.Contains(normalizedKey))
                    {
                        AddLeafFileReference(property.Value, propertyPointer, raw);
                    }
                    else if (FileContainerKeys.Contains(normalizedKey))
                    {
                        CollectContainerFileReferences(property.Value, propertyPointer, raw);
                    }

                    var childContext = new List<string>(context) { normalizedKey };
                    Walk(property.Value, childContext, propertyPointer, raw, report);
                }
            }
            else if (value.Kind == PixelLabJsonKind.Array)
            {
                for (int i = 0; i < value.ArrayValue.Count; i++)
                {
                    Walk(value.ArrayValue[i], context, pointer + "[" + i + "]", raw, report);
                }
            }
            else if (value.Kind == PixelLabJsonKind.String && LooksLikeWorkerArtifact(value.StringValue))
            {
                raw.AddFileReference(pointer, value.StringValue);
            }
        }

        private static IdCategory ClassifyIdKey(string normalizedKey, List<string> context)
        {
            if (normalizedKey != "id")
            {
                if (normalizedKey.EndsWith("resourceid", StringComparison.Ordinal)
                    || normalizedKey.EndsWith("resourceids", StringComparison.Ordinal))
                {
                    return IdCategory.Resource;
                }

                if (normalizedKey.EndsWith("operationid", StringComparison.Ordinal)
                    || normalizedKey.EndsWith("operationids", StringComparison.Ordinal))
                {
                    return IdCategory.Operation;
                }

                if (normalizedKey.EndsWith("assetid", StringComparison.Ordinal)
                    || normalizedKey.EndsWith("assetids", StringComparison.Ordinal))
                {
                    return IdCategory.Asset;
                }

                return IdCategory.None;
            }

            for (int i = context.Count - 1; i >= 0; i--)
            {
                string segment = context[i];
                if (segment.IndexOf("resource", StringComparison.Ordinal) >= 0) return IdCategory.Resource;
                if (segment.IndexOf("operation", StringComparison.Ordinal) >= 0) return IdCategory.Operation;
                if (segment.IndexOf("asset", StringComparison.Ordinal) >= 0) return IdCategory.Asset;
            }

            return IdCategory.None;
        }

        private static void AddIds(
            PixelLabJsonValue value,
            IdCategory category,
            string pointer,
            RawManifestData raw,
            PixelLabManifestReport report)
        {
            if (value.Kind == PixelLabJsonKind.Array)
            {
                for (int i = 0; i < value.ArrayValue.Count; i++)
                {
                    AddIds(value.ArrayValue[i], category, pointer + "[" + i + "]", raw, report);
                }

                return;
            }

            string scalar = value.ToScalarString();
            if (scalar == null || value.Kind == PixelLabJsonKind.Boolean)
            {
                report.AddIssue(
                    PixelLabIssueSeverity.Error,
                    "invalid-id-type",
                    category.ToString() + " ID at " + pointer + " must be a string or number.");
                return;
            }

            switch (category)
            {
                case IdCategory.Asset: raw.AssetIds.Add(scalar); break;
                case IdCategory.Operation: raw.OperationIds.Add(scalar); break;
                case IdCategory.Resource: raw.ResourceIds.Add(scalar); break;
            }
        }

        private static IReadOnlyList<string> ValidateIds(
            string label,
            List<string> values,
            PixelLabManifestReport report)
        {
            var valid = new List<string>();
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string original in values)
            {
                string value = (original ?? string.Empty).Trim();
                if (value.Length == 0)
                {
                    report.AddIssue(
                        PixelLabIssueSeverity.Error,
                        "empty-" + label + "-id",
                        "Manifest contains an empty " + label + " ID.");
                    continue;
                }

                if (value.Length > 512)
                {
                    report.AddIssue(
                        PixelLabIssueSeverity.Warning,
                        "long-" + label + "-id",
                        label + " ID is longer than 512 characters: " + Abbreviate(value));
                }

                if (value.Any(char.IsControl))
                {
                    report.AddIssue(
                        PixelLabIssueSeverity.Error,
                        "control-character-in-" + label + "-id",
                        label + " ID contains a control character.");
                    continue;
                }

                if (!string.Equals(value, original, StringComparison.Ordinal))
                {
                    report.AddIssue(
                        PixelLabIssueSeverity.Warning,
                        "trimmed-" + label + "-id",
                        label + " ID contains surrounding whitespace: '" + value + "'.");
                }

                int count;
                counts.TryGetValue(value, out count);
                counts[value] = count + 1;
                if (count == 0)
                {
                    valid.Add(value);
                }
            }

            if (valid.Count == 0)
            {
                report.AddIssue(
                    PixelLabIssueSeverity.Error,
                    "missing-" + label + "-id",
                    "No " + label + " ID was found. Supported forms include '" + label + "Id' and '" + label + ": { id: ... }'.");
            }

            foreach (KeyValuePair<string, int> pair in counts.Where(pair => pair.Value > 1))
            {
                report.AddIssue(
                    PixelLabIssueSeverity.Warning,
                    "duplicate-" + label + "-id",
                    label + " ID appears " + pair.Value + " times: " + pair.Key);
            }

            return valid;
        }

        private static void AddLeafFileReference(PixelLabJsonValue value, string pointer, RawManifestData raw)
        {
            if (value.Kind == PixelLabJsonKind.String)
            {
                raw.AddFileReference(pointer, value.StringValue);
            }
            else if (value.Kind == PixelLabJsonKind.Array)
            {
                CollectContainerFileReferences(value, pointer, raw);
            }
        }

        private static void CollectContainerFileReferences(PixelLabJsonValue value, string pointer, RawManifestData raw)
        {
            if (value.Kind == PixelLabJsonKind.String)
            {
                if (LooksLikePathReference(value.StringValue))
                {
                    raw.AddFileReference(pointer, value.StringValue);
                }

                return;
            }

            if (value.Kind == PixelLabJsonKind.Array)
            {
                for (int i = 0; i < value.ArrayValue.Count; i++)
                {
                    CollectContainerFileReferences(value.ArrayValue[i], pointer + "[" + i + "]", raw);
                }
            }
            else if (value.Kind == PixelLabJsonKind.Object)
            {
                foreach (KeyValuePair<string, PixelLabJsonValue> property in value.ObjectValue)
                {
                    CollectContainerFileReferences(property.Value, pointer + "." + property.Key, raw);
                }
            }
        }

        private static bool LooksLikePathReference(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string trimmed = value.Trim();
            return trimmed.IndexOf('/') >= 0
                || trimmed.IndexOf('\\') >= 0
                || LooksLikeWorkerArtifact(trimmed)
                || string.Equals(Path.GetExtension(trimmed), ".json", StringComparison.OrdinalIgnoreCase);
        }

        private static bool LooksLikeWorkerArtifact(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string withoutQuery = value.Trim();
            int query = withoutQuery.IndexOfAny(new[] { '?', '#' });
            if (query >= 0) withoutQuery = withoutQuery.Substring(0, query);
            string extension = Path.GetExtension(withoutQuery);
            return string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase);
        }

        private static void ValidateFileReferences(
            string assetDirectoryFullPath,
            List<RawFileReference> references,
            PixelLabManifestReport report)
        {
            if (references.Count == 0)
            {
                report.AddIssue(
                    PixelLabIssueSeverity.Error,
                    "missing-file-list",
                    "No local PNG or ZIP file references were found in the manifest.");
                AddUnlistedArtifacts(assetDirectoryFullPath, new HashSet<string>(StringComparer.OrdinalIgnoreCase), report);
                return;
            }

            string rootFullPath = Path.GetFullPath(assetDirectoryFullPath);
            var declaredFullPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int localReferences = 0;
            foreach (RawFileReference reference in references)
            {
                string declared = (reference.Value ?? string.Empty).Trim();
                var file = new PixelLabManifestFile(declared);
                report.Files.Add(file);
                if (declared.Length == 0)
                {
                    report.AddIssue(PixelLabIssueSeverity.Error, "empty-file-path", "Manifest contains an empty file path at " + reference.Pointer + ".");
                    continue;
                }

                if (IsRemoteReference(declared))
                {
                    file.IsRemote = true;
                    report.AddIssue(
                        PixelLabIssueSeverity.Warning,
                        "remote-file-reference",
                        "Remote file reference is not validated as a project artifact: " + declared);
                    continue;
                }

                if (HasTraversalSegment(declared))
                {
                    report.AddIssue(
                        PixelLabIssueSeverity.Error,
                        "file-path-traversal",
                        "File reference contains a '..' traversal segment: " + declared);
                    continue;
                }

                if (Path.IsPathRooted(declared) || HasDrivePrefix(declared))
                {
                    report.AddIssue(
                        PixelLabIssueSeverity.Error,
                        "absolute-file-path",
                        "File reference must be relative to its asset folder: " + declared);
                    continue;
                }

                string fullPath;
                string assetPath;
                try
                {
                    string normalizedDeclared = declared.Replace('\\', '/');
                    if (normalizedDeclared.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                    {
                        assetPath = PixelLabPaths.NormalizeAssetPath(normalizedDeclared);
                        fullPath = PixelLabPaths.AssetPathToFullPath(assetPath);
                    }
                    else
                    {
                        fullPath = Path.GetFullPath(Path.Combine(rootFullPath, normalizedDeclared.Replace('/', Path.DirectorySeparatorChar)));
                        if (!PixelLabPaths.TryFullPathToAssetPath(fullPath, out assetPath))
                        {
                            assetPath = report.AssetDirectoryPath + "/" + normalizedDeclared.TrimStart('/');
                        }
                    }
                }
                catch (Exception exception)
                {
                    report.AddIssue(
                        PixelLabIssueSeverity.Error,
                        "invalid-file-path",
                        "File reference cannot be resolved ('" + declared + "'): " + exception.Message);
                    continue;
                }

                if (!PixelLabPaths.IsFullPathInside(fullPath, rootFullPath, false))
                {
                    report.AddIssue(
                        PixelLabIssueSeverity.Error,
                        "file-outside-asset-folder",
                        "File reference resolves outside its PixelLab asset folder: " + declared);
                    continue;
                }

                file.AssetPath = PixelLabPaths.NormalizeAssetPath(assetPath);
                file.Exists = File.Exists(fullPath);
                localReferences++;
                if (!file.Exists)
                {
                    string message = Directory.Exists(fullPath)
                        ? "Manifest file reference points to a directory: " + declared
                        : "Manifest file is missing: " + declared;
                    report.AddIssue(PixelLabIssueSeverity.Error, "declared-file-missing", message);
                }

                string canonical = Path.GetFullPath(fullPath);
                if (!declaredFullPaths.Add(canonical))
                {
                    report.AddIssue(
                        PixelLabIssueSeverity.Warning,
                        "duplicate-file-reference",
                        "Manifest references the same file more than once: " + declared);
                }
            }

            if (localReferences == 0)
            {
                report.AddIssue(
                    PixelLabIssueSeverity.Error,
                    "missing-local-file-list",
                    "Manifest contains no valid local file path inside its PixelLab asset folder.");
            }

            AddUnlistedArtifacts(rootFullPath, declaredFullPaths, report);
        }

        private static void AddUnlistedArtifacts(
            string assetDirectoryFullPath,
            HashSet<string> declaredFullPaths,
            PixelLabManifestReport report)
        {
            if (!Directory.Exists(assetDirectoryFullPath))
            {
                report.AddIssue(
                    PixelLabIssueSeverity.Error,
                    "asset-folder-missing",
                    "PixelLab asset folder does not exist: " + report.AssetDirectoryPath);
                return;
            }

            try
            {
                foreach (string fullPath in Directory.EnumerateFiles(assetDirectoryFullPath, "*", SearchOption.AllDirectories))
                {
                    string extension = Path.GetExtension(fullPath);
                    if (!string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string canonical = Path.GetFullPath(fullPath);
                    if (!declaredFullPaths.Contains(canonical))
                    {
                        report.AddIssue(
                            PixelLabIssueSeverity.Info,
                            "unlisted-artifact",
                            "PNG or ZIP exists but is not declared by the manifest: "
                            + PixelLabPaths.MakeRelativePath(assetDirectoryFullPath, fullPath));
                    }
                }
            }
            catch (Exception exception)
            {
                report.AddIssue(
                    PixelLabIssueSeverity.Error,
                    "artifact-scan-failed",
                    "Could not scan the asset folder: " + exception.Message);
            }
        }

        private static bool IsRemoteReference(string value)
        {
            return value.IndexOf("://", StringComparison.Ordinal) >= 0
                || value.StartsWith("data:", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasTraversalSegment(string value)
        {
            string[] segments = value.Replace('\\', '/').Split('/');
            return segments.Any(segment => string.Equals(segment, "..", StringComparison.Ordinal));
        }

        private static bool HasDrivePrefix(string value)
        {
            return value.Length >= 2 && char.IsLetter(value[0]) && value[1] == ':';
        }

        private static string Abbreviate(string value)
        {
            return value.Length <= 80 ? value : value.Substring(0, 77) + "...";
        }

        private enum IdCategory
        {
            None,
            Asset,
            Operation,
            Resource
        }

        private sealed class RawFileReference
        {
            internal RawFileReference(string pointer, string value)
            {
                Pointer = pointer;
                Value = value;
            }

            internal string Pointer { get; private set; }
            internal string Value { get; private set; }
        }

        private sealed class RawManifestData
        {
            private readonly HashSet<string> filePointers = new HashSet<string>(StringComparer.Ordinal);

            internal List<string> AssetIds { get; } = new List<string>();
            internal List<string> OperationIds { get; } = new List<string>();
            internal List<string> ResourceIds { get; } = new List<string>();
            internal List<RawFileReference> FileReferences { get; } = new List<RawFileReference>();

            internal void AddFileReference(string pointer, string value)
            {
                if (filePointers.Add(pointer))
                {
                    FileReferences.Add(new RawFileReference(pointer, value));
                }
            }
        }
    }
}
