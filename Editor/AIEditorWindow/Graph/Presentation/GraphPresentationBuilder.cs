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
                    new List<GraphFlowScope>(),
                    tree: null,
                    virtualItems: new List<GraphPresentationItem>());
            }

            Dictionary<UUID, GraphPresentationItem> primary = new();
            List<GraphFlowScope> completionScopes = new();
            foreach (GraphNodeDescriptor descriptor in topology.Nodes)
            {
                GraphPresentationItem item = GraphPresentationItem.CreateNode(GetKind(descriptor.Node), descriptor);
                item.LeafVisual = BuildLeafVisual(topology.Tree, descriptor);
                if (descriptor.Node is Capture capture
                    && (capture.result == null || !capture.result.HasEditorReference))
                {
                    item.AppendWarning("Capture has no result variable");
                }

                primary[descriptor.UUID] = item;
                if (descriptor.Node is Sequence)
                {
                    item.FlowScope = new GraphSequenceScope(item);
                    completionScopes.Add(item.FlowScope);
                }
                else if (descriptor.Node is Aggregate aggregate)
                {
                    item.FlowScope = new GraphAggregateScope(item, aggregate.resultMode);
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

            AttachPredicateSubtrees(topology, primary, embedded);
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

            HashSet<GraphPresentationItem> decoratorAnchors = decoratorStacks
                .Select(stack => stack.Anchor)
                .Where(anchor => anchor.DecoratorPlaceholder != null)
                .ToHashSet();
            roots.AddRange(virtualItems.Where(item => !decoratorAnchors.Contains(item)));

            GraphPresentationItem entrance = GraphPresentationItem.CreateBoundary(GraphPresentationKind.Entrance);
            GraphPresentationItem exit = GraphPresentationItem.CreateBoundary(GraphPresentationKind.Exit);
            if (topology.Tree?.GraphLayout?.HasEntrancePosition == true)
            {
                entrance.Position = topology.Tree.GraphLayout.EntrancePosition;
                entrance.HasExplicitPosition = true;
            }

            if (topology.Tree?.GraphLayout?.HasExitPosition == true)
            {
                exit.Position = topology.Tree.GraphLayout.ExitPosition;
                exit.HasExplicitPosition = true;
            }

            roots.Add(entrance);
            roots.Add(exit);

            BehaviourTreeData tree = topology.Tree;
            GraphPresentationItem head = tree != null && tree.headNodeUUID != UUID.Empty
                ? primary.GetValueOrDefault(tree.headNodeUUID)
                : null;
            if (head != null)
            {
                relations.Add(GraphPresentationRelation.CreateSynthetic(
                    entrance.Output,
                    head.Entry,
                    GraphPresentationRelationKind.Entrance,
                    GraphPresentationRelationRole.AuthoredTreeHead,
                    ""));
                relations.Add(GraphPresentationRelation.CreateSynthetic(
                    head.Completion,
                    exit.Entry,
                    GraphPresentationRelationKind.Exit,
                    GraphPresentationRelationRole.DerivedCompletion,
                    ""));
            }

            return new GraphPresentation(
                roots,
                primary,
                relations,
                completionScopes,
                serviceScopes,
                decoratorStacks,
                entrance,
                exit,
                topology.Tree,
                virtualItems);
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
                return new GraphLeafVisualDescriptor(
                    title,
                    $"{descriptor.DisplayName}\nBoolean · {full}",
                    GraphPresentationMetrics.BooleanNodeSize,
                    true,
                    null);
            }

            if (descriptor.Node is Constant constant)
            {
                string title = constant.returnValue ? "TRUE" : "FALSE";
                return new GraphLeafVisualDescriptor(
                    title,
                    $"{descriptor.DisplayName}\nConstant · {title}",
                    GraphPresentationMetrics.ConstantNodeSize,
                    false,
                    constant.returnValue);
            }

            return null;
        }

        /// <summary>Builds only unambiguous decorator chains; malformed or shared references remain independent cards.</summary>
        private static List<GraphDecoratorStack> BuildDecoratorStacks(IReadOnlyList<GraphPresentationRelation> relations)
        {
            Dictionary<GraphPresentationItem, GraphPresentationItem> next = new();
            Dictionary<GraphPresentationItem, int> incoming = new();
            HashSet<GraphPresentationItem> decoratorChildren = new();
            HashSet<GraphPresentationItem> ambiguousSources = new();
            foreach (GraphPresentationRelation relation in relations)
            {
                bool emptyDecoratorChild = relation.Target.Item?.DecoratorPlaceholder != null
                    && relation.Source.Item?.Node?.Node is Decorator;
                if (relation.Target.Item == null
                    || (!emptyDecoratorChild && (relation.AuthoredEdge == null
                        || relation.Role != GraphPresentationRelationRole.AuthoredReference)))
                {
                    continue;
                }

                if (relation.Kind is not (GraphPresentationRelationKind.Service or GraphPresentationRelationKind.Raw))
                {
                    incoming.TryGetValue(relation.Target.Item, out int count);
                    incoming[relation.Target.Item] = count + 1;
                }

                if ((!emptyDecoratorChild && relation.AuthoredEdge.Reference.Address.FieldName != nameof(Decorator.node))
                    || relation.Source.Item?.Node?.Node is not Decorator
                    || ambiguousSources.Contains(relation.Source.Item))
                {
                    continue;
                }

                if (!next.TryAdd(relation.Source.Item, relation.Target.Item))
                {
                    next.Remove(relation.Source.Item);
                    ambiguousSources.Add(relation.Source.Item);
                    continue;
                }

                decoratorChildren.Add(relation.Target.Item);
            }

            List<GraphDecoratorStack> result = new();
            foreach (GraphPresentationItem outer in next.Keys)
            {
                incoming.TryGetValue(outer, out int count);
                // Decorators are structured wrappers: their one external execution owner may be
                // a Sequence, Aggregate, Loop, or predicate scope. Only nested decorators are
                // skipped here because the outer decorator will render the complete stack.
                if (count > 1 || decoratorChildren.Contains(outer))
                {
                    continue;
                }

                List<GraphPresentationItem> badges = new();
                HashSet<GraphPresentationItem> visited = new();
                GraphPresentationItem current = outer;
                while (current?.Node?.Node is Decorator && next.TryGetValue(current, out GraphPresentationItem child)
                    && visited.Add(current) && (!incoming.TryGetValue(child, out int childIncoming) || childIncoming == 1))
                {
                    badges.Add(current);
                    current = child;
                }

                if (badges.Count == 0 || current == null
                    || (current.Node == null && current.DecoratorPlaceholder == null)
                    || current.Node?.Node is Decorator || !visited.Add(current))
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

        /// <summary>Derives the embedded predicate subtrees owned by Conditions and predicate-based Loops.</summary>
        private static void AttachPredicateSubtrees(
            GraphTopology topology,
            IReadOnlyDictionary<UUID, GraphPresentationItem> primary,
            ISet<UUID> embedded)
        {
            Dictionary<UUID, IGraphPredicateScope> ownership = new();

            // Establish every direct predicate root before deriving nested ownership. This keeps
            // the result independent of serialized node order.
            foreach (GraphNodeDescriptor descriptor in topology.Nodes)
            {
                if (!TryGetPredicateScope(descriptor.Node, primary, descriptor.UUID, out IGraphPredicateScope scope))
                {
                    continue;
                }

                GraphPresentationItem predicate = GetPredicateRootCandidate(scope);
                if (predicate?.Node == null)
                {
                    continue;
                }

                scope.SetPredicateRoot(predicate);
            }

            foreach (GraphNodeDescriptor descriptor in topology.Nodes)
            {
                if (!TryGetPredicateScope(descriptor.Node, primary, descriptor.UUID, out IGraphPredicateScope scope))
                {
                    continue;
                }

                GraphPresentationItem predicate = scope.PredicateRoot;
                if (predicate?.Node == null)
                {
                    continue;
                }

                CollectConditionPredicate(
                    topology,
                    primary,
                    scope,
                    predicate,
                    embedded,
                    ownership,
                    new HashSet<UUID>());
                foreach (GraphPresentationItem member in scope.PredicateMembers)
                {
                    if (member.Parent == null || ReferenceEquals(member.Parent, scope.Owner))
                    {
                        scope.AddPredicateVisualRoot(member);
                    }
                }
            }
        }

        /// <summary>Gets the authored predicate root from the owning scope's canonical storage.</summary>
        private static GraphPresentationItem GetPredicateRootCandidate(IGraphPredicateScope scope)
        {
            if (scope is GraphLoopScope loopScope)
            {
                return loopScope.Condition;
            }

            return scope?.Owner?.Slots.Count > 0 ? scope.Owner.Slots[0].Content : null;
        }

        /// <summary>Resolves the presentation scope that owns one authored predicate reference.</summary>
        private static bool TryGetPredicateScope(
            TreeNode node,
            IReadOnlyDictionary<UUID, GraphPresentationItem> primary,
            UUID uuid,
            out IGraphPredicateScope scope)
        {
            scope = null;
            if (!primary.TryGetValue(uuid, out GraphPresentationItem owner))
            {
                return false;
            }

            scope = node switch
            {
                Condition => owner.ConditionScope,
                Loop loop when loop.loopType is Loop.LoopType.@while or Loop.LoopType.doWhile => owner.LoopScope,
                _ => null,
            };
            return scope != null;
        }

        /// <summary>Collects valid structural descendants while enforcing unique predicate ownership.</summary>
        private static void CollectConditionPredicate(
            GraphTopology topology,
            IReadOnlyDictionary<UUID, GraphPresentationItem> primary,
            IGraphPredicateScope scope,
            GraphPresentationItem current,
            ISet<UUID> embedded,
            IDictionary<UUID, IGraphPredicateScope> ownership,
            ISet<UUID> path)
        {
            if (current.Kind == GraphPresentationKind.ReferenceProxy)
            {
                scope.AddPredicateMember(current);
                scope.Owner.AppendWarning(current.Warning);
                return;
            }

            if (!path.Add(current.TargetUUID))
            {
                scope.Owner.AppendWarning($"Predicate cycle detected at {current.Node?.DisplayName ?? current.TargetUUID.ToString()}");
                return;
            }

            if (ownership.TryGetValue(current.TargetUUID, out IGraphPredicateScope existingOwner)
                && !ReferenceEquals(existingOwner, scope))
            {
                scope.Owner.AppendWarning($"Predicate node {current.Node?.DisplayName ?? current.TargetUUID.ToString()} is shared by multiple Conditions");
                path.Remove(current.TargetUUID);
                return;
            }

            ownership[current.TargetUUID] = scope;
            scope.AddPredicateMember(current);
            embedded.Add(current.TargetUUID);

            if (current.Node?.Node is Condition && !ReferenceEquals(current, scope.Owner))
            {
                if (scope is GraphConditionScope conditionScope)
                {
                    conditionScope.AddNestedPredicateScope(current.ConditionScope);
                }
                path.Remove(current.TargetUUID);
                return;
            }

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

                CollectConditionPredicate(topology, primary, scope, child, embedded, ownership, path);
            }

            path.Remove(current.TargetUUID);
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
                BuildSequence(source, outgoing, primary, relations, virtualItems);
            }
            else if (source.Node.Node is Aggregate)
            {
                BuildAggregate(source, outgoing, primary, relations, virtualItems);
            }
            else if (source.Node.Node is Loop)
            {
                BuildLoop(topology, source, outgoing, primary, relations, virtualItems);
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
                if (source.Node.Node is Decorator
                    && outgoing.All(edge => edge.Reference.Address.FieldName != nameof(Decorator.node)))
                {
                    GraphPresentationItem placeholder = GraphPresentationItem.CreateDecoratorPlaceholder(
                        new GraphDecoratorPlaceholder(source.TargetUUID));
                    placeholder.Position = source.Position;
                    virtualItems.Add(placeholder);
                    relations.Add(GraphPresentationRelation.CreateSynthetic(
                        source.Output, placeholder.Entry, GraphPresentationRelationKind.Structural,
                        GraphPresentationRelationRole.PlaceholderHint, "Child"));
                }

                foreach (GraphEdgeDescriptor edge in outgoing)
                {
                    GraphPresentationRelationKind kind = edge.Kind == GraphEdgeKind.Child
                        ? GraphPresentationRelationKind.Structural
                        : ConvertTopologyKind(edge.Kind);
                    string label = edge.Kind == GraphEdgeKind.Child ? BuildBranchLabel(edge, kind) : edge.Label;
                    if (source.Node.Node is Decorator
                        && edge.Reference.Address.FieldName == nameof(Decorator.node)
                        && edge.Target == null)
                    {
                        GraphPresentationItem placeholder = GraphPresentationItem.CreateDecoratorPlaceholder(
                            new GraphDecoratorPlaceholder(source.TargetUUID));
                        placeholder.Position = source.Position;
                        virtualItems.Add(placeholder);
                        relations.Add(GraphPresentationRelation.CreateFromEdge(
                            source.Output, placeholder.Entry, GraphPresentationRelationKind.Structural,
                            GraphPresentationRelationRole.PlaceholderHint, label, edge));
                    }
                    else if (edge.Kind == GraphEdgeKind.Service && edge.Target == null)
                    {
                        GraphPresentationItem placeholder = GraphPresentationItem.CreateServicePlaceholder(
                            new GraphServicePlaceholder(source, label, edge.Reference.TargetUUID));
                        virtualItems.Add(placeholder);
                        relations.Add(GraphPresentationRelation.CreateFromEdge(
                            source.Output,
                            placeholder.Entry,
                            GraphPresentationRelationKind.Service,
                            GraphPresentationRelationRole.PlaceholderHint,
                            label,
                            edge));
                    }
                    else
                    {
                        relations.Add(CreateTopologyRelation(source.Output, edge, primary, kind, label, virtualItems));
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
                if (edge.Reference.Address.FieldName == fieldName
                    && edge.Reference.Address.Index == collectionIndex)
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
            string label,
            ICollection<GraphPresentationItem> virtualItems = null)
        {
            GraphPresentationItem targetItem = edge.Target != null && primary.TryGetValue(edge.Target.UUID, out GraphPresentationItem item)
                ? item
                : null;
            if (targetItem == null && virtualItems != null && edge.Kind != GraphEdgeKind.Service)
            {
                targetItem = virtualItems.FirstOrDefault(candidate =>
                    candidate.InvalidReference?.Edge == edge);
                if (targetItem == null)
                {
                    targetItem = GraphPresentationItem.CreateInvalidReference(
                        new GraphInvalidReferenceDescriptor(edge));
                    virtualItems.Add(targetItem);
                }
            }

            GraphPresentationEndpoint target = targetItem?.Entry ?? default;
            return GraphPresentationRelation.CreateFromEdge(
                source,
                target,
                kind,
                GraphPresentationRelationRole.AuthoredReference,
                label,
                edge);
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
