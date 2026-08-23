using Aethiumian.AI.Editor.Mutations;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Editor.Mutations.Tests
{
    /// <summary>Focused tests for typed behaviour-tree mutations and Graph-compatible deletion.</summary>
    public sealed class BehaviourTreeMutatorTests
    {
        [Test]
        public void AddNode_InsertsTypedNodeIntoCollectionAndAssignsParent()
        {
            Sequence head = CreateNode<Sequence>("Head");
            head.events = Array.Empty<NodeReference>();
            BehaviourTreeData tree = CreateTree(head);

            try
            {
                BehaviourTreeAddResult result = BehaviourTreeMutator.AddNode(tree, new BehaviourTreeAddRequest
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
                Assert.That(result.Location.Kind, Is.EqualTo(BehaviourTreeNodeLocationKind.Reference));
                Assert.That(result.Location.OwnerNodeId, Is.EqualTo(head.uuid));
                Assert.That(result.Location.Field, Is.EqualTo(nameof(Sequence.events)));
                Assert.That(result.Location.Index, Is.EqualTo(0));
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
                BehaviourTreeAddResult result = BehaviourTreeMutator.AddNode(tree, new BehaviourTreeAddRequest
                {
                    Type = nameof(Sequence),
                    Name = "New Head",
                });

                Assert.That(result.Success, Is.True, result.Error);
                Assert.That(tree.headNodeUUID, Is.EqualTo(result.CreatedNodeId));
                Assert.That(result.Location.Kind, Is.EqualTo(BehaviourTreeNodeLocationKind.Head));
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
                BehaviourTreeAddResult result = BehaviourTreeMutator.AddNode(tree, new BehaviourTreeAddRequest
                {
                    Type = nameof(Update),
                    Name = "Update Service",
                    ParentNode = head.uuid,
                    Field = nameof(ServiceHostNode.services),
                    Index = -1,
                });

                Assert.That(result.Success, Is.True, result.Error);
                Assert.That(head.services, Has.Count.EqualTo(1));
                Assert.That(result.Location.Kind, Is.EqualTo(BehaviourTreeNodeLocationKind.Reference));
                Assert.That(result.Location.Field, Is.EqualTo(nameof(ServiceHostNode.services)));
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
                BehaviourTreeAddResult result = BehaviourTreeMutator.AddNode(tree, new BehaviourTreeAddRequest
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
                BehaviourTreeRemoveResult result = BehaviourTreeMutator.RemoveNodes(tree, new[] { decorator.uuid });

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
        public void ReorderNode_MovesCollectionEntryAndPreservesOwnership()
        {
            Sequence head = CreateNode<Sequence>("Head");
            Always first = CreateNode<Always>("First");
            Always second = CreateNode<Always>("Second");
            head.events = new[] { new NodeReference(first.uuid), new NodeReference(second.uuid) };
            first.parent = new NodeReference(head.uuid);
            second.parent = new NodeReference(head.uuid);
            BehaviourTreeData tree = CreateTree(head, first, second);

            try
            {
                BehaviourTreeRearrangeResult result = BehaviourTreeMutator.ReorderNode(tree, new BehaviourTreeReorderRequest
                {
                    NodeId = first.uuid,
                    Index = 1,
                });

                Assert.That(result.Success, Is.True, result.Error);
                Assert.That(result.NodeId, Is.EqualTo(first.uuid));
                Assert.That(result.Source.Kind, Is.EqualTo(BehaviourTreeNodeLocationKind.Reference));
                Assert.That(result.Source.OwnerNodeId, Is.EqualTo(head.uuid));
                Assert.That(result.Source.Index, Is.EqualTo(0));
                Assert.That(result.Destination.Kind, Is.EqualTo(BehaviourTreeNodeLocationKind.Reference));
                Assert.That(result.Destination.OwnerNodeId, Is.EqualTo(head.uuid));
                Assert.That(result.Destination.Index, Is.EqualTo(1));
                Assert.That(head.events.Select(reference => reference.UUID), Is.EqualTo(new[] { second.uuid, first.uuid }));
                Assert.That(first.parent.UUID, Is.EqualTo(head.uuid));
            }
            finally
            {
                DestroyTree(tree);
            }
        }

        [Test]
        public void MoveNode_ReparentsCollectionEntryAndUpdatesParent()
        {
            Sequence head = CreateNode<Sequence>("Head");
            Sequence source = CreateNode<Sequence>("Source");
            Sequence target = CreateNode<Sequence>("Target");
            Always child = CreateNode<Always>("Child");
            head.events = new[] { new NodeReference(source.uuid), new NodeReference(target.uuid) };
            source.parent = new NodeReference(head.uuid);
            target.parent = new NodeReference(head.uuid);
            source.events = new[] { new NodeReference(child.uuid) };
            child.parent = new NodeReference(source.uuid);
            target.events = Array.Empty<NodeReference>();
            BehaviourTreeData tree = CreateTree(head, source, target, child);

            try
            {
                BehaviourTreeRearrangeResult result = BehaviourTreeMutator.MoveNode(tree, new BehaviourTreeMoveRequest
                {
                    NodeId = child.uuid,
                    TargetParent = target.uuid,
                    Field = nameof(Sequence.events),
                    Index = -1,
                });

                Assert.That(result.Success, Is.True, result.Error);
                Assert.That(result.Source.Kind, Is.EqualTo(BehaviourTreeNodeLocationKind.Reference));
                Assert.That(result.Source.OwnerNodeId, Is.EqualTo(source.uuid));
                Assert.That(result.Source.Field, Is.EqualTo(nameof(Sequence.events)));
                Assert.That(result.Destination.Kind, Is.EqualTo(BehaviourTreeNodeLocationKind.Reference));
                Assert.That(result.Destination.OwnerNodeId, Is.EqualTo(target.uuid));
                Assert.That(result.Destination.Index, Is.EqualTo(0));
                Assert.That(source.events, Is.Empty);
                Assert.That(target.events.Select(reference => reference.UUID), Is.EqualTo(new[] { child.uuid }));
                Assert.That(child.parent.UUID, Is.EqualTo(target.uuid));
            }
            finally
            {
                DestroyTree(tree);
            }
        }

        [Test]
        public void MoveNode_AssignsScalarReferenceAndUpdatesParent()
        {
            Sequence head = CreateNode<Sequence>("Head");
            Sequence source = CreateNode<Sequence>("Source");
            Inverter target = CreateNode<Inverter>("Target");
            Always child = CreateNode<Always>("Child");
            head.events = new[] { new NodeReference(source.uuid), new NodeReference(target.uuid) };
            source.parent = new NodeReference(head.uuid);
            target.parent = new NodeReference(head.uuid);
            source.events = new[] { new NodeReference(child.uuid) };
            child.parent = new NodeReference(source.uuid);
            target.node = NodeReference.Empty;
            BehaviourTreeData tree = CreateTree(head, source, target, child);

            try
            {
                BehaviourTreeRearrangeResult result = BehaviourTreeMutator.MoveNode(tree, new BehaviourTreeMoveRequest
                {
                    NodeId = child.uuid,
                    TargetParent = target.uuid,
                    Field = nameof(Decorator.node),
                });

                Assert.That(result.Success, Is.True, result.Error);
                Assert.That(result.Source.Kind, Is.EqualTo(BehaviourTreeNodeLocationKind.Reference));
                Assert.That(result.Destination.Kind, Is.EqualTo(BehaviourTreeNodeLocationKind.Reference));
                Assert.That(result.Destination.OwnerNodeId, Is.EqualTo(target.uuid));
                Assert.That(result.Destination.Field, Is.EqualTo(nameof(Decorator.node)));
                Assert.That(result.Destination.Index, Is.EqualTo(-1));
                Assert.That(source.events, Is.Empty);
                Assert.That(target.node.UUID, Is.EqualTo(child.uuid));
                Assert.That(child.parent.UUID, Is.EqualTo(target.uuid));
            }
            finally
            {
                DestroyTree(tree);
            }
        }

        [Test]
        public void MoveNode_MovesServiceBetweenHosts()
        {
            Sequence head = CreateNode<Sequence>("Head");
            Sequence target = CreateNode<Sequence>("Target");
            Update service = CreateNode<Update>("Service");
            head.events = new[] { new NodeReference(target.uuid) };
            target.parent = new NodeReference(head.uuid);
            head.services = new List<NodeReference> { new NodeReference(service.uuid) };
            target.services = new List<NodeReference>();
            service.parent = new NodeReference(head.uuid);
            BehaviourTreeData tree = CreateTree(head, target, service);

            try
            {
                BehaviourTreeRearrangeResult result = BehaviourTreeMutator.MoveNode(tree, new BehaviourTreeMoveRequest
                {
                    NodeId = service.uuid,
                    TargetParent = target.uuid,
                    Field = nameof(ServiceHostNode.services),
                    Index = -1,
                });

                Assert.That(result.Success, Is.True, result.Error);
                Assert.That(result.Source.Kind, Is.EqualTo(BehaviourTreeNodeLocationKind.Reference));
                Assert.That(result.Destination.Kind, Is.EqualTo(BehaviourTreeNodeLocationKind.Reference));
                Assert.That(result.Source.OwnerNodeId, Is.EqualTo(head.uuid));
                Assert.That(result.Source.Field, Is.EqualTo(nameof(ServiceHostNode.services)));
                Assert.That(result.Destination.OwnerNodeId, Is.EqualTo(target.uuid));
                Assert.That(result.Destination.Field, Is.EqualTo(nameof(ServiceHostNode.services)));
                Assert.That(result.Destination.Index, Is.EqualTo(0));
                Assert.That(head.services, Is.Empty);
                Assert.That(target.services.Select(reference => reference.UUID), Is.EqualTo(new[] { service.uuid }));
                Assert.That(service.parent.UUID, Is.EqualTo(target.uuid));
            }
            finally
            {
                DestroyTree(tree);
            }
        }

        [Test]
        public void DetachNode_RemovesOwnershipAndKeepsAuthoredNode()
        {
            Sequence head = CreateNode<Sequence>("Head");
            Always child = CreateNode<Always>("Child");
            head.events = new[] { new NodeReference(child.uuid) };
            child.parent = new NodeReference(head.uuid);
            BehaviourTreeData tree = CreateTree(head, child);

            try
            {
                BehaviourTreeRearrangeResult result = BehaviourTreeMutator.DetachNode(tree, child.uuid);

                Assert.That(result.Success, Is.True, result.Error);
                Assert.That(result.Destination.Kind, Is.EqualTo(BehaviourTreeNodeLocationKind.Detached));
                Assert.That(head.events, Is.Empty);
                Assert.That(tree.GetNode(child.uuid), Is.SameAs(child));
                Assert.That(child.parent.UUID, Is.EqualTo(UUID.Empty));
            }
            finally
            {
                DestroyTree(tree);
            }
        }

        [Test]
        public void SetHead_MovesOwnedNodeToHeadAndDetachesPreviousOccurrence()
        {
            Sequence oldHead = CreateNode<Sequence>("Old Head");
            Always child = CreateNode<Always>("New Head");
            oldHead.events = new[] { new NodeReference(child.uuid) };
            child.parent = new NodeReference(oldHead.uuid);
            BehaviourTreeData tree = CreateTree(oldHead, child);

            try
            {
                BehaviourTreeRearrangeResult result = BehaviourTreeMutator.SetHead(tree, child.uuid);

                Assert.That(result.Success, Is.True, result.Error);
                Assert.That(result.Destination.Kind, Is.EqualTo(BehaviourTreeNodeLocationKind.Head));
                Assert.That(result.HeadNodeId, Is.EqualTo(child.uuid));
                Assert.That(tree.headNodeUUID, Is.EqualTo(child.uuid));
                Assert.That(child.parent.UUID, Is.EqualTo(UUID.Empty));
                Assert.That(oldHead.events, Is.Empty);
                Assert.That(tree.GetNode(oldHead.uuid), Is.SameAs(oldHead));
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
                BehaviourTreeRemoveResult result = BehaviourTreeMutator.RemoveNodes(tree, new[] { unknown });

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
                BehaviourTreeAddResult result = BehaviourTreeMutator.AddNode(tree, new BehaviourTreeAddRequest
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
