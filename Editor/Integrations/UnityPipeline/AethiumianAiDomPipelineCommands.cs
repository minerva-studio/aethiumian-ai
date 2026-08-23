#if UNITY_EDITOR && AETHIUMIAN_HAS_UNITY_PIPELINE
using Aethiumian.AI;
using Aethiumian.AI.Editor.Exporting;
using Aethiumian.AI.Editor.Mutations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using Unity.Pipeline.Commands;
using UnityEngine;

namespace Aethiumian.AI.Editor.Integrations.UnityPipeline
{
    /// <summary>Exposes the Aethiumian DOM queries and guarded mutations through Unity Pipeline commands.</summary>
    public static class AethiumianAiDomPipelineCommands
    {
        /// <summary>Returns a compact read-only summary for a behaviour-tree asset.</summary>
        [CliCommand("athm_bt_summary", "Return a read-only summary of an Aethiumian behaviour tree.", MainThreadRequired = true)]
        public static AethiumianAiDomSummaryResponse TreeSummary(
            [CliArg("asset_path", "Project-relative BehaviourTreeData asset path.", Required = true)] string assetPath,
            [CliArg("start_node", "Optional start node UUID; defaults to Head.")] string startNode = null)
        {
            BehaviourTreeData tree = LoadTree(assetPath);
            BehaviourTreeDomSummary summary = BehaviourTreeDomInspector.GetSummary(tree, ParseStartNode(startNode));
            return MapSummary(summary);
        }

        /// <summary>Finds authored nodes without returning the complete DOM document.</summary>
        [CliCommand("athm_bt_find_nodes", "Find authored Aethiumian behaviour-tree nodes by name and type.", MainThreadRequired = true)]
        public static AethiumianAiDomFindResponse FindNodes(
            [CliArg("asset_path", "Project-relative BehaviourTreeData asset path.", Required = true)] string assetPath,
            [CliArg("start_node", "Optional start node UUID; defaults to Head.")] string startNode = null,
            [CliArg("name_contains", "Optional case-insensitive name substring.")] string nameContains = null,
            [CliArg("type", "Optional short or full CLR type filter.")] string type = null,
            [CliArg("reachable_only", "Exclude nodes outside the selected ownership projection.")] bool reachableOnly = true)
        {
            BehaviourTreeData tree = LoadTree(assetPath);
            UUID effectiveStartNode = ParseStartNode(startNode);
            if (effectiveStartNode == UUID.Empty)
            {
                effectiveStartNode = tree.headNodeUUID;
            }
            BehaviourTreeDomFindOptions options = new BehaviourTreeDomFindOptions
            {
                NameContains = nameContains,
                Type = type,
                ReachableOnly = reachableOnly,
            };
            IReadOnlyList<BehaviourTreeDomNodeInfo> nodes =
                BehaviourTreeDomInspector.FindNodes(tree, options, effectiveStartNode);

            AethiumianAiDomFindResponse response = new AethiumianAiDomFindResponse
            {
                assetPath = assetPath.Replace('\\', '/'),
                startNode = effectiveStartNode.ToString(),
                nodes = MapNodes(nodes),
            };
            return response;
        }

        /// <summary>Exports a read-only DOM to Temp or returns it inline on request.</summary>
        [CliCommand("athm_bt_export_dom", "Export an Aethiumian behaviour tree as read-only semantic YAML.", MainThreadRequired = true)]
        public static AethiumianAiDomExportResponse ExportDom(
            [CliArg("asset_path", "Project-relative BehaviourTreeData asset path.", Required = true)] string assetPath,
            [CliArg("start_node", "Optional start node UUID; defaults to Head.")] string startNode = null,
            [CliArg("output_mode", "Output mode: path (default) or inline.")] string outputMode = "path",
            [CliArg("output_path", "Optional output path under Temp/Aethiumian.AI.")] string outputPath = null)
        {
            BehaviourTreeData tree = LoadTree(assetPath);
            UUID effectiveStartNode = ParseStartNode(startNode);
            if (effectiveStartNode == UUID.Empty)
            {
                effectiveStartNode = tree.headNodeUUID;
            }

            BehaviourTreeDomExportResult exportResult =
                BehaviourTreeDomExporter.ExportYaml(tree, effectiveStartNode);

            string content = exportResult.Content ?? string.Empty;
            AethiumianAiDomExportResponse response = new AethiumianAiDomExportResponse
            {
                assetPath = assetPath.Replace('\\', '/'),
                startNode = effectiveStartNode.ToString(),
                exportedNodeCount = exportResult.ExportedNodeCount,
                diagnostics = MapDiagnostics(exportResult.Diagnostics),
                bytes = Encoding.UTF8.GetByteCount(content),
                lines = CountLines(content),
            };

            if (string.Equals(outputMode, "inline", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(outputPath))
                {
                    throw new ArgumentException("output_path must be empty when output_mode is inline.", nameof(outputPath));
                }

                response.content = content;
                return response;
            }

            if (!string.Equals(outputMode, "path", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("output_mode must be either path or inline.", nameof(outputMode));
            }

            string projectRoot = GetProjectRoot();
            string defaultDirectory = Path.Combine(projectRoot, "Temp", "Aethiumian.AI");
            string targetPath = string.IsNullOrWhiteSpace(outputPath)
                ? Path.Combine(defaultDirectory, SanitizeFileName(Path.GetFileNameWithoutExtension(assetPath)) + ".dom.yaml")
                : ResolveTempOutputPath(projectRoot, outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
            File.WriteAllText(targetPath, content, new UTF8Encoding(false));
            response.path = ToProjectRelativePath(projectRoot, targetPath);
            return response;
        }

        /// <summary>Creates and attaches a default node, then saves the behaviour-tree asset.</summary>
        [CliCommand("athm_bt_add_node", "Create and attach a typed Aethiumian behaviour-tree node.", MainThreadRequired = true)]
        public static AethiumianAiTreeAddResponse AddNode(
            [CliArg("asset_path", "Project-relative BehaviourTreeData asset path.", Required = true)] string assetPath,
            [CliArg("type", "Concrete short node type or full clrType.", Required = true)] string type,
            [CliArg("name", "Optional authored node name.")] string name = null,
            [CliArg("parent_node", "Optional parent node UUID; omit to create a new Head.")] string parentNode = null,
            [CliArg("field", "Optional node-reference field on parent_node.")] string field = null,
            [CliArg("index", "Collection insertion index; -1 appends or selects a scalar field.")] int index = -1)
        {
            BehaviourTreeData tree = LoadTree(assetPath);
            BehaviourTreeAddRequest request = new BehaviourTreeAddRequest
            {
                Type = type,
                Name = name,
                ParentNode = ParseOptionalUuid(parentNode, "parent_node"),
                Field = field,
                Index = index,
            };
            BehaviourTreeAddResult result = BehaviourTreeMutator.AddNode(tree, request);
            return EnsureAddMutationSucceeded(assetPath, result);
        }

        /// <summary>Deletes selected nodes using Graph editor decorator-unwrapping semantics.</summary>
        [CliCommand("athm_bt_remove_nodes", "Delete selected Aethiumian behaviour-tree nodes and save the asset.", MainThreadRequired = true)]
        public static AethiumianAiTreeRemoveResponse RemoveNodes(
            [CliArg("asset_path", "Project-relative BehaviourTreeData asset path.", Required = true)] string assetPath,
            [CliArg("node_ids", "Comma-separated authored node UUIDs to delete.", Required = true)] string nodeIds)
        {
            BehaviourTreeData tree = LoadTree(assetPath);
            UUID[] parsedIds = ParseNodeIds(nodeIds);
            BehaviourTreeRemoveResult result = BehaviourTreeMutator.RemoveNodes(tree, parsedIds);
            return EnsureRemoveMutationSucceeded(assetPath, result);
        }

        /// <summary>Reorders one node within its current owning collection.</summary>
        [CliCommand("athm_bt_reorder_node", "Reorder one Aethiumian behaviour-tree node within its current collection.", MainThreadRequired = true)]
        public static AethiumianAiTreeRearrangeResponse ReorderNode(
            [CliArg("asset_path", "Project-relative BehaviourTreeData asset path.", Required = true)] string assetPath,
            [CliArg("node_id", "Authored node UUID.", Required = true)] string nodeId,
            [CliArg("index", "Destination index within the current collection.", Required = true)] int index)
        {
            BehaviourTreeData tree = LoadTree(assetPath);
            BehaviourTreeReorderRequest request = new BehaviourTreeReorderRequest
            {
                NodeId = ParseOptionalUuid(nodeId, "node_id"),
                Index = index,
            };
            BehaviourTreeRearrangeResult result = BehaviourTreeMutator.ReorderNode(tree, request);
            return EnsureRearrangeMutationSucceeded(assetPath, result);
        }

        /// <summary>Moves one node to another structural or Service reference slot.</summary>
        [CliCommand("athm_bt_move_node", "Move an Aethiumian behaviour-tree node to another parent and field.", MainThreadRequired = true)]
        public static AethiumianAiTreeRearrangeResponse MoveNode(
            [CliArg("asset_path", "Project-relative BehaviourTreeData asset path.", Required = true)] string assetPath,
            [CliArg("node_id", "Authored node UUID.", Required = true)] string nodeId,
            [CliArg("target_parent", "Destination parent node UUID.", Required = true)] string targetParent,
            [CliArg("field", "Destination node-reference field.", Required = true)] string field,
            [CliArg("index", "Collection insertion index; -1 appends or selects a scalar field.")] int index = -1)
        {
            BehaviourTreeData tree = LoadTree(assetPath);
            BehaviourTreeMoveRequest request = new BehaviourTreeMoveRequest
            {
                NodeId = ParseOptionalUuid(nodeId, "node_id"),
                TargetParent = ParseOptionalUuid(targetParent, "target_parent"),
                Field = field,
                Index = index,
            };
            BehaviourTreeRearrangeResult result = BehaviourTreeMutator.MoveNode(tree, request);
            return EnsureRearrangeMutationSucceeded(assetPath, result);
        }

        /// <summary>Detaches one node while keeping it in the authored node list.</summary>
        [CliCommand("athm_bt_detach_node", "Detach an Aethiumian behaviour-tree node from its owning slot.", MainThreadRequired = true)]
        public static AethiumianAiTreeRearrangeResponse DetachNode(
            [CliArg("asset_path", "Project-relative BehaviourTreeData asset path.", Required = true)] string assetPath,
            [CliArg("node_id", "Authored node UUID.", Required = true)] string nodeId)
        {
            BehaviourTreeData tree = LoadTree(assetPath);
            BehaviourTreeRearrangeResult result =
                BehaviourTreeMutator.DetachNode(tree, ParseOptionalUuid(nodeId, "node_id"));
            return EnsureRearrangeMutationSucceeded(assetPath, result);
        }

        /// <summary>Moves one existing node to the tree Head.</summary>
        [CliCommand("athm_bt_set_head", "Set an existing Aethiumian behaviour-tree node as Head.", MainThreadRequired = true)]
        public static AethiumianAiTreeRearrangeResponse SetHead(
            [CliArg("asset_path", "Project-relative BehaviourTreeData asset path.", Required = true)] string assetPath,
            [CliArg("node_id", "Authored node UUID.", Required = true)] string nodeId)
        {
            BehaviourTreeData tree = LoadTree(assetPath);
            BehaviourTreeRearrangeResult result =
                BehaviourTreeMutator.SetHead(tree, ParseOptionalUuid(nodeId, "node_id"));
            return EnsureRearrangeMutationSucceeded(assetPath, result);
        }

        private static BehaviourTreeData LoadTree(string assetPath)
        {
            string normalizedPath = ValidateAssetPath(assetPath);
            BehaviourTreeData tree = AssetDatabase.LoadAssetAtPath<BehaviourTreeData>(normalizedPath);
            if (tree == null)
            {
                throw new ArgumentException($"BehaviourTreeData asset was not found at '{normalizedPath}'.", nameof(assetPath));
            }

            return tree;
        }

        private static UUID ParseStartNode(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return UUID.Empty;
            }

            if (UUID.TryParse(value, out UUID result))
            {
                return result;
            }

            throw new ArgumentException($"Invalid start_node UUID: '{value}'.", nameof(value));
        }

        private static UUID ParseOptionalUuid(string value, string argumentName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return UUID.Empty;
            }

            if (UUID.TryParse(value.Trim(), out UUID result))
            {
                return result;
            }

            throw new ArgumentException($"Invalid {argumentName} UUID: '{value}'.", argumentName);
        }

        private static UUID[] ParseNodeIds(string value)
        {
            string[] parts = (value ?? string.Empty)
                .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                throw new ArgumentException("node_ids must contain at least one UUID.", nameof(value));
            }

            return parts.Select(part => ParseOptionalUuid(part.Trim(), "node_ids")).ToArray();
        }

        private static AethiumianAiDomSummaryResponse MapSummary(BehaviourTreeDomSummary summary)
        {
            return new AethiumianAiDomSummaryResponse
            {
                assetPath = summary.AssetPath,
                assetGuid = summary.AssetGuid,
                headNode = MapNode(summary.Head),
                headNodeId = summary.HeadNodeId.ToString(),
                startNodeId = summary.StartNodeId.ToString(),
                totalNodeCount = summary.TotalNodeCount,
                exportedNodeCount = summary.ExportedNodeCount,
                unreachableNodeCount = summary.UnreachableNodeCount,
                variableReferenceCount = summary.VariableReferenceCount,
                unresolvedReferenceCount = summary.UnresolvedReferenceCount,
                variables = MapVariables(summary.Variables),
                diagnostics = MapDiagnostics(summary.Diagnostics),
            };
        }

        private static AethiumianAiTreeAddResponse MapAddMutation(BehaviourTreeAddResult result)
        {
            return new AethiumianAiTreeAddResponse
            {
                success = result.Success,
                saved = result.Saved,
                error = result.Error,
                createdNodeId = result.CreatedNodeId.ToString(),
                createdNodeName = result.CreatedNodeName,
                createdNodeType = result.CreatedNodeType,
                location = MapLocation(result.Location),
                headNodeId = result.HeadNodeId.ToString(),
                diagnostics = result.Diagnostics?.ToList() ?? new List<string>(),
            };
        }

        private static AethiumianAiTreeRemoveResponse MapRemoveMutation(BehaviourTreeRemoveResult result)
        {
            return new AethiumianAiTreeRemoveResponse
            {
                success = result.Success,
                saved = result.Saved,
                error = result.Error,
                removedNodeIds = result.RemovedNodeIds?.Select(uuid => uuid.ToString()).ToList() ?? new List<string>(),
                headNodeId = result.HeadNodeId.ToString(),
                diagnostics = result.Diagnostics?.ToList() ?? new List<string>(),
            };
        }

        private static AethiumianAiTreeRearrangeResponse MapRearrangeMutation(BehaviourTreeRearrangeResult result)
        {
            return new AethiumianAiTreeRearrangeResponse
            {
                success = result.Success,
                saved = result.Saved,
                error = result.Error,
                nodeId = result.NodeId.ToString(),
                source = MapLocation(result.Source),
                destination = MapLocation(result.Destination),
                headNodeId = result.HeadNodeId.ToString(),
                diagnostics = result.Diagnostics?.ToList() ?? new List<string>(),
            };
        }

        private static AethiumianAiTreeAddResponse EnsureAddMutationSucceeded(string assetPath, BehaviourTreeAddResult result)
        {
            AethiumianAiTreeAddResponse response = MapAddMutation(result);
            EnsureMutationSuccess(assetPath, response.success, response.error);
            response.assetPath = assetPath.Replace('\\', '/');
            return response;
        }

        private static AethiumianAiTreeRemoveResponse EnsureRemoveMutationSucceeded(string assetPath, BehaviourTreeRemoveResult result)
        {
            AethiumianAiTreeRemoveResponse response = MapRemoveMutation(result);
            EnsureMutationSuccess(assetPath, response.success, response.error);
            response.assetPath = assetPath.Replace('\\', '/');
            return response;
        }

        private static AethiumianAiTreeRearrangeResponse EnsureRearrangeMutationSucceeded(string assetPath, BehaviourTreeRearrangeResult result)
        {
            AethiumianAiTreeRearrangeResponse response = MapRearrangeMutation(result);
            EnsureMutationSuccess(assetPath, response.success, response.error);
            response.assetPath = assetPath.Replace('\\', '/');
            return response;
        }

        private static void EnsureMutationSuccess(string assetPath, bool success, string error)
        {
            if (!success)
            {
                throw new InvalidOperationException(error ?? $"Mutation failed for '{assetPath}'.");
            }
        }

        private static AethiumianAiTreeLocationResponse MapLocation(BehaviourTreeNodeLocation value)
        {
            if (value == null)
            {
                return null;
            }

            return new AethiumianAiTreeLocationResponse
            {
                kind = value.Kind.ToString().ToLowerInvariant(),
                ownerNodeId = value.OwnerNodeId.ToString(),
                field = value.Field,
                index = value.Index,
            };
        }

        private static List<AethiumianAiDomNodeResponse> MapNodes(IEnumerable<BehaviourTreeDomNodeInfo> values)
        {
            List<AethiumianAiDomNodeResponse> result = new List<AethiumianAiDomNodeResponse>();
            if (values == null)
            {
                return result;
            }

            foreach (BehaviourTreeDomNodeInfo value in values)
            {
                result.Add(MapNode(value));
            }

            return result;
        }

        private static AethiumianAiDomNodeResponse MapNode(BehaviourTreeDomNodeInfo value)
        {
            if (value == null)
            {
                return null;
            }

            return new AethiumianAiDomNodeResponse
            {
                id = value.Id.ToString(),
                name = value.Name,
                type = value.Type,
                clrType = value.ClrType,
                reachable = value.Reachable,
                authoredIndex = value.AuthoredIndex,
            };
        }

        private static List<AethiumianAiDomVariableResponse> MapVariables(IEnumerable<BehaviourTreeDomVariableInfo> values)
        {
            List<AethiumianAiDomVariableResponse> result = new List<AethiumianAiDomVariableResponse>();
            if (values == null)
            {
                return result;
            }

            foreach (BehaviourTreeDomVariableInfo value in values)
            {
                result.Add(new AethiumianAiDomVariableResponse
                {
                    id = value.Id.ToString(),
                    name = value.Name,
                    type = value.Type,
                });
            }

            return result;
        }

        private static List<AethiumianAiDomDiagnosticResponse> MapDiagnostics(IEnumerable<BehaviourTreeDomDiagnostic> values)
        {
            List<AethiumianAiDomDiagnosticResponse> result = new List<AethiumianAiDomDiagnosticResponse>();
            if (values == null)
            {
                return result;
            }

            foreach (BehaviourTreeDomDiagnostic value in values)
            {
                result.Add(new AethiumianAiDomDiagnosticResponse
                {
                    code = value.Code,
                    severity = value.Severity.ToString(),
                    node = value.NodeId.ToString(),
                    field = value.FieldPath,
                    occurrence = value.SourceOccurrence,
                    message = value.Message,
                });
            }

            return result;
        }

        private static string ValidateAssetPath(string assetPath)
        {
            string normalized = (assetPath ?? string.Empty).Trim().Replace('\\', '/');
            if (!normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("../", StringComparison.Ordinal)
                || normalized.Contains("/..", StringComparison.Ordinal))
            {
                throw new ArgumentException("asset_path must be a project-relative path under Assets.", nameof(assetPath));
            }

            return normalized;
        }

        private static string ResolveTempOutputPath(string projectRoot, string outputPath)
        {
            string normalized = (outputPath ?? string.Empty).Trim().Replace('\\', '/');
            if (Path.IsPathRooted(normalized)
                || (!normalized.Equals("Temp/Aethiumian.AI", StringComparison.OrdinalIgnoreCase)
                    && !normalized.StartsWith("Temp/Aethiumian.AI/", StringComparison.OrdinalIgnoreCase))
                || normalized.Contains("../", StringComparison.Ordinal)
                || normalized.Contains("/..", StringComparison.Ordinal))
            {
                throw new ArgumentException("output_path must remain under Temp/Aethiumian.AI.", nameof(outputPath));
            }

            string fullPath = Path.GetFullPath(Path.Combine(projectRoot, normalized));
            string allowedRoot = Path.GetFullPath(Path.Combine(projectRoot, "Temp", "Aethiumian.AI")) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("output_path must remain under Temp/Aethiumian.AI.", nameof(outputPath));
            }

            return fullPath;
        }

        private static string GetProjectRoot()
        {
            return Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("Unity project root could not be resolved.");
        }

        private static string ToProjectRelativePath(string projectRoot, string fullPath)
        {
            string relative = Path.GetRelativePath(projectRoot, fullPath);
            return relative.Replace('\\', '/');
        }

        private static string SanitizeFileName(string value)
        {
            string name = string.IsNullOrWhiteSpace(value) ? "BehaviourTree" : value;
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalid, '_');
            }

            return name;
        }

        private static int CountLines(string content)
        {
            return string.IsNullOrEmpty(content) ? 0 : content.Count(character => character == '\n') + 1;
        }
    }

    [Serializable]
    public sealed class AethiumianAiDomSummaryResponse
    {
        public string assetPath;
        public string assetGuid;
        public string headNodeId;
        public string startNodeId;
        public AethiumianAiDomNodeResponse headNode;
        public int totalNodeCount;
        public int exportedNodeCount;
        public int unreachableNodeCount;
        public int variableReferenceCount;
        public int unresolvedReferenceCount;
        public List<AethiumianAiDomVariableResponse> variables;
        public List<AethiumianAiDomDiagnosticResponse> diagnostics;
    }

    [Serializable]
    public sealed class AethiumianAiDomFindResponse
    {
        public string assetPath;
        public string startNode;
        public List<AethiumianAiDomNodeResponse> nodes;
    }

    [Serializable]
    public sealed class AethiumianAiDomExportResponse
    {
        public string assetPath;
        public string startNode;
        public string path;
        public string content;
        public int bytes;
        public int lines;
        public int exportedNodeCount;
        public List<AethiumianAiDomDiagnosticResponse> diagnostics;
    }

    [Serializable]
    public sealed class AethiumianAiTreeAddResponse
    {
        public string assetPath;
        public bool success;
        public bool saved;
        public string error;
        public string createdNodeId;
        public string createdNodeName;
        public string createdNodeType;
        public AethiumianAiTreeLocationResponse location;
        public string headNodeId;
        public List<string> diagnostics;
    }

    [Serializable]
    public sealed class AethiumianAiTreeRemoveResponse
    {
        public string assetPath;
        public bool success;
        public bool saved;
        public string error;
        public List<string> removedNodeIds;
        public string headNodeId;
        public List<string> diagnostics;
    }

    [Serializable]
    public sealed class AethiumianAiTreeRearrangeResponse
    {
        public string assetPath;
        public bool success;
        public bool saved;
        public string error;
        public string nodeId;
        public AethiumianAiTreeLocationResponse source;
        public AethiumianAiTreeLocationResponse destination;
        public string headNodeId;
        public List<string> diagnostics;
    }

    [Serializable]
    public sealed class AethiumianAiTreeLocationResponse
    {
        public string kind;
        public string ownerNodeId;
        public string field;
        public int index;
    }

    [Serializable]
    public sealed class AethiumianAiDomNodeResponse
    {
        public string id;
        public string name;
        public string type;
        public string clrType;
        public bool reachable;
        public int authoredIndex;
    }

    [Serializable]
    public sealed class AethiumianAiDomVariableResponse
    {
        public string id;
        public string name;
        public string type;
    }

    [Serializable]
    public sealed class AethiumianAiDomDiagnosticResponse
    {
        public string code;
        public string severity;
        public string node;
        public string field;
        public string occurrence;
        public string message;
    }
}
#endif

