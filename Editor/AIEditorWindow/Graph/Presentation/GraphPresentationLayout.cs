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
    /// Measures free nodes, Condition compounds, and derived Flow scopes.
    /// </summary>
    internal static class GraphPresentationLayout
    {
        private const float ExitBoundaryGap = 80f;
        /// <summary>Measures presentation items without modifying source descriptors.</summary>
        internal static void Layout(GraphPresentation presentation)
        {
            if (presentation == null)
            {
                return;
            }

            foreach (GraphPresentationItem item in presentation.Roots)
            {
                Measure(presentation, item);
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

            ResolveDecoratorStacks(presentation);
            PositionBoundaries(presentation);
        }

        /// <summary>Places boundary items around the configured Head when no persisted position exists.</summary>
        private static void PositionBoundaries(GraphPresentation presentation)
        {
            GraphPresentationItem entrance = presentation.Entrance;
            GraphPresentationItem exit = presentation.Exit;
            if (entrance == null || exit == null)
            {
                return;
            }

            entrance.Size = new Vector2(132f, 48f);
            exit.Size = entrance.Size;
            GraphPresentationItem head = presentation.Find(presentation.Relations
                .FirstOrDefault(relation => relation.Kind == GraphPresentationRelationKind.Entrance)?.TargetUUID ?? UUID.Empty);
            if (head == null)
            {
                head = presentation.Roots.FirstOrDefault(item => item.Node?.IsHead == true);
            }

            if (head != null)
            {
                Rect headCardBounds = new(head.Position, head.Size);
                // The entrance is an attached presentation badge, like a decorator, rather than an independently laid out card.
                entrance.Position = new Vector2(
                    headCardBounds.center.x - entrance.Size.x * 0.5f,
                    headCardBounds.yMin - entrance.Size.y + 1f);
            }
            else if (!entrance.HasExplicitPosition)
            {
                entrance.Position = Vector2.zero;
            }

            if (!exit.HasExplicitPosition)
            {
                Rect bounds = head == null ? new Rect(entrance.Position, entrance.Size) : GetBounds(head);
                exit.Position = new Vector2(bounds.center.x - exit.Size.x * 0.5f, bounds.yMax + ExitBoundaryGap);
            }
        }

        /// <summary>Attaches decorator badges above their real child without altering any descriptor position.</summary>
        private static void ResolveDecoratorStacks(GraphPresentation presentation)
        {
            foreach (GraphDecoratorStack stack in presentation.DecoratorStacks)
            {
                PositionDecoratorStack(stack);
            }
        }

        /// <summary>Positions one derived decorator stack immediately above its real child.</summary>
        private static void PositionDecoratorStack(GraphDecoratorStack stack)
        {
            GraphPresentationItem anchor = stack.Anchor;
            float bottom = anchor.Position.y;
            for (int index = stack.Badges.Count - 1; index >= 0; index--)
            {
                GraphPresentationItem badge = stack.Badges[index];
                badge.Size = GetDecoratorBadgeSize();
                bottom -= badge.Size.y;
                badge.Position = new Vector2(
                    anchor.Position.x + (anchor.Size.x - badge.Size.x) * 0.5f,
                    bottom);
            }
        }

        /// <summary>Returns the fixed canvas size of one attached decorator badge.</summary>
        private static Vector2 GetDecoratorBadgeSize()
        {
            return GraphPresentationMetrics.DecoratorNodeSize;
        }

        /// <summary>Gets the default card size for an item.</summary>
        internal static Vector2 GetItemSize(GraphPresentationItem item)
        {
            if (item?.LeafVisual != null)
            {
                return item.LeafVisual.Size;
            }

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

        private static Vector2 Measure(GraphPresentation presentation, GraphPresentationItem item)
        {
            if (item == null)
            {
                return GraphPresentationMetrics.ReferenceItemSize;
            }

            if (!item.IsContainer)
            {
                GraphDecoratorStack decoratorStack = presentation.FindDecoratorStack(item.TargetUUID);
                item.Size = decoratorStack?.Badges.Contains(item) == true
                    ? GetDecoratorBadgeSize()
                    : GetItemSize(item);
                return item.Size;
            }

            item.Position = item.Node?.Position ?? Vector2.zero;
            GraphConditionScope scope = item.ConditionScope;
            foreach (GraphPresentationItem predicate in scope?.PredicateRoots ?? Array.Empty<GraphPresentationItem>())
            {
                Measure(presentation, predicate);
            }

            Rect predicateBounds = LayoutConditionPredicate(presentation, item, scope);

            scope.PredicateBounds = predicateBounds;
            item.Size = GetConditionSize(item.Position, predicateBounds, GetConditionPredicatePadding(scope));

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

            if (!visiting.Add(scope))
            {
                Rect fallbackBounds = new(scope.Owner.Position, scope.Owner.Size);
                SetFallbackScopeBounds(scope, fallbackBounds);
                return;
            }

            if (scope is GraphConditionScope predicateOwner)
            {
                foreach (GraphPresentationItem predicate in predicateOwner.PredicateMembers)
                {
                    if (predicate?.FlowScope != null && !ReferenceEquals(predicate.FlowScope, scope))
                    {
                        ResolveScope(presentation, predicate.FlowScope, resolved, visiting);
                    }
                }

                ResolveConditionPredicateBounds(presentation, predicateOwner);
            }

            Rect ownerBounds = new(scope.Owner.Position, scope.Owner.Size);

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
                    ResolveLoopScope(presentation, loopScope, ownerBounds);
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

        /// <summary>Expands a Condition shell from the final geometry of its predicate subtree.</summary>
        private static void ResolveConditionPredicateBounds(GraphPresentation presentation, GraphConditionScope scope)
        {
            Rect predicateBounds = scope.PredicateBounds;
            bool hasPredicate = scope.PredicateRoot != null;
            foreach (GraphPresentationItem predicate in scope.PredicateMembers)
            {
                Rect bounds = GetBounds(predicate);
                GraphServiceScope serviceScope = presentation.FindServiceScope(predicate.TargetUUID);
                if (serviceScope != null)
                {
                    ResolveServiceScope(serviceScope);
                    bounds = Union(bounds, serviceScope.Bounds);
                }

                predicateBounds = hasPredicate ? Union(predicateBounds, bounds) : bounds;
                hasPredicate = true;
            }

            if (!hasPredicate)
            {
                predicateBounds = new Rect(
                    scope.Owner.Position + new Vector2(
                        GraphPresentationMetrics.ConditionPadding,
                        GraphPresentationMetrics.ConditionHeader + GraphPresentationMetrics.ConditionPadding),
                    GraphPresentationMetrics.DecoratorNodeSize);
            }

            float padding = GetConditionPredicatePadding(scope);
            Vector2 contentOrigin = scope.Owner.Position + new Vector2(
                padding,
                GraphPresentationMetrics.ConditionHeader + padding);
            Vector2 offset = new(
                Mathf.Max(0f, contentOrigin.x - predicateBounds.xMin),
                Mathf.Max(0f, contentOrigin.y - predicateBounds.yMin));
            if (scope.PredicateRoot != null && offset != Vector2.zero)
            {
                // Nested Flow bounds can grow to the left after branch resolution; shift the complete visual scope back inside its owner.
                presentation.MoveEmbeddedItem(scope.PredicateRoot, scope.PredicateRoot.Position + offset);
                predicateBounds = new Rect(predicateBounds.position + offset, predicateBounds.size);
            }

            scope.PredicateBounds = predicateBounds;
            scope.Owner.Size = GetConditionSize(
                scope.Owner.Position,
                predicateBounds,
                padding);
        }

        private static Vector2 GetConditionSize(Vector2 ownerPosition, Rect predicateBounds, float padding)
        {
            return new Vector2(
                Mathf.Max(
                    GraphPresentationMetrics.ConditionMinimumWidth,
                    predicateBounds.width + padding * 2f,
                    predicateBounds.xMax - ownerPosition.x + padding),
                Mathf.Max(
                    GraphPresentationMetrics.ConditionHeader + predicateBounds.height
                        + padding * 2f,
                    predicateBounds.yMax - ownerPosition.y + padding));
        }

        /// <summary>Returns additional shell clearance when a predicate contains complete nested Condition scopes.</summary>
        private static float GetConditionPredicatePadding(GraphConditionScope scope)
        {
            return scope?.NestedPredicateScopes.Count > 0
                ? GraphPresentationMetrics.ConditionNestedScopePadding
                : GraphPresentationMetrics.ConditionPadding;
        }

        /// <summary>
        /// Derives compact, owner-local positions for a Condition predicate without changing authored node positions.
        /// </summary>
        private static Rect LayoutConditionPredicate(GraphPresentation presentation, GraphPresentationItem owner, GraphConditionScope scope)
        {
            float padding = GetConditionPredicatePadding(scope);
            Vector2 origin = owner.Position + new Vector2(
                padding,
                GraphPresentationMetrics.ConditionHeader + padding);
            return LayoutPredicate(
                presentation,
                owner,
                scope?.PredicateRoot,
                scope?.PredicateMembers,
                origin,
                GraphPresentationMetrics.ConditionMinimumWidth,
                padding);
        }

        /// <summary>Lays out one embedded predicate subtree without mutating authored node positions.</summary>
        private static Rect LayoutPredicate(
            GraphPresentation presentation,
            GraphPresentationItem owner,
            GraphPresentationItem root,
            IReadOnlyList<GraphPresentationItem> predicateMembers,
            Vector2 origin,
            float minimumWidth,
            float padding)
        {
            if (root == null)
            {
                return new Rect(origin, GraphPresentationMetrics.DecoratorNodeSize);
            }

            HashSet<GraphPresentationItem> members = new(predicateMembers ?? Array.Empty<GraphPresentationItem>());
            Dictionary<GraphPresentationItem, List<GraphPresentationItem>> children = new();
            Dictionary<GraphPresentationItem, List<GraphPresentationItem>> services = new();
            foreach (GraphPresentationRelation relation in presentation.Relations)
            {
                if (relation.Kind == GraphPresentationRelationKind.Service)
                {
                    AddPredicateChild(relation.Source.Item, relation.Target.Item, members, services);
                }
                else if (relation.Role == GraphPresentationRelationRole.AuthoredReference
                    && relation.Kind != GraphPresentationRelationKind.Raw)
                {
                    AddPredicateChild(relation.Source.Item, relation.Target.Item, members, children);
                }
            }

            Dictionary<GraphPresentationItem, PredicateEnvelope> envelopes = new();
            MeasurePredicate(root, children, services, envelopes, new HashSet<GraphPresentationItem>());
            Dictionary<GraphPresentationItem, Vector2> positions = new();
            PlacePredicate(root, origin.x, origin.y, children, services, envelopes, positions, new HashSet<GraphPresentationItem>());

            foreach (KeyValuePair<GraphPresentationItem, Vector2> pair in positions)
            {
                PositionPredicateItem(presentation, pair.Key, pair.Value);
            }

            foreach (GraphDecoratorStack stack in presentation.DecoratorStacks)
            {
                if (members.Contains(stack.Anchor) && stack.Badges.All(members.Contains))
                {
                    PositionDecoratorStack(stack);
                }
            }

            if (positions.Count == 0)
            {
                return new Rect(origin, GraphPresentationMetrics.DecoratorNodeSize);
            }

            Rect bounds = CalculatePredicateBounds(positions.Keys);
            float width = Mathf.Max(
                minimumWidth,
                bounds.width + padding * 2f);
            Vector2 offset = new(
                owner.Position.x + (width - bounds.width) * 0.5f - bounds.xMin,
                origin.y - bounds.yMin);
            foreach (GraphPresentationItem member in positions.Keys)
            {
                PositionPredicateItem(presentation, member, member.Position + offset);
            }

            return CalculatePredicateBounds(positions.Keys);
        }

        /// <summary>Positions one predicate item without recursively moving separately placed descendants.</summary>
        private static void PositionPredicateItem(
            GraphPresentation presentation,
            GraphPresentationItem item,
            Vector2 position)
        {
            if (item.IsContainer)
            {
                // Descendants of a nested Condition shell are not part of the containing predicate placement map.
                presentation.MoveEmbeddedItem(item, position);
                return;
            }

            item.Position = position;
        }

        /// <summary>Calculates the final card bounds of independently positioned predicate members.</summary>
        private static Rect CalculatePredicateBounds(IEnumerable<GraphPresentationItem> members)
        {
            Rect bounds = default;
            bool hasBounds = false;
            foreach (GraphPresentationItem member in members)
            {
                Rect itemBounds = new(member.Position, member.Size);
                bounds = hasBounds ? Union(bounds, itemBounds) : itemBounds;
                hasBounds = true;
            }

            return bounds;
        }

        private static void AddPredicateChild(
            GraphPresentationItem owner,
            GraphPresentationItem candidate,
            ISet<GraphPresentationItem> members,
            IDictionary<GraphPresentationItem, List<GraphPresentationItem>> map)
        {
            if (candidate == null || candidate == owner || !members.Contains(candidate))
            {
                return;
            }

            if (!map.TryGetValue(owner, out List<GraphPresentationItem> list))
            {
                list = new List<GraphPresentationItem>();
                map.Add(owner, list);
            }

            if (!list.Contains(candidate))
            {
                list.Add(candidate);
            }
        }

        private static PredicateEnvelope MeasurePredicate(
            GraphPresentationItem item,
            IReadOnlyDictionary<GraphPresentationItem, List<GraphPresentationItem>> children,
            IReadOnlyDictionary<GraphPresentationItem, List<GraphPresentationItem>> services,
            IDictionary<GraphPresentationItem, PredicateEnvelope> envelopes,
            ISet<GraphPresentationItem> visiting)
        {
            if (envelopes.TryGetValue(item, out PredicateEnvelope cached))
            {
                return cached;
            }

            // Broken topology is already represented by the normal presentation warnings. Do not recurse forever here.
            if (!visiting.Add(item))
            {
                return new PredicateEnvelope(item.Size.x, item.Size.y, 0f);
            }

            float childrenWidth = 0f;
            float childrenHeight = 0f;
            if (children.TryGetValue(item, out List<GraphPresentationItem> childItems))
            {
                for (int index = 0; index < childItems.Count; index++)
                {
                    PredicateEnvelope child = MeasurePredicate(childItems[index], children, services, envelopes, visiting);
                    childrenWidth += child.TotalWidth;
                    childrenHeight = Mathf.Max(childrenHeight, child.Height);
                    if (index > 0)
                    {
                        childrenWidth += GraphPresentationMetrics.SiblingGap;
                    }
                }
            }

            float mainWidth = Mathf.Max(item.Size.x, childrenWidth);
            float height = item.Size.y + (childrenHeight > 0f ? GraphPresentationMetrics.LevelGap + childrenHeight : 0f);
            float serviceWidth = 0f;
            float serviceHeight = 0f;
            if (services.TryGetValue(item, out List<GraphPresentationItem> serviceItems))
            {
                foreach (GraphPresentationItem service in serviceItems)
                {
                    PredicateEnvelope envelope = MeasurePredicate(service, children, services, envelopes, visiting);
                    serviceWidth = Mathf.Max(serviceWidth, envelope.TotalWidth);
                    serviceHeight += envelope.Height + (serviceHeight > 0f ? GraphPresentationMetrics.ServiceGap : 0f);
                }
            }

            visiting.Remove(item);
            PredicateEnvelope result = new(mainWidth, Mathf.Max(height, serviceHeight), serviceWidth);
            envelopes[item] = result;
            return result;
        }

        private static void PlacePredicate(
            GraphPresentationItem item,
            float left,
            float top,
            IReadOnlyDictionary<GraphPresentationItem, List<GraphPresentationItem>> children,
            IReadOnlyDictionary<GraphPresentationItem, List<GraphPresentationItem>> services,
            IReadOnlyDictionary<GraphPresentationItem, PredicateEnvelope> envelopes,
            IDictionary<GraphPresentationItem, Vector2> positions,
            ISet<GraphPresentationItem> visiting)
        {
            if (!visiting.Add(item))
            {
                return;
            }

            PredicateEnvelope envelope = envelopes[item];
            positions[item] = new Vector2(left + (envelope.MainWidth - item.Size.x) * 0.5f, top);
            if (children.TryGetValue(item, out List<GraphPresentationItem> childItems))
            {
                float width = 0f;
                foreach (GraphPresentationItem child in childItems)
                {
                    width += envelopes[child].TotalWidth;
                }

                width += GraphPresentationMetrics.SiblingGap * Mathf.Max(0, childItems.Count - 1);
                float childLeft = left + (envelope.MainWidth - width) * 0.5f;
                float childTop = top + item.Size.y + GraphPresentationMetrics.LevelGap;
                foreach (GraphPresentationItem child in childItems)
                {
                    PlacePredicate(child, childLeft, childTop, children, services, envelopes, positions, visiting);
                    childLeft += envelopes[child].TotalWidth + GraphPresentationMetrics.SiblingGap;
                }
            }

            if (services.TryGetValue(item, out List<GraphPresentationItem> serviceItems))
            {
                float serviceTop = top;
                float serviceLeft = left + envelope.MainWidth + GraphPresentationMetrics.ServiceGap;
                foreach (GraphPresentationItem service in serviceItems)
                {
                    PlacePredicate(service, serviceLeft, serviceTop, children, services, envelopes, positions, visiting);
                    serviceTop += envelopes[service].Height + GraphPresentationMetrics.ServiceGap;
                }
            }

            visiting.Remove(item);
        }

        private readonly struct PredicateEnvelope
        {
            internal PredicateEnvelope(float mainWidth, float height, float serviceWidth)
            {
                MainWidth = mainWidth;
                Height = height;
                ServiceWidth = serviceWidth;
            }

            internal float MainWidth { get; }
            internal float Height { get; }
            internal float ServiceWidth { get; }
            internal float TotalWidth => MainWidth + (ServiceWidth > 0f ? GraphPresentationMetrics.ServiceGap + ServiceWidth : 0f);
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
        private static void ResolveLoopScope(GraphPresentation presentation, GraphLoopScope scope, Rect ownerBounds)
        {
            Rect conditionBounds;
            if (scope.PredicateRoot != null)
            {
                foreach (GraphPresentationItem predicate in scope.PredicateRoots)
                {
                    Measure(presentation, predicate);
                }

                Vector2 origin;
                if (scope.Mode == Loop.LoopType.doWhile)
                {
                    Rect bodyEnd = PositionLoopBodyItems(presentation, scope, ownerBounds);
                    origin = new Vector2(bodyEnd.center.x, bodyEnd.yMax + GraphPresentationMetrics.LevelGap);
                }
                else
                {
                    origin = new Vector2(ownerBounds.center.x, ownerBounds.yMax + GraphPresentationMetrics.LevelGap);
                }

                scope.PredicateBounds = LayoutPredicate(
                    presentation,
                    scope.Owner,
                    scope.PredicateRoot,
                    scope.PredicateMembers,
                    origin,
                    GraphPresentationMetrics.ConditionMinimumWidth,
                    GraphPresentationMetrics.ConditionPadding);
                if (scope.Mode != Loop.LoopType.doWhile)
                {
                    PositionLoopBodyItems(presentation, scope, scope.PredicateBounds);
                }

                conditionBounds = scope.PredicateBounds;
            }
            else
            {
                PositionLoopDerivedItems(presentation, scope, ownerBounds);
                conditionBounds = GetLoopMemberBounds(scope, scope.Condition);
            }

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
        private static void PositionLoopDerivedItems(
            GraphPresentation presentation,
            GraphLoopScope scope,
            Rect ownerBounds)
        {
            GraphPresentationItem condition = scope.Condition;
            if (scope.Mode == Loop.LoopType.doWhile)
            {
                Rect bodyEnd = PositionLoopBodyItems(presentation, scope, ownerBounds);
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

                PositionLoopBodyItems(presentation, scope, GetLoopMemberBounds(scope, condition));
            }

        }

        /// <summary>Positions the ordered Loop body as owner-derived geometry and returns its final bounds.</summary>
        private static Rect PositionLoopBodyItems(
            GraphPresentation presentation,
            GraphLoopScope scope,
            Rect preceding)
        {
            Rect previous = preceding;
            foreach (GraphPresentationItem member in scope.Body)
            {
                Vector2 position = new(
                    previous.center.x - member.Size.x * 0.5f,
                    previous.yMax + GraphPresentationMetrics.LevelGap);
                if (member.Node == null)
                {
                    member.Position = position;
                }
                else
                {
                    presentation.MoveEmbeddedItem(member, position);
                    if (member.FlowScope != null && !ReferenceEquals(member.FlowScope, scope))
                    {
                        ResolveScope(
                            presentation,
                            member.FlowScope,
                            new HashSet<GraphFlowScope>(),
                            new HashSet<GraphFlowScope>());
                    }
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
