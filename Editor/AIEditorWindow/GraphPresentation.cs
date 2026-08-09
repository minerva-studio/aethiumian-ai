using Aethiumian.AI.Nodes;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
    /// <summary>
    /// Editor-only semantic presentation role for a graph item.
    /// </summary>
    internal enum GraphPresentationKind
    {
        Card,
        Sequence,
        Decision,
        Condition,
        ReferenceProxy,
        Missing,
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
        ProbabilityBranch,
        ParallelBranch,
        ConditionTrue,
        ConditionFalse,
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
            int occurrenceId)
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
    /// Shared editor-only scope for a composite Flow with a derived completion marker.
    /// </summary>
    internal abstract class GraphFlowScope
    {
        private static readonly Vector2 defaultCompletionSize = new(156f, 24f);
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
        internal virtual Vector2 CompletionSize => defaultCompletionSize;

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
            bool isRoot = true)
        {
            Kind = kind;
            Node = node;
            TargetUUID = targetUUID;
            Warning = warning;
            IsRoot = isRoot;
            Position = node?.Position ?? Vector2.zero;
        }

        /// <summary>Gets the semantic presentation kind.</summary>
        internal GraphPresentationKind Kind { get; }

        /// <summary>Gets the source node descriptor.</summary>
        internal GraphNodeDescriptor Node { get; }

        /// <summary>Gets the referenced UUID for proxy or missing items.</summary>
        internal UUID TargetUUID { get; }

        /// <summary>Gets the parent compound item, if any.</summary>
        internal GraphPresentationItem Parent { get; private set; }

        /// <summary>Gets whether the item is a top-level movable item.</summary>
        internal bool IsRoot { get; }

        /// <summary>Gets the warning associated with this item.</summary>
        internal string Warning { get; }

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

        internal GraphPresentation(
            List<GraphPresentationItem> roots,
            Dictionary<UUID, GraphPresentationItem> primaryByUUID,
            List<GraphPresentationRelation> relations,
            List<GraphFlowScope> completionScopes)
        {
            this.roots = roots;
            this.primaryByUUID = primaryByUUID;
            this.relations = relations;
            this.completionScopes = completionScopes;
        }

        /// <summary>Gets all top-level free items.</summary>
        internal IReadOnlyList<GraphPresentationItem> Roots => roots;

        /// <summary>Gets all semantic presentation relations.</summary>
        internal IReadOnlyList<GraphPresentationRelation> Relations => relations;

        /// <summary>Gets all composite Flow scopes with derived completion markers.</summary>
        internal IReadOnlyList<GraphFlowScope> CompletionScopes => completionScopes;

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
            }

            HashSet<UUID> embedded = new();
            List<GraphPresentationRelation> relations = new();
            foreach (GraphNodeDescriptor descriptor in topology.Nodes)
            {
                IReadOnlyList<GraphEdgeDescriptor> outgoing = GetOutgoing(topology, descriptor);
                BuildRelations(primary[descriptor.UUID], outgoing, primary, embedded, relations);
            }

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

            return new GraphPresentation(roots, primary, relations, completionScopes);
        }

        private static void BuildRelations(
            GraphPresentationItem source,
            IReadOnlyList<GraphEdgeDescriptor> outgoing,
            IReadOnlyDictionary<UUID, GraphPresentationItem> primary,
            ISet<UUID> embedded,
            ICollection<GraphPresentationRelation> relations)
        {
            if (source.Node.Node is Condition)
            {
                BuildCondition(source, outgoing, primary, embedded, relations);
                return;
            }

            if (source.Node.Node is Sequence)
            {
                BuildSequence(source, outgoing, primary, relations);
                return;
            }

            GraphPresentationRelationKind branchKind = source.Node.Node is Decision
                ? GraphPresentationRelationKind.DecisionBranch
                : source.Node.Node is Probability or PseudoProbability
                    ? GraphPresentationRelationKind.ProbabilityBranch
                    : source.Node.Node is Parallel
                        ? GraphPresentationRelationKind.ParallelBranch
                        : GraphPresentationRelationKind.Structural;

            foreach (GraphEdgeDescriptor edge in outgoing)
            {
                GraphPresentationRelationKind kind = edge.Kind == GraphEdgeKind.Child
                    ? branchKind
                    : ConvertTopologyKind(edge.Kind);
                string label = edge.Kind == GraphEdgeKind.Child ? BuildBranchLabel(edge, kind) : edge.Label;
                relations.Add(CreateTopologyRelation(source.Output, edge, primary, kind, label));
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

        private static void BuildCondition(
            GraphPresentationItem source,
            IReadOnlyList<GraphEdgeDescriptor> outgoing,
            IReadOnlyDictionary<UUID, GraphPresentationItem> primary,
            ISet<UUID> embedded,
            ICollection<GraphPresentationRelation> relations)
        {
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

                GraphPresentationRelationKind kind = edge.Label switch
                {
                    "trueNode" => GraphPresentationRelationKind.ConditionTrue,
                    "falseNode" => GraphPresentationRelationKind.ConditionFalse,
                    _ => ConvertTopologyKind(edge.Kind),
                };
                string label = edge.Label switch
                {
                    "trueNode" => "True",
                    "falseNode" => "False",
                    _ => edge.Label,
                };
                relations.Add(CreateTopologyRelation(source.Output, edge, primary, kind, label));
            }
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
                Decision => GraphPresentationKind.Decision,
                Condition => GraphPresentationKind.Condition,
                _ => GraphPresentationKind.Card,
            };
        }
    }

    /// <summary>
    /// Measures free nodes, Condition compounds, and derived Sequence scopes.
    /// </summary>
    internal static class GraphPresentationLayout
    {
        private const float ConditionPadding = 20f;
        private const float ConditionHeader = 34f;
        private const float SequenceRailOffset = 24f;
        private const float SequenceCompletionGap = 52f;

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

            HashSet<GraphSequenceScope> resolved = new();
            HashSet<GraphSequenceScope> visiting = new();
            foreach (GraphFlowScope candidate in presentation.CompletionScopes)
            {
                if (candidate is GraphSequenceScope scope)
                {
                    ResolveScope(scope, resolved, visiting);
                }
            }
        }

        /// <summary>Gets the default card size for an item.</summary>
        internal static Vector2 GetItemSize(GraphPresentationItem item)
        {
            return item?.Node == null
                ? new Vector2(220f, 52f)
                : GraphLayoutResolver.GetNodeSize(item.Node);
        }

        /// <summary>Gets the complete bounds of an item, including its composite Flow scope.</summary>
        internal static Rect GetBounds(GraphPresentationItem item)
        {
            if (item?.FlowScope != null)
            {
                return item.FlowScope.Bounds;
            }

            return item == null ? new Rect(0f, 0f, 220f, 52f) : new Rect(item.Position, item.Size);
        }

        private static Vector2 Measure(GraphPresentationItem item)
        {
            if (item == null)
            {
                return new Vector2(220f, 52f);
            }

            if (!item.IsContainer)
            {
                item.Size = GetItemSize(item);
                return item.Size;
            }

            GraphPresentationItem predicate = item.Slots.Count > 0 ? item.Slots[0].Content : null;
            Vector2 predicateSize = Measure(predicate);
            item.Size = new Vector2(
                Mathf.Max(280f, predicateSize.x + ConditionPadding * 2f),
                ConditionHeader + predicateSize.y + ConditionPadding * 2f);
            item.Position = item.Node?.Position ?? Vector2.zero;
            if (predicate != null)
            {
                predicate.Position = item.Position + new Vector2(ConditionPadding, ConditionHeader + ConditionPadding);
            }

            return item.Size;
        }

        private static void ResolveScope(
            GraphSequenceScope scope,
            ISet<GraphSequenceScope> resolved,
            ISet<GraphSequenceScope> visiting)
        {
            if (scope == null || resolved.Contains(scope))
            {
                return;
            }

            Rect ownerBounds = new(scope.Owner.Position, scope.Owner.Size);
            if (!visiting.Add(scope))
            {
                SetScopeBounds(scope, ownerBounds);
                return;
            }

            Rect contentBounds = ownerBounds;
            foreach (GraphPresentationItem member in scope.Members)
            {
                if (member?.FlowScope is GraphSequenceScope memberScope)
                {
                    ResolveScope(memberScope, resolved, visiting);
                }

                contentBounds = Union(contentBounds, GetBounds(member));
            }

            SetScopeBounds(scope, contentBounds);
            visiting.Remove(scope);
            resolved.Add(scope);
        }

        private static void SetScopeBounds(GraphSequenceScope scope, Rect contentBounds)
        {
            float completionY = Mathf.Max(contentBounds.yMax, scope.Owner.Position.y + scope.Owner.Size.y) + SequenceCompletionGap;
            float completionX = scope.Owner.Position.x + (scope.Owner.Size.x - scope.CompletionSize.x) * 0.5f;
            scope.CompletionPosition = new Vector2(completionX, completionY);
            scope.RailX = contentBounds.xMin - SequenceRailOffset;
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
