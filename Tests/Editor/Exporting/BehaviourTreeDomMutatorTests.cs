using Aethiumian.AI.Editor.Exporting;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Editor.Tests.Exporting
{
    /// <summary>Focused tests for typed DOM mutations and Graph-compatible deletion.</summary>
    public sealed class BehaviourTreeDomMutatorTests
    {
        [Test]
        public void AddNode_InsertsTypedNodeIntoCollectionAndAssignsParent()
        {
            Sequence head = CreateNode<Sequence>("Head");
            head.events = Array.Empty<NodeReference>();
            BehaviourTreeData tree = CreateTree(head);

            try
            {
                BehaviourTreeDomMutationResult result = BehaviourTreeDomMutator.AddNode(tree, new BehaviourTreeDomAddRequest
                {
                    Type = nameof(Always),
                    Name = "Added",
                    ParentNode = head.uuid,
                    Field = nameof(Sequence.events),
                    Index = -1,
                });

                Assert.That(result.Success, Is.True, result.Error);
                Assert.That(result.Saved, Is.True);
                Assert.That(result.CreatedNodeType, Is.EqualTo(nameof(Always)));
                Assert.That(tree.nodes.Count, Is.EqualTo(2));
                Assert.That(head.events, Has.Length.EqualTo(1));
                Assert.That(head.events[0].UUID, Is.EqualTo(result.CreatedNodeId));
                Assert.That(tree.GetNode(result.CreatedNodeId).parent.UUID, Is.EqualTo(head.uuid));
            }
            finally
            {
                DestroyTree(tree);
            }
        }

        [Test]
        public void AddNode_CreatesHeadWhenTreeHasNoHead()
        {
            BehaviourTreeData tree = ScriptableObject.CreateInstance<BehaviourTreeData>();

            try
            {
                BehaviourTreeDomMutationResult result = BehaviourTreeDomMutator.AddNode(tree, new BehaviourTreeDomAddRequest
                {
                    Type = nameof(Sequence),
                    Name = "New Head",
                });

                Assert.That(result.Success, Is.True, result.Error);
                Assert.That(tree.headNodeUUID, Is.EqualTo(result.CreatedNodeId));
                Assert.That(tree.GetNode(result.CreatedNodeId), Is.TypeOf<Sequence>());
                Assert.That(tree.GetNode(result.CreatedNodeId).parent.UUID, Is.EqualTo(UUID.Empty));
            }
            finally
            {
                DestroyTree(tree);
            }
        }

        [Test]
        public void AddNode_InsertsServiceIntoServiceHost()
        {
            Sequence head = CreateNode<Sequence>("Head");
            head.services = new List<NodeReference>();
            BehaviourTreeData tree = CreateTree(head);

            try
            {
                BehaviourTreeDomMutationResult result = BehaviourTreeDomMutator.AddNode(tree, new BehaviourTreeDomAddRequest
                {
                    Type = nameof(Update),
                    Name = "Update Service",
                    ParentNode = head.uuid,
                    Field = nameof(ServiceHostNode.services),
                    Index = -1,
                });

                Assert.That(result.Success, Is.True, result.Error);
                Assert.That(head.services, Has.Count.EqualTo(1));
                Assert.That(head.services[0].UUID, Is.EqualTo(result.CreatedNodeId));
                Assert.That(tree.GetNode(result.CreatedNodeId), Is.TypeOf<Update>());
                Assert.That(tree.GetNode(result.CreatedNodeId).parent.UUID, Is.EqualTo(head.uuid));
            }
            finally
            {
                DestroyTree(tree);
            }
        }

        [Test]
        public void AddNode_RejectsOccupiedHeadAndLeavesTreeUnchanged()
        {
            Sequence head = CreateNode<Sequence>("Head");
            BehaviourTreeData tree = CreateTree(head);
            EditorUtility.ClearDirty(tree);

            try
            {
                BehaviourTreeDomMutationResult result = BehaviourTreeDomMutator.AddNode(tree, new BehaviourTreeDomAddRequest
                {
                    Type = nameof(Always),
                    Name = "Rejected",
                });

                Assert.That(result.Success, Is.False);
                Assert.That(result.Error, Does.Contain("already has a Head"));
                Assert.That(tree.nodes, Has.Count.EqualTo(1));
                Assert.That(EditorUtility.IsDirty(tree), Is.False);
            }
            finally
            {
                DestroyTree(tree);
            }
        }

        [Test]
        public void RemoveNodes_UsesGraphDecoratorUnwrapSemantics()
        {
            Inverter decorator = CreateNode<Inverter>("Invert");
            Always child = CreateNode<Always>("Child");
            decorator.node = new NodeReference(child.uuid);
            child.parent = new NodeReference(decorator.uuid);
            BehaviourTreeData tree = CreateTree(decorator, child);

            try
            {
                BehaviourTreeDomMutationResult result = BehaviourTreeDomMutator.RemoveNodes(tree, new[] { decorator.uuid });

                Assert.That(result.Success, Is.True, result.Error);
                Assert.That(result.Saved, Is.True);
                Assert.That(result.RemovedNodeIds, Is.EquivalentTo(new[] { decorator.uuid }));
                Assert.That(tree.GetNode(decorator.uuid), Is.Null);
                Assert.That(tree.headNodeUUID, Is.EqualTo(child.uuid));
                Assert.That(tree.GetNode(child.uuid).parent.UUID, Is.EqualTo(UUID.Empty));
            }
            finally
            {
                DestroyTree(tree);
            }
        }

        [Test]
        public void RemoveNodes_RejectsUnknownUuidWithoutMutation()
        {
            Sequence head = CreateNode<Sequence>("Head");
            BehaviourTreeData tree = CreateTree(head);
            EditorUtility.ClearDirty(tree);
            UUID unknown = UUID.NewUUID();

            try
            {
                BehaviourTreeDomMutationResult result = BehaviourTreeDomMutator.RemoveNodes(tree, new[] { unknown });

                Assert.That(result.Success, Is.False);
                Assert.That(result.Error, Does.Contain("was not found"));
                Assert.That(tree.nodes.Select(node => node.uuid), Is.EquivalentTo(new[] { head.uuid }));
                Assert.That(EditorUtility.IsDirty(tree), Is.False);
            }
            finally
            {
                DestroyTree(tree);
            }
        }

        [Test]
        public void AddNode_RecordsOneUndoOperation()
        {
            Sequence head = CreateNode<Sequence>("Head");
            head.events = Array.Empty<NodeReference>();
            BehaviourTreeData tree = CreateTree(head);

            try
            {
                BehaviourTreeDomMutationResult result = BehaviourTreeDomMutator.AddNode(tree, new BehaviourTreeDomAddRequest
                {
                    Type = nameof(Always),
                    ParentNode = head.uuid,
                    Field = nameof(Sequence.events),
                });

                Assert.That(result.Success, Is.True, result.Error);
                Assert.That(tree.nodes, Has.Count.EqualTo(2));

                Undo.PerformUndo();
                tree.RegenerateTable();
                Assert.That(tree.nodes, Has.Count.EqualTo(1));
                Assert.That(tree.GetNode(result.CreatedNodeId), Is.Null);
            }
            finally
            {
                DestroyTree(tree);
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
            if (nodes.Length > 0)
            {
                tree.headNodeUUID = nodes[0].uuid;
                tree.nodes.AddRange(nodes);
            }

            return tree;
        }

        private static void DestroyTree(BehaviourTreeData tree)
        {
            if (tree != null)
            {
                UnityEngine.Object.DestroyImmediate(tree);
            }
        }
    }
}
