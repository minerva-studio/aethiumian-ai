using Aethiumian.AI.Nodes;
using Aethiumian.AI.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Aethiumian.AI.Editor.Exporting
{
    /// <summary>Provides read-only metadata queries over authored behaviour trees.</summary>
    public static class BehaviourTreeDomInspector
    {
        /// <summary>Returns a stable summary for the complete tree or selected ownership projection.</summary>
        /// <param name="tree">The loaded behaviour-tree asset.</param>
        /// <param name="startNode">The optional node UUID; empty selects the tree head.</param>
        /// <returns>The read-only tree summary.</returns>
        public static BehaviourTreeDomSummary GetSummary(BehaviourTreeData tree, UUID startNode = default)
        {
            DomExportContext context = Analyze(tree, startNode);
            TreeNode head = context.AuthoredNodes.FirstOrDefault(node => node.uuid == tree.headNodeUUID);
            BehaviourTreeDomNodeInfo headInfo = head == null ? null : CreateNodeInfo(context, head, GetAuthoredIndex(context, head));

            List<BehaviourTreeDomVariableInfo> variables = new List<BehaviourTreeDomVariableInfo>();
            if (tree.variables != null)
            {
                foreach (VariableData variable in tree.variables)
                {
                    if (variable == null)
                    {
                        continue;
                    }

                    variables.Add(new BehaviourTreeDomVariableInfo(variable.UUID, variable.name, variable.Type.ToString()));
                }
            }

            string assetPath = AssetDatabase.GetAssetPath(tree);
            return new BehaviourTreeDomSummary(
                assetPath,
                AssetDatabase.AssetPathToGUID(assetPath),
                tree.headNodeUUID,
                context.StartNode?.uuid ?? UUID.Empty,
                headInfo,
                context.AuthoredNodes.Count,
                context.ExportedNodeCount,
                context.AuthoredNodes.Count - context.ExportedNodeCount,
                context.VariableReferenceCount,
                context.UnresolvedReferenceCount,
                variables,
                context.Diagnostics.ToArray());
        }

        /// <summary>Finds authored nodes by stable name/type filters.</summary>
        /// <param name="tree">The loaded behaviour-tree asset.</param>
        /// <param name="options">The optional case-insensitive filter options.</param>
        /// <returns>Matching nodes in authored order.</returns>
        public static IReadOnlyList<BehaviourTreeDomNodeInfo> FindNodes(
            BehaviourTreeData tree,
            BehaviourTreeDomFindOptions options = null)
        {
            return FindNodes(tree, options, UUID.Empty);
        }

        /// <summary>Finds authored nodes using an explicit ownership projection start node.</summary>
        /// <param name="tree">The loaded behaviour-tree asset.</param>
        /// <param name="options">The optional case-insensitive filter options.</param>
        /// <param name="startNode">The optional node UUID; empty selects the tree head.</param>
        /// <returns>Matching nodes in authored order.</returns>
        public static IReadOnlyList<BehaviourTreeDomNodeInfo> FindNodes(
            BehaviourTreeData tree,
            BehaviourTreeDomFindOptions options,
            UUID startNode)
        {
            DomExportContext context = Analyze(tree, startNode);
            options ??= new BehaviourTreeDomFindOptions();
            string nameFilter = options.NameContains?.Trim();
            string typeFilter = options.Type?.Trim();

            List<BehaviourTreeDomNodeInfo> result = new List<BehaviourTreeDomNodeInfo>();
            for (int index = 0; index < context.AuthoredNodes.Count; index++)
            {
                TreeNode node = context.AuthoredNodes[index];
                DomTypeIdentity identity = context.GetTypeIdentity(node.GetType());
                if (options.ReachableOnly && !context.IsExported(node.uuid))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(nameFilter)
                    && (node.name ?? string.Empty).IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(typeFilter)
                    && !string.Equals(typeFilter, identity.ShortName, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(typeFilter, identity.FullName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.Add(CreateNodeInfo(context, node, index));
            }

            return result
                .OrderBy(node => node.AuthoredIndex)
                .ThenBy(node => node.Id.ToString(), StringComparer.Ordinal)
                .ToArray();
        }

        private static DomExportContext Analyze(BehaviourTreeData tree, UUID startNode)
        {
            if (tree == null)
            {
                throw new ArgumentNullException(nameof(tree));
            }

            DomExportContext context = new DomExportContext(tree, startNode);
            context.BuildDocument();
            return context;
        }

        private static BehaviourTreeDomNodeInfo CreateNodeInfo(DomExportContext context, TreeNode node, int authoredIndex)
        {
            DomTypeIdentity identity = context.GetTypeIdentity(node.GetType());
            return new BehaviourTreeDomNodeInfo(
                node.uuid,
                node.name,
                identity.ShortName,
                identity.IncludeClrType ? identity.FullName : null,
                context.IsExported(node.uuid),
                authoredIndex);
        }

        private static int GetAuthoredIndex(DomExportContext context, TreeNode target)
        {
            for (int index = 0; index < context.AuthoredNodes.Count; index++)
            {
                if (ReferenceEquals(context.AuthoredNodes[index], target))
                {
                    return index;
                }
            }

            return -1;
        }
    }

    /// <summary>Stable node filters used by the read-only inspector.</summary>
    public sealed class BehaviourTreeDomFindOptions
    {
        /// <summary>Case-insensitive substring matched against the authored node name.</summary>
        public string NameContains { get; set; }

        /// <summary>Case-insensitive exact match against short type or full CLR type.</summary>
        public string Type { get; set; }

        /// <summary>When true, exclude nodes outside the selected Head ownership projection.</summary>
        public bool ReachableOnly { get; set; } = true;
    }

    /// <summary>Stable read-only summary of a behaviour-tree asset.</summary>
    public sealed class BehaviourTreeDomSummary
    {
        internal BehaviourTreeDomSummary(
            string assetPath,
            string assetGuid,
            UUID headNodeId,
            UUID startNodeId,
            BehaviourTreeDomNodeInfo head,
            int totalNodeCount,
            int exportedNodeCount,
            int unreachableNodeCount,
            int variableReferenceCount,
            int unresolvedReferenceCount,
            IReadOnlyList<BehaviourTreeDomVariableInfo> variables,
            IReadOnlyList<BehaviourTreeDomDiagnostic> diagnostics)
        {
            AssetPath = assetPath ?? string.Empty;
            AssetGuid = assetGuid ?? string.Empty;
            HeadNodeId = headNodeId;
            StartNodeId = startNodeId;
            Head = head;
            TotalNodeCount = totalNodeCount;
            ExportedNodeCount = exportedNodeCount;
            UnreachableNodeCount = unreachableNodeCount;
            VariableReferenceCount = variableReferenceCount;
            UnresolvedReferenceCount = unresolvedReferenceCount;
            Variables = variables ?? Array.Empty<BehaviourTreeDomVariableInfo>();
            Diagnostics = diagnostics ?? Array.Empty<BehaviourTreeDomDiagnostic>();
        }

        public string AssetPath { get; }
        public string AssetGuid { get; }
        public UUID HeadNodeId { get; }
        public UUID StartNodeId { get; }
        public BehaviourTreeDomNodeInfo Head { get; }
        public int TotalNodeCount { get; }
        public int ExportedNodeCount { get; }
        public int UnreachableNodeCount { get; }
        public int VariableReferenceCount { get; }
        public int UnresolvedReferenceCount { get; }
        public IReadOnlyList<BehaviourTreeDomVariableInfo> Variables { get; }
        public IReadOnlyList<BehaviourTreeDomDiagnostic> Diagnostics { get; }
    }

    /// <summary>Stable read-only node metadata returned by the inspector.</summary>
    public sealed class BehaviourTreeDomNodeInfo
    {
        internal BehaviourTreeDomNodeInfo(
            UUID id,
            string name,
            string type,
            string clrType,
            bool reachable,
            int authoredIndex)
        {
            Id = id;
            Name = name ?? string.Empty;
            Type = type ?? string.Empty;
            ClrType = clrType;
            Reachable = reachable;
            AuthoredIndex = authoredIndex;
        }

        public UUID Id { get; }
        public string Name { get; }
        public string Type { get; }
        public string ClrType { get; }
        public bool Reachable { get; }
        public int AuthoredIndex { get; }
    }

    /// <summary>Stable variable metadata returned by the inspector.</summary>
    public sealed class BehaviourTreeDomVariableInfo
    {
        internal BehaviourTreeDomVariableInfo(UUID id, string name, string type)
        {
            Id = id;
            Name = name ?? string.Empty;
            Type = type ?? string.Empty;
        }

        public UUID Id { get; }
        public string Name { get; }
        public string Type { get; }
    }
}
