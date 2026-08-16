using Aethiumian.AI.Editor;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Accessors;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

namespace Aethiumian.AI.Tests
{
    /// <summary>
    /// Evidence-only baselines for the large graph canvas path.
    /// </summary>
    public sealed class GraphCanvasPerformanceBaselineTests : GraphEditorTestFixture
    {
        /// <summary>Builds one reachable synthetic chain with the requested authored node count.</summary>
        private BehaviourTreeData CreateSyntheticTree(int nodeCount)
        {
            if (nodeCount < 1) throw new ArgumentOutOfRangeException(nameof(nodeCount));

            SyntheticNode[] nodes = new SyntheticNode[nodeCount];
            for (int index = 0; index < nodeCount; index++)
            {
                nodes[index] = new SyntheticNode
                {
                    name = $"Synthetic {index}",
                    uuid = UUID.NewUUID(),
                };
            }

            for (int index = 0; index + 1 < nodes.Length; index++)
            {
                nodes[index].child = nodes[index + 1].ToReference();
            }

            return Tree(nodes);
        }

        /// <summary>Verifies topology and presentation scale without changing production code.</summary>
        [Test]
        public void SyntheticTrees_100_500_1000NodesHaveExpectedTopologyAndPresentationScale()
        {
            foreach (int nodeCount in new[] { 100, 500, 1000 })
            {
                BehaviourTreeData tree = CreateSyntheticTree(nodeCount);
                GraphTopology topology = GraphTopologyBuilder.Build(tree);
                GraphPresentation presentation = GraphPresentationBuilder.Build(topology);

                Assert.That(topology.Nodes, Has.Count.EqualTo(nodeCount), $"topology nodes: {nodeCount}");
                Assert.That(topology.Edges, Has.Count.EqualTo(Math.Max(0, nodeCount - 1)), $"topology edges: {nodeCount}");
                Assert.That(presentation.Find(tree.headNodeUUID), Is.Not.Null, $"presentation head: {nodeCount}");
                Assert.That(presentation.Roots, Has.Count.GreaterThan(0), $"presentation roots: {nodeCount}");
            }
        }

        /// <summary>Measures the existing topology/presentation and full SetTopology rebuild paths.</summary>
        [Test]
        public void SyntheticTrees_ReportTopologyPresentationAndCanvasRebuildBaselines()
        {
            GraphEditorModule module = null;
            try
            {
                foreach (int nodeCount in new[] { 100, 500, 1000 })
                {
                    BehaviourTreeData tree = CreateSyntheticTree(nodeCount);
                    module ??= CreateHiddenGraphModule(tree);
                    GraphTopology topology = GraphTopologyBuilder.Build(tree);
                    module.Canvas.SetTopology(topology);

                    const int samples = 3;
                    long fullCanvasTicks = 0;
                    long fastCanvasTicks = 0;
                    for (int sample = 0; sample < samples; sample++)
                    {
                        topology = GraphTopologyBuilder.Build(tree);
                        Stopwatch timer = Stopwatch.StartNew();
                        module.Canvas.SetTopology(topology);
                        fullCanvasTicks += timer.ElapsedTicks;

                        timer.Restart();
                        module.Canvas.SetTopology(topology);
                        fastCanvasTicks += timer.ElapsedTicks;
                    }

                    double fullMilliseconds = Milliseconds(fullCanvasTicks, samples);
                    double fastMilliseconds = Milliseconds(fastCanvasTicks, samples);
                    Debug.Log($"[GraphCanvasBaselineAfter] nodes={nodeCount} full_set_topology_ms={fullMilliseconds:F3} fast_same_snapshot_ms={fastMilliseconds:F3} improvement_percent={Improvement(fullMilliseconds, fastMilliseconds):F1}");
                }
            }
            finally
            {
                // The fixture owns teardown; this scope only prevents accidental retained references.
                module = null;
            }
        }

        /// <summary>Confirms that unchanged SetTopology currently replaces node visual elements.</summary>
        [Test]
        public void UnchangedSetTopology_PreservesNodeVisualElementIdentityAndSelection()
        {
            BehaviourTreeData tree = CreateSyntheticTree(100);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            module.Canvas.SetTopology(topology);
            module.SetGraphSelection(new[] { tree.nodes[0], tree.nodes[1] });

            List<VisualElement> before = module.Canvas.Q<VisualElement>("ai-editor-graph-node-layer").Children().ToList();
            module.Canvas.SetTopology(topology);
            List<VisualElement> after = module.Canvas.Q<VisualElement>("ai-editor-graph-node-layer").Children().ToList();

            Assert.That(after, Has.Count.EqualTo(before.Count));
            Assert.That(after.Zip(before, (current, previous) => ReferenceEquals(current, previous)).All(value => value), Is.True);
            Assert.That(module.SelectedNodes.Select(node => node.uuid), Is.EqualTo(new[] { tree.nodes[0].uuid, tree.nodes[1].uuid }));
        }

        /// <summary>Confirms that a different snapshot replaces visual elements instead of reusing them.</summary>
        [Test]
        public void DifferentTopologySnapshot_ReplacesNodeVisualElements()
        {
            BehaviourTreeData firstTree = CreateSyntheticTree(20);
            BehaviourTreeData secondTree = CreateSyntheticTree(20);
            GraphEditorModule module = CreateHiddenGraphModule(firstTree);
            module.Canvas.SetTopology(GraphTopologyBuilder.Build(firstTree));
            List<VisualElement> before = module.Canvas.Q<VisualElement>("ai-editor-graph-node-layer").Children().ToList();

            module.Canvas.SetTopology(GraphTopologyBuilder.Build(secondTree));
            List<VisualElement> after = module.Canvas.Q<VisualElement>("ai-editor-graph-node-layer").Children().ToList();

            Assert.That(after, Has.Count.EqualTo(before.Count));
            Assert.That(after.Zip(before, (current, previous) => ReferenceEquals(current, previous)).Any(value => value), Is.False);
        }

        /// <summary>Confirms descriptor position changes update the retained presentation geometry.</summary>
        [Test]
        public void SameTopologySnapshot_PositionChangeUpdatesPresentationGeometry()
        {
            BehaviourTreeData tree = CreateSyntheticTree(20);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            module.Canvas.SetTopology(topology);
            Vector2 target = new(321f, 123f);
            topology.FindNode(tree.nodes[0].uuid).Position = target;

            module.Canvas.SetTopology(topology);

            Assert.That(module.Canvas.Presentation.Find(tree.nodes[0].uuid).Position, Is.EqualTo(target));
        }

        /// <summary>Converts stopwatch ticks to milliseconds without adding a production measurement API.</summary>
        private static double Milliseconds(long ticks, int samples)
        {
            return ticks * 1000d / Stopwatch.Frequency / samples;
        }

        /// <summary>Calculates the relative reduction from the full path to the same-snapshot path.</summary>
        private static double Improvement(double fullMilliseconds, double fastMilliseconds)
        {
            return fullMilliseconds <= 0d ? 0d : (fullMilliseconds - fastMilliseconds) / fullMilliseconds * 100d;
        }

        [Serializable]
        private sealed class SyntheticNode : TreeNode
        {
            public NodeReference child;

            public override void Initialize() { }
            public override State Execute() => State.Success;
        }
    }
}
