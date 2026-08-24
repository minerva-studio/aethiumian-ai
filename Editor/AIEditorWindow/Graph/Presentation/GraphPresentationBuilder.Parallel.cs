using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
    /// <summary>
    /// Converts topology references into free-node semantic relations.
    /// </summary>
    internal static partial class GraphPresentationBuilder
    {
        private static void BuildParallel(
            GraphPresentationItem source,
            IReadOnlyList<GraphEdgeDescriptor> outgoing,
            IReadOnlyDictionary<UUID, GraphPresentationItem> primary,
            ICollection<GraphPresentationRelation> relations,
            ICollection<GraphPresentationItem> virtualItems)
        {
            foreach (GraphEdgeDescriptor edge in outgoing)
            {
                if (edge.Reference.Address.FieldName == "events" && edge.Reference.IsCollection)
                {
                    continue;
                }

                relations.Add(CreateTopologyRelation(source.Output, edge, primary, ConvertTopologyKind(edge.Kind), edge.Label, virtualItems));
            }

            Parallel parallel = (Parallel)source.Node.Node;
            NodeReference[] references = parallel.events ?? Array.Empty<NodeReference>();
            if (references.Length == 0)
            {
                GraphPresentationItem placeholder = GraphPresentationItem.CreateParallelPlaceholder(
                    new GraphParallelPlaceholder(GraphParallelPlaceholderKind.NoBranches, -1, UUID.Empty));
                virtualItems.Add(placeholder);
                source.ParallelScope.AddBranch(placeholder);
                relations.Add(GraphPresentationRelation.CreateSynthetic(
                    source.Output, placeholder.Entry, GraphPresentationRelationKind.ParallelBranch,
                    GraphPresentationRelationRole.PlaceholderHint, string.Empty));
                relations.Add(GraphPresentationRelation.CreateSynthetic(
                    placeholder.Output, source.FlowComplete, GraphPresentationRelationKind.ParallelComplete,
                    GraphPresentationRelationRole.DerivedCompletion, "Returns Success"));
                return;
            }

            HashSet<UUID> scheduled = new();
            for (int index = 0; index < references.Length; index++)
            {
                NodeReference reference = references[index];
                GraphEdgeDescriptor edge = FindEdge(outgoing, "events", index);
                GraphPresentationItem target = null;
                bool valid = reference != null && reference.UUID != UUID.Empty
                    && primary.TryGetValue(reference.UUID, out target);
                if (!valid)
                {
                    bool missing = reference != null && reference.UUID != UUID.Empty;
                    GraphParallelPlaceholderKind placeholderKind = parallel.mode == Parallel.Mode.WaitAll
                        ? GraphParallelPlaceholderKind.IgnoredBranch
                        : GraphParallelPlaceholderKind.ImmediateCompletion;
                    GraphPresentationItem placeholder = GraphPresentationItem.CreateParallelPlaceholder(
                        new GraphParallelPlaceholder(placeholderKind, index, missing ? reference.UUID : UUID.Empty));
                    virtualItems.Add(placeholder);
                    source.ParallelScope.AddBranch(placeholder);
                    relations.Add(GraphPresentationRelation.CreateFromEdge(
                        source.Output, placeholder.Entry, GraphPresentationRelationKind.ParallelBranch,
                        GraphPresentationRelationRole.PlaceholderHint, $"Branch {index + 1}", edge));
                    if (parallel.mode == Parallel.Mode.WaitAny)
                    {
                        relations.Add(GraphPresentationRelation.CreateFromEdge(
                            placeholder.Output, source.FlowComplete, GraphPresentationRelationKind.ParallelComplete,
                            GraphPresentationRelationRole.DerivedCompletion, "Completes immediately", edge));
                    }

                    AppendWarning(source.Node, $"Invalid Parallel branch (events [{index}])");
                    continue;
                }

                bool isFirstScheduled = scheduled.Add(target.TargetUUID);
                if (isFirstScheduled)
                {
                    source.ParallelScope.AddBranch(target);
                }
                else
                {
                    AppendWarning(source.Node, $"Repeated Parallel target {target.TargetUUID} (events [{index}]); one stack is scheduled.");
                }

                relations.Add(GraphPresentationRelation.CreateFromEdge(
                    source.Output, target.Entry, GraphPresentationRelationKind.ParallelBranch,
                    GraphPresentationRelationRole.AuthoredReference, isFirstScheduled ? $"Branch {index + 1}" : "Shared stack",
                    edge));

                if (!isFirstScheduled || ReferenceEquals(target, source))
                {
                    continue;
                }

                relations.Add(GraphPresentationRelation.CreateFromEdge(
                    target.Completion, source.FlowComplete, GraphPresentationRelationKind.ParallelComplete,
                    GraphPresentationRelationRole.DerivedCompletion, parallel.mode == Parallel.Mode.WaitAll ? "Arrive" : "First complete",
                    edge));
            }
        }

        /// <summary>Builds the enumerable check, free Body, repeat, and exhausted completion of a ForEach Flow.</summary>
}
}
