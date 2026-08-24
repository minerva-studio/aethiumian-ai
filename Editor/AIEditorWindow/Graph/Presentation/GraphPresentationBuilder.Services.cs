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
                        new GraphServicePlaceholder(
                            relation.Source.Item,
                            relation.Label,
                            relation.AuthoredEdge.Reference.TargetUUID));
                    virtualItems.Add(placeholder);
                    relation = GraphPresentationRelation.CreateFromEdge(
                        relation.Source,
                        placeholder.Entry,
                        GraphPresentationRelationKind.Service,
                        GraphPresentationRelationRole.PlaceholderHint,
                        relation.Label,
                        relation.AuthoredEdge);
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

}
}
