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
    /// Defines the shared unscaled geometry used by graph presentation, layout, and rendering.
    /// </summary>
    internal static class GraphPresentationMetrics
    {
        internal static readonly Vector2 NormalNodeSize = new(180f, 58f);
        internal static readonly Vector2 FlowNodeSize = new(200f, 48f);
        internal static readonly Vector2 BranchNodeSize = new(176f, 68f);
        internal static readonly Vector2 ServiceNodeSize = new(152f, 42f);
        internal static readonly Vector2 ReferenceItemSize = new(180f, 48f);
        internal static readonly Vector2 ConditionPlaceholderSize = new(160f, 46f);
        internal static readonly Vector2 LoopPlaceholderSize = new(160f, 46f);
        internal static readonly Vector2 LoopCountCheckSize = new(160f, 42f);
        internal static readonly Vector2 ServicePlaceholderSize = new(152f, 42f);
        internal static readonly Vector2 ProbabilityPlaceholderSize = new(176f, 48f);
        internal static readonly Vector2 DecisionPlaceholderSize = new(176f, 48f);
        internal static readonly Vector2 ParallelPlaceholderSize = new(176f, 48f);
        internal static readonly Vector2 ForEachPlaceholderSize = new(176f, 48f);
        internal static readonly Vector2 ForEachCheckSize = new(164f, 42f);

        internal const float FlowCompletionMinimumWidth = 96f;
        internal const float FlowCompletionMaximumWidth = 220f;
        internal const float FlowCompletionHeight = 24f;

        internal const float SiblingGap = 32f;
        internal const float LevelGap = 36f;
        internal const float ServiceGap = 20f;
        internal const float ServiceScopePadding = 12f;
        internal const float ServiceScopeHeader = 22f;
        internal const float UnreachableGap = 44f;
        internal const float ConditionPadding = 14f;
        internal const float ConditionHeader = 28f;
        internal const float ConditionMinimumWidth = 216f;
        internal const float ConditionBranchGap = 48f;
        internal const float ConditionBranchLevelGap = 48f;
        internal const float ConditionBracketOffset = 14f;
        internal const float ProbabilityBranchGap = 48f;
        internal const float ProbabilityBranchLevelGap = 48f;
        internal const float ProbabilityFanOffset = 14f;
        internal const float DecisionBranchGap = 48f;
        internal const float DecisionBranchLevelGap = 48f;
        internal const float FlowCompletionGap = 30f;
        internal const float SequenceRailOffset = 18f;
        internal const float LoopBodyFramePadding = 14f;
        internal const float LoopBodyFrameHeader = 20f;
        internal const float LoopReturnRailGap = 18f;
        internal const float LoopExitRailGap = 18f;
        internal const float ParallelForkGap = 22f;
        internal const float ParallelJoinGap = 28f;
        internal const float ForEachBodyFramePadding = 14f;
        internal const float ForEachBodyFrameHeader = 20f;

        /// <summary>
        /// Returns a deterministic completion marker size without depending on resolved panel geometry.
        /// </summary>
        /// <param name="displayName">The owning Flow display name.</param>
        /// <returns>The unscaled presentation size reserved for the completion marker.</returns>
        internal static Vector2 GetFlowCompletionSize(string displayName)
        {
            const float fixedTextAndPaddingWidth = 54f;
            float estimatedNameWidth = 0f;
            foreach (char character in displayName ?? string.Empty)
            {
                estimatedNameWidth += char.IsWhiteSpace(character) ? 3.5f : character <= 0x7f ? 5.5f : 9f;
            }

            return new Vector2(
                Mathf.Clamp(
                    fixedTextAndPaddingWidth + estimatedNameWidth,
                    FlowCompletionMinimumWidth,
                    FlowCompletionMaximumWidth),
                FlowCompletionHeight);
        }
    }

    /// <summary>
    /// Editor-only semantic presentation role for a graph item.
    /// </summary>
    internal enum GraphPresentationKind
    {
        Card,
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
        Structural,
        SequenceStart,
        SequenceNext,
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
            GraphPresentationItem contextualOwner = null)
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

        /// <summary>Gets whether this relation should be visible for the selected runtime node.</summary>
        internal bool IsVisibleFor(TreeNode selectedNode)
        {
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
    internal abstract class GraphFlowScope
    {
        private readonly List<GraphPresentationItem> members = new();

        protected GraphFlowScope(GraphPresentationItem owner)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        /// <summary>Gets the Flow presentation that owns this scope.</summary>
        internal GraphPresentationItem Owner { get; }

        /// <summary>Gets direct scope members in semantic order.</summary>
        internal IReadOnlyList<GraphPresentationItem> Members => members;

        /// <summary>Gets or sets the derived completion marker position.</summary>
        internal Vector2 CompletionPosition { get; set; }

        /// <summary>Gets the presentation size of this Flow completion marker.</summary>
        internal virtual Vector2 CompletionSize => GraphPresentationMetrics.GetFlowCompletionSize(
            Owner.Node?.DisplayName ?? "Flow");

        /// <summary>Gets or sets the derived scope bounds.</summary>
        internal Rect Bounds { get; set; }

        /// <summary>Adds one direct member to the scope.</summary>
        internal void AddMember(GraphPresentationItem member)
        {
            if (member != null)
            {
                members.Add(member);
            }
        }
    }

    /// <summary>
    /// Derived scope and rail for one free Sequence presentation.
    /// </summary>
    internal sealed class GraphSequenceScope : GraphFlowScope
    {
        internal GraphSequenceScope(GraphPresentationItem owner) : base(owner)
        {
        }

        /// <summary>Gets or sets the derived bracket rail x coordinate.</summary>
        internal float RailX { get; set; }

        /// <summary>Gets or sets the derived bracket start y coordinate.</summary>
        internal float RailStartY { get; set; }

        /// <summary>Gets or sets the derived bracket end y coordinate.</summary>
        internal float RailEndY { get; set; }

    }

    /// <summary>
    /// Derived bracket and completion state for one free Condition presentation.
    /// </summary>
    internal sealed class GraphConditionScope : GraphFlowScope
    {
        internal GraphConditionScope(GraphPresentationItem owner) : base(owner)
        {
        }

        /// <summary>Gets the True branch item or its presentation-only placeholder.</summary>
        internal GraphPresentationItem TrueBranch { get; private set; }

        /// <summary>Gets the False branch item or its presentation-only placeholder.</summary>
        internal GraphPresentationItem FalseBranch { get; private set; }

        /// <summary>Gets or sets the left bracket rail coordinate.</summary>
        internal float LeftX { get; set; }

        /// <summary>Gets or sets the right bracket rail coordinate.</summary>
        internal float RightX { get; set; }

        /// <summary>Gets or sets the top coordinate of the branch bracket.</summary>
        internal float BracketTopY { get; set; }

        /// <summary>Gets or sets the bottom coordinate of the branch bracket.</summary>
        internal float BracketBottomY { get; set; }

        /// <summary>Assigns one branch lane and registers its item as a direct scope member.</summary>
        internal void SetBranch(GraphConditionBranch branch, GraphPresentationItem item)
        {
            if (branch == GraphConditionBranch.True)
            {
                TrueBranch = item;
            }
            else
            {
                FalseBranch = item;
            }

            AddMember(item);
        }
    }

    /// <summary>
    /// Derived fan and completion state for one freely arranged Probability family Flow.
    /// </summary>
    internal sealed class GraphProbabilityScope : GraphFlowScope
    {
        private readonly List<GraphProbabilityOption> options = new();

        internal GraphProbabilityScope(GraphPresentationItem owner, BehaviourTreeData tree) : base(owner)
        {
            Subtitle = BuildSubtitle(owner, tree);
        }

        /// <summary>Gets authored option occurrences in collection order.</summary>
        internal IReadOnlyList<GraphProbabilityOption> Options => options;

        /// <summary>Gets the concise card summary for the Probability mode.</summary>
        internal string Subtitle { get; }

        /// <summary>Gets or sets the left boundary of the derived candidate fan.</summary>
        internal float LeftX { get; set; }

        /// <summary>Gets or sets the right boundary of the derived candidate fan.</summary>
        internal float RightX { get; set; }

        /// <summary>Gets or sets the upper candidate fan coordinate.</summary>
        internal float FanTopY { get; set; }

        /// <summary>Gets or sets the lower candidate fan coordinate.</summary>
        internal float FanBottomY { get; set; }

        /// <summary>Adds one authored option occurrence as a direct scope member.</summary>
        internal void AddOption(GraphProbabilityOption option)
        {
            if (option == null)
            {
                throw new ArgumentNullException(nameof(option));
            }

            options.Add(option);
            AddMember(option.Item);
        }

        private static string BuildSubtitle(GraphPresentationItem owner, BehaviourTreeData tree)
        {
            if (owner.Node?.Node is not PseudoProbability pseudo)
            {
                return "PICK ONE";
            }

            VariableField<int> field = pseudo.maxConsecutiveBranch;
            if (field == null || field.IsConstant)
            {
                int value = field?.Constant ?? -1;
                return value > 0 ? $"PICK ONE · MAX STREAK {value}" : "PICK ONE · NO STREAK LIMIT";
            }

            string name = tree ? tree.GetVariableDescName(field.UUID) : VariableData.MISSING_VARIABLE_NAME;
            return $"PICK ONE · MAX STREAK {name}";
        }
    }

    /// <summary>Derived concurrent fork and synchronization join for one Parallel Flow.</summary>
    internal sealed class GraphParallelScope : GraphFlowScope
    {
        private readonly List<GraphPresentationItem> branches = new();

        internal GraphParallelScope(GraphPresentationItem owner) : base(owner)
        {
        }

        internal Parallel.Mode Mode => ((Parallel)Owner.Node.Node).mode;
        internal IReadOnlyList<GraphPresentationItem> Branches => branches;
        internal float ForkY { get; set; }
        internal float JoinY { get; set; }
        internal string JoinTitle => Mode == Parallel.Mode.WaitAll ? "WAIT ALL" : "FIRST COMPLETE";
        internal string JoinSubtitle => Mode == Parallel.Mode.WaitAll ? "All stacks stop" : "Stops remaining stacks";

        internal void AddBranch(GraphPresentationItem item)
        {
            if (item != null)
            {
                branches.Add(item);
                AddMember(item);
            }
        }
    }

    /// <summary>Derived enumerable check, free Body frame, and repeat path for one ForEach Flow.</summary>
    internal sealed class GraphForEachScope : GraphFlowScope
    {
        internal GraphForEachScope(GraphPresentationItem owner) : base(owner)
        {
        }

        internal GraphPresentationItem Check { get; private set; }
        internal GraphPresentationItem Body { get; private set; }
        internal GraphPresentationItem ItemOutputHint { get; private set; }
        internal Rect BodyFrameBounds { get; set; }
        internal float ReturnRailX => BodyFrameBounds.xMin - GraphPresentationMetrics.LoopReturnRailGap;

        internal void SetCheck(GraphPresentationItem item)
        {
            Check = item;
            AddMember(item);
        }

        internal void SetBody(GraphPresentationItem item)
        {
            Body = item;
            AddMember(item);
        }

        internal void SetItemOutputHint(GraphPresentationItem item)
        {
            ItemOutputHint = item;
            AddMember(item);
        }
    }

    /// <summary>
    /// Derived completion state for one Decision whose authored alternatives remain free branches.
    /// </summary>
    internal sealed class GraphDecisionScope : GraphFlowScope
    {
        private readonly List<GraphDecisionOption> options = new();

        internal GraphDecisionScope(GraphPresentationItem owner) : base(owner)
        {
        }

        /// <summary>Gets authored alternatives in runtime attempt order.</summary>
        internal IReadOnlyList<GraphDecisionOption> Options => options;

        /// <summary>Gets or sets the shared success merge rail coordinate.</summary>
        internal float SuccessRailY { get; set; }

        /// <summary>Adds one authored alternative occurrence as a direct scope member.</summary>
        internal void AddOption(GraphDecisionOption option)
        {
            if (option == null)
            {
                throw new ArgumentNullException(nameof(option));
            }

            options.Add(option);
            AddMember(option.Item);
        }
    }

    /// <summary>
    /// Derived Body frame and exit state for one free Loop presentation.
    /// </summary>
    internal sealed class GraphLoopScope : GraphFlowScope
    {
        private readonly List<GraphPresentationItem> body = new();

        internal GraphLoopScope(GraphPresentationItem owner) : base(owner)
        {
        }

        /// <summary>Gets the authored Loop mode.</summary>
        internal Loop.LoopType Mode => ((Loop)Owner.Node.Node).loopType;

        /// <summary>Gets the condition card, placeholder, or derived count check.</summary>
        internal GraphPresentationItem Condition { get; private set; }

        /// <summary>Gets direct body occurrences in authored execution order.</summary>
        internal IReadOnlyList<GraphPresentationItem> Body => body;

        /// <summary>Gets or sets the lightweight frame derived from the complete Body envelope.</summary>
        internal Rect BodyFrameBounds { get; set; }

        /// <summary>Gets the derived repeat rail to the left of the Body frame.</summary>
        internal float ReturnRailX => BodyFrameBounds.xMin - GraphPresentationMetrics.LoopReturnRailGap;

        /// <summary>Gets the derived exit rail to the right of the Body frame.</summary>
        internal float ExitRailX => BodyFrameBounds.xMax + GraphPresentationMetrics.LoopExitRailGap;

        /// <summary>Assigns the condition or count-check item.</summary>
        internal void SetCondition(GraphPresentationItem item)
        {
            Condition = item;
            AddMember(item);
        }

        /// <summary>Adds one body occurrence in authored execution order.</summary>
        internal void AddBody(GraphPresentationItem item)
        {
            if (item == null)
            {
                return;
            }

            body.Add(item);
            AddMember(item);
        }

    }

    /// <summary>
    /// Derived, freely arranged boundary for one Service and its structural subtree.
    /// </summary>
    internal sealed class GraphServiceScope
    {
        private readonly List<GraphPresentationItem> members = new();

        internal GraphServiceScope(GraphPresentationItem owner, GraphPresentationItem host)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Host = host ?? throw new ArgumentNullException(nameof(host));
        }

        /// <summary>Gets the Service card that owns this unique scope.</summary>
        internal GraphPresentationItem Owner { get; }

        /// <summary>Gets the first-placement authored host.</summary>
        internal GraphPresentationItem Host { get; }

        /// <summary>Gets all real cards contained by this Service structural subtree.</summary>
        internal IReadOnlyList<GraphPresentationItem> Members => members;

        /// <summary>Gets or sets the derived frame bounds.</summary>
        internal Rect Bounds { get; set; }

        /// <summary>Gets or sets the number of additional authored hosts.</summary>
        internal int AdditionalHostCount { get; set; }

        /// <summary>Adds a unique member to this scope.</summary>
        internal void AddMember(GraphPresentationItem member)
        {
            if (member != null && !members.Contains(member))
            {
                members.Add(member);
            }
        }
    }

    /// <summary>
    /// A node presentation. Ordinary nodes are top-level free items; only a
    /// Condition may own an embedded predicate item.
    /// </summary>
    internal sealed class GraphPresentationItem
    {
        private readonly List<GraphPresentationSlot> slots = new();

        internal GraphPresentationItem(
            GraphPresentationKind kind,
            GraphNodeDescriptor node,
            UUID targetUUID,
            string warning,
            bool isRoot = true,
            GraphConditionPlaceholder placeholder = null,
            GraphLoopPlaceholder loopPlaceholder = null,
            GraphLoopJunction loopJunction = null,
            GraphProbabilityPlaceholder probabilityPlaceholder = null,
            GraphDecisionPlaceholder decisionPlaceholder = null,
            GraphServicePlaceholder servicePlaceholder = null,
            GraphParallelPlaceholder parallelPlaceholder = null,
            GraphForEachPlaceholder forEachPlaceholder = null,
            GraphForEachJunction forEachJunction = null)
        {
            Kind = kind;
            Node = node;
            TargetUUID = targetUUID;
            Warning = warning;
            IsRoot = isRoot;
            Placeholder = placeholder;
            LoopPlaceholder = loopPlaceholder;
            LoopJunction = loopJunction;
            ProbabilityPlaceholder = probabilityPlaceholder;
            DecisionPlaceholder = decisionPlaceholder;
            ServicePlaceholder = servicePlaceholder;
            ParallelPlaceholder = parallelPlaceholder;
            ForEachPlaceholder = forEachPlaceholder;
            ForEachJunction = forEachJunction;
            Position = node?.Position ?? Vector2.zero;
        }

        /// <summary>Creates one non-persistent missing Service placeholder item.</summary>
        internal static GraphPresentationItem CreateServicePlaceholder(GraphServicePlaceholder placeholder)
        {
            if (placeholder == null)
            {
                throw new ArgumentNullException(nameof(placeholder));
            }

            return new GraphPresentationItem(
                GraphPresentationKind.ServicePlaceholder,
                null,
                placeholder.MissingUUID,
                placeholder.Tooltip,
                isRoot: true,
                servicePlaceholder: placeholder);
        }

        /// <summary>Creates one non-persistent Condition branch placeholder item.</summary>
        internal static GraphPresentationItem CreateConditionPlaceholder(GraphConditionPlaceholder placeholder)
        {
            if (placeholder == null)
            {
                throw new ArgumentNullException(nameof(placeholder));
            }

            return new GraphPresentationItem(
                GraphPresentationKind.ConditionPlaceholder,
                null,
                placeholder.MissingUUID,
                placeholder.Tooltip,
                isRoot: false,
                placeholder: placeholder);
        }

        /// <summary>Creates one non-persistent Loop condition or body placeholder item.</summary>
        internal static GraphPresentationItem CreateLoopPlaceholder(GraphLoopPlaceholder placeholder)
        {
            if (placeholder == null)
            {
                throw new ArgumentNullException(nameof(placeholder));
            }

            return new GraphPresentationItem(
                GraphPresentationKind.LoopPlaceholder,
                null,
                placeholder.MissingUUID,
                placeholder.Tooltip,
                isRoot: false,
                loopPlaceholder: placeholder);
        }

        /// <summary>Creates one non-persistent Loop control junction.</summary>
        internal static GraphPresentationItem CreateLoopJunction(GraphLoopJunction junction)
        {
            if (junction == null)
            {
                throw new ArgumentNullException(nameof(junction));
            }

            return new GraphPresentationItem(
                GraphPresentationKind.LoopJunction,
                null,
                UUID.Empty,
                string.Empty,
                isRoot: false,
                loopJunction: junction);
        }

        /// <summary>Creates one non-persistent Probability option placeholder.</summary>
        internal static GraphPresentationItem CreateProbabilityPlaceholder(GraphProbabilityPlaceholder placeholder)
        {
            if (placeholder == null)
            {
                throw new ArgumentNullException(nameof(placeholder));
            }

            return new GraphPresentationItem(
                GraphPresentationKind.ProbabilityPlaceholder,
                null,
                placeholder.MissingUUID,
                placeholder.Tooltip,
                isRoot: false,
                probabilityPlaceholder: placeholder);
        }

        /// <summary>Creates one non-persistent Decision option placeholder.</summary>
        internal static GraphPresentationItem CreateDecisionPlaceholder(GraphDecisionPlaceholder placeholder)
        {
            if (placeholder == null)
            {
                throw new ArgumentNullException(nameof(placeholder));
            }

            return new GraphPresentationItem(
                GraphPresentationKind.DecisionPlaceholder,
                null,
                placeholder.MissingUUID,
                placeholder.Tooltip,
                isRoot: false,
                decisionPlaceholder: placeholder);
        }

        /// <summary>Creates one non-persistent Parallel diagnostic item.</summary>
        internal static GraphPresentationItem CreateParallelPlaceholder(GraphParallelPlaceholder placeholder)
        {
            if (placeholder == null)
            {
                throw new ArgumentNullException(nameof(placeholder));
            }

            return new GraphPresentationItem(
                GraphPresentationKind.ParallelPlaceholder,
                null,
                placeholder.MissingUUID,
                placeholder.Tooltip,
                isRoot: false,
                parallelPlaceholder: placeholder);
        }

        /// <summary>Creates one non-persistent ForEach diagnostic item.</summary>
        internal static GraphPresentationItem CreateForEachPlaceholder(GraphForEachPlaceholder placeholder)
        {
            if (placeholder == null)
            {
                throw new ArgumentNullException(nameof(placeholder));
            }

            return new GraphPresentationItem(
                GraphPresentationKind.ForEachPlaceholder,
                null,
                placeholder.MissingUUID,
                placeholder.Tooltip,
                isRoot: false,
                forEachPlaceholder: placeholder);
        }

        /// <summary>Creates one non-persistent ForEach enumerable check item.</summary>
        internal static GraphPresentationItem CreateForEachJunction(GraphForEachJunction junction)
        {
            if (junction == null)
            {
                throw new ArgumentNullException(nameof(junction));
            }

            return new GraphPresentationItem(
                GraphPresentationKind.ForEachJunction,
                null,
                UUID.Empty,
                string.Empty,
                isRoot: false,
                forEachJunction: junction);
        }

        /// <summary>Gets the semantic presentation kind.</summary>
        internal GraphPresentationKind Kind { get; }

        /// <summary>Gets the source node descriptor.</summary>
        internal GraphNodeDescriptor Node { get; }

        /// <summary>Gets the referenced UUID for proxy or missing items.</summary>
        internal UUID TargetUUID { get; }

        /// <summary>Gets the parent compound item, if any.</summary>
        internal GraphPresentationItem Parent { get; private set; }

        /// <summary>Gets whether the item owns a persistent top-level node position.</summary>
        internal bool IsRoot { get; }

        /// <summary>Gets the warning associated with this item.</summary>
        internal string Warning { get; }

        /// <summary>Gets presentation-only Condition fallback metadata, when applicable.</summary>
        internal GraphConditionPlaceholder Placeholder { get; }

        /// <summary>Gets presentation-only Loop fallback metadata, when applicable.</summary>
        internal GraphLoopPlaceholder LoopPlaceholder { get; }

        /// <summary>Gets presentation-only Loop control metadata, when applicable.</summary>
        internal GraphLoopJunction LoopJunction { get; }

        /// <summary>Gets presentation-only Probability fallback metadata, when applicable.</summary>
        internal GraphProbabilityPlaceholder ProbabilityPlaceholder { get; }

        /// <summary>Gets presentation-only Decision fallback metadata, when applicable.</summary>
        internal GraphDecisionPlaceholder DecisionPlaceholder { get; }

        /// <summary>Gets presentation-only missing Service metadata, when applicable.</summary>
        internal GraphServicePlaceholder ServicePlaceholder { get; }

        /// <summary>Gets presentation-only Parallel fallback metadata, when applicable.</summary>
        internal GraphParallelPlaceholder ParallelPlaceholder { get; }

        /// <summary>Gets presentation-only ForEach fallback metadata, when applicable.</summary>
        internal GraphForEachPlaceholder ForEachPlaceholder { get; }

        /// <summary>Gets presentation-only ForEach control metadata, when applicable.</summary>
        internal GraphForEachJunction ForEachJunction { get; }

        /// <summary>Gets or sets the in-memory canvas position.</summary>
        internal Vector2 Position { get; set; }

        /// <summary>Gets or sets the measured unscaled size.</summary>
        internal Vector2 Size { get; set; }

        /// <summary>Gets embedded semantic slots.</summary>
        internal IReadOnlyList<GraphPresentationSlot> Slots => slots;

        /// <summary>Gets whether this item is a compound presentation.</summary>
        internal bool IsContainer => Kind == GraphPresentationKind.Condition;

        /// <summary>Gets or sets the derived composite Flow scope.</summary>
        internal GraphFlowScope FlowScope { get; set; }

        /// <summary>Gets the derived Sequence scope, when this item is a Sequence.</summary>
        internal GraphSequenceScope SequenceScope => FlowScope as GraphSequenceScope;

        /// <summary>Gets the derived Condition scope, when this item is a Condition.</summary>
        internal GraphConditionScope ConditionScope => FlowScope as GraphConditionScope;

        /// <summary>Gets the derived Loop scope, when this item is a Loop.</summary>
        internal GraphLoopScope LoopScope => FlowScope as GraphLoopScope;

        /// <summary>Gets the derived Probability family scope, when applicable.</summary>
        internal GraphProbabilityScope ProbabilityScope => FlowScope as GraphProbabilityScope;

        /// <summary>Gets the derived Decision scope, when applicable.</summary>
        internal GraphDecisionScope DecisionScope => FlowScope as GraphDecisionScope;

        /// <summary>Gets the derived Parallel scope, when applicable.</summary>
        internal GraphParallelScope ParallelScope => FlowScope as GraphParallelScope;

        /// <summary>Gets the derived ForEach scope, when applicable.</summary>
        internal GraphForEachScope ForEachScope => FlowScope as GraphForEachScope;

        /// <summary>Gets this item's entry anchor.</summary>
        internal GraphPresentationEndpoint Entry => new(this, GraphPresentationAnchorKind.Entry);

        /// <summary>Gets this item's ordinary output anchor.</summary>
        internal GraphPresentationEndpoint Output => new(this, GraphPresentationAnchorKind.Output);

        /// <summary>Gets this composite Flow's virtual completion anchor.</summary>
        internal GraphPresentationEndpoint FlowComplete => new(this, GraphPresentationAnchorKind.FlowComplete);

        /// <summary>Gets the completion anchor used by a containing Sequence.</summary>
        internal GraphPresentationEndpoint Completion => FlowScope != null ? FlowComplete : Output;

        /// <summary>Adds an embedded slot and assigns its parent.</summary>
        internal void AddSlot(GraphPresentationSlot slot)
        {
            if (slot == null)
            {
                throw new ArgumentNullException(nameof(slot));
            }

            slots.Add(slot);
            if (slot.Content != null)
            {
                slot.Content.Parent = this;
            }
        }
    }

    /// <summary>
    /// Complete editor-only presentation of a topology snapshot.
    /// </summary>
    internal sealed class GraphPresentation
    {
        private readonly Dictionary<UUID, GraphPresentationItem> primaryByUUID;
        private readonly List<GraphPresentationItem> roots;
        private readonly List<GraphPresentationRelation> relations;
        private readonly List<GraphFlowScope> completionScopes;
        private readonly List<GraphServiceScope> serviceScopes;

        internal GraphPresentation(
            List<GraphPresentationItem> roots,
            Dictionary<UUID, GraphPresentationItem> primaryByUUID,
            List<GraphPresentationRelation> relations,
            List<GraphFlowScope> completionScopes,
            List<GraphServiceScope> serviceScopes = null)
        {
            this.roots = roots;
            this.primaryByUUID = primaryByUUID;
            this.relations = relations;
            this.completionScopes = completionScopes;
            this.serviceScopes = serviceScopes ?? new List<GraphServiceScope>();
        }

        /// <summary>Gets all top-level real cards and presentation-only placeholders.</summary>
        internal IReadOnlyList<GraphPresentationItem> Roots => roots;

        /// <summary>Gets all semantic presentation relations.</summary>
        internal IReadOnlyList<GraphPresentationRelation> Relations => relations;

        /// <summary>Gets all composite Flow scopes with derived completion markers.</summary>
        internal IReadOnlyList<GraphFlowScope> CompletionScopes => completionScopes;

        /// <summary>Gets the unique first-placement Service scopes.</summary>
        internal IReadOnlyList<GraphServiceScope> ServiceScopes => serviceScopes;

        /// <summary>Finds the unique scope owned by one Service UUID.</summary>
        internal GraphServiceScope FindServiceScope(UUID uuid)
        {
            foreach (GraphServiceScope scope in serviceScopes)
            {
                if (scope.Owner.TargetUUID == uuid)
                {
                    return scope;
                }
            }

            return null;
        }

        /// <summary>Finds the primary presentation item for a UUID.</summary>
        internal GraphPresentationItem Find(UUID uuid)
        {
            return primaryByUUID.TryGetValue(uuid, out GraphPresentationItem item) ? item : null;
        }

        /// <summary>Moves one free item and any embedded predicate in memory.</summary>
        internal void MoveRoot(UUID uuid, Vector2 position)
        {
            GraphPresentationItem item = Find(uuid);
            if (item == null || !item.IsRoot)
            {
                return;
            }

            Vector2 delta = position - item.Position;
            Offset(item, delta);
        }

        private static void Offset(GraphPresentationItem item, Vector2 delta)
        {
            item.Position += delta;
            foreach (GraphPresentationSlot slot in item.Slots)
            {
                if (slot.Content != null)
                {
                    Offset(slot.Content, delta);
                }
            }
        }
    }

    /// <summary>
    /// Converts topology references into free-node semantic relations.
    /// </summary>
    internal static class GraphPresentationBuilder
    {
        /// <summary>Builds an editor-only presentation without modifying the tree.</summary>
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

            foreach (GraphNodeDescriptor descriptor in topology.Nodes)
            {
                if (!embedded.Contains(descriptor.UUID))
                {
                    primary[descriptor.UUID].Position = descriptor.Position;
                }
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
        private static List<GraphServiceScope> BuildServiceScopes(
            List<GraphPresentationRelation> relations,
            List<GraphPresentationItem> virtualItems)
        {
            Dictionary<UUID, GraphServiceScope> byService = new();
            for (int index = 0; index < relations.Count; index++)
            {
                GraphPresentationRelation relation = relations[index];
                if (relation.Kind == GraphPresentationRelationKind.Service && !relation.Target.IsValid)
                {
                    GraphPresentationItem placeholder = GraphPresentationItem.CreateServicePlaceholder(
                        new GraphServicePlaceholder(relation.Source.Item, relation.Label, relation.TargetUUID));
                    virtualItems.Add(placeholder);
                    relation = new GraphPresentationRelation(
                        relation.Source,
                        placeholder.Entry,
                        GraphPresentationRelationKind.Service,
                        GraphPresentationRelationRole.PlaceholderHint,
                        relation.Label,
                        relation.Origin,
                        relation.TargetUUID,
                        true,
                        relation.OccurrenceId);
                    relations[index] = relation;
                }

                if (relation.Kind != GraphPresentationRelationKind.Service || !relation.Target.IsValid
                    || relation.Target.Item.Node?.Node is not Service)
                {
                    continue;
                }

                GraphPresentationItem service = relation.Target.Item;
                GraphPresentationItem host = relation.Source.Item;
                if (byService.TryGetValue(service.TargetUUID, out GraphServiceScope shared))
                {
                    shared.AdditionalHostCount++;
                    continue;
                }

                GraphServiceScope scope = new(service, host);
                byService.Add(service.TargetUUID, scope);
                CollectServiceMembers(service, scope, relations, new HashSet<GraphPresentationItem>());
            }

            PositionMissingServicePlaceholders(relations, virtualItems);
            return new List<GraphServiceScope>(byService.Values);
        }

        /// <summary>Collects the non-Service structural subtree owned by a Service scope.</summary>
        private static void CollectServiceMembers(
            GraphPresentationItem item,
            GraphServiceScope scope,
            IReadOnlyList<GraphPresentationRelation> relations,
            ISet<GraphPresentationItem> visited)
        {
            if (item == null || !visited.Add(item))
            {
                return;
            }

            scope.AddMember(item);
            foreach (GraphPresentationRelation relation in relations)
            {
                if (!relation.Target.IsValid || relation.Kind is GraphPresentationRelationKind.Service or GraphPresentationRelationKind.Raw
                    || relation.Role == GraphPresentationRelationRole.DerivedCompletion
                    || relation.Source.Item != item)
                {
                    continue;
                }

                CollectServiceMembers(relation.Target.Item, scope, relations, visited);
            }
        }

        /// <summary>Places missing Service placeholders deterministically beside their authored hosts.</summary>
        private static void PositionMissingServicePlaceholders(
            IReadOnlyList<GraphPresentationRelation> relations,
            IReadOnlyList<GraphPresentationItem> virtualItems)
        {
            Dictionary<GraphPresentationItem, int> lanes = new();
            foreach (GraphPresentationRelation relation in relations)
            {
                GraphPresentationItem placeholder = relation.Target.Item;
                if (relation.Kind != GraphPresentationRelationKind.Service || placeholder?.ServicePlaceholder == null)
                {
                    continue;
                }

                GraphPresentationItem host = placeholder.ServicePlaceholder.Host;
                lanes.TryGetValue(host, out int lane);
                placeholder.Position = host.Position + new Vector2(
                    GraphPresentationLayout.GetItemSize(host).x + GraphPresentationMetrics.SiblingGap,
                    lane * (GraphPresentationMetrics.ServicePlaceholderSize.y + GraphPresentationMetrics.ServiceGap));
                lanes[host] = lane + 1;
            }
        }

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
                string label = childIndex == 0 ? "start" : $"next ({childIndex + 1})";
                GraphPresentationRelation relation = CreateTopologyRelation(previousCompletion, edge, primary, kind, label);
                relations.Add(relation);
                childIndex++;
                if (!relation.Target.IsValid)
                {
                    continue;
                }

                GraphPresentationItem member = relation.Target.Item;
                source.SequenceScope.AddMember(member);
                previousCompletion = member.Completion;
            }

            if (previousCompletion != source.FlowComplete)
            {
                relations.Add(new GraphPresentationRelation(
                    previousCompletion,
                    source.FlowComplete,
                    GraphPresentationRelationKind.FlowComplete,
                    GraphPresentationRelationRole.DerivedCompletion,
                    string.Empty,
                    null,
                    source.TargetUUID,
                    false,
                    -1));
            }
        }

        /// <summary>Builds mode-specific Loop condition, body, repeat, and exit relations.</summary>
        private static void BuildLoop(
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
                    new GraphLoopJunction(GraphLoopJunctionKind.CountCheck));
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

            string bodyLabel = loop.loopType == Loop.LoopType.@for ? "Continue · Body 1" : "True · Body 1";
            GraphPresentationEndpoint completion = BuildLoopBody(
                condition.Completion,
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
                condition.Completion,
                source.FlowComplete,
                GraphPresentationRelationKind.LoopExit,
                GraphPresentationRelationRole.DerivedCompletion,
                loop.loopType == Loop.LoopType.@for ? "Exhausted" : "False · Exit",
                source.TargetUUID);
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
        private static void BuildParallel(
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

                relations.Add(CreateTopologyRelation(source.Output, edge, primary, ConvertTopologyKind(edge.Kind), edge.Label));
            }

            Parallel parallel = (Parallel)source.Node.Node;
            NodeReference[] references = parallel.events ?? Array.Empty<NodeReference>();
            if (references.Length == 0)
            {
                GraphPresentationItem placeholder = GraphPresentationItem.CreateParallelPlaceholder(
                    new GraphParallelPlaceholder(GraphParallelPlaceholderKind.NoBranches, -1, UUID.Empty));
                virtualItems.Add(placeholder);
                source.ParallelScope.AddBranch(placeholder);
                relations.Add(new GraphPresentationRelation(
                    source.Output, placeholder.Entry, GraphPresentationRelationKind.ParallelBranch,
                    GraphPresentationRelationRole.PlaceholderHint, string.Empty, null, UUID.Empty, false, -300));
                relations.Add(new GraphPresentationRelation(
                    placeholder.Output, source.FlowComplete, GraphPresentationRelationKind.ParallelComplete,
                    GraphPresentationRelationRole.DerivedCompletion, "Returns Success", null, source.TargetUUID, false, -300));
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
                    relations.Add(new GraphPresentationRelation(
                        source.Output, placeholder.Entry, GraphPresentationRelationKind.ParallelBranch,
                        GraphPresentationRelationRole.PlaceholderHint, $"Branch {index + 1}", edge,
                        placeholder.TargetUUID, missing, edge?.OccurrenceId ?? -310 - index));
                    if (parallel.mode == Parallel.Mode.WaitAny)
                    {
                        relations.Add(new GraphPresentationRelation(
                            placeholder.Output, source.FlowComplete, GraphPresentationRelationKind.ParallelComplete,
                            GraphPresentationRelationRole.DerivedCompletion, "Completes immediately", edge,
                            source.TargetUUID, false, edge?.OccurrenceId ?? -310 - index));
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

                relations.Add(new GraphPresentationRelation(
                    source.Output, target.Entry, GraphPresentationRelationKind.ParallelBranch,
                    GraphPresentationRelationRole.AuthoredReference, isFirstScheduled ? $"Branch {index + 1}" : "Shared stack",
                    edge, target.TargetUUID, false, edge?.OccurrenceId ?? -320 - index));

                if (!isFirstScheduled || ReferenceEquals(target, source))
                {
                    continue;
                }

                relations.Add(new GraphPresentationRelation(
                    target.Completion, source.FlowComplete, GraphPresentationRelationKind.ParallelComplete,
                    GraphPresentationRelationRole.DerivedCompletion, parallel.mode == Parallel.Mode.WaitAll ? "Arrive" : "First complete",
                    edge, source.TargetUUID, false, edge?.OccurrenceId ?? -320 - index));
            }
        }

        /// <summary>Builds the enumerable check, free Body, repeat, and exhausted completion of a ForEach Flow.</summary>
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
                source.TargetUUID, false, -336, contextualOwner: source));

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
                    edge.Label));
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
                    edge.Label));
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
                    if (edge.Target != null && edge.Target.UUID != source.Node.UUID && !embedded.Contains(edge.Target.UUID))
                    {
                        source.AddSlot(new GraphPresentationSlot("Condition", -1, edge, primary[edge.Target.UUID]));
                        embedded.Add(edge.Target.UUID);
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
                    edge.Label));
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

    /// <summary>
    /// Measures free nodes, Condition compounds, and derived Flow scopes.
    /// </summary>
    internal static class GraphPresentationLayout
    {
        /// <summary>Measures presentation items without modifying source descriptors.</summary>
        internal static void Layout(GraphPresentation presentation)
        {
            if (presentation == null)
            {
                return;
            }

            foreach (GraphPresentationItem item in presentation.Roots)
            {
                Measure(item);
            }

            HashSet<GraphFlowScope> resolved = new();
            HashSet<GraphFlowScope> visiting = new();
            foreach (GraphFlowScope scope in presentation.CompletionScopes)
            {
                ResolveScope(presentation, scope, resolved, visiting);
            }

            PositionServicePlaceholders(presentation);
            foreach (GraphServiceScope scope in presentation.ServiceScopes)
            {
                ResolveServiceScope(scope);
            }
        }

        /// <summary>Gets the default card size for an item.</summary>
        internal static Vector2 GetItemSize(GraphPresentationItem item)
        {
            if (item?.Placeholder != null)
            {
                return GraphPresentationMetrics.ConditionPlaceholderSize;
            }

            if (item?.LoopPlaceholder != null)
            {
                return GraphPresentationMetrics.LoopPlaceholderSize;
            }

            if (item?.LoopJunction != null)
            {
                return GraphPresentationMetrics.LoopCountCheckSize;
            }

            if (item?.ProbabilityPlaceholder != null)
            {
                return GraphPresentationMetrics.ProbabilityPlaceholderSize;
            }

            if (item?.DecisionPlaceholder != null)
            {
                return GraphPresentationMetrics.DecisionPlaceholderSize;
            }

            if (item?.ParallelPlaceholder != null)
            {
                return GraphPresentationMetrics.ParallelPlaceholderSize;
            }

            if (item?.ForEachPlaceholder != null)
            {
                return GraphPresentationMetrics.ForEachPlaceholderSize;
            }

            if (item?.ForEachJunction != null)
            {
                return GraphPresentationMetrics.ForEachCheckSize;
            }

            if (item?.ServicePlaceholder != null)
            {
                return GraphPresentationMetrics.ServicePlaceholderSize;
            }

            return item?.Node == null ? GraphPresentationMetrics.ReferenceItemSize : GraphLayoutResolver.GetNodeSize(item.Node);
        }

        /// <summary>Gets the complete bounds of an item, including its composite Flow scope.</summary>
        internal static Rect GetBounds(GraphPresentationItem item)
        {
            if (item?.FlowScope != null)
            {
                return item.FlowScope.Bounds;
            }

            return item == null
                ? new Rect(Vector2.zero, GraphPresentationMetrics.ReferenceItemSize)
                : new Rect(item.Position, item.Size);
        }

        /// <summary>Positions unresolved Service slots beside their current host geometry.</summary>
        private static void PositionServicePlaceholders(GraphPresentation presentation)
        {
            Dictionary<GraphPresentationItem, int> lanes = new();
            foreach (GraphPresentationRelation relation in presentation.Relations)
            {
                GraphPresentationItem placeholder = relation.Target.Item;
                if (relation.Kind != GraphPresentationRelationKind.Service || placeholder?.ServicePlaceholder == null)
                {
                    continue;
                }

                GraphPresentationItem host = placeholder.ServicePlaceholder.Host;
                lanes.TryGetValue(host, out int lane);
                Rect hostBounds = GetBounds(host);
                placeholder.Position = new Vector2(
                    hostBounds.xMax + GraphPresentationMetrics.SiblingGap,
                    hostBounds.yMin + lane * (GraphPresentationMetrics.ServicePlaceholderSize.y + GraphPresentationMetrics.ServiceGap));
                lanes[host] = lane + 1;
            }
        }

        /// <summary>Derives a lightweight frame around one Service structural subtree.</summary>
        private static void ResolveServiceScope(GraphServiceScope scope)
        {
            Rect content = new(scope.Owner.Position, scope.Owner.Size);
            foreach (GraphPresentationItem member in scope.Members)
            {
                content = Union(content, GetBounds(member));
            }

            scope.Bounds = Rect.MinMaxRect(
                content.xMin - GraphPresentationMetrics.ServiceScopePadding,
                content.yMin - GraphPresentationMetrics.ServiceScopeHeader,
                content.xMax + GraphPresentationMetrics.ServiceScopePadding,
                content.yMax + GraphPresentationMetrics.ServiceScopePadding);
        }

        private static Vector2 Measure(GraphPresentationItem item)
        {
            if (item == null)
            {
                return GraphPresentationMetrics.ReferenceItemSize;
            }

            if (!item.IsContainer)
            {
                item.Size = GetItemSize(item);
                return item.Size;
            }

            GraphPresentationItem predicate = item.Slots.Count > 0 ? item.Slots[0].Content : null;
            Vector2 predicateSize = Measure(predicate);
            item.Size = new Vector2(
                Mathf.Max(GraphPresentationMetrics.ConditionMinimumWidth,
                    predicateSize.x + GraphPresentationMetrics.ConditionPadding * 2f),
                GraphPresentationMetrics.ConditionHeader + predicateSize.y
                    + GraphPresentationMetrics.ConditionPadding * 2f);
            item.Position = item.Node?.Position ?? Vector2.zero;
            if (predicate != null)
            {
                predicate.Position = item.Position + new Vector2(
                    GraphPresentationMetrics.ConditionPadding,
                    GraphPresentationMetrics.ConditionHeader + GraphPresentationMetrics.ConditionPadding);
            }

            return item.Size;
        }

        private static void ResolveScope(
            GraphPresentation presentation,
            GraphFlowScope scope,
            ISet<GraphFlowScope> resolved,
            ISet<GraphFlowScope> visiting)
        {
            if (scope == null || resolved.Contains(scope))
            {
                return;
            }

            Rect ownerBounds = new(scope.Owner.Position, scope.Owner.Size);
            if (!visiting.Add(scope))
            {
                SetFallbackScopeBounds(scope, ownerBounds);
                return;
            }

            foreach (GraphPresentationItem member in scope.Members)
            {
                if (member?.FlowScope != null && !ReferenceEquals(member.FlowScope, scope))
                {
                    ResolveScope(presentation, member.FlowScope, resolved, visiting);
                }
            }

            switch (scope)
            {
                case GraphSequenceScope sequenceScope:
                    ResolveSequenceScope(sequenceScope, ownerBounds);
                    break;
                case GraphConditionScope conditionScope:
                    ResolveConditionScope(presentation, conditionScope, ownerBounds);
                    break;
                case GraphLoopScope loopScope:
                    ResolveLoopScope(loopScope, ownerBounds);
                    break;
                case GraphProbabilityScope probabilityScope:
                    ResolveProbabilityScope(presentation, probabilityScope, ownerBounds);
                    break;
                case GraphDecisionScope decisionScope:
                    ResolveDecisionScope(presentation, decisionScope, ownerBounds);
                    break;
                case GraphParallelScope parallelScope:
                    ResolveParallelScope(presentation, parallelScope, ownerBounds);
                    break;
                case GraphForEachScope forEachScope:
                    ResolveForEachScope(presentation, forEachScope, ownerBounds);
                    break;
                default:
                    SetFallbackScopeBounds(scope, ownerBounds);
                    break;
            }

            visiting.Remove(scope);
            resolved.Add(scope);
        }

        /// <summary>Resolves a free Sequence rail and completion from its direct member bounds.</summary>
        private static void ResolveSequenceScope(GraphSequenceScope scope, Rect ownerBounds)
        {
            Rect contentBounds = ownerBounds;
            foreach (GraphPresentationItem member in scope.Members)
            {
                contentBounds = Union(contentBounds, GetBounds(member));
            }

            SetSequenceScopeBounds(scope, contentBounds);
        }

        /// <summary>Resolves Condition placeholder lanes, bracket bounds, and convergence completion.</summary>
        private static void ResolveConditionScope(
            GraphPresentation presentation,
            GraphConditionScope scope,
            Rect ownerBounds)
        {
            PositionConditionPlaceholders(scope, ownerBounds);
            Rect trueBounds = CalculateBranchEnvelope(presentation, scope.TrueBranch, scope, new HashSet<GraphPresentationItem>());
            Rect falseBounds = CalculateBranchEnvelope(presentation, scope.FalseBranch, scope, new HashSet<GraphPresentationItem>());
            Rect branchBounds = Union(trueBounds, falseBounds);
            float completionX = branchBounds.center.x - scope.CompletionSize.x * 0.5f;
            float completionY = branchBounds.yMax + GraphPresentationMetrics.FlowCompletionGap;
            scope.CompletionPosition = new Vector2(completionX, completionY);
            scope.LeftX = branchBounds.xMin - GraphPresentationMetrics.ConditionBracketOffset;
            scope.RightX = branchBounds.xMax + GraphPresentationMetrics.ConditionBracketOffset;
            scope.BracketTopY = branchBounds.yMin - GraphPresentationMetrics.ConditionBracketOffset;
            scope.BracketBottomY = completionY + scope.CompletionSize.y * 0.5f;

            Rect completionBounds = new(scope.CompletionPosition, scope.CompletionSize);
            Rect bounds = Union(ownerBounds, Union(branchBounds, completionBounds));
            bounds.xMin = Mathf.Min(bounds.xMin, scope.LeftX);
            bounds.xMax = Mathf.Max(bounds.xMax, scope.RightX);
            bounds.yMin = Mathf.Min(bounds.yMin, scope.BracketTopY);
            bounds.yMax = Mathf.Max(bounds.yMax, scope.BracketBottomY);
            scope.Bounds = bounds;
        }

        /// <summary>Resolves Probability placeholders, candidate fan bounds, and shared completion.</summary>
        private static void ResolveProbabilityScope(
            GraphPresentation presentation,
            GraphProbabilityScope scope,
            Rect ownerBounds)
        {
            PositionProbabilityPlaceholders(scope, ownerBounds);
            Rect branchBounds = ownerBounds;
            bool hasBranch = false;
            foreach (GraphProbabilityOption option in scope.Options)
            {
                Rect optionBounds = CalculateBranchEnvelope(
                    presentation,
                    option.Item,
                    scope,
                    new HashSet<GraphPresentationItem>());
                branchBounds = hasBranch ? Union(branchBounds, optionBounds) : optionBounds;
                hasBranch = true;
            }

            if (!hasBranch)
            {
                branchBounds = ownerBounds;
            }

            scope.CompletionPosition = new Vector2(
                branchBounds.center.x - scope.CompletionSize.x * 0.5f,
                branchBounds.yMax + GraphPresentationMetrics.FlowCompletionGap);
            scope.LeftX = branchBounds.xMin - GraphPresentationMetrics.ProbabilityFanOffset;
            scope.RightX = branchBounds.xMax + GraphPresentationMetrics.ProbabilityFanOffset;
            scope.FanTopY = branchBounds.yMin - GraphPresentationMetrics.ProbabilityFanOffset;
            scope.FanBottomY = scope.CompletionPosition.y + scope.CompletionSize.y * 0.5f;

            Rect completionBounds = new(scope.CompletionPosition, scope.CompletionSize);
            Rect bounds = Union(ownerBounds, Union(branchBounds, completionBounds));
            bounds.xMin = Mathf.Min(bounds.xMin, scope.LeftX);
            bounds.xMax = Mathf.Max(bounds.xMax, scope.RightX);
            bounds.yMin = Mathf.Min(bounds.yMin, scope.FanTopY);
            bounds.yMax = Mathf.Max(bounds.yMax, scope.FanBottomY);
            scope.Bounds = bounds;
        }

        /// <summary>Resolves free Decision alternatives and their shared completion below all branch envelopes.</summary>
        private static void ResolveDecisionScope(
            GraphPresentation presentation,
            GraphDecisionScope scope,
            Rect ownerBounds)
        {
            PositionDecisionPlaceholders(scope, ownerBounds);
            Rect branchBounds = ownerBounds;
            bool hasBranch = false;
            foreach (GraphDecisionOption option in scope.Options)
            {
                Rect optionBounds = CalculateBranchEnvelope(
                    presentation,
                    option.Item,
                    scope,
                    new HashSet<GraphPresentationItem>());
                branchBounds = hasBranch ? Union(branchBounds, optionBounds) : optionBounds;
                hasBranch = true;
            }

            if (!hasBranch)
            {
                branchBounds = ownerBounds;
            }

            scope.CompletionPosition = new Vector2(
                branchBounds.center.x - scope.CompletionSize.x * 0.5f,
                branchBounds.yMax + GraphPresentationMetrics.FlowCompletionGap);
            scope.SuccessRailY = branchBounds.yMax + GraphPresentationMetrics.FlowCompletionGap * 0.5f;
            scope.Bounds = Union(
                ownerBounds,
                Union(branchBounds, new Rect(scope.CompletionPosition, scope.CompletionSize)));
        }

        /// <summary>Resolves the fork, synchronization join, and completion of a Parallel scope.</summary>
        private static void ResolveParallelScope(
            GraphPresentation presentation,
            GraphParallelScope scope,
            Rect ownerBounds)
        {
            PositionParallelPlaceholders(scope, ownerBounds);
            Rect branchBounds = ownerBounds;
            bool hasBranch = false;
            foreach (GraphPresentationItem branch in scope.Branches)
            {
                Rect bounds = CalculateBranchEnvelope(presentation, branch, scope, new HashSet<GraphPresentationItem>());
                branchBounds = hasBranch ? Union(branchBounds, bounds) : bounds;
                hasBranch = true;
            }

            if (!hasBranch)
            {
                branchBounds = ownerBounds;
            }

            scope.ForkY = ownerBounds.yMax + GraphPresentationMetrics.ParallelForkGap;
            scope.JoinY = branchBounds.yMax + GraphPresentationMetrics.ParallelJoinGap;
            scope.CompletionPosition = new Vector2(
                branchBounds.center.x - scope.CompletionSize.x * 0.5f,
                scope.JoinY + GraphPresentationMetrics.FlowCompletionGap);
            scope.Bounds = Union(ownerBounds, Union(branchBounds, new Rect(scope.CompletionPosition, scope.CompletionSize)));
        }

        /// <summary>Resolves the ForEach enumerable check, free Body frame, repeat rail, and exhausted completion.</summary>
        private static void ResolveForEachScope(
            GraphPresentation presentation,
            GraphForEachScope scope,
            Rect ownerBounds)
        {
            if (scope.Check != null)
            {
                scope.Check.Position = new Vector2(
                    ownerBounds.center.x - scope.Check.Size.x * 0.5f,
                    ownerBounds.yMax + GraphPresentationMetrics.LevelGap);
            }

            Rect checkBounds = GetBounds(scope.Check);
            if (scope.Body != null && (scope.Body.ForEachPlaceholder != null || scope.Body.ForEachJunction != null))
            {
                scope.Body.Position = new Vector2(
                    checkBounds.center.x - scope.Body.Size.x * 0.5f,
                    checkBounds.yMax + GraphPresentationMetrics.LevelGap);
            }

            Rect bodyBounds = scope.Body == null ? checkBounds : CalculateBranchEnvelope(
                presentation, scope.Body, scope, new HashSet<GraphPresentationItem>());
            scope.BodyFrameBounds = new Rect(
                bodyBounds.xMin - GraphPresentationMetrics.ForEachBodyFramePadding,
                bodyBounds.yMin - GraphPresentationMetrics.ForEachBodyFrameHeader,
                bodyBounds.width + GraphPresentationMetrics.ForEachBodyFramePadding * 2f,
                bodyBounds.height + GraphPresentationMetrics.ForEachBodyFrameHeader + GraphPresentationMetrics.ForEachBodyFramePadding);

            if (scope.ItemOutputHint != null)
            {
                scope.ItemOutputHint.Position = new Vector2(
                    Mathf.Max(scope.BodyFrameBounds.xMax, checkBounds.xMax) + GraphPresentationMetrics.ServiceGap,
                    checkBounds.yMin);
            }

            Rect structure = Union(ownerBounds, Union(checkBounds, scope.BodyFrameBounds));
            if (scope.ItemOutputHint != null)
            {
                structure = Union(structure, GetBounds(scope.ItemOutputHint));
            }

            scope.CompletionPosition = new Vector2(
                structure.center.x - scope.CompletionSize.x * 0.5f,
                structure.yMax + GraphPresentationMetrics.FlowCompletionGap);
            scope.Bounds = Union(structure, new Rect(scope.CompletionPosition, scope.CompletionSize));
        }

        /// <summary>Resolves Loop virtual controls, the Body frame, and exit completion.</summary>
        private static void ResolveLoopScope(GraphLoopScope scope, Rect ownerBounds)
        {
            PositionLoopDerivedItems(scope, ownerBounds);
            Rect conditionBounds = GetLoopMemberBounds(scope, scope.Condition);
            Rect bodyBounds = GetLoopMemberBounds(scope, scope.Body[0]);
            for (int index = 1; index < scope.Body.Count; index++)
            {
                bodyBounds = Union(bodyBounds, GetLoopMemberBounds(scope, scope.Body[index]));
            }

            scope.BodyFrameBounds = Rect.MinMaxRect(
                bodyBounds.xMin - GraphPresentationMetrics.LoopBodyFramePadding,
                bodyBounds.yMin - GraphPresentationMetrics.LoopBodyFrameHeader,
                bodyBounds.xMax + GraphPresentationMetrics.LoopBodyFramePadding,
                bodyBounds.yMax + GraphPresentationMetrics.LoopBodyFramePadding);

            Rect structureBounds = Union(conditionBounds, scope.BodyFrameBounds);
            scope.CompletionPosition = new Vector2(
                structureBounds.center.x - scope.CompletionSize.x * 0.5f,
                structureBounds.yMax + GraphPresentationMetrics.FlowCompletionGap);
            Rect completionBounds = new(scope.CompletionPosition, scope.CompletionSize);
            Rect bounds = Union(ownerBounds, Union(structureBounds, completionBounds));
            bounds.xMin = Mathf.Min(bounds.xMin, scope.ReturnRailX);
            bounds.xMax = Mathf.Max(bounds.xMax, scope.ExitRailX);
            scope.Bounds = bounds;
        }

        /// <summary>Positions non-persistent Loop placeholders and control junctions from authored node geometry.</summary>
        private static void PositionLoopDerivedItems(GraphLoopScope scope, Rect ownerBounds)
        {
            GraphPresentationItem condition = scope.Condition;
            if (scope.Mode == Loop.LoopType.doWhile)
            {
                Rect bodyEnd = PositionLoopBodyPlaceholders(scope, ownerBounds);
                if (condition.LoopPlaceholder != null)
                {
                    condition.Position = new Vector2(
                        bodyEnd.center.x - condition.Size.x * 0.5f,
                        bodyEnd.yMax + GraphPresentationMetrics.LevelGap);
                }
            }
            else
            {
                if (condition.LoopPlaceholder != null || condition.LoopJunction != null)
                {
                    condition.Position = new Vector2(
                        ownerBounds.center.x - condition.Size.x * 0.5f,
                        ownerBounds.yMax + GraphPresentationMetrics.LevelGap);
                }

                PositionLoopBodyPlaceholders(scope, GetLoopMemberBounds(scope, condition));
            }

        }

        /// <summary>Positions derived body placeholders and returns the final body occurrence bounds.</summary>
        private static Rect PositionLoopBodyPlaceholders(
            GraphLoopScope scope,
            Rect preceding)
        {
            Rect previous = preceding;
            foreach (GraphPresentationItem member in scope.Body)
            {
                if (member.LoopPlaceholder != null)
                {
                    member.Position = new Vector2(
                        previous.center.x - member.Size.x * 0.5f,
                        previous.yMax + GraphPresentationMetrics.LevelGap);
                }

                previous = GetLoopMemberBounds(scope, member);
            }

            return previous;
        }

        /// <summary>Gets a Loop member's visible bounds without recursively reading its owning scope.</summary>
        private static Rect GetLoopMemberBounds(GraphLoopScope ownerScope, GraphPresentationItem item)
        {
            return ReferenceEquals(item?.FlowScope, ownerScope)
                ? new Rect(item.Position, item.Size)
                : GetBounds(item);
        }

        /// <summary>Calculates one free branch envelope including structural descendants and Service lanes.</summary>
        private static Rect CalculateBranchEnvelope(
            GraphPresentation presentation,
            GraphPresentationItem item,
            GraphFlowScope ownerScope,
            ISet<GraphPresentationItem> visited)
        {
            if (item == null)
            {
                return new Rect(Vector2.zero, GetItemSize(null));
            }

            Rect bounds = ReferenceEquals(item.FlowScope, ownerScope)
                ? new Rect(item.Position, item.Size)
                : GetBounds(item);
            if (!visited.Add(item) || presentation == null)
            {
                return bounds;
            }

            foreach (GraphPresentationRelation relation in presentation.Relations)
            {
                if (relation.Role == GraphPresentationRelationRole.DerivedCompletion
                    || relation.Kind == GraphPresentationRelationKind.Raw
                    || !relation.Target.IsValid
                    || relation.Origin?.Source == null
                    || relation.Origin.Source.UUID != item.TargetUUID)
                {
                    continue;
                }

                if (item.FlowScope != null && relation.Kind != GraphPresentationRelationKind.Service)
                {
                    continue;
                }

                GraphPresentationItem target = GetRootItem(relation.Target.Item);
                if (target == null || ReferenceEquals(target, ownerScope.Owner))
                {
                    continue;
                }

                bounds = Union(bounds, CalculateBranchEnvelope(presentation, target, ownerScope, visited));
            }

            return bounds;
        }

        /// <summary>Places non-persistent Probability placeholders in stable authored lanes.</summary>
        private static void PositionProbabilityPlaceholders(GraphProbabilityScope scope, Rect ownerBounds)
        {
            int count = scope.Options.Count;
            if (count == 0)
            {
                return;
            }

            float width = GraphPresentationMetrics.ProbabilityPlaceholderSize.x;
            float totalWidth = count * width + Mathf.Max(0, count - 1) * GraphPresentationMetrics.ProbabilityBranchGap;
            float startX = ownerBounds.center.x - totalWidth * 0.5f;
            float y = ownerBounds.yMax + GraphPresentationMetrics.ProbabilityBranchLevelGap;
            for (int index = 0; index < count; index++)
            {
                GraphPresentationItem item = scope.Options[index].Item;
                if (item.ProbabilityPlaceholder != null)
                {
                    item.Position = new Vector2(
                        startX + index * (width + GraphPresentationMetrics.ProbabilityBranchGap),
                        y);
                }
            }
        }

        /// <summary>Places Parallel diagnostics as stable sibling branch lanes beneath the fork.</summary>
        private static void PositionParallelPlaceholders(GraphParallelScope scope, Rect ownerBounds)
        {
            List<GraphPresentationItem> placeholders = new();
            foreach (GraphPresentationItem item in scope.Branches)
            {
                if (item?.ParallelPlaceholder != null)
                {
                    placeholders.Add(item);
                }
            }

            if (placeholders.Count == 0)
            {
                return;
            }

            float width = GraphPresentationMetrics.ParallelPlaceholderSize.x;
            float totalWidth = placeholders.Count * width + Mathf.Max(0, placeholders.Count - 1) * GraphPresentationMetrics.ProbabilityBranchGap;
            float left = ownerBounds.center.x - totalWidth * 0.5f;
            float top = ownerBounds.yMax + GraphPresentationMetrics.LevelGap;
            for (int index = 0; index < placeholders.Count; index++)
            {
                placeholders[index].Position = new Vector2(
                    left + index * (width + GraphPresentationMetrics.ProbabilityBranchGap), top);
            }
        }

        /// <summary>Places non-persistent Decision placeholders in stable authored lanes.</summary>
        private static void PositionDecisionPlaceholders(GraphDecisionScope scope, Rect ownerBounds)
        {
            int count = scope.Options.Count;
            if (count == 0)
            {
                return;
            }

            float width = GraphPresentationMetrics.DecisionPlaceholderSize.x;
            float totalWidth = count * width + Mathf.Max(0, count - 1) * GraphPresentationMetrics.DecisionBranchGap;
            float startX = ownerBounds.center.x - totalWidth * 0.5f;
            float y = ownerBounds.yMax + GraphPresentationMetrics.DecisionBranchLevelGap;
            for (int index = 0; index < count; index++)
            {
                GraphPresentationItem item = scope.Options[index].Item;
                if (item.DecisionPlaceholder != null)
                {
                    item.Position = new Vector2(
                        startX + index * (width + GraphPresentationMetrics.DecisionBranchGap),
                        y);
                }
            }
        }

        /// <summary>Resolves an embedded item to the root card that owns its canvas position.</summary>
        private static GraphPresentationItem GetRootItem(GraphPresentationItem item)
        {
            while (item?.Parent != null)
            {
                item = item.Parent;
            }

            return item;
        }

        /// <summary>Places Condition fallback cards deterministically without moving authored nodes.</summary>
        private static void PositionConditionPlaceholders(GraphConditionScope scope, Rect ownerBounds)
        {
            GraphPresentationItem trueBranch = scope.TrueBranch;
            GraphPresentationItem falseBranch = scope.FalseBranch;
            bool truePlaceholder = trueBranch?.Placeholder != null;
            bool falsePlaceholder = falseBranch?.Placeholder != null;
            float defaultY = ownerBounds.yMax + GraphPresentationMetrics.ConditionBranchLevelGap;
            if (truePlaceholder && falsePlaceholder)
            {
                trueBranch.Position = new Vector2(
                    ownerBounds.center.x - GraphPresentationMetrics.ConditionBranchGap * 0.5f - trueBranch.Size.x,
                    defaultY);
                falseBranch.Position = new Vector2(
                    ownerBounds.center.x + GraphPresentationMetrics.ConditionBranchGap * 0.5f,
                    defaultY);
                return;
            }

            if (truePlaceholder)
            {
                Rect falseBounds = GetBounds(falseBranch);
                trueBranch.Position = new Vector2(
                    Mathf.Min(ownerBounds.center.x - GraphPresentationMetrics.ConditionBranchGap - trueBranch.Size.x,
                        falseBounds.xMin - GraphPresentationMetrics.ConditionBranchGap - trueBranch.Size.x),
                    Mathf.Max(defaultY, falseBounds.yMin));
            }

            if (falsePlaceholder)
            {
                Rect trueBounds = GetBounds(trueBranch);
                falseBranch.Position = new Vector2(
                    Mathf.Max(ownerBounds.center.x + GraphPresentationMetrics.ConditionBranchGap,
                        trueBounds.xMax + GraphPresentationMetrics.ConditionBranchGap),
                    Mathf.Max(defaultY, trueBounds.yMin));
            }
        }

        /// <summary>Sets minimal completion bounds when a composite scope cycle is encountered.</summary>
        private static void SetFallbackScopeBounds(GraphFlowScope scope, Rect ownerBounds)
        {
            scope.CompletionPosition = new Vector2(
                ownerBounds.center.x - scope.CompletionSize.x * 0.5f,
                ownerBounds.yMax + GraphPresentationMetrics.FlowCompletionGap);
            scope.Bounds = Union(ownerBounds, new Rect(scope.CompletionPosition, scope.CompletionSize));
        }

        private static void SetSequenceScopeBounds(GraphSequenceScope scope, Rect contentBounds)
        {
            float completionY = Mathf.Max(contentBounds.yMax, scope.Owner.Position.y + scope.Owner.Size.y)
                + GraphPresentationMetrics.FlowCompletionGap;
            float completionX = scope.Owner.Position.x + (scope.Owner.Size.x - scope.CompletionSize.x) * 0.5f;
            scope.CompletionPosition = new Vector2(completionX, completionY);
            scope.RailX = contentBounds.xMin - GraphPresentationMetrics.SequenceRailOffset;
            scope.RailStartY = scope.Owner.Position.y + scope.Owner.Size.y * 0.5f;
            scope.RailEndY = completionY + scope.CompletionSize.y * 0.5f;

            Rect completionBounds = new(scope.CompletionPosition, scope.CompletionSize);
            Rect bounds = Union(contentBounds, completionBounds);
            bounds.xMin = Mathf.Min(bounds.xMin, scope.RailX);
            scope.Bounds = bounds;
        }

        private static Rect Union(Rect left, Rect right)
        {
            return Rect.MinMaxRect(
                Mathf.Min(left.xMin, right.xMin),
                Mathf.Min(left.yMin, right.yMin),
                Mathf.Max(left.xMax, right.xMax),
                Mathf.Max(left.yMax, right.yMax));
        }
    }
}
