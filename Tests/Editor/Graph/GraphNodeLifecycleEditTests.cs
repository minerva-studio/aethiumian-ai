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
    /// <summary>Graph Editor topology tests for GraphNodeLifecycleEditTests.</summary>
    [Category("GraphEditor")]
    public sealed class GraphNodeLifecycleEditTests : GraphTopologyEditTestBase
    {
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

            module.NodeCommands.Copy(source, includeSubtree: false);
            Assert.That(module.NodeCommands.PasteValue(target), Is.True);
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
            module.NodeCommands.Copy(source, includeSubtree: false);

            INodeReferenceSingleSlot singleSlot = singleOwner.ToReferenceSlots().OfType<INodeReferenceSingleSlot>()
                .Single(slot => slot.Name == nameof(TestNode.child));
            Assert.That(module.NodeCommands.PasteTo(singleOwner, singleSlot), Is.Not.Null);
            TreeNode singlePaste = tree.GetNode(singleOwner.child.UUID);
            Assert.That(singlePaste.parent.UUID, Is.EqualTo(singleOwner.uuid));

            INodeReferenceListSlot listSlot = listOwner.ToReferenceSlots().OfType<INodeReferenceListSlot>().Single();
            int originalCount = listSlot.Count;
            Assert.That(module.NodeCommands.PasteAt(listOwner, listSlot, 0), Is.Not.Null);
            Assert.That(module.NodeCommands.PasteAt(listOwner, listSlot, listSlot.Count), Is.Not.Null);
            Assert.That(listSlot.Count, Is.EqualTo(originalCount + 2));
            Assert.That(listOwner.events[0].UUID, Is.Not.EqualTo(source.uuid));
            Assert.That(listOwner.events[listOwner.events.Length - 1].UUID, Is.Not.EqualTo(source.uuid));

            module.NodeCommands.Copy(source, includeSubtree: false);
            Assert.That(module.NodeCommands.TryGetSiblingPasteTarget(sibling, out TreeNode parent, out INodeReferenceListSlot occurrence, out int index), Is.True);
            Assert.That(module.NodeCommands.PasteAt(parent, occurrence, index), Is.Not.Null);
            Assert.That(module.NodeCommands.PasteAt(parent, occurrence, index + 2), Is.Not.Null);
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
            module.NodeCommands.Copy(source, includeSubtree: false);
            INodeReferenceListSlot slot = owner.ToReferenceSlots().OfType<INodeReferenceListSlot>().Single();
            int originalCount = slot.Count;
            int insertionIndex = operation == "After" ? originalCount : operation == "Before" ? 0 : originalCount;

            Assert.That(module.NodeCommands.PasteAt(owner, slot, insertionIndex), Is.Not.Null);
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

            module.NodeCommands.Copy(child, includeSubtree: false);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
            module.NodeCommands.Copy(child, includeSubtree: true);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }
    }
}
