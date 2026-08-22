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
    /// Describes the presentation data for a Flow that has an authored predicate subtree.
    /// The scope records the semantic predicate members separately from the visual roots
    /// that the graph canvas must render.
    /// </summary>
    internal interface IGraphPredicateScope
    {
        /// <summary>Gets the Flow presentation that owns this predicate scope.</summary>
        GraphPresentationItem Owner { get; }

        /// <summary>
        /// Gets the first authored predicate item referenced by the owner, or <see langword="null"/>
        /// when the owner has no resolvable predicate root.
        /// </summary>
        GraphPresentationItem PredicateRoot { get; }

        /// <summary>
        /// Gets all resolved presentation items that belong to the predicate subtree,
        /// including decorator items and their real child presentations, in discovery order.
        /// </summary>
        IReadOnlyList<GraphPresentationItem> PredicateMembers { get; }

        /// <summary>
        /// Gets the top-level semantic roots expanded by the current predicate visual host.
        /// A root may expand into Decorator wrapper cards and its real anchor card.
        /// </summary>
        IReadOnlyList<GraphPresentationItem> PredicateRoots { get; }

        /// <summary>
        /// Gets a value indicating whether the owner's own <c>VisualElement</c> hosts the
        /// predicate visuals. This is <see langword="true"/> for Condition; when it is
        /// <see langword="false"/>, the Canvas hosts the predicate visuals for the scope,
        /// as it does for Loop. This flag does not indicate whether a predicate exists or
        /// whether its visuals are currently visible.
        /// </summary>
        bool HostsPredicateVisuals { get; }

        /// <summary>
        /// Records the authored predicate root used by this scope before layout derives
        /// its presentation geometry.
        /// </summary>
        void SetPredicateRoot(GraphPresentationItem item);

        /// <summary>
        /// Adds a resolved presentation item to the semantic predicate-member collection.
        /// Duplicate items are ignored by the scope implementation.
        /// </summary>
        void AddPredicateMember(GraphPresentationItem item);

        /// <summary>
        /// Adds a predicate item as a top-level semantic root for the current visual host.
        /// Duplicate roots are ignored by the scope implementation.
        /// </summary>
        void AddPredicateVisualRoot(GraphPresentationItem item);
    }

    /// <summary>Derived canvas-only structure of Decorator wrapper cards around their direct child.</summary>
    internal sealed class GraphDecoratorStack
    {
        private readonly List<GraphPresentationItem> badges = new();

        internal GraphDecoratorStack(GraphPresentationItem anchor)
        {
            Anchor = anchor ?? throw new ArgumentNullException(nameof(anchor));
            Anchor.AttachDecoratorStack(this);
        }

        internal GraphPresentationItem Anchor { get; }
        internal IReadOnlyList<GraphPresentationItem> Badges => badges;

        /// <summary>Gets the descriptor whose persisted position owns this stack.</summary>
        internal GraphNodeDescriptor PlacementOwner => Anchor.Node ?? badges.FirstOrDefault()?.Node;

        /// <summary>Gets the badge-and-anchor bounds used by layout placement.</summary>
        internal Rect OwnBounds => CalculateOwnBounds();

        /// <summary>Gets the complete visual bounds, including an anchor Flow scope.</summary>
        internal Rect VisualBounds
        {
            get
            {
                Rect bounds = CalculateOwnBounds();
                if (Anchor.FlowScope != null)
                {
                    Rect flowBounds = GraphPresentationLayout.GetBoundsWithoutDecorator(Anchor);
                    bounds = GraphPresentationLayout.UnionBounds(bounds, flowBounds);
                }

                return bounds;
            }
        }

        /// <summary>Gets the anchor offset from the composite layout unit's top-left corner.</summary>
        internal Vector2 AnchorOffset => Anchor.Position - VisualBounds.position;

        /// <summary>Gets the anchor offset within only the badges and anchor card.</summary>
        internal Vector2 OwnAnchorOffset => Anchor.Position - OwnBounds.position;

        /// <summary>Applies a composite layout position and normalizes all wrapper descriptors.</summary>
        internal void ApplyLayoutPosition(Vector2 compositePosition)
        {
            Vector2 anchorPosition = compositePosition + AnchorOffset;
            ApplyAnchorPosition(anchorPosition);
        }

        /// <summary>Applies an Auto Layout position without including the wrapped Flow's stale scope bounds.</summary>
        internal void ApplyOwnLayoutPosition(Vector2 compositePosition)
        {
            Vector2 anchorPosition = compositePosition + OwnAnchorOffset;
            ApplyAnchorPosition(anchorPosition);
        }

        private void ApplyAnchorPosition(Vector2 anchorPosition)
        {
            Anchor.Position = anchorPosition;
            if (Anchor.Node != null)
            {
                Anchor.Node.Position = anchorPosition;
            }

            foreach (GraphPresentationItem badge in badges)
            {
                badge.Position = anchorPosition;
                if (badge.Node != null)
                {
                    badge.Node.Position = anchorPosition;
                }
            }
        }

        /// <summary>Synchronizes an empty child anchor with the outer decorator that owns free placement.</summary>
        internal void RefreshEmptyAnchorPosition()
        {
            if (Anchor.DecoratorPlaceholder != null && badges.Count > 0 && badges[0].Node != null)
            {
                Anchor.Position = badges[0].Node.Position;
            }
        }

        /// <summary>Moves attached Decorator wrapper cards with their anchor during an in-memory move.</summary>
        internal void OffsetAttachedBadges(Vector2 delta)
        {
            if (delta == Vector2.zero)
            {
                return;
            }

            foreach (GraphPresentationItem badge in badges)
            {
                badge.Position += delta;
            }
        }

        internal bool Contains(UUID uuid)
        {
            return Anchor.TargetUUID == uuid || badges.Exists(item => item.TargetUUID == uuid);
        }

        /// <summary>Reports whether an item is one of this structure's Decorator wrapper cards.</summary>
        internal bool ContainsWrapper(GraphPresentationItem item)
        {
            return item != null && badges.Contains(item);
        }

        internal void AddBadge(GraphPresentationItem badge)
        {
            if (badge != null)
            {
                badges.Add(badge);
                badge.AttachDecoratorStack(this);
            }
        }

        private Rect CalculateOwnBounds()
        {
            Rect bounds = GetCardBounds(Anchor);
            foreach (GraphPresentationItem badge in badges)
            {
                bounds = GraphPresentationLayout.UnionBounds(
                    bounds,
                    GetCardBounds(badge));
            }

            return bounds;
        }

        /// <summary>Gets one card's bounds without expanding its derived Flow scope.</summary>
        private static Rect GetCardBounds(GraphPresentationItem item)
        {
            return item == null
                ? new Rect(Vector2.zero, GraphPresentationMetrics.ReferenceItemSize)
                : new Rect(item.Position, item.Size);
        }
    }

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
    internal abstract class GraphOrderedScope : GraphFlowScope
    {
        protected GraphOrderedScope(GraphPresentationItem owner) : base(owner)
        {
        }

        /// <summary>Gets or sets the derived bracket rail x coordinate.</summary>
        internal float RailX { get; set; }

        /// <summary>Gets or sets the derived bracket start y coordinate.</summary>
        internal float RailStartY { get; set; }

        /// <summary>Gets or sets the derived bracket end y coordinate.</summary>
        internal float RailEndY { get; set; }
    }

    /// <summary>Derived short-circuit AND scope for one free Sequence presentation.</summary>
    internal sealed class GraphSequenceScope : GraphOrderedScope
    {
        internal GraphSequenceScope(GraphPresentationItem owner) : base(owner)
        {
        }

        internal float FailureRailX { get; set; }
    }

    /// <summary>Derived full-execution scope for one Aggregate presentation.</summary>
    internal sealed class GraphAggregateScope : GraphOrderedScope
    {
        internal GraphAggregateScope(GraphPresentationItem owner, Aggregate.ResultMode resultMode) : base(owner)
        {
            ResultMode = resultMode;
        }

        internal Aggregate.ResultMode ResultMode { get; }
    }

    /// <summary>
    /// Derived bracket and completion state for one free Condition presentation.
    /// </summary>
    internal sealed class GraphConditionScope : GraphFlowScope, IGraphPredicateScope
    {
        private readonly List<GraphPresentationItem> predicateMembers = new();
        private readonly List<GraphPresentationItem> predicateRoots = new();
        private readonly List<GraphConditionScope> nestedPredicateScopes = new();
        internal GraphConditionScope(GraphPresentationItem owner) : base(owner)
        {
        }

        GraphPresentationItem IGraphPredicateScope.Owner => Owner;
        GraphPresentationItem IGraphPredicateScope.PredicateRoot => PredicateRoot;
        IReadOnlyList<GraphPresentationItem> IGraphPredicateScope.PredicateMembers => PredicateMembers;
        IReadOnlyList<GraphPresentationItem> IGraphPredicateScope.PredicateRoots => PredicateRoots;
        bool IGraphPredicateScope.HostsPredicateVisuals => true;
        void IGraphPredicateScope.SetPredicateRoot(GraphPresentationItem item) => SetPredicateRoot(item);
        void IGraphPredicateScope.AddPredicateMember(GraphPresentationItem item) => AddPredicateMember(item);
        void IGraphPredicateScope.AddPredicateVisualRoot(GraphPresentationItem item) => AddPredicateVisualRoot(item);

        /// <summary>Gets the True branch item or its presentation-only placeholder.</summary>
        internal GraphPresentationItem TrueBranch { get; private set; }

        /// <summary>Gets the False branch item or its presentation-only placeholder.</summary>
        internal GraphPresentationItem FalseBranch { get; private set; }

        /// <summary>Gets the first predicate item directly referenced by this Condition.</summary>
        internal GraphPresentationItem PredicateRoot { get; private set; }

        /// <summary>Gets all real structural members included by this Condition presentation.</summary>
        internal IReadOnlyList<GraphPresentationItem> PredicateMembers => predicateMembers;

        /// <summary>Gets the top-level visual roots rendered inside the Condition shell.</summary>
        internal IReadOnlyList<GraphPresentationItem> PredicateRoots => predicateRoots;

        /// <summary>Gets the predicate scope that directly contains this Condition shell.</summary>
        internal GraphConditionScope ParentPredicateScope { get; private set; }

        /// <summary>Gets complete nested Condition scopes directly contained by this predicate.</summary>
        internal IReadOnlyList<GraphConditionScope> NestedPredicateScopes => nestedPredicateScopes;

        /// <summary>Gets the derived bounding rectangle of the full predicate subtree.</summary>
        internal Rect PredicateBounds { get; set; }

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

        /// <summary>Records the authored predicate root before layout derives the Condition shell geometry.</summary>
        internal void SetPredicateRoot(GraphPresentationItem item)
        {
            PredicateRoot = item;
            if (item != null && !predicateRoots.Contains(item))
            {
                predicateRoots.Add(item);
            }
        }

        /// <summary>Registers one presentation item as part of this editor-only predicate subtree.</summary>
        internal void AddPredicateMember(GraphPresentationItem item)
        {
            if (item != null && !predicateMembers.Contains(item))
            {
                predicateMembers.Add(item);
            }
        }

        /// <summary>Registers one top-level visual root rendered by this Condition shell.</summary>
        internal void AddPredicateVisualRoot(GraphPresentationItem item)
        {
            if (item != null && !predicateRoots.Contains(item))
            {
                predicateRoots.Add(item);
            }
        }

        /// <summary>Registers one nested Condition as an opaque predicate shell owned by this scope.</summary>
        internal void AddNestedPredicateScope(GraphConditionScope nested)
        {
            if (nested == null || ReferenceEquals(nested, this) || nestedPredicateScopes.Contains(nested))
            {
                return;
            }

            nested.ParentPredicateScope = this;
            nestedPredicateScopes.Add(nested);
        }

        /// <summary>Offsets cached predicate geometry when this embedded Condition shell moves.</summary>
        internal void OffsetPredicateGeometry(Vector2 delta)
        {
            if (PredicateBounds.size != Vector2.zero)
            {
                PredicateBounds = new Rect(PredicateBounds.position + delta, PredicateBounds.size);
            }

            if (Bounds.size == Vector2.zero)
            {
                return;
            }

            Bounds = new Rect(Bounds.position + delta, Bounds.size);
            CompletionPosition += delta;
            LeftX += delta.x;
            RightX += delta.x;
            BracketTopY += delta.y;
            BracketBottomY += delta.y;
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
                return "Pick one";
            }

            VariableField<int> field = pseudo.maxConsecutiveBranch;
            if (field == null || field.IsConstant)
            {
                int value = field?.Constant ?? -1;
                return value > 0 ? $"Max streak: {value}" : "No streak limit";
            }

            string name = tree ? tree.GetVariableDescName(field.UUID) : VariableData.MISSING_VARIABLE_NAME;
            return $"Max streak: ${name}";
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
    internal sealed class GraphLoopScope : GraphFlowScope, IGraphPredicateScope
    {
        private readonly List<GraphPresentationItem> body = new();
        private readonly List<GraphPresentationItem> predicateMembers = new();
        private readonly List<GraphPresentationItem> predicateRoots = new();

        internal GraphLoopScope(GraphPresentationItem owner) : base(owner)
        {
        }

        GraphPresentationItem IGraphPredicateScope.Owner => Owner;
        GraphPresentationItem IGraphPredicateScope.PredicateRoot => PredicateRoot;
        IReadOnlyList<GraphPresentationItem> IGraphPredicateScope.PredicateMembers => PredicateMembers;
        IReadOnlyList<GraphPresentationItem> IGraphPredicateScope.PredicateRoots => PredicateRoots;
        bool IGraphPredicateScope.HostsPredicateVisuals => false;
        void IGraphPredicateScope.SetPredicateRoot(GraphPresentationItem item) => SetPredicateRoot(item);
        void IGraphPredicateScope.AddPredicateMember(GraphPresentationItem item) => AddPredicateMember(item);
        void IGraphPredicateScope.AddPredicateVisualRoot(GraphPresentationItem item) => AddPredicateVisualRoot(item);

        /// <summary>Gets the authored Loop mode.</summary>
        internal Loop.LoopType Mode => ((Loop)Owner.Node.Node).loopType;

        /// <summary>Gets the condition card, placeholder, or derived count check.</summary>
        internal GraphPresentationItem Condition { get; private set; }

        /// <summary>Gets the authored condition root when this loop uses a predicate.</summary>
        internal GraphPresentationItem PredicateRoot { get; private set; }

        /// <summary>Gets the structural condition members embedded by this loop presentation.</summary>
        internal IReadOnlyList<GraphPresentationItem> PredicateMembers => predicateMembers;

        /// <summary>Gets the top-level visual roots rendered in the loop condition area.</summary>
        internal IReadOnlyList<GraphPresentationItem> PredicateRoots => predicateRoots;

        /// <summary>Gets or sets the derived bounds of the embedded loop condition predicate.</summary>
        internal Rect PredicateBounds { get; set; }

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

        /// <summary>Registers the authored condition root before deriving its compact presentation.</summary>
        internal void SetPredicateRoot(GraphPresentationItem item)
        {
            PredicateRoot = item;
            if (item != null && !predicateRoots.Contains(item))
            {
                predicateRoots.Add(item);
            }
        }

        /// <summary>Registers one item that belongs to the loop condition predicate subtree.</summary>
        internal void AddPredicateMember(GraphPresentationItem item)
        {
            if (item != null && !predicateMembers.Contains(item))
            {
                predicateMembers.Add(item);
            }
        }

        /// <summary>Registers one predicate item that has no embedded visual parent.</summary>
        internal void AddPredicateVisualRoot(GraphPresentationItem item)
        {
            if (item != null && !predicateRoots.Contains(item))
            {
                predicateRoots.Add(item);
            }
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
}
