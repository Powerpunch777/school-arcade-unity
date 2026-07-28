using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SchoolArcade.Editor.PixelLab
{
    internal sealed class PixelLabManifestReportWindow : EditorWindow
    {
        private readonly List<PixelLabManifestReport> reports = new List<PixelLabManifestReport>();
        private readonly Dictionary<string, bool> foldouts = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private Vector2 scroll;
        private string emptyMessage = string.Empty;

        internal static void ShowAllReports()
        {
            PixelLabManifestReportWindow window = GetWindow<PixelLabManifestReportWindow>();
            window.titleContent = new GUIContent("PixelLab Report");
            window.minSize = new Vector2(620f, 360f);
            window.ScanAll();
            window.Show();
        }

        internal static void ShowForSelection(string selectedAssetPath)
        {
            PixelLabManifestReportWindow window = GetWindow<PixelLabManifestReportWindow>();
            window.titleContent = new GUIContent("PixelLab Report");
            window.minSize = new Vector2(620f, 360f);
            window.ScanSelection(selectedAssetPath);
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("PixelLab Report");
            minSize = new Vector2(620f, 360f);
            if (reports.Count == 0)
            {
                ScanAll();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("PixelLab Generated Candidate Report", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Generated candidates stay in " + PixelLabPaths.CandidateRoot
                + ". This tooling never copies or moves them into the approved runtime path "
                + PixelLabPaths.RuntimeArtRoot + ".",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Validate All", EditorStyles.toolbarButton, GUILayout.Width(90f)))
                {
                    ScanAll();
                }

                if (GUILayout.Button("Validate Selection", EditorStyles.toolbarButton, GUILayout.Width(115f)))
                {
                    string selected = Selection.activeObject == null
                        ? string.Empty
                        : AssetDatabase.GetAssetPath(Selection.activeObject);
                    ScanSelection(selected);
                }

                GUILayout.FlexibleSpace();
                int errors = reports.Sum(report => report.Issues.Count(issue => issue.Severity == PixelLabIssueSeverity.Error));
                int warnings = reports.Sum(report => report.Issues.Count(issue => issue.Severity == PixelLabIssueSeverity.Warning));
                GUILayout.Label(reports.Count + " manifest(s), " + errors + " error(s), " + warnings + " warning(s)");
            }

            if (reports.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    string.IsNullOrEmpty(emptyMessage) ? "No PixelLab manifests were found." : emptyMessage,
                    MessageType.Info);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (PixelLabManifestReport report in reports)
            {
                DrawReport(report);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawReport(PixelLabManifestReport report)
        {
            bool expanded;
            if (!foldouts.TryGetValue(report.ManifestAssetPath, out expanded))
            {
                expanded = true;
            }

            string status = report.IsValid ? "OK" : "ERROR";
            expanded = EditorGUILayout.Foldout(
                expanded,
                status + "  " + report.ManifestAssetPath,
                true,
                EditorStyles.foldoutHeader);
            foldouts[report.ManifestAssetPath] = expanded;
            if (!expanded)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.SelectableLabel(
                        report.ManifestAssetPath,
                        EditorStyles.textField,
                        GUILayout.Height(EditorGUIUtility.singleLineHeight));
                    if (GUILayout.Button("Select Manifest", GUILayout.Width(110f)))
                    {
                        SelectAsset(report.ManifestAssetPath);
                    }

                    if (GUILayout.Button("Select Folder", GUILayout.Width(95f)))
                    {
                        SelectAsset(report.AssetDirectoryPath);
                    }
                }

                DrawIds("Asset IDs", report.AssetIds);
                DrawIds("Operation IDs", report.OperationIds);
                DrawIds("Resource IDs", report.ResourceIds);

                EditorGUILayout.LabelField("Declared Files (" + report.Files.Count + ")", EditorStyles.boldLabel);
                if (report.Files.Count == 0)
                {
                    EditorGUILayout.LabelField("None found");
                }

                foreach (PixelLabManifestFile file in report.Files)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        string fileStatus = file.IsRemote ? "REMOTE" : file.Exists ? "OK" : "MISSING";
                        GUILayout.Label(fileStatus, GUILayout.Width(60f));
                        EditorGUILayout.SelectableLabel(
                            file.DeclaredPath,
                            EditorStyles.textField,
                            GUILayout.Height(EditorGUIUtility.singleLineHeight));
                        using (new EditorGUI.DisabledScope(!file.Exists || string.IsNullOrEmpty(file.AssetPath)))
                        {
                            if (GUILayout.Button("Select", GUILayout.Width(60f)))
                            {
                                SelectAsset(file.AssetPath);
                            }
                        }
                    }
                }

                EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
                if (report.Issues.Count == 0)
                {
                    EditorGUILayout.HelpBox("Manifest IDs and declared files are valid.", MessageType.Info);
                }

                foreach (PixelLabValidationIssue issue in report.Issues)
                {
                    EditorGUILayout.HelpBox("[" + issue.Code + "] " + issue.Message, ToMessageType(issue.Severity));
                }
            }

            EditorGUILayout.Space(6f);
        }

        private static void DrawIds(string label, IReadOnlyList<string> ids)
        {
            EditorGUILayout.LabelField(label + " (" + ids.Count + ")", EditorStyles.boldLabel);
            if (ids.Count == 0)
            {
                EditorGUILayout.LabelField("None found");
                return;
            }

            foreach (string id in ids)
            {
                EditorGUILayout.SelectableLabel(
                    id,
                    EditorStyles.textField,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
        }

        private void ScanAll()
        {
            reports.Clear();
            foldouts.Clear();
            IReadOnlyList<string> manifests = PixelLabPaths.FindAllManifestAssetPaths();
            foreach (string manifest in manifests)
            {
                reports.Add(PixelLabManifestValidator.ValidateAssetManifest(manifest));
            }

            emptyMessage = manifests.Count == 0
                ? "No '" + PixelLabPaths.ManifestFileName + "' files were found below " + PixelLabPaths.CandidateRoot + "."
                : string.Empty;
            Repaint();
        }

        private void ScanSelection(string selectedAssetPath)
        {
            reports.Clear();
            foldouts.Clear();
            string normalized = PixelLabPaths.NormalizeAssetPath(selectedAssetPath);
            if (!PixelLabPaths.IsUnderAssetPath(normalized, PixelLabPaths.CandidateRoot))
            {
                emptyMessage = "Select a file or folder below " + PixelLabPaths.CandidateRoot + ".";
                Repaint();
                return;
            }

            if (string.Equals(normalized, PixelLabPaths.CandidateRoot, StringComparison.OrdinalIgnoreCase))
            {
                ScanAll();
                return;
            }

            string manifest = PixelLabPaths.FindNearestManifestAssetPath(normalized);
            if (string.IsNullOrEmpty(manifest))
            {
                string assetRoot;
                if (PixelLabPaths.TryGetAssetRoot(normalized, out assetRoot))
                {
                    manifest = assetRoot + "/" + PixelLabPaths.ManifestFileName;
                }
            }

            if (string.IsNullOrEmpty(manifest))
            {
                emptyMessage = "Could not identify a PixelLab asset folder for the selection.";
            }
            else
            {
                reports.Add(PixelLabManifestValidator.ValidateAssetManifest(manifest));
                emptyMessage = string.Empty;
            }

            Repaint();
        }

        private static MessageType ToMessageType(PixelLabIssueSeverity severity)
        {
            switch (severity)
            {
                case PixelLabIssueSeverity.Error: return MessageType.Error;
                case PixelLabIssueSeverity.Warning: return MessageType.Warning;
                default: return MessageType.Info;
            }
        }

        private static void SelectAsset(string assetPath)
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset == null)
            {
                EditorUtility.DisplayDialog("PixelLab", "Unity asset is not available: " + assetPath, "OK");
                return;
            }

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }
}
