using Aethiumian.AI.Editor.Exporting;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
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

        [Test]
        public void Inspector_GetSummaryReportsReachabilityAndHeadIdentity()
        {
            Sequence head = CreateNode<Sequence>("Head");
            Always child = CreateNode<Always>("Child");
            Always unreachable = CreateNode<Always>("Unreachable");
            head.events = new[] { new NodeReference(child.uuid) };
            child.parent = new NodeReference(head.uuid);
            BehaviourTreeData tree = CreateTree(head, child, unreachable);

            try
            {
                BehaviourTreeDomSummary summary = BehaviourTreeDomInspector.GetSummary(tree);

                Assert.That(summary.TotalNodeCount, Is.EqualTo(3));
                Assert.That(summary.ExportedNodeCount, Is.EqualTo(2));
                Assert.That(summary.UnreachableNodeCount, Is.EqualTo(1));
                Assert.That(summary.HeadNodeId, Is.EqualTo(head.uuid));
                Assert.That(summary.StartNodeId, Is.EqualTo(head.uuid));
                Assert.That(summary.Head.Type, Is.EqualTo("Sequence"));
                Assert.That(summary.Diagnostics.Count, Is.EqualTo(1));
            }
            finally
            {
                DestroyTree(tree);
            }
        }

        [Test]
        public void Inspector_FindNodesFiltersTypeAndReachabilityInAuthoredOrder()
        {
            Sequence head = CreateNode<Sequence>("Head");
            Always reachable = CreateNode<Always>("Reachable Always");
            Always unreachable = CreateNode<Always>("Unreachable Always");
            head.events = new[] { new NodeReference(reachable.uuid) };
            reachable.parent = new NodeReference(head.uuid);
            BehaviourTreeData tree = CreateTree(head, reachable, unreachable);

            try
            {
                IReadOnlyList<BehaviourTreeDomNodeInfo> reachableMatches = BehaviourTreeDomInspector.FindNodes(
                    tree,
                    new BehaviourTreeDomFindOptions { Type = "Always" });
                IReadOnlyList<BehaviourTreeDomNodeInfo> allMatches = BehaviourTreeDomInspector.FindNodes(
                    tree,
                    new BehaviourTreeDomFindOptions { NameContains = "always", ReachableOnly = false });
                IReadOnlyList<BehaviourTreeDomNodeInfo> selectedMatches = BehaviourTreeDomInspector.FindNodes(
                    tree,
                    new BehaviourTreeDomFindOptions { ReachableOnly = true },
                    reachable.uuid);

                Assert.That(reachableMatches.Count, Is.EqualTo(1));
                Assert.That(reachableMatches[0].Id, Is.EqualTo(reachable.uuid));
                Assert.That(reachableMatches[0].AuthoredIndex, Is.EqualTo(1));
                Assert.That(allMatches.Count, Is.EqualTo(2));
                Assert.That(allMatches[0].Id, Is.EqualTo(reachable.uuid));
                Assert.That(allMatches[1].Id, Is.EqualTo(unreachable.uuid));
                Assert.That(allMatches[1].Reachable, Is.False);
                Assert.That(selectedMatches.Count, Is.EqualTo(1));
                Assert.That(selectedMatches[0].Id, Is.EqualTo(reachable.uuid));
            }
            finally
            {
                DestroyTree(tree);
            }
        }

        [Test]
        public void ExportJson_ProducesNativeDocumentWithTheSameNodeIdentity()
        {
            Sequence head = CreateNode<Sequence>("Head");
            Always child = CreateNode<Always>("Child");
            head.events = new[] { new NodeReference(child.uuid) };
            child.parent = new NodeReference(head.uuid);
            BehaviourTreeData tree = CreateTree(head, child);

            try
            {
                BehaviourTreeDomExportResult yaml = BehaviourTreeDomExporter.ExportYaml(tree);
                BehaviourTreeDomExportResult json = BehaviourTreeDomExporter.ExportJson(tree);
                JObject document = JObject.Parse(json.Content);

                Assert.That(json.HasErrors, Is.False);
                Assert.That(document["schema"]?.Value<string>(), Is.EqualTo("aethiumian.behaviour-tree-dom/v1.1"));
                Assert.That(document["root"]?["id"]?.Value<string>(), Is.EqualTo(head.uuid.ToString()));
                Assert.That(document["root"]?["$type"]?.Value<string>(), Is.EqualTo("Sequence"));
                Assert.That(document["root"]?["fields"]?["events"]?[0]?["id"]?.Value<string>(), Is.EqualTo(child.uuid.ToString()));
                Assert.That(document["root"]?.Type, Is.EqualTo(JTokenType.Object));
                Assert.That(json.Content, Does.Not.Contain("\\n"));
                Assert.That(yaml.ExportedNodeCount, Is.EqualTo(json.ExportedNodeCount));
            }
            finally
            {
                DestroyTree(tree);
            }
        }

        [Test]
        public void ExportJson_WithMissingStartNodeProducesNullDocumentAndDiagnostic()
        {
            Sequence head = CreateNode<Sequence>("Head");
            BehaviourTreeData tree = CreateTree(head);

            try
            {
                BehaviourTreeDomExportResult result = BehaviourTreeDomExporter.ExportJson(tree, UUID.NewUUID());

                Assert.That(result.Content, Is.EqualTo("null"));
                Assert.That(result.Diagnostics.Any(diagnostic => diagnostic.Code == "BTDOM_MISSING_START"), Is.True);
            }
            finally
            {
                DestroyTree(tree);
            }
        }

        [Test]
        public void Inspector_ValidateTreatsIntentionalUnreachableNodesAsValid()
        {
            Sequence head = CreateNode<Sequence>("Head");
            Always reachable = CreateNode<Always>("Reachable");
            Always unreachable = CreateNode<Always>("Unreachable");
            head.events = new[] { new NodeReference(reachable.uuid) };
            reachable.parent = new NodeReference(head.uuid);
            BehaviourTreeData tree = CreateTree(head, reachable, unreachable);

            try
            {
                BehaviourTreeValidationResult result = BehaviourTreeDomInspector.Validate(tree);

                Assert.That(result.Valid, Is.True);
                Assert.That(result.NodeCount, Is.EqualTo(3));
                Assert.That(result.Diagnostics, Is.Not.Empty);
                Assert.That(result.Diagnostics.Any(
                    diagnostic => diagnostic.Severity == BehaviourTreeDomDiagnosticSeverity.Error), Is.False);
            }
            finally
            {
                DestroyTree(tree);
            }
        }

        [Test]
        public void Inspector_ValidateKeepsWarningOnlyAssetValid()
        {
            BehaviourTreeDomDiagnostic warning = new BehaviourTreeDomDiagnostic(
                "BTDOM_TEST_WARNING",
                BehaviourTreeDomDiagnosticSeverity.Warning,
                UUID.Empty,
                string.Empty,
                "Warning-only validation fixture.");
            BehaviourTreeValidationResult result = new BehaviourTreeValidationResult(
                "Assets/Test.asset",
                1,
                new[] { warning });

            Assert.That(result.Valid, Is.True);
            Assert.That(result.NodeCount, Is.EqualTo(1));
            Assert.That(result.Diagnostics, Is.EqualTo(new[] { warning }));
        }

        [Test]
        public void Inspector_ValidateFailsWhenStructuralDiagnosticsContainErrors()
        {
            Sequence head = CreateNode<Sequence>("Head");
            Always child = CreateNode<Always>("Child");
            head.events = new[]
            {
                new NodeReference(child.uuid),
                new NodeReference(child.uuid),
            };
            child.parent = new NodeReference(head.uuid);
            BehaviourTreeData tree = CreateTree(head, child);

            try
            {
                BehaviourTreeValidationResult result = BehaviourTreeDomInspector.Validate(tree);

                Assert.That(result.Valid, Is.False);
                Assert.That(result.NodeCount, Is.EqualTo(2));
                Assert.That(result.Diagnostics.Any(
                    diagnostic => diagnostic.Severity == BehaviourTreeDomDiagnosticSeverity.Error), Is.True);
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
