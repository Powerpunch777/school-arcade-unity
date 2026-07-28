using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SchoolArcade.Editor.PixelLab
{
    internal static class PixelLabMenus
    {
        private const string ToolsReportMenu = "Tools/PixelLab/Open Manifest Report";
        private const string AssetsReportMenu = "Assets/PixelLab/Validate Selected Asset Manifest";
        private const string ToolsExtractMenu = "Tools/PixelLab/Extract Selected Character ZIP...";
        private const string AssetsExtractMenu = "Assets/PixelLab/Extract Selected Character ZIP...";

        [MenuItem(ToolsReportMenu, false, 2100)]
        private static void OpenAllReports()
        {
            PixelLabManifestReportWindow.ShowAllReports();
        }

        [MenuItem(AssetsReportMenu, false, 2100)]
        private static void OpenSelectedReport()
        {
            string selectedPath = GetSelectedAssetPath();
            if (string.IsNullOrEmpty(selectedPath))
            {
                EditorUtility.DisplayDialog(
                    "PixelLab Manifest Report",
                    "Select a PixelLab candidate file or folder in the Project window.",
                    "OK");
                return;
            }

            PixelLabManifestReportWindow.ShowForSelection(selectedPath);
        }

        [MenuItem(AssetsReportMenu, true)]
        private static bool CanOpenSelectedReport()
        {
            return PixelLabPaths.IsUnderAssetPath(GetSelectedAssetPath(), PixelLabPaths.CandidateRoot);
        }

        [MenuItem(ToolsExtractMenu, false, 2110)]
        private static void ExtractSelectedFromTools()
        {
            ExtractSelectedArchive();
        }

        [MenuItem(ToolsExtractMenu, true)]
        private static bool CanExtractSelectedFromTools()
        {
            return PixelLabPaths.IsGeneratedZip(GetSelectedAssetPath());
        }

        [MenuItem(AssetsExtractMenu, false, 2110)]
        private static void ExtractSelectedFromAssets()
        {
            ExtractSelectedArchive();
        }

        [MenuItem(AssetsExtractMenu, true)]
        private static bool CanExtractSelectedFromAssets()
        {
            return PixelLabPaths.IsGeneratedZip(GetSelectedAssetPath());
        }

        [MenuItem("Tools/PixelLab/Reimport All Generated PNGs", false, 2120)]
        private static void ReimportGeneratedPngs()
        {
            string fullRoot = PixelLabPaths.AssetPathToFullPath(PixelLabPaths.CandidateRoot);
            if (!Directory.Exists(fullRoot))
            {
                EditorUtility.DisplayDialog(
                    "PixelLab Reimport",
                    "Candidate folder does not exist: " + PixelLabPaths.CandidateRoot,
                    "OK");
                return;
            }

            var pngPaths = new List<string>();
            foreach (string fullPath in Directory.EnumerateFiles(fullRoot, "*", SearchOption.AllDirectories))
            {
                if (!string.Equals(Path.GetExtension(fullPath), ".png", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string assetPath;
                if (PixelLabPaths.TryFullPathToAssetPath(fullPath, out assetPath))
                {
                    pngPaths.Add(assetPath);
                }
            }

            pngPaths.Sort(StringComparer.OrdinalIgnoreCase);
            try
            {
                for (int i = 0; i < pngPaths.Count; i++)
                {
                    EditorUtility.DisplayProgressBar(
                        "PixelLab Reimport",
                        pngPaths[i],
                        pngPaths.Count == 0 ? 1f : (float)i / pngPaths.Count);
                    AssetDatabase.ImportAsset(
                        pngPaths[i],
                        ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            EditorUtility.DisplayDialog(
                "PixelLab Reimport",
                "Reimported " + pngPaths.Count + " generated PNG file(s).",
                "OK");
        }

        [MenuItem("Tools/PixelLab/Select Generated Candidates", false, 2140)]
        private static void SelectCandidateRoot()
        {
            SelectFolder(PixelLabPaths.CandidateRoot, "PixelLab candidate folder does not exist yet.");
        }

        [MenuItem("Tools/PixelLab/Select Approved Runtime Art", false, 2141)]
        private static void SelectRuntimeRoot()
        {
            SelectFolder(PixelLabPaths.RuntimeArtRoot, "Runtime art folder does not exist.");
        }

        private static void ExtractSelectedArchive()
        {
            string zipAssetPath = GetSelectedAssetPath();
            string destination = PixelLabZipExtractor.GetDestinationAssetPath(zipAssetPath);
            bool confirmed = EditorUtility.DisplayDialog(
                "Extract PixelLab Character ZIP",
                "This explicitly extracts the selected temporary archive after checking every entry.\n\n"
                + "ZIP: " + zipAssetPath + "\n"
                + "Destination: " + destination + "\n\n"
                + "Existing destinations are never overwritten.",
                "Extract",
                "Cancel");
            if (!confirmed)
            {
                return;
            }

            try
            {
                PixelLabZipExtractionResult result = PixelLabZipExtractor.ExtractProjectArchive(zipAssetPath);
                UnityEngine.Object destinationAsset = AssetDatabase.LoadMainAssetAtPath(result.DestinationAssetPath);
                if (destinationAsset != null)
                {
                    Selection.activeObject = destinationAsset;
                    EditorGUIUtility.PingObject(destinationAsset);
                }

                EditorUtility.DisplayDialog(
                    "PixelLab ZIP Extracted",
                    "Extracted " + result.ExtractedAssetPaths.Count + " file(s) into:\n"
                    + result.DestinationAssetPath
                    + "\n\nThe files remain Generated candidates and were not promoted to runtime art.",
                    "OK");
            }
            catch (Exception exception)
            {
                Debug.LogError("PixelLab ZIP extraction failed: " + exception);
                EditorUtility.DisplayDialog(
                    "PixelLab ZIP Extraction Failed",
                    exception.Message,
                    "OK");
            }
        }

        private static string GetSelectedAssetPath()
        {
            return Selection.activeObject == null
                ? string.Empty
                : PixelLabPaths.NormalizeAssetPath(AssetDatabase.GetAssetPath(Selection.activeObject));
        }

        private static void SelectFolder(string assetPath, string missingMessage)
        {
            UnityEngine.Object folder = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (folder == null)
            {
                EditorUtility.DisplayDialog("PixelLab", missingMessage + "\n\n" + assetPath, "OK");
                return;
            }

            Selection.activeObject = folder;
            EditorGUIUtility.PingObject(folder);
        }
    }
}
