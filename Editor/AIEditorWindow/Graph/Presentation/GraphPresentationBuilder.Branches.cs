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
        private static void BuildDecision(
            GraphPresentationItem source,
            IReadOnlyList<GraphEdgeDescriptor> outgoing,
            IReadOnlyDictionary<UUID, GraphPresentationItem> primary,
            ICollection<GraphPresentationRelation> relations,
            ICollection<GraphPresentationItem> virtualItems)
        {
            foreach (GraphEdgeDescriptor edge in outgoing)
            {
                if (edge.FieldName == "events" && edge.CollectionIndex >= 0)
                {
                    continue;
                }

                relations.Add(CreateTopologyRelation(
                    source.Output,
                    edge,
                    primary,
                    ConvertTopologyKind(edge.Kind),
                    edge.Label,
                    virtualItems));
            }

            Decision decision = (Decision)source.Node.Node;
            NodeReference[] references = decision.events ?? Array.Empty<NodeReference>();
            if (references.Length == 0)
            {
                AddNoOptionsDecisionPlaceholder(source, relations, virtualItems);
                return;
            }

            List<GraphDecisionOption> options = new(references.Length);
            HashSet<UUID> seen = new();
            for (int index = 0; index < references.Length; index++)
            {
                NodeReference reference = references[index];
                GraphEdgeDescriptor edge = FindEdge(outgoing, "events", index);
                GraphPresentationItem target = ResolveDecisionTarget(reference, edge, index, primary, virtualItems);
                GraphDecisionOption option = new(index, target, edge);
                options.Add(option);
                source.DecisionScope.AddOption(option);

                GraphPresentationRelationRole role = target.DecisionPlaceholder != null
                    ? GraphPresentationRelationRole.PlaceholderHint
                    : GraphPresentationRelationRole.AuthoredReference;
                relations.Add(new GraphPresentationRelation(
                    source.Output,
                    target.Entry,
                    GraphPresentationRelationKind.DecisionBranch,
                    role,
                    string.Empty,
                    edge,
                    target.TargetUUID,
                    target.DecisionPlaceholder?.Kind == GraphDecisionPlaceholderKind.MissingOption,
                    edge?.OccurrenceId ?? -200 - index));

                if (target.DecisionPlaceholder?.Kind == GraphDecisionPlaceholderKind.EmptyOption)
                {
                    AppendWarning(source.Node, $"Empty Decision option (events [{index}])");
                }

                if (target.Node != null && !seen.Add(target.TargetUUID))
                {
                    AppendWarning(source.Node, $"Repeated Decision target {target.TargetUUID} (events [{index}])");
                }
            }

            for (int index = 0; index < options.Count; index++)
            {
                GraphDecisionOption option = options[index];
                GraphPresentationItem target = option.Item;
                if (target.DecisionPlaceholder?.IsError == true || ReferenceEquals(target, source))
                {
                    continue;
                }

                bool isLast = index == options.Count - 1;
                relations.Add(new GraphPresentationRelation(
                    target.Completion,
                    source.FlowComplete,
                    GraphPresentationRelationKind.DecisionSuccess,
                    GraphPresentationRelationRole.DerivedCompletion,
                    isLast ? "Complete" : "Success",
                    option.Edge,
                    source.TargetUUID,
                    false,
                    option.Edge?.OccurrenceId ?? -200 - index));

                if (isLast)
                {
                    continue;
                }

                GraphDecisionOption next = options[index + 1];
                relations.Add(new GraphPresentationRelation(
                    target.Completion,
                    next.Item.Entry,
                    GraphPresentationRelationKind.DecisionFailure,
                    GraphPresentationRelationRole.DerivedControl,
                    "Failed",
                    next.Edge,
                    next.Item.TargetUUID,
                    next.Item.DecisionPlaceholder?.Kind == GraphDecisionPlaceholderKind.MissingOption,
                    next.Edge?.OccurrenceId ?? -200 - next.Index,
                    contextualOwner: source));
            }
        }

        /// <summary>Adds the normal Failed completion used by an empty Decision list.</summary>
        private static void AddNoOptionsDecisionPlaceholder(
            GraphPresentationItem source,
            ICollection<GraphPresentationRelation> relations,
            ICollection<GraphPresentationItem> virtualItems)
        {
            GraphDecisionPlaceholder descriptor = new(
                GraphDecisionPlaceholderKind.NoOptions,
                -1,
                UUID.Empty);
            GraphPresentationItem placeholder = GraphPresentationItem.CreateDecisionPlaceholder(descriptor);
            virtualItems.Add(placeholder);
            source.DecisionScope.AddOption(new GraphDecisionOption(-1, placeholder, null));
            relations.Add(new GraphPresentationRelation(
                source.Output,
                placeholder.Entry,
                GraphPresentationRelationKind.DecisionBranch,
                GraphPresentationRelationRole.PlaceholderHint,
                string.Empty,
                null,
                UUID.Empty,
                false,
                -200));
            relations.Add(new GraphPresentationRelation(
                placeholder.Output,
                source.FlowComplete,
                GraphPresentationRelationKind.DecisionSuccess,
                GraphPresentationRelationRole.DerivedCompletion,
                "Returns Failed",
                null,
                source.TargetUUID,
                false,
                -200));
        }

        /// <summary>Resolves one Decision occurrence to a real node or an explicit Error placeholder.</summary>
        private static GraphPresentationItem ResolveDecisionTarget(
            NodeReference reference,
            GraphEdgeDescriptor edge,
            int index,
            IReadOnlyDictionary<UUID, GraphPresentationItem> primary,
            ICollection<GraphPresentationItem> virtualItems)
        {
            if (reference != null && reference.UUID != UUID.Empty
                && primary.TryGetValue(reference.UUID, out GraphPresentationItem target))
            {
                return target;
            }

            bool missing = reference != null && reference.UUID != UUID.Empty;
            GraphDecisionPlaceholder descriptor = new(
                missing ? GraphDecisionPlaceholderKind.MissingOption : GraphDecisionPlaceholderKind.EmptyOption,
                index,
                missing ? reference.UUID : UUID.Empty);
            GraphPresentationItem placeholder = GraphPresentationItem.CreateDecisionPlaceholder(descriptor);
            virtualItems.Add(placeholder);
            return placeholder;
        }

        /// <summary>Builds weighted candidate relations and one shared completion for the Probability family.</summary>
        private static void BuildProbability(
            GraphTopology topology,
            GraphPresentationItem source,
            IReadOnlyList<GraphEdgeDescriptor> outgoing,
            IReadOnlyDictionary<UUID, GraphPresentationItem> primary,
            ICollection<GraphPresentationRelation> relations,
            ICollection<GraphPresentationItem> virtualItems)
        {
            foreach (GraphEdgeDescriptor edge in outgoing)
            {
                if (edge.FieldName == "events" && edge.CollectionIndex >= 0)
                {
                    continue;
                }

                relations.Add(CreateTopologyRelation(
                    source.Output,
                    edge,
                    primary,
                    ConvertTopologyKind(edge.Kind),
                    edge.Label,
                    virtualItems));
            }

            List<(NodeReference Reference, GraphProbabilityWeightDescriptor Weight)> authored = new();
            if (source.Node.Node is Probability probability)
            {
                Probability.EventWeight[] options = probability.events ?? Array.Empty<Probability.EventWeight>();
                for (int index = 0; index < options.Length; index++)
                {
                    authored.Add((options[index]?.reference, GraphProbabilityWeightDescriptor.Create(index, options[index])));
                }
            }
            else
            {
                PseudoProbability pseudo = (PseudoProbability)source.Node.Node;
                PseudoProbability.EventWeight[] options = pseudo.events ?? Array.Empty<PseudoProbability.EventWeight>();
                for (int index = 0; index < options.Length; index++)
                {
                    GraphProbabilityWeightDescriptor weight = GraphProbabilityWeightDescriptor.Create(
                        topology.Tree,
                        index,
                        options[index]);
                    authored.Add((options[index]?.reference, weight));
                    if (weight.IsMissingVariable)
                    {
                        AppendWarning(source.Node, $"Missing weight variable {weight.VariableUUID} (events [{index}])");
                    }
                }
            }

            if (authored.Count == 0)
            {
                AddNoOptionsProbabilityPlaceholder(source, relations, virtualItems);
                return;
            }

            bool allConstant = true;
            long totalWeight = 0;
            foreach ((NodeReference _, GraphProbabilityWeightDescriptor weight) in authored)
            {
                allConstant &= !weight.IsDynamic;
                totalWeight += weight.ConstantWeight;
            }

            bool uniformFallback = allConstant && totalWeight <= 0;
            foreach ((NodeReference reference, GraphProbabilityWeightDescriptor weight) in authored)
            {
                bool eligible = !allConstant || uniformFallback || weight.ConstantWeight > 0;
                string label = BuildProbabilityLabel(weight, allConstant, uniformFallback, totalWeight);
                GraphEdgeDescriptor edge = FindEdge(outgoing, "events", weight.Index);
                GraphPresentationItem target = ResolveProbabilityTarget(
                    reference,
                    edge,
                    weight.Index,
                    primary,
                    virtualItems);
                GraphProbabilityOption option = new(weight, target, edge)
                {
                    IsEligible = eligible,
                    Label = label,
                };
                source.ProbabilityScope.AddOption(option);

                bool invalid = target.ProbabilityPlaceholder?.IsInvalidSelection == true;
                GraphPresentationRelationRole role = invalid
                    ? GraphPresentationRelationRole.PlaceholderHint
                    : GraphPresentationRelationRole.AuthoredReference;
                relations.Add(new GraphPresentationRelation(
                    source.Output,
                    target.Entry,
                    GraphPresentationRelationKind.ProbabilityBranch,
                    role,
                    label,
                    edge,
                    target.TargetUUID,
                    target.ProbabilityPlaceholder?.Kind == GraphProbabilityPlaceholderKind.MissingOption,
                    edge?.OccurrenceId ?? -100 - weight.Index,
                    isVisuallyDisabled: !eligible));

                if (!eligible || invalid || target.Completion == source.FlowComplete)
                {
                    continue;
                }

                relations.Add(new GraphPresentationRelation(
                    target.Completion,
                    source.FlowComplete,
                    GraphPresentationRelationKind.FlowComplete,
                    GraphPresentationRelationRole.DerivedCompletion,
                    string.Empty,
                    edge,
                    target.TargetUUID,
                    false,
                    edge?.OccurrenceId ?? -100 - weight.Index));
            }
        }

        /// <summary>Adds the runtime Failed path used when no Probability candidates exist.</summary>
        private static void AddNoOptionsProbabilityPlaceholder(
            GraphPresentationItem source,
            ICollection<GraphPresentationRelation> relations,
            ICollection<GraphPresentationItem> virtualItems)
        {
            GraphProbabilityPlaceholder descriptor = new(
                GraphProbabilityPlaceholderKind.NoOptions,
                -1,
                UUID.Empty);
            GraphPresentationItem placeholder = GraphPresentationItem.CreateProbabilityPlaceholder(descriptor);
            virtualItems.Add(placeholder);
            source.ProbabilityScope.AddOption(new GraphProbabilityOption(
                GraphProbabilityWeightDescriptor.Create(-1, null),
                placeholder,
                null)
            {
                IsEligible = true,
                Label = "No options",
            });
            relations.Add(new GraphPresentationRelation(
                source.Output,
                placeholder.Entry,
                GraphPresentationRelationKind.ProbabilityBranch,
                GraphPresentationRelationRole.PlaceholderHint,
                "No options",
                null,
                UUID.Empty,
                false,
                -100));
            relations.Add(new GraphPresentationRelation(
                placeholder.Output,
                source.FlowComplete,
                GraphPresentationRelationKind.FlowComplete,
                GraphPresentationRelationRole.DerivedCompletion,
                "Returns Failed",
                null,
                source.TargetUUID,
                false,
                -100));
        }

        /// <summary>Resolves one candidate to a real node or an explicit invalid-selection placeholder.</summary>
        private static GraphPresentationItem ResolveProbabilityTarget(
            NodeReference reference,
            GraphEdgeDescriptor edge,
            int index,
            IReadOnlyDictionary<UUID, GraphPresentationItem> primary,
            ICollection<GraphPresentationItem> virtualItems)
        {
            if (reference != null && reference.UUID != UUID.Empty
                && primary.TryGetValue(reference.UUID, out GraphPresentationItem target))
            {
                return target;
            }

            bool missing = reference != null && reference.UUID != UUID.Empty;
            GraphProbabilityPlaceholder descriptor = new(
                missing ? GraphProbabilityPlaceholderKind.MissingOption : GraphProbabilityPlaceholderKind.EmptyOption,
                index,
                missing ? reference.UUID : UUID.Empty);
            GraphPresentationItem placeholder = GraphPresentationItem.CreateProbabilityPlaceholder(descriptor);
            virtualItems.Add(placeholder);
            return placeholder;
        }

        /// <summary>Builds a runtime-consistent option label without parsing topology display text.</summary>
        private static string BuildProbabilityLabel(
            GraphProbabilityWeightDescriptor weight,
            bool allConstant,
            bool uniformFallback,
            long totalWeight)
        {
            string prefix = $"Option {weight.Index + 1}";
            if (weight.IsDynamic)
            {
                return $"{prefix} · Weight · {weight.VariableName}";
            }

            if (!allConstant)
            {
                return $"{prefix} · Weight {weight.ConstantWeight}";
            }

            if (uniformFallback)
            {
                return $"{prefix} · Uniform fallback";
            }

            float percent = totalWeight > 0 ? weight.ConstantWeight * 100f / totalWeight : 0f;
            string formatted = percent.ToString("0.#", CultureInfo.InvariantCulture);
            return weight.ConstantWeight == 0
                ? $"{prefix} · Weight 0 · 0% · Disabled"
                : $"{prefix} · Weight {weight.ConstantWeight} · {formatted}%";
        }

        /// <summary>Appends one presentation warning without replacing topology diagnostics.</summary>
        private static void AppendWarning(GraphNodeDescriptor node, string warning)
        {
            node.Warning = string.IsNullOrEmpty(node.Warning) ? warning : node.Warning + ", " + warning;
        }

        private static void BuildCondition(
            GraphPresentationItem source,
            IReadOnlyList<GraphEdgeDescriptor> outgoing,
            IReadOnlyDictionary<UUID, GraphPresentationItem> primary,
            ISet<UUID> embedded,
            ICollection<GraphPresentationRelation> relations,
            ICollection<GraphPresentationItem> virtualItems)
        {
            Condition condition = (Condition)source.Node.Node;
            foreach (GraphEdgeDescriptor edge in outgoing)
            {
                if (edge.Label == "condition")
                {
                    if (edge.Target != null)
                    {
                        GraphPresentationItem target = primary[edge.Target.UUID];
                        bool requiresProxy = edge.Target.UUID == source.Node.UUID
                            || embedded.Contains(edge.Target.UUID)
                            || WouldCreateParentCycle(source, target);
                        GraphPresentationItem content = requiresProxy
                            ? GraphPresentationItem.CreateReferenceProxy(
                                edge.Target,
                                edge.Target.UUID == source.Node.UUID || WouldCreateParentCycle(source, target)
                                    ? "Predicate cycle"
                                    : "Predicate is owned by another Condition")
                            : target;
                        source.AddSlot(new GraphPresentationSlot("Condition", -1, edge, content));
                        if (!requiresProxy)
                        {
                            embedded.Add(edge.Target.UUID);
                        }
                    }

                    continue;
                }

                if (edge.Label is "trueNode" or "falseNode")
                {
                    continue;
                }

                relations.Add(CreateTopologyRelation(
                    source.Output,
                    edge,
                    primary,
                    ConvertTopologyKind(edge.Kind),
                    edge.Label,
                    virtualItems));
            }

            BuildConditionBranch(
                source,
                GraphConditionBranch.True,
                condition.trueNode,
                FindEdge(outgoing, "trueNode"),
                primary,
                relations,
                virtualItems);
            BuildConditionBranch(
                source,
                GraphConditionBranch.False,
                condition.falseNode,
                FindEdge(outgoing, "falseNode"),
                primary,
                relations,
                virtualItems);
        }

        /// <summary>Returns whether embedding the target below the source would create a parent cycle.</summary>
        private static bool WouldCreateParentCycle(GraphPresentationItem source, GraphPresentationItem target)
        {
            for (GraphPresentationItem current = source; current != null; current = current.Parent)
            {
                if (ReferenceEquals(current, target))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Builds one authored or placeholder Condition branch and its derived completion.</summary>
        private static void BuildConditionBranch(
            GraphPresentationItem source,
            GraphConditionBranch branch,
            NodeReference reference,
            GraphEdgeDescriptor edge,
            IReadOnlyDictionary<UUID, GraphPresentationItem> primary,
            ICollection<GraphPresentationRelation> relations,
            ICollection<GraphPresentationItem> virtualItems)
        {
            GraphPresentationRelationKind kind = branch == GraphConditionBranch.True
                ? GraphPresentationRelationKind.ConditionTrue
                : GraphPresentationRelationKind.ConditionFalse;
            string label = branch == GraphConditionBranch.True ? "True" : "False";
            GraphPresentationItem target = edge?.Target != null && primary.TryGetValue(edge.Target.UUID, out GraphPresentationItem item)
                ? item
                : null;
            GraphPresentationRelation authored;
            if (target != null)
            {
                authored = CreateTopologyRelation(source.Output, edge, primary, kind, label);
            }
            else
            {
                bool isMissing = reference != null && reference.UUID != UUID.Empty;
                UUID targetUUID = isMissing ? reference.UUID : UUID.Empty;
                GraphConditionPlaceholder descriptor = new(branch, targetUUID);
                target = GraphPresentationItem.CreateConditionPlaceholder(descriptor);
                virtualItems.Add(target);
                authored = new GraphPresentationRelation(
                    source.Output,
                    target.Entry,
                    kind,
                    GraphPresentationRelationRole.PlaceholderHint,
                    label,
                    edge,
                    targetUUID,
                    isMissing,
                    edge?.OccurrenceId ?? (branch == GraphConditionBranch.True ? -2 : -3));
            }

            relations.Add(authored);
            source.ConditionScope.SetBranch(branch, target);
            if (target.Completion == source.FlowComplete)
            {
                return;
            }

            relations.Add(new GraphPresentationRelation(
                target.Completion,
                source.FlowComplete,
                GraphPresentationRelationKind.FlowComplete,
                GraphPresentationRelationRole.DerivedCompletion,
                string.Empty,
                edge,
                authored.TargetUUID,
                authored.IsMissingTarget,
                authored.OccurrenceId));
        }

        /// <summary>Finds one exact authored field edge in accessor declaration order.</summary>
}
}
