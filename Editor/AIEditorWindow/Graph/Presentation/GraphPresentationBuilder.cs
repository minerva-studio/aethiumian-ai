using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
    /// <summary>
    /// Converts topology references into free-node semantic relations.
    /// </summary>
    internal static partial class GraphPresentationBuilder
    {
        internal static GraphPresentation Build(GraphTopology topology)
        {
            if (topology == null)
            {
                return new GraphPresentation(
                    new List<GraphPresentationItem>(),
                    new Dictionary<UUID, GraphPresentationItem>(),
                    new List<GraphPresentationRelation>(),
                    new List<GraphFlowScope>());
            }

            Dictionary<UUID, GraphPresentationItem> primary = new();
            List<GraphFlowScope> completionScopes = new();
            foreach (GraphNodeDescriptor descriptor in topology.Nodes)
            {
                GraphPresentationItem item = new(GetKind(descriptor.Node), descriptor, descriptor.UUID, descriptor.Warning);
                primary[descriptor.UUID] = item;
                if (descriptor.Node is Sequence)
                {
                    item.FlowScope = new GraphSequenceScope(item);
                    completionScopes.Add(item.FlowScope);
                }
                else if (descriptor.Node is Condition)
                {
                    item.FlowScope = new GraphConditionScope(item);
                    completionScopes.Add(item.FlowScope);
                }
                else if (descriptor.Node is Loop)
                {
                    item.FlowScope = new GraphLoopScope(item);
                    completionScopes.Add(item.FlowScope);
                }
                else if (descriptor.Node is Probability or PseudoProbability)
                {
                    item.FlowScope = new GraphProbabilityScope(item, topology.Tree);
                    completionScopes.Add(item.FlowScope);
                }
                else if (descriptor.Node is Decision)
                {
                    item.FlowScope = new GraphDecisionScope(item);
                    completionScopes.Add(item.FlowScope);
                }
                else if (descriptor.Node is Parallel)
                {
                    item.FlowScope = new GraphParallelScope(item);
                    completionScopes.Add(item.FlowScope);
                }
                else if (descriptor.Node is ForEach)
                {
                    item.FlowScope = new GraphForEachScope(item);
                    completionScopes.Add(item.FlowScope);
                }
            }

            HashSet<UUID> embedded = new();
            List<GraphPresentationRelation> relations = new();
            List<GraphPresentationItem> virtualItems = new();
            foreach (GraphNodeDescriptor descriptor in topology.Nodes)
            {
                IReadOnlyList<GraphEdgeDescriptor> outgoing = GetOutgoing(topology, descriptor);
                BuildRelations(topology, primary[descriptor.UUID], outgoing, primary, embedded, relations, virtualItems);
            }

            List<GraphServiceScope> serviceScopes = BuildServiceScopes(relations, virtualItems);

            AttachConditionPredicateSubtrees(topology, primary, embedded);

            foreach (GraphNodeDescriptor descriptor in topology.Nodes)
            {
                primary[descriptor.UUID].Position = descriptor.Position;
            }

            List<GraphPresentationItem> roots = new();
            foreach (GraphNodeDescriptor descriptor in topology.Nodes)
            {
                if (!embedded.Contains(descriptor.UUID))
                {
                    roots.Add(primary[descriptor.UUID]);
                }
            }

            roots.AddRange(virtualItems);

            return new GraphPresentation(roots, primary, relations, completionScopes, serviceScopes);
        }

        /// <summary>Derives each Condition predicate subtree from the existing authored predicate slot.</summary>
        private static void AttachConditionPredicateSubtrees(
            GraphTopology topology,
            IReadOnlyDictionary<UUID, GraphPresentationItem> primary,
            ISet<UUID> embedded)
        {
            foreach (GraphNodeDescriptor descriptor in topology.Nodes)
            {
                if (descriptor.Node is not Condition || !primary.TryGetValue(descriptor.UUID, out GraphPresentationItem owner))
                {
                    continue;
                }

                GraphConditionScope scope = owner.ConditionScope;
                GraphPresentationItem predicate = owner.Slots.Count > 0 ? owner.Slots[0].Content : null;
                if (predicate?.Node == null)
                {
                    continue;
                }

                scope.SetPredicateRoot(predicate);
                CollectConditionPredicate(topology, primary, scope, predicate, embedded);
                foreach (GraphPresentationItem member in scope.PredicateMembers)
                {
                    if (member.Parent == null || ReferenceEquals(member.Parent, owner))
                    {
                        scope.AddPredicateVisualRoot(member);
                    }
                }
            }
        }

        /// <summary>Collects the valid structural descendants of one authored predicate.</summary>
        private static void CollectConditionPredicate(
            GraphTopology topology,
            IReadOnlyDictionary<UUID, GraphPresentationItem> primary,
            GraphConditionScope scope,
            GraphPresentationItem current,
            ISet<UUID> embedded)
        {
            scope.AddPredicateMember(current);
            embedded.Add(current.TargetUUID);
            foreach (GraphEdgeDescriptor edge in topology.Edges.Where(candidate => candidate.Source.UUID == current.TargetUUID))
            {
                if (edge.Target == null || edge.Kind == GraphEdgeKind.Raw)
                {
                    continue;
                }

                if (!primary.TryGetValue(edge.Target.UUID, out GraphPresentationItem child))
                {
                    continue;
                }

                CollectConditionPredicate(topology, primary, scope, child, embedded);
            }
        }

        private static void BuildRelations(
            GraphTopology topology,
            GraphPresentationItem source,
            IReadOnlyList<GraphEdgeDescriptor> outgoing,
            IReadOnlyDictionary<UUID, GraphPresentationItem> primary,
            ISet<UUID> embedded,
            ICollection<GraphPresentationRelation> relations,
            ICollection<GraphPresentationItem> virtualItems)
        {
            if (source.Node.Node is Condition)
            {
                BuildCondition(source, outgoing, primary, embedded, relations, virtualItems);
                return;
            }

            if (source.Node.Node is Sequence)
            {
                BuildSequence(source, outgoing, primary, relations);
                return;
            }

            if (source.Node.Node is Loop)
            {
                BuildLoop(source, outgoing, primary, relations, virtualItems);
                return;
            }

            if (source.Node.Node is Probability or PseudoProbability)
            {
                BuildProbability(topology, source, outgoing, primary, relations, virtualItems);
                return;
            }

            if (source.Node.Node is Decision)
            {
                BuildDecision(source, outgoing, primary, relations, virtualItems);
                return;
            }

            if (source.Node.Node is Parallel)
            {
                BuildParallel(source, outgoing, primary, relations, virtualItems);
                return;
            }

            if (source.Node.Node is ForEach)
            {
                BuildForEach(topology, source, outgoing, primary, relations, virtualItems);
                return;
            }

            GraphPresentationRelationKind branchKind = GraphPresentationRelationKind.Structural;

            foreach (GraphEdgeDescriptor edge in outgoing)
            {
                GraphPresentationRelationKind kind = edge.Kind == GraphEdgeKind.Child
                    ? branchKind
                    : ConvertTopologyKind(edge.Kind);
                string label = edge.Kind == GraphEdgeKind.Child ? BuildBranchLabel(edge, kind) : edge.Label;
                if (edge.Kind == GraphEdgeKind.Service && edge.Target == null)
                {
                    GraphPresentationItem placeholder = GraphPresentationItem.CreateServicePlaceholder(
                        new GraphServicePlaceholder(source, label, edge.TargetUUID));
                    virtualItems.Add(placeholder);
                    relations.Add(new GraphPresentationRelation(
                        source.Output,
                        placeholder.Entry,
                        GraphPresentationRelationKind.Service,
                        GraphPresentationRelationRole.PlaceholderHint,
                        label,
                        edge,
                        edge.TargetUUID,
                        true,
                        edge.OccurrenceId));
                }
                else
                {
                    relations.Add(CreateTopologyRelation(source.Output, edge, primary, kind, label));
                }
            }
        }

        /// <summary>Builds one unique first-placement scope for every referenced real Service.</summary>
        private static GraphEdgeDescriptor FindEdge(IReadOnlyList<GraphEdgeDescriptor> outgoing, string label)
        {
            foreach (GraphEdgeDescriptor edge in outgoing)
            {
                if (edge.Label == label)
                {
                    return edge;
                }
            }

            return null;
        }

        /// <summary>Finds one authored collection occurrence without parsing its display label.</summary>
        private static GraphEdgeDescriptor FindEdge(
            IReadOnlyList<GraphEdgeDescriptor> outgoing,
            string fieldName,
            int collectionIndex)
        {
            foreach (GraphEdgeDescriptor edge in outgoing)
            {
                if (edge.FieldName == fieldName && edge.CollectionIndex == collectionIndex)
                {
                    return edge;
                }
            }

            return null;
        }

        private static GraphPresentationRelation CreateTopologyRelation(
            GraphPresentationEndpoint source,
            GraphEdgeDescriptor edge,
            IReadOnlyDictionary<UUID, GraphPresentationItem> primary,
            GraphPresentationRelationKind kind,
            string label)
        {
            GraphPresentationEndpoint target = edge.Target != null && primary.TryGetValue(edge.Target.UUID, out GraphPresentationItem item)
                ? item.Entry
                : default;
            return new GraphPresentationRelation(
                source,
                target,
                kind,
                GraphPresentationRelationRole.AuthoredReference,
                label,
                edge,
                edge.TargetUUID,
                edge.IsMissingTarget,
                edge.OccurrenceId);
        }

        private static IReadOnlyList<GraphEdgeDescriptor> GetOutgoing(GraphTopology topology, GraphNodeDescriptor source)
        {
            List<GraphEdgeDescriptor> result = new();
            foreach (GraphEdgeDescriptor edge in topology.Edges)
            {
                if (edge.Source == source)
                {
                    result.Add(edge);
                }
            }

            return result;
        }

        private static GraphPresentationRelationKind ConvertTopologyKind(GraphEdgeKind kind)
        {
            return kind switch
            {
                GraphEdgeKind.Service => GraphPresentationRelationKind.Service,
                GraphEdgeKind.Raw => GraphPresentationRelationKind.Raw,
                _ => GraphPresentationRelationKind.Structural,
            };
        }

        private static string BuildBranchLabel(GraphEdgeDescriptor edge, GraphPresentationRelationKind kind)
        {
            return kind switch
            {
                GraphPresentationRelationKind.DecisionBranch => $"Branch {edge.Label}",
                GraphPresentationRelationKind.ProbabilityBranch => $"Weight {edge.Label}",
                GraphPresentationRelationKind.ParallelBranch => $"Parallel {edge.Label}",
                _ => edge.Label,
            };
        }

        private static GraphPresentationKind GetKind(TreeNode node)
        {
            return node switch
            {
                Sequence => GraphPresentationKind.Sequence,
                Parallel => GraphPresentationKind.Parallel,
                ForEach => GraphPresentationKind.ForEach,
                Decision => GraphPresentationKind.Decision,
                Condition => GraphPresentationKind.Condition,
                Loop => GraphPresentationKind.Loop,
                _ => GraphPresentationKind.Card,
            };
        }
}
}
