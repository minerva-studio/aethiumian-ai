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

namespace Aethiumian.AI.Tests
{
    /// <summary>
    /// EditMode coverage for graph topology and non-dirty layout resolution.
    /// </summary>
    /// <summary>Graph Editor GraphLayout contract tests.</summary>
    [Category("GraphEditor")]
    public sealed class GraphLayoutTests : GraphEditorTestFixture
    {
        [Test]
        public void Groups_PersistIdentityAndRetainSingleMemberButRemoveEmpty()
        {
            UUID first = UUID.NewUUID();
            UUID second = UUID.NewUUID();
            UUID groupUUID = UUID.NewUUID();
            GraphGroupLayoutEntry group = new(groupUUID, "Frame", Color.green, new[] { first, second });
            GraphLayoutData layout = GraphLayoutData.Create(Array.Empty<GraphLayoutEntry>(), groupEntries: new[] { group });

            layout.RemoveNode(second);
            Assert.That(layout.Groups.Count, Is.EqualTo(1));
            Assert.That(layout.Groups[0].UUID, Is.EqualTo(groupUUID));
            Assert.That(layout.Groups[0].Members, Is.EqualTo(new[] { first }));

            layout.RemoveNode(first);
            Assert.That(layout.Groups, Is.Empty);
        }

        [Test]
        public void Resolver_PreservesSingleMemberGroupAndFiltersOnlyMissingMembers()
        {
            TestNode head = Node<TestNode>("Head");
            BehaviourTreeData tree = Tree(head);
            GraphGroupLayoutEntry group = new(UUID.NewUUID(), "Frame", Color.blue, new[] { head.uuid, UUID.NewUUID() });
            tree.GraphLayout = GraphLayoutData.Create(
                new[] { new GraphLayoutEntry(head.uuid, Vector2.zero) }, groupEntries: new[] { group });
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphLayoutData resolved = GraphLayoutResolver.CreateLayout(topology, tree.GraphLayout);
            Assert.That(resolved.Groups.Count, Is.EqualTo(1));
            Assert.That(resolved.Groups[0].Members, Is.EqualTo(new[] { head.uuid }));
        }

        [Test]
        public void GroupCommands_EnforceSingleOwnershipAndPreserveOtherGroupMetadata()
        {
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            TestNode third = Node<TestNode>("Third");
            BehaviourTreeData tree = Tree(first, second, third);
            UUID oldGroupUUID = UUID.NewUUID();
            UUID targetGroupUUID = UUID.NewUUID();
            tree.GraphLayout = GraphLayoutData.Create(Array.Empty<GraphLayoutEntry>(), groupEntries: new[]
            {
                new GraphGroupLayoutEntry(oldGroupUUID, "Old", Color.red, new[] { first.uuid, third.uuid }),
                new GraphGroupLayoutEntry(targetGroupUUID, "Target", Color.green, new[] { second.uuid })
            });
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            module.SetGraphSelection(new[] { first, second });

            Assert.That(module.AddSelectedToGroup(targetGroupUUID), Is.True);
            GraphGroupLayoutEntry oldGroup = tree.GraphLayout.Groups.Single(group => group.UUID == oldGroupUUID);
            GraphGroupLayoutEntry targetGroup = tree.GraphLayout.Groups.Single(group => group.UUID == targetGroupUUID);
            Assert.That(oldGroup.Title, Is.EqualTo("Old"));
            Assert.That(oldGroup.Color, Is.EqualTo(Color.red));
            Assert.That(oldGroup.Members, Is.EqualTo(new[] { third.uuid }));
            Assert.That(targetGroup.Members, Is.EqualTo(new[] { second.uuid, first.uuid }));

            module.SetGraphSelection(new[] { first });
            Assert.That(module.RemoveSelectedFromGroup(targetGroupUUID), Is.True);
            Assert.That(tree.GraphLayout.Groups.Single(group => group.UUID == targetGroupUUID).Members,
                Is.EqualTo(new[] { second.uuid }));
            module.SetGraphSelection(new[] { second });
            Assert.That(module.RemoveSelectedFromGroup(targetGroupUUID), Is.True);
            Assert.That(tree.GraphLayout.Groups.Any(group => group.UUID == targetGroupUUID), Is.False);
        }

        [Test]
        public void GroupMove_CommitsOnceAndSupportsUndoRedo()
        {
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            BehaviourTreeData tree = Tree(first, second);
            UUID groupUUID = UUID.NewUUID();
            tree.GraphLayout = GraphLayoutData.Create(
                new[] { new GraphLayoutEntry(first.uuid, Vector2.zero), new GraphLayoutEntry(second.uuid, new Vector2(50f, 0f)) },
                groupEntries: new[] { new GraphGroupLayoutEntry(groupUUID, "Frame", Color.blue, new[] { first.uuid, second.uuid }) });
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            Vector2 before = module.Topology.FindNode(first.uuid).Position;
            Assert.That(module.MoveGroup(groupUUID, new Vector2(25f, 10f)), Is.True);
            module.CommitGroupMove();
            Assert.That(tree.GraphLayout.TryGetPosition(first.uuid, out Vector2 after), Is.True);
            Assert.That(after, Is.EqualTo(before + new Vector2(25f, 10f)));
            Undo.PerformUndo();
            module.RebuildTopology();
            Assert.That(module.Topology.FindNode(first.uuid).Position, Is.EqualTo(before));
            Undo.PerformRedo();
            module.RebuildTopology();
            Assert.That(module.Topology.FindNode(first.uuid).Position, Is.EqualTo(after));
        }

        [Test]
        public void GroupRename_PreservesIdentityMembersAndColorAndSupportsUndo()
        {
            TestNode node = Node<TestNode>("Grouped");
            BehaviourTreeData tree = Tree(node);
            UUID groupUUID = UUID.NewUUID();
            Color color = Color.magenta;
            tree.GraphLayout = GraphLayoutData.Create(Array.Empty<GraphLayoutEntry>(), groupEntries: new[]
            {
                new GraphGroupLayoutEntry(groupUUID, "Before", color, new[] { node.uuid }),
            });
            GraphEditorModule module = CreateHiddenGraphModule(tree);

            Assert.That(module.RenameGroup(groupUUID, "After"), Is.True);
            GraphGroupLayoutEntry renamed = tree.GraphLayout.Groups.Single();
            Assert.That(renamed.UUID, Is.EqualTo(groupUUID));
            Assert.That(renamed.Members, Is.EqualTo(new[] { node.uuid }));
            Assert.That(renamed.Color, Is.EqualTo(color));
            Assert.That(renamed.Title, Is.EqualTo("After"));

            Undo.PerformUndo();
            Assert.That(tree.GraphLayout.Groups.Single().Title, Is.EqualTo("Before"));
        }

        [Test]
        public void TidySelection_UsesTemporaryTopologyLayoutAndPreservesUnselectedPositions()
        {
            Sequence head = Node<Sequence>("Head");
            Decision branch = Node<Decision>("Branch");
            TestNode trueNode = Node<TestNode>("True");
            TestNode falseNode = Node<TestNode>("False");
            TestNode continuation = Node<TestNode>("Continuation");
            TestNode detachedFirst = Node<TestNode>("Detached First");
            TestNode detachedSecond = Node<TestNode>("Detached Second");
            head.events = new[] { branch.ToReference(), continuation.ToReference() };
            branch.events = new[] { trueNode.ToReference(), falseNode.ToReference() };
            detachedFirst.parent = NodeReference.Empty;
            detachedSecond.parent = NodeReference.Empty;
            BehaviourTreeData tree = Tree(head, branch, trueNode, falseNode, continuation, detachedFirst, detachedSecond);
            tree.GraphLayout = GraphLayoutData.Create(new[]
            {
                new GraphLayoutEntry(head.uuid, new Vector2(-360f, 220f)),
                new GraphLayoutEntry(branch.uuid, new Vector2(-80f, -220f)),
                new GraphLayoutEntry(trueNode.uuid, new Vector2(260f, 60f)),
                new GraphLayoutEntry(falseNode.uuid, new Vector2(420f, 320f)),
                new GraphLayoutEntry(continuation.uuid, new Vector2(600f, -180f)),
                new GraphLayoutEntry(detachedFirst.uuid, new Vector2(-40f, 620f)),
                new GraphLayoutEntry(detachedSecond.uuid, new Vector2(320f, 420f)),
            });
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            UUID[] selectedRoots = { head.uuid, detachedFirst.uuid, detachedSecond.uuid };
            Dictionary<UUID, Vector2> before = module.Topology.Nodes
                .ToDictionary(node => node.UUID, node => node.Position);
            Dictionary<UUID, Vector2> expected = BuildTopologyTidyTargets(module, selectedRoots);
            module.SetGraphSelection(new TreeNode[] { head, detachedFirst, detachedSecond });

            Assert.That(module.TidySelection(), Is.True);
            foreach (UUID uuid in selectedRoots)
            {
                Assert.That(module.Topology.FindNode(uuid).Position, Is.EqualTo(expected[uuid]).Within(0.01f));
            }
            foreach (KeyValuePair<UUID, Vector2> pair in before.Where(pair => !selectedRoots.Contains(pair.Key)))
            {
                Assert.That(module.Topology.FindNode(pair.Key).Position, Is.EqualTo(pair.Value));
            }

            Vector2 firstVector = expected[detachedFirst.uuid] - expected[head.uuid];
            Vector2 secondVector = expected[detachedSecond.uuid] - expected[head.uuid];
            float crossProduct = firstVector.x * secondVector.y - firstVector.y * secondVector.x;
            Assert.That(Mathf.Abs(crossProduct), Is.GreaterThan(0.01f));

            Undo.PerformUndo();
            module.RebuildTopology();
            foreach (KeyValuePair<UUID, Vector2> pair in before)
            {
                Assert.That(module.Topology.FindNode(pair.Key).Position, Is.EqualTo(pair.Value));
            }
            Undo.PerformRedo();
            module.RebuildTopology();
            foreach (UUID uuid in selectedRoots)
            {
                Assert.That(module.Topology.FindNode(uuid).Position, Is.EqualTo(expected[uuid]).Within(0.01f));
            }
        }

        [Test]
        public void TidySelection_NoOpWithAlreadyTidySelectionDoesNotDirty()
        {
            TestNode head = Node<TestNode>("Head");
            TestNode detached = Node<TestNode>("Detached");
            detached.parent = NodeReference.Empty;
            BehaviourTreeData tree = Tree(head, detached);
            GraphTopology expectedTopology = GraphTopologyBuilder.Build(tree);
            GraphLayoutResolver.ApplyAutoLayout(tree, expectedTopology);
            tree.GraphLayout = GraphLayoutResolver.CreateLayout(expectedTopology);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            module.SetGraphSelection(new[] { head, detached });
            EditorUtility.ClearDirty(tree);

            Assert.That(module.TidySelection(), Is.False);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        [Test]
        public void TidyGroup_UsesTemporaryTopologyLayoutAndCanonicalizesMembers()
        {
            Condition condition = Node<Condition>("Condition");
            Aethiumian.AI.Nodes.Boolean predicate = Node<Aethiumian.AI.Nodes.Boolean>("Predicate");
            TestNode detached = Node<TestNode>("Detached");
            condition.condition = predicate.ToReference();
            predicate.parent = new NodeReference(condition.uuid);
            detached.parent = NodeReference.Empty;
            BehaviourTreeData tree = Tree(condition, predicate, detached);
            tree.GraphLayout = GraphLayoutData.Create(
                new[]
                {
                    new GraphLayoutEntry(condition.uuid, new Vector2(-260f, 180f)),
                    new GraphLayoutEntry(predicate.uuid, new Vector2(80f, 320f)),
                    new GraphLayoutEntry(detached.uuid, new Vector2(260f, -120f)),
                },
                groupEntries: new[]
                {
                    new GraphGroupLayoutEntry(UUID.NewUUID(), "Frame", Color.blue,
                        new[] { condition.uuid, predicate.uuid, detached.uuid }),
                });
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            UUID groupUUID = tree.GraphLayout.Groups[0].UUID;
            Dictionary<UUID, Vector2> before = module.Topology.Nodes
                .ToDictionary(node => node.UUID, node => node.Position);
            UUID[] canonicalRoots = { condition.uuid, detached.uuid };
            Dictionary<UUID, Vector2> expected = BuildTopologyTidyTargets(module, canonicalRoots);

            Assert.That(module.TidyGroup(groupUUID), Is.True);
            foreach (UUID uuid in canonicalRoots)
            {
                Assert.That(module.Topology.FindNode(uuid).Position, Is.EqualTo(expected[uuid]).Within(0.01f));
            }
            Assert.That(module.Topology.FindNode(predicate.uuid).Position, Is.EqualTo(before[predicate.uuid]));
            Assert.That(tree.GraphLayout.Groups[0].Members, Is.EqualTo(new[] { condition.uuid, predicate.uuid, detached.uuid }));

            Undo.PerformUndo();
            module.RebuildTopology();
            foreach (KeyValuePair<UUID, Vector2> pair in before)
            {
                Assert.That(module.Topology.FindNode(pair.Key).Position, Is.EqualTo(pair.Value));
            }
            Undo.PerformRedo();
            module.RebuildTopology();
            foreach (UUID uuid in canonicalRoots)
            {
                Assert.That(module.Topology.FindNode(uuid).Position, Is.EqualTo(expected[uuid]).Within(0.01f));
            }
        }

        [Test]
        public void TidySelection_ConditionEmbeddedNodesUseCanonicalOwnerRoot()
        {
            Condition condition = Node<Condition>("Condition");
            Aethiumian.AI.Nodes.Boolean predicate = Node<Aethiumian.AI.Nodes.Boolean>("Predicate");
            TestNode detached = Node<TestNode>("Detached");
            condition.condition = predicate.ToReference();
            BehaviourTreeData tree = Tree(condition, predicate, detached);
            condition.parent = NodeReference.Empty;
            predicate.parent = new NodeReference(condition.uuid);
            detached.parent = NodeReference.Empty;
            tree.GraphLayout = GraphLayoutData.Create(new[]
            {
                new GraphLayoutEntry(condition.uuid, new Vector2(-260f, 120f)),
                new GraphLayoutEntry(predicate.uuid, new Vector2(40f, 220f)),
                new GraphLayoutEntry(detached.uuid, new Vector2(260f, -100f)),
            });
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            module.Canvas.RefreshPresentationGeometry();
            Vector2 predicateBefore = module.Topology.FindNode(predicate.uuid).Position;
            Vector2 conditionVisualBefore = module.Canvas.Presentation.Find(condition.uuid).Position;
            Vector2 predicateVisualBefore = module.Canvas.Presentation.Find(predicate.uuid).Position;
            UUID conditionReference = condition.condition.UUID;
            UUID predicateParent = predicate.parent.UUID;
            Assert.That(module.Canvas.Presentation.ResolveMovableRoot(predicate.uuid).UUID, Is.EqualTo(condition.uuid));

            module.SetGraphSelection(new TreeNode[] { condition, predicate, detached });
            Assert.That(module.TidySelection(), Is.True);
            Assert.That(module.Topology.FindNode(predicate.uuid).Position, Is.EqualTo(predicateBefore));
            Assert.That(tree.GraphLayout.TryGetPosition(predicate.uuid, out Vector2 persistedPredicate), Is.True);
            Assert.That(persistedPredicate, Is.EqualTo(predicateBefore));
            Vector2 conditionVisualAfter = module.Canvas.Presentation.Find(condition.uuid).Position;
            Vector2 predicateVisualAfter = module.Canvas.Presentation.Find(predicate.uuid).Position;
            Assert.That(predicateVisualAfter - conditionVisualAfter,
                Is.EqualTo(predicateVisualBefore - conditionVisualBefore));
            Assert.That(condition.condition.UUID, Is.EqualTo(conditionReference));
            Assert.That(predicate.parent.UUID, Is.EqualTo(predicateParent));
        }

        /// <summary>Builds the expected topology-aware tidy targets without mutating the editor graph.</summary>
        /// <param name="module">The graph module whose current presentation supplies the selection center.</param>
        /// <param name="rootUUIDs">Canonical authored root UUIDs to arrange.</param>
        /// <returns>Expected persisted positions after temporary auto-layout and center translation.</returns>
        private static Dictionary<UUID, Vector2> BuildTopologyTidyTargets(
            GraphEditorModule module,
            IReadOnlyList<UUID> rootUUIDs)
        {
            Rect currentBounds = GetPresentationBounds(module.Canvas.Presentation, rootUUIDs);
            GraphTopology temporaryTopology = GraphTopologyBuilder.Build(module.TopologyTree);
            Dictionary<UUID, Vector2> currentPositions = module.Topology.Nodes
                .ToDictionary(node => node.UUID, node => node.Position);
            foreach (GraphNodeDescriptor descriptor in temporaryTopology.Nodes)
            {
                if (currentPositions.TryGetValue(descriptor.UUID, out Vector2 position))
                {
                    descriptor.Position = position;
                }
            }

            GraphLayoutResolver.ApplyAutoLayout(module.TopologyTree, temporaryTopology);
            GraphPresentation temporaryPresentation = GraphPresentationBuilder.Build(temporaryTopology);
            GraphPresentationLayout.Layout(temporaryPresentation);
            Vector2 translation = currentBounds.center - GetPresentationBounds(temporaryPresentation, rootUUIDs).center;
            return rootUUIDs.ToDictionary(
                uuid => uuid,
                uuid => temporaryTopology.FindNode(uuid).Position + translation);
        }

        /// <summary>Returns the union of real presentation bounds for canonical authored roots.</summary>
        /// <param name="presentation">The graph presentation to inspect.</param>
        /// <param name="rootUUIDs">Canonical authored root UUIDs.</param>
        /// <returns>The union bounds of all requested roots.</returns>
        private static Rect GetPresentationBounds(GraphPresentation presentation, IReadOnlyList<UUID> rootUUIDs)
        {
            Rect bounds = GraphPresentationLayout.GetBounds(presentation.Find(rootUUIDs[0]));
            for (int index = 1; index < rootUUIDs.Count; index++)
            {
                Rect next = GraphPresentationLayout.GetBounds(presentation.Find(rootUUIDs[index]));
                bounds = Rect.MinMaxRect(
                    Mathf.Min(bounds.xMin, next.xMin),
                    Mathf.Min(bounds.yMin, next.yMin),
                    Mathf.Max(bounds.xMax, next.xMax),
                    Mathf.Max(bounds.yMax, next.yMax));
            }

            return bounds;
        }

        [Test]
        public void Layout_BoundaryPositionsRoundTrip()
        {
            TestNode head = Node<TestNode>("Head");
            BehaviourTreeData tree = Tree(head);
            Vector2 entrancePosition = new(-55f, -144f);
            Vector2 exitPosition = new(85f, 233f);
            tree.GraphLayout = GraphLayoutData.Create(
                new[] { new GraphLayoutEntry(head.uuid, new Vector2(12f, 34f)) },
                entrancePosition: entrancePosition,
                exitPosition: exitPosition);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            GraphLayoutData roundTripped = GraphLayoutResolver.CreateLayout(topology, tree.GraphLayout);
            GraphPresentationItem headItem = presentation.Find(head.uuid);

            Assert.That(presentation.Entrance.Position, Is.EqualTo(new Vector2(
                headItem.Position.x + (headItem.Size.x - presentation.Entrance.Size.x) * 0.5f,
                headItem.Position.y - presentation.Entrance.Size.y + 1f)));
            Assert.That(presentation.Exit.Position, Is.EqualTo(exitPosition));
            Assert.That(roundTripped.HasEntrancePosition, Is.True);
            Assert.That(roundTripped.EntrancePosition, Is.EqualTo(entrancePosition));
            Assert.That(roundTripped.HasExitPosition, Is.True);
            Assert.That(roundTripped.ExitPosition, Is.EqualTo(exitPosition));
        }

        [Test]
        public void AutoLayout_RawReferencesDoNotAffectPositions()
        {
            TestHost head = Node<TestHost>("Head");
            TestNode child = Node<TestNode>("Child");
            head.children = new[] { child.ToReference() };
            head.raw = new RawNodeReference { UUID = child.uuid };
            BehaviourTreeData tree = Tree(head, child);
            GraphTopology hidden = GraphTopologyBuilder.Build(tree);
            GraphTopology shown = GraphTopologyBuilder.Build(tree, includeRawReferences: true);

            GraphLayoutResolver.ApplyAutoLayout(tree, hidden);
            GraphLayoutResolver.ApplyAutoLayout(tree, shown);

            Assert.That(shown.FindNode(head.uuid).Position, Is.EqualTo(hidden.FindNode(head.uuid).Position));
            Assert.That(shown.FindNode(child.uuid).Position, Is.EqualTo(hidden.FindNode(child.uuid).Position));
        }

        [Test]
        public void AutoLayout_UsesDeclarationOrderAndSubtreeWidth()

        {
            TestHost head = Node<TestHost>("Head");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            TestNode grandchild = Node<TestNode>("Grandchild");
            head.children = new[] { first.ToReference(), second.ToReference() };
            first.child = grandchild.ToReference();
            BehaviourTreeData tree = Tree(head, first, second, grandchild);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphLayoutResolver.ApplyAutoLayout(tree, topology);

            GraphNodeDescriptor headNode = topology.FindNode(head.uuid);
            GraphNodeDescriptor firstNode = topology.FindNode(first.uuid);
            GraphNodeDescriptor secondNode = topology.FindNode(second.uuid);
            GraphNodeDescriptor grandchildNode = topology.FindNode(grandchild.uuid);
            Assert.That(firstNode.Position.x, Is.LessThan(secondNode.Position.x));
            Assert.That(firstNode.Position.y, Is.EqualTo(secondNode.Position.y));
            Assert.That(grandchildNode.Position.y, Is.GreaterThan(firstNode.Position.y));
            Assert.That(headNode.Position.y, Is.EqualTo(0f));
            Assert.That(headNode.Position.y, Is.LessThan(firstNode.Position.y));
            Assert.That(
                GraphLayoutResolver.FindPresentationOverlaps(GraphPresentationBuilder.Build(topology)),
                Is.Empty);
        }

        [Test]
        public void AutoLayout_AttachesServiceSubtreeBesideHost()
        {
            TestHost head = Node<TestHost>("Head");
            TestService service = Node<TestService>("Service");
            TestNode serviceChild = Node<TestNode>("Service Child");
            head.services = new List<NodeReference> { service.ToReference() };
            service.child = serviceChild.ToReference();
            BehaviourTreeData tree = Tree(head, service, serviceChild);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphLayoutResolver.ApplyAutoLayout(tree, topology);

            Assert.That(topology.FindNode(service.uuid).Position.x, Is.GreaterThan(topology.FindNode(head.uuid).Position.x));
            Assert.That(topology.FindNode(serviceChild.uuid).Position.y, Is.GreaterThan(topology.FindNode(service.uuid).Position.y));
            Assert.That(topology.FindNode(serviceChild.uuid).IsReachable, Is.True);
        }

        [Test]
        public void AutoLayout_StacksServiceSubtreesWithoutOverlap()
        {
            TestHost head = Node<TestHost>("Head");
            TestService firstService = Node<TestService>("First Service");
            TestService secondService = Node<TestService>("Second Service");
            TestNode child = Node<TestNode>("Child");
            TestNode grandchild = Node<TestNode>("Grandchild");
            head.services = new List<NodeReference> { firstService.ToReference(), secondService.ToReference() };
            firstService.child = child.ToReference();
            child.child = grandchild.ToReference();
            BehaviourTreeData tree = Tree(head, firstService, secondService, child, grandchild);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphLayoutResolver.ApplyAutoLayout(tree, topology);

            GraphNodeDescriptor grandchildNode = topology.FindNode(grandchild.uuid);
            GraphNodeDescriptor secondServiceNode = topology.FindNode(secondService.uuid);
            float grandchildBottom = grandchildNode.Position.y + GraphLayoutResolver.GetNodeSize(grandchildNode).y;
            Assert.That(secondServiceNode.Position.y, Is.GreaterThan(grandchildBottom));
            Assert.That(
                GraphLayoutResolver.FindPresentationOverlaps(GraphPresentationBuilder.Build(topology)),
                Is.Empty);
        }

        [Test]
        public void AutoLayout_ServiceEnvelopeDoesNotOverlapAdjacentMainBranch()
        {
            TestHost head = Node<TestHost>("Head");
            TestHost left = Node<TestHost>("Left Host");
            TestNode right = Node<TestNode>("Right Branch");
            TestService service = Node<TestService>("Left Service");
            TestNode serviceChild = Node<TestNode>("Service Child");
            TestNode serviceGrandchild = Node<TestNode>("Service Grandchild");
            head.children = new[] { left.ToReference(), right.ToReference() };
            left.services = new List<NodeReference> { service.ToReference() };
            service.child = serviceChild.ToReference();
            serviceChild.child = serviceGrandchild.ToReference();
            BehaviourTreeData tree = Tree(head, left, right, service, serviceChild, serviceGrandchild);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphLayoutResolver.ApplyAutoLayout(tree, topology);

            GraphNodeDescriptor rightNode = topology.FindNode(right.uuid);
            GraphNodeDescriptor serviceChildNode = topology.FindNode(serviceChild.uuid);
            float serviceSubtreeRight = serviceChildNode.Position.x + GraphLayoutResolver.GetNodeSize(serviceChildNode).x;
            Assert.That(rightNode.Position.x, Is.GreaterThan(serviceSubtreeRight));
            Assert.That(
                GraphLayoutResolver.FindPresentationOverlaps(GraphPresentationBuilder.Build(topology)),
                Is.Empty);
        }

        [Test]
        public void AutoLayout_ProbabilityBranchesReserveServiceEnvelopes()
        {
            Probability probability = Node<Probability>("Probability");
            TestHost first = Node<TestHost>("First Branch");
            TestHost second = Node<TestHost>("Second Branch");
            TestHost third = Node<TestHost>("Third Branch");
            TestHost fourth = Node<TestHost>("Fourth Branch");
            TestService firstService = Node<TestService>("First Service");
            TestService thirdService = Node<TestService>("Third Service");
            TestNode firstServiceChild = Node<TestNode>("First Service Child");
            TestNode thirdServiceChild = Node<TestNode>("Third Service Child");
            probability.events = new[]
            {
                new Probability.EventWeight { reference = first.ToReference(), weight = 1 },
                new Probability.EventWeight { reference = second.ToReference(), weight = 2 },
                new Probability.EventWeight { reference = third.ToReference(), weight = 3 },
                new Probability.EventWeight { reference = fourth.ToReference(), weight = 4 },
            };
            first.services = new List<NodeReference> { firstService.ToReference() };
            third.services = new List<NodeReference> { thirdService.ToReference() };
            firstService.child = firstServiceChild.ToReference();
            thirdService.child = thirdServiceChild.ToReference();
            BehaviourTreeData tree = Tree(
                probability,
                first,
                second,
                third,
                fourth,
                firstService,
                thirdService,
                firstServiceChild,
                thirdServiceChild);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphLayoutResolver.ApplyAutoLayout(tree, topology);

            Assert.That(topology.FindNode(first.uuid).Position.x, Is.LessThan(topology.FindNode(second.uuid).Position.x));
            Assert.That(topology.FindNode(second.uuid).Position.x, Is.LessThan(topology.FindNode(third.uuid).Position.x));
            Assert.That(topology.FindNode(third.uuid).Position.x, Is.LessThan(topology.FindNode(fourth.uuid).Position.x));
            Assert.That(
                GraphLayoutResolver.FindPresentationOverlaps(GraphPresentationBuilder.Build(topology)),
                Is.Empty);
        }

        [Test]
        public void AutoLayout_MultipleParentsAndCycleTerminateWithOnePositionPerUuid()
        {
            TestHost head = Node<TestHost>("Head");
            TestNode left = Node<TestNode>("Left");
            TestNode right = Node<TestNode>("Right");
            TestNode shared = Node<TestNode>("Shared");
            head.children = new[] { left.ToReference(), right.ToReference() };
            left.child = shared.ToReference();
            right.child = shared.ToReference();
            shared.child = head.ToReference();
            BehaviourTreeData tree = Tree(head, left, right, shared);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            Assert.DoesNotThrow(() => GraphLayoutResolver.ApplyAutoLayout(tree, topology));
            Assert.That(topology.Nodes.Select(node => node.Position).All(position => float.IsFinite(position.x) && float.IsFinite(position.y)), Is.True);
            Assert.That(topology.Edges.Count(edge => edge.Target?.Node == shared), Is.EqualTo(2));
        }

        [Test]
        public void AutoLayout_SequenceEventsFormVerticalContinuationChain()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            TestNode third = Node<TestNode>("Third");
            sequence.events = new[] { first.ToReference(), second.ToReference(), third.ToReference() };
            BehaviourTreeData tree = Tree(sequence, first, second, third);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphLayoutResolver.ApplyAutoLayout(tree, topology);

            GraphNodeDescriptor sequenceNode = topology.FindNode(sequence.uuid);
            GraphNodeDescriptor firstNode = topology.FindNode(first.uuid);
            GraphNodeDescriptor secondNode = topology.FindNode(second.uuid);
            GraphNodeDescriptor thirdNode = topology.FindNode(third.uuid);
            Assert.That(firstNode.Position.y, Is.GreaterThan(sequenceNode.Position.y));
            Assert.That(secondNode.Position.y, Is.GreaterThan(firstNode.Position.y));
            Assert.That(thirdNode.Position.y, Is.GreaterThan(secondNode.Position.y));
            Assert.That(firstNode.Position.x, Is.EqualTo(secondNode.Position.x));
            Assert.That(secondNode.Position.x, Is.EqualTo(thirdNode.Position.x));
        }

        [Test]
        public void AutoLayout_NestedSequenceCompletionPrecedesOuterNext()
        {
            Sequence outer = Node<Sequence>("Outer");
            TestNode first = Node<TestNode>("A");
            Sequence inner = Node<Sequence>("Inner");
            TestNode innerFirst = Node<TestNode>("B");
            TestNode innerLast = Node<TestNode>("C");
            TestNode outerLast = Node<TestNode>("D");
            outer.events = new[] { first.ToReference(), inner.ToReference(), outerLast.ToReference() };
            inner.events = new[] { innerFirst.ToReference(), innerLast.ToReference() };
            BehaviourTreeData tree = Tree(outer, first, inner, innerFirst, innerLast, outerLast);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphLayoutResolver.ApplyAutoLayout(tree, topology);

            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            GraphSequenceScope innerScope = presentation.Find(inner.uuid).SequenceScope;
            Assert.That(topology.FindNode(innerFirst.uuid).Position.y, Is.GreaterThan(topology.FindNode(inner.uuid).Position.y));
            Assert.That(topology.FindNode(innerLast.uuid).Position.y, Is.GreaterThan(topology.FindNode(innerFirst.uuid).Position.y));
            Assert.That(innerScope.CompletionPosition.y, Is.GreaterThan(topology.FindNode(innerLast.uuid).Position.y));
            Assert.That(topology.FindNode(outerLast.uuid).Position.y, Is.GreaterThan(innerScope.CompletionPosition.y));
            Assert.That(GraphLayoutResolver.CreateLayout(topology).Positions.Count, Is.EqualTo(topology.Nodes.Count));
            Assert.That(GraphLayoutResolver.FindPresentationOverlaps(presentation), Is.Empty);
        }

        [Test]
        public void AutoLayout_ConditionUsesCompoundBoundsAndDoesNotPlaceEmbeddedPredicate()
        {
            Condition condition = Node<Condition>("Condition");
            TestNode predicate = Node<TestNode>("Predicate");
            TestNode trueNode = Node<TestNode>("True");
            TestNode falseNode = Node<TestNode>("False");
            condition.condition = predicate.ToReference();
            condition.trueNode = trueNode.ToReference();
            condition.falseNode = falseNode.ToReference();
            BehaviourTreeData tree = Tree(condition, predicate, trueNode, falseNode);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            Vector2 embeddedSentinel = new(137f, 211f);
            topology.FindNode(predicate.uuid).Position = embeddedSentinel;

            GraphLayoutResolver.ApplyAutoLayout(tree, topology);

            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            GraphPresentationItem conditionItem = presentation.Find(condition.uuid);
            GraphNodeDescriptor conditionNode = topology.FindNode(condition.uuid);
            GraphNodeDescriptor trueDescriptor = topology.FindNode(trueNode.uuid);
            GraphNodeDescriptor falseDescriptor = topology.FindNode(falseNode.uuid);
            Assert.That(topology.FindNode(predicate.uuid).Position, Is.EqualTo(embeddedSentinel));
            Assert.That(trueDescriptor.Position.y, Is.EqualTo(falseDescriptor.Position.y));
            Assert.That(trueDescriptor.Position.y, Is.GreaterThanOrEqualTo(conditionNode.Position.y + conditionItem.Size.y));
            Assert.That(trueDescriptor.Position.x, Is.LessThan(falseDescriptor.Position.x));
            Assert.That(GraphLayoutResolver.FindPresentationOverlaps(presentation), Is.Empty);
        }

        [Test]
        public void AutoLayout_DeepSequenceCompletionScopesRemainCollisionFree()
        {
            Sequence outer = Node<Sequence>("Outer");
            Sequence middle = Node<Sequence>("Middle");
            Sequence inner = Node<Sequence>("Inner");
            TestNode leaf = Node<TestNode>("Leaf");
            TestNode middleNext = Node<TestNode>("Middle Next");
            TestNode outerNext = Node<TestNode>("Outer Next");
            outer.events = new[] { middle.ToReference(), outerNext.ToReference() };
            middle.events = new[] { inner.ToReference(), middleNext.ToReference() };
            inner.events = new[] { leaf.ToReference() };
            BehaviourTreeData tree = Tree(outer, middle, inner, leaf, middleNext, outerNext);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphLayoutResolver.ApplyAutoLayout(tree, topology);

            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            GraphSequenceScope innerScope = presentation.Find(inner.uuid).SequenceScope;
            GraphSequenceScope middleScope = presentation.Find(middle.uuid).SequenceScope;
            Assert.That(innerScope.CompletionPosition.y, Is.LessThan(topology.FindNode(middleNext.uuid).Position.y));
            Assert.That(middleScope.CompletionPosition.y, Is.LessThan(topology.FindNode(outerNext.uuid).Position.y));
            Assert.That(GraphLayoutResolver.FindPresentationOverlaps(presentation), Is.Empty);
        }

        [Test]
        public void CollisionAudit_ReportsOverlappingVisibleCards()
        {
            TestHost head = Node<TestHost>("Head");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            head.children = new[] { first.ToReference(), second.ToReference() };
            BehaviourTreeData tree = Tree(head, first, second);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            foreach (GraphNodeDescriptor node in topology.Nodes)
            {
                node.Position = Vector2.zero;
            }

            IReadOnlyList<string> overlaps = GraphLayoutResolver.FindPresentationOverlaps(
                GraphPresentationBuilder.Build(topology));

            Assert.That(overlaps, Is.Not.Empty);
            Assert.That(overlaps.Any(overlap => overlap.Contains("First") && overlap.Contains("Second")), Is.True);
        }

        [Test]
        public void AutoLayout_PacksUnreachableSubtreesWithinAvailableRowWidth()
        {
            TestNode head = Node<TestNode>("Head");
            TestHost first = Node<TestHost>("First");
            TestNode second = Node<TestNode>("Second");
            TestNode third = Node<TestNode>("Third");
            TestNode fourth = Node<TestNode>("Fourth");
            TestNode fifth = Node<TestNode>("Fifth");
            TestNode firstChild = Node<TestNode>("First Child");
            TestNode secondChild = Node<TestNode>("Second Child");
            first.children = new[] { firstChild.ToReference(), secondChild.ToReference() };
            BehaviourTreeData tree = Tree(head, first, second, third, fourth, fifth, firstChild, secondChild);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphLayoutResolver.ApplyAutoLayout(tree, topology);

            Vector2 firstPosition = topology.FindNode(first.uuid).Position;
            Vector2 secondPosition = topology.FindNode(second.uuid).Position;
            Vector2 thirdPosition = topology.FindNode(third.uuid).Position;
            Vector2 fifthPosition = topology.FindNode(fifth.uuid).Position;
            Assert.That(secondPosition.y, Is.GreaterThan(firstPosition.y));
            Assert.That(thirdPosition.y, Is.EqualTo(secondPosition.y));
            Assert.That(thirdPosition.x, Is.GreaterThan(secondPosition.x));
            Assert.That(fifthPosition.y, Is.GreaterThan(thirdPosition.y));
            Assert.That(
                GraphLayoutResolver.FindPresentationOverlaps(GraphPresentationBuilder.Build(topology)),
                Is.Empty);
        }

        [Test]
        public void CommitNodeMove_UndoRedoRestoresLayoutWrite()
        {
            TestNode head = Node<TestNode>("Head");
            BehaviourTreeData tree = Tree(head);
            AIEditorWindow window = ScriptableObject.CreateInstance<AIEditorWindow>();
            hiddenWindows.Add(window);
            window.Load(tree);
            GraphEditorModule module = new(window);
            module.Attach(CreateDeclaredGraphHost(window));
            module.MoveNode(module.Topology.FindNode(head.uuid), new Vector2(75f, 125f));

            module.CommitNodeMove();
            Assert.That(tree.GraphLayout.TryGetPosition(head.uuid, out Vector2 committed), Is.True);
            Assert.That(committed, Is.EqualTo(new Vector2(75f, 125f)));

            Undo.PerformUndo();
            Assert.That(tree.GraphLayout, Is.Null);

            Undo.PerformRedo();
            Assert.That(tree.GraphLayout.TryGetPosition(head.uuid, out Vector2 redone), Is.True);
            Assert.That(redone, Is.EqualTo(new Vector2(75f, 125f)));
        }

        [Test]
        public void MoveSelectedNodes_MovesSelectionAsOneLayoutTransaction()
        {
            TestHost head = Node<TestHost>("Head");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            BehaviourTreeData tree = Tree(head, first, second);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            GraphNodeDescriptor firstDescriptor = module.Topology.FindNode(first.uuid);
            Vector2 firstStart = firstDescriptor.Position;
            Vector2 secondStart = module.Topology.FindNode(second.uuid).Position;
            Vector2 delta = new(45f, 35f);
            module.SetGraphSelection(new TreeNode[] { first, second });

            module.MoveNode(firstDescriptor, firstStart + delta);
            module.CommitNodeMove();

            Assert.That(module.Topology.FindNode(first.uuid).Position, Is.EqualTo(firstStart + delta));
            Assert.That(module.Topology.FindNode(second.uuid).Position, Is.EqualTo(secondStart + delta));
            Assert.That(tree.GraphLayout.TryGetPosition(first.uuid, out Vector2 firstSaved), Is.True);
            Assert.That(firstSaved, Is.EqualTo(firstStart + delta));
        }

        [TestCase(35f, 41f, 24f, 48f)]
        [TestCase(-13f, -37f, -24f, -48f)]
        public void MoveNode_SnapToGridRoundsAnchorAndSupportsUndo(float targetX, float targetY, float expectedX, float expectedY)
        {
            TestNode head = Node<TestNode>("Head");
            BehaviourTreeData tree = Tree(head);
            Vector2 start = new(50f, 50f);
            tree.GraphLayout = GraphLayoutData.Create(new[] { new GraphLayoutEntry(head.uuid, start) });
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            module.SnapToGrid = true;
            EditorUtility.ClearDirty(tree);

            module.MoveNode(module.Topology.FindNode(head.uuid), new Vector2(targetX, targetY));

            Assert.That(module.Topology.FindNode(head.uuid).Position, Is.EqualTo(new Vector2(expectedX, expectedY)));
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
            module.CommitNodeMove();
            Assert.That(tree.GraphLayout.TryGetPosition(head.uuid, out Vector2 committed), Is.True);
            Assert.That(committed, Is.EqualTo(new Vector2(expectedX, expectedY)));

            Undo.PerformUndo();
            Assert.That(tree.GraphLayout.TryGetPosition(head.uuid, out Vector2 restored), Is.True);
            Assert.That(restored, Is.EqualTo(start));
            Undo.PerformRedo();
            Assert.That(tree.GraphLayout.TryGetPosition(head.uuid, out Vector2 redone), Is.True);
            Assert.That(redone, Is.EqualTo(new Vector2(expectedX, expectedY)));
        }

        [Test]
        public void MoveNode_SnapOffPreservesContinuousPositionAndZeroMoveDoesNotDirty()
        {
            TestNode head = Node<TestNode>("Head");
            BehaviourTreeData tree = Tree(head);
            Vector2 start = new(24f, 24f);
            tree.GraphLayout = GraphLayoutData.Create(new[] { new GraphLayoutEntry(head.uuid, start) });
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            EditorUtility.ClearDirty(tree);

            module.SnapToGrid = true;
            module.MoveNode(module.Topology.FindNode(head.uuid), start);
            module.CommitNodeMove();
            Assert.That(EditorUtility.IsDirty(tree), Is.False);

            module.SnapToGrid = false;
            Vector2 continuous = new(35f, 41f);
            module.MoveNode(module.Topology.FindNode(head.uuid), continuous);

            Assert.That(module.Topology.FindNode(head.uuid).Position, Is.EqualTo(continuous));
            module.CommitNodeMove();
            Assert.That(EditorUtility.IsDirty(tree), Is.True);
        }

        [Test]
        public void MoveSelectedNodes_SnapUsesOneAnchorDelta()
        {
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            BehaviourTreeData tree = Tree(first, second);
            Vector2 firstStart = new(10f, 14f);
            Vector2 secondStart = new(130f, 80f);
            tree.GraphLayout = GraphLayoutData.Create(new[]
            {
                new GraphLayoutEntry(first.uuid, firstStart),
                new GraphLayoutEntry(second.uuid, secondStart),
            });
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            module.SetGraphSelection(new TreeNode[] { first, second });
            module.SnapToGrid = true;

            module.MoveNode(module.Topology.FindNode(first.uuid), new Vector2(35f, 41f));

            Vector2 delta = new(14f, 34f);
            Assert.That(module.Topology.FindNode(first.uuid).Position, Is.EqualTo(firstStart + delta));
            Assert.That(module.Topology.FindNode(second.uuid).Position, Is.EqualTo(secondStart + delta));
        }

        [Test]
        public void MoveSelectedNodes_SnapDeduplicatesServiceMovementGroup()
        {
            TestHost head = Node<TestHost>("Head");
            TestService service = Node<TestService>("Service");
            TestNode child = Node<TestNode>("Service Child");
            head.services = new List<NodeReference> { service.ToReference() };
            service.child = child.ToReference();
            BehaviourTreeData tree = Tree(head, service, child);
            Vector2 headStart = new(10f, 10f);
            Vector2 serviceStart = new(100f, 100f);
            Vector2 childStart = new(200f, 200f);
            tree.GraphLayout = GraphLayoutData.Create(new[]
            {
                new GraphLayoutEntry(head.uuid, headStart),
                new GraphLayoutEntry(service.uuid, serviceStart),
                new GraphLayoutEntry(child.uuid, childStart),
            });
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            module.SetGraphSelection(new TreeNode[] { head, service });
            module.SnapToGrid = true;

            module.MoveNode(module.Topology.FindNode(head.uuid), new Vector2(35f, 41f));

            Vector2 delta = new(14f, 38f);
            Assert.That(module.Topology.FindNode(head.uuid).Position, Is.EqualTo(headStart + delta));
            Assert.That(module.Topology.FindNode(service.uuid).Position, Is.EqualTo(serviceStart + delta));
            Assert.That(module.Topology.FindNode(child.uuid).Position, Is.EqualTo(childStart + delta));
        }

        [Test]
        public void MoveCondition_SnapLeavesEmbeddedPredicateInPlace()
        {
            Condition condition = Node<Condition>("Condition");
            TestNode predicate = Node<TestNode>("Predicate");
            condition.condition = predicate.ToReference();
            BehaviourTreeData tree = Tree(condition, predicate);
            Vector2 conditionStart = new(10f, 10f);
            Vector2 predicateStart = new(150f, 100f);
            tree.GraphLayout = GraphLayoutData.Create(new[]
            {
                new GraphLayoutEntry(condition.uuid, conditionStart),
                new GraphLayoutEntry(predicate.uuid, predicateStart),
            });
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            module.SnapToGrid = true;

            module.MoveNode(module.Topology.FindNode(condition.uuid), new Vector2(35f, 41f));

            Assert.That(module.Topology.FindNode(condition.uuid).Position, Is.EqualTo(new Vector2(24f, 48f)));
            Assert.That(module.Topology.FindNode(predicate.uuid).Position, Is.EqualTo(predicateStart));
        }

        [TestCase(35f, 41f, 24f, 48f)]
        [TestCase(-13f, -37f, -24f, -48f)]
        public void MoveExitBoundary_SnapRoundsPositionAndSupportsUndo(float targetX, float targetY, float expectedX, float expectedY)
        {
            TestNode head = Node<TestNode>("Head");
            BehaviourTreeData tree = Tree(head);
            Vector2 start = new(50f, 50f);
            tree.GraphLayout = GraphLayoutData.Create(
                new[] { new GraphLayoutEntry(head.uuid, new Vector2(120f, 160f)) },
                exitPosition: start);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            module.SnapToGrid = true;
            GraphPresentationItem exit = module.Canvas.Presentation.Exit;
            Assert.That(exit.Position, Is.EqualTo(start));

            Vector2 applied = module.MoveBoundary(exit, new Vector2(targetX, targetY));

            Assert.That(applied, Is.EqualTo(new Vector2(expectedX, expectedY)));
            Assert.That(exit.Position, Is.EqualTo(new Vector2(expectedX, expectedY)));
            module.CommitBoundaryMove();
            Assert.That(tree.GraphLayout.ExitPosition, Is.EqualTo(new Vector2(expectedX, expectedY)));

            Undo.PerformUndo();
            Assert.That(tree.GraphLayout.HasExitPosition, Is.True);
            Assert.That(tree.GraphLayout.ExitPosition, Is.EqualTo(start));
            Undo.PerformRedo();
            Assert.That(tree.GraphLayout.ExitPosition, Is.EqualTo(new Vector2(expectedX, expectedY)));
        }

        [Test]
        public void MoveBoundary_SnapHandlesUnconnectedEntranceAndRejectsConnectedEntrance()
        {
            TestNode detached = Node<TestNode>("Detached");
            BehaviourTreeData unconnectedTree = Tree(detached);
            unconnectedTree.headNodeUUID = UUID.Empty;
            Vector2 entranceStart = new(50f, 50f);
            unconnectedTree.GraphLayout = GraphLayoutData.Create(
                new[] { new GraphLayoutEntry(detached.uuid, new Vector2(120f, 160f)) },
                entrancePosition: entranceStart);
            GraphEditorModule unconnectedModule = CreateHiddenGraphModule(unconnectedTree);
            unconnectedModule.SnapToGrid = true;

            GraphPresentationItem entrance = unconnectedModule.Canvas.Presentation.Entrance;
            Assert.That(unconnectedModule.MoveBoundary(entrance, new Vector2(35f, 41f)), Is.EqualTo(new Vector2(24f, 48f)));
            Assert.That(entrance.Position, Is.EqualTo(new Vector2(24f, 48f)));

            TestNode head = Node<TestNode>("Head");
            BehaviourTreeData connectedTree = Tree(head);
            GraphEditorModule connectedModule = CreateHiddenGraphModule(connectedTree);
            connectedModule.SnapToGrid = true;
            GraphPresentationItem attachedEntrance = connectedModule.Canvas.Presentation.Entrance;
            Vector2 attachedStart = attachedEntrance.Position;

            Assert.That(connectedModule.MoveBoundary(attachedEntrance, attachedStart + new Vector2(35f, 41f)), Is.EqualTo(attachedStart));
            Assert.That(attachedEntrance.Position, Is.EqualTo(attachedStart));
        }

        [Test]
        public void MoveExitBoundary_SnapOffPreservesContinuousPositionAndZeroMoveStaysClean()
        {
            TestNode head = Node<TestNode>("Head");
            BehaviourTreeData tree = Tree(head);
            Vector2 start = new(24f, 24f);
            tree.GraphLayout = GraphLayoutData.Create(
                new[] { new GraphLayoutEntry(head.uuid, new Vector2(120f, 160f)) },
                exitPosition: start);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            GraphPresentationItem exit = module.Canvas.Presentation.Exit;
            EditorUtility.ClearDirty(tree);

            module.SnapToGrid = true;
            Assert.That(module.MoveBoundary(exit, start), Is.EqualTo(start));
            Assert.That(EditorUtility.IsDirty(tree), Is.False);

            module.SnapToGrid = false;
            Vector2 continuous = new(35f, 41f);
            Assert.That(module.MoveBoundary(exit, continuous), Is.EqualTo(continuous));
            Assert.That(exit.Position, Is.EqualTo(continuous));
            module.CommitBoundaryMove();
            Assert.That(tree.GraphLayout.ExitPosition, Is.EqualTo(continuous));
        }

        [TestCase((int)GraphSelectionAlignment.Left)]
        [TestCase((int)GraphSelectionAlignment.Right)]
        [TestCase((int)GraphSelectionAlignment.Top)]
        [TestCase((int)GraphSelectionAlignment.Center)]
        [TestCase((int)GraphSelectionAlignment.Middle)]
        [TestCase((int)GraphSelectionAlignment.Bottom)]
        public void GraphSelection_AlignsSelectedNodesByVisualBounds(int alignmentValue)
        {
            GraphSelectionAlignment alignment = (GraphSelectionAlignment)alignmentValue;
            TestHost head = Node<TestHost>("Head");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            TestNode untouched = Node<TestNode>("Untouched");
            BehaviourTreeData tree = Tree(head, first, second, untouched);
            tree.GraphLayout = GraphLayoutData.Create(new[]
            {
                new GraphLayoutEntry(head.uuid, new Vector2(40f, 20f)),
                new GraphLayoutEntry(first.uuid, new Vector2(250f, 180f)),
                new GraphLayoutEntry(second.uuid, new Vector2(520f, 360f)),
                new GraphLayoutEntry(untouched.uuid, new Vector2(900f, 700f)),
            });
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            module.SetGraphSelection(new TreeNode[] { head, first, second });
            Vector2 untouchedStart = module.Topology.FindNode(untouched.uuid).Position;

            Assert.That(module.AlignSelectedNodes(alignment), Is.True);

            List<Rect> bounds = new TreeNode[] { head, first, second }
                .Select(node => GraphPresentationLayout.GetBounds(module.Canvas.Presentation.Find(node.uuid)))
                .ToList();
            switch (alignment)
            {
                case GraphSelectionAlignment.Left:
                    Assert.That(bounds.Select(item => item.xMin).Distinct().Count(), Is.EqualTo(1));
                    break;
                case GraphSelectionAlignment.Center:
                    Assert.That(bounds.Select(item => item.center.x).Distinct().Count(), Is.EqualTo(1));
                    break;
                case GraphSelectionAlignment.Right:
                    Assert.That(bounds.Select(item => item.xMax).Distinct().Count(), Is.EqualTo(1));
                    break;
                case GraphSelectionAlignment.Top:
                    Assert.That(bounds.Select(item => item.yMin).Distinct().Count(), Is.EqualTo(1));
                    break;
                case GraphSelectionAlignment.Middle:
                    Assert.That(bounds.Select(item => item.center.y).Distinct().Count(), Is.EqualTo(1));
                    break;
                case GraphSelectionAlignment.Bottom:
                    Assert.That(bounds.Select(item => item.yMax).Distinct().Count(), Is.EqualTo(1));
                    break;
            }

            Assert.That(module.Topology.FindNode(untouched.uuid).Position, Is.EqualTo(untouchedStart));
            Assert.That(EditorUtility.IsDirty(tree), Is.True);
        }

        [Test]
        public void GraphSelection_AlignCenterPreservesCentersAndPacksVisualBoundsVertically()
        {
            TestNode head = Node<TestNode>("Head");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            BehaviourTreeData tree = Tree(head, first, second);
            tree.GraphLayout = GraphLayoutData.Create(new[]
            {
                new GraphLayoutEntry(head.uuid, new Vector2(0f, 0f)),
                new GraphLayoutEntry(first.uuid, new Vector2(260f, 10f)),
                new GraphLayoutEntry(second.uuid, new Vector2(520f, 20f)),
            });
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            module.SetGraphSelection(new TreeNode[] { head, first, second });

            Assert.That(module.AlignSelectedNodes(GraphSelectionAlignment.Center), Is.True);

            List<Rect> bounds = new TreeNode[] { head, first, second }
                .Select(node => GraphPresentationLayout.GetBounds(module.Canvas.Presentation.Find(node.uuid)))
                .OrderBy(bounds => bounds.yMin)
                .ToList();
            Assert.That(bounds.Select(bounds => bounds.center.x).Distinct().Count(), Is.EqualTo(1));
            Assert.That(bounds[0].yMin, Is.EqualTo(0f));
            Assert.That(bounds[1].yMin - bounds[0].yMax,
                Is.GreaterThanOrEqualTo(GraphPresentationMetrics.SelectionLayoutMinimumGap));
            Assert.That(bounds[2].yMin - bounds[1].yMax,
                Is.GreaterThanOrEqualTo(GraphPresentationMetrics.SelectionLayoutMinimumGap));
        }

        [Test]
        public void GraphSelection_AlignTopPacksCompoundVisualBoundsHorizontally()
        {
            TestNode head = Node<TestNode>("Head");
            Condition condition = Node<Condition>("Condition");
            TestNode predicate = Node<TestNode>("Predicate");
            TestNode trueNode = Node<TestNode>("True");
            TestNode falseNode = Node<TestNode>("False");
            TestNode detached = Node<TestNode>("Detached");
            condition.condition = predicate.ToReference();
            condition.trueNode = trueNode.ToReference();
            condition.falseNode = falseNode.ToReference();
            BehaviourTreeData tree = Tree(head, condition, predicate, trueNode, falseNode, detached);
            tree.GraphLayout = GraphLayoutData.Create(new[]
            {
                new GraphLayoutEntry(head.uuid, new Vector2(0f, 0f)),
                new GraphLayoutEntry(condition.uuid, new Vector2(0f, 100f)),
                new GraphLayoutEntry(predicate.uuid, new Vector2(0f, 0f)),
                new GraphLayoutEntry(trueNode.uuid, new Vector2(0f, 0f)),
                new GraphLayoutEntry(falseNode.uuid, new Vector2(0f, 0f)),
                new GraphLayoutEntry(detached.uuid, new Vector2(100f, 120f)),
            });
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            module.SetGraphSelection(new TreeNode[] { condition, detached });

            Assert.That(module.AlignSelectedNodes(GraphSelectionAlignment.Top), Is.True);

            Rect conditionBounds = GraphPresentationLayout.GetBounds(module.Canvas.Presentation.Find(condition.uuid));
            Rect detachedBounds = GraphPresentationLayout.GetBounds(module.Canvas.Presentation.Find(detached.uuid));
            Assert.That(conditionBounds.width,
                Is.GreaterThan(GraphLayoutResolver.GetNodeSize(module.Topology.FindNode(condition.uuid)).x));
            Assert.That(conditionBounds.yMin, Is.EqualTo(detachedBounds.yMin));
            Assert.That(detachedBounds.xMin - conditionBounds.xMax,
                Is.GreaterThanOrEqualTo(GraphPresentationMetrics.SelectionLayoutMinimumGap));
        }

        [Test]
        public void GraphSelection_FoldsSelectedSequenceMembersIntoOneCompositeMoveUnit()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            TestNode third = Node<TestNode>("Third");
            TestNode detached = Node<TestNode>("Detached");
            TestNode secondDetached = Node<TestNode>("Second Detached");
            sequence.events = new[] { first.ToReference(), second.ToReference(), third.ToReference() };
            BehaviourTreeData tree = Tree(sequence, first, second, third, detached, secondDetached);
            tree.GraphLayout = GraphLayoutData.Create(new[]
            {
                new GraphLayoutEntry(sequence.uuid, new Vector2(0f, 0f)),
                new GraphLayoutEntry(first.uuid, new Vector2(20f, 80f)),
                new GraphLayoutEntry(second.uuid, new Vector2(40f, 140f)),
                new GraphLayoutEntry(third.uuid, new Vector2(60f, 200f)),
                new GraphLayoutEntry(detached.uuid, new Vector2(400f, 300f)),
                new GraphLayoutEntry(secondDetached.uuid, new Vector2(600f, 520f)),
            });
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            Vector2 firstOffset = module.Topology.FindNode(first.uuid).Position - module.Topology.FindNode(sequence.uuid).Position;
            Vector2 secondOffset = module.Topology.FindNode(second.uuid).Position - module.Topology.FindNode(sequence.uuid).Position;
            Vector2 thirdOffset = module.Topology.FindNode(third.uuid).Position - module.Topology.FindNode(sequence.uuid).Position;
            module.SetGraphSelection(new TreeNode[] { sequence, first, second, third, detached, secondDetached });

            Assert.That(module.DistributeSelectedNodes(GraphSelectionDistribution.Vertical), Is.True);

            GraphNodeDescriptor sequenceNode = module.Topology.FindNode(sequence.uuid);
            Assert.That(module.Topology.FindNode(first.uuid).Position - sequenceNode.Position, Is.EqualTo(firstOffset));
            Assert.That(module.Topology.FindNode(second.uuid).Position - sequenceNode.Position, Is.EqualTo(secondOffset));
            Assert.That(module.Topology.FindNode(third.uuid).Position - sequenceNode.Position, Is.EqualTo(thirdOffset));
            List<Rect> bounds = new TreeNode[] { sequence, detached, secondDetached }
                .Select(node => GraphPresentationLayout.GetBounds(module.Canvas.Presentation.Find(node.uuid)))
                .OrderBy(bounds => bounds.yMin)
                .ToList();
            float firstGap = bounds[1].yMin - bounds[0].yMax;
            float secondGap = bounds[2].yMin - bounds[1].yMax;
            Assert.That(firstGap, Is.GreaterThanOrEqualTo(GraphPresentationMetrics.SelectionLayoutMinimumGap));
            Assert.That(secondGap, Is.EqualTo(firstGap).Within(0.01f));
        }

        [TestCase((int)GraphSelectionDistribution.Horizontal)]
        [TestCase((int)GraphSelectionDistribution.Vertical)]
        public void GraphSelection_DistributeCrowdedBoundsExpandsWithMinimumGap(int distributionValue)
        {
            GraphSelectionDistribution distribution = (GraphSelectionDistribution)distributionValue;
            TestNode head = Node<TestNode>("Head");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            BehaviourTreeData tree = Tree(head, first, second);
            tree.GraphLayout = GraphLayoutData.Create(new[]
            {
                new GraphLayoutEntry(head.uuid, new Vector2(0f, 0f)),
                new GraphLayoutEntry(first.uuid, new Vector2(10f, 10f)),
                new GraphLayoutEntry(second.uuid, new Vector2(20f, 20f)),
            });
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            module.SetGraphSelection(new TreeNode[] { head, first, second });

            Assert.That(module.DistributeSelectedNodes(distribution), Is.True);

            List<Rect> bounds = new TreeNode[] { head, first, second }
                .Select(node => GraphPresentationLayout.GetBounds(module.Canvas.Presentation.Find(node.uuid)))
                .OrderBy(bounds => distribution == GraphSelectionDistribution.Horizontal ? bounds.xMin : bounds.yMin)
                .ToList();
            float firstStart = distribution == GraphSelectionDistribution.Horizontal ? bounds[0].xMin : bounds[0].yMin;
            float firstGap = distribution == GraphSelectionDistribution.Horizontal
                ? bounds[1].xMin - bounds[0].xMax
                : bounds[1].yMin - bounds[0].yMax;
            float secondGap = distribution == GraphSelectionDistribution.Horizontal
                ? bounds[2].xMin - bounds[1].xMax
                : bounds[2].yMin - bounds[1].yMax;
            Assert.That(firstStart, Is.EqualTo(0f));
            Assert.That(firstGap, Is.EqualTo(GraphPresentationMetrics.SelectionLayoutMinimumGap));
            Assert.That(secondGap, Is.EqualTo(GraphPresentationMetrics.SelectionLayoutMinimumGap));
        }

        [TestCase((int)GraphSelectionDistribution.Horizontal)]
        [TestCase((int)GraphSelectionDistribution.Vertical)]
        public void GraphSelection_DistributesSelectedNodesWithOneUndo(int distributionValue)
        {
            GraphSelectionDistribution distribution = (GraphSelectionDistribution)distributionValue;
            TestNode head = Node<TestNode>("Head");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            TestNode untouched = Node<TestNode>("Untouched");
            BehaviourTreeData tree = Tree(head, first, second, untouched);
            Vector2 headStart = new(20f, 30f);
            Vector2 firstStart = new(240f, 260f);
            Vector2 secondStart = new(680f, 620f);
            tree.GraphLayout = GraphLayoutData.Create(new[]
            {
                new GraphLayoutEntry(head.uuid, headStart),
                new GraphLayoutEntry(first.uuid, firstStart),
                new GraphLayoutEntry(second.uuid, secondStart),
                new GraphLayoutEntry(untouched.uuid, new Vector2(900f, 900f)),
            });
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            module.SetGraphSelection(new TreeNode[] { head, first, second });
            Vector2 untouchedStart = module.Topology.FindNode(untouched.uuid).Position;

            Assert.That(module.DistributeSelectedNodes(distribution), Is.True);

            List<Rect> bounds = new TreeNode[] { head, first, second }
                .Select(node => GraphPresentationLayout.GetBounds(module.Canvas.Presentation.Find(node.uuid)))
                .OrderBy(item => distribution == GraphSelectionDistribution.Horizontal ? item.xMin : item.yMin)
                .ToList();
            if (distribution == GraphSelectionDistribution.Horizontal)
            {
                Assert.That(bounds[0].xMin, Is.EqualTo(headStart.x).Within(0.01f));
                Assert.That(bounds[2].xMax, Is.EqualTo(secondStart.x + bounds[2].width).Within(0.01f));
                Assert.That(bounds[1].xMin - bounds[0].xMax, Is.EqualTo(bounds[2].xMin - bounds[1].xMax).Within(0.01f));
            }
            else
            {
                Assert.That(bounds[0].yMin, Is.EqualTo(headStart.y).Within(0.01f));
                Assert.That(bounds[2].yMax, Is.EqualTo(secondStart.y + bounds[2].height).Within(0.01f));
                Assert.That(bounds[1].yMin - bounds[0].yMax, Is.EqualTo(bounds[2].yMin - bounds[1].yMax).Within(0.01f));
            }

            Assert.That(module.Topology.FindNode(untouched.uuid).Position, Is.EqualTo(untouchedStart));
            Assert.That(EditorUtility.IsDirty(tree), Is.True);
            Undo.PerformUndo();
            Assert.That(tree.GraphLayout.TryGetPosition(head.uuid, out Vector2 restoredHead), Is.True);
            Assert.That(restoredHead, Is.EqualTo(headStart));
            Assert.That(tree.GraphLayout.TryGetPosition(first.uuid, out Vector2 restoredFirst), Is.True);
            Assert.That(restoredFirst, Is.EqualTo(firstStart));
            Assert.That(tree.GraphLayout.TryGetPosition(second.uuid, out Vector2 restoredSecond), Is.True);
            Assert.That(restoredSecond, Is.EqualTo(secondStart));
            Undo.PerformRedo();
            Assert.That(tree.GraphLayout.TryGetPosition(head.uuid, out _), Is.True);
        }

        [Test]
        public void Resolve_VersionOneCoordinatesRemainSupportedWithoutDirtyingTree()
        {
            TestNode head = Node<TestNode>("Head");
            BehaviourTreeData tree = Tree(head);
            tree.GraphLayout = GraphLayoutData.Create(new[]
            {
                new GraphLayoutEntry(head.uuid, new Vector2(41f, 73f)),
            });
            typeof(GraphLayoutData).GetField("version", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(tree.GraphLayout, 1);
            EditorUtility.ClearDirty(tree);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphLayoutResolver.Resolve(tree, topology);

            Assert.That(topology.FindNode(head.uuid).Position, Is.EqualTo(new Vector2(41f, 73f)));
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        [Test]
        public void MoveHost_MovesEnabledServiceScopeAndCommitsCurrentVersionOnce()
        {
            TestHost head = Node<TestHost>("Head");
            TestService service = Node<TestService>("Service");
            TestNode child = Node<TestNode>("Service Child");
            head.services = new List<NodeReference> { service.ToReference() };
            service.child = child.ToReference();
            BehaviourTreeData tree = Tree(head, service, child);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            Vector2 headStart = module.Topology.FindNode(head.uuid).Position;
            Vector2 serviceStart = module.Topology.FindNode(service.uuid).Position;
            Vector2 childStart = module.Topology.FindNode(child.uuid).Position;
            Vector2 delta = new(37f, 29f);

            module.MoveNode(module.Topology.FindNode(head.uuid), headStart + delta);

            Assert.That(module.Topology.FindNode(service.uuid).Position, Is.EqualTo(serviceStart + delta));
            Assert.That(module.Topology.FindNode(child.uuid).Position, Is.EqualTo(childStart + delta));
            Assert.That(tree.GraphLayout, Is.Null);
            module.CommitNodeMove();
            Assert.That(tree.GraphLayout.Version, Is.EqualTo(GraphLayoutData.CurrentVersion));
            Assert.That(tree.GraphLayout.GetServiceFollowParent(service.uuid), Is.True);
        }

        [Test]
        public void MoveHost_DoesNotMoveDisabledServiceScope()
        {
            TestHost head = Node<TestHost>("Head");
            TestService service = Node<TestService>("Service");
            TestNode child = Node<TestNode>("Service Child");
            head.services = new List<NodeReference> { service.ToReference() };
            service.child = child.ToReference();
            BehaviourTreeData tree = Tree(head, service, child);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            module.ToggleServiceFollowParent(service.uuid);
            Vector2 serviceStart = module.Topology.FindNode(service.uuid).Position;
            Vector2 childStart = module.Topology.FindNode(child.uuid).Position;
            GraphNodeDescriptor headNode = module.Topology.FindNode(head.uuid);

            module.MoveNode(headNode, headNode.Position + new Vector2(50f, 25f));

            Assert.That(module.Topology.FindNode(service.uuid).Position, Is.EqualTo(serviceStart));
            Assert.That(module.Topology.FindNode(child.uuid).Position, Is.EqualTo(childStart));
            Assert.That(tree.GraphLayout.GetServiceFollowParent(service.uuid), Is.False);
        }

        [Test]
        public void MoveServiceCard_MovesCompleteScopeRegardlessOfFollowSetting()
        {
            TestHost head = Node<TestHost>("Head");
            TestService service = Node<TestService>("Service");
            TestNode child = Node<TestNode>("Service Child");
            head.services = new List<NodeReference> { service.ToReference() };
            service.child = child.ToReference();
            BehaviourTreeData tree = Tree(head, service, child);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            module.ToggleServiceFollowParent(service.uuid);
            GraphNodeDescriptor serviceNode = module.Topology.FindNode(service.uuid);
            Vector2 childStart = module.Topology.FindNode(child.uuid).Position;
            Vector2 delta = new(-30f, 44f);

            module.MoveNode(serviceNode, serviceNode.Position + delta);

            Assert.That(module.Topology.FindNode(child.uuid).Position, Is.EqualTo(childStart + delta));
        }

        [Test]
        public void MoveServiceCard_RespectsNestedServiceFollowSetting()
        {
            TestHost head = Node<TestHost>("Head");
            TestService outerService = Node<TestService>("Outer Service");
            TestHost nestedHost = Node<TestHost>("Nested Host");
            TestService nestedService = Node<TestService>("Nested Service");
            head.services = new List<NodeReference> { outerService.ToReference() };
            outerService.child = nestedHost.ToReference();
            nestedHost.services = new List<NodeReference> { nestedService.ToReference() };
            BehaviourTreeData tree = Tree(head, outerService, nestedHost, nestedService);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            module.ToggleServiceFollowParent(nestedService.uuid);
            GraphNodeDescriptor outer = module.Topology.FindNode(outerService.uuid);
            Vector2 nestedHostStart = module.Topology.FindNode(nestedHost.uuid).Position;
            Vector2 nestedServiceStart = module.Topology.FindNode(nestedService.uuid).Position;
            Vector2 delta = new(28f, 36f);

            module.MoveNode(outer, outer.Position + delta);

            Assert.That(module.Topology.FindNode(nestedHost.uuid).Position, Is.EqualTo(nestedHostStart + delta));
            Assert.That(module.Topology.FindNode(nestedService.uuid).Position, Is.EqualTo(nestedServiceStart));
        }

        [Test]
        public void AutoLayout_ProbabilityPlacesCompletionBelowCandidateEnvelopes()
        {
            Sequence outer = Node<Sequence>("Outer");
            Probability probability = Node<Probability>("Probability");
            TestHost first = Node<TestHost>("First");
            TestNode firstChild = Node<TestNode>("First Child");
            TestNode second = Node<TestNode>("Second");
            TestNode after = Node<TestNode>("After");
            outer.events = new[] { probability.ToReference(), after.ToReference() };
            probability.events = new[]
            {
                new Probability.EventWeight { weight = 2, reference = first.ToReference() },
                new Probability.EventWeight { weight = 1, reference = second.ToReference() },
            };
            first.children = new[] { firstChild.ToReference() };
            BehaviourTreeData tree = Tree(outer, probability, first, firstChild, second, after);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphLayoutResolver.ApplyAutoLayout(tree, topology);
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            GraphProbabilityScope scope = presentation.Find(probability.uuid).ProbabilityScope;
            Rect firstBounds = GraphPresentationLayout.GetBounds(presentation.Find(first.uuid));
            Rect secondBounds = GraphPresentationLayout.GetBounds(presentation.Find(second.uuid));

            Assert.That(scope.CompletionPosition.y, Is.GreaterThan(Mathf.Max(firstBounds.yMax, secondBounds.yMax)));
            Assert.That(topology.FindNode(after.uuid).Position.y, Is.GreaterThan(scope.CompletionPosition.y));
            Assert.That(GraphLayoutResolver.FindPresentationOverlaps(presentation), Is.Empty);
        }

        [TestCase(Loop.LoopType.@while)]
        [TestCase(Loop.LoopType.doWhile)]
        [TestCase(Loop.LoopType.@for)]
        public void AutoLayout_LoopModesRemainCollisionFree(Loop.LoopType mode)
        {
            Sequence outer = Node<Sequence>("Outer");
            Loop loop = Node<Loop>("Loop");
            TestNode condition = Node<TestNode>("Condition");
            Sequence body = Node<Sequence>("Body");
            TestNode bodyLeaf = Node<TestNode>("Body Leaf");
            TestNode after = Node<TestNode>("After");
            outer.events = new[] { loop.ToReference(), after.ToReference() };
            loop.loopType = mode;
            loop.condition = condition.ToReference();
            loop.events = new[] { body.ToReference() };
            body.events = new[] { bodyLeaf.ToReference() };
            BehaviourTreeData tree = Tree(outer, loop, condition, body, bodyLeaf, after);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphLayoutResolver.ApplyAutoLayout(tree, topology);

            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            GraphLoopScope scope = presentation.Find(loop.uuid).LoopScope;
            Rect bodyBounds = GraphPresentationLayout.GetBounds(presentation.Find(body.uuid));
            Rect conditionBounds = GraphPresentationLayout.GetBounds(scope.Condition);
            Rect completionBounds = new(scope.CompletionPosition, scope.CompletionSize);
            Rect afterBounds = GraphPresentationLayout.GetBounds(presentation.Find(after.uuid));
            Rect structureBounds = Rect.MinMaxRect(
                Mathf.Min(conditionBounds.xMin, scope.BodyFrameBounds.xMin),
                Mathf.Min(conditionBounds.yMin, scope.BodyFrameBounds.yMin),
                Mathf.Max(conditionBounds.xMax, scope.BodyFrameBounds.xMax),
                Mathf.Max(conditionBounds.yMax, scope.BodyFrameBounds.yMax));
            Assert.That(scope.BodyFrameBounds.xMin, Is.LessThanOrEqualTo(bodyBounds.xMin));
            Assert.That(scope.BodyFrameBounds.xMax, Is.GreaterThanOrEqualTo(bodyBounds.xMax));
            Assert.That(scope.BodyFrameBounds.yMin, Is.LessThanOrEqualTo(bodyBounds.yMin));
            Assert.That(scope.BodyFrameBounds.yMax, Is.GreaterThanOrEqualTo(bodyBounds.yMax));
            Assert.That(scope.BodyFrameBounds.Overlaps(conditionBounds), Is.False);
            Assert.That(scope.BodyFrameBounds.Overlaps(completionBounds), Is.False);
            Assert.That(completionBounds.yMin, Is.GreaterThan(structureBounds.yMax));
            Assert.That(completionBounds.center.x, Is.EqualTo(structureBounds.center.x).Within(0.01f));
            Assert.That(scope.ReturnRailX, Is.LessThan(scope.BodyFrameBounds.xMin));
            Assert.That(scope.ExitRailX, Is.GreaterThan(scope.BodyFrameBounds.xMax));
            Assert.That(scope.Bounds.xMin, Is.LessThanOrEqualTo(scope.ReturnRailX));
            Assert.That(scope.Bounds.xMax, Is.GreaterThanOrEqualTo(scope.ExitRailX));
            Assert.That(afterBounds.yMin, Is.GreaterThan(completionBounds.yMax));
            Assert.That(afterBounds.center.x, Is.EqualTo(completionBounds.center.x).Within(0.01f));
            Assert.That(GraphLayoutResolver.FindPresentationOverlaps(presentation), Is.Empty);
        }

        [Test]
        public void AutoLayout_SelfReferencingLoopTerminatesWithOneCompletion()
        {
            Loop loop = Node<Loop>("Loop");
            loop.loopType = Loop.LoopType.@while;
            loop.condition = loop.ToReference();
            loop.events = new[] { loop.ToReference() };
            BehaviourTreeData tree = Tree(loop);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            Assert.DoesNotThrow(() => GraphLayoutResolver.ApplyAutoLayout(tree, topology));
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            Assert.DoesNotThrow(() => GraphPresentationLayout.Layout(presentation));
            Assert.That(presentation.CompletionScopes.Count(scope => scope.Owner.Node?.Node == loop), Is.EqualTo(1));
            Assert.That(presentation.Relations.Count(relation =>
                relation.Kind == GraphPresentationRelationKind.LoopRepeat), Is.EqualTo(1));
            Assert.That(GraphLayoutResolver.FindPresentationOverlaps(presentation), Is.Empty);
        }

        [Test]
        public void AutoLayout_ConditionBlockPlacesCompletionBeforeOuterNext()
        {
            Sequence outer = Node<Sequence>("Outer");
            Condition condition = Node<Condition>("Condition");
            TestNode predicate = Node<TestNode>("Predicate");
            Sequence trueSequence = Node<Sequence>("True Sequence");
            TestNode trueLeaf = Node<TestNode>("True Leaf");
            TestNode falseNode = Node<TestNode>("False");
            TestNode after = Node<TestNode>("After");
            outer.events = new[] { condition.ToReference(), after.ToReference() };
            condition.condition = predicate.ToReference();
            condition.trueNode = trueSequence.ToReference();
            condition.falseNode = falseNode.ToReference();
            trueSequence.events = new[] { trueLeaf.ToReference() };
            BehaviourTreeData tree = Tree(outer, condition, predicate, trueSequence, trueLeaf, falseNode, after);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphLayoutResolver.ApplyAutoLayout(tree, topology);

            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            GraphConditionScope scope = presentation.Find(condition.uuid).ConditionScope;
            Rect trueBounds = GraphPresentationLayout.GetBounds(presentation.Find(trueSequence.uuid));
            Rect falseBounds = GraphPresentationLayout.GetBounds(presentation.Find(falseNode.uuid));
            Assert.That(trueBounds.Overlaps(falseBounds), Is.False);
            Assert.That(scope.CompletionPosition.y, Is.GreaterThan(Mathf.Max(trueBounds.yMax, falseBounds.yMax)));
            Assert.That(topology.FindNode(after.uuid).Position.y, Is.GreaterThan(scope.CompletionPosition.y + scope.CompletionSize.y));
            Assert.That(GraphLayoutResolver.FindPresentationOverlaps(presentation), Is.Empty);
        }

        [Test]
        public void AutoLayout_ConditionCompletionClearsBranchServiceSubtree()
        {
            Condition condition = Node<Condition>("Condition");
            TestNode predicate = Node<TestNode>("Predicate");
            TestHost trueHost = Node<TestHost>("True Host");
            TestNode trueChild = Node<TestNode>("True Child");
            TestService service = Node<TestService>("Service");
            TestNode serviceChild = Node<TestNode>("Service Child");
            TestNode falseNode = Node<TestNode>("False");
            condition.condition = predicate.ToReference();
            condition.trueNode = trueHost.ToReference();
            condition.falseNode = falseNode.ToReference();
            trueHost.children = new[] { trueChild.ToReference() };
            trueHost.services = new List<NodeReference> { service.ToReference() };
            service.child = serviceChild.ToReference();
            BehaviourTreeData tree = Tree(condition, predicate, trueHost, trueChild, service, serviceChild, falseNode);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphLayoutResolver.ApplyAutoLayout(tree, topology);

            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            GraphConditionScope scope = presentation.Find(condition.uuid).ConditionScope;
            float serviceBottom = topology.FindNode(serviceChild.uuid).Position.y
                + GraphLayoutResolver.GetNodeSize(topology.FindNode(serviceChild.uuid)).y;
            Assert.That(scope.CompletionPosition.y, Is.GreaterThan(serviceBottom));
            Assert.That(GraphLayoutResolver.FindPresentationOverlaps(presentation), Is.Empty);
        }

        [Test]
        public void AutoLayout_SelfReferencingConditionTerminatesWithOneCompletion()
        {
            Condition condition = Node<Condition>("Condition");
            TestNode predicate = Node<TestNode>("Predicate");
            condition.condition = predicate.ToReference();
            condition.trueNode = condition.ToReference();
            condition.falseNode = NodeReference.Empty;
            BehaviourTreeData tree = Tree(condition, predicate);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            Assert.DoesNotThrow(() => GraphLayoutResolver.ApplyAutoLayout(tree, topology));
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            Assert.That(presentation.CompletionScopes.Count(scope => scope.Owner.Node?.Node == condition), Is.EqualTo(1));
            Assert.That(presentation.Relations.Any(relation =>
                relation.Kind == GraphPresentationRelationKind.ConditionTrue
                && relation.Target.Item?.Node?.Node == condition), Is.True);
        }

        [Test]
        public void AutoLayout_UnreachableConditionBlockRemainsCollisionFree()
        {
            TestNode head = Node<TestNode>("Head");
            Condition condition = Node<Condition>("Unreachable Condition");
            TestNode predicate = Node<TestNode>("Predicate");
            TestNode trueNode = Node<TestNode>("True");
            TestNode falseNode = Node<TestNode>("False");
            condition.condition = predicate.ToReference();
            condition.trueNode = trueNode.ToReference();
            condition.falseNode = falseNode.ToReference();
            BehaviourTreeData tree = Tree(head, condition, predicate, trueNode, falseNode);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphLayoutResolver.ApplyAutoLayout(tree, topology);

            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            Assert.That(presentation.Find(condition.uuid).ConditionScope.CompletionPosition.y,

                Is.GreaterThan(topology.FindNode(condition.uuid).Position.y));
            Assert.That(GraphLayoutResolver.FindPresentationOverlaps(presentation), Is.Empty);
        }

        [Test]
        public void AutoLayout_ConditionAncestorOwnsHeadBranchAsOneLayoutComponent()
        {
            TestNode head = Node<TestNode>("Head");
            Condition condition = Node<Condition>("Unreachable Condition");
            TestNode predicate = Node<TestNode>("Predicate");
            TestNode falseNode = Node<TestNode>("False");
            condition.condition = predicate.ToReference();
            condition.trueNode = head.ToReference();
            condition.falseNode = falseNode.ToReference();
            BehaviourTreeData tree = Tree(head, condition, predicate, falseNode);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphLayoutResolver.ApplyAutoLayout(tree, topology);

            GraphNodeDescriptor conditionNode = topology.FindNode(condition.uuid);
            GraphNodeDescriptor headNode = topology.FindNode(head.uuid);
            GraphNodeDescriptor falseBranchNode = topology.FindNode(falseNode.uuid);
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);

            Assert.That(conditionNode.Position.y, Is.GreaterThan(0f));
            Assert.That(headNode.Position.y, Is.GreaterThan(conditionNode.Position.y));
            Assert.That(headNode.Position.y, Is.EqualTo(falseBranchNode.Position.y));
            Assert.That(GraphLayoutResolver.FindPresentationOverlaps(presentation), Is.Empty);
        }

        [Test]
        public void AutoLayout_DecisionPlacesBranchesAboveCompletionAndOuterNext()
        {
            Sequence outer = Node<Sequence>("Outer");
            Decision decision = Node<Decision>("Decision");
            TestHost first = Node<TestHost>("First");
            TestNode firstChild = Node<TestNode>("First Child");
            TestNode second = Node<TestNode>("Second");
            TestNode after = Node<TestNode>("After");
            outer.events = new[] { decision.ToReference(), after.ToReference() };
            decision.events = new[] { first.ToReference(), second.ToReference() };
            first.children = new[] { firstChild.ToReference() };
            BehaviourTreeData tree = Tree(outer, decision, first, firstChild, second, after);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphLayoutResolver.ApplyAutoLayout(tree, topology);
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            GraphDecisionScope scope = presentation.Find(decision.uuid).DecisionScope;
            Rect firstBounds = GraphPresentationLayout.GetBounds(presentation.Find(first.uuid));
            Rect secondBounds = GraphPresentationLayout.GetBounds(presentation.Find(second.uuid));

            Assert.That(topology.FindNode(first.uuid).Position.y, Is.GreaterThan(topology.FindNode(decision.uuid).Position.y));
            Assert.That(topology.FindNode(first.uuid).Position.x, Is.LessThan(topology.FindNode(second.uuid).Position.x));
            Assert.That(scope.CompletionPosition.y, Is.GreaterThan(Mathf.Max(firstBounds.yMax, secondBounds.yMax)));
            Assert.That(topology.FindNode(after.uuid).Position.y,
                Is.GreaterThan(scope.CompletionPosition.y + scope.CompletionSize.y));
            Assert.That(GraphLayoutResolver.FindPresentationOverlaps(presentation), Is.Empty);
        }

        [Test]
        public void AutoLayout_SequenceUsesCompactVerticalRhythm()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            sequence.events = new[] { first.ToReference(), second.ToReference() };
            BehaviourTreeData tree = Tree(sequence, first, second);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphLayoutResolver.ApplyAutoLayout(tree, topology);

            GraphNodeDescriptor sequenceNode = topology.FindNode(sequence.uuid);
            GraphNodeDescriptor firstNode = topology.FindNode(first.uuid);
            GraphNodeDescriptor secondNode = topology.FindNode(second.uuid);
            Assert.That(firstNode.Position.y - sequenceNode.Position.y - GraphPresentationMetrics.FlowNodeSize.y,
                Is.EqualTo(GraphPresentationMetrics.LevelGap).Within(0.01f));
            Assert.That(secondNode.Position.y - firstNode.Position.y - GraphPresentationMetrics.NormalNodeSize.y,
                Is.EqualTo(GraphPresentationMetrics.LevelGap).Within(0.01f));

            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            GraphSequenceScope scope = presentation.Find(sequence.uuid).SequenceScope;
            Assert.That(scope.CompletionPosition.y - secondNode.Position.y - GraphPresentationMetrics.NormalNodeSize.y,
                Is.EqualTo(GraphPresentationMetrics.FlowCompletionGap).Within(0.01f));
            Assert.That(GraphLayoutResolver.FindPresentationOverlaps(presentation), Is.Empty);
        }

    }
}
