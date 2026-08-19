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
        private GraphDecoratorStack decoratorStack;

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
            GraphDecoratorPlaceholder decoratorPlaceholder = null,
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
            DecoratorPlaceholder = decoratorPlaceholder;
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

        /// <summary>Creates one non-persistent empty Decorator child slot.</summary>
        internal static GraphPresentationItem CreateDecoratorPlaceholder(GraphDecoratorPlaceholder placeholder)
        {
            if (placeholder == null) throw new ArgumentNullException(nameof(placeholder));
            return new GraphPresentationItem(GraphPresentationKind.DecoratorPlaceholder, null, UUID.Empty,
                placeholder.Tooltip, isRoot: true, decoratorPlaceholder: placeholder);
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

        /// <summary>Creates one non-owning visual reference to an authored node already placed elsewhere.</summary>
        internal static GraphPresentationItem CreateReferenceProxy(GraphNodeDescriptor target, string warning)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            return new GraphPresentationItem(
                GraphPresentationKind.ReferenceProxy,
                target,
                target.UUID,
                warning,
                isRoot: false);
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
        internal string Warning { get; private set; }

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

        /// <summary>Gets presentation-only empty Decorator child metadata, when applicable.</summary>
        internal GraphDecoratorPlaceholder DecoratorPlaceholder { get; }

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

        /// <summary>Gets the derived decorator stack that owns this item, when applicable.</summary>
        internal GraphDecoratorStack DecoratorStack => decoratorStack;

        /// <summary>Associates this presentation item with one derived decorator stack.</summary>
        internal void AttachDecoratorStack(GraphDecoratorStack stack)
        {
            decoratorStack = stack;
        }

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

        /// <summary>Gets the derived Aggregate scope, when this item is an Aggregate.</summary>
        internal GraphAggregateScope AggregateScope => FlowScope as GraphAggregateScope;

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
        internal GraphPresentationEndpoint Completion => DecoratorStack?.ContainsWrapper(this) == true
            ? DecoratorStack.Anchor.Completion
            : FlowScope != null ? FlowComplete : Output;

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

        /// <summary>Appends one presentation-only diagnostic without replacing topology warnings.</summary>
        internal void AppendWarning(string warning)
        {
            if (string.IsNullOrWhiteSpace(warning))
            {
                return;
            }

            Warning = string.IsNullOrEmpty(Warning) ? warning : Warning + ", " + warning;
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
        private readonly BehaviourTreeData tree;
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
            GraphPresentationItem exit = null,
            BehaviourTreeData tree = null)
        {
            this.roots = roots;
            this.primaryByUUID = primaryByUUID;
            this.relations = relations;
            this.completionScopes = completionScopes;
            this.serviceScopes = serviceScopes ?? new List<GraphServiceScope>();
            this.decoratorStacks = decoratorStacks ?? new List<GraphDecoratorStack>();
            this.tree = tree;
            this.entrance = entrance;
            this.exit = exit;
        }

        /// <summary>Gets all top-level real cards and presentation-only placeholders.</summary>
        internal IReadOnlyList<GraphPresentationItem> Roots => roots;

        /// <summary>Gets every authored presentation item, including members embedded by a structure.</summary>
        internal IEnumerable<GraphPresentationItem> Items => primaryByUUID.Values;

        /// <summary>Gets all semantic presentation relations.</summary>
        internal IReadOnlyList<GraphPresentationRelation> Relations => relations;

        /// <summary>Gets all composite Flow scopes with derived completion markers.</summary>
        internal IReadOnlyList<GraphFlowScope> CompletionScopes => completionScopes;

        /// <summary>Gets the unique first-placement Service scopes.</summary>
        internal IReadOnlyList<GraphServiceScope> ServiceScopes => serviceScopes;

        /// <summary>Gets canvas-only stacks that attach decorator badges to real child cards.</summary>
        internal IReadOnlyList<GraphDecoratorStack> DecoratorStacks => decoratorStacks;

        /// <summary>Gets the authored tree used to resolve presentation-only semantic titles.</summary>
        internal BehaviourTreeData Tree => tree;

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

        /// <summary>
        /// Resolves the single visible continuation source for one semantic relation. All edge,
        /// port, and layout consumers share this method so an outer Sequence reads the same
        /// exit as the decorated child's real card.
        /// </summary>
        internal GraphPresentationEndpoint ResolveContinuationSource(GraphPresentationRelation relation)
        {
            if (relation == null || !relation.Source.IsValid)
            {
                return relation?.Source ?? default;
            }

            // Internal Decorator.node relations stay exactly where the authored wrapper port is.
            if (relation.Origin?.FieldName == nameof(Decorator.node))
            {
                return relation.Source;
            }

            GraphPresentationItem source = relation.Source.Item;
            GraphDecoratorStack stack = source?.DecoratorStack;
            if (stack == null || !stack.ContainsWrapper(source))
            {
                return relation.Source;
            }

            GraphPresentationItem anchor = stack.Anchor;
            if (anchor == null)
            {
                return relation.Source;
            }

            if (anchor.DecoratorPlaceholder != null)
            {
                return anchor.Output;
            }

            return anchor.FlowScope != null ? anchor.FlowComplete : anchor.Output;
        }

        /// <summary>Expands one semantic visual root into the cards that represent it on the canvas.</summary>
        internal IEnumerable<GraphPresentationItem> ResolveVisualItems(GraphPresentationItem root)
        {
            if (root == null)
            {
                yield break;
            }

            GraphDecoratorStack stack = FindDecoratorStack(root.TargetUUID);
            if (stack == null || stack.Badges.Count == 0 || !ReferenceEquals(stack.Badges[0], root))
            {
                yield return root;
                yield break;
            }

            foreach (GraphPresentationItem badge in stack.Badges)
            {
                yield return badge;
            }

            yield return stack.Anchor;
        }

        /// <summary>Reports whether one presentation item is rendered as a decorator badge.</summary>
        internal bool IsDecoratorBadge(GraphPresentationItem item)
        {
            GraphDecoratorStack stack = item == null ? null : FindDecoratorStack(item.TargetUUID);
            if (stack == null)
            {
                return false;
            }

            foreach (GraphPresentationItem badge in stack.Badges)
            {
                if (ReferenceEquals(badge, item))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Enumerates predicate scopes without exposing their concrete owning Flow type.</summary>
        internal IEnumerable<IGraphPredicateScope> PredicateScopes
        {
            get
            {
                foreach (GraphFlowScope scope in completionScopes)
                {
                    if (scope is IGraphPredicateScope predicateScope)
                    {
                        yield return predicateScope;
                    }
                }
            }
        }

        /// <summary>Resolves one authored item to the single descriptor that owns movable placement.</summary>
        /// <param name="uuid">The authored or presentation UUID.</param>
        /// <returns>The canonical movable descriptor, or null for non-movable presentation items.</returns>
        internal GraphNodeDescriptor ResolveMovableRoot(UUID uuid)
        {
            GraphPresentationItem item = Find(uuid);
            if (item == null)
            {
                return null;
            }

            // Loop predicates are positioned relative to their Loop shell, including decorator badges.
            foreach (GraphPresentationItem root in Roots)
            {
                if (root.LoopScope == null)
                {
                    continue;
                }

                foreach (GraphPresentationItem predicate in root.LoopScope.PredicateMembers)
                {
                    if (ReferenceEquals(predicate, item))
                    {
                        return root.Node;
                    }
                }

                foreach (GraphPresentationItem body in root.LoopScope.Body)
                {
                    if (ReferenceEquals(body, item) || ContainsFlowScopeItem(body.FlowScope, item, new HashSet<GraphFlowScope>()))
                    {
                        return root.Node;
                    }
                }
            }

            GraphDecoratorStack decorator = FindDecoratorStack(uuid);
            if (decorator?.Anchor.Node != null)
            {
                return decorator.Anchor.Node;
            }

            if (decorator?.Anchor.DecoratorPlaceholder != null && decorator.Badges.Count > 0)
            {
                // Any badge in a childless stack drags the free stack through its outer wrapper.
                return decorator.Badges[0].Node;
            }

            foreach (GraphPresentationItem root in Roots)
            {
                IReadOnlyList<GraphPresentationItem> predicateMembers = root.ConditionScope?.PredicateMembers
                    ?? root.LoopScope?.PredicateMembers;
                IReadOnlyList<GraphPresentationItem> predicateRoots = root.ConditionScope?.PredicateRoots
                    ?? root.LoopScope?.PredicateRoots;
                if (predicateMembers == null)
                {
                    continue;
                }

                foreach (GraphPresentationItem predicate in predicateMembers)
                {
                    if (ReferenceEquals(predicate, item))
                    {
                        return root.Node;
                    }
                }

                foreach (GraphPresentationItem predicate in predicateRoots)
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

        /// <summary>Reports whether one authored item is contained by a nested Flow scope.</summary>
        private static bool ContainsFlowScopeItem(
            GraphFlowScope scope,
            GraphPresentationItem target,
            ISet<GraphFlowScope> visited)
        {
            if (scope == null || target == null || !visited.Add(scope))
            {
                return false;
            }

            foreach (GraphPresentationItem member in scope.Members)
            {
                if (ReferenceEquals(member, target) || ContainsFlowScopeItem(member.FlowScope, target, visited))
                {
                    return true;
                }
            }

            return false;
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

        /// <summary>Moves one embedded item and all presentation content it owns by the same delta.</summary>
        internal void MoveEmbeddedItem(GraphPresentationItem item, Vector2 position)
        {
            if (item == null)
            {
                return;
            }

            Offset(item, position - item.Position);
        }

        private static void Offset(GraphPresentationItem item, Vector2 delta)
        {
            Offset(item, delta, new HashSet<GraphPresentationItem>());
        }

        private static void Offset(
            GraphPresentationItem item,
            Vector2 delta,
            ISet<GraphPresentationItem> visited)
        {
            if (item == null || !visited.Add(item))
            {
                return;
            }

            item.Position += delta;
            if (item.DecoratorStack?.Anchor == item)
            {
                item.DecoratorStack.OffsetAttachedBadges(delta);
            }
            item.ConditionScope?.OffsetPredicateGeometry(delta);
            foreach (GraphPresentationSlot slot in item.Slots)
            {
                if (slot.Content != null)
                {
                    Offset(slot.Content, delta, visited);
                }
            }

            // A nested shell owns the complete visual scope even though branch roots are not predicate slots.
            foreach (GraphPresentationItem member in item.FlowScope?.Members ?? Array.Empty<GraphPresentationItem>())
            {
                Offset(member, delta, visited);
            }

            if (item.ConditionScope != null)
            {
                foreach (GraphPresentationItem predicate in item.ConditionScope.PredicateRoots)
                {
                    if (predicate.Parent == null)
                    {
                        Offset(predicate, delta, visited);
                    }
                }
            }

            if (item.LoopScope != null)
            {
                foreach (GraphPresentationItem predicate in item.LoopScope.PredicateRoots)
                {
                    if (predicate.Parent == null)
                    {
                        Offset(predicate, delta, visited);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Converts topology references into free-node semantic relations.
    /// </summary>
}
