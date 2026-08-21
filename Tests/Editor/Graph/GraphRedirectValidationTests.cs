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
    /// <summary>Graph Editor topology tests for GraphRedirectValidationTests.</summary>
    [Category("GraphEditor")]
    public sealed class GraphRedirectValidationTests : GraphTopologyEditTestBase
    {
        [Test]
        public void TopologyEdit_CanAssignPortsWithoutDirtyingTree()
        {
            TestHost host = Node<TestHost>("Host");
            TestNode child = Node<TestNode>("Child");
            BehaviourTreeData tree = Tree(host, child);
            EditorUtility.ClearDirty(tree);
            bool accepted = tree.CanInsertReference(host.uuid, nameof(TestHost.children), child.uuid, false);
            bool rejected = tree.CanInsertReference(host.uuid, nameof(ServiceHostNode.services), child.uuid, false);

            Assert.That(accepted, Is.True);
            Assert.That(rejected, Is.False);
            Assert.That(host.children, Is.Empty);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }
        [Test]
        public void TopologyEdit_CanAssignPortsRejectsStructuralAndCrossTreeViolationsWithoutDirtyingTree()
        {
            TestHost head = Node<TestHost>("Head");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            TestNode child = Node<TestNode>("Child");
            TestNode foreign = Node<TestNode>("Foreign");
            head.children = new[] { first.ToReference(), second.ToReference() };
            head.raw = RawNodeReference.Empty;
            first.child = child.ToReference();
            child.parent = first.ToReference();
            BehaviourTreeData tree = Tree(head, first, second, child);
            EditorUtility.ClearDirty(tree);
            bool cycle = tree.CanConnectReference(child.uuid, nameof(TestNode.child), -1, head.uuid);
            bool secondParent = tree.CanConnectReference(second.uuid, nameof(TestNode.child), -1, child.uuid);
            bool crossTree = tree.CanConnectReference(second.uuid, nameof(TestNode.child), -1, foreign.uuid);
            bool occupied = tree.CanConnectReference(first.uuid, nameof(TestNode.child), -1, second.uuid);
            bool noOp = tree.CanReplaceReference(first.uuid, nameof(TestNode.child), -1, child.uuid);
            bool raw = tree.CanConnectReference(head.uuid, nameof(TestHost.raw), -1, child.uuid);

            Assert.That(cycle, Is.False);
            Assert.That(secondParent, Is.False);
            Assert.That(crossTree, Is.False);
            Assert.That(occupied, Is.False);
            Assert.That(noOp, Is.False);
            Assert.That(raw, Is.True);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }
        [Test]
        public void TopologyEdit_ForwardChainRedirectSequencePreservesSkippedNodesAndUndo()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            TestNode a = Node<TestNode>("A");
            TestNode b = Node<TestNode>("B");
            TestNode c = Node<TestNode>("C");
            TestNode d = Node<TestNode>("D");
            sequence.events = new[] { a.ToReference(), b.ToReference(), c.ToReference(), d.ToReference() };
            foreach (TestNode member in new[] { a, b, c, d })
            {
                member.parent = sequence.ToReference();
            }

            BehaviourTreeData tree = Tree(sequence, a, b, c, d);
            EditorUtility.ClearDirty(tree);
            bool compatible = tree.CanRedirectReferenceChain(sequence.uuid, nameof(Sequence.events), 1, d.uuid);
            Assert.That(compatible, Is.True);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);

            bool redirected = tree.TryRedirectReferenceChain(sequence.uuid, nameof(Sequence.events), 1, d.uuid, "Redirect events");
            Assert.That(redirected, Is.True);
            Assert.That(sequence.events.Select(reference => reference.UUID), Is.EqualTo(new[] { a.uuid, d.uuid }));
            Assert.That(b.parent.UUID, Is.EqualTo(UUID.Empty));
            Assert.That(c.parent.UUID, Is.EqualTo(UUID.Empty));
            Assert.That(d.parent.UUID, Is.EqualTo(sequence.uuid));
            Assert.That(tree.EditorNodes, Has.Member(b).And.Member(c));
            Assert.That(EditorUtility.IsDirty(tree), Is.True);

            Undo.PerformUndo();
            Assert.That(sequence.events.Select(reference => reference.UUID), Is.EqualTo(new[] { a.uuid, b.uuid, c.uuid, d.uuid }));
            Assert.That(b.parent.UUID, Is.EqualTo(sequence.uuid));
            Assert.That(c.parent.UUID, Is.EqualTo(sequence.uuid));

            Undo.PerformRedo();
            Assert.That(sequence.events.Select(reference => reference.UUID), Is.EqualTo(new[] { a.uuid, d.uuid }));
            Assert.That(b.parent.UUID, Is.EqualTo(UUID.Empty));
            Assert.That(c.parent.UUID, Is.EqualTo(UUID.Empty));
        }
        [Test]
        public void ConnectionDrag_RedirectPreservesExistingGraphPositions()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            TestNode first = Node<TestNode>("First");
            TestNode skipped = Node<TestNode>("Skipped");
            TestNode target = Node<TestNode>("Target");
            sequence.events = new[] { first.ToReference(), skipped.ToReference(), target.ToReference() };
            foreach (TestNode node in new[] { first, skipped, target })
            {
                node.parent = sequence.ToReference();
            }

            BehaviourTreeData tree = Tree(sequence, first, skipped, target);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            Dictionary<UUID, Vector2> positions = module.Topology.Nodes.ToDictionary(node => node.UUID, node => node.Position);
            GraphPortDescriptor replace = FindPort(BuildPorts(module.Topology), sequence.uuid, nameof(Sequence.events), 1);

            Assert.That(module.Assign(replace, target.uuid), Is.True);
            Assert.That(sequence.events.Select(reference => reference.UUID), Is.EqualTo(new[] { first.uuid, target.uuid }));
            AssertGraphPositions(module.Topology, positions);
        }
        [Test]
        public void ConnectionDrag_StructuralPromotionDetachesSkippedBranchAndSupportsUndoRedo()
        {
            TestNode owner = Node<TestNode>("Owner");
            TestHost middle = Node<TestHost>("Middle");
            TestNode target = Node<TestNode>("Target");
            TestNode sideBranch = Node<TestNode>("Side Branch");
            owner.child = middle.ToReference();
            middle.children = new[] { target.ToReference(), sideBranch.ToReference() };
            middle.parent = owner.ToReference();
            target.parent = middle.ToReference();
            sideBranch.parent = middle.ToReference();
            BehaviourTreeData tree = Tree(owner, middle, target, sideBranch);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            Dictionary<UUID, Vector2> positions = module.Topology.Nodes.ToDictionary(node => node.UUID, node => node.Position);
            GraphPortDescriptor replace = FindPort(BuildPorts(module.Topology), owner.uuid, nameof(TestNode.child), -1);
            Undo.ClearAll();

            Assert.That(module.CanAssign(replace, target.uuid), Is.True);
            Assert.That(module.Assign(replace, target.uuid), Is.True);
            Assert.That(owner.child.UUID, Is.EqualTo(target.uuid));
            Assert.That(middle.children.Select(reference => reference.UUID), Is.EqualTo(new[] { sideBranch.uuid }));
            Assert.That(middle.parent.UUID, Is.EqualTo(UUID.Empty));
            Assert.That(target.parent.UUID, Is.EqualTo(owner.uuid));
            Assert.That(sideBranch.parent.UUID, Is.EqualTo(middle.uuid));
            Assert.That(module.Topology.FindNode(middle.uuid).IsReachable, Is.False);
            Assert.That(module.Topology.FindNode(sideBranch.uuid).IsReachable, Is.False);
            AssertGraphPositions(module.Topology, positions);

            Undo.PerformUndo();
            tree.SerializedObject.Update();
            Assert.That(owner.child.UUID, Is.EqualTo(middle.uuid));
            Assert.That(middle.children.Select(reference => reference.UUID), Is.EqualTo(new[] { target.uuid, sideBranch.uuid }));
            Assert.That(target.parent.UUID, Is.EqualTo(middle.uuid));

            Undo.PerformRedo();
            tree.SerializedObject.Update();
            Assert.That(owner.child.UUID, Is.EqualTo(target.uuid));
            Assert.That(middle.children.Select(reference => reference.UUID), Is.EqualTo(new[] { sideBranch.uuid }));
        }
        [Test]
        public void ConnectionDrag_ConditionBranchPromotesStructuralDescendant()
        {
            Condition condition = Node<Condition>("Condition");
            TestNode middle = Node<TestNode>("Middle");
            TestNode target = Node<TestNode>("Target");
            condition.trueNode = middle.ToReference();
            middle.child = target.ToReference();
            middle.parent = condition.ToReference();
            target.parent = middle.ToReference();
            BehaviourTreeData tree = Tree(condition, middle, target);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            GraphPortDescriptor replace = FindPort(BuildPorts(module.Topology), condition.uuid, nameof(Condition.trueNode), -1);

            Assert.That(module.Assign(replace, target.uuid), Is.True);
            Assert.That(condition.trueNode.UUID, Is.EqualTo(target.uuid));
            Assert.That(middle.child.UUID, Is.EqualTo(UUID.Empty));
            Assert.That(middle.parent.UUID, Is.EqualTo(UUID.Empty));
            Assert.That(target.parent.UUID, Is.EqualTo(condition.uuid));
        }
        [Test]
        public void TopologyEdit_ForwardChainRedirectLoopRejectsCurrentAndBackwardTargets()
        {
            Loop loop = Node<Loop>("Loop");
            TestNode a = Node<TestNode>("A");
            TestNode b = Node<TestNode>("B");
            TestNode c = Node<TestNode>("C");
            TestNode d = Node<TestNode>("D");
            loop.events = new[] { a.ToReference(), b.ToReference(), c.ToReference(), d.ToReference() };
            foreach (TestNode member in new[] { a, b, c, d })
            {
                member.parent = loop.ToReference();
            }

            BehaviourTreeData tree = Tree(loop, a, b, c, d);
            bool current = tree.CanRedirectReferenceChain(loop.uuid, nameof(Loop.events), 1, b.uuid);
            bool backward = tree.CanRedirectReferenceChain(loop.uuid, nameof(Loop.events), 3, a.uuid);
            bool forward = tree.TryRedirectReferenceChain(loop.uuid, nameof(Loop.events), 1, d.uuid, "Redirect events");

            Assert.That(current, Is.False);
            Assert.That(backward, Is.False);
            Assert.That(forward, Is.True);
            Assert.That(loop.events.Select(reference => reference.UUID), Is.EqualTo(new[] { a.uuid, d.uuid }));
            Assert.That(b.parent.UUID, Is.EqualTo(UUID.Empty));
            Assert.That(c.parent.UUID, Is.EqualTo(UUID.Empty));
        }
        [Test]
        public void TopologyEdit_ForwardChainRedirectCanReplaceSequenceStart()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            TestNode a = Node<TestNode>("A");
            TestNode b = Node<TestNode>("B");
            TestNode c = Node<TestNode>("C");
            TestNode d = Node<TestNode>("D");
            sequence.events = new[] { a.ToReference(), b.ToReference(), c.ToReference(), d.ToReference() };
            foreach (TestNode member in new[] { a, b, c, d })
            {
                member.parent = sequence.ToReference();
            }

            BehaviourTreeData tree = Tree(sequence, a, b, c, d);
            bool result = tree.TryRedirectReferenceChain(sequence.uuid, nameof(Sequence.events), 0, c.uuid, "Redirect events");

            Assert.That(result, Is.True);
            Assert.That(sequence.events.Select(reference => reference.UUID), Is.EqualTo(new[] { c.uuid, d.uuid }));
            Assert.That(a.parent.UUID, Is.EqualTo(UUID.Empty));
            Assert.That(b.parent.UUID, Is.EqualTo(UUID.Empty));
        }
        [Test]
        public void TopologyEdit_ForwardChainRedirectDoesNotApplyToDistributedOrWeightedCollections()
        {
            Decision decision = Node<Decision>("Decision");
            Probability probability = Node<Probability>("Probability");
            TestNode first = Node<TestNode>("First");
            TestNode later = Node<TestNode>("Later");
            decision.events = new[] { first.ToReference(), later.ToReference() };
            probability.events = new[]
            {
                new Probability.EventWeight { reference = first.ToReference(), weight = 1 },
                new Probability.EventWeight { reference = later.ToReference(), weight = 1 },
            };
            later.parent = decision.ToReference();
            BehaviourTreeData tree = Tree(decision, probability, first, later);
            bool distributed = tree.CanRedirectReferenceChain(decision.uuid, nameof(Decision.events), 0, later.uuid);
            bool weighted = tree.CanRedirectReferenceChain(probability.uuid, nameof(Probability.events), 0, later.uuid);

            Assert.That(distributed, Is.False);
            Assert.That(weighted, Is.False);
            Assert.That(decision.events.Select(reference => reference.UUID), Is.EqualTo(new[] { first.uuid, later.uuid }));
            Assert.That(probability.events.Select(entry => entry.reference.UUID), Is.EqualTo(new[] { first.uuid, later.uuid }));
        }
        [Test]
        public void TopologyEdit_ServiceOwnsParentWhileRawReferenceDoesNot()
        {
            TestHost host = Node<TestHost>("Host");
            TestService service = Node<TestService>("Service");
            TestNode rawTarget = Node<TestNode>("Raw target");
            BehaviourTreeData tree = Tree(host, service, rawTarget);
            bool serviceResult = tree.TryInsertReference(host.uuid, nameof(ServiceHostNode.services), 0, service.uuid, false, "Connect Service");
            bool rawResult = tree.TryConnectReference(host.uuid, nameof(TestHost.raw), -1, rawTarget.uuid, "Connect Raw");

            Assert.That(serviceResult, Is.True);
            Assert.That(rawResult, Is.True);
            Assert.That(host.services.Select(reference => reference.UUID), Is.EqualTo(new[] { service.uuid }));
            Assert.That(service.parent?.UUID, Is.EqualTo(host.uuid));
            Assert.That(host.raw.UUID, Is.EqualTo(rawTarget.uuid));
            Assert.That(rawTarget.parent?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
        }
        /// <summary>Verifies that a Service already hosted by one owner cannot be attached to another owner.</summary>
        [Test]
        public void TopologyEdit_ServiceSecondHostIsRejectedWithoutMutation()
        {
            TestHost firstHost = Node<TestHost>("First Host");
            TestHost secondHost = Node<TestHost>("Second Host");
            TestService service = Node<TestService>("Service");
            firstHost.services = new List<NodeReference> { service.ToReference() };
            service.parent = firstHost.ToReference();
            BehaviourTreeData tree = Tree(firstHost, secondHost, service);
            EditorUtility.ClearDirty(tree);
            int undoGroup = Undo.GetCurrentGroup();
            bool result = tree.TryInsertReference(secondHost.uuid, nameof(ServiceHostNode.services), 0, service.uuid, false, "Connect Service");

            Assert.That(result, Is.False);
            Assert.That(firstHost.services.Select(reference => reference.UUID), Is.EqualTo(new[] { service.uuid }));
            Assert.That(secondHost.services, Is.Null.Or.Empty);
            Assert.That(service.parent.UUID, Is.EqualTo(firstHost.uuid));
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
            Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(undoGroup));
            Assert.That(tree.GetStructureValidationErrors(), Is.Empty);
        }
        /// <summary>Verifies that Service edges participate in ancestor-cycle rejection.</summary>
        [Test]
        public void TopologyEdit_ServiceEdgeParticipatesInCycleDetection()
        {
            TestHost host = Node<TestHost>("Host");
            TestService service = Node<TestService>("Service");
            service.child = NodeReference.Empty;
            host.services = new List<NodeReference> { service.ToReference() };
            service.parent = host.ToReference();
            BehaviourTreeData tree = Tree(host, service);
            EditorUtility.ClearDirty(tree);
            bool result = tree.CanConnectReference(service.uuid, nameof(TestService.child), -1, host.uuid);

            Assert.That(result, Is.False);
            Assert.That(service.child.UUID, Is.EqualTo(UUID.Empty));
            Assert.That(host.services.Select(reference => reference.UUID), Is.EqualTo(new[] { service.uuid }));
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
            Assert.That(tree.GetStructureValidationErrors(), Is.Empty);
        }
        /// <summary>Verifies validation reports Service DAGs while preserving unreachable history.</summary>
        [Test]
        public void StructureValidation_ReportsServiceDagAndOrphanParent()
        {
            TestHost firstHost = Node<TestHost>("First Host");
            TestHost secondHost = Node<TestHost>("Second Host");
            TestService sharedService = Node<TestService>("Shared Service");
            firstHost.services = new List<NodeReference> { sharedService.ToReference() };
            secondHost.services = new List<NodeReference> { sharedService.ToReference() };
            sharedService.parent = firstHost.ToReference();
            BehaviourTreeData dagTree = Tree(firstHost, secondHost, sharedService);

            Assert.That(dagTree.GetStructureValidationErrors(), Has.Some.Contains("owning incoming"));

            TestNode owner = Node<TestNode>("Owner");
            TestNode orphan = Node<TestNode>("Orphan");
            orphan.parent = owner.ToReference();
            BehaviourTreeData orphanTree = Tree(owner, orphan);

            Assert.That(orphanTree.GetStructureValidationErrors(), Is.Empty);
        }
        /// <summary>Verifies Raw sharing and self-reference remain outside authored ownership validation.</summary>
        [Test]
        public void StructureValidation_ExcludesRawSharingAndSelfReference()
        {
            TestHost first = Node<TestHost>("First");
            TestHost second = Node<TestHost>("Second");
            first.raw = new RawNodeReference { UUID = first.uuid };
            second.raw = new RawNodeReference { UUID = first.uuid };
            BehaviourTreeData tree = Tree(first, second);

            Assert.That(tree.GetStructureValidationErrors(), Is.Empty);
        }
        /// <summary>Verifies parent repair only changes single-incoming unambiguous nodes.</summary>
        [Test]
        public void RepairParentMetadata_RepairsOnlyUnambiguousNodes()
        {
            TestNode owner = Node<TestNode>("Owner");
            TestNode child = Node<TestNode>("Child");
            TestNode orphan = Node<TestNode>("Orphan");
            owner.child = child.ToReference();
            orphan.parent = owner.ToReference();
            BehaviourTreeData tree = Tree(owner, child, orphan);

            IReadOnlyList<string> remaining = tree.RepairParentMetadata();

            Assert.That(child.parent.UUID, Is.EqualTo(owner.uuid));
            Assert.That(orphan.parent.UUID, Is.EqualTo(owner.uuid));
            Assert.That(remaining, Is.Empty);
        }
        [Test]
        public void TopologyEdit_RejectsNewStructuralCycle()
        {
            TestHost head = Node<TestHost>("Head");
            TestNode child = Node<TestNode>("Child");
            head.children = new[] { child.ToReference() };
            BehaviourTreeData tree = Tree(head, child);
            bool result = tree.TryConnectReference(child.uuid, nameof(TestNode.child), -1, head.uuid, "Connect child");

            Assert.That(result, Is.False);
            Assert.That(child.child?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
        }
        [Test]
        public void TopologyEdit_RejectsSecondStructuralParent()
        {
            TestHost head = Node<TestHost>("Head");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            TestNode child = Node<TestNode>("Child");
            head.children = new[] { first.ToReference(), second.ToReference() };
            first.child = child.ToReference();
            child.parent = first.ToReference();
            BehaviourTreeData tree = Tree(head, first, second, child);
            bool result = tree.TryConnectReference(second.uuid, nameof(TestNode.child), -1, child.uuid, "Connect child");

            Assert.That(result, Is.False);
            Assert.That(second.child?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
            Assert.That(child.parent.UUID, Is.EqualTo(first.uuid));
        }
        [Test]
        public void TopologyEdit_RejectedOccupiedAndNoOpCommandsDoNotDirtyTree()
        {
            TestNode head = Node<TestNode>("Head");
            TestNode child = Node<TestNode>("Child");
            head.child = child.ToReference();
            BehaviourTreeData tree = Tree(head, child);
            EditorUtility.ClearDirty(tree);
            bool occupied = tree.TryConnectReference(head.uuid, nameof(TestNode.child), -1, child.uuid, "Connect child");
            bool noOp = tree.TryReplaceReference(head.uuid, nameof(TestNode.child), -1, child.uuid, "Replace child");

            Assert.That(occupied, Is.False);
            Assert.That(noOp, Is.False);
            Assert.That(head.child.UUID, Is.EqualTo(child.uuid));
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }
        [Test]
        public void TopologyEdit_ServiceSlotRejectsNonServiceTarget()
        {
            TestHost head = Node<TestHost>("Head");
            TestNode child = Node<TestNode>("Child");
            BehaviourTreeData tree = Tree(head, child);
            EditorUtility.ClearDirty(tree);

            bool result = tree.TryInsertReference(head.uuid, nameof(ServiceHostNode.services), 0, child.uuid, false, "Connect Service");

            Assert.That(result, Is.False);
            Assert.That(head.services, Is.Null.Or.Empty);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }
        /// <summary>Verifies the shared transaction moves a Service occurrence with Undo and Redo.</summary>
        [Test]
        public void TopologyMutation_ServiceMoveUsesExactOccurrenceAndUndoRedo()
        {
            TestHost firstHost = Node<TestHost>("First Host");
            TestHost secondHost = Node<TestHost>("Second Host");
            TestService service = Node<TestService>("Service");
            firstHost.services = new List<NodeReference> { service.ToReference() };
            service.parent = firstHost.ToReference();
            BehaviourTreeData tree = Tree(firstHost, secondHost, service);
            Undo.ClearAll();

            bool moved = tree.TryInsertReference(
                secondHost.uuid,
                nameof(ServiceHostNode.services),
                -1,
                service.uuid,
                allowMoveExisting: true,
                undoName: "Move Service");

            Assert.That(moved, Is.True);
            Assert.That(firstHost.services, Is.Empty);
            Assert.That(secondHost.services.Select(reference => reference.UUID), Is.EqualTo(new[] { service.uuid }));
            Assert.That(service.parent.UUID, Is.EqualTo(secondHost.uuid));
            Assert.That(tree.GetStructureValidationErrors(), Is.Empty);

            Undo.PerformUndo();
            Assert.That(firstHost.services.Select(reference => reference.UUID), Is.EqualTo(new[] { service.uuid }));
            Assert.That(secondHost.services, Is.Empty);
            Assert.That(service.parent.UUID, Is.EqualTo(firstHost.uuid));

            Undo.PerformRedo();
            Assert.That(firstHost.services, Is.Empty);
            Assert.That(secondHost.services.Select(reference => reference.UUID), Is.EqualTo(new[] { service.uuid }));
            Assert.That(service.parent.UUID, Is.EqualTo(secondHost.uuid));
        }
        [Test]
        public void ConnectionDrag_MovesExistingServiceToNewHostAndPreservesPositions()
        {
            TestHost firstHost = Node<TestHost>("First Host");
            TestHost secondHost = Node<TestHost>("Second Host");
            TestService service = Node<TestService>("Service");
            firstHost.services = new List<NodeReference> { service.ToReference() };
            service.parent = firstHost.ToReference();
            BehaviourTreeData tree = Tree(firstHost, secondHost, service);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            Dictionary<UUID, Vector2> positions = module.Topology.Nodes.ToDictionary(node => node.UUID, node => node.Position);
            GraphPortDescriptor destination = FindPort(BuildPorts(module.Topology), secondHost.uuid, nameof(ServiceHostNode.services), -1);
            Undo.ClearAll();

            Assert.That(module.CanAssign(destination, service.uuid), Is.True);
            Assert.That(module.Assign(destination, service.uuid), Is.True);
            Assert.That(firstHost.services, Is.Empty);
            Assert.That(secondHost.services.Select(reference => reference.UUID), Is.EqualTo(new[] { service.uuid }));
            Assert.That(service.parent.UUID, Is.EqualTo(secondHost.uuid));
            AssertGraphPositions(module.Topology, positions);

            Undo.PerformUndo();
            tree.SerializedObject.Update();
            Assert.That(firstHost.services.Select(reference => reference.UUID), Is.EqualTo(new[] { service.uuid }));
            Assert.That(secondHost.services, Is.Empty);
            Assert.That(service.parent.UUID, Is.EqualTo(firstHost.uuid));

            Undo.PerformRedo();
            tree.SerializedObject.Update();
            Assert.That(firstHost.services, Is.Empty);
            Assert.That(secondHost.services.Select(reference => reference.UUID), Is.EqualTo(new[] { service.uuid }));
            Assert.That(service.parent.UUID, Is.EqualTo(secondHost.uuid));
        }
        /// <summary>Verifies a damaged multi-owner Service is rejected without a mutation.</summary>
        [Test]
        public void TopologyMutation_MultipleServiceOwnersAreRejectedWithoutMutation()
        {
            TestHost firstHost = Node<TestHost>("First Host");
            TestHost secondHost = Node<TestHost>("Second Host");
            TestHost destination = Node<TestHost>("Destination");
            TestService service = Node<TestService>("Service");
            firstHost.services = new List<NodeReference> { service.ToReference() };
            secondHost.services = new List<NodeReference> { service.ToReference() };
            service.parent = firstHost.ToReference();
            BehaviourTreeData tree = Tree(firstHost, secondHost, destination, service);
            bool result = tree.CanInsertReference(
                destination.uuid,
                nameof(ServiceHostNode.services),
                service.uuid,
                allowMoveExisting: true);

            Assert.That(result, Is.False);
            Assert.That(firstHost.services.Select(reference => reference.UUID), Is.EqualTo(new[] { service.uuid }));
            Assert.That(secondHost.services.Select(reference => reference.UUID), Is.EqualTo(new[] { service.uuid }));
            Assert.That(tree.GetStructureValidationErrors(), Has.Some.Contains("owning incoming"));
        }
        [Test]
        public void TopologyEdit_DisconnectExistingCycleSucceeds()
        {
            TestHost head = Node<TestHost>("Head");
            TestHost child = Node<TestHost>("Child");
            head.children = new[] { child.ToReference() };
            child.children = new[] { head.ToReference() };
            head.parent = child.ToReference();
            child.parent = head.ToReference();
            BehaviourTreeData tree = Tree(head, child);
            tree.Relink();

            bool result = tree.TryDisconnectReference(child.uuid, nameof(TestHost.children), 0, "Disconnect cycle");

            Assert.That(result, Is.True);
            Assert.That(child.children, Is.Empty);
            Assert.That(head.parent.UUID, Is.EqualTo(UUID.Empty));
            Assert.That(child.parent.UUID, Is.EqualTo(head.uuid));
        }
        [Test]
        public void TopologyEdit_MultipleIncomingOwnersKeepExistingParentFallback()
        {
            TestHost head = Node<TestHost>("Head");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            TestNode shared = Node<TestNode>("Shared");
            TestNode added = Node<TestNode>("Added");
            head.children = new[] { first.ToReference(), second.ToReference() };
            first.child = shared.ToReference();
            second.child = shared.ToReference();
            shared.parent = first.ToReference();
            BehaviourTreeData tree = Tree(head, first, second, shared, added);

            bool result = tree.TryInsertReference(head.uuid, nameof(TestHost.children), -1, added.uuid, false, "Connect child");

            Assert.That(result, Is.True);
            Assert.That(shared.parent.UUID, Is.EqualTo(first.uuid));
            Assert.That(added.parent.UUID, Is.EqualTo(head.uuid));
        }
        [Test]
        public void TopologyEdit_UndoRedoRestoresAuthoredReferenceAndParent()
        {
            TestNode head = Node<TestNode>("Head");
            TestNode child = Node<TestNode>("Child");
            BehaviourTreeData tree = Tree(head, child);
            bool result = tree.TryConnectReference(head.uuid, nameof(TestNode.child), -1, child.uuid, "Connect child");
            Assert.That(result, Is.True);
            Assert.That(head.child.UUID, Is.EqualTo(child.uuid));
            Assert.That(child.parent.UUID, Is.EqualTo(head.uuid));

            Undo.PerformUndo();
            Assert.That(head.child.UUID, Is.EqualTo(UUID.Empty));
            Assert.That(child.parent.UUID, Is.EqualTo(UUID.Empty));

            Undo.PerformRedo();
            Assert.That(head.child.UUID, Is.EqualTo(child.uuid));
            Assert.That(child.parent.UUID, Is.EqualTo(head.uuid));
        }
        [Test]
        public void TopologyEdit_RebuiltTopologyReflectsCommandMutation()
        {
            TestNode head = Node<TestNode>("Head");
            TestNode child = Node<TestNode>("Child");
            BehaviourTreeData tree = Tree(head, child);

            bool result = tree.TryConnectReference(head.uuid, nameof(TestNode.child), -1, child.uuid, "Connect child");
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            Assert.That(result, Is.True);
            GraphEdgeDescriptor edge = topology.Edges.Single(candidate => candidate.Source.Node == head);
            Assert.That(edge.Target.Node, Is.SameAs(child));
            Assert.That(topology.FindNode(child.uuid).IsReachable, Is.True);
        }
    }
}
