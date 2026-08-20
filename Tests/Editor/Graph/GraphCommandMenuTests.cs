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
    /// <summary>Graph Editor topology tests for GraphCommandMenuTests.</summary>
    [Category("GraphEditor")]
    public sealed class GraphCommandMenuTests : GraphTopologyEditTestBase
    {
private static DropdownMenuAction FindMenuAction(DropdownMenu menu, string name)
        {
            DropdownMenuAction action = menu.MenuItems().OfType<DropdownMenuAction>().Single(item => item.name == name);
            action.UpdateActionStatus(null);
            return action;
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


private static void AssertRecordedMenu(RecordingNodeCommandMenu menu, int separatorCount, bool hasRename)
        {
            Assert.That(menu.Entries.Count(entry => entry.IsSeparator), Is.EqualTo(separatorCount));
            Assert.That(menu.Entries.First().IsSeparator, Is.False);
            Assert.That(menu.Entries.Last().IsSeparator, Is.False);
            Assert.That(menu.Entries.Any(item => item.Path == "Rename"), Is.EqualTo(hasRename));
            for (int i = 1; i < menu.Entries.Count; i++)
                Assert.That(menu.Entries[i].IsSeparator && menu.Entries[i - 1].IsSeparator, Is.False);
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
    }
}
