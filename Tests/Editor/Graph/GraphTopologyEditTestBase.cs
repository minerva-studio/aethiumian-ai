using Aethiumian.AI.Accessors;
using Aethiumian.AI.Editor;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using Aethiumian.AI.Visual;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Aethiumian.AI.Editor.Tests.Graph
{
    /// <summary>Shared topology-edit fixture and assertions for Graph Editor topology tests.</summary>
    public abstract class GraphTopologyEditTestBase : GraphEditorTestFixture
    {
private protected static void SetScalarReference(Condition owner, string fieldName, TreeNode target)
        {
            NodeReference reference = target.ToReference();
            switch (fieldName)
            {
                case nameof(Condition.condition): owner.condition = reference; break;
                case nameof(Condition.trueNode): owner.trueNode = reference; break;
                case nameof(Condition.falseNode): owner.falseNode = reference; break;
                default: throw new ArgumentOutOfRangeException(nameof(fieldName), fieldName, null);
            }
        }


private protected static NodeReference GetScalarReference(Condition owner, string fieldName)
        {
            return fieldName switch
            {
                nameof(Condition.condition) => owner.condition,
                nameof(Condition.trueNode) => owner.trueNode,
                nameof(Condition.falseNode) => owner.falseNode,
                _ => throw new ArgumentOutOfRangeException(nameof(fieldName), fieldName, null),
            };
        }


private protected static void AssertGraphPositions(GraphTopology topology, IReadOnlyDictionary<UUID, Vector2> positions)
        {
            foreach (GraphNodeDescriptor node in topology.Nodes)
                Assert.That(node.Position, Is.EqualTo(positions[node.UUID]), node.UUID.ToString());
        }


private protected static GraphPortDescriptor FindPort(
            IEnumerable<GraphPortDescriptor> ports, UUID ownerUUID, string fieldName, int index)
        {
            return ports.Single(port => port.OwnerUUID == ownerUUID
                && port.FieldName == fieldName && port.CollectionIndex == index);
        }


private protected static IReadOnlyList<GraphPortDescriptor> BuildPorts(GraphTopology topology)
        {
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            return GraphPortDescriptorBuilder.Build(topology, presentation, includeRawReferences: false);
        }
    }
}
