using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace SchoolArcade.Editor.PixelLab
{
    [Serializable]
    internal sealed class PixelLabDirectionApproval
    {
        public string direction;
        public string source;
        public string[] sourceFrames;
        public int[] frameIndices;
        public string destination;
    }

    [Serializable]
    internal sealed class PixelLabCropApproval
    {
        public int x;
        public int y;
        public int width;
        public int height;
    }

    [Serializable]
    internal sealed class PixelLabCharacterApproval
    {
        public int version = 1;
        public string approvedAtUtc;
        public string sourceManifest;
        public string sourceManifestSha256;
        public string sourceZip;
        public string sourceZipSha256;
        public string provider;
        public string operation;
        public string characterId;
        public string[] jobIds;
        public int sourceWidth;
        public int sourceHeight;
        public int cropX;
        public int cropY;
        public int cellWidth;
        public int cellHeight;
        public int[] frameIndices;
        public string[] directionOrder;
        public PixelLabCropApproval crop;
        public PixelLabDirectionApproval[] directions;
    }

    internal sealed class PixelLabCharacterPromotionResult
    {
        internal string CharacterId;
        internal string ApprovalAssetPath;
        internal int CellWidth;
        internal int CellHeight;
        internal string[] DestinationAssetPaths;
        internal string SourceZipAssetPath;
    }

    internal static class PixelLabDirectionalCharacterPromoter
    {
        internal static readonly string[] DirectionOrder =
        {
            "south", "south-east", "east", "north-east",
            "north", "north-west", "west", "south-west"
        };

        internal const int WalkFrameCount = 6;
        private const string WalkArchiveRoot = "Leon_Style_Anchor_B/animations/walk/";

        private static readonly string[] DirectionDetectionOrder =
        {
            "north-east", "north-west", "south-east", "south-west",
            "south", "north", "east", "west"
        };

        internal static PixelLabCharacterPromotionResult Promote(
            string manifestAssetPath,
            string destinationBaseAssetPath,
            bool overwrite)
        {
            string manifestPath = PixelLabPaths.NormalizeAssetPath(manifestAssetPath);
            string destinationBase = PixelLabPaths.NormalizeAssetPath(destinationBaseAssetPath);
            if (!PixelLabPaths.IsUnderAssetPath(manifestPath, PixelLabPaths.CandidateRoot, false))
            {
                throw new InvalidOperationException("The source manifest must be inside the PixelLab candidate root.");
            }
            if (!PixelLabPaths.IsUnderAssetPath(destinationBase, PixelLabPaths.RuntimeArtRoot, false))
            {
                throw new InvalidOperationException("The approved destination must be inside Assets/Resources/Art.");
            }
            if (!string.IsNullOrEmpty(Path.GetExtension(destinationBase)))
            {
                throw new InvalidOperationException("The destination must be an extensionless resource base path.");
            }

            string manifestFullPath = PixelLabPaths.AssetPathToFullPath(manifestPath);
            string manifestJson = File.ReadAllText(manifestFullPath);
            PixelLabJsonValue root;
            string parseError;
            if (!PixelLabJson.TryParse(manifestJson, out root, out parseError) || root.Kind != PixelLabJsonKind.Object)
            {
                throw new InvalidOperationException("Could not parse the PixelLab manifest: " + parseError);
            }

            string provider = RequiredString(root, "provider");
            string operation = RequiredString(root, "operation");
            if (!string.Equals(provider, "pixel_lab", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(operation, "create_character_v3", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Directional promotion requires a pixel_lab create_character_v3 manifest.");
            }

            PixelLabJsonValue resources = RequiredObject(root, "resources");
            string characterId = RequiredString(resources, "characterId");
            PixelLabJsonValue request = RequiredObject(root, "request");
            PixelLabJsonValue input = RequiredObject(request, "input");
            PixelLabJsonValue imageSize = RequiredObject(input, "imageSize");
            int cellWidth = RequiredPositiveInteger(imageSize, "width");
            int cellHeight = RequiredPositiveInteger(imageSize, "height");
            Dictionary<string, string> sources = DirectionalSources(root, manifestPath);

            var destinationPaths = new string[DirectionOrder.Length];
            string destinationFolder = PixelLabPaths.NormalizeAssetPath(Path.GetDirectoryName(destinationBase));
            string destinationName = Path.GetFileName(destinationBase);
            for (int index = 0; index < DirectionOrder.Length; index++)
            {
                destinationPaths[index] = destinationFolder + "/" + destinationName + "_" + DirectionOrder[index] + ".png";
                if (!overwrite && File.Exists(PixelLabPaths.AssetPathToFullPath(destinationPaths[index])))
                {
                    throw new IOException("Approved destination already exists: " + destinationPaths[index]);
                }
            }

            string approvalAssetPath = destinationBase + ".approval.json";
            if (!overwrite && File.Exists(PixelLabPaths.AssetPathToFullPath(approvalAssetPath)))
            {
                throw new IOException("Approval record already exists: " + approvalAssetPath);
            }

            Directory.CreateDirectory(PixelLabPaths.AssetPathToFullPath(destinationFolder));
            var approvals = new PixelLabDirectionApproval[DirectionOrder.Length];
            int sourceWidth = 0;
            int sourceHeight = 0;
            int cropX = 0;
            int cropY = 0;
            for (int index = 0; index < DirectionOrder.Length; index++)
            {
                string direction = DirectionOrder[index];
                string sourceAssetPath = sources[direction];
                Texture2D source = LoadTexture(sourceAssetPath);
                try
                {
                    if (index == 0)
                    {
                        sourceWidth = source.width;
                        sourceHeight = source.height;
                        cropX = (sourceWidth - cellWidth) / 2;
                        cropY = (sourceHeight - cellHeight) / 2;
                        if (cropX < 0 || cropY < 0)
                        {
                            throw new InvalidOperationException("The requested image size is larger than the generated canvas.");
                        }
                    }
                    else if (source.width != sourceWidth || source.height != sourceHeight)
                    {
                        throw new InvalidOperationException("All directional canvases must use the same dimensions.");
                    }

                    byte[] png = CropWithoutLosingAlpha(source, cropX, cropY, cellWidth, cellHeight, direction);
                    WriteAtomically(PixelLabPaths.AssetPathToFullPath(destinationPaths[index]), png);
                    approvals[index] = new PixelLabDirectionApproval
                    {
                        direction = direction,
                        source = sourceAssetPath,
                        destination = destinationPaths[index]
                    };
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(source);
                }
            }

            var approval = new PixelLabCharacterApproval
            {
                approvedAtUtc = DateTime.UtcNow.ToString("O"),
                sourceManifest = manifestPath,
                sourceManifestSha256 = Sha256(manifestFullPath),
                provider = provider,
                operation = operation,
                characterId = characterId,
                sourceWidth = sourceWidth,
                sourceHeight = sourceHeight,
                cropX = cropX,
                cropY = cropY,
                cellWidth = cellWidth,
                cellHeight = cellHeight,
                directions = approvals
            };
            WriteAtomically(
                PixelLabPaths.AssetPathToFullPath(approvalAssetPath),
                System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(approval, true) + "\n"));

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string destinationPath in destinationPaths)
            {
                var importer = AssetImporter.GetAtPath(destinationPath) as TextureImporter;
                if (importer == null)
                {
                    throw new InvalidOperationException("Unity did not import the approved PNG: " + destinationPath);
                }
                PixelLabTexturePostprocessor.ApplyPixelArtSettings(importer, Mathf.Max(cellWidth, cellHeight));
                importer.SaveAndReimport();
            }

            return new PixelLabCharacterPromotionResult
            {
                CharacterId = characterId,
                ApprovalAssetPath = approvalAssetPath,
                CellWidth = cellWidth,
                CellHeight = cellHeight,
                DestinationAssetPaths = destinationPaths
            };
        }

        internal static PixelLabCharacterPromotionResult PromoteWalkAnimation(
            string manifestAssetPath,
            string zipAssetPath,
            string destinationBaseAssetPath,
            bool overwrite)
        {
            string manifestPath = PixelLabPaths.NormalizeAssetPath(manifestAssetPath);
            string zipPath = PixelLabPaths.NormalizeAssetPath(zipAssetPath);
            string destinationBase = PixelLabPaths.NormalizeAssetPath(destinationBaseAssetPath);
            if (!PixelLabPaths.IsUnderAssetPath(manifestPath, PixelLabPaths.CandidateRoot, false)
                || !PixelLabPaths.IsUnderAssetPath(zipPath, PixelLabPaths.CandidateRoot, false))
            {
                throw new InvalidOperationException("The walk inputs must be inside the PixelLab candidate root.");
            }
            if (!PixelLabPaths.IsUnderAssetPath(destinationBase, PixelLabPaths.RuntimeArtRoot, false)
                || !string.IsNullOrEmpty(Path.GetExtension(destinationBase)))
            {
                throw new InvalidOperationException("The approved destination must be an extensionless path inside Assets/Resources/Art.");
            }

            string manifestFullPath = PixelLabPaths.AssetPathToFullPath(manifestPath);
            string zipFullPath = PixelLabPaths.AssetPathToFullPath(zipPath);
            if (!File.Exists(manifestFullPath) || !File.Exists(zipFullPath))
            {
                throw new FileNotFoundException("The walk manifest or ZIP does not exist.");
            }

            PixelLabJsonValue root;
            string parseError;
            if (!PixelLabJson.TryParse(File.ReadAllText(manifestFullPath), out root, out parseError)
                || root.Kind != PixelLabJsonKind.Object)
            {
                throw new InvalidOperationException("Could not parse the PixelLab manifest: " + parseError);
            }
            string provider = RequiredString(root, "provider");
            string operation = RequiredString(root, "operation");
            if (!string.Equals(provider, "pixel_lab", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(operation, "animate_character", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Walk promotion requires a pixel_lab animate_character manifest.");
            }

            PixelLabJsonValue resources = RequiredObject(root, "resources");
            string characterId = RequiredString(resources, "characterId");
            string[] jobIds = RequiredStringArray(root, "jobIds");
            if (jobIds.Length != DirectionOrder.Length)
                throw new InvalidOperationException("The walk manifest must contain eight provider job IDs.");
            PixelLabJsonValue request = RequiredObject(root, "request");
            PixelLabJsonValue input = RequiredObject(request, "input");
            ValidateDirectionArray(input, "directions");
            int requestedFrameCount = RequiredPositiveInteger(input, "frameCount");
            if (requestedFrameCount != WalkFrameCount)
            {
                throw new InvalidOperationException("Walk promotion requires exactly six requested frames.");
            }
            PixelLabJsonValue delivery = RequiredObject(root, "delivery");
            PixelLabJsonValue animation = RequiredObject(delivery, "animation");
            int storedFrameCount = RequiredPositiveInteger(animation, "storedFrameCount");
            int referenceFrameIndex = RequiredNonNegativeInteger(animation, "referenceFrameIndex");
            if (storedFrameCount != WalkFrameCount + 1 || referenceFrameIndex != 0
                || !string.Equals(RequiredString(animation, "frameCountSemantics"), "generated_frames_excluding_reference", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The manifest frame semantics do not identify frame_000 as the reference frame.");
            }
            ValidateFrameIndices(animation, "playableFrameIndices");

            string destinationFolder = PixelLabPaths.NormalizeAssetPath(Path.GetDirectoryName(destinationBase));
            string destinationName = Path.GetFileName(destinationBase);
            var destinationPaths = new string[DirectionOrder.Length];
            for (int index = 0; index < DirectionOrder.Length; index++)
            {
                destinationPaths[index] = destinationFolder + "/" + destinationName + "_" + DirectionOrder[index] + ".png";
                if (!overwrite && File.Exists(PixelLabPaths.AssetPathToFullPath(destinationPaths[index])))
                {
                    throw new IOException("Approved destination already exists: " + destinationPaths[index]);
                }
            }
            string approvalAssetPath = destinationBase + ".approval.json";
            if (!overwrite && File.Exists(PixelLabPaths.AssetPathToFullPath(approvalAssetPath)))
            {
                throw new IOException("Approval record already exists: " + approvalAssetPath);
            }

            Directory.CreateDirectory(PixelLabPaths.AssetPathToFullPath(destinationFolder));
            var frameBytes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            int sourceWidth = 0;
            int sourceHeight = 0;
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = -1;
            int maxY = -1;
            using (ZipArchive archive = ZipFile.OpenRead(zipFullPath))
            {
                for (int directionIndex = 0; directionIndex < DirectionOrder.Length; directionIndex++)
                {
                    string direction = DirectionOrder[directionIndex];
                    for (int frameIndex = 1; frameIndex <= WalkFrameCount; frameIndex++)
                    {
                        string archivePath = WalkArchiveRoot + direction + "/frame_" + frameIndex.ToString("D3") + ".png";
                        ZipArchiveEntry entry = archive.GetEntry(archivePath);
                        if (entry == null) throw new InvalidDataException("Missing explicit walk frame in ZIP: " + archivePath);
                        using (Stream stream = entry.Open())
                        using (MemoryStream buffer = new MemoryStream())
                        {
                            stream.CopyTo(buffer);
                            byte[] bytes = buffer.ToArray();
                            frameBytes.Add(archivePath, bytes);
                            Texture2D texture = LoadTexture(bytes, archivePath);
                            try
                            {
                                if (sourceWidth == 0) { sourceWidth = texture.width; sourceHeight = texture.height; }
                                if (texture.width != sourceWidth || texture.height != sourceHeight)
                                    throw new InvalidDataException("All walk frames must use the same canvas dimensions.");
                                Color32[] pixels = texture.GetPixels32();
                                for (int y = 0; y < texture.height; y++)
                                for (int x = 0; x < texture.width; x++)
                                {
                                    if (pixels[y * texture.width + x].a == 0) continue;
                                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                                }
                            }
                            finally { UnityEngine.Object.DestroyImmediate(texture); }
                        }
                    }
                }
            }
            if (maxX < minX || maxY < minY) throw new InvalidDataException("Walk frames contain no opaque pixels.");
            int cell = MakeEven(Mathf.Max(maxX - minX + 1, maxY - minY + 1));
            if (cell > sourceWidth || cell > sourceHeight) throw new InvalidDataException("Opaque bounds do not fit in a square crop.");
            int cropX = ChooseCropStart(minX, maxX, sourceWidth, cell);
            int cropY = ChooseCropStart(minY, maxY, sourceHeight, cell);
            if (!ContainsBounds(cropX, cropY, cell, minX, minY, maxX, maxY))
                throw new InvalidDataException("The computed crop would remove opaque pixels.");

            var approvals = new PixelLabDirectionApproval[DirectionOrder.Length];
            for (int directionIndex = 0; directionIndex < DirectionOrder.Length; directionIndex++)
            {
                string direction = DirectionOrder[directionIndex];
                var sheet = new Texture2D(cell * WalkFrameCount, cell, TextureFormat.RGBA32, false, false);
                try
                {
                    var sheetPixels = new Color32[cell * WalkFrameCount * cell];
                    for (int frameIndex = 1; frameIndex <= WalkFrameCount; frameIndex++)
                    {
                        string archivePath = WalkArchiveRoot + direction + "/frame_" + frameIndex.ToString("D3") + ".png";
                        Texture2D texture = LoadTexture(frameBytes[archivePath], archivePath);
                        try
                        {
                            Color32[] pixels = texture.GetPixels32();
                            for (int y = 0; y < cell; y++)
                            {
                                Array.Copy(pixels, (cropY + y) * sourceWidth + cropX, sheetPixels,
                                    y * cell * WalkFrameCount + (frameIndex - 1) * cell, cell);
                            }
                        }
                        finally { UnityEngine.Object.DestroyImmediate(texture); }
                    }
                    sheet.SetPixels32(sheetPixels);
                    sheet.Apply(false, false);
                    WriteAtomically(PixelLabPaths.AssetPathToFullPath(destinationPaths[directionIndex]), sheet.EncodeToPNG());
                }
                finally { UnityEngine.Object.DestroyImmediate(sheet); }
                var sources = new string[WalkFrameCount];
                for (int frameIndex = 1; frameIndex <= WalkFrameCount; frameIndex++)
                    sources[frameIndex - 1] = WalkArchiveRoot + direction + "/frame_" + frameIndex.ToString("D3") + ".png";
                approvals[directionIndex] = new PixelLabDirectionApproval
                {
                    direction = direction,
                    source = sources[0],
                    sourceFrames = sources,
                    frameIndices = new[] { 1, 2, 3, 4, 5, 6 },
                    destination = destinationPaths[directionIndex]
                };
            }

            var approval = new PixelLabCharacterApproval
            {
                approvedAtUtc = DateTime.UtcNow.ToString("O"),
                sourceManifest = manifestPath,
                sourceManifestSha256 = Sha256(manifestFullPath),
                sourceZip = zipPath,
                sourceZipSha256 = Sha256(zipFullPath),
                provider = provider,
                operation = operation,
                characterId = characterId,
                jobIds = jobIds,
                sourceWidth = sourceWidth,
                sourceHeight = sourceHeight,
                cropX = cropX,
                cropY = cropY,
                cellWidth = cell,
                cellHeight = cell,
                frameIndices = new[] { 1, 2, 3, 4, 5, 6 },
                directionOrder = (string[])DirectionOrder.Clone(),
                crop = new PixelLabCropApproval { x = cropX, y = cropY, width = cell, height = cell },
                directions = approvals
            };
            WriteAtomically(PixelLabPaths.AssetPathToFullPath(approvalAssetPath),
                System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(approval, true) + "\n"));

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string destinationPath in destinationPaths)
            {
                TextureImporter importer = AssetImporter.GetAtPath(destinationPath) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Unity did not import the approved walk PNG: " + destinationPath);
                PixelLabTexturePostprocessor.ApplyPixelArtSettings(importer, cell);
                importer.SaveAndReimport();
            }
            return new PixelLabCharacterPromotionResult
            {
                CharacterId = characterId,
                ApprovalAssetPath = approvalAssetPath,
                CellWidth = cell,
                CellHeight = cell,
                DestinationAssetPaths = destinationPaths,
                SourceZipAssetPath = zipPath
            };
        }

        private static int MakeEven(int value) { return value % 2 == 0 ? value : value + 1; }

        private static int ChooseCropStart(int min, int max, int canvas, int cell)
        {
            int lower = Mathf.Max(0, max + 1 - cell);
            int upper = Mathf.Min(min, canvas - cell);
            if (lower > upper) throw new InvalidDataException("Opaque bounds cannot fit in computed crop.");
            return Mathf.Clamp((canvas - cell) / 2, lower, upper);
        }

        private static bool ContainsBounds(int x, int y, int cell, int minX, int minY, int maxX, int maxY)
        {
            return minX >= x && minY >= y && maxX < x + cell && maxY < y + cell;
        }

        private static Texture2D LoadTexture(byte[] bytes, string sourceName)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!ImageConversion.LoadImage(texture, bytes, false))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                throw new InvalidDataException("Could not decode walk PNG: " + sourceName);
            }
            return texture;
        }

        private static void ValidateDirectionArray(PixelLabJsonValue parent, string key)
        {
            PixelLabJsonValue value;
            if (parent == null || parent.Kind != PixelLabJsonKind.Object
                || !parent.ObjectValue.TryGetValue(key, out value) || value.Kind != PixelLabJsonKind.Array
                || value.ArrayValue.Count != DirectionOrder.Length)
            {
                throw new InvalidOperationException("The manifest must contain the eight directions in the approved order.");
            }
            for (int index = 0; index < DirectionOrder.Length; index++)
            {
                if (!string.Equals(value.ArrayValue[index].ToScalarString(), DirectionOrder[index], StringComparison.Ordinal))
                    throw new InvalidOperationException("The manifest direction order does not match the approved order.");
            }
        }

        private static string[] RequiredStringArray(PixelLabJsonValue parent, string key)
        {
            PixelLabJsonValue value;
            if (parent == null || parent.Kind != PixelLabJsonKind.Object
                || !parent.ObjectValue.TryGetValue(key, out value) || value.Kind != PixelLabJsonKind.Array)
            {
                throw new InvalidOperationException("The manifest is missing string array '" + key + "'.");
            }
            var result = new string[value.ArrayValue.Count];
            for (int index = 0; index < result.Length; index++)
            {
                result[index] = value.ArrayValue[index].ToScalarString();
                if (string.IsNullOrWhiteSpace(result[index]))
                    throw new InvalidOperationException("The manifest string array '" + key + "' contains an empty value.");
            }
            return result;
        }

        private static void ValidateFrameIndices(PixelLabJsonValue parent, string key)
        {
            PixelLabJsonValue value;
            if (parent == null || parent.Kind != PixelLabJsonKind.Object
                || !parent.ObjectValue.TryGetValue(key, out value) || value.Kind != PixelLabJsonKind.Array
                || value.ArrayValue.Count != WalkFrameCount)
            {
                throw new InvalidOperationException("The manifest must identify playable frame indices 1..6.");
            }
            for (int index = 0; index < WalkFrameCount; index++)
            {
                if (value.ArrayValue[index].Kind != PixelLabJsonKind.Number
                    || Mathf.RoundToInt((float)value.ArrayValue[index].NumberValue) != index + 1)
                    throw new InvalidOperationException("The manifest playable frame indices must be exactly 1..6.");
            }
        }

        private static Dictionary<string, string> DirectionalSources(PixelLabJsonValue root, string manifestAssetPath)
        {
            PixelLabJsonValue files;
            if (!root.ObjectValue.TryGetValue("files", out files) || files.Kind != PixelLabJsonKind.Array)
            {
                throw new InvalidOperationException("The manifest does not contain a local files array.");
            }

            string manifestFolder = PixelLabPaths.NormalizeAssetPath(Path.GetDirectoryName(manifestAssetPath));
            string manifestFolderFullPath = PixelLabPaths.AssetPathToFullPath(manifestFolder);
            var mapped = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (PixelLabJsonValue entry in files.ArrayValue)
            {
                string declared = entry.ToScalarString();
                if (string.IsNullOrWhiteSpace(declared)) continue;
                string normalized = PixelLabPaths.NormalizeAssetPath(declared);
                string assetPath = normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                    ? normalized
                    : manifestFolder + "/" + normalized;
                string fullPath = PixelLabPaths.AssetPathToFullPath(assetPath);
                if (!PixelLabPaths.IsFullPathInside(fullPath, manifestFolderFullPath) || !File.Exists(fullPath))
                {
                    throw new InvalidOperationException("A declared candidate file is missing or outside its asset folder: " + declared);
                }
                if (!string.Equals(Path.GetExtension(assetPath), ".png", StringComparison.OrdinalIgnoreCase)) continue;

                string fileName = Path.GetFileNameWithoutExtension(assetPath);
                string direction = DetectDirection(fileName);
                if (string.IsNullOrEmpty(direction)) continue;
                if (mapped.ContainsKey(direction))
                {
                    throw new InvalidOperationException("The manifest declares more than one PNG for direction " + direction + ".");
                }
                mapped.Add(direction, assetPath);
            }

            foreach (string direction in DirectionOrder)
            {
                if (!mapped.ContainsKey(direction))
                {
                    throw new InvalidOperationException("The manifest is missing direction " + direction + ".");
                }
            }
            return mapped;
        }

        private static string DetectDirection(string fileName)
        {
            foreach (string direction in DirectionDetectionOrder)
            {
                if (fileName.EndsWith("-" + direction, StringComparison.OrdinalIgnoreCase)
                    || fileName.EndsWith("_" + direction, StringComparison.OrdinalIgnoreCase))
                {
                    return direction;
                }
            }
            return string.Empty;
        }

        private static Texture2D LoadTexture(string assetPath)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(PixelLabPaths.AssetPathToFullPath(assetPath)), false))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                throw new InvalidOperationException("Could not decode candidate PNG: " + assetPath);
            }
            return texture;
        }

        private static byte[] CropWithoutLosingAlpha(Texture2D source, int cropX, int cropY, int width, int height, string direction)
        {
            Color32[] sourcePixels = source.GetPixels32();
            for (int y = 0; y < source.height; y++)
            for (int x = 0; x < source.width; x++)
            {
                if (sourcePixels[y * source.width + x].a == 0) continue;
                if (x < cropX || x >= cropX + width || y < cropY || y >= cropY + height)
                {
                    throw new InvalidOperationException("The centered crop would remove visible pixels for direction " + direction + ".");
                }
            }

            var croppedPixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                Array.Copy(sourcePixels, (cropY + y) * source.width + cropX, croppedPixels, y * width, width);
            }

            var cropped = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            try
            {
                cropped.SetPixels32(croppedPixels);
                cropped.Apply(false, false);
                return cropped.EncodeToPNG();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cropped);
            }
        }

        private static PixelLabJsonValue RequiredObject(PixelLabJsonValue parent, string key)
        {
            PixelLabJsonValue value;
            if (parent == null || parent.Kind != PixelLabJsonKind.Object
                || !parent.ObjectValue.TryGetValue(key, out value) || value.Kind != PixelLabJsonKind.Object)
            {
                throw new InvalidOperationException("The manifest is missing object '" + key + "'.");
            }
            return value;
        }

        private static string RequiredString(PixelLabJsonValue parent, string key)
        {
            PixelLabJsonValue value;
            string result = parent != null && parent.Kind == PixelLabJsonKind.Object
                && parent.ObjectValue.TryGetValue(key, out value)
                ? value.ToScalarString()
                : null;
            if (string.IsNullOrWhiteSpace(result))
            {
                throw new InvalidOperationException("The manifest is missing string '" + key + "'.");
            }
            return result;
        }

        private static int RequiredPositiveInteger(PixelLabJsonValue parent, string key)
        {
            PixelLabJsonValue value;
            if (parent == null || parent.Kind != PixelLabJsonKind.Object
                || !parent.ObjectValue.TryGetValue(key, out value) || value.Kind != PixelLabJsonKind.Number)
            {
                throw new InvalidOperationException("The manifest is missing numeric '" + key + "'.");
            }
            int result = Mathf.RoundToInt((float)value.NumberValue);
            if (result <= 0 || Math.Abs(value.NumberValue - result) > 0.0001d)
            {
                throw new InvalidOperationException("The manifest value '" + key + "' must be a positive integer.");
            }
            return result;
        }

        private static int RequiredNonNegativeInteger(PixelLabJsonValue parent, string key)
        {
            PixelLabJsonValue value;
            if (parent == null || parent.Kind != PixelLabJsonKind.Object
                || !parent.ObjectValue.TryGetValue(key, out value) || value.Kind != PixelLabJsonKind.Number)
            {
                throw new InvalidOperationException("The manifest is missing numeric '" + key + "'.");
            }
            int result = Mathf.RoundToInt((float)value.NumberValue);
            if (result < 0 || Math.Abs(value.NumberValue - result) > 0.0001d)
                throw new InvalidOperationException("The manifest value '" + key + "' must be a non-negative integer.");
            return result;
        }

        private static string Sha256(string fullPath)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(fullPath))
            {
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static void WriteAtomically(string fullPath, byte[] bytes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            string temporary = fullPath + ".tmp";
            try
            {
                File.WriteAllBytes(temporary, bytes);
                if (File.Exists(fullPath)) File.Delete(fullPath);
                File.Move(temporary, fullPath);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }

    public static class PixelLabBatchOperations
    {
        public static void PromoteApprovedLeonStyleAnchorB()
        {
            PixelLabCharacterPromotionResult result = PixelLabDirectionalCharacterPromoter.Promote(
                "Assets/Art/Generated/PixelLab/leon-style-anchor-b/asset-manifest.json",
                "Assets/Resources/Art/Player/Leon/leon_idle",
                true);
            Debug.Log("Promoted PixelLab Leon Style Anchor B: " + result.CharacterId + " -> " + result.ApprovalAssetPath);
        }

        public static void PromoteApprovedLeonWalkCandidateB()
        {
            PixelLabCharacterPromotionResult result = PixelLabDirectionalCharacterPromoter.PromoteWalkAnimation(
                "Assets/Art/Generated/PixelLab/leon-walk-b/asset-manifest.json",
                "Assets/Art/Generated/PixelLab/leon-walk-b/leon-walk-b-65-character-animation-bundle.zip",
                "Assets/Resources/Art/Player/Leon/leon_walk",
                true);
            Debug.Log("Promoted PixelLab Leon walk candidate B: " + result.CharacterId + " cell="
                + result.CellWidth + " -> " + result.ApprovalAssetPath);
        }
    }
}
