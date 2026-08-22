using Aethiumian.AI.Nodes;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Editor.Exporting
{
    /// <summary>Editor menu actions that expose the read-only DOM exporter.</summary>
    internal static class BehaviourTreeDomExportCommands
    {
        internal static void CopyYaml(BehaviourTreeData tree, TreeNode node)
        {
            BehaviourTreeDomExportResult result = BehaviourTreeDomExporter.ExportYaml(tree, node?.uuid ?? UUID.Empty);
            if (string.IsNullOrEmpty(result.Content))
            {
                Debug.LogWarning("Readonly DOM export produced no document.");
                return;
            }

            GUIUtility.systemCopyBuffer = result.Content;
            LogDiagnostics(result);
        }

        internal static void SaveYaml(BehaviourTreeData tree, TreeNode node)
        {
            string projectPath = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string directory = Path.Combine(projectPath, "Temp", "Aethiumian.AI");
            Directory.CreateDirectory(directory);
            string treeName = string.IsNullOrEmpty(tree?.name) ? "BehaviourTree" : tree.name;
            string nodeName = string.IsNullOrEmpty(node?.name) ? "Head" : node.name;
            string path = EditorUtility.SaveFilePanel(
                "Save Readonly Behaviour Tree DOM",
                directory,
                treeName + "." + nodeName + ".dom.yaml",
                "yaml");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            BehaviourTreeDomExportResult result = BehaviourTreeDomExporter.ExportYaml(tree, node?.uuid ?? UUID.Empty);
            File.WriteAllText(path, result.Content);
            LogDiagnostics(result);
        }

        private static void LogDiagnostics(BehaviourTreeDomExportResult result)
        {
            if (result.Diagnostics.Count > 0)
            {
                Debug.LogWarning($"Readonly DOM export completed with {result.Diagnostics.Count} diagnostic(s).");
            }

            foreach (BehaviourTreeDomDiagnostic diagnostic in result.Diagnostics)
            {
                if (diagnostic.Severity == BehaviourTreeDomDiagnosticSeverity.Error)
                {
                    Debug.LogError($"[{diagnostic.Code}] {diagnostic.Message}");
                }
                else if (diagnostic.Severity == BehaviourTreeDomDiagnosticSeverity.Warning)
                {
                    Debug.LogWarning($"[{diagnostic.Code}] {diagnostic.Message}");
                }
            }
        }
    }
}
