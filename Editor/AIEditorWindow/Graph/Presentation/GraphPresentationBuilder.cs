using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using BooleanNode = Aethiumian.AI.Nodes.Boolean;

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
                item.LeafVisual = BuildLeafVisual(topology.Tree, descriptor);
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
            List<GraphDecoratorStack> decoratorStacks = BuildDecoratorStacks(relations);

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

            return new GraphPresentation(roots, primary, relations, completionScopes, serviceScopes, decoratorStacks);
        }

        /// <summary>Builds one stable semantic leaf descriptor before any canvas layout is measured.</summary>
        private static GraphLeafVisualDescriptor BuildLeafVisual(BehaviourTreeData tree, GraphNodeDescriptor descriptor)
        {
            if (descriptor.Node is BooleanNode boolean)
            {
                string variable = boolean.boolean == null || !boolean.boolean.HasEditorReference
                    ? "MISSING"
                    : tree?.GetVariableDescName(boolean.boolean.UUID) ?? "MISSING";
                string full = $"${variable}";
                string title = full.Length > 22 ? full.Substring(0, 21) + "…" : full;
                float width = Mathf.Clamp(20f + full.Length * 7f, 72f, 168f);
                return new GraphLeafVisualDescriptor(title, $"{descriptor.DisplayName}\nBoolean · {full}", new Vector2(width, 26f), true, null);
            }

            if (descriptor.Node is Constant constant)
            {
                string title = constant.returnValue ? "TRUE" : "FALSE";
                return new GraphLeafVisualDescriptor(title, $"{descriptor.DisplayName}\nConstant · {title}", new Vector2(58f, 24f), false, constant.returnValue);
            }

            return null;
        }

        /// <summary>Builds only unambiguous Inverter/Always chains; malformed or shared references remain independent cards.</summary>
        private static List<GraphDecoratorStack> BuildDecoratorStacks(IReadOnlyList<GraphPresentationRelation> relations)
        {
            Dictionary<GraphPresentationItem, GraphPresentationItem> next = new();
            Dictionary<GraphPresentationItem, int> incoming = new();
            HashSet<GraphPresentationItem> ambiguousSources = new();
            foreach (GraphPresentationRelation relation in relations)
            {
                if (relation.Role != GraphPresentationRelationRole.AuthoredReference || relation.Origin == null
                    || relation.Target.Item?.Node == null)
                {
                    continue;
                }

                if (relation.Kind is not (GraphPresentationRelationKind.Service or GraphPresentationRelationKind.Raw))
                {
                    incoming.TryGetValue(relation.Target.Item, out int count);
                    incoming[relation.Target.Item] = count + 1;
                }

                if (relation.Origin.FieldName != "node"
                    || relation.Source.Item?.Node?.Node is not (Inverter or Always)
                    || ambiguousSources.Contains(relation.Source.Item))
                {
                    continue;
                }

                if (!next.TryAdd(relation.Source.Item, relation.Target.Item))
                {
                    next.Remove(relation.Source.Item);
                    ambiguousSources.Add(relation.Source.Item);
                }
            }

            List<GraphDecoratorStack> result = new();
            foreach (GraphPresentationItem outer in next.Keys)
            {
                if (incoming.TryGetValue(outer, out int count) && count > 0)
                {
                    continue;
                }

                List<GraphPresentationItem> badges = new();
                HashSet<GraphPresentationItem> visited = new();
                GraphPresentationItem current = outer;
                while (current?.Node?.Node is Inverter or Always && next.TryGetValue(current, out GraphPresentationItem child)
                    && visited.Add(current) && (!incoming.TryGetValue(child, out int childIncoming) || childIncoming == 1))
                {
                    badges.Add(current);
                    current = child;
                }

                if (badges.Count == 0 || current?.Node == null || current.Node.Node is Inverter or Always || !visited.Add(current))
                {
                    continue;
                }

                GraphDecoratorStack stack = new(current);
                foreach (GraphPresentationItem badge in badges)
                {
                    stack.AddBadge(badge);
                }

                result.Add(stack);
            }

            return result;
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
            List<GraphPresentationRelation> relations,
            ICollection<GraphPresentationItem> virtualItems)
        {
            int firstOwnedRelation = relations.Count;
            if (source.Node.Node is Condition)
            {
                BuildCondition(source, outgoing, primary, embedded, relations, virtualItems);
            }
            else if (source.Node.Node is Sequence)
            {
                BuildSequence(source, outgoing, primary, relations);
            }
            else if (source.Node.Node is Loop)
            {
                BuildLoop(source, outgoing, primary, relations, virtualItems);
            }
            else if (source.Node.Node is Probability or PseudoProbability)
            {
                BuildProbability(topology, source, outgoing, primary, relations, virtualItems);
            }
            else if (source.Node.Node is Decision)
            {
                BuildDecision(source, outgoing, primary, relations, virtualItems);
            }
            else if (source.Node.Node is Parallel)
            {
                BuildParallel(source, outgoing, primary, relations, virtualItems);
            }
            else if (source.Node.Node is ForEach)
            {
                BuildForEach(topology, source, outgoing, primary, relations, virtualItems);
            }
            else
            {
                foreach (GraphEdgeDescriptor edge in outgoing)
                {
                    GraphPresentationRelationKind kind = edge.Kind == GraphEdgeKind.Child
                        ? GraphPresentationRelationKind.Structural
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

                return;
            }

            for (int index = firstOwnedRelation; index < relations.Count; index++)
            {
                relations[index].SetVisualOwner(source);
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
