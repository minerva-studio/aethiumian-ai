using Aethiumian.AI.Nodes;
using Aethiumian.AI.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

                    variables.Add(new BehaviourTreeDomVariableInfo(
                        variable.UUID,
                        variable.name,
                        variable.Type.ToString(),
                        GetRuntimeSourceName(variable),
                        variable.IsTimer ? tree.timeSettings.domain.ToString() : string.Empty,
                        variable.IsTimer ? tree.timeSettings.scaleMode.ToString() : string.Empty));
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

        private static string GetRuntimeSourceName(VariableData variable)
        {
            return variable.IsTimer ? "Timer" : variable.IsScript ? "Script" : "Value";
        }

        /// <summary>Validates one complete authored asset without checking external prefab or runtime behavior.</summary>
        /// <param name="tree">The loaded behaviour-tree asset.</param>
        /// <returns>The asset path, validity, node count, and structured diagnostics.</returns>
        public static BehaviourTreeValidationResult Validate(BehaviourTreeData tree)
        {
            BehaviourTreeDomSummary summary = GetSummary(tree);
            List<BehaviourTreeDomDiagnostic> diagnostics = summary.Diagnostics.ToList();
            foreach (string error in tree.GetStructureValidationErrors())
            {
                if (diagnostics.Any(diagnostic => string.Equals(diagnostic.Message, error, StringComparison.Ordinal)))
                {
                    continue;
                }

                diagnostics.Add(new BehaviourTreeDomDiagnostic(
                    "BTSTRUCT_INVALID",
                    BehaviourTreeDomDiagnosticSeverity.Error,
                    UUID.Empty,
                    "topology",
                    error));
            }

            foreach (TreeNode node in tree.nodes)
            {
                VariableFieldBase binding = node switch
                {
                    Cooldown cooldown => cooldown.timer,
                    Throttle throttle => throttle.timer,
                    Countdown countdown => countdown.updatingVariable,
                    _ => null,
                };
                if (node is not Cooldown && node is not Throttle && node is not Countdown) continue;
                VariableData definition = binding == null ? null : ResolveVariableDefinition(tree, binding.UUID);
                bool isTimer = definition?.IsTimer == true;
                bool valid = node is Countdown
                    ? IsValidCountdownDefinition(tree, definition)
                    : definition?.Type == VariableType.Float && isTimer && !definition.IsStatic && !definition.IsGlobal && !definition.IsScript;
                if (!valid)
                    diagnostics.Add(new BehaviourTreeDomDiagnostic("BT_TIMER_BINDING", BehaviourTreeDomDiagnosticSeverity.Error,
                        node.uuid, node is Countdown ? "updatingVariable" : "timer",
                        node is Countdown ? "Countdown requires an ordinary Float binding." : "Cooldown and Throttle require a TimerVariable binding."));
            }

            foreach (Subtree subtree in tree.nodes.OfType<Subtree>())
            {
                if (subtree.behaviourTreeData == null || subtree.variableTable?.entries == null) continue;
                foreach (VariableTranslationTable.Entry entry in subtree.variableTable.entries)
                {
                    if (entry.from == UUID.Empty || entry.to == UUID.Empty) continue;
                    VariableData child = subtree.behaviourTreeData.GetVariable(entry.from);
                    VariableData parent = ResolveVariableDefinition(tree, entry.to);
                    if (child == null || parent == null)
                    {
                        diagnostics.Add(new BehaviourTreeDomDiagnostic(
                            "BT_SUBTREE_TRANSLATION_MISSING",
                            BehaviourTreeDomDiagnosticSeverity.Error,
                            subtree.uuid,
                            "variableTable.entries",
                            $"Subtree '{subtree.name}' mapping {entry.from} -> {entry.to} has a missing variable endpoint."));
                        continue;
                    }

                    bool childTimer = child.IsTimer;
                    if (childTimer && (!parent.IsTimer
                        || parent.IsStatic || parent.IsGlobal || parent.IsScript))
                    {
                        diagnostics.Add(new BehaviourTreeDomDiagnostic(
                            "BT_SUBTREE_TIMER_TRANSLATION",
                            BehaviourTreeDomDiagnosticSeverity.Error,
                            subtree.uuid,
                            "variableTable.entries",
                            $"Subtree '{subtree.name}' timer {entry.from} must map to a local TimerVariable, but target {entry.to} is incompatible."));
                    }
                }
            }

            return new BehaviourTreeValidationResult(
                summary.AssetPath,
                summary.TotalNodeCount,
                diagnostics);
        }

        private static VariableData ResolveVariableDefinition(BehaviourTreeData tree, UUID uuid)
        {
            VariableData local = tree.GetVariable(uuid);
            if (local != null) return local;
            return AISetting.Instance?.GetGlobalVariableData(uuid);
        }

        private static bool IsValidCountdownDefinition(BehaviourTreeData tree, VariableData definition)
        {
            if (definition == null || definition.Type != VariableType.Float) return false;
            if (definition.IsScript)
            {
                if (!tree.targetScript) return false;
                MemberInfo[] members = tree.targetScript.GetClass().GetMember(definition.Path);
                return members.Length > 0
                    && definition.IsReadable(tree.targetScript.GetClass()) == true
                    && definition.IsWritable(tree.targetScript.GetClass()) == true;
            }

            return !definition.IsTimer;
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
            TimeSettings? settings = node is Timeout or Countdown ? context.Tree.timeSettings : null;
            return new BehaviourTreeDomNodeInfo(
                node.uuid,
                node.name,
                identity.ShortName,
                identity.IncludeClrType ? identity.FullName : null,
                context.IsExported(node.uuid),
                authoredIndex,
                settings?.domain.ToString() ?? string.Empty,
                settings?.scaleMode.ToString() ?? string.Empty);
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

    /// <summary>Minimal read-only validation result for one behaviour-tree asset.</summary>
    public sealed class BehaviourTreeValidationResult
    {
        internal BehaviourTreeValidationResult(
            string assetPath,
            int nodeCount,
            IReadOnlyList<BehaviourTreeDomDiagnostic> diagnostics)
        {
            AssetPath = assetPath ?? string.Empty;
            NodeCount = nodeCount;
            Diagnostics = diagnostics ?? Array.Empty<BehaviourTreeDomDiagnostic>();
            Valid = Diagnostics.All(
                diagnostic => diagnostic.Severity != BehaviourTreeDomDiagnosticSeverity.Error);
        }

        public string AssetPath { get; }
        public bool Valid { get; }
        public int NodeCount { get; }
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
            int authoredIndex,
            string timeDomain,
            string timeScaleMode)
        {
            Id = id;
            Name = name ?? string.Empty;
            Type = type ?? string.Empty;
            ClrType = clrType;
            Reachable = reachable;
            AuthoredIndex = authoredIndex;
            TimeDomain = timeDomain ?? string.Empty;
            TimeScaleMode = timeScaleMode ?? string.Empty;
        }

        public UUID Id { get; }
        public string Name { get; }
        public string Type { get; }
        public string ClrType { get; }
        public bool Reachable { get; }
        public int AuthoredIndex { get; }
        public string TimeDomain { get; }
        public string TimeScaleMode { get; }
    }

    /// <summary>Stable variable metadata returned by the inspector.</summary>
    public sealed class BehaviourTreeDomVariableInfo
    {
        internal BehaviourTreeDomVariableInfo(UUID id, string name, string type, string source, string timeDomain, string timeScaleMode)
        {
            Id = id;
            Name = name ?? string.Empty;
            Type = type ?? string.Empty;
            Source = source ?? string.Empty;
            TimeDomain = timeDomain ?? string.Empty;
            TimeScaleMode = timeScaleMode ?? string.Empty;
        }

        public UUID Id { get; }
        public string Name { get; }
        public string Type { get; }
        public string Source { get; }
        public string TimeDomain { get; }
        public string TimeScaleMode { get; }
    }
}
