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
        private static void BuildSequence(
            GraphPresentationItem source,
            IReadOnlyList<GraphEdgeDescriptor> outgoing,
            IReadOnlyDictionary<UUID, GraphPresentationItem> primary,
            ICollection<GraphPresentationRelation> relations)
        {
            GraphPresentationEndpoint previousCompletion = source.Output;
            int childIndex = 0;
            foreach (GraphEdgeDescriptor edge in outgoing)
            {
                if (edge.Kind != GraphEdgeKind.Child)
                {
                    relations.Add(CreateTopologyRelation(source.Output, edge, primary, ConvertTopologyKind(edge.Kind), edge.Label));
                    continue;
                }

                GraphPresentationRelationKind kind = childIndex == 0
                    ? GraphPresentationRelationKind.SequenceStart
                    : GraphPresentationRelationKind.SequenceNext;
                string label = childIndex == 0 ? "Start" : "Next";
                GraphPresentationRelation relation = CreateTopologyRelation(previousCompletion, edge, primary, kind, label);
                relations.Add(relation);
                childIndex++;
                if (!relation.Target.IsValid)
                {
                    continue;
                }

                GraphPresentationItem member = relation.Target.Item;
                source.SequenceScope.AddMember(member);
                relations.Add(new GraphPresentationRelation(
                    member.Completion,
                    source.FlowComplete,
                    GraphPresentationRelationKind.SequenceFailure,
                    GraphPresentationRelationRole.DerivedCompletion,
                    "False · Failed",
                    edge,
                    source.TargetUUID,
                    false,
                    edge.OccurrenceId,
                    contextualTrigger: member));
                previousCompletion = member.Completion;
            }

            relations.Add(new GraphPresentationRelation(
                previousCompletion,
                source.FlowComplete,
                GraphPresentationRelationKind.SequenceSuccess,
                GraphPresentationRelationRole.DerivedCompletion,
                childIndex == 0 ? "Returns Success" : "Complete",
                null,
                source.TargetUUID,
                false,
                -1));
        }

        /// <summary>Builds an unconditional ordered chain whose child results are aggregated after all execute.</summary>
        private static void BuildAggregate(
            GraphPresentationItem source,
            IReadOnlyList<GraphEdgeDescriptor> outgoing,
            IReadOnlyDictionary<UUID, GraphPresentationItem> primary,
            ICollection<GraphPresentationRelation> relations)
        {
            GraphPresentationEndpoint previousCompletion = source.Output;
            int childIndex = 0;
            foreach (GraphEdgeDescriptor edge in outgoing)
            {
                if (edge.Kind != GraphEdgeKind.Child)
                {
                    relations.Add(CreateTopologyRelation(source.Output, edge, primary, ConvertTopologyKind(edge.Kind), edge.Label));
                    continue;
                }

                GraphPresentationRelationKind kind = childIndex == 0
                    ? GraphPresentationRelationKind.AggregateStart
                    : GraphPresentationRelationKind.AggregateNext;
                string label = childIndex == 0 ? "Start" : "Next";
                GraphPresentationRelation relation = CreateTopologyRelation(previousCompletion, edge, primary, kind, label);
                relations.Add(relation);
                childIndex++;
                if (!relation.Target.IsValid)
                {
                    continue;
                }

                GraphPresentationItem member = relation.Target.Item;
                source.AggregateScope.AddMember(member);
                previousCompletion = member.Completion;
            }

            string completionLabel = source.AggregateScope.ResultMode switch
            {
                Aggregate.ResultMode.All => childIndex == 0 ? "Returns Success" : "All",
                Aggregate.ResultMode.Any => childIndex == 0 ? "Returns Failed" : "Any",
                Aggregate.ResultMode.True => "Returns True",
                Aggregate.ResultMode.False => "Returns False",
                _ => string.Empty,
            };
            relations.Add(new GraphPresentationRelation(
                previousCompletion,
                source.FlowComplete,
                GraphPresentationRelationKind.AggregateComplete,
                GraphPresentationRelationRole.DerivedCompletion,
                completionLabel,
                null,
                source.TargetUUID,
                false,
                -1));
        }

        /// <summary>Builds mode-specific Loop condition, body, repeat, and exit relations.</summary>
        private static void BuildLoop(
            GraphTopology topology,
            GraphPresentationItem source,
            IReadOnlyList<GraphEdgeDescriptor> outgoing,
            IReadOnlyDictionary<UUID, GraphPresentationItem> primary,
            ICollection<GraphPresentationRelation> relations,
            ICollection<GraphPresentationItem> virtualItems)
        {
            Loop loop = (Loop)source.Node.Node;
            GraphLoopScope scope = source.LoopScope;
            foreach (GraphEdgeDescriptor edge in outgoing)
            {
                if (edge.Kind != GraphEdgeKind.Child)
                {
                    relations.Add(CreateTopologyRelation(
                        source.Output,
                        edge,
                        primary,
                        ConvertTopologyKind(edge.Kind),
                        edge.Label));
                }
            }

            GraphPresentationItem condition;
            GraphEdgeDescriptor conditionEdge = FindEdge(outgoing, "condition");
            if (loop.loopType == Loop.LoopType.@for)
            {
                condition = GraphPresentationItem.CreateLoopJunction(
                    new GraphLoopJunction(GraphLoopJunctionKind.CountCheck, FormatLoopCount(topology?.Tree, loop)));
                virtualItems.Add(condition);
            }
            else
            {
                condition = ResolveLoopTarget(
                    loop.condition,
                    conditionEdge,
                    GraphLoopPart.Condition,
                    -1,
                    primary,
                    virtualItems);
            }

            scope.SetCondition(condition);

            List<(GraphPresentationItem Item, GraphEdgeDescriptor Edge)> body = new();
            NodeReference[] bodyReferences = loop.events ?? Array.Empty<NodeReference>();
            for (int index = 0; index < bodyReferences.Length; index++)
            {
                GraphEdgeDescriptor edge = FindEdge(outgoing, $"events [{index}]");
                GraphPresentationItem item = ResolveLoopTarget(
                    bodyReferences[index],
                    edge,
                    GraphLoopPart.Body,
                    index,
                    primary,
                    virtualItems);
                body.Add((item, edge));
                scope.AddBody(item);
            }

            if (body.Count == 0)
            {
                GraphPresentationItem emptyBody = ResolveLoopTarget(
                    null,
                    null,
                    GraphLoopPart.Body,
                    -1,
                    primary,
                    virtualItems);
                body.Add((emptyBody, null));
                scope.AddBody(emptyBody);
            }

            if (loop.loopType == Loop.LoopType.doWhile)
            {
                GraphPresentationEndpoint bodyCompletion = BuildLoopBody(
                    source.Output,
                    body,
                    primary,
                    relations,
                    firstLabel: "Body 1");
                relations.Add(CreateLoopTargetRelation(
                    bodyCompletion,
                    condition,
                    conditionEdge,
                    primary,
                    GraphPresentationRelationKind.LoopCondition,
                    "Condition"));
                AddDerivedLoopRelation(
                    relations,
                    condition.Completion,
                    body[0].Item.Entry,
                    GraphPresentationRelationKind.LoopRepeat,
                    GraphPresentationRelationRole.DerivedControl,
                    "True · Repeat",
                    source.TargetUUID);
                AddDerivedLoopRelation(
                    relations,
                    condition.Completion,
                    source.FlowComplete,
                    GraphPresentationRelationKind.LoopExit,
                    GraphPresentationRelationRole.DerivedCompletion,
                    "False · Exit",
                    source.TargetUUID);
                return;
            }

            if (loop.loopType == Loop.LoopType.@for)
            {
                AddDerivedLoopRelation(
                    relations,
                    source.Output,
                    condition.Entry,
                    GraphPresentationRelationKind.LoopCondition,
                    GraphPresentationRelationRole.DerivedControl,
                    "Count",
                    source.TargetUUID);
            }
            else
            {
                relations.Add(CreateLoopTargetRelation(
                    source.Output,
                    condition,
                    conditionEdge,
                    primary,
                    GraphPresentationRelationKind.LoopCondition,
                    "Condition"));
            }

            string bodyLabel = loop.loopType == Loop.LoopType.@for ? "Continue" : "True · Body 1";
            GraphPresentationEndpoint completion = BuildLoopBody(
                loop.loopType == Loop.LoopType.@for ? condition.Output : condition.Completion,
                body,
                primary,
                relations,
                bodyLabel);
            AddDerivedLoopRelation(
                relations,
                completion,
                condition.Entry,
                GraphPresentationRelationKind.LoopRepeat,
                GraphPresentationRelationRole.DerivedControl,
                loop.loopType == Loop.LoopType.@for ? "Next" : "Repeat",
                source.TargetUUID);
            AddDerivedLoopRelation(
                relations,
                loop.loopType == Loop.LoopType.@for ? condition.Output : condition.Completion,
                source.FlowComplete,
                GraphPresentationRelationKind.LoopExit,
                GraphPresentationRelationRole.DerivedCompletion,
                loop.loopType == Loop.LoopType.@for ? "Exhausted" : "False · Exit",
                source.TargetUUID);
        }

        /// <summary>Formats the presentation-only For count without changing the authored variable field.</summary>
        private static string FormatLoopCount(BehaviourTreeData tree, Loop loop)
        {
            if (loop?.loopCount == null || !loop.loopCount.HasEditorReference)
            {
                return (loop?.loopCount?.ConstantIntValue ?? 0).ToString(CultureInfo.InvariantCulture);
            }

            string variableName = tree?.GetVariableDescName(loop.loopCount.UUID);
            return "$" + (string.IsNullOrEmpty(variableName)
                ? VariableData.MISSING_VARIABLE_NAME
                : variableName);
        }

        /// <summary>Builds the ordered body chain and returns its final completion endpoint.</summary>
        private static GraphPresentationEndpoint BuildLoopBody(
            GraphPresentationEndpoint start,
            IReadOnlyList<(GraphPresentationItem Item, GraphEdgeDescriptor Edge)> body,
            IReadOnlyDictionary<UUID, GraphPresentationItem> primary,
            ICollection<GraphPresentationRelation> relations,
            string firstLabel)
        {
            GraphPresentationEndpoint previous = start;
            for (int index = 0; index < body.Count; index++)
            {
                (GraphPresentationItem item, GraphEdgeDescriptor edge) = body[index];
                string label = index == 0 ? firstLabel : $"Body {index + 1}";
                relations.Add(CreateLoopTargetRelation(
                    previous,
                    item,
                    edge,
                    primary,
                    GraphPresentationRelationKind.LoopBody,
                    label));
                previous = item.Completion;
            }

            return previous;
        }

        /// <summary>Resolves one real or presentation-only Loop target.</summary>
        private static GraphPresentationItem ResolveLoopTarget(
            NodeReference reference,
            GraphEdgeDescriptor edge,
            GraphLoopPart part,
            int index,
            IReadOnlyDictionary<UUID, GraphPresentationItem> primary,
            ICollection<GraphPresentationItem> virtualItems)
        {
            if (edge?.Target != null && primary.TryGetValue(edge.Target.UUID, out GraphPresentationItem item))
            {
                return item;
            }

            UUID missingUUID = reference != null && reference.UUID != UUID.Empty ? reference.UUID : UUID.Empty;
            GraphPresentationItem placeholder = GraphPresentationItem.CreateLoopPlaceholder(
                new GraphLoopPlaceholder(part, index, missingUUID));
            virtualItems.Add(placeholder);
            return placeholder;
        }

        /// <summary>Creates an authored Loop relation or a non-editable placeholder hint.</summary>
        private static GraphPresentationRelation CreateLoopTargetRelation(
            GraphPresentationEndpoint source,
            GraphPresentationItem target,
            GraphEdgeDescriptor edge,
            IReadOnlyDictionary<UUID, GraphPresentationItem> primary,
            GraphPresentationRelationKind kind,
            string label)
        {
            if (target.Node != null && edge != null)
            {
                return CreateTopologyRelation(source, edge, primary, kind, label);
            }

            return new GraphPresentationRelation(
                source,
                target.Entry,
                kind,
                GraphPresentationRelationRole.PlaceholderHint,
                label,
                edge,
                target.TargetUUID,
                target.LoopPlaceholder?.IsMissing == true,
                edge?.OccurrenceId ?? -10);
        }

        /// <summary>Adds one non-editable Loop control or completion relation.</summary>
        private static void AddDerivedLoopRelation(
            ICollection<GraphPresentationRelation> relations,
            GraphPresentationEndpoint source,
            GraphPresentationEndpoint target,
            GraphPresentationRelationKind kind,
            GraphPresentationRelationRole role,
            string label,
            UUID ownerUUID)
        {
            relations.Add(new GraphPresentationRelation(
                source,
                target,
                kind,
                role,
                label,
                null,
                ownerUUID,
                false,
                -1));
        }

        /// <summary>Builds concurrent Parallel branches and their runtime-specific synchronization completion.</summary>
        private static void BuildForEach(
            GraphTopology topology,
            GraphPresentationItem source,
            IReadOnlyList<GraphEdgeDescriptor> outgoing,
            IReadOnlyDictionary<UUID, GraphPresentationItem> primary,
            ICollection<GraphPresentationRelation> relations,
            ICollection<GraphPresentationItem> virtualItems)
        {
            foreach (GraphEdgeDescriptor edge in outgoing)
            {
                if (edge.FieldName == "event")
                {
                    continue;
                }

                relations.Add(CreateTopologyRelation(source.Output, edge, primary, ConvertTopologyKind(edge.Kind), edge.Label));
            }

            ForEach flow = (ForEach)source.Node.Node;
            bool enumerableExists = flow.enumerable != null
                && flow.enumerable.HasEditorReference
                && topology.Tree.GetVariable(flow.enumerable.UUID) != null;
            string enumerableName = enumerableExists ? topology.Tree.GetVariableDescName(flow.enumerable.UUID) : string.Empty;
            GraphPresentationItem check = GraphPresentationItem.CreateForEachJunction(
                new GraphForEachJunction(GraphForEachJunctionKind.EnumerableCheck, enumerableName));
            virtualItems.Add(check);
            source.ForEachScope.SetCheck(check);
            relations.Add(new GraphPresentationRelation(
                source.Output, check.Entry, GraphPresentationRelationKind.ForEachCheck,
                GraphPresentationRelationRole.DerivedControl, "enumerable", null, UUID.Empty, false, -330));
            relations.Add(new GraphPresentationRelation(
                check.Output, source.FlowComplete, GraphPresentationRelationKind.ForEachExit,
                GraphPresentationRelationRole.DerivedControl, "Not IEnumerable · Returns Failed", null,
                source.TargetUUID, false, -336));

            if (!enumerableExists)
            {
                UUID missing = flow.enumerable?.HasEditorReference == true ? flow.enumerable.UUID : UUID.Empty;
                GraphPresentationItem placeholder = GraphPresentationItem.CreateForEachPlaceholder(
                    new GraphForEachPlaceholder(GraphForEachPlaceholderKind.MissingEnumerable, missing));
                virtualItems.Add(placeholder);
                source.ForEachScope.SetBody(placeholder);
                relations.Add(new GraphPresentationRelation(
                    check.Output, placeholder.Entry, GraphPresentationRelationKind.ForEachCheck,
                    GraphPresentationRelationRole.PlaceholderHint, "Invalid", null, missing, missing != UUID.Empty, -331));
                relations.Add(new GraphPresentationRelation(
                    placeholder.Output, source.FlowComplete, GraphPresentationRelationKind.ForEachExit,
                    GraphPresentationRelationRole.DerivedCompletion, "Returns Failed", null, source.TargetUUID, false, -331));
                return;
            }

            GraphEdgeDescriptor bodyEdge = FindEdge(outgoing, "event", -1);
            GraphPresentationItem body = null;
            bool hasBody = flow.@event != null && flow.@event.UUID != UUID.Empty
                && primary.TryGetValue(flow.@event.UUID, out body);
            if (!hasBody)
            {
                bool missing = flow.@event != null && flow.@event.UUID != UUID.Empty;
                GraphPresentationItem placeholder = GraphPresentationItem.CreateForEachPlaceholder(
                    new GraphForEachPlaceholder(
                        missing ? GraphForEachPlaceholderKind.MissingBody : GraphForEachPlaceholderKind.EmptyBody,
                        missing ? flow.@event.UUID : UUID.Empty));
                virtualItems.Add(placeholder);
                body = placeholder;
            }

            source.ForEachScope.SetBody(body);
            relations.Add(new GraphPresentationRelation(
                check.Output, body.Entry, GraphPresentationRelationKind.ForEachBody,
                hasBody ? GraphPresentationRelationRole.AuthoredReference : GraphPresentationRelationRole.PlaceholderHint,
                "Has item", bodyEdge, body.TargetUUID, !hasBody && body.TargetUUID != UUID.Empty,
                bodyEdge?.OccurrenceId ?? -332));
            relations.Add(new GraphPresentationRelation(
                check.Output, source.FlowComplete, GraphPresentationRelationKind.ForEachExit,
                GraphPresentationRelationRole.DerivedCompletion, "Exhausted", null, source.TargetUUID, false, -333));

            if (hasBody && !ReferenceEquals(body, source))
            {
                relations.Add(new GraphPresentationRelation(
                    body.Completion, check.Entry, GraphPresentationRelationKind.ForEachRepeat,
                    GraphPresentationRelationRole.DerivedControl, "Next Item", bodyEdge, source.TargetUUID, false,
                    bodyEdge?.OccurrenceId ?? -334));
            }

            bool itemExists = flow.item != null
                && flow.item.HasEditorReference
                && topology.Tree.GetVariable(flow.item.UUID) != null;
            if (!itemExists)
            {
                UUID missing = flow.item?.HasEditorReference == true ? flow.item.UUID : UUID.Empty;
                if (missing != UUID.Empty)
                {
                    AppendWarning(source.Node, $"Missing ForEach item variable {missing}");
                }

                GraphPresentationItem hint = GraphPresentationItem.CreateForEachPlaceholder(
                    new GraphForEachPlaceholder(
                        missing == UUID.Empty ? GraphForEachPlaceholderKind.MissingItemOutput : GraphForEachPlaceholderKind.MissingItemVariable,
                        missing));
                virtualItems.Add(hint);
                source.ForEachScope.SetItemOutputHint(hint);
                relations.Add(new GraphPresentationRelation(
                    source.Output, hint.Entry, GraphPresentationRelationKind.ForEachCheck,
                    GraphPresentationRelationRole.PlaceholderHint, string.Empty, null, UUID.Empty, false, -335));
            }
        }

        /// <summary>Builds direct authored alternatives and runtime-ordered Decision return semantics.</summary>
}
}
