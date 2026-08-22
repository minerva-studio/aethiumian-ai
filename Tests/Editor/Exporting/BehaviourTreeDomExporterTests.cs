using Aethiumian.AI.Editor.Exporting;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using NUnit.Framework;
using System;
using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Editor.Tests.Exporting
{
    /// <summary>Focused tests for the read-only semantic behaviour-tree DOM.</summary>
    public sealed class BehaviourTreeDomExporterTests
    {
        [Test]
        public void ExportYaml_ExpandsStructuralChildrenAndKeepsNodeIdentity()
        {
            Sequence head = CreateNode<Sequence>("Head");
            Always child = CreateNode<Always>("Child");
            head.events = new[] { new NodeReference(child.uuid) };
            child.parent = new NodeReference(head.uuid);
            BehaviourTreeData tree = CreateTree(head, child);

            try
            {
                BehaviourTreeDomExportResult result = BehaviourTreeDomExporter.ExportYaml(tree);

                Assert.That(result.HasErrors, Is.False);
                Assert.That(result.ExportedNodeCount, Is.EqualTo(2));
                StringAssert.Contains("schema: aethiumian.behaviour-tree-dom/v1.1", result.Content);
                StringAssert.Contains("$type: Sequence", result.Content);
                StringAssert.Contains("$type: Always", result.Content);
                Assert.That(result.Content, Does.Not.Contain("$type: Aethiumian.AI.Nodes.Sequence"));
                StringAssert.Contains("events:", result.Content);
                StringAssert.Contains(child.uuid.ToString(), result.Content);
                Assert.That(CountOccurrences(result.Content, child.uuid.ToString()), Is.EqualTo(1));
            }
            finally
            {
                DestroyTree(tree);
            }
        }

        [Test]
        public void ExportYaml_FromSelectedNodeDoesNotIncludeItsAncestor()
        {
            Sequence head = CreateNode<Sequence>("Head");
            Always child = CreateNode<Always>("Selected");
            head.events = new[] { new NodeReference(child.uuid) };
            child.parent = new NodeReference(head.uuid);
            BehaviourTreeData tree = CreateTree(head, child);

            try
            {
                BehaviourTreeDomExportResult result = BehaviourTreeDomExporter.ExportYaml(tree, child.uuid);

                Assert.That(result.HasErrors, Is.False);
                Assert.That(result.ExportedNodeCount, Is.EqualTo(1));
                StringAssert.Contains("startNode: " + child.uuid, result.Content);
                StringAssert.Contains("name: Selected", result.Content);
                Assert.That(result.Content, Does.Not.Contain("name: Head"));
            }
            finally
            {
                DestroyTree(tree);
            }
        }

        [Test]
        public void ExportYaml_KeepsRawReferenceAsReference()
        {
            ReferenceProbe head = CreateNode<ReferenceProbe>("Head");
            Always child = CreateNode<Always>("Child");
            Always rawTarget = CreateNode<Always>("Raw Target");
            head.child = new NodeReference(child.uuid);
            head.raw = new RawNodeReference { UUID = rawTarget.uuid };
            child.parent = new NodeReference(head.uuid);
            BehaviourTreeData tree = CreateTree(head, child, rawTarget);

            try
            {
                BehaviourTreeDomExportResult result = BehaviourTreeDomExporter.ExportYaml(tree);

                Assert.That(result.HasErrors, Is.False);
                Assert.That(result.ExportedNodeCount, Is.EqualTo(2));
                StringAssert.Contains("raw:", result.Content);
                StringAssert.Contains("name: Raw Target", result.Content);
                Assert.That(result.Content, Does.Not.Contain("$type: Aethiumian.AI.Nodes.Always\n      name: Raw Target"));
            }
            finally
            {
                DestroyTree(tree);
            }
        }

        [Test]
        public void ExportYaml_CompactsVariableConstantAndEmptyReference()
        {
            Loop head = CreateNode<Loop>("Loop");
            head.loopType = Loop.LoopType.@for;
            head.loopCount = 3;
            head.condition = NodeReference.Empty;
            head.events = Array.Empty<NodeReference>();
            BehaviourTreeData tree = CreateTree(head);

            try
            {
                BehaviourTreeDomExportResult result = BehaviourTreeDomExporter.ExportYaml(tree);

                Assert.That(result.HasErrors, Is.False);
                StringAssert.Contains("loopCount: 3", result.Content);
                StringAssert.Contains("condition: null", result.Content);
                StringAssert.Contains("events: []", result.Content);
                Assert.That(result.Content, Does.Not.Contain("stringValue:"));
                Assert.That(result.Content, Does.Not.Contain("vector3Value:"));
            }
            finally
            {
                DestroyTree(tree);
            }
        }

        [Test]
        public void ExportYaml_DoesNotDirtyTree()
        {
            Sequence head = CreateNode<Sequence>("Head");
            BehaviourTreeData tree = CreateTree(head);
            EditorUtility.ClearDirty(tree);

            try
            {
                _ = BehaviourTreeDomExporter.ExportYaml(tree);
                Assert.That(EditorUtility.IsDirty(tree), Is.False);
            }
            finally
            {
                DestroyTree(tree);
            }
        }

        [Test]
        public void ExportYaml_UsesClrFallbackForThirdPartyTypesAndOmitsEmptyServices()
        {
            ReferenceProbe head = CreateNode<ReferenceProbe>("Head");
            Sequence unreachable = CreateNode<Sequence>("Unreachable");
            head.child = NodeReference.Empty;
            head.raw = RawNodeReference.Empty;
            head.parent = NodeReference.Empty;
            BehaviourTreeData tree = CreateTree(head, unreachable);

            try
            {
                BehaviourTreeDomExportResult result = BehaviourTreeDomExporter.ExportYaml(tree);

                Assert.That(result.HasErrors, Is.False);
                StringAssert.Contains("$type: ReferenceProbe", result.Content);
                StringAssert.Contains("clrType: Aethiumian.AI.Editor.Tests.Exporting.BehaviourTreeDomExporterTests+ReferenceProbe", result.Content);
                Assert.That(result.Content, Does.Not.Contain("services: []"));
                StringAssert.Contains("exportedNodeCount: 1", result.Content);
                StringAssert.Contains("unreachableNodeCount: 1", result.Content);
                StringAssert.Contains("severity: Info", result.Content);
            }
            finally
            {
                DestroyTree(tree);
            }
        }

        [Test]
        public void ExportYaml_CompactsResolvedObjectActionParameters()
        {
            ObjectAction action = CreateNode<ObjectAction>("Act");
            action.methodName = nameof(ReferenceProbe.Act);
            action.type = new GenericTypeReference();
            action.type.SetBaseType(typeof(object));
            action.type.SetReferType(typeof(ReferenceProbe));
            action.parameters = new System.Collections.Generic.List<Parameter>
            {
                new Parameter(VariableType.Node),
                new Parameter(3),
                new Parameter(false),
            };
            BehaviourTreeData tree = CreateTree(action);

            try
            {
                BehaviourTreeDomExportResult result = BehaviourTreeDomExporter.ExportYaml(tree);

                Assert.That(result.HasErrors, Is.False);
                StringAssert.Contains("name: progress", result.Content);
                StringAssert.Contains("source: injected", result.Content);
                StringAssert.Contains("name: action", result.Content);
                StringAssert.Contains("value: 3", result.Content);
                StringAssert.Contains("name: isBreakable", result.Content);
                StringAssert.Contains("value: false", result.Content);
                Assert.That(result.Content, Does.Not.Contain("index:"));
            }
            finally
            {
                DestroyTree(tree);
            }
        }

        [Test]
        public void ExportYaml_IsDeterministic()
        {
            Sequence head = CreateNode<Sequence>("Head");
            Always child = CreateNode<Always>("Child");
            head.events = new[] { new NodeReference(child.uuid) };
            child.parent = new NodeReference(head.uuid);
            BehaviourTreeData tree = CreateTree(head, child);

            try
            {
                BehaviourTreeDomExportResult first = BehaviourTreeDomExporter.ExportYaml(tree);
                BehaviourTreeDomExportResult second = BehaviourTreeDomExporter.ExportYaml(tree);

                Assert.That(second.Content, Is.EqualTo(first.Content));
            }
            finally
            {
                DestroyTree(tree);
            }
        }

        [Serializable]
        private sealed class ReferenceProbe : Flow
        {
            public NodeReference child;
            public RawNodeReference raw;

            public bool Act(NodeProgress progress, int action, bool isBreakable) => true;

            public override State Execute() => State.Success;
            public override void Initialize()
            {
            }
        }

        private static T CreateNode<T>(string name) where T : TreeNode, new()
        {
            return new T
            {
                name = name,
                uuid = UUID.NewUUID(),
                parent = NodeReference.Empty,
            };
        }

        private static BehaviourTreeData CreateTree(params TreeNode[] nodes)
        {
            BehaviourTreeData tree = ScriptableObject.CreateInstance<BehaviourTreeData>();
            tree.noActionMaximumDurationLimit = true;
            tree.headNodeUUID = nodes[0].uuid;
            tree.nodes.AddRange(nodes);
            return tree;
        }

        private static void DestroyTree(BehaviourTreeData tree)
        {
            if (tree != null)
            {
                UnityEngine.Object.DestroyImmediate(tree);
            }
        }

        private static int CountOccurrences(string text, string value)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }

            return count;
        }
    }
}
