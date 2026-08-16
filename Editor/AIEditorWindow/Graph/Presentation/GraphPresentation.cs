using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
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

        /// <summary>Creates one editor-only graph boundary item.</summary>
        internal static GraphPresentationItem CreateBoundary(GraphPresentationKind kind)
        {
            if (kind is not (GraphPresentationKind.Entrance or GraphPresentationKind.Exit))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Only graph boundary kinds are valid.");
            }

            return new GraphPresentationItem(kind, null, UUID.Empty, string.Empty, isRoot: true);
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

        /// <summary>Gets or sets whether the initial position came from persisted boundary layout data.</summary>
        internal bool HasExplicitPosition { get; set; }

        /// <summary>Gets or sets the measured unscaled size.</summary>
        internal Vector2 Size { get; set; }

        /// <summary>Gets or sets the derived semantic visual for a Boolean or Constant leaf.</summary>
        internal GraphLeafVisualDescriptor LeafVisual { get; set; }

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
        private readonly List<GraphDecoratorStack> decoratorStacks;
        private readonly GraphPresentationItem entrance;
        private readonly GraphPresentationItem exit;

        internal GraphPresentation(
            List<GraphPresentationItem> roots,
            Dictionary<UUID, GraphPresentationItem> primaryByUUID,
            List<GraphPresentationRelation> relations,
            List<GraphFlowScope> completionScopes,
            List<GraphServiceScope> serviceScopes = null,
            List<GraphDecoratorStack> decoratorStacks = null,
            GraphPresentationItem entrance = null,
            GraphPresentationItem exit = null)
        {
            this.roots = roots;
            this.primaryByUUID = primaryByUUID;
            this.relations = relations;
            this.completionScopes = completionScopes;
            this.serviceScopes = serviceScopes ?? new List<GraphServiceScope>();
            this.decoratorStacks = decoratorStacks ?? new List<GraphDecoratorStack>();
            this.entrance = entrance;
            this.exit = exit;
        }

        /// <summary>Gets all top-level real cards and presentation-only placeholders.</summary>
        internal IReadOnlyList<GraphPresentationItem> Roots => roots;

        /// <summary>Gets all semantic presentation relations.</summary>
        internal IReadOnlyList<GraphPresentationRelation> Relations => relations;

        /// <summary>Gets all composite Flow scopes with derived completion markers.</summary>
        internal IReadOnlyList<GraphFlowScope> CompletionScopes => completionScopes;

        /// <summary>Gets the unique first-placement Service scopes.</summary>
        internal IReadOnlyList<GraphServiceScope> ServiceScopes => serviceScopes;

        /// <summary>Gets canvas-only stacks that attach decorator badges to real child cards.</summary>
        internal IReadOnlyList<GraphDecoratorStack> DecoratorStacks => decoratorStacks;

        /// <summary>Finds the derived decorator stack containing one real presentation item.</summary>
        internal GraphDecoratorStack FindDecoratorStack(UUID uuid)
        {
            foreach (GraphDecoratorStack stack in decoratorStacks)
            {
                if (stack.Contains(uuid))
                {
                    return stack;
                }
            }

            return null;
        }

        /// <summary>Resolves one authored item to the single descriptor that owns movable placement.</summary>
        /// <param name="uuid">The authored or presentation UUID.</param>
        /// <returns>The canonical movable descriptor, or null for non-movable presentation items.</returns>
        internal GraphNodeDescriptor ResolveMovableRoot(UUID uuid)
        {
            GraphDecoratorStack decorator = FindDecoratorStack(uuid);
            if (decorator?.Anchor.Node != null)
            {
                return decorator.Anchor.Node;
            }

            GraphPresentationItem item = Find(uuid);
            if (item == null)
            {
                return null;
            }

            foreach (GraphPresentationItem root in Roots)
            {
                if (root.ConditionScope == null)
                {
                    continue;
                }

                foreach (GraphPresentationItem predicate in root.ConditionScope.PredicateMembers)
                {
                    if (ReferenceEquals(predicate, item))
                    {
                        return root.Node;
                    }
                }

                foreach (GraphPresentationItem predicate in root.ConditionScope.PredicateRoots)
                {
                    if (ReferenceEquals(predicate, item))
                    {
                        return root.Node;
                    }
                }
            }

            foreach (GraphPresentationItem root in Roots)
            {
                if (ReferenceEquals(root, item))
                {
                    return root.Node;
                }
            }

            return null;
        }

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

        /// <summary>Gets the editor-only Entrance boundary.</summary>
        internal GraphPresentationItem Entrance => entrance;

        /// <summary>Gets the editor-only Exit boundary.</summary>
        internal GraphPresentationItem Exit => exit;

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

            if (item.ConditionScope != null)
            {
                foreach (GraphPresentationItem predicate in item.ConditionScope.PredicateRoots)
                {
                    if (predicate.Parent == null)
                    {
                        Offset(predicate, delta);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Converts topology references into free-node semantic relations.
    /// </summary>
}
