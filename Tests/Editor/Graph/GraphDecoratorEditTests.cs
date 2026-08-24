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
    /// <summary>Graph Editor topology tests for GraphDecoratorEditTests.</summary>
    [Category("GraphEditor")]
    public sealed class GraphDecoratorEditTests : GraphTopologyEditTestBase
    {
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
            Assert.That(edgeLayer.SelectedRelation.AuthoredEdge.Reference.Address.FieldName, Is.EqualTo(nameof(Decorator.node)));
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
            Assert.That(tree.TryAddAndWrapReference(Address(sequence.uuid, nameof(Sequence.events), 0),
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
    }
}
