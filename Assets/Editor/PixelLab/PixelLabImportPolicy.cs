using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace SchoolArcade.Editor.PixelLab
{
    internal enum PixelLabPpuSource
    {
        Default,
        ManifestGlobal,
        ManifestFile,
        FileName
    }

    internal struct PixelLabPpuDecision
    {
        internal PixelLabPpuDecision(float value, PixelLabPpuSource source, string warning)
        {
            Value = value;
            Source = source;
            Warning = warning ?? string.Empty;
        }

        internal float Value { get; private set; }
        internal PixelLabPpuSource Source { get; private set; }
        internal string Warning { get; private set; }
    }

    internal static class PixelLabImportPolicy
    {
        internal const float DefaultPixelsPerUnit = 16f;
        internal const float MinimumPixelsPerUnit = 1f;
        internal const float MaximumPixelsPerUnit = 4096f;

        private static readonly Regex FileNamePpuPattern = new Regex(
            @"(?:^|[._-])ppu[._-]?([0-9]+(?:\.[0-9]+)?)(?=$|[._-])",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        internal static PixelLabPpuDecision DeterminePixelsPerUnit(string assetPath)
        {
            string normalizedAssetPath = PixelLabPaths.NormalizeAssetPath(assetPath);
            PixelLabPpuDecision fileNameDecision;
            if (TryReadFileNamePpu(normalizedAssetPath, out fileNameDecision))
            {
                return fileNameDecision;
            }

            string assetRoot;
            if (!PixelLabPaths.TryGetAssetRoot(normalizedAssetPath, out assetRoot))
            {
                return DefaultDecision(string.Empty);
            }

            string manifestAssetPath = assetRoot + "/" + PixelLabPaths.ManifestFileName;
            string manifestFullPath = PixelLabPaths.AssetPathToFullPath(manifestAssetPath);
            if (!File.Exists(manifestFullPath))
            {
                return DefaultDecision(string.Empty);
            }

            try
            {
                var info = new FileInfo(manifestFullPath);
                if (info.Length > PixelLabManifestValidator.MaxManifestBytes)
                {
                    return DefaultDecision("Manifest is larger than the 4 MiB safety limit; default PPU 16 was used.");
                }

                string json = File.ReadAllText(manifestFullPath);
                string relativePath = normalizedAssetPath.Substring(assetRoot.Length + 1);
                return SelectFromManifestJson(json, relativePath);
            }
            catch (Exception exception)
            {
                return DefaultDecision("Could not read PPU from '" + manifestAssetPath + "': " + exception.Message);
            }
        }

        internal static PixelLabPpuDecision SelectFromManifestJson(string json, string assetRelativePath)
        {
            PixelLabJsonValue root;
            string parseError;
            if (!PixelLabJson.TryParse(json, out root, out parseError))
            {
                return DefaultDecision("Manifest JSON could not be parsed for PPU selection: " + parseError);
            }

            var candidates = new List<PpuCandidate>();
            int invalidValues = 0;
            CollectPpuCandidates(root, NormalizeReference(assetRelativePath), 0, candidates, ref invalidValues);
            if (candidates.Count == 0)
            {
                string warning = invalidValues > 0
                    ? "Manifest PPU values were outside the supported range 1-4096; default PPU 16 was used."
                    : string.Empty;
                return DefaultDecision(warning);
            }

            int bestScore = int.MinValue;
            foreach (PpuCandidate candidate in candidates)
            {
                bestScore = Math.Max(bestScore, candidate.Score);
            }

            PpuCandidate best = null;
            bool conflicting = false;
            foreach (PpuCandidate candidate in candidates)
            {
                if (candidate.Score != bestScore)
                {
                    continue;
                }

                if (best == null)
                {
                    best = candidate;
                }
                else if (Math.Abs(best.Value - candidate.Value) > 0.0001f)
                {
                    conflicting = true;
                }
            }

            if (conflicting)
            {
                return DefaultDecision("Manifest contains conflicting equally specific PPU values; default PPU 16 was used.");
            }

            PixelLabPpuSource source = best.Score >= 500
                ? PixelLabPpuSource.ManifestFile
                : PixelLabPpuSource.ManifestGlobal;
            string invalidWarning = invalidValues > 0
                ? "Some manifest PPU values were invalid and ignored."
                : string.Empty;
            return new PixelLabPpuDecision(best.Value, source, invalidWarning);
        }

        internal static bool IsPpuKey(string key)
        {
            string normalized = NormalizeKey(key);
            return normalized == "ppu"
                || normalized == "pixelsperunit"
                || normalized == "spritepixelsperunit"
                || normalized == "defaultppu"
                || normalized == "defaultpixelsperunit";
        }

        internal static bool TryReadPpuValue(PixelLabJsonValue value, out float ppu)
        {
            ppu = 0f;
            if (value == null)
            {
                return false;
            }

            double parsed;
            if (value.Kind == PixelLabJsonKind.Number)
            {
                parsed = value.NumberValue;
            }
            else if (value.Kind == PixelLabJsonKind.String
                && double.TryParse(value.StringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
            {
                // String values are accepted for compatibility with worker schema changes.
            }
            else
            {
                return false;
            }

            if (double.IsNaN(parsed)
                || double.IsInfinity(parsed)
                || parsed < MinimumPixelsPerUnit
                || parsed > MaximumPixelsPerUnit)
            {
                return false;
            }

            ppu = (float)parsed;
            return true;
        }

        internal static string NormalizeKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            var characters = new char[key.Length];
            int count = 0;
            foreach (char character in key)
            {
                if (char.IsLetterOrDigit(character))
                {
                    characters[count++] = char.ToLowerInvariant(character);
                }
            }

            return new string(characters, 0, count);
        }

        private static bool TryReadFileNamePpu(string assetPath, out PixelLabPpuDecision decision)
        {
            decision = default(PixelLabPpuDecision);
            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            Match match = FileNamePpuPattern.Match(fileName ?? string.Empty);
            if (!match.Success)
            {
                return false;
            }

            float value;
            if (!float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                || value < MinimumPixelsPerUnit
                || value > MaximumPixelsPerUnit)
            {
                decision = DefaultDecision(
                    "File name PPU marker '" + match.Value + "' is outside the supported range 1-4096; default PPU 16 was used.");
                return true;
            }

            decision = new PixelLabPpuDecision(value, PixelLabPpuSource.FileName, string.Empty);
            return true;
        }

        private static void CollectPpuCandidates(
            PixelLabJsonValue value,
            string requestedRelativePath,
            int depth,
            List<PpuCandidate> candidates,
            ref int invalidValues)
        {
            if (value == null)
            {
                return;
            }

            if (value.Kind == PixelLabJsonKind.Object)
            {
                foreach (KeyValuePair<string, PixelLabJsonValue> property in value.ObjectValue)
                {
                    if (!IsPpuKey(property.Key))
                    {
                        continue;
                    }

                    float ppu;
                    if (!TryReadPpuValue(property.Value, out ppu))
                    {
                        invalidValues++;
                        continue;
                    }

                    int score = ScoreObject(value, requestedRelativePath, depth, property.Key);
                    candidates.Add(new PpuCandidate(ppu, score));
                }

                foreach (KeyValuePair<string, PixelLabJsonValue> property in value.ObjectValue)
                {
                    CollectPpuCandidates(property.Value, requestedRelativePath, depth + 1, candidates, ref invalidValues);
                }
            }
            else if (value.Kind == PixelLabJsonKind.Array)
            {
                foreach (PixelLabJsonValue item in value.ArrayValue)
                {
                    CollectPpuCandidates(item, requestedRelativePath, depth + 1, candidates, ref invalidValues);
                }
            }
        }

        private static int ScoreObject(PixelLabJsonValue objectValue, string requestedRelativePath, int depth, string ppuKey)
        {
            string normalizedKey = NormalizeKey(ppuKey);
            if (depth == 0 || normalizedKey.StartsWith("default", StringComparison.Ordinal))
            {
                return 100 - Math.Min(depth, 50);
            }

            var strings = new List<string>();
            CollectStrings(objectValue, strings, 0);
            string requestedFileName = Path.GetFileName(requestedRelativePath);
            foreach (string candidate in strings)
            {
                string reference = NormalizeReference(candidate);
                if (string.IsNullOrEmpty(reference) || IsRemoteReference(reference))
                {
                    continue;
                }

                if (string.Equals(reference, requestedRelativePath, StringComparison.OrdinalIgnoreCase)
                    || reference.EndsWith("/" + requestedRelativePath, StringComparison.OrdinalIgnoreCase))
                {
                    return 1000 + Math.Min(depth, 100);
                }

                if (!string.IsNullOrEmpty(requestedFileName)
                    && string.Equals(Path.GetFileName(reference), requestedFileName, StringComparison.OrdinalIgnoreCase))
                {
                    return 500 + Math.Min(depth, 100);
                }
            }

            return 10 - Math.Min(depth, 9);
        }

        private static void CollectStrings(PixelLabJsonValue value, List<string> results, int depth)
        {
            if (value == null || depth > 16 || results.Count >= 4096)
            {
                return;
            }

            if (value.Kind == PixelLabJsonKind.String)
            {
                results.Add(value.StringValue);
            }
            else if (value.Kind == PixelLabJsonKind.Object)
            {
                foreach (PixelLabJsonValue child in value.ObjectValue.Values)
                {
                    CollectStrings(child, results, depth + 1);
                }
            }
            else if (value.Kind == PixelLabJsonKind.Array)
            {
                foreach (PixelLabJsonValue child in value.ArrayValue)
                {
                    CollectStrings(child, results, depth + 1);
                }
            }
        }

        private static string NormalizeReference(string value)
        {
            string normalized = (value ?? string.Empty).Trim().Replace('\\', '/');
            while (normalized.StartsWith("./", StringComparison.Ordinal))
            {
                normalized = normalized.Substring(2);
            }

            return normalized.TrimStart('/');
        }

        private static bool IsRemoteReference(string value)
        {
            return value.IndexOf("://", StringComparison.Ordinal) >= 0
                || value.StartsWith("data:", StringComparison.OrdinalIgnoreCase);
        }

        private static PixelLabPpuDecision DefaultDecision(string warning)
        {
            return new PixelLabPpuDecision(DefaultPixelsPerUnit, PixelLabPpuSource.Default, warning);
        }

        private sealed class PpuCandidate
        {
            internal PpuCandidate(float value, int score)
            {
                Value = value;
                Score = score;
            }

            internal float Value { get; private set; }
            internal int Score { get; private set; }
        }
    }
}
