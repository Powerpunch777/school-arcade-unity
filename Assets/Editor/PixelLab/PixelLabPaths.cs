using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SchoolArcade.Editor.PixelLab
{
    internal static class PixelLabPaths
    {
        internal const string CandidateRoot = "Assets/Art/Generated/PixelLab";
        internal const string RuntimeArtRoot = "Assets/Resources/Art";
        internal const string ManifestFileName = "asset-manifest.json";

        internal static string ProjectRoot
        {
            get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..")); }
        }

        internal static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Replace('\\', '/').TrimEnd('/');
        }

        internal static bool IsUnderAssetPath(string path, string root, bool allowRoot = true)
        {
            string normalizedPath = NormalizeAssetPath(path);
            string normalizedRoot = NormalizeAssetPath(root);
            if (allowRoot && string.Equals(normalizedPath, normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return normalizedPath.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsGeneratedPng(string assetPath)
        {
            return IsUnderAssetPath(assetPath, CandidateRoot, false)
                && string.Equals(Path.GetExtension(assetPath), ".png", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsGeneratedZip(string assetPath)
        {
            return IsUnderAssetPath(assetPath, CandidateRoot, false)
                && string.Equals(Path.GetExtension(assetPath), ".zip", StringComparison.OrdinalIgnoreCase);
        }

        internal static string AssetPathToFullPath(string assetPath)
        {
            string normalized = NormalizeAssetPath(assetPath);
            if (!string.Equals(normalized, "Assets", StringComparison.OrdinalIgnoreCase)
                && !normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Expected a project-relative path beginning with 'Assets'.", nameof(assetPath));
            }

            string relative = normalized.Length == "Assets".Length
                ? string.Empty
                : normalized.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(Application.dataPath, relative));
        }

        internal static bool TryFullPathToAssetPath(string fullPath, out string assetPath)
        {
            assetPath = string.Empty;
            string normalizedFullPath;
            try
            {
                normalizedFullPath = Path.GetFullPath(fullPath);
            }
            catch (Exception)
            {
                return false;
            }

            string assetsRoot = Path.GetFullPath(Application.dataPath);
            if (string.Equals(normalizedFullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    assetsRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    PathComparison))
            {
                assetPath = "Assets";
                return true;
            }

            string prefix = EnsureTrailingSeparator(assetsRoot);
            if (!normalizedFullPath.StartsWith(prefix, PathComparison))
            {
                return false;
            }

            assetPath = "Assets/" + normalizedFullPath.Substring(prefix.Length).Replace('\\', '/');
            return true;
        }

        internal static bool TryGetAssetRoot(string assetPath, out string assetRoot)
        {
            assetRoot = string.Empty;
            string normalized = NormalizeAssetPath(assetPath);
            if (!IsUnderAssetPath(normalized, CandidateRoot, false))
            {
                return false;
            }

            string remainder = normalized.Substring(CandidateRoot.Length + 1);
            int slash = remainder.IndexOf('/');
            string assetFolder = slash < 0 ? remainder : remainder.Substring(0, slash);
            if (string.IsNullOrWhiteSpace(assetFolder))
            {
                return false;
            }

            assetRoot = CandidateRoot + "/" + assetFolder;
            return true;
        }

        internal static string FindNearestManifestAssetPath(string selectedAssetPath)
        {
            string normalized = NormalizeAssetPath(selectedAssetPath);
            if (!IsUnderAssetPath(normalized, CandidateRoot))
            {
                return string.Empty;
            }

            string current = normalized;
            string full = AssetPathToFullPath(current);
            if (File.Exists(full))
            {
                current = NormalizeAssetPath(Path.GetDirectoryName(current));
            }

            while (IsUnderAssetPath(current, CandidateRoot, false))
            {
                string candidate = current + "/" + ManifestFileName;
                if (File.Exists(AssetPathToFullPath(candidate)))
                {
                    return candidate;
                }

                current = NormalizeAssetPath(Path.GetDirectoryName(current));
            }

            return string.Empty;
        }

        internal static IReadOnlyList<string> FindAllManifestAssetPaths()
        {
            var results = new List<string>();
            string fullRoot = AssetPathToFullPath(CandidateRoot);
            if (!Directory.Exists(fullRoot))
            {
                return results;
            }

            foreach (string fullPath in Directory.EnumerateFiles(fullRoot, ManifestFileName, SearchOption.AllDirectories))
            {
                string assetPath;
                if (TryFullPathToAssetPath(fullPath, out assetPath))
                {
                    results.Add(NormalizeAssetPath(assetPath));
                }
            }

            results.Sort(StringComparer.OrdinalIgnoreCase);
            return results;
        }

        internal static bool IsFullPathInside(string fullPath, string allowedRoot, bool allowRoot = false)
        {
            string normalizedPath = Path.GetFullPath(fullPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedRoot = Path.GetFullPath(allowedRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (allowRoot && string.Equals(normalizedPath, normalizedRoot, PathComparison))
            {
                return true;
            }

            return normalizedPath.StartsWith(EnsureTrailingSeparator(normalizedRoot), PathComparison);
        }

        internal static string MakeRelativePath(string rootFullPath, string childFullPath)
        {
            string root = EnsureTrailingSeparator(Path.GetFullPath(rootFullPath));
            string child = Path.GetFullPath(childFullPath);
            if (!child.StartsWith(root, PathComparison))
            {
                throw new ArgumentException("The child path is outside the requested root.", nameof(childFullPath));
            }

            return child.Substring(root.Length).Replace('\\', '/');
        }

        internal static StringComparison PathComparison
        {
            get { return Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal; }
        }

        private static string EnsureTrailingSeparator(string path)
        {
            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
        }
    }
}
