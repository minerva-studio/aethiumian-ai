using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
    /// <summary>One canvas-only semantic leaf visual shared by layout and node drawing.</summary>
    internal sealed class GraphLeafVisualDescriptor
    {
        internal GraphLeafVisualDescriptor(string title, string tooltip, Vector2 size, bool isBoolean, bool? constantValue)
        {
            Title = title;
            Tooltip = tooltip;
            Size = size;
            IsBoolean = isBoolean;
            ConstantValue = constantValue;
        }

        internal string Title { get; }
        internal string Tooltip { get; }
        internal Vector2 Size { get; }
        internal bool IsBoolean { get; }
        internal bool? ConstantValue { get; }
    }

    internal enum GraphPresentationKind
    {
        Card,
        Entrance,
        Exit,
        Sequence,
        Parallel,
        ForEach,
        Decision,
        Condition,
        ConditionPlaceholder,
        Loop,
        LoopPlaceholder,
        LoopJunction,
        ProbabilityPlaceholder,
        DecisionPlaceholder,
        ParallelPlaceholder,
        ForEachPlaceholder,
        ForEachJunction,
        ServicePlaceholder,
        ReferenceProxy,
        Missing,
    }

    /// <summary>
    /// Presentation-only metadata for an unresolved authored Service slot.
    /// </summary>
    internal sealed class GraphServicePlaceholder
    {
        internal GraphServicePlaceholder(GraphPresentationItem host, string label, UUID missingUUID)
        {
            Host = host ?? throw new ArgumentNullException(nameof(host));
            Label = string.IsNullOrEmpty(label) ? "Service" : label;
            MissingUUID = missingUUID;
        }

        /// <summary>Gets the presentation item that owns the authored Service slot.</summary>
        internal GraphPresentationItem Host { get; }

        /// <summary>Gets the authored Service field label.</summary>
        internal string Label { get; }

        /// <summary>Gets the unresolved UUID.</summary>
        internal UUID MissingUUID { get; }

        /// <summary>Gets the visible placeholder title.</summary>
        internal string Title => $"MISSING {Label.ToUpperInvariant()}";

        /// <summary>Gets diagnostic detail for the placeholder tooltip.</summary>
        internal string Tooltip => $"Missing Service target {MissingUUID}";
    }

    /// <summary>
    /// Identifies one authored branch lane of a Condition presentation.
    /// </summary>
    internal enum GraphConditionBranch
    {
        True,
        False,
    }

    /// <summary>
    /// Immutable editor description of one authored Probability weight.
    /// </summary>
    internal sealed class GraphProbabilityWeightDescriptor
    {
        private GraphProbabilityWeightDescriptor(
            int index,
            bool isDynamic,
            int constantWeight,
            UUID variableUUID,
            string variableName,
            bool isMissingVariable)
        {
            Index = index;
            IsDynamic = isDynamic;
            ConstantWeight = constantWeight;
            VariableUUID = variableUUID;
            VariableName = variableName ?? string.Empty;
            IsMissingVariable = isMissingVariable;
        }

        /// <summary>Gets the authored collection index.</summary>
        internal int Index { get; }

        /// <summary>Gets whether the runtime weight comes from a variable.</summary>
        internal bool IsDynamic { get; }

        /// <summary>Gets the non-negative runtime-equivalent constant weight.</summary>
        internal int ConstantWeight { get; }

        /// <summary>Gets the referenced variable UUID for a dynamic weight.</summary>
        internal UUID VariableUUID { get; }

        /// <summary>Gets the editor display name for a dynamic weight.</summary>
        internal string VariableName { get; }

        /// <summary>Gets whether a dynamic variable UUID does not resolve in the tree.</summary>
        internal bool IsMissingVariable { get; }

        /// <summary>Creates a descriptor for a constant Probability entry.</summary>
        internal static GraphProbabilityWeightDescriptor Create(int index, Probability.EventWeight option)
        {
            return new GraphProbabilityWeightDescriptor(
                index,
                isDynamic: false,
                Mathf.Max(0, option?.weight ?? 0),
                UUID.Empty,
                string.Empty,
                isMissingVariable: false);
        }

        /// <summary>Creates a descriptor for a constant or variable PseudoProbability entry.</summary>
        internal static GraphProbabilityWeightDescriptor Create(
            BehaviourTreeData tree,
            int index,
            PseudoProbability.EventWeight option)
        {
            VariableField<int> field = option?.weight;
            if (field == null || field.IsConstant)
            {
                return new GraphProbabilityWeightDescriptor(
                    index,
                    isDynamic: false,
                    Mathf.Max(0, field?.Constant ?? 0),
                    UUID.Empty,
                    string.Empty,
                    isMissingVariable: false);
            }

            UUID uuid = field.UUID;
            VariableData variable = tree ? tree.GetVariable(uuid) : null;
            return new GraphProbabilityWeightDescriptor(
                index,
                isDynamic: true,
                0,
                uuid,
                tree ? tree.GetVariableDescName(uuid) : VariableData.MISSING_VARIABLE_NAME,
                variable == null);
        }
    }

    /// <summary>Identifies the runtime meaning of a Probability placeholder.</summary>
    internal enum GraphProbabilityPlaceholderKind
    {
        NoOptions,
        EmptyOption,
        MissingOption,
    }

    /// <summary>Presentation-only fallback for a Probability option without a real card.</summary>
    internal sealed class GraphProbabilityPlaceholder
    {
        internal GraphProbabilityPlaceholder(GraphProbabilityPlaceholderKind kind, int index, UUID missingUUID)
        {
            Kind = kind;
            Index = index;
            MissingUUID = missingUUID;
        }

        /// <summary>Gets the placeholder runtime meaning.</summary>
        internal GraphProbabilityPlaceholderKind Kind { get; }

        /// <summary>Gets the authored option index, or -1 for an empty option list.</summary>
        internal int Index { get; }

        /// <summary>Gets the unresolved UUID for a missing option.</summary>
        internal UUID MissingUUID { get; }

        /// <summary>Gets whether this placeholder represents an invalid selectable entry.</summary>
        internal bool IsInvalidSelection => Kind != GraphProbabilityPlaceholderKind.NoOptions;

        /// <summary>Gets the concise placeholder title.</summary>
        internal string Title => Kind switch
        {
            GraphProbabilityPlaceholderKind.NoOptions => "NO OPTIONS",
            GraphProbabilityPlaceholderKind.MissingOption => $"MISSING OPTION [{Index}]",
            _ => $"EMPTY OPTION [{Index}]",
        };

        /// <summary>Gets the execution consequence shown by the placeholder.</summary>
        internal string Subtitle => IsInvalidSelection ? "Invalid selection" : "Returns Failed";

        /// <summary>Gets diagnostic detail for the placeholder tooltip.</summary>
        internal string Tooltip => Kind switch
        {
            GraphProbabilityPlaceholderKind.NoOptions => "No candidate exists; the Flow returns Failed.",
            GraphProbabilityPlaceholderKind.MissingOption => $"Missing Probability target {MissingUUID}",
            _ => "The authored Probability option has no target.",
        };
    }

    /// <summary>One authored candidate occurrence in a Probability scope.</summary>
    internal sealed class GraphProbabilityOption
    {
        internal GraphProbabilityOption(
            GraphProbabilityWeightDescriptor weight,
            GraphPresentationItem item,
            GraphEdgeDescriptor edge)
        {
            Weight = weight ?? throw new ArgumentNullException(nameof(weight));
            Item = item ?? throw new ArgumentNullException(nameof(item));
            Edge = edge;
        }

        /// <summary>Gets the authored weight descriptor.</summary>
        internal GraphProbabilityWeightDescriptor Weight { get; }

        /// <summary>Gets the real target or presentation-only placeholder.</summary>
        internal GraphPresentationItem Item { get; }

        /// <summary>Gets the source topology edge when the reference is non-empty.</summary>
        internal GraphEdgeDescriptor Edge { get; }

        /// <summary>Gets or sets whether this occurrence can be selected under known editor weights.</summary>
        internal bool IsEligible { get; set; }

        /// <summary>Gets or sets the runtime-consistent label shown on its authored relation.</summary>
        internal string Label { get; set; }
    }

    /// <summary>Identifies the runtime meaning of a Decision option placeholder.</summary>
    internal enum GraphDecisionPlaceholderKind
    {
        NoOptions,
        EmptyOption,
        MissingOption,
    }

    /// <summary>Presentation-only fallback for a Decision option without a real card.</summary>
    internal sealed class GraphDecisionPlaceholder
    {
        internal GraphDecisionPlaceholder(GraphDecisionPlaceholderKind kind, int index, UUID missingUUID)
        {
            Kind = kind;
            Index = index;
            MissingUUID = missingUUID;
        }

        /// <summary>Gets the placeholder runtime meaning.</summary>
        internal GraphDecisionPlaceholderKind Kind { get; }

        /// <summary>Gets the authored option index, or -1 for an empty option list.</summary>
        internal int Index { get; }

        /// <summary>Gets the unresolved UUID for a missing option.</summary>
        internal UUID MissingUUID { get; }

        /// <summary>Gets whether this placeholder terminates execution with Error.</summary>
        internal bool IsError => Kind != GraphDecisionPlaceholderKind.NoOptions;

        /// <summary>Gets the concise placeholder title.</summary>
        internal string Title => Kind switch
        {
            GraphDecisionPlaceholderKind.NoOptions => "NO OPTIONS",
            GraphDecisionPlaceholderKind.MissingOption => $"MISSING OPTION [{Index}]",
            _ => $"EMPTY OPTION [{Index}]",
        };

        /// <summary>Gets the runtime consequence shown by the placeholder.</summary>
        internal string Subtitle => IsError ? "Returns Error" : "Returns Failed";

        /// <summary>Gets diagnostic detail for the placeholder tooltip.</summary>
        internal string Tooltip => Kind switch
        {
            GraphDecisionPlaceholderKind.NoOptions => "No alternative exists; the Flow returns Failed.",
            GraphDecisionPlaceholderKind.MissingOption => $"Missing Decision target {MissingUUID}",
            _ => "The authored Decision option has no target and returns Error when reached.",
        };
    }

    /// <summary>One authored candidate occurrence in a Decision scope.</summary>
    internal sealed class GraphDecisionOption
    {
        internal GraphDecisionOption(int index, GraphPresentationItem item, GraphEdgeDescriptor edge)
        {
            Index = index;
            Item = item ?? throw new ArgumentNullException(nameof(item));
            Edge = edge;
        }

        /// <summary>Gets the authored collection index.</summary>
        internal int Index { get; }

        /// <summary>Gets the real target or presentation-only placeholder.</summary>
        internal GraphPresentationItem Item { get; }

        /// <summary>Gets the source topology edge when the reference is non-empty.</summary>
        internal GraphEdgeDescriptor Edge { get; }
    }

    /// <summary>
    /// Identifies one semantic part of a Loop presentation.
    /// </summary>
    internal enum GraphLoopPart
    {
        Condition,
        Body,
    }

    /// <summary>
    /// Identifies one derived control point in a Loop presentation.
    /// </summary>
    internal enum GraphLoopJunctionKind
    {
        CountCheck,
    }

    /// <summary>
    /// Semantic relation rendered by the presentation layer.
    /// </summary>
    internal enum GraphPresentationRelationKind
    {
        Entrance,
        Exit,
        Structural,
        SequenceStart,
        SequenceNext,
        SequenceFailure,
        SequenceSuccess,
        AggregateStart,
        AggregateNext,
        AggregateComplete,
        FlowComplete,
        DecisionBranch,
        DecisionSuccess,
        DecisionFailure,
        ProbabilityBranch,
        ParallelBranch,
        ParallelComplete,
        ForEachCheck,
        ForEachBody,
        ForEachRepeat,
        ForEachExit,
        ConditionTrue,
        ConditionFalse,
        LoopCondition,
        LoopBody,
        LoopRepeat,
        LoopExit,
        Service,
        Raw,
    }

    /// <summary>
    /// Describes whether a presentation relation represents authored data or derived visual semantics.
    /// </summary>
    internal enum GraphPresentationRelationRole
    {
        AuthoredReference,
        AuthoredTreeHead,
        DerivedCompletion,
        DerivedControl,
        PlaceholderHint,
    }

    /// <summary>
    /// Anchor role used by a presentation relation endpoint.
    /// </summary>
    internal enum GraphPresentationAnchorKind
    {
        Entry,
        Output,
        FlowComplete,
    }

    /// <summary>
    /// One semantic anchor on a presentation item.
    /// </summary>
    internal readonly struct GraphPresentationEndpoint : IEquatable<GraphPresentationEndpoint>
    {
        internal GraphPresentationEndpoint(GraphPresentationItem item, GraphPresentationAnchorKind anchor)
        {
            Item = item;
            Anchor = anchor;
        }

        /// <summary>Gets the owning presentation item.</summary>
        internal GraphPresentationItem Item { get; }

        /// <summary>Gets the semantic anchor role.</summary>
        internal GraphPresentationAnchorKind Anchor { get; }

        /// <summary>Gets whether this endpoint resolves to a presentation item.</summary>
        internal bool IsValid => Item != null;

        /// <inheritdoc />
        public bool Equals(GraphPresentationEndpoint other)
        {
            return ReferenceEquals(Item, other.Item) && Anchor == other.Anchor;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is GraphPresentationEndpoint other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return HashCode.Combine(Item, (int)Anchor);
        }

        public static bool operator ==(GraphPresentationEndpoint left, GraphPresentationEndpoint right) => left.Equals(right);
        public static bool operator !=(GraphPresentationEndpoint left, GraphPresentationEndpoint right) => !left.Equals(right);
    }

    /// <summary>
    /// One editor-only semantic relation between presentation anchors.
    /// </summary>
    internal sealed class GraphPresentationRelation
    {
        internal GraphPresentationRelation(
            GraphPresentationEndpoint source,
            GraphPresentationEndpoint target,
            GraphPresentationRelationKind kind,
            GraphPresentationRelationRole role,
            string label,
            GraphEdgeDescriptor origin,
            UUID targetUUID,
            bool isMissingTarget,
            int occurrenceId,
            bool isVisuallyDisabled = false,
            GraphPresentationItem contextualOwner = null,
            GraphPresentationItem contextualTrigger = null)
        {
            Source = source;
            Target = target;
            Kind = kind;
            Role = role;
            Label = label ?? string.Empty;
            Origin = origin;
            TargetUUID = targetUUID;
            IsMissingTarget = isMissingTarget;
            OccurrenceId = occurrenceId;
            IsVisuallyDisabled = isVisuallyDisabled;
            ContextualOwner = contextualOwner;
            ContextualTrigger = contextualTrigger;
        }

        /// <summary>Gets the source presentation anchor.</summary>
        internal GraphPresentationEndpoint Source { get; }

        /// <summary>Gets the target presentation anchor.</summary>
        internal GraphPresentationEndpoint Target { get; }

        /// <summary>Gets the semantic presentation relation kind.</summary>
        internal GraphPresentationRelationKind Kind { get; }

        /// <summary>Gets whether this relation is authored, derived completion, or a placeholder hint.</summary>
        internal GraphPresentationRelationRole Role { get; }

        /// <summary>Gets the displayed relation label.</summary>
        internal string Label { get; }

        /// <summary>Gets the authoritative topology edge, if this relation came from one.</summary>
        internal GraphEdgeDescriptor Origin { get; }

        /// <summary>Gets the referenced UUID, including missing topology targets.</summary>
        internal UUID TargetUUID { get; }

        /// <summary>Gets whether the authoritative target was missing.</summary>
        internal bool IsMissingTarget { get; }

        /// <summary>Gets the stable occurrence id assigned by topology discovery.</summary>
        internal int OccurrenceId { get; }

        /// <summary>Gets whether known constant weights make this authored candidate inactive.</summary>
        internal bool IsVisuallyDisabled { get; }

        /// <summary>Gets the owner whose selection reveals this contextual relation.</summary>
        internal GraphPresentationItem ContextualOwner { get; }

        /// <summary>Gets the direct member whose single selection reveals this relation.</summary>
        internal GraphPresentationItem ContextualTrigger { get; }

        /// <summary>Gets the nearest composite owner that supplies this relation's visual family.</summary>
        internal GraphPresentationItem VisualOwner { get; private set; }

        /// <summary>Assigns editor-only visual ownership without changing contextual visibility.</summary>
        internal void SetVisualOwner(GraphPresentationItem owner)
        {
            VisualOwner ??= owner;
        }

        /// <summary>Gets whether this relation should be visible for the selected runtime node.</summary>
        internal bool IsVisibleFor(TreeNode selectedNode)
        {
            if (ContextualTrigger != null)
            {
                return ContextualTrigger.Node?.Node == selectedNode;
            }

            return ContextualOwner == null || ContextualOwner.Node?.Node == selectedNode;
        }

        /// <summary>
        /// Gets whether this relation can represent an authored reference to a future topology editing service.
        /// </summary>
        internal bool IsEditableReference => Role == GraphPresentationRelationRole.AuthoredReference && Origin != null;
    }

    /// <summary>
    /// A named embedded item used by a compound presentation node.
    /// </summary>
    internal sealed class GraphPresentationSlot
    {
        internal GraphPresentationSlot(string label, int index, GraphEdgeDescriptor edge, GraphPresentationItem content)
        {
            Label = label;
            Index = index;
            Edge = edge;
            Content = content;
        }

        /// <summary>Gets the semantic field or collection label.</summary>
        internal string Label { get; }

        /// <summary>Gets the collection index, or -1 for a single reference.</summary>
        internal int Index { get; }

        /// <summary>Gets the source topology edge.</summary>
        internal GraphEdgeDescriptor Edge { get; }

        /// <summary>Gets the embedded presentation content.</summary>
        internal GraphPresentationItem Content { get; }
    }

    /// <summary>
    /// Presentation-only fallback shown for an empty or unresolved Condition branch.
    /// </summary>
    internal sealed class GraphConditionPlaceholder
    {
        internal GraphConditionPlaceholder(GraphConditionBranch branch, UUID missingUUID)
        {
            Branch = branch;
            MissingUUID = missingUUID;
        }

        /// <summary>Gets the authored Condition branch represented by this placeholder.</summary>
        internal GraphConditionBranch Branch { get; }

        /// <summary>Gets whether the authored UUID failed to resolve.</summary>
        internal bool IsMissing => MissingUUID != UUID.Empty;

        /// <summary>Gets the unresolved authored UUID, or Empty for an empty slot.</summary>
        internal UUID MissingUUID { get; }

        /// <summary>Gets the concise placeholder title.</summary>
        internal string Title => $"{(IsMissing ? "MISSING" : "EMPTY")} {Branch.ToString().ToUpperInvariant()}";

        /// <summary>Gets the runtime fallback result when this branch has no executable target.</summary>
        internal string Subtitle => Branch == GraphConditionBranch.True ? "Returns Success" : "Returns Failed";

        /// <summary>Gets diagnostic detail for the placeholder tooltip.</summary>
        internal string Tooltip => IsMissing ? $"Missing target {MissingUUID}" : $"{Branch} branch has no target.";
    }

    /// <summary>
    /// Presentation-only fallback shown for an empty or unresolved Loop condition or body occurrence.
    /// </summary>
    internal sealed class GraphLoopPlaceholder
    {
        internal GraphLoopPlaceholder(GraphLoopPart part, int index, UUID missingUUID)
        {
            Part = part;
            Index = index;
            MissingUUID = missingUUID;
        }

        /// <summary>Gets the Loop part represented by this placeholder.</summary>
        internal GraphLoopPart Part { get; }

        /// <summary>Gets the body occurrence index, or -1 for the condition.</summary>
        internal int Index { get; }

        /// <summary>Gets whether the authored UUID failed to resolve.</summary>
        internal bool IsMissing => MissingUUID != UUID.Empty;

        /// <summary>Gets the unresolved authored UUID, or Empty for an empty slot.</summary>
        internal UUID MissingUUID { get; }

        /// <summary>Gets the concise placeholder title.</summary>
        internal string Title
        {
            get
            {
                string state = IsMissing ? "MISSING" : "EMPTY";
                return Part == GraphLoopPart.Condition
                    ? $"{state} CONDITION"
                    : Index >= 0 ? $"{state} BODY {Index + 1}" : $"{state} BODY";
            }
        }

        /// <summary>Gets the runtime-oriented placeholder subtitle.</summary>
        internal string Subtitle => Part == GraphLoopPart.Condition
            ? "Cannot evaluate loop"
            : "No action before repeat";

        /// <summary>Gets diagnostic detail for the placeholder tooltip.</summary>
        internal string Tooltip => IsMissing
            ? $"Missing Loop {Part.ToString().ToLowerInvariant()} target {MissingUUID}"
            : $"Loop {Part.ToString().ToLowerInvariant()} has no target.";
    }

    /// <summary>
    /// Presentation-only control point used for a Loop count check.
    /// </summary>
    internal sealed class GraphLoopJunction
    {
        internal GraphLoopJunction(GraphLoopJunctionKind kind)
        {
            Kind = kind;
        }

        /// <summary>Gets the derived Loop control role.</summary>
        internal GraphLoopJunctionKind Kind { get; }

        /// <summary>Gets the concise control-point title.</summary>
        internal string Title => "COUNT CHECK";

        /// <summary>Gets optional explanatory text.</summary>
        internal string Subtitle => "Uses loopCount";
    }

    /// <summary>Identifies the runtime consequence represented by a Parallel placeholder.</summary>
    internal enum GraphParallelPlaceholderKind
    {
        NoBranches,
        IgnoredBranch,
        ImmediateCompletion,
    }

    /// <summary>Presentation-only explanation for a Parallel occurrence without a runnable stack.</summary>
    internal sealed class GraphParallelPlaceholder
    {
        internal GraphParallelPlaceholder(GraphParallelPlaceholderKind kind, int index, UUID missingUUID)
        {
            Kind = kind;
            Index = index;
            MissingUUID = missingUUID;
        }

        internal GraphParallelPlaceholderKind Kind { get; }
        internal int Index { get; }
        internal UUID MissingUUID { get; }
        internal bool IsMissing => MissingUUID != UUID.Empty;
        internal string Title => Kind switch
        {
            GraphParallelPlaceholderKind.NoBranches => "NO BRANCHES",
            GraphParallelPlaceholderKind.IgnoredBranch => $"{(IsMissing ? "MISSING" : "EMPTY")} BRANCH [{Index}]",
            _ => $"{(IsMissing ? "MISSING" : "EMPTY")} BRANCH [{Index}]",
        };
        internal string Subtitle => Kind switch
        {
            GraphParallelPlaceholderKind.NoBranches => "Returns Success",
            GraphParallelPlaceholderKind.IgnoredBranch => "Ignored by Wait All",
            _ => "Completes Wait Any immediately",
        };
        internal string Tooltip => IsMissing
            ? $"Missing Parallel target {MissingUUID}. {Subtitle}."
            : Subtitle;
    }

    /// <summary>Identifies one presentation-only ForEach control point.</summary>
    internal enum GraphForEachJunctionKind
    {
        EnumerableCheck,
    }

    /// <summary>Describes the enumerable gate used by a ForEach scope.</summary>
    internal sealed class GraphForEachJunction
    {
        internal GraphForEachJunction(GraphForEachJunctionKind kind, string enumerableName)
        {
            Kind = kind;
            EnumerableName = enumerableName ?? string.Empty;
        }

        internal GraphForEachJunctionKind Kind { get; }
        internal string EnumerableName { get; }
        internal string Title => "ENUMERABLE CHECK";
        internal string Subtitle => string.IsNullOrEmpty(EnumerableName) ? "IEnumerable required" : EnumerableName;
    }

    /// <summary>Identifies an explicit non-persistent ForEach failure or optional-output annotation.</summary>
    internal enum GraphForEachPlaceholderKind
    {
        MissingEnumerable,
        MissingItemOutput,
        MissingItemVariable,
        EmptyBody,
        MissingBody,
    }

    /// <summary>Presentation-only ForEach diagnostic with its exact runtime consequence.</summary>
    internal sealed class GraphForEachPlaceholder
    {
        internal GraphForEachPlaceholder(GraphForEachPlaceholderKind kind, UUID missingUUID)
        {
            Kind = kind;
            MissingUUID = missingUUID;
        }

        internal GraphForEachPlaceholderKind Kind { get; }
        internal UUID MissingUUID { get; }
        internal bool IsMissing => MissingUUID != UUID.Empty;
        internal string Title => Kind switch
        {
            GraphForEachPlaceholderKind.MissingEnumerable => IsMissing ? "MISSING ENUMERABLE" : "EMPTY ENUMERABLE",
            GraphForEachPlaceholderKind.MissingItemOutput => "NO ITEM OUTPUT",
            GraphForEachPlaceholderKind.MissingItemVariable => "MISSING ITEM OUTPUT",
            GraphForEachPlaceholderKind.MissingBody => "MISSING BODY",
            _ => "EMPTY BODY",
        };
        internal string Subtitle => Kind switch
        {
            GraphForEachPlaceholderKind.MissingEnumerable => "Returns Failed",
            GraphForEachPlaceholderKind.MissingItemOutput or GraphForEachPlaceholderKind.MissingItemVariable
                => "Body runs without assigning item",
            _ => "Errors when an item exists",
        };
        internal string Tooltip => IsMissing
            ? $"Missing ForEach target {MissingUUID}. {Subtitle}."
            : Subtitle;
    }

    /// <summary>
    /// Shared editor-only scope for a composite Flow with a derived completion marker.
    /// </summary>
}
