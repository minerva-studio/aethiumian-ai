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
    /// <summary>
    /// EditMode coverage for graph topology and non-dirty layout resolution.
    /// </summary>
    /// <summary>Graph Editor GraphTopologyEdit contract tests.</summary>
    [Category("GraphEditor")]
    public sealed class GraphTopologyEditTests : GraphEditorTestFixture
    {
        private static void AssertProbabilityAnchors(
            TreeNode node,
            IReadOnlyList<GraphPortDescriptor> ports,
            GraphPresentation presentation,
            GraphEdgeLayerElement painted,
            GraphEdgeLayerElement unmodified)
        {
            GraphPresentationItem owner = presentation.Find(node.uuid);
            GraphPortDescriptor port = ports.Single(candidate => candidate.OwnerUUID == node.uuid
                && candidate.FieldName == "events");
            GraphPresentationRelation authored = presentation.Relations.Single(relation =>
                relation.Role == GraphPresentationRelationRole.AuthoredReference
                && relation.Kind == GraphPresentationRelationKind.ProbabilityBranch
                && relation.Source.Item == owner);
            GraphPresentationRelation[] completion = presentation.Relations.Where(relation =>
                relation.Role == GraphPresentationRelationRole.DerivedCompletion
                && relation.Target == owner.FlowComplete).ToArray();
            GraphPresentationRelation continuation = presentation.Relations.Single(relation =>
                relation.Source == owner.FlowComplete && relation.Target.Item?.Node?.Node is TestNode);

            Assert.That(painted.GetSourceAnchor(authored), Is.EqualTo(painted.GetSourceAnchor(port)));
            Assert.That(completion, Is.Not.Empty);
            Assert.That(completion.All(relation => painted.GetSourceAnchor(relation) == unmodified.GetSourceAnchor(relation)), Is.True);
            Assert.That(completion.All(relation => painted.GetSourceAnchor(relation) != painted.GetSourceAnchor(port)), Is.True);
            Assert.That(continuation.Source, Is.EqualTo(owner.FlowComplete));
            Assert.That(painted.GetSourceAnchor(continuation), Is.EqualTo(unmodified.GetSourceAnchor(continuation)));
        }

        [Test]
        public void TopologyEdit_ConnectAndDisconnectCollectionOccurrenceReconcilesParent()
        {
            TestHost head = Node<TestHost>("Head");
            TestNode child = Node<TestNode>("Child");
            BehaviourTreeData tree = Tree(head, child);
            bool connected = tree.TryInsertReference(head.uuid, nameof(TestHost.children), 0, child.uuid, false, "Connect children");

            Assert.That(connected, Is.True);
            Assert.That(head.children.Select(reference => reference.UUID), Is.EqualTo(new[] { child.uuid }));
            Assert.That(child.parent?.UUID, Is.EqualTo(head.uuid));
            Assert.That(EditorUtility.IsDirty(tree), Is.True);

            bool disconnected = tree.TryDisconnectReference(head.uuid, nameof(TestHost.children), 0, "Disconnect children");

            Assert.That(disconnected, Is.True);
            Assert.That(head.children, Is.Empty);
            Assert.That(child.parent?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
        }

        [Test]
        public void TopologyEdit_SequenceDisconnectsExactOccurrenceAndRebuildsGraph()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            sequence.events = new[] { first.ToReference(), second.ToReference() };
            first.parent = sequence.ToReference();
            second.parent = sequence.ToReference();
            BehaviourTreeData tree = Tree(sequence, first, second);

            Assert.That(tree.TryDisconnectReference(
                sequence.uuid,
                nameof(Sequence.events),
                0,
                "Remove Sequence child",
                second.uuid), Is.False);
            Assert.That(sequence.events.Select(reference => reference.UUID),
                Is.EqualTo(new[] { first.uuid, second.uuid }));

            Assert.That(tree.TryDisconnectReference(
                sequence.uuid,
                nameof(Sequence.events),
                0,
                "Remove Sequence child",
                first.uuid), Is.True);

            GraphTopology rebuilt = GraphTopologyBuilder.Build(tree);
            Assert.That(sequence.events.Select(reference => reference.UUID), Is.EqualTo(new[] { second.uuid }));
            Assert.That(first.parent?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
            Assert.That(rebuilt.Edges.Any(edge => edge.Source.UUID == sequence.uuid
                && edge.Target.UUID == first.uuid), Is.False);
            Assert.That(rebuilt.Edges.Any(edge => edge.Source.UUID == sequence.uuid
                && edge.Target.UUID == second.uuid
                && edge.CollectionIndex == 0), Is.True);
        }

        /// <summary>Verifies that every Condition scalar authored edge disconnects without deleting its target.</summary>
        [TestCase(nameof(Condition.condition))]
        [TestCase(nameof(Condition.trueNode))]
        [TestCase(nameof(Condition.falseNode))]
        public void TopologyEdit_DisconnectsConditionScalarReference(string fieldName)
        {
            Condition owner = Node<Condition>("Condition");
            TestNode target = Node<TestNode>("Target");
            SetScalarReference(owner, fieldName, target);
            target.parent = owner.ToReference();
            BehaviourTreeData tree = Tree(owner, target);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            GraphEdgeDescriptor edge = module.Topology.Edges.Single(candidate => candidate.Source.UUID == owner.uuid
                && candidate.FieldName == fieldName);

            Assert.That(module.Disconnect(edge), Is.True);
            Assert.That(GetScalarReference(owner, fieldName)?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
            Assert.That(tree.GetNode(target.uuid), Is.SameAs(target));
            Assert.That(target.parent?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
        }

        /// <summary>Verifies scalar Loop and Raw references use the authored disconnect contract.</summary>
        [Test]
        public void TopologyEdit_DisconnectsLoopScalarAndRawWithoutOwningRawTarget()
        {
            Loop loop = Node<Loop>("Loop");
            TestNode scalarTarget = Node<TestNode>("Loop condition");
            TestNode rawOwner = Node<TestNode>("Raw owner");
            TestNode rawTarget = Node<TestNode>("Raw target");
            loop.condition = scalarTarget.ToReference();
            rawOwner.raw = rawTarget.ToRawReference();
            scalarTarget.parent = loop.ToReference();
            BehaviourTreeData tree = Tree(loop, scalarTarget, rawOwner, rawTarget);

            Assert.That(tree.TryDisconnectReference(loop.uuid, nameof(Loop.condition), -1, "Disconnect Loop condition",
                scalarTarget.uuid), Is.True);
            Assert.That(loop.condition?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
            Assert.That(scalarTarget.parent?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
            Assert.That(tree.TryDisconnectReference(rawOwner.uuid, nameof(TestNode.raw), -1, "Disconnect Loop raw",
                rawTarget.uuid), Is.True);
            Assert.That(rawOwner.raw?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
            Assert.That(rawTarget.parent?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
            Assert.That(tree.GetNode(rawTarget.uuid), Is.SameAs(rawTarget));
        }

        /// <summary>Verifies scalar graph disconnect rejects a stale edge target and supports Undo/Redo.</summary>
        [Test]
        public void GraphEdges_ScalarDisconnectChecksTargetAndSupportsUndoRedo()
        {
            TestNode owner = Node<TestNode>("Owner");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            owner.child = first.ToReference();
            first.parent = owner.ToReference();
            BehaviourTreeData tree = Tree(owner, first, second);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            GraphEdgeDescriptor current = module.Topology.Edges.Single(edge => edge.FieldName == nameof(TestNode.child));
            GraphEdgeDescriptor stale = new(current.Source, current.Target, second.uuid, current.Kind, current.Label,
                current.IsMissingTarget, current.OccurrenceId, current.FieldName, current.CollectionIndex);

            Assert.That(module.Disconnect(stale), Is.False);
            Assert.That(owner.child.UUID, Is.EqualTo(first.uuid));
            Assert.That(module.Disconnect(current), Is.True);
            Assert.That(owner.child?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
            Assert.That(first.parent?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));

            Undo.PerformUndo();
            tree.SerializedObject.Update();
            tree.RegenerateTable();
            Assert.That(owner.child?.UUID ?? UUID.Empty, Is.EqualTo(first.uuid));
            Assert.That(first.parent?.UUID ?? UUID.Empty, Is.EqualTo(owner.uuid));
            Undo.PerformRedo();
            tree.SerializedObject.Update();
            tree.RegenerateTable();
            Assert.That(owner.child?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
            Assert.That(tree.GetNode(first.uuid), Is.SameAs(first));
        }

        /// <summary>Verifies that a rejected Clipboard destination reports failure without changing the tree.</summary>
        [Test]
        public void ClipboardPaste_RejectsInvalidDestinationWithoutMutation()
        {
            TestNode owner = Node<TestNode>("Owner");
            TestNode source = Node<TestNode>("Source");
            TestNode foreignOwner = Node<TestNode>("Foreign Owner");
            BehaviourTreeData tree = Tree(owner, source);
            Clipboard clipboard = new();
            clipboard.Write(source, tree);
            INodeReferenceSingleSlot slot = owner.ToReferenceSlots()
                .OfType<INodeReferenceSingleSlot>()
                .Single(candidate => candidate.Name == nameof(TestNode.child));
            int nodeCount = tree.EditorNodes.Count;
            UUID childUUID = owner.child?.UUID ?? UUID.Empty;
            EditorUtility.ClearDirty(tree);

            Assert.That(clipboard.PasteTo(tree, foreignOwner, slot), Is.False);
            Assert.That(tree.EditorNodes, Has.Count.EqualTo(nodeCount));
            Assert.That(owner.child?.UUID ?? UUID.Empty, Is.EqualTo(childUUID));
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        [Test]
        public void TopologyEdit_DeleteClearsIncomingReferencesAndKeepsChildren()
        {
            TestHost head = Node<TestHost>("Head");
            TestHost target = Node<TestHost>("Target");
            TestNode child = Node<TestNode>("Child");
            head.children = new[] { target.ToReference(), target.ToReference() };
            target.children = new[] { child.ToReference() };
            BehaviourTreeData tree = Tree(head, target, child);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            Assert.That(module.TryAnalyzeDelete(target.uuid, out GraphNodeDeleteImpact impact), Is.True);
            Assert.That(impact.StructuralIncoming, Is.EqualTo(2));
            Assert.That(impact.DirectStructuralChildCount, Is.EqualTo(1));
            Assert.That(tree.TryDeleteNodes(new HashSet<UUID> { target.uuid }, "Delete target"), Is.True);
            Assert.That(tree.GetNode(target.uuid), Is.Null);
            Assert.That(head.children, Is.Empty);
            Assert.That(tree.GetNode(child.uuid), Is.SameAs(child));
            Assert.That(child.parent?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
        }

        [Test]
        public void Entrance_AssignmentChangesOnlyHeadAndSupportsUndoRedo()
        {
            TestNode firstHead = Node<TestNode>("First Head");
            TestNode replacement = Node<TestNode>("Replacement");
            TestService service = Node<TestService>("Service");
            BehaviourTreeData tree = Tree(firstHead, replacement, service);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            IReadOnlyList<TreeNode> beforeNodes = tree.EditorNodes.ToArray();
            UUID replacementParent = replacement.parent?.UUID ?? UUID.Empty;

            Assert.That(module.CanAssignEntrance(service.uuid), Is.False);
            Assert.That(module.AssignEntrance(replacement.uuid), Is.True);
            Assert.That(tree.headNodeUUID, Is.EqualTo(replacement.uuid));
            Assert.That(tree.EditorNodes, Is.EqualTo(beforeNodes));
            Assert.That(replacement.parent?.UUID ?? UUID.Empty, Is.EqualTo(replacementParent));
            Undo.PerformUndo();
            tree.SerializedObject.Update();
            tree.RegenerateTable();
            Assert.That(tree.headNodeUUID, Is.EqualTo(firstHead.uuid));
            Undo.PerformRedo();
            tree.SerializedObject.Update();
            tree.RegenerateTable();
            Assert.That(tree.headNodeUUID, Is.EqualTo(replacement.uuid));
            Assert.That(module.DisconnectEntrance(), Is.True);
            Assert.That(tree.headNodeUUID, Is.EqualTo(UUID.Empty));
            Undo.PerformUndo();
            tree.SerializedObject.Update();
            tree.RegenerateTable();
            Assert.That(tree.headNodeUUID, Is.EqualTo(replacement.uuid));
            Undo.PerformRedo();
            tree.SerializedObject.Update();
            tree.RegenerateTable();
            Assert.That(tree.headNodeUUID, Is.EqualTo(UUID.Empty));
        }

        [Test]
        public void Entrance_CreateNodeAssignsHeadAndRejectsServices()
        {
            TestNode existing = Node<TestNode>("Existing");
            BehaviourTreeData tree = Tree(existing);
            GraphEditorModule module = CreateHiddenGraphModule(tree);

            Assert.That(module.CreateEntranceNode(typeof(Sequence), new Vector2(17f, 29f)), Is.True);
            TreeNode created = tree.EditorNodes.Single(node => node is Sequence);
            Assert.That(tree.headNodeUUID, Is.EqualTo(created.uuid));
            Assert.That(module.CreateEntranceNode(typeof(Branch), new Vector2(3f, 5f)), Is.False);
        }

        [Test]
        public void ConnectionDrag_SourcePortHitTestingUsesNearestAnchor()
        {
            TestHost host = Node<TestHost>("Host");
            TestNode child = Node<TestNode>("Child");
            host.children = new[] { child.ToReference() };
            BehaviourTreeData tree = Tree(host, child);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            IReadOnlyList<GraphPortDescriptor> ports = GraphPortDescriptorBuilder.Build(topology, presentation, includeRawReferences: false);
            GraphEdgeLayerElement edgeLayer = new(new GraphCanvasAppearance());
            edgeLayer.SetPresentation(presentation, ports);
            GraphPortLayerElement portLayer = new();
            portLayer.SetPorts(topology, presentation, edgeLayer, ports);
            GraphPortDescriptor occupied = ports.Single(port => port.OwnerUUID == host.uuid
                && port.FieldName == nameof(TestHost.children)
                && port.CollectionIndex == 0);
            Vector2 anchor = portLayer.GetSourcePosition(occupied);

            Assert.That(portLayer.FindSourcePort(anchor + new Vector2(3f, 0f), 4f), Is.SameAs(occupied));
            Assert.That(portLayer.FindSourcePort(anchor + new Vector2(5f, 0f), 4f), Is.Null);
        }

        [Test]
        public void DecoratorChildPort_SeparatesChildAttachmentFromSequenceContinuation()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            Inverter decorator = Node<Inverter>("Decorator");
            TestNode decoratedChild = Node<TestNode>("Decorated Child");
            TestNode next = Node<TestNode>("Next");
            sequence.events = new[] { decorator.ToReference(), next.ToReference() };
            decorator.node = decoratedChild.ToReference();
            BehaviourTreeData tree = Tree(sequence, decorator, decoratedChild, next);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            IReadOnlyList<GraphPortDescriptor> ports = GraphPortDescriptorBuilder.Build(topology, presentation, includeRawReferences: false);
            GraphCanvasAppearance appearance = new();
            GraphEdgeLayerElement edgeLayer = new(appearance);
            edgeLayer.SetPresentation(presentation, ports);
            GraphPortLayerElement portLayer = new();
            portLayer.SetPorts(topology, presentation, edgeLayer, ports);
            GraphPortDescriptor childPort = FindPort(ports, decorator.uuid, nameof(Decorator.node), -1);
            GraphPortDescriptor nextPort = FindPort(ports, sequence.uuid, nameof(Sequence.events), 1);
            Vector2 childPosition = portLayer.GetSourcePosition(childPort);
            Vector2 nextPosition = portLayer.GetSourcePosition(nextPort);
            GraphPresentationItem decoratorItem = presentation.Find(decorator.uuid);
            GraphPresentationItem childItem = presentation.Find(decoratedChild.uuid);
            GraphDecoratorStack decoratorStack = presentation.FindDecoratorStack(decorator.uuid);

            Assert.That(childPort.AnchorKind, Is.EqualTo(GraphPortAnchorKind.DecoratorChild));
            Assert.That(nextPort.AnchorKind, Is.EqualTo(GraphPortAnchorKind.ChainedOutput));
            Assert.That(childPosition, Is.EqualTo(decoratorItem.Position + new Vector2(decoratorItem.Size.x * 0.5f, decoratorItem.Size.y)));
            Assert.That(nextPosition, Is.EqualTo(
                childItem.Position + new Vector2(childItem.Size.x * 0.5f, childItem.Size.y)));
            Assert.That(nextPosition, Is.Not.EqualTo(childPosition));
            Assert.That(portLayer.FindSourcePort(childPosition, 1f), Is.SameAs(childPort));
            Assert.That(portLayer.FindSourcePort(nextPosition, 1f), Is.SameAs(nextPort));
            Assert.That(portLayer.GetSourceColor(childPort), Is.EqualTo(appearance.DecoratorPort));
            Assert.That(edgeLayer.SelectPortRelation(childPort), Is.True);
            Assert.That(edgeLayer.SelectedRelation.Origin.FieldName, Is.EqualTo(nameof(Decorator.node)));
            Assert.That(edgeLayer.SelectedRelation.Target.Item.TargetUUID, Is.EqualTo(decoratedChild.uuid));
        }

        [Test]
        public void DecoratorChildPort_CreateNodeConnectsOnlyDecoratorChild()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            Inverter decorator = Node<Inverter>("Decorator");
            sequence.events = new[] { decorator.ToReference() };
            decorator.parent = sequence.ToReference();
            BehaviourTreeData tree = Tree(sequence, decorator);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            GraphPortDescriptor childPort = FindPort(BuildPorts(module.Topology), decorator.uuid, nameof(Decorator.node), -1);

            Assert.That(module.CreateNode(typeof(Sequence), new Vector2(42f, 24f), childPort), Is.True);
            TreeNode created = tree.EditorNodes.Single(node => node != sequence && node != decorator);

            Assert.That(decorator.node.UUID, Is.EqualTo(created.uuid));
            Assert.That(sequence.events.Select(reference => reference.UUID), Is.EqualTo(new[] { decorator.uuid }));
            Assert.That(created.parent.UUID, Is.EqualTo(decorator.uuid));
        }

        /// <summary>Verifies the canvas routes an empty Decorator child port through Wrap.</summary>
        [Test]
        public void DecoratorChildPort_EmptyUsesWrapAndPreservesPositions()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            Inverter decorator = Node<Inverter>("Decorator");
            TestNode target = Node<TestNode>("Target");
            sequence.events = new[] { decorator.ToReference(), target.ToReference() };
            decorator.parent = sequence.ToReference();
            target.parent = sequence.ToReference();
            BehaviourTreeData tree = Tree(sequence, decorator, target);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            GraphPortDescriptor port = FindPort(BuildPorts(module.Topology), decorator.uuid, nameof(Decorator.node), -1);
            Vector2 decoratorPosition = module.Topology.FindNode(decorator.uuid).Position;
            Vector2 targetPosition = module.Topology.FindNode(target.uuid).Position;

            Assert.That(port.Operation, Is.EqualTo(GraphPortOperation.Wrap));
            Assert.That(GraphPortLayerElement.GetVisualShape(port.Operation), Is.EqualTo(GraphPortVisualShape.Ring));
            Assert.That(module.CanAssign(port, target.uuid), Is.True);
            Assert.That(module.Assign(port, target.uuid), Is.True);
            Assert.That(sequence.events.Select(reference => reference.UUID), Is.EqualTo(new[] { decorator.uuid }));
            Assert.That(target.parent.UUID, Is.EqualTo(decorator.uuid));
            Assert.That(module.Topology.FindNode(decorator.uuid).Position, Is.EqualTo(decoratorPosition));
            Assert.That(module.Topology.FindNode(target.uuid).Position, Is.EqualTo(targetPosition));
        }

        /// <summary>Verifies an occupied Decorator child remains a replacement port.</summary>
        [Test]
        public void DecoratorChildPort_OccupiedUsesReplace()
        {
            Inverter decorator = Node<Inverter>("Decorator");
            TestNode target = Node<TestNode>("Target");
            decorator.node = target.ToReference();
            target.parent = decorator.ToReference();
            BehaviourTreeData tree = Tree(decorator, target);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            GraphPortDescriptor port = FindPort(BuildPorts(module.Topology), decorator.uuid, nameof(Decorator.node), -1);
            Assert.That(port.Operation, Is.EqualTo(GraphPortOperation.Replace));
        }

        /// <summary>Verifies wrapping removes the exact Sequence occurrence and supports Undo/Redo.</summary>
        [Test]
        public void DecoratorChildWrap_SequenceMemberSupportsUndoRedo()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            Inverter decorator = Node<Inverter>("Capture");
            TestNode target = Node<TestNode>("Target");
            sequence.events = new[] { decorator.ToReference(), target.ToReference() };
            decorator.parent = sequence.ToReference();
            target.parent = sequence.ToReference();
            BehaviourTreeData tree = Tree(sequence, decorator, target);
            Assert.That(tree.TryWrapDecoratorChild(decorator.uuid, target.uuid, "Wrap child"), Is.True);
            Assert.That(sequence.events.Select(x => x.UUID), Is.EqualTo(new[] { decorator.uuid }));
            Assert.That(decorator.node.UUID, Is.EqualTo(target.uuid));
            Assert.That(target.parent.UUID, Is.EqualTo(decorator.uuid));
            Undo.PerformUndo();
            Assert.That(sequence.events.Select(x => x.UUID), Is.EqualTo(new[] { decorator.uuid, target.uuid }));
            Assert.That(decorator.node?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
            Undo.PerformRedo();
            Assert.That(decorator.node.UUID, Is.EqualTo(target.uuid));
        }

        /// <summary>Verifies an existing Decorator can become the wrapped child of another Decorator.</summary>
        [Test]
        public void DecoratorChildWrap_WrapsAnotherDecorator()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            Inverter outer = Node<Inverter>("Outer");
            Always inner = Node<Always>("Inner");
            sequence.events = new[] { outer.ToReference(), inner.ToReference() };
            outer.parent = sequence.ToReference();
            inner.parent = sequence.ToReference();
            BehaviourTreeData tree = Tree(sequence, outer, inner);
            Assert.That(tree.TryWrapDecoratorChild(outer.uuid, inner.uuid, "Wrap decorator"), Is.True);
            Assert.That(outer.node.UUID, Is.EqualTo(inner.uuid));
            Assert.That(inner.parent.UUID, Is.EqualTo(outer.uuid));
        }

        /// <summary>Verifies Condition branch targets can be wrapped without affecting the other branch.</summary>
        [Test]
        public void DecoratorChildWrap_ConditionBranchPreservesSibling()
        {
            Condition condition = Node<Condition>("Condition");
            Inverter decorator = Node<Inverter>("Capture");
            TestNode trueTarget = Node<TestNode>("True");
            TestNode falseTarget = Node<TestNode>("False");
            condition.trueNode = decorator.ToReference();
            condition.falseNode = falseTarget.ToReference();
            decorator.parent = condition.ToReference();
            falseTarget.parent = condition.ToReference();
            BehaviourTreeData tree = Tree(condition, decorator, trueTarget, falseTarget);
            Assert.That(tree.TryWrapDecoratorChild(decorator.uuid, falseTarget.uuid, "Wrap branch"), Is.True);
            Assert.That(condition.trueNode?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
            Assert.That(condition.falseNode.UUID, Is.EqualTo(decorator.uuid));
            Assert.That(decorator.node.UUID, Is.EqualTo(falseTarget.uuid));
        }

        /// <summary>Verifies invalid ownership, Service, cycle, and cross-tree candidates are rejected without dirtying.</summary>
        [Test]
        public void DecoratorChildWrap_RejectsInvalidCandidatesWithoutDirtying()
        {
            Inverter decorator = Node<Inverter>("Capture");
            TestNode owner = Node<TestNode>("Owner");
            TestNode secondOwner = Node<TestNode>("Second Owner");
            TestNode target = Node<TestNode>("Target");
            TestService service = Node<TestService>("Service");
            owner.child = target.ToReference();
            secondOwner.child = target.ToReference();
            target.parent = owner.ToReference();
            BehaviourTreeData tree = Tree(decorator, owner, secondOwner, target, service);
            EditorUtility.ClearDirty(tree);
            Assert.That(tree.CanWrapDecoratorChild(decorator.uuid, target.uuid), Is.False);
            Assert.That(tree.CanWrapDecoratorChild(decorator.uuid, service.uuid), Is.False);
            Assert.That(tree.CanWrapDecoratorChild(decorator.uuid, decorator.uuid), Is.False);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
            BehaviourTreeData foreignTree = Tree(Node<TestNode>("Foreign"));
            Assert.That(tree.CanWrapDecoratorChild(decorator.uuid, foreignTree.headNodeUUID), Is.False);
        }

        /// <summary>Verifies Wrap keeps the target occurrence stable when the wrapper is before or after it.</summary>
        [Test]
        public void DecoratorWrap_CollectionOccurrenceRemainsTargetedBeforeAndAfterSource()
        {
            foreach (bool sourceBeforeTarget in new[] { true, false })
            {
                Sequence sequence = Node<Sequence>("Sequence");
                Inverter decorator = Node<Inverter>("Wrapper");
                TestNode target = Node<TestNode>("Target");
                TestNode sibling = Node<TestNode>("Sibling");
                sequence.events = sourceBeforeTarget
                    ? new[] { decorator.ToReference(), sibling.ToReference(), target.ToReference() }
                    : new[] { target.ToReference(), sibling.ToReference(), decorator.ToReference() };
                decorator.parent = sequence.ToReference();
                target.parent = sequence.ToReference();
                sibling.parent = sequence.ToReference();
                BehaviourTreeData tree = Tree(sequence, decorator, target, sibling);

                Assert.That(tree.TryWrapDecoratorChild(decorator.uuid, target.uuid, "Wrap occurrence"), Is.True);
                Assert.That(sequence.events.Count(reference => reference.UUID == target.uuid), Is.EqualTo(0));
                Assert.That(sequence.events.Count(reference => reference.UUID == decorator.uuid), Is.EqualTo(1));
                Assert.That(decorator.node.UUID, Is.EqualTo(target.uuid));
                Assert.That(target.parent.UUID, Is.EqualTo(decorator.uuid));
                Assert.That(sequence.events.Single(reference => reference.UUID == sibling.uuid).UUID, Is.EqualTo(sibling.uuid));
            }
        }

        /// <summary>Verifies Wrap handles scalar Condition, another Decorator, and head targets.</summary>
        [Test]
        public void DecoratorWrap_ScalarConditionDecoratorAndHeadTargets()
        {
            Condition condition = Node<Condition>("Condition");
            Inverter wrapper = Node<Inverter>("Wrapper");
            TestNode target = Node<TestNode>("Target");
            condition.trueNode = wrapper.ToReference();
            wrapper.parent = condition.ToReference();
            target.parent = NodeReference.Empty;
            BehaviourTreeData tree = Tree(condition, wrapper, target);
            Assert.That(tree.TryWrapDecoratorChild(wrapper.uuid, target.uuid, "Wrap scalar"), Is.True);
            Assert.That(condition.trueNode?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
            Assert.That(wrapper.node.UUID, Is.EqualTo(target.uuid));

            Inverter outer = Node<Inverter>("Outer");
            Always inner = Node<Always>("Inner");
            TestNode leaf = Node<TestNode>("Leaf");
            outer.node = inner.ToReference();
            inner.parent = outer.ToReference();
            inner.node = leaf.ToReference();
            leaf.parent = inner.ToReference();
            Inverter free = Node<Inverter>("Free Wrapper");
            BehaviourTreeData nested = Tree(outer, inner, leaf, free);
            Assert.That(nested.TryWrapDecoratorChild(free.uuid, outer.uuid, "Wrap head"), Is.True);
            Assert.That(nested.headNodeUUID, Is.EqualTo(free.uuid));
            Assert.That(free.node.UUID, Is.EqualTo(outer.uuid));
        }

        /// <summary>Verifies wrapping an old structural ancestor is evaluated after extracting the source occurrence.</summary>
        [Test]
        public void DecoratorWrap_OldAncestorTargetExtractsSourceWithoutCycle()
        {
            Sequence owner = Node<Sequence>("Owner");
            Inverter decorator = Node<Inverter>("Empty Wrapper");
            owner.events = new[] { decorator.ToReference() };
            decorator.parent = owner.ToReference();
            BehaviourTreeData tree = Tree(owner, decorator);
            EditorUtility.ClearDirty(tree);

            Assert.That(tree.TryWrapDecoratorChild(decorator.uuid, owner.uuid, "Wrap old ancestor"), Is.True);
            Assert.That(tree.headNodeUUID, Is.EqualTo(decorator.uuid));
            Assert.That(decorator.node.UUID, Is.EqualTo(owner.uuid));
            Assert.That(owner.parent.UUID, Is.EqualTo(decorator.uuid));
            Assert.That(owner.events.Count(reference => reference.UUID == decorator.uuid), Is.EqualTo(0));
            Assert.That(decorator.node.UUID, Is.EqualTo(owner.uuid));

            Undo.PerformUndo();
            Assert.That(tree.headNodeUUID, Is.EqualTo(owner.uuid));
            Assert.That(owner.events.Select(reference => reference.UUID), Is.EqualTo(new[] { decorator.uuid }));
            Assert.That(decorator.node?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
            Assert.That(decorator.parent.UUID, Is.EqualTo(owner.uuid));
        }

        /// <summary>Verifies extraction restores a child at head, structural, and free Decorator sources.</summary>
        [Test]
        public void DecoratorExtractToFree_RestoresChildAndLeavesDecoratorEmpty()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            Inverter middle = Node<Inverter>("Middle");
            TestNode child = Node<TestNode>("Child");
            sequence.events = new[] { middle.ToReference() };
            middle.parent = sequence.ToReference();
            middle.node = child.ToReference();
            child.parent = middle.ToReference();
            BehaviourTreeData tree = Tree(sequence, middle, child);
            Assert.That(tree.TryExtractDecoratorToFree(middle.uuid, "Extract"), Is.True);
            Assert.That(middle.node?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
            Assert.That(sequence.events.Select(reference => reference.UUID), Is.EqualTo(new[] { child.uuid }));
            Assert.That(child.parent.UUID, Is.EqualTo(sequence.uuid));
            Undo.PerformUndo();
            Assert.That(middle.node.UUID, Is.EqualTo(child.uuid));
            Undo.PerformRedo();
            Assert.That(middle.node?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
        }

        /// <summary>Verifies extraction also supports a Decorator head and a free Decorator source.</summary>
        [Test]
        public void DecoratorExtractToFree_HeadAndFreeSources()
        {
            Inverter headDecorator = Node<Inverter>("Head Decorator");
            TestNode headChild = Node<TestNode>("Head Child");
            headDecorator.node = headChild.ToReference();
            headChild.parent = headDecorator.ToReference();
            BehaviourTreeData headTree = Tree(headDecorator, headChild);
            Assert.That(headTree.TryExtractDecoratorToFree(headDecorator.uuid, "Extract head"), Is.True);
            Assert.That(headTree.headNodeUUID, Is.EqualTo(headChild.uuid));
            Assert.That(headDecorator.node?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
            Assert.That(headChild.parent?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));

            Inverter freeDecorator = Node<Inverter>("Free Decorator");
            TestNode freeChild = Node<TestNode>("Free Child");
            freeDecorator.node = freeChild.ToReference();
            freeChild.parent = freeDecorator.ToReference();
            BehaviourTreeData freeTree = Tree(freeDecorator, freeChild);
            freeTree.headNodeUUID = UUID.Empty;
            Assert.That(freeTree.TryExtractDecoratorToFree(freeDecorator.uuid, "Extract free"), Is.True);
            Assert.That(freeDecorator.node?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
            Assert.That(freeChild.parent?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
        }

        /// <summary>Verifies an attached empty Decorator can be detached as a free empty node.</summary>
        [Test]
        public void DecoratorDetachEmptyToFree_RemovesAttachedOccurrence()
        {
            Sequence owner = Node<Sequence>("Owner");
            Inverter decorator = Node<Inverter>("Empty Decorator");
            owner.events = new[] { decorator.ToReference() };
            decorator.parent = owner.ToReference();
            BehaviourTreeData tree = Tree(owner, decorator);

            Assert.That(tree.TryDetachEmptyDecoratorToFree(decorator.uuid, "Detach empty Decorator"), Is.True);
            Assert.That(owner.events, Is.Empty);
            Assert.That(decorator.parent?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
            Assert.That(decorator.node?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
            Assert.That(tree.headNodeUUID, Is.EqualTo(owner.uuid));
            Assert.That(tree.IsFreeEmptyDecorator(decorator.uuid), Is.True);

            Undo.PerformUndo();
            Assert.That(owner.events.Select(reference => reference.UUID), Is.EqualTo(new[] { decorator.uuid }));
            Assert.That(decorator.parent.UUID, Is.EqualTo(owner.uuid));
        }

        /// <summary>Verifies Extract-and-Wrap is one transaction and supports head/free targets.</summary>
        [Test]
        public void DecoratorExtractAndWrapTarget_IsSingleUndoAndPreservesChild()
        {
            Inverter decorator = Node<Inverter>("Decorator");
            TestNode child = Node<TestNode>("Child");
            TestNode target = Node<TestNode>("Target");
            decorator.node = child.ToReference();
            child.parent = decorator.ToReference();
            BehaviourTreeData tree = Tree(decorator, child, target);
            Assert.That(tree.TryExtractDecoratorAndWrapTarget(decorator.uuid, target.uuid, "Extract and wrap"), Is.True);
            Assert.That(tree.headNodeUUID, Is.EqualTo(child.uuid));
            Assert.That(decorator.node.UUID, Is.EqualTo(target.uuid));
            Assert.That(target.parent.UUID, Is.EqualTo(decorator.uuid));
            Assert.That(child.parent?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
            Assert.That(decorator.parent?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
            Undo.PerformUndo();
            Assert.That(decorator.node.UUID, Is.EqualTo(child.uuid));
            Assert.That(tree.headNodeUUID, Is.EqualTo(decorator.uuid));
        }

        /// <summary>Verifies CreateNode wrapping does not displace the existing target occurrence.</summary>
        [Test]
        public void DecoratorWrap_CreateAndWrapReferencePreservesExistingTarget()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            TestNode target = Node<TestNode>("Target");
            sequence.events = new[] { target.ToReference() };
            target.parent = sequence.ToReference();
            BehaviourTreeData tree = Tree(sequence, target);
            Inverter decorator = Node<Inverter>("Created Wrapper");
            Assert.That(tree.TryAddAndWrapReference(sequence.uuid, nameof(Sequence.events), 0,
                new[] { decorator }, decorator.uuid, "Create and wrap"), Is.True);
            Assert.That(sequence.events.Select(reference => reference.UUID), Is.EqualTo(new[] { decorator.uuid }));
            Assert.That(decorator.node.UUID, Is.EqualTo(target.uuid));
            Assert.That(target.parent.UUID, Is.EqualTo(decorator.uuid));
        }

        [Test]
        public void DecoratorDelete_UnwrapsMiddleAndSupportsUndoRedo()
        {
            Undo.ClearAll();
            Inverter outer = Node<Inverter>("Outer");
            Always middle = Node<Always>("Middle");
            Constant child = Node<Constant>("Child");
            outer.node = middle.ToReference();
            middle.parent = outer.ToReference();
            middle.node = child.ToReference();
            child.parent = middle.ToReference();
            BehaviourTreeData tree = Tree(outer, middle, child);
            UUID outerUUID = outer.uuid;
            UUID middleUUID = middle.uuid;
            UUID childUUID = child.uuid;

            Assert.That(tree.TryDeleteNodesWithDecoratorUnwrap(new HashSet<UUID> { middleUUID }, "Unwrap middle"), Is.True);
            Assert.That(tree.GetNode(middleUUID), Is.Null);
            Assert.That(outer.node.UUID, Is.EqualTo(childUUID));
            Assert.That(child.parent.UUID, Is.EqualTo(outerUUID));

            Undo.PerformUndo();
            tree.SerializedObject.Update();
            tree.RegenerateTable();
            outer = (Inverter)tree.GetNode(outerUUID);
            middle = (Always)tree.GetNode(middleUUID);
            child = (Constant)tree.GetNode(childUUID);
            Assert.That(outer.node.UUID, Is.EqualTo(middleUUID));
            Assert.That(middle.node.UUID, Is.EqualTo(childUUID));

            Undo.PerformRedo();
            tree.SerializedObject.Update();
            tree.RegenerateTable();
            Assert.That(((Inverter)tree.GetNode(outerUUID)).node.UUID, Is.EqualTo(childUUID));
        }

        [Test]
        public void DecoratorDelete_MultipleHeadWrappersUpliftSurvivingChild()
        {
            Inverter outer = Node<Inverter>("Outer");
            Always inner = Node<Always>("Inner");
            Constant child = Node<Constant>("Child");
            outer.node = inner.ToReference();
            inner.parent = outer.ToReference();
            inner.node = child.ToReference();
            child.parent = inner.ToReference();
            BehaviourTreeData tree = Tree(outer, inner, child);

            Assert.That(tree.TryDeleteNodesWithDecoratorUnwrap(
                new HashSet<UUID> { outer.uuid, inner.uuid }, "Unwrap decorators"), Is.True);
            Assert.That(tree.headNodeUUID, Is.EqualTo(child.uuid));
            Assert.That(child.parent.UUID, Is.EqualTo(UUID.Empty));
        }

        [Test]
        public void DecoratorStack_ReorderRewiresHeadAndWrapperChain()
        {
            Inverter outer = Node<Inverter>("Outer");
            Always inner = Node<Always>("Inner");
            Constant child = Node<Constant>("Child");
            outer.node = inner.ToReference();
            inner.parent = outer.ToReference();
            inner.node = child.ToReference();
            child.parent = inner.ToReference();
            BehaviourTreeData tree = Tree(outer, inner, child);

            Assert.That(tree.TryReorderDecoratorStack(new[] { inner.uuid, outer.uuid }, "Reorder decorators"), Is.True);
            Assert.That(tree.headNodeUUID, Is.EqualTo(inner.uuid));
            Assert.That(inner.node.UUID, Is.EqualTo(outer.uuid));
            Assert.That(outer.node.UUID, Is.EqualTo(child.uuid));
            Assert.That(inner.parent.UUID, Is.EqualTo(UUID.Empty));
            Assert.That(outer.parent.UUID, Is.EqualTo(inner.uuid));
            Assert.That(child.parent.UUID, Is.EqualTo(outer.uuid));
        }

        [Test]
        public void DecoratorContinuationPorts_AggregateAndLoopUseDecoratorCompletion()
        {
            Aggregate aggregate = Node<Aggregate>("Aggregate");
            Inverter aggregateDecorator = Node<Inverter>("Aggregate Decorator");
            TestNode aggregateChild = Node<TestNode>("Aggregate Child");
            TestNode aggregateNext = Node<TestNode>("Aggregate Next");
            aggregate.events = new[] { aggregateDecorator.ToReference(), aggregateNext.ToReference() };
            aggregateDecorator.node = aggregateChild.ToReference();
            AssertContinuationAnchor(
                Tree(aggregate, aggregateDecorator, aggregateChild, aggregateNext),
                aggregate.uuid,
                aggregateDecorator.uuid,
                aggregateChild.uuid,
                nameof(Aggregate.events),
                1);

            Loop loop = Node<Loop>("Loop");
            Inverter loopDecorator = Node<Inverter>("Loop Decorator");
            TestNode loopChild = Node<TestNode>("Loop Child");
            TestNode loopNext = Node<TestNode>("Loop Next");
            loop.events = new[] { loopDecorator.ToReference(), loopNext.ToReference() };
            loopDecorator.node = loopChild.ToReference();
            AssertContinuationAnchor(
                Tree(loop, loopDecorator, loopChild, loopNext),
                loop.uuid,
                loopDecorator.uuid,
                loopChild.uuid,
                nameof(Loop.events),
                1);
        }

        [Test]
        public void DecoratedSequenceContinuationUsesWrappedSequenceCompletion()
        {
            Sequence outer = Node<Sequence>("Outer");
            Always decorator = Node<Always>("Always");
            Sequence inner = Node<Sequence>("Inner");
            TestNode innerChild = Node<TestNode>("Inner Child");
            TestNode next = Node<TestNode>("Next");
            outer.events = new[] { decorator.ToReference(), next.ToReference() };
            decorator.node = inner.ToReference();
            inner.events = new[] { innerChild.ToReference() };

            GraphTopology topology = GraphTopologyBuilder.Build(Tree(outer, decorator, inner, innerChild, next));
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            IReadOnlyList<GraphPortDescriptor> ports = GraphPortDescriptorBuilder.Build(topology, presentation, includeRawReferences: false);
            GraphEdgeLayerElement edgeLayer = new(new GraphCanvasAppearance());
            edgeLayer.SetPresentation(presentation, ports);
            GraphPortDescriptor continuationPort = FindPort(ports, outer.uuid, nameof(Sequence.events), 1);
            GraphPresentationRelation continuation = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.SequenceNext
                && relation.Target.Item?.TargetUUID == next.uuid);
            GraphPresentationItem innerItem = presentation.Find(inner.uuid);
            Vector2 expected = edgeLayer.GetSourceAnchor(innerItem.FlowComplete);

            Assert.That(edgeLayer.GetSourceAnchor(continuationPort), Is.EqualTo(expected));
            Assert.That(edgeLayer.GetSourceAnchor(continuation), Is.EqualTo(expected));
            Assert.That(edgeLayer.GetSourceAnchor(continuationPort), Is.Not.EqualTo(
                innerItem.Position + new Vector2(innerItem.Size.x * 0.5f, innerItem.Size.y)));
        }

        [Test]
        public void ConnectionDrag_TargetHitTestingPrefersEmbeddedCard()
        {
            TestHost owner = Node<TestHost>("Owner");
            TestNode child = Node<TestNode>("Child");
            BehaviourTreeData tree = Tree(owner, child);
            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphPresentationItem ownerItem = presentation.Find(owner.uuid);
            GraphPresentationItem childItem = presentation.Find(child.uuid);
            ownerItem.Position = Vector2.zero;
            ownerItem.Size = new Vector2(300f, 240f);
            childItem.Position = new Vector2(80f, 60f);
            childItem.Size = new Vector2(120f, 60f);
            GraphConnectionTarget ownerTarget = new(ownerItem, compatible: true);
            GraphConnectionTarget childTarget = new(childItem, compatible: false);

            GraphConnectionTarget found = GraphConnectionPreviewElement.FindTarget(
                new[] { ownerTarget, childTarget },
                new Vector2(100f, 80f));

            Assert.That(found, Is.SameAs(childTarget));
            Assert.That(found.Compatible, Is.False);
        }

        [Test]
        public void ConnectionDrag_AssignDispatchesPortOperationAndRebuilds()
        {
            TestNode owner = Node<TestNode>("Owner");
            TestHost host = Node<TestHost>("Host");
            TestNode first = Node<TestNode>("First");
            TestNode replacement = Node<TestNode>("Replacement");
            BehaviourTreeData tree = Tree(owner, host, first, replacement);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            Dictionary<UUID, Vector2> positions = module.Topology.Nodes.ToDictionary(node => node.UUID, node => node.Position);

            GraphPortDescriptor connect = FindPort(BuildPorts(module.Topology), owner.uuid, nameof(TestNode.child), -1);
            Assert.That(module.Assign(connect, first.uuid), Is.True);
            Assert.That(owner.child.UUID, Is.EqualTo(first.uuid));
            AssertGraphPositions(module.Topology, positions);

            GraphPortDescriptor replace = FindPort(BuildPorts(module.Topology), owner.uuid, nameof(TestNode.child), -1);
            Assert.That(module.Assign(replace, replacement.uuid), Is.True);
            Assert.That(owner.child.UUID, Is.EqualTo(replacement.uuid));
            AssertGraphPositions(module.Topology, positions);

            GraphPortDescriptor insert = FindPort(BuildPorts(module.Topology), host.uuid, nameof(TestHost.children), -1);
            Assert.That(module.Assign(insert, first.uuid), Is.True);
            Assert.That(host.children.Select(reference => reference.UUID), Is.EqualTo(new[] { first.uuid }));
            AssertGraphPositions(module.Topology, positions);
            Assert.That(EditorUtility.IsDirty(tree), Is.True);
        }

        [Test]
        public void DecisionAppendPort_AppendsThenIndexCommandReorders()
        {
            Decision decision = Node<Decision>("Decision");
            TestNode first = Node<TestNode>("First");
            TestNode inserted = Node<TestNode>("Inserted");
            TestNode last = Node<TestNode>("Last");
            decision.events = new[] { first.ToReference(), last.ToReference() };
            first.parent = decision.ToReference();
            last.parent = decision.ToReference();
            BehaviourTreeData tree = Tree(decision, first, inserted, last);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            GraphPortDescriptor append = BuildPorts(module.Topology).Single(port =>
                port.OwnerUUID == decision.uuid
                && port.FieldName == nameof(Decision.events)
                && port.AnchorKind == GraphPortAnchorKind.DecisionAppend);

            Assert.That(append.CollectionIndex, Is.EqualTo(-1));
            Assert.That(module.Assign(append, inserted.uuid), Is.True);
            Assert.That(decision.events.Select(reference => reference.UUID),
                Is.EqualTo(new[] { first.uuid, last.uuid, inserted.uuid }));
            Assert.That(inserted.parent.UUID, Is.EqualTo(decision.uuid));
            Assert.That(module.ReorderCollection(decision.uuid, nameof(Decision.events), 2, 1), Is.True);
            Assert.That(decision.events.Select(reference => reference.UUID),
                Is.EqualTo(new[] { first.uuid, inserted.uuid, last.uuid }));

            Undo.PerformUndo();
            tree.SerializedObject.Update();
            Assert.That(decision.events.Select(reference => reference.UUID),
                Is.EqualTo(new[] { first.uuid, last.uuid, inserted.uuid }));
            Undo.PerformUndo();
            tree.SerializedObject.Update();
            Assert.That(decision.events.Select(reference => reference.UUID), Is.EqualTo(new[] { first.uuid, last.uuid }));
        }

        [Test]
        public void DecisionStandardPorts_PrependAndReplaceByCollectionAddress()
        {
            Decision decision = Node<Decision>("Decision");
            TestNode first = Node<TestNode>("First");
            TestNode last = Node<TestNode>("Last");
            TestNode prepended = Node<TestNode>("Prepended");
            TestNode replacement = Node<TestNode>("Replacement");
            decision.events = new[] { first.ToReference(), last.ToReference() };
            first.parent = decision.ToReference();
            last.parent = decision.ToReference();
            BehaviourTreeData tree = Tree(decision, first, last, prepended, replacement);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            GraphPortDescriptor prepend = BuildPorts(module.Topology).Single(port =>
                port.OwnerUUID == decision.uuid
                && port.FieldName == nameof(Decision.events)
                && port.AnchorKind == GraphPortAnchorKind.DecisionPrepend);

            Assert.That(module.Assign(prepend, prepended.uuid), Is.True);
            Assert.That(decision.events.Select(reference => reference.UUID),
                Is.EqualTo(new[] { prepended.uuid, first.uuid, last.uuid }));
            GraphPortDescriptor replace = BuildPorts(module.Topology).Single(port =>
                port.OwnerUUID == decision.uuid
                && port.FieldName == nameof(Decision.events)
                && port.AnchorKind == GraphPortAnchorKind.DecisionOption
                && port.CollectionIndex == 1);
            Assert.That(module.Assign(replace, replacement.uuid), Is.True);
            Assert.That(decision.events.Select(reference => reference.UUID),
                Is.EqualTo(new[] { prepended.uuid, replacement.uuid, last.uuid }));

            Undo.PerformUndo();
            tree.SerializedObject.Update();
            Assert.That(decision.events.Select(reference => reference.UUID),
                Is.EqualTo(new[] { prepended.uuid, first.uuid, last.uuid }));
            Undo.PerformUndo();
            tree.SerializedObject.Update();
            Assert.That(decision.events.Select(reference => reference.UUID),
                Is.EqualTo(new[] { first.uuid, last.uuid }));
        }

        [Test]
        public void DecisionStandardPorts_CreateAtPrependAndReplaceMissingSlot()
        {
            Decision decision = Node<Decision>("Decision");
            UUID missing = UUID.NewUUID();
            decision.events = new[] { new NodeReference(missing) };
            BehaviourTreeData tree = Tree(decision);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            GraphPortDescriptor prepend = BuildPorts(module.Topology).Single(port =>
                port.OwnerUUID == decision.uuid && port.AnchorKind == GraphPortAnchorKind.DecisionPrepend);

            Assert.That(module.CreateNode(typeof(Sequence), new Vector2(20f, 30f), prepend), Is.True);
            TreeNode createdFirst = tree.EditorNodes.Single(node => node != decision);
            Assert.That(decision.events.Select(reference => reference.UUID),
                Is.EqualTo(new[] { createdFirst.uuid, missing }));
            GraphPortDescriptor missingPort = BuildPorts(module.Topology).Single(port =>
                port.OwnerUUID == decision.uuid
                && port.AnchorKind == GraphPortAnchorKind.DecisionOption
                && port.CollectionIndex == 1);
            Assert.That(module.CreateNode(typeof(Sequence), new Vector2(40f, 50f), missingPort), Is.True);
            Assert.That(decision.events[1].UUID, Is.Not.EqualTo(missing));
            Assert.That(tree.GraphLayout.TryGetPosition(decision.events[1].UUID, out Vector2 replacementPosition), Is.True);
            Assert.That(replacementPosition, Is.EqualTo(new Vector2(40f, 50f)));
        }

        [Test]
        public void DecisionIndexCommand_ReordersEmptyAndMissingReferences()
        {
            Decision decision = Node<Decision>("Decision");
            TestNode valid = Node<TestNode>("Valid");
            UUID missing = UUID.NewUUID();
            decision.events = new[]
            {
                new NodeReference(),
                new NodeReference(missing),
                valid.ToReference(),
            };
            BehaviourTreeData tree = Tree(decision, valid);
            GraphEditorModule module = CreateHiddenGraphModule(tree);

            Assert.That(module.ReorderCollection(decision.uuid, nameof(Decision.events), 0, 2), Is.True);
            Assert.That(decision.events.Select(reference => reference?.UUID ?? UUID.Empty),
                Is.EqualTo(new[] { missing, valid.uuid, UUID.Empty }));
        }

        [TestCase(0f, 0)]
        [TestCase(47f, 0)]
        [TestCase(49f, 1)]
        [TestCase(143f, 1)]
        [TestCase(145f, 2)]
        [TestCase(400f, 4)]
        public void DecisionOrderBoundary_UsesNearestMemberGap(float localX, int expectedBoundary)
        {
            Assert.That(GraphDecisionOrderStripElement.GetInsertionBoundary(localX, 4), Is.EqualTo(expectedBoundary));
        }

        [TestCase(1, 0, 0, true)]
        [TestCase(1, 1, 1, false)]
        [TestCase(1, 2, 1, false)]
        [TestCase(1, 3, 2, true)]
        [TestCase(1, 4, 3, true)]
        [TestCase(3, 0, 0, true)]
        [TestCase(3, 2, 2, true)]
        [TestCase(0, 2, 1, true)]
        [TestCase(0, 4, 3, true)]
        public void DecisionOrderBoundary_ConvertsToFinalCollectionIndex(
            int sourceIndex,
            int boundaryIndex,
            int expectedDestination,
            bool expectedMove)
        {
            bool canMove = GraphDecisionOrderStripElement.TryGetDestinationIndex(
                sourceIndex,
                boundaryIndex,
                4,
                out int destinationIndex);

            Assert.That(canMove, Is.EqualTo(expectedMove));
            Assert.That(destinationIndex, Is.EqualTo(expectedDestination));
        }

        [Test]
        public void NodeCreation_CreateAndConnectPersistsPosition()
        {
            TestHost host = Node<TestHost>("Host");
            BehaviourTreeData tree = Tree(host);
            Vector2 hostPosition = new(-140f, 75f);
            tree.GraphLayout = GraphLayoutData.Create(new[] { new GraphLayoutEntry(host.uuid, hostPosition) });
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            GraphPortDescriptor port = FindPort(BuildPorts(module.Topology), host.uuid, nameof(TestHost.children), -1);
            Vector2 requestedPosition = new(187f, 263f);

            Assert.That(module.CreateNode(typeof(Sequence), requestedPosition, port), Is.True);
            TreeNode created = tree.EditorNodes.Single(node => node.uuid != host.uuid);

            Assert.That(host.children.Select(reference => reference.UUID), Is.EqualTo(new[] { created.uuid }));
            Assert.That(created.parent.UUID, Is.EqualTo(host.uuid));
            Assert.That(tree.GraphLayout.TryGetPosition(created.uuid, out Vector2 persistedPosition), Is.True);
            Assert.That(persistedPosition, Is.EqualTo(requestedPosition));
            Assert.That(tree.GraphLayout.TryGetPosition(host.uuid, out Vector2 preservedHostPosition), Is.True);
            Assert.That(preservedHostPosition, Is.EqualTo(hostPosition));
            Assert.That(module.SelectedNode, Is.SameAs(created));
        }

        [Test]
        public void TryAddNodes_PreservesGraphGroupsAcrossUndoRedo()
        {
            TestNode existing = Node<TestNode>("Existing");
            BehaviourTreeData tree = Tree(existing);
            UUID groupUUID = UUID.NewUUID();
            tree.GraphLayout = GraphLayoutData.Create(
                new[] { new GraphLayoutEntry(existing.uuid, new Vector2(10f, 20f)) },
                groupEntries: new[] { new GraphGroupLayoutEntry(groupUUID, "Existing frame", Color.magenta, new[] { existing.uuid }) });
            TestNode added = Node<TestNode>("Added");

            Assert.That(tree.TryAddNodes(new[] { added }, "Add focused group test",
                new Dictionary<UUID, Vector2> { [added.uuid] = new Vector2(30f, 40f) }), Is.True);
            Assert.That(tree.GraphLayout.Groups.Single(group => group.UUID == groupUUID).Title, Is.EqualTo("Existing frame"));
            Assert.That(tree.GraphLayout.Groups.Single(group => group.UUID == groupUUID).Members, Is.EqualTo(new[] { existing.uuid }));

            Undo.PerformUndo();
            Assert.That(tree.GraphLayout.Groups.Single(group => group.UUID == groupUUID).Members, Is.EqualTo(new[] { existing.uuid }));
            Undo.PerformRedo();
            Assert.That(tree.GraphLayout.Groups.Single(group => group.UUID == groupUUID).Members, Is.EqualTo(new[] { existing.uuid }));
        }

        [Test]
        public void NodeLifecycle_CreateSupportsPortKindsAndRollsBackInvalidPort()
        {
            TestNode singleOwner = Node<TestNode>("Single Owner");
            BehaviourTreeData singleTree = Tree(singleOwner);
            GraphEditorModule singleModule = CreateHiddenGraphModule(singleTree);
            GraphPortDescriptor singlePort = FindPort(BuildPorts(singleModule.Topology), singleOwner.uuid, nameof(TestNode.child), -1);
            Assert.That(singleModule.CreateNode(typeof(Sequence), new Vector2(11f, 22f), singlePort), Is.True);
            TreeNode singleChild = singleTree.EditorNodes.Single(node => node is Sequence);
            Assert.That(singleOwner.child.UUID, Is.EqualTo(singleChild.uuid));
            Assert.That(singleChild.parent.UUID, Is.EqualTo(singleOwner.uuid));
            Assert.That(singleTree.GraphLayout.TryGetPosition(singleChild.uuid, out Vector2 singlePosition), Is.True);
            Assert.That(singlePosition, Is.EqualTo(new Vector2(11f, 22f)));

            TestHost listOwner = Node<TestHost>("List Owner");
            BehaviourTreeData listTree = Tree(listOwner);
            GraphEditorModule listModule = CreateHiddenGraphModule(listTree);
            GraphPortDescriptor listPort = FindPort(BuildPorts(listModule.Topology), listOwner.uuid, nameof(TestHost.children), -1);
            Assert.That(listModule.CreateNode(typeof(Sequence), new Vector2(33f, 44f), listPort), Is.True);
            TreeNode listChild = listTree.EditorNodes.Single(node => node is Sequence);
            Assert.That(listOwner.children.Select(reference => reference.UUID), Is.EqualTo(new[] { listChild.uuid }));
            Assert.That(listChild.parent.UUID, Is.EqualTo(listOwner.uuid));

            TestHost serviceOwner = Node<TestHost>("Service Owner");
            BehaviourTreeData serviceTree = Tree(serviceOwner);
            GraphEditorModule serviceModule = CreateHiddenGraphModule(serviceTree);
            GraphPortDescriptor servicePort = FindPort(BuildPorts(serviceModule.Topology), serviceOwner.uuid, nameof(ServiceHostNode.services), -1);
            Assert.That(serviceModule.CreateNode(typeof(Branch), new Vector2(55f, 66f), servicePort), Is.True);
            TreeNode createdService = serviceTree.EditorNodes.Single(node => node is Branch);
            Assert.That(serviceOwner.services.Select(reference => reference.UUID), Is.EqualTo(new[] { createdService.uuid }));
            Assert.That(createdService.parent.UUID, Is.EqualTo(serviceOwner.uuid));

            int nodeCount = serviceTree.EditorNodes.Count;
            EditorUtility.ClearDirty(serviceTree);
            Assert.That(serviceModule.CreateNode(typeof(Sequence), new Vector2(77f, 88f), servicePort), Is.False);
            Assert.That(serviceTree.EditorNodes, Has.Count.EqualTo(nodeCount));
            Assert.That(EditorUtility.IsDirty(serviceTree), Is.False);
        }

        [Test]
        public void NodeLifecycle_RenameAndPasteValuePreserveIdentityAndLayout()
        {
            Sequence head = Node<Sequence>("Head");
            Constant source = Node<Constant>("Source");
            Constant target = Node<Constant>("Target");
            source.returnValue = true;
            target.returnValue = false;
            head.events = new[] { source.ToReference(), target.ToReference() };
            source.parent = head.ToReference();
            target.parent = head.ToReference();
            BehaviourTreeData tree = Tree(head, source, target);
            tree.GraphLayout = GraphLayoutData.Create(new[]
            {
                new GraphLayoutEntry(head.uuid, new Vector2(-10f, -20f)),
                new GraphLayoutEntry(source.uuid, new Vector2(10f, 20f)),
                new GraphLayoutEntry(target.uuid, new Vector2(30f, 40f)),
            });
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            UUID targetUUID = target.uuid;
            UUID parentUUID = target.parent.UUID;
            Assert.That(tree.GraphLayout.TryGetPosition(targetUUID, out Vector2 targetPosition), Is.True);

            Assert.That(module.RenameNode(target, "Renamed Target"), Is.True);
            Assert.That(target.name, Is.EqualTo("Renamed Target"));
            Undo.PerformUndo();
            tree.SerializedObject.Update();
            tree.RegenerateTable();
            target = (Constant)tree.GetNode(targetUUID);
            Assert.That(target.name, Is.EqualTo("Target"));
            Undo.PerformRedo();
            tree.SerializedObject.Update();
            tree.RegenerateTable();
            target = (Constant)tree.GetNode(targetUUID);
            Assert.That(target.name, Is.EqualTo("Renamed Target"));

            module.CopyNode(source, includeSubtree: false);
            Assert.That(module.PasteValue(target), Is.True);
            Assert.That(target.returnValue, Is.True);
            Assert.That(target.uuid, Is.EqualTo(targetUUID));
            Assert.That(target.parent.UUID, Is.EqualTo(parentUUID));
            Assert.That(tree.GraphLayout.TryGetPosition(targetUUID, out Vector2 persistedTargetPosition), Is.True);
            Assert.That(persistedTargetPosition, Is.EqualTo(targetPosition));
            Undo.PerformUndo();
            tree.SerializedObject.Update();
            tree.RegenerateTable();
            target = (Constant)tree.GetNode(targetUUID);
            Assert.That(target.returnValue, Is.False);
            Undo.PerformRedo();
            tree.SerializedObject.Update();
            tree.RegenerateTable();
            target = (Constant)tree.GetNode(targetUUID);
            Assert.That(target.returnValue, Is.True);
        }

        [Test]

        public void NodeLifecycle_PasteUnderBeforeAndAfterUseActualSlots()
        {
            TestNode singleOwner = Node<TestNode>("Single Owner");
            Sequence listOwner = Node<Sequence>("List Owner");
            Constant source = Node<Constant>("Source");
            Constant sibling = Node<Constant>("Sibling");
            listOwner.events = new[] { source.ToReference(), sibling.ToReference() };
            source.parent = listOwner.ToReference();
            sibling.parent = listOwner.ToReference();
            BehaviourTreeData tree = Tree(singleOwner, listOwner, source, sibling);
            tree.GraphLayout = GraphLayoutData.Create(new[]
            {
                new GraphLayoutEntry(singleOwner.uuid, new Vector2(1f, 2f)),
                new GraphLayoutEntry(listOwner.uuid, new Vector2(3f, 4f)),
                new GraphLayoutEntry(source.uuid, new Vector2(5f, 6f)),
                new GraphLayoutEntry(sibling.uuid, new Vector2(7f, 8f)),
            });
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            module.CopyNode(source, includeSubtree: false);

            INodeReferenceSingleSlot singleSlot = singleOwner.ToReferenceSlots().OfType<INodeReferenceSingleSlot>()
                .Single(slot => slot.Name == nameof(TestNode.child));
            Assert.That(module.PasteTo(singleOwner, singleSlot), Is.True);
            TreeNode singlePaste = tree.GetNode(singleOwner.child.UUID);
            Assert.That(singlePaste.parent.UUID, Is.EqualTo(singleOwner.uuid));

            INodeReferenceListSlot listSlot = listOwner.ToReferenceSlots().OfType<INodeReferenceListSlot>().Single();
            int originalCount = listSlot.Count;
            Assert.That(module.PasteAt(listOwner, listSlot, 0), Is.True);
            Assert.That(module.PasteAt(listOwner, listSlot, listSlot.Count), Is.True);
            Assert.That(listSlot.Count, Is.EqualTo(originalCount + 2));
            Assert.That(listOwner.events[0].UUID, Is.Not.EqualTo(source.uuid));
            Assert.That(listOwner.events[listOwner.events.Length - 1].UUID, Is.Not.EqualTo(source.uuid));

            module.CopyNode(source, includeSubtree: false);
            Assert.That(module.TreeModule.TryGetSiblingPasteTarget(sibling, out TreeNode parent, out INodeReferenceListSlot occurrence, out int index), Is.True);
            Assert.That(module.PasteAt(parent, occurrence, index), Is.True);
            Assert.That(module.PasteAt(parent, occurrence, index + 2), Is.True);
            Assert.That(listOwner.events[index].UUID, Is.Not.EqualTo(sibling.uuid));
            Assert.That(listOwner.events[index + 2].UUID, Is.Not.EqualTo(sibling.uuid));
            Assert.That(tree.GraphLayout.TryGetPosition(listOwner.uuid, out Vector2 ownerPosition), Is.True);
            Assert.That(ownerPosition, Is.EqualTo(new Vector2(3f, 4f)));
        }

        [Test]
        public void NodeLifecycle_DeleteAnalyzesAllIncomingReferenceKinds()
        {
            TestHost host = Node<TestHost>("Host");
            TestNode structuralTarget = Node<TestNode>("Structural Target");
            TestNode structuralChild = Node<TestNode>("Structural Child");
            TestService serviceTarget = Node<TestService>("Service Target");
            host.children = new[] { structuralTarget.ToReference(), structuralTarget.ToReference() };
            host.raw = structuralTarget.ToRawReference();
            host.AddService(serviceTarget);
            structuralTarget.child = structuralChild.ToReference();
            structuralChild.parent = structuralTarget.ToReference();
            BehaviourTreeData tree = Tree(host, structuralTarget, structuralChild, serviceTarget);
            GraphEditorModule module = CreateHiddenGraphModule(tree);

            Assert.That(module.TryAnalyzeDelete(structuralTarget.uuid, out GraphNodeDeleteImpact structuralImpact), Is.True);
            Assert.That(structuralImpact.StructuralIncoming, Is.EqualTo(2));
            Assert.That(structuralImpact.RawIncoming, Is.EqualTo(1));
            Assert.That(tree.TryDeleteNodes(new HashSet<UUID> { structuralTarget.uuid }, "Delete structural target"), Is.True);
            Assert.That(host.children, Is.Empty);
            Assert.That(host.raw.UUID, Is.EqualTo(UUID.Empty));
            Assert.That(tree.GetNode(structuralChild.uuid), Is.SameAs(structuralChild));
            Assert.That(structuralChild.parent.UUID, Is.EqualTo(UUID.Empty));

            Assert.That(module.TryAnalyzeDelete(serviceTarget.uuid, out GraphNodeDeleteImpact serviceImpact), Is.True);
            Assert.That(serviceImpact.ServiceIncoming, Is.EqualTo(1));
            Assert.That(tree.TryDeleteNodes(new HashSet<UUID> { serviceTarget.uuid }, "Delete service target"), Is.True);
            Assert.That(host.services, Is.Empty);
        }

        [Test]
        public void NodeLifecycle_CreateAndConnectUndoRedoRestoresNodeReferenceAndLayout()
        {
            Undo.ClearAll();
            Sequence head = Node<Sequence>("Head");
            BehaviourTreeData tree = Tree(head);
            tree.GraphLayout = GraphLayoutData.Create(Array.Empty<GraphLayoutEntry>());
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            head.parent = NodeReference.Empty;
            GraphPortDescriptor port = FindPort(BuildPorts(module.Topology), head.uuid, nameof(Sequence.events), -1);
            Vector2 position = new(17f, 29f);

            Assert.That(module.CreateNode(typeof(Sequence), position, port), Is.True);
            TreeNode created = tree.EditorNodes.Single(node => node.uuid != head.uuid);
            UUID createdUUID = created.uuid;
            Assert.That(head.events.Select(reference => reference.UUID), Is.EqualTo(new[] { createdUUID }));
            Assert.That(tree.GraphLayout.TryGetPosition(createdUUID, out Vector2 createdPosition), Is.True);
            Assert.That(createdPosition, Is.EqualTo(position));

            Undo.PerformUndo();
            tree.SerializedObject.Update();
            tree.RegenerateTable();
            Assert.That(tree.GetNode(createdUUID), Is.Null);
            Assert.That(head.events, Is.Empty);
            Assert.That(tree.GraphLayout.TryGetPosition(createdUUID, out _), Is.False);

            Undo.PerformRedo();
            tree.SerializedObject.Update();
            tree.RegenerateTable();
            TreeNode redone = tree.GetNode(createdUUID);
            Assert.That(redone, Is.Not.Null);
            Assert.That(head.events.Select(reference => reference.UUID), Is.EqualTo(new[] { createdUUID }));
            Assert.That(tree.GraphLayout.TryGetPosition(createdUUID, out Vector2 redonePosition), Is.True);
            Assert.That(redonePosition, Is.EqualTo(createdPosition));
        }

        [TestCase("Under")]
        [TestCase("Before")]
        [TestCase("After")]
        public void NodeLifecycle_StructuralPasteUndoRedoRestoresCollection(string operation)
        {
            Undo.ClearAll();
            Sequence owner = Node<Sequence>("Owner");
            Constant source = Node<Constant>("Source");
            Constant sibling = Node<Constant>("Sibling");
            owner.parent = NodeReference.Empty;
            source.parent = owner.ToReference();
            owner.events = new[] { sibling.ToReference() };
            sibling.parent = owner.ToReference();
            BehaviourTreeData tree = Tree(owner, source, sibling);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            source.parent = owner.ToReference();
            sibling.parent = owner.ToReference();
            module.CopyNode(source, includeSubtree: false);
            INodeReferenceListSlot slot = owner.ToReferenceSlots().OfType<INodeReferenceListSlot>().Single();
            int originalCount = slot.Count;
            int insertionIndex = operation == "After" ? originalCount : operation == "Before" ? 0 : originalCount;

            Assert.That(module.PasteAt(owner, slot, insertionIndex), Is.True);
            Assert.That(slot.Count, Is.EqualTo(originalCount + 1));
            UUID pastedUUID = owner.events[insertionIndex].UUID;
            Assert.That(tree.GetNode(pastedUUID), Is.Not.Null);

            Undo.PerformUndo();
            tree.SerializedObject.Update();
            tree.RegenerateTable();
            slot = owner.ToReferenceSlots().OfType<INodeReferenceListSlot>().Single();
            Assert.That(slot.Count, Is.EqualTo(originalCount));
            Assert.That(tree.GetNode(pastedUUID), Is.Null);

            Undo.PerformRedo();
            tree.SerializedObject.Update();
            tree.RegenerateTable();
            slot = owner.ToReferenceSlots().OfType<INodeReferenceListSlot>().Single();
            Assert.That(slot.Count, Is.EqualTo(originalCount + 1));
            Assert.That(owner.events.Any(reference => reference.UUID == pastedUUID), Is.True);
        }

        [Test]
        public void NodeLifecycle_DeleteCommitUndoRedoRestoresReferencesChildrenAndLayout()
        {
            Undo.ClearAll();
            TestHost head = Node<TestHost>("Head");
            TestHost target = Node<TestHost>("Target");
            TestNode child = Node<TestNode>("Child");
            head.children = new[] { target.ToReference() };
            target.children = new[] { child.ToReference() };
            child.parent = target.ToReference();
            BehaviourTreeData tree = Tree(head, target, child);
            tree.GraphLayout = GraphLayoutData.Create(new[]
            {
                new GraphLayoutEntry(head.uuid, new Vector2(1f, 2f)),
                new GraphLayoutEntry(target.uuid, new Vector2(3f, 4f)),
                new GraphLayoutEntry(child.uuid, new Vector2(5f, 6f)),
            });
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            module.SelectNode(target);
            Assert.That(module.TryAnalyzeDelete(target.uuid, out GraphNodeDeleteImpact impact), Is.True);

            Assert.That(module.CommitDeleteNode(target, impact), Is.True);
            tree.RegenerateTable();
            Assert.That(tree.GetNode(target.uuid), Is.Null);
            Assert.That(head.children, Is.Empty);
            Assert.That(child.parent.UUID, Is.EqualTo(UUID.Empty));

            Undo.PerformUndo();
            tree.RegenerateTable();
            TreeNode restoredTarget = tree.GetNode(target.uuid);
            Assert.That(restoredTarget, Is.Not.Null);
            Assert.That(head.children.Select(reference => reference.UUID), Is.EqualTo(new[] { target.uuid }));
            Assert.That(child.parent.UUID, Is.EqualTo(target.uuid));
            Assert.That(tree.GraphLayout.TryGetPosition(target.uuid, out Vector2 restoredPosition), Is.True);
            Assert.That(restoredPosition, Is.EqualTo(new Vector2(3f, 4f)));

            Undo.PerformRedo();
            tree.RegenerateTable();
            Assert.That(tree.GetNode(target.uuid), Is.Null);
            Assert.That(head.children, Is.Empty);
            Assert.That(child.parent.UUID, Is.EqualTo(UUID.Empty));
        }

        [Test]
        public void NodeLifecycle_CopyAndCopySubtreeDoNotDirtyTree()
        {
            Undo.ClearAll();
            Sequence head = Node<Sequence>("Head");
            TestNode child = Node<TestNode>("Child");
            head.parent = NodeReference.Empty;
            head.events = new[] { child.ToReference() };
            child.parent = head.ToReference();
            BehaviourTreeData tree = Tree(head, child);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            child.parent = head.ToReference();
            EditorUtility.ClearDirty(tree);

            module.CopyNode(child, includeSubtree: false);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
            module.CopyNode(child, includeSubtree: true);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        [Test]
        public void GraphEdges_ProbabilityFamilyKeepsDerivedCompletionAnchors()
        {
            Sequence probabilityOuter = Node<Sequence>("Probability Outer");
            Probability probability = Node<Probability>("Probability");
            TestNode probabilityCandidate = Node<TestNode>("Probability Candidate");
            TestNode probabilityAfter = Node<TestNode>("Probability After");
            probabilityOuter.events = new[] { probability.ToReference(), probabilityAfter.ToReference() };
            probability.events = new[] { new Probability.EventWeight { weight = 1, reference = probabilityCandidate.ToReference() } };

            Sequence pseudoOuter = Node<Sequence>("Pseudo Outer");
            PseudoProbability pseudo = Node<PseudoProbability>("Pseudo Probability");
            TestNode pseudoCandidate = Node<TestNode>("Pseudo Candidate");
            TestNode pseudoAfter = Node<TestNode>("Pseudo After");
            pseudoOuter.events = new[] { pseudo.ToReference(), pseudoAfter.ToReference() };
            pseudo.events = new[] { new PseudoProbability.EventWeight { weight = 1, reference = pseudoCandidate.ToReference() } };

            BehaviourTreeData tree = Tree(
                probabilityOuter, probability, probabilityCandidate, probabilityAfter,
                pseudoOuter, pseudo, pseudoCandidate, pseudoAfter);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            IReadOnlyList<GraphPortDescriptor> ports = GraphPortDescriptorBuilder.Build(topology, presentation, includeRawReferences: false);
            GraphEdgeLayerElement painted = new(new GraphCanvasAppearance());
            painted.SetPresentation(presentation, ports);
            GraphEdgeLayerElement unmodified = new(new GraphCanvasAppearance());
            unmodified.SetPresentation(presentation, Array.Empty<GraphPortDescriptor>());

            AssertProbabilityAnchors(probability, ports, presentation, painted, unmodified);
            AssertProbabilityAnchors(pseudo, ports, presentation, painted, unmodified);
        }

        [Test]
        public void GraphEdges_SelectRenderedAuthoredOccurrenceWithoutDirtyingTree()
        {
            TestHost host = Node<TestHost>("Host");
            TestNode child = Node<TestNode>("Child");
            host.children = new[] { child.ToReference() };
            BehaviourTreeData tree = Tree(host, child);
            EditorUtility.ClearDirty(tree);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            IReadOnlyList<GraphPortDescriptor> ports = GraphPortDescriptorBuilder.Build(topology, presentation, includeRawReferences: false);
            GraphEdgeLayerElement edgeLayer = new(new GraphCanvasAppearance());
            edgeLayer.SetPresentation(presentation, ports);
            GraphPresentationRelation relation = presentation.Relations.Single(candidate => candidate.Origin != null);
            Vector2 from = edgeLayer.GetSourceAnchor(relation);
            Vector2 to = GraphPortLayerElement.GetTargetPosition(presentation.Find(child.uuid));

            Assert.That(edgeLayer.SelectAt((from + to) * 0.5f, 8f), Is.True);
            Assert.That(edgeLayer.SelectedRelation, Is.SameAs(relation));
            edgeLayer.ClearEdgeSelection();
            Assert.That(edgeLayer.SelectedRelation, Is.Null);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        [Test]
        public void GraphEdges_DisconnectUsesExactOccurrenceAndRebuildsOnce()
        {
            TestHost host = Node<TestHost>("Host");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            host.children = new[] { first.ToReference(), second.ToReference() };
            first.parent = host.ToReference();
            second.parent = host.ToReference();
            BehaviourTreeData tree = Tree(host, first, second);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            Dictionary<UUID, Vector2> positions = module.Topology.Nodes.ToDictionary(node => node.UUID, node => node.Position);
            GraphEdgeDescriptor selected = module.Topology.Edges.Single(edge => edge.Source.UUID == host.uuid
                && edge.FieldName == nameof(TestHost.children)
                && edge.CollectionIndex == 0);
            EditorUtility.ClearDirty(tree);

            Assert.That(module.Disconnect(selected), Is.True);
            Assert.That(host.children.Select(reference => reference.UUID), Is.EqualTo(new[] { second.uuid }));
            Assert.That(module.Topology.Edges.Count(edge => edge.Source.UUID == host.uuid
                && edge.FieldName == nameof(TestHost.children)), Is.EqualTo(1));
            AssertGraphPositions(module.Topology, positions);
            Assert.That(EditorUtility.IsDirty(tree), Is.True);
        }

        [Test]
        public void GraphNodeMenu_SetAsHeadRejectsOwnedNodeWithoutMutation()
        {
            TestHost head = Node<TestHost>("Head");
            TestNode child = Node<TestNode>("Child");
            TestService service = Node<TestService>("Service");
            head.children = new[] { child.ToReference() };
            child.parent = head.ToReference();
            BehaviourTreeData tree = Tree(head, child, service);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            Dictionary<UUID, Vector2> positions = module.Topology.Nodes.ToDictionary(node => node.UUID, node => node.Position);
            UUID parentUUID = child.parent.UUID;
            UUID childReferenceUUID = head.children[0].UUID;
            Undo.ClearAll();
            EditorUtility.ClearDirty(tree);

            DropdownMenu menu = new();
            module.Canvas.PopulateNodeCommandMenu(menu, child);
            DropdownMenuAction setHead = FindMenuAction(menu, "Set as Head");
            Assert.That(setHead.status, Is.EqualTo(DropdownMenuAction.Status.Disabled));
            Assert.That(FindMenuAction(module, head, "Set as Head").status,
                Is.EqualTo(DropdownMenuAction.Status.Disabled));
            Assert.That(module.SetHead(child), Is.False);
            Assert.That(tree.headNodeUUID, Is.EqualTo(head.uuid));
            Assert.That(child.parent.UUID, Is.EqualTo(parentUUID));
            Assert.That(head.children[0].UUID, Is.EqualTo(childReferenceUUID));
            AssertGraphPositions(module.Topology, positions);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);

            Assert.That(FindMenuAction(module, child, "Set as Head").status,
                Is.EqualTo(DropdownMenuAction.Status.Disabled));
            Assert.That(FindMenuAction(module, service, "Set as Head").status,
                Is.EqualTo(DropdownMenuAction.Status.Disabled));
            TestNode foreign = Node<TestNode>("Foreign");
            Assert.That(FindMenuAction(module, foreign, "Set as Head").status,
                Is.EqualTo(DropdownMenuAction.Status.Disabled));

            Assert.That(tree.GetStructureValidationErrors(), Is.Empty);
        }

        [Test]
        public void GraphEdgeMenu_ReorderUsesOccurrenceAddressAndPreservesLayout()
        {
            TestHost host = Node<TestHost>("Host");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            TestNode third = Node<TestNode>("Third");
            host.children = new[] { first.ToReference(), second.ToReference(), third.ToReference() };
            BehaviourTreeData tree = Tree(host, first, second, third);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            Dictionary<UUID, Vector2> positions = module.Topology.Nodes.ToDictionary(node => node.UUID, node => node.Position);
            GraphEdgeDescriptor middle = module.Topology.Edges.Single(edge => edge.Source.UUID == host.uuid
                && edge.FieldName == nameof(TestHost.children)
                && edge.CollectionIndex == 1);
            GraphPresentationRelation relation = new(
                default,
                default,
                GraphPresentationRelationKind.Structural,
                GraphPresentationRelationRole.AuthoredReference,
                middle.Label,
                middle,
                middle.TargetUUID,
                middle.IsMissingTarget,

                middle.OccurrenceId);
            DropdownMenu menu = new();
            module.Canvas.PopulateEdgeCommandMenu(menu, relation);

            Assert.That(menu.MenuItems().OfType<DropdownMenuAction>().Select(action => action.name),
                Is.EqualTo(new[] { "Move First", "Move Earlier", "Move Later", "Move Last", "Disconnect" }));
            Assert.That(FindMenuAction(menu, "Move First").status, Is.EqualTo(DropdownMenuAction.Status.Normal));
            Assert.That(FindMenuAction(menu, "Move Earlier").status, Is.EqualTo(DropdownMenuAction.Status.Normal));
            Assert.That(FindMenuAction(menu, "Move Later").status, Is.EqualTo(DropdownMenuAction.Status.Normal));
            Assert.That(FindMenuAction(menu, "Move Last").status, Is.EqualTo(DropdownMenuAction.Status.Normal));
            Assert.That(FindMenuAction(module, host, nameof(TestHost.children), 0, "Move First").status,
                Is.EqualTo(DropdownMenuAction.Status.Disabled));
            Assert.That(FindMenuAction(module, host, nameof(TestHost.children), 0, "Move Earlier").status,
                Is.EqualTo(DropdownMenuAction.Status.Disabled));
            Assert.That(FindMenuAction(module, host, nameof(TestHost.children), 2, "Move Later").status,
                Is.EqualTo(DropdownMenuAction.Status.Disabled));
            Assert.That(FindMenuAction(module, host, nameof(TestHost.children), 2, "Move Last").status,
                Is.EqualTo(DropdownMenuAction.Status.Disabled));

            FindMenuAction(menu, "Move First").Execute();
            Assert.That(host.children.Select(reference => reference.UUID), Is.EqualTo(new[] { second.uuid, first.uuid, third.uuid }));
            AssertGraphPositions(module.Topology, positions);
            Assert.That(EditorUtility.IsDirty(tree), Is.True);

            Undo.PerformUndo();
            tree.SerializedObject.Update();
            Assert.That(host.children.Select(reference => reference.UUID), Is.EqualTo(new[] { first.uuid, second.uuid, third.uuid }));
            Undo.PerformRedo();
            tree.SerializedObject.Update();
            Assert.That(host.children.Select(reference => reference.UUID), Is.EqualTo(new[] { second.uuid, first.uuid, third.uuid }));
        }

        [Test]
        public void GraphEdgeMenu_HidesReorderForNonCollectionAndNonAuthoredRelations()
        {
            TestNode owner = Node<TestNode>("Owner");
            TestNode target = Node<TestNode>("Target");
            owner.child = target.ToReference();
            BehaviourTreeData tree = Tree(owner, target);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            GraphEdgeDescriptor single = module.Topology.Edges.Single(edge => edge.FieldName == nameof(TestNode.child));
            GraphPresentationRelation singleRelation = new(
                default,
                default,
                GraphPresentationRelationKind.Structural,
                GraphPresentationRelationRole.AuthoredReference,
                single.Label,
                single,
                single.TargetUUID,
                single.IsMissingTarget,
                single.OccurrenceId);
            DropdownMenu singleMenu = new();
            module.Canvas.PopulateEdgeCommandMenu(singleMenu, singleRelation);
            Assert.That(singleMenu.MenuItems().OfType<DropdownMenuAction>().Select(action => action.name),
                Is.EqualTo(new[] { "Disconnect" }));

            owner.raw = new RawNodeReference { UUID = target.uuid };
            GraphTopology rawTopology = GraphTopologyBuilder.Build(tree, includeRawReferences: true);
            GraphEdgeDescriptor raw = rawTopology.Edges.Single(edge => edge.Kind == GraphEdgeKind.Raw);
            GraphPresentationRelation rawRelation = new(
                default,
                default,
                GraphPresentationRelationKind.Raw,
                GraphPresentationRelationRole.AuthoredReference,
                raw.Label,
                raw,
                raw.TargetUUID,
                raw.IsMissingTarget,
                raw.OccurrenceId);
            DropdownMenu rawMenu = new();
            module.Canvas.PopulateEdgeCommandMenu(rawMenu, rawRelation);
            Assert.That(rawMenu.MenuItems().OfType<DropdownMenuAction>().Select(action => action.name),
                Does.Not.Contain("Move First")
                .And.Not.Contain("Move Earlier")
                .And.Not.Contain("Move Later")
                .And.Not.Contain("Move Last"));

            GraphPresentationRelation derivedRelation = new(
                default,
                default,
                GraphPresentationRelationKind.FlowComplete,
                GraphPresentationRelationRole.DerivedCompletion,
                "completion",
                single,
                single.TargetUUID,
                false,
                single.OccurrenceId);
            DropdownMenu derivedMenu = new();
            module.Canvas.PopulateEdgeCommandMenu(derivedMenu, derivedRelation);
            Assert.That(derivedMenu.MenuItems().OfType<DropdownMenuAction>().Select(action => action.name),
                Does.Not.Contain("Move First")
                .And.Not.Contain("Move Earlier")
                .And.Not.Contain("Move Later")
                .And.Not.Contain("Move Last"));
        }

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
        public void TopologyEdit_WeightedReplaceAndReorderPreserveEntryMetadata()
        {
            Probability probability = Node<Probability>("Probability");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            TestNode replacement = Node<TestNode>("Replacement");
            probability.events = new[]
            {
                new Probability.EventWeight { reference = first.ToReference(), weight = 7 },
                new Probability.EventWeight { reference = second.ToReference(), weight = 19 },
            };
            first.parent = probability.ToReference();
            second.parent = probability.ToReference();
            BehaviourTreeData tree = Tree(probability, first, second, replacement);
            bool replaced = tree.TryReplaceReference(probability.uuid, nameof(Probability.events), 0, replacement.uuid, "Replace weighted event");
            bool reordered = tree.TryReorderReference(probability.uuid, nameof(Probability.events), 1, 0, "Reorder weighted event");

            Assert.That(replaced, Is.True);
            Assert.That(reordered, Is.True);
            Assert.That(probability.events.Select(entry => entry.reference.UUID), Is.EqualTo(new[] { second.uuid, replacement.uuid }));
            Assert.That(probability.events.Select(entry => entry.weight), Is.EqualTo(new[] { 19, 7 }));
            Assert.That(replacement.parent?.UUID, Is.EqualTo(probability.uuid));
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
        public void TopologyEdit_InsertWeightedEntriesUsesDefaultWeightOne()
        {
            Probability probability = Node<Probability>("Probability");
            PseudoProbability pseudoProbability = Node<PseudoProbability>("Pseudo Probability");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            BehaviourTreeData tree = Tree(probability, pseudoProbability, first, second);
            bool probabilityResult = tree.TryInsertReference(probability.uuid, nameof(Probability.events), 0, first.uuid, false, "Insert event");
            bool pseudoResult = tree.TryInsertReference(pseudoProbability.uuid, nameof(PseudoProbability.events), 0, second.uuid, false, "Insert event");

            Assert.That(probabilityResult, Is.True);
            Assert.That(pseudoResult, Is.True);
            Assert.That(probability.events, Has.Length.EqualTo(1));
            Assert.That(probability.events[0].reference.UUID, Is.EqualTo(first.uuid));
            Assert.That(probability.events[0].weight, Is.EqualTo(1));
            Assert.That(pseudoProbability.events, Has.Length.EqualTo(1));
            Assert.That(pseudoProbability.events[0].reference.UUID, Is.EqualTo(second.uuid));
            Assert.That(pseudoProbability.events[0].weight.IsConstant, Is.True);
            Assert.That((int)pseudoProbability.events[0].weight, Is.EqualTo(1));
        }

        [Test]
        public void TopologyEdit_PseudoProbabilityEditsPreserveVariableWeightMetadata()
        {
            PseudoProbability probability = Node<PseudoProbability>("Pseudo Probability");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            TestNode replacement = Node<TestNode>("Replacement");
            VariableData dynamicWeight = new("Dynamic Weight", VariableType.Int);
            VariableField<int> dynamicField = new();
            dynamicField.SetReference(dynamicWeight);
            probability.events = new[]
            {
                new PseudoProbability.EventWeight { reference = first.ToReference(), weight = dynamicField },
                new PseudoProbability.EventWeight { reference = second.ToReference(), weight = 9 },
            };
            first.parent = probability.ToReference();
            second.parent = probability.ToReference();
            BehaviourTreeData tree = Tree(probability, first, second, replacement);
            tree.variables.Add(dynamicWeight);
            bool replaced = tree.TryReplaceReference(probability.uuid, nameof(PseudoProbability.events), 0, replacement.uuid, "Replace event");
            bool reordered = tree.TryReorderReference(probability.uuid, nameof(PseudoProbability.events), 0, 1, "Reorder event");

            Assert.That(replaced, Is.True);
            Assert.That(reordered, Is.True);
            Assert.That(probability.events.Select(entry => entry.reference.UUID), Is.EqualTo(new[] { second.uuid, replacement.uuid }));
            Assert.That(probability.events[1].weight.IsConstant, Is.False);
            Assert.That(probability.events[1].weight.UUID, Is.EqualTo(dynamicWeight.UUID));
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

        [Test]
        public void NodeCommandMenuRegistrar_UsesStableGroupsAndStructuralTargets()
        {
            TestHost head = Node<TestHost>("Head");
            TestNode child = Node<TestNode>("Child");
            head.children = new[] { child.ToReference() };
            child.parent = head.ToReference();
            BehaviourTreeData tree = Tree(head, child);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            RecordingNodeCommandMenu graphMenu = new();
            RecordingNodeCommandMenu nodesMenu = new();
            RecordingNodeCommandHandler graphHandler = new(true);
            RecordingNodeCommandHandler nodesHandler = new(false);
            NodeCommandMenuRegistrar.Register(graphMenu, module.TreeModule, head, graphHandler);
            NodeCommandMenuRegistrar.Register(nodesMenu, module.TreeModule, head, nodesHandler);

            AssertRecordedMenu(graphMenu, 3, hasRename: true);
            AssertRecordedMenu(nodesMenu, 2, hasRename: false);
            Assert.That(graphMenu.Entries.Where(entry => !entry.IsSeparator && !entry.Enabled)
                .Select(entry => entry.Path), Is.EqualTo(new[] { "Duplicate", "Paste Value", "Paste Under/As Raw",
                    "Paste Under/First/Children", "Paste Under/Last/Children", "Paste Before", "Paste After" }));
            Assert.That(graphMenu.Entries.Where(entry => !entry.IsSeparator && !entry.Enabled)
                .All(entry => entry.Execute == null), Is.True);
            Assert.That(graphMenu.Entries.Single(entry => entry.Path == "Copy").Execute, Is.Not.Null);
            graphMenu.Entries.Single(entry => entry.Path == "Copy").Execute();
            Assert.That(graphHandler.LastCommand, Is.EqualTo("Copy"));
            Assert.That(nodesMenu.Entries.Where(entry => !entry.IsSeparator).Select(entry => entry.Path),
                Is.EqualTo(graphMenu.Entries.Where(entry => !entry.IsSeparator && entry.Path != "Rename")
                    .Select(entry => entry.Path)));
        }

        [Test]
        public void NodeCommandMenuAdapters_KeepDisabledActionsNonExecutable()
        {
            RecordingNodeCommandMenu menu = new();
            Assert.That(() => menu.AddAction("Action", null), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => menu.AddDisabledAction(""), Throws.TypeOf<ArgumentException>());
            menu.AddDisabledAction("Disabled");
            menu.AddSeparator();
            Assert.That(menu.Entries.Single(entry => entry.Path == "Disabled").Execute, Is.Null);
            Assert.That(menu.Entries.Single(entry => entry.Path == "Disabled").Enabled, Is.False);
            Assert.That(menu.Entries.Last().IsSeparator, Is.True);
        }

        [Test]
        public void NodeCommands_DuplicateUndoRedoRestoresGraphAndNodes()
        {
            TestHost graphHead = Node<TestHost>("Graph Head");
            TestNode graphChild = Node<TestNode>("Graph Child");
            graphHead.children = new[] { graphChild.ToReference() };
            graphChild.parent = graphHead.ToReference();
            BehaviourTreeData graphTree = Tree(graphHead, graphChild);
            GraphEditorModule graphModule = CreateHiddenGraphModule(graphTree);

            EditorUtility.ClearDirty(graphTree);
            Assert.That(graphModule.DuplicateNode(graphChild), Is.True);
            TreeNode graphDuplicate = graphTree.EditorNodes.Single(node => node.uuid != graphHead.uuid && node.uuid != graphChild.uuid);
            UUID graphDuplicateUUID = graphDuplicate.uuid;
            string graphDuplicateName = graphDuplicate.name;
            Assert.That(graphDuplicate.parent.UUID, Is.EqualTo(graphHead.uuid));
            Assert.That(graphHead.children.Select(reference => reference.UUID), Is.EqualTo(new[] { graphChild.uuid, graphDuplicateUUID }));
            Assert.That(graphTree.GraphLayout.TryGetPosition(graphDuplicateUUID, out Vector2 graphDuplicatePosition), Is.True);
            Assert.That(EditorUtility.IsDirty(graphTree), Is.True);

            Undo.PerformUndo();
            graphTree.RegenerateTable();
            Assert.That(graphTree.EditorNodes.Any(node => node.uuid == graphDuplicateUUID), Is.False);
            Assert.That(graphHead.children.Select(reference => reference.UUID), Is.EqualTo(new[] { graphChild.uuid }));

            Undo.PerformRedo();
            graphTree.RegenerateTable();
            TreeNode redoneGraphDuplicate = graphTree.EditorNodes.Single(node => node.uuid == graphDuplicateUUID);
            Assert.That(redoneGraphDuplicate.name, Is.EqualTo(graphDuplicateName));
            Assert.That(redoneGraphDuplicate.parent.UUID, Is.EqualTo(graphHead.uuid));
            Assert.That(graphHead.children.Select(reference => reference.UUID), Is.EqualTo(new[] { graphChild.uuid, graphDuplicateUUID }));
            Assert.That(graphTree.GraphLayout.TryGetPosition(graphDuplicateUUID, out Vector2 redoneGraphPosition), Is.True);
            Assert.That(redoneGraphPosition, Is.EqualTo(graphDuplicatePosition));

            TestHost nodesHead = Node<TestHost>("Nodes Head");
            TestNode nodesChild = Node<TestNode>("Nodes Child");
            nodesHead.children = new[] { nodesChild.ToReference() };
            nodesChild.parent = nodesHead.ToReference();
            BehaviourTreeData nodesTree = Tree(nodesHead, nodesChild);
            AIEditorWindow nodesWindow = ScriptableObject.CreateInstance<AIEditorWindow>();
            hiddenWindows.Add(nodesWindow);
            nodesWindow.Load(nodesTree);
            EditorUtility.ClearDirty(nodesTree);

            Assert.That(nodesWindow.TreeModule.DuplicateNodeWithUndo(nodesChild), Is.True);
            TreeNode nodesDuplicate = nodesTree.EditorNodes.Single(node => node.uuid != nodesHead.uuid && node.uuid != nodesChild.uuid);
            UUID nodesDuplicateUUID = nodesDuplicate.uuid;
            string nodesDuplicateName = nodesDuplicate.name;
            Assert.That(nodesHead.children.Select(reference => reference.UUID), Is.EqualTo(new[] { nodesChild.uuid, nodesDuplicateUUID }));
            Assert.That(EditorUtility.IsDirty(nodesTree), Is.True);

            Undo.PerformUndo();
            nodesTree.RegenerateTable();
            Assert.That(nodesTree.EditorNodes.Any(node => node.uuid == nodesDuplicateUUID), Is.False);
            Assert.That(nodesHead.children.Select(reference => reference.UUID), Is.EqualTo(new[] { nodesChild.uuid }));

            Undo.PerformRedo();
            nodesTree.RegenerateTable();
            TreeNode redoneNodesDuplicate = nodesTree.EditorNodes.Single(node => node.uuid == nodesDuplicateUUID);
            Assert.That(redoneNodesDuplicate.name, Is.EqualTo(nodesDuplicateName));
            Assert.That(nodesHead.children.Select(reference => reference.UUID), Is.EqualTo(new[] { nodesChild.uuid, nodesDuplicateUUID }));
        }
        private static DropdownMenuAction FindMenuAction(DropdownMenu menu, string name)
        {
            DropdownMenuAction action = menu.MenuItems().OfType<DropdownMenuAction>().Single(item => item.name == name);
            action.UpdateActionStatus(null);
            return action;
        }

        /// <summary>Assigns a test target to one of Condition's scalar authored slots.</summary>
        private static void SetScalarReference(Condition owner, string fieldName, TreeNode target)
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

        /// <summary>Reads one of Condition's scalar authored slots for invariant assertions.</summary>
        private static NodeReference GetScalarReference(Condition owner, string fieldName)
        {
            return fieldName switch
            {
                nameof(Condition.condition) => owner.condition,
                nameof(Condition.trueNode) => owner.trueNode,
                nameof(Condition.falseNode) => owner.falseNode,
                _ => throw new ArgumentOutOfRangeException(nameof(fieldName), fieldName, null),
            };
        }

        private static DropdownMenuAction FindMenuAction(GraphEditorModule module, TreeNode node, string name)
        {
            DropdownMenu menu = new();
            module.Canvas.PopulateNodeCommandMenu(menu, node);
            return FindMenuAction(menu, name);
        }

        private static DropdownMenuAction FindMenuAction(
            GraphEditorModule module, TreeNode owner, string fieldName, int index, string name)
        {
            GraphEdgeDescriptor edge = module.Topology.Edges.Single(candidate => candidate.Source.UUID == owner.uuid
                && candidate.FieldName == fieldName && candidate.CollectionIndex == index);
            GraphPresentationRelation relation = new(default, default, GraphPresentationRelationKind.Structural,
                GraphPresentationRelationRole.AuthoredReference, edge.Label, edge, edge.TargetUUID,
                edge.IsMissingTarget, edge.OccurrenceId);
            DropdownMenu menu = new();
            module.Canvas.PopulateEdgeCommandMenu(menu, relation);
            return FindMenuAction(menu, name);
        }

        private static void AssertGraphPositions(GraphTopology topology, IReadOnlyDictionary<UUID, Vector2> positions)
        {
            foreach (GraphNodeDescriptor node in topology.Nodes)
                Assert.That(node.Position, Is.EqualTo(positions[node.UUID]), node.UUID.ToString());
        }

        private static void AssertRecordedMenu(RecordingNodeCommandMenu menu, int separatorCount, bool hasRename)
        {
            Assert.That(menu.Entries.Count(entry => entry.IsSeparator), Is.EqualTo(separatorCount));
            Assert.That(menu.Entries.First().IsSeparator, Is.False);
            Assert.That(menu.Entries.Last().IsSeparator, Is.False);
            Assert.That(menu.Entries.Any(item => item.Path == "Rename"), Is.EqualTo(hasRename));
            for (int i = 1; i < menu.Entries.Count; i++)
                Assert.That(menu.Entries[i].IsSeparator && menu.Entries[i - 1].IsSeparator, Is.False);
        }

        private static GraphPortDescriptor FindPort(
            IEnumerable<GraphPortDescriptor> ports, UUID ownerUUID, string fieldName, int index)
        {
            return ports.Single(port => port.OwnerUUID == ownerUUID
                && port.FieldName == fieldName && port.CollectionIndex == index);
        }

        private static IReadOnlyList<GraphPortDescriptor> BuildPorts(GraphTopology topology)
        {
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            return GraphPortDescriptorBuilder.Build(topology, presentation, includeRawReferences: false);
        }

        private static void AssertContinuationAnchor(
            BehaviourTreeData tree,
            UUID ownerUUID,
            UUID decoratorUUID,
            UUID childUUID,
            string fieldName,
            int index)
        {
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            IReadOnlyList<GraphPortDescriptor> ports = GraphPortDescriptorBuilder.Build(topology, presentation, includeRawReferences: false);
            GraphEdgeLayerElement edgeLayer = new(new GraphCanvasAppearance());
            edgeLayer.SetPresentation(presentation, ports);
            GraphPortDescriptor continuation = FindPort(ports, ownerUUID, fieldName, index);
            GraphPresentationItem child = presentation.Find(childUUID);
            Vector2 expected = edgeLayer.GetSourceAnchor(child.Completion);

            Assert.That(edgeLayer.GetSourceAnchor(continuation), Is.EqualTo(expected));
            Assert.That(edgeLayer.GetSourceAnchor(continuation), Is.Not.EqualTo(
                presentation.Find(decoratorUUID).Position + new Vector2(
                    presentation.Find(decoratorUUID).Size.x * 0.5f,
                    presentation.Find(decoratorUUID).Size.y)));
        }

        private sealed class RecordingNodeCommandHandler : INodeCommandHandler
        {
            internal string LastCommand { get; private set; }
            public bool SupportsRename { get; }
            internal RecordingNodeCommandHandler(bool supportsRename) => SupportsRename = supportsRename;
            public void Rename(TreeNode node) => LastCommand = "Rename";
            public void Copy(TreeNode node) => LastCommand = "Copy";
            public void CopySubtree(TreeNode node) => LastCommand = "CopySubtree";
            public void Duplicate(TreeNode node) => LastCommand = "Duplicate";
            public void PasteValue(TreeNode node) => LastCommand = "PasteValue";
            public void PasteTo(TreeNode owner, INodeReferenceSingleSlot slot) => LastCommand = "PasteTo";
            public void PasteAt(TreeNode owner, INodeReferenceListSlot slot, int index) => LastCommand = "PasteAt";
            public void Delete(TreeNode node) => LastCommand = "Delete";
        }
    }
}
