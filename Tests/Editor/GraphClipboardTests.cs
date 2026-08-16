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
    /// <summary>Graph Editor GraphClipboard contract tests.</summary>
    [Category("GraphEditor")]
    public sealed class GraphClipboardTests : GraphEditorTestFixture
    {
        [Test]
        public void GraphClipboard_CopiesOnlyGroupsWhoseMembersAreComplete()
        {
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            BehaviourTreeData tree = Tree(first, second);
            GraphLayoutData layout = GraphLayoutData.Create(
                new[] { new GraphLayoutEntry(first.uuid, Vector2.zero), new GraphLayoutEntry(second.uuid, new Vector2(100f, 0f)) },
                groupEntries: new[] { new GraphGroupLayoutEntry(UUID.NewUUID(), "Frame", Color.cyan, new[] { first.uuid, second.uuid }) });
            tree.GraphLayout = layout;
            GraphEditorModule module = CreateHiddenGraphModule(tree);

            module.SetGraphSelection(new[] { first, second });
            Assert.That(module.CopySelectedNodes(), Is.True);
            Assert.That(module.PasteGraphSelection(new Vector2(400f, 300f)), Is.True);
            Assert.That(tree.GraphLayout.Groups.Count, Is.EqualTo(2));

            module.SetGraphSelection(new[] { first });
            Assert.That(module.CopySelectedNodes(), Is.True);
            Assert.That(module.PasteGraphSelection(new Vector2(700f, 300f)), Is.True);
            Assert.That(tree.GraphLayout.Groups.Count, Is.EqualTo(2));
        }

        [Test]
        public void GraphClipboard_PastesDetachedSubgraphWithInternalReferencesAndRelativeLayout()
        {

            TestHost head = Node<TestHost>("Head");
            TestHost selectedOwner = Node<TestHost>("Selected Owner");
            TestNode selectedChild = Node<TestNode>("Selected Child");
            TestNode external = Node<TestNode>("External");
            head.children = new[] { selectedOwner.ToReference() };
            selectedOwner.children = new[] { selectedChild.ToReference(), external.ToReference() };
            selectedOwner.raw = new RawNodeReference { UUID = external.uuid };
            selectedOwner.parent = new NodeReference(head.uuid);
            selectedChild.parent = new NodeReference(selectedOwner.uuid);
            external.parent = new NodeReference(selectedOwner.uuid);
            BehaviourTreeData tree = Tree(head, selectedOwner, selectedChild, external);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            module.Topology.FindNode(selectedOwner.uuid).Position = new Vector2(20f, 30f);
            module.Topology.FindNode(selectedChild.uuid).Position = new Vector2(120f, 80f);
            module.SetGraphSelection(new TreeNode[] { selectedOwner, selectedChild });

            Assert.That(module.CopySelectedNodes(), Is.True);
            Assert.That(module.PasteGraphSelection(new Vector2(400f, 300f)), Is.True);

            Assert.That(module.SelectedNodes.Count, Is.EqualTo(2));
            TestHost pastedOwner = module.SelectedNodes.OfType<TestHost>().Single();
            TestNode pastedChild = module.SelectedNodes.OfType<TestNode>().Single();
            Assert.That(pastedOwner.children.Select(reference => reference.UUID), Is.EqualTo(new[] { pastedChild.uuid }));
            Assert.That(pastedChild.parent.UUID, Is.EqualTo(pastedOwner.uuid));
            Assert.That(pastedOwner.parent.UUID, Is.EqualTo(UUID.Empty));
            Assert.That(pastedOwner.raw.UUID, Is.EqualTo(external.uuid));
            Vector2 pastedDelta = module.Topology.FindNode(pastedChild.uuid).Position - module.Topology.FindNode(pastedOwner.uuid).Position;
            Assert.That(pastedDelta, Is.EqualTo(new Vector2(100f, 50f)));
        }

        [Test]
        public void GraphSelection_DuplicateAndDeleteAreAtomicUndoTransactions()
        {
            TestHost head = Node<TestHost>("Head");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            head.children = new[] { first.ToReference(), second.ToReference() };
            first.parent = new NodeReference(head.uuid);
            second.parent = new NodeReference(head.uuid);
            BehaviourTreeData tree = Tree(head, first, second);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            module.SetGraphSelection(new TreeNode[] { first, second });

            Assert.That(module.DuplicateSelectedNodes(), Is.True);
            Assert.That(tree.EditorNodes.Count, Is.EqualTo(5));
            Undo.PerformUndo();
            Assert.That(tree.EditorNodes.Count, Is.EqualTo(3));
            Undo.PerformRedo();
            Assert.That(tree.EditorNodes.Count, Is.EqualTo(5));

            IReadOnlyList<TreeNode> duplicated = module.SelectedNodes;
            Assert.That(module.CommitDeleteSelectedNodes(duplicated), Is.True);
            Assert.That(tree.EditorNodes.Count, Is.EqualTo(3));
            Undo.PerformUndo();
            Assert.That(tree.EditorNodes.Count, Is.EqualTo(5));
        }
    }
}
