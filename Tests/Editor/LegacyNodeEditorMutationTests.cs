using Aethiumian.AI.Accessors;
using Aethiumian.AI.Editor;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aethiumian.AI.Tests
{
    /// <summary>
    /// EditMode regression coverage for the legacy Nodes editor mutation commands.
    /// </summary>
    public sealed class LegacyNodeEditorMutationTests
    {
        private readonly List<BehaviourTreeData> trees = new();
        private readonly List<AIEditorWindow> windows = new();

        /// <summary>Resets Undo and the shared editor clipboard before each mutation scenario.</summary>
        [SetUp]
        public void SetUp()
        {
            Undo.ClearAll();
            AIEditorWindow.SharedClipboard.Clear();
        }

        /// <summary>Destroys hidden editor fixtures and clears shared editor state after each scenario.</summary>
        [TearDown]
        public void TearDown()
        {
            AIEditorWindow.SharedClipboard.Clear();
            foreach (AIEditorWindow window in windows)
            {
                if (window)
                {
                    UnityEngine.Object.DestroyImmediate(window);
                }
            }

            foreach (BehaviourTreeData tree in trees)
            {
                if (tree)
                {
                    UnityEngine.Object.DestroyImmediate(tree);
                }
            }

            windows.Clear();
            trees.Clear();
            Undo.ClearAll();
        }

        /// <summary>Verifies node-only deletion clears all incoming kinds, detaches children, and round-trips through Undo/Redo.</summary>
        [UnityTest]
        public IEnumerator DeleteNodeOnly_ClearsEveryIncomingReferenceAndDetachesChildrenWithUndoRedo()
        {
            TestHost head = Node<TestHost>("Head");
            TestNode target = Node<TestNode>("Target");
            TestNode child = Node<TestNode>("Child");
            TestNode untouched = Node<TestNode>("Untouched");
            TestHost unrelatedOwner = Node<TestHost>("Unrelated Owner");
            head.single = Reference(target);
            head.list = new[] { Reference(target), Reference(target) };
            head.weighted = new[] { new Probability.EventWeight { weight = 3, reference = Reference(target) } };
            head.raw = RawReference(target);
            target.child = Reference(child);
            target.parent = Reference(head);
            child.parent = Reference(target);
            unrelatedOwner.list = new[] { NodeReference.Empty };
            NodeReference[] unrelatedArray = unrelatedOwner.list;
            BehaviourTreeData tree = Tree(head, target, child, untouched, unrelatedOwner);
            SetLayout(tree, head, target, child, untouched);
            Vector2 untouchedPosition = Position(tree, untouched);
            EditorUtility.ClearDirty(tree);
            TreeNodeModule module = OpenWindow(tree).TreeModule;

            Assert.That(module.TryDeleteNodeOnly(target, ok: true), Is.True);
            Assert.That(tree.GetNode(target.uuid), Is.Null);
            Assert.That(tree.GetNode(child.uuid), Is.SameAs(child));
            Assert.That(child.parent?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
            Assert.That(tree.headNodeUUID, Is.EqualTo(head.uuid));
            Assert.That(unrelatedOwner.list, Is.SameAs(unrelatedArray));
            AssertValid(tree);
            AssertLayoutAfterDelete(tree, target.uuid, untouched.uuid, untouchedPosition);
            Assert.That(EditorUtility.IsDirty(tree), Is.True);
            yield return AssertRuntimeInitializes(tree);

            Undo.PerformUndo();
            tree.RegenerateTable();
            TestNode restoredTarget = tree.GetNode(target.uuid) as TestNode;
            Assert.That(restoredTarget, Is.Not.Null);
            Assert.That(head.list.Select(reference => reference.UUID), Is.EqualTo(new[] { target.uuid, target.uuid }));
            Assert.That(head.weighted.Single().reference.UUID, Is.EqualTo(target.uuid));
            Assert.That(head.raw.UUID, Is.EqualTo(target.uuid));
            Assert.That(tree.GetNode(child.uuid).parent?.UUID, Is.EqualTo(target.uuid));
            Assert.That(tree.GraphLayout.TryGetPosition(target.uuid, out _), Is.True);

            Undo.PerformRedo();
            tree.RegenerateTable();
            Assert.That(tree.GetNode(target.uuid), Is.Null);
            AssertValid(tree);
            AssertLayoutAfterDelete(tree, target.uuid, untouched.uuid, untouchedPosition);
            yield return AssertRuntimeInitializes(tree);
        }

        /// <summary>Verifies node-only deletion removes a Service occurrence and detaches its child.</summary>
        [UnityTest]
        public IEnumerator DeleteNodeOnly_ServiceSlotRemovesServiceAndDetachesItsChild()
        {
            TestHost head = Node<TestHost>("Head");
            TestService service = Node<TestService>("Service");
            TestNode child = Node<TestNode>("Service Child");
            head.services = new List<NodeReference> { Reference(service) };
            service.child = Reference(child);
            service.parent = Reference(head);
            child.parent = Reference(service);
            BehaviourTreeData tree = Tree(head, service, child);
            TreeNodeModule module = OpenWindow(tree).TreeModule;

            Assert.That(module.TryDeleteNodeOnly(service, ok: true), Is.True);
            Assert.That(tree.GetNode(service.uuid), Is.Null);
            Assert.That(head.services, Is.Empty);
            Assert.That(child.parent?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
            AssertValid(tree);
            yield return AssertRuntimeInitializes(tree);
        }

        /// <summary>Verifies subtree deletion uses a stable UUID set and clears external references before removal.</summary>
        [UnityTest]
        public IEnumerator DeleteSubTree_CollectsUUIDsBeforeClearingExternalReferencesAndUndoRedo()
        {
            TestHost head = Node<TestHost>("Head");
            TestHost target = Node<TestHost>("Target");
            TestNode child = Node<TestNode>("Child");
            TestService service = Node<TestService>("Service");
            TestNode serviceChild = Node<TestNode>("Service Child");
            TestNode untouched = Node<TestNode>("Untouched");
            head.single = Reference(target);
            head.list = new[] { Reference(target), Reference(target) };
            head.weighted = new[] { new Probability.EventWeight { weight = 2, reference = Reference(child) } };
            head.raw = RawReference(serviceChild);
            target.list = new[] { Reference(child), Reference(child) };
            target.services = new List<NodeReference> { Reference(service) };
            target.parent = Reference(head);
            child.parent = Reference(target);
            service.child = Reference(serviceChild);
            service.parent = Reference(target);
            serviceChild.parent = Reference(service);
            BehaviourTreeData tree = Tree(head, target, child, service, serviceChild, untouched);
            SetLayout(tree, head, target, child, service, serviceChild, untouched);
            Vector2 headPosition = Position(tree, head);
            Vector2 untouchedPosition = Position(tree, untouched);
            TreeNodeModule module = OpenWindow(tree).TreeModule;

            Assert.That(module.TryDeleteSubTree(target, ok: true), Is.True);
            Assert.That(tree.GetNode(target.uuid), Is.Null);
            Assert.That(tree.GetNode(child.uuid), Is.Null);
            Assert.That(tree.GetNode(service.uuid), Is.Null);
            Assert.That(tree.GetNode(serviceChild.uuid), Is.Null);
            Assert.That(tree.GetNode(untouched.uuid), Is.SameAs(untouched));
            Assert.That(head.single?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
            Assert.That(head.list, Is.Empty);
            Assert.That(head.weighted, Is.Empty);
            Assert.That(head.raw?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
            AssertValid(tree);
            AssertLayoutAfterDelete(tree, target.uuid, head.uuid, headPosition, untouched.uuid, untouchedPosition);
            yield return AssertRuntimeInitializes(tree);

            Undo.PerformUndo();
            tree.RegenerateTable();
            Assert.That(tree.nodes.Select(node => node.uuid), Is.EquivalentTo(new[]
                { head.uuid, target.uuid, child.uuid, service.uuid, serviceChild.uuid, untouched.uuid }));
            Assert.That(head.single.UUID, Is.EqualTo(target.uuid));
            Assert.That(target.list.Select(reference => reference.UUID), Is.EqualTo(new[] { child.uuid, child.uuid }));
            Assert.That(target.services.Single().UUID, Is.EqualTo(service.uuid));

            Undo.PerformRedo();
            tree.RegenerateTable();
            AssertValid(tree);
            Assert.That(tree.headNodeUUID, Is.EqualTo(head.uuid));
            yield return AssertRuntimeInitializes(tree);
        }

        /// <summary>Verifies deleting the Head clears only the Head identity and preserves the detached child.</summary>
        [UnityTest]
        public IEnumerator DeleteHeadNodeOnly_ClearsHeadAndKeepsChildDetachedWithUndoRedo()
        {
            TestHost head = Node<TestHost>("Head");
            TestNode child = Node<TestNode>("Child");
            head.single = Reference(child);
            child.parent = Reference(head);
            BehaviourTreeData tree = Tree(head, child);
            SetLayout(tree, head, child);
            TreeNodeModule module = OpenWindow(tree).TreeModule;

            Assert.That(module.TryDeleteNodeOnly(head, ok: true), Is.True);
            Assert.That(tree.headNodeUUID, Is.EqualTo(UUID.Empty));
            Assert.That(tree.GetNode(head.uuid), Is.Null);
            Assert.That(tree.GetNode(child.uuid), Is.SameAs(child));
            Assert.That(child.parent?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
            Assert.That(tree.GraphLayout.TryGetPosition(head.uuid, out _), Is.False);
            AssertValid(tree);

            Undo.PerformUndo();
            tree.RegenerateTable();
            Assert.That(tree.headNodeUUID, Is.EqualTo(head.uuid));
            Assert.That(head.single.UUID, Is.EqualTo(child.uuid));
            Assert.That(child.parent.UUID, Is.EqualTo(head.uuid));

            Undo.PerformRedo();
            tree.RegenerateTable();
            Assert.That(tree.headNodeUUID, Is.EqualTo(UUID.Empty));
            AssertValid(tree);
            yield return null;
        }

        /// <summary>Verifies deleting the Head subtree removes its Services and all persisted layout entries.</summary>
        [UnityTest]
        public IEnumerator DeleteHeadSubTree_RemovesServicesAndLayoutWithoutLeavingReferences()
        {
            TestHost head = Node<TestHost>("Head");
            TestNode child = Node<TestNode>("Child");
            TestService service = Node<TestService>("Service");
            TestNode serviceChild = Node<TestNode>("Service Child");
            head.single = Reference(child);
            head.services = new List<NodeReference> { Reference(service) };
            child.parent = Reference(head);
            service.child = Reference(serviceChild);
            service.parent = Reference(head);
            serviceChild.parent = Reference(service);
            BehaviourTreeData tree = Tree(head, child, service, serviceChild);
            SetLayout(tree, head, child, service, serviceChild);
            TreeNodeModule module = OpenWindow(tree).TreeModule;

            Assert.That(module.TryDeleteSubTree(head, ok: true), Is.True);
            Assert.That(tree.headNodeUUID, Is.EqualTo(UUID.Empty));
            Assert.That(tree.nodes, Is.Empty);
            Assert.That(tree.GraphLayout.Positions, Is.Empty);
            Assert.That(tree.GraphLayout.Services, Is.Empty);
            AssertValid(tree);
            yield return null;
        }

        /// <summary>Verifies Paste Value copies authored values without changing production node identity, ownership, or layout.</summary>
        [UnityTest]
        public IEnumerator PasteValue_PreservesIdentityParentReferencesAndLayout()
        {
            TestHost head = Node<TestHost>("Head");
            Sequence source = Node<Sequence>("Source");
            Sequence sourceChild = Node<Sequence>("Source Child");
            Sequence target = Node<Sequence>("Target");
            Sequence targetChild = Node<Sequence>("Target Child");
            source.hasTrue = true;
            source.events = new[] { Reference(sourceChild) };
            target.hasTrue = false;
            target.events = new[] { Reference(targetChild) };
            head.list = new[] { Reference(target) };
            target.parent = Reference(head);
            targetChild.parent = Reference(target);
            sourceChild.parent = Reference(source);
            BehaviourTreeData tree = Tree(head, source, sourceChild, target, targetChild);
            SetLayout(tree, head, source, target, targetChild);
            Vector2 targetPosition = Position(tree, target);
            UUID targetUUID = target.uuid;
            NodeReference targetParent = new(target.parent.UUID);
            TreeNodeModule module = OpenWindow(tree).TreeModule;
            EditorUtility.ClearDirty(tree);

            module.CopyNode(source, includeSubtree: false);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
            Assert.That(module.PasteValue(target), Is.True);
            Assert.That(target.uuid, Is.EqualTo(targetUUID));
            Assert.That(target.name, Is.EqualTo("Target"));
            Assert.That(target.hasTrue, Is.EqualTo(source.hasTrue));
            Assert.That(target.events.Select(reference => reference.UUID), Is.EqualTo(new[] { targetChild.uuid }));
            Assert.That(target.parent.UUID, Is.EqualTo(targetParent.UUID));
            Assert.That(Position(tree, target), Is.EqualTo(targetPosition));
            AssertValid(tree);
            yield return AssertRuntimeInitializes(tree);
        }

        /// <summary>Verifies Head replacement changes only the Head identity and supports Undo/Redo.</summary>
        [UnityTest]
        public IEnumerator ReplaceHead_ChangesOnlyHeadIdentityWithUndoRedo()
        {
            TestHost head = Node<TestHost>("Head");
            TestHost replacement = Node<TestHost>("Replacement");
            TestNode child = Node<TestNode>("Child");
            head.single = Reference(child);
            child.parent = Reference(head);
            BehaviourTreeData tree = Tree(head, replacement, child);
            SetLayout(tree, head, replacement, child);
            Vector2 headPosition = Position(tree, head);
            Vector2 childPosition = Position(tree, child);
            TreeNodeModule module = OpenWindow(tree).TreeModule;
            EditorUtility.ClearDirty(tree);

            Assert.That(module.TrySetHeadNode(replacement), Is.True);
            Assert.That(tree.headNodeUUID, Is.EqualTo(replacement.uuid));
            Assert.That(head.single.UUID, Is.EqualTo(child.uuid));
            Assert.That(child.parent.UUID, Is.EqualTo(head.uuid));
            Assert.That(Position(tree, head), Is.EqualTo(headPosition));
            Assert.That(Position(tree, child), Is.EqualTo(childPosition));
            AssertValid(tree);
            yield return AssertRuntimeInitializes(tree);

            Undo.PerformUndo();
            tree.RegenerateTable();
            Assert.That(tree.headNodeUUID, Is.EqualTo(head.uuid));
            AssertValid(tree);
            yield return AssertRuntimeInitializes(tree);

            Undo.PerformRedo();
            tree.RegenerateTable();
            Assert.That(tree.headNodeUUID, Is.EqualTo(replacement.uuid));
            AssertValid(tree);
            yield return AssertRuntimeInitializes(tree);
        }

        /// <summary>Verifies Paste Value preserves a Raw reference on a generated production node.</summary>
        [UnityTest]
        public IEnumerator PasteValue_PreservesRawReferenceOnGeneratedProductionNode()
        {
            TestHost head = Node<TestHost>("Head");
            Rollback source = Node<Rollback>("Source");
            Rollback target = Node<Rollback>("Target");
            TestNode external = Node<TestNode>("External");
            head.single = Reference(target);
            target.parent = Reference(head);
            source.stopAt = RawReference(external);
            target.stopAt = RawReference(source);
            BehaviourTreeData tree = Tree(head, source, target, external);
            TreeNodeModule module = OpenWindow(tree).TreeModule;

            module.CopyNode(source, includeSubtree: false);
            Assert.That(module.PasteValue(target), Is.True);
            Assert.That(target.stopAt.UUID, Is.EqualTo(source.uuid));
            AssertValid(tree);
            yield return AssertRuntimeInitializes(tree);
        }

        /// <summary>Verifies duplication inserts after the actual list occurrence and round-trips through Undo/Redo.</summary>
        [UnityTest]
        public IEnumerator DuplicateNode_ListOccurrenceUsesActualOwnerAndUndoRedo()
        {
            TestHost head = Node<TestHost>("Head");
            TestNode child = Node<TestNode>("Child");
            head.list = new[] { Reference(child) };
            child.parent = Reference(head);
            BehaviourTreeData tree = Tree(head, child);
            TreeNodeModule module = OpenWindow(tree).TreeModule;

            Assert.That(module.DuplicateNodeWithUndo(child), Is.True);
            TestNode duplicate = tree.nodes.OfType<TestNode>().Single(node => node.uuid != child.uuid);
            Assert.That(duplicate.name, Is.Not.EqualTo(child.name));
            Assert.That(duplicate.parent.UUID, Is.EqualTo(head.uuid));
            Assert.That(head.list.Select(reference => reference.UUID), Is.EqualTo(new[] { child.uuid, duplicate.uuid }));
            AssertValid(tree);
            yield return AssertRuntimeInitializes(tree);

            Undo.PerformUndo();
            tree.RegenerateTable();
            Assert.That(tree.nodes.Count, Is.EqualTo(2));
            Assert.That(head.list.Select(reference => reference.UUID), Is.EqualTo(new[] { child.uuid }));

            Undo.PerformRedo();
            tree.RegenerateTable();
            Assert.That(tree.nodes.Count, Is.EqualTo(3));
            AssertValid(tree);
        }

        /// <summary>Verifies duplication uses the actual Service host occurrence and round-trips through Undo/Redo.</summary>
        [UnityTest]
        public IEnumerator DuplicateNode_ServiceOccurrenceUsesActualHostAndUndoRedo()
        {
            TestHost head = Node<TestHost>("Head");
            TestService service = Node<TestService>("Service");
            head.services = new List<NodeReference> { Reference(service) };
            service.parent = Reference(head);
            BehaviourTreeData tree = Tree(head, service);
            TreeNodeModule module = OpenWindow(tree).TreeModule;

            Assert.That(module.DuplicateNodeWithUndo(service), Is.True);
            Assert.That(tree.nodes.OfType<TestService>().Count(), Is.EqualTo(2));
            Assert.That(head.services, Has.Count.EqualTo(2));
            Assert.That(head.services.All(reference => reference.UUID != UUID.Empty), Is.True);
            Assert.That(tree.nodes.OfType<TestService>().All(node => node.parent.UUID == head.uuid), Is.True);
            AssertValid(tree);
            yield return AssertRuntimeInitializes(tree);

            Undo.PerformUndo();
            tree.RegenerateTable();
            Assert.That(tree.nodes.OfType<TestService>().Count(), Is.EqualTo(1));

            Undo.PerformRedo();
            tree.RegenerateTable();
            Assert.That(tree.nodes.OfType<TestService>().Count(), Is.EqualTo(2));
            AssertValid(tree);
        }

        /// <summary>Verifies structural Paste translates internal UUIDs while retaining external Raw targets across slots.</summary>
        [UnityTest]
        public IEnumerator PasteStructure_TranslatesInternalReferencesAndPreservesExternalRawReferences()
        {
            TestHost head = Node<TestHost>("Head");
            TestHost destination = Node<TestHost>("Destination");
            TestHost source = Node<TestHost>("Source");
            TestNode sourceChild = Node<TestNode>("Source Child");
            TestNode external = Node<TestNode>("External");
            head.single = Reference(destination);
            destination.parent = Reference(head);
            source.list = new[] { Reference(sourceChild) };
            source.raw = RawReference(external);
            sourceChild.parent = Reference(source);
            BehaviourTreeData tree = Tree(head, destination, source, sourceChild, external);
            TreeNodeModule module = OpenWindow(tree).TreeModule;
            EditorUtility.ClearDirty(tree);

            module.CopyNode(source, includeSubtree: true);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
            INodeReferenceSingleSlot single = SingleSlot(destination, nameof(TestHost.single));
            TestHost pasted = (TestHost)module.PasteTo(destination, single);
            Assert.That(pasted, Is.Not.Null);
            TestNode pastedChild = tree.GetNode(pasted.list.Single().UUID) as TestNode;
            Assert.That(pastedChild, Is.Not.Null);
            Assert.That(pasted.uuid, Is.Not.EqualTo(source.uuid));
            Assert.That(pastedChild.uuid, Is.Not.EqualTo(sourceChild.uuid));
            Assert.That(pasted.raw.UUID, Is.EqualTo(external.uuid));
            Assert.That(pasted.list.Single().UUID, Is.EqualTo(pastedChild.uuid));
            AssertValid(tree);

            module.CopyNode(source, includeSubtree: true);
            TestHost pastedList = (TestHost)module.PasteAt(destination, ListSlot(destination, nameof(TestHost.list)), 0);
            Assert.That(pastedList, Is.Not.Null);
            module.CopyNode(source, includeSubtree: true);
            TestHost pastedWeighted = (TestHost)module.PasteAt(destination, WeightedSlot(destination), 0);
            Assert.That(pastedWeighted, Is.Not.Null);
            AssertValid(tree);
            yield return AssertRuntimeInitializes(tree);
        }

        /// <summary>Verifies structural Paste into a Service host uses the Service node's authored child slot.</summary>
        [UnityTest]
        public IEnumerator PasteStructure_ServiceHostUsesTheServiceNodeChildSlot()
        {
            TestHost head = Node<TestHost>("Head");
            TestService service = Node<TestService>("Service");
            TestNode source = Node<TestNode>("Source");
            head.services = new List<NodeReference> { Reference(service) };
            service.parent = Reference(head);
            BehaviourTreeData tree = Tree(head, service, source);
            TreeNodeModule module = OpenWindow(tree).TreeModule;

            module.CopyNode(source, includeSubtree: true);
            TestNode pasted = (TestNode)module.PasteTo(service, SingleSlot(service, nameof(TestService.child)));
            Assert.That(pasted, Is.Not.Null);
            Assert.That(service.child.UUID, Is.EqualTo(pasted.uuid));
            Assert.That(pasted.parent.UUID, Is.EqualTo(service.uuid));
            AssertValid(tree);
            yield return AssertRuntimeInitializes(tree);
        }

        /// <summary>Verifies legacy Before and After paste commands use the exact sibling occurrence and preserve external references.</summary>
        [UnityTest]
        public IEnumerator PasteStructure_BeforeAndAfterUseActualSiblingOccurrence()
        {
            TestHost head = Node<TestHost>("Head");
            TestHost target = Node<TestHost>("Target");
            TestHost source = Node<TestHost>("Source");
            TestNode external = Node<TestNode>("External");
            head.list = new[] { Reference(target) };
            target.parent = Reference(head);
            source.raw = RawReference(external);
            BehaviourTreeData tree = Tree(head, target, source, external);
            TreeNodeModule module = OpenWindow(tree).TreeModule;

            module.CopyNode(source, includeSubtree: true);
            Assert.That(module.TryGetSiblingPasteTarget(target, out TreeNode parent, out INodeReferenceListSlot slot, out int index), Is.True);
            TestHost before = (TestHost)module.PasteAt(parent, slot, index);
            Assert.That(before, Is.Not.Null);
            Assert.That(head.list.Select(reference => reference.UUID), Is.EqualTo(new[] { before.uuid, target.uuid }));
            Assert.That(before.raw.UUID, Is.EqualTo(external.uuid));

            module.CopyNode(source, includeSubtree: true);
            Assert.That(module.TryGetSiblingPasteTarget(target, out parent, out slot, out index), Is.True);
            TestHost after = (TestHost)module.PasteAt(parent, slot, index + 1);
            Assert.That(after, Is.Not.Null);
            Assert.That(head.list.Select(reference => reference.UUID), Is.EqualTo(new[] { before.uuid, target.uuid, after.uuid }));
            Assert.That(after.raw.UUID, Is.EqualTo(external.uuid));
            AssertValid(tree);
            yield return AssertRuntimeInitializes(tree);
        }

        /// <summary>Verifies Copy and Copy Subtree mutate only the shared clipboard and not the tree asset.</summary>
        [Test]
        public void CopyAndCopySubtree_OnlyChangeSharedClipboard()
        {
            TestHost head = Node<TestHost>("Head");
            TestNode source = Node<TestNode>("Source");
            TestNode child = Node<TestNode>("Child");
            source.child = Reference(child);
            child.parent = Reference(source);
            BehaviourTreeData tree = Tree(head, source, child);
            TreeNodeModule module = OpenWindow(tree).TreeModule;
            EditorUtility.ClearDirty(tree);
            int undoGroup = Undo.GetCurrentGroup();

            module.CopyNode(source, includeSubtree: false);
            Assert.That(module.clipboard.Count, Is.EqualTo(1));
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
            Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(undoGroup));

            module.CopyNode(source, includeSubtree: true);
            Assert.That(module.clipboard.Count, Is.EqualTo(2));
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
            Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(undoGroup));
            AssertValid(tree);
        }

        /// <summary>Verifies a supported Upgrade preserves identity, incoming ownership, and hosted Services.</summary>
        [UnityTest]
        public IEnumerator UpgradeNode_PreservesIdentityIncomingReferenceParentAndServices()
        {
            TestHost head = Node<TestHost>("Head");
            UpgradeableHost node = Node<UpgradeableHost>("Upgradeable");
            TestService service = Node<TestService>("Service");
            head.single = Reference(node);
            node.parent = Reference(head);
            node.services = new List<NodeReference> { Reference(service) };
            service.parent = Reference(node);
            BehaviourTreeData tree = Tree(head, node, service);
            TreeNodeModule module = OpenWindow(tree).TreeModule;
            UUID uuid = node.uuid;

            Assert.That(module.TryUpgradeNode(node, prompt: false), Is.True);
            UpgradeableHostV2 upgraded = tree.GetNode(uuid) as UpgradeableHostV2;
            Assert.That(upgraded, Is.Not.Null);
            Assert.That(upgraded.name, Is.EqualTo("Upgradeable"));
            Assert.That(upgraded.parent.UUID, Is.EqualTo(head.uuid));
            Assert.That(head.single.UUID, Is.EqualTo(uuid));
            Assert.That(upgraded.services.Single().UUID, Is.EqualTo(service.uuid));
            Assert.That(service.parent.UUID, Is.EqualTo(uuid));
            AssertValid(tree);
            yield return AssertRuntimeInitializes(tree);
        }

        /// <summary>Verifies an unsupported Upgrade returns without mutating data, Dirty state, or Undo state.</summary>
        [Test]
        public void UnsupportedUpgrade_IsAZeroMutation()
        {
            TestHost head = Node<TestHost>("Head");
            TestNode node = Node<TestNode>("Node");
            head.single = Reference(node);
            node.parent = Reference(head);
            BehaviourTreeData tree = Tree(head, node);
            TreeNodeModule module = OpenWindow(tree).TreeModule;
            EditorUtility.ClearDirty(tree);
            string before = JsonUtility.ToJson(node);

            Assert.That(module.TryUpgradeNode(node, prompt: false), Is.False);
            Assert.That(JsonUtility.ToJson(node), Is.EqualTo(before));
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
            Assert.That(tree.GetNode(node.uuid), Is.SameAs(node));
        }

        /// <summary>Verifies legacy list replacement mutates only the exact occurrence and round-trips ownership through Undo/Redo.</summary>
        [Test]
        public void CommitChoiceToReference_ReplacesExactListOccurrenceWithUndoRedo()
        {
            TestHost owner = Node<TestHost>("Owner");
            TestNode first = Node<TestNode>("First");
            TestNode oldTarget = Node<TestNode>("Old Target");
            TestNode other = Node<TestNode>("Other");
            TestNode detachedCandidate = Node<TestNode>("Detached Candidate");
            owner.list = new[] { Reference(first), Reference(oldTarget), Reference(other) };
            first.parent = Reference(owner);
            oldTarget.parent = Reference(owner);
            other.parent = Reference(owner);
            BehaviourTreeData tree = Tree(owner, first, oldTarget, other, detachedCandidate);
            TreeNodeModule module = OpenWindow(tree).TreeModule;
            EditorUtility.ClearDirty(tree);

            Assert.That(module.CommitChoiceToReference(
                NodeSelectionChoice.Existing(detachedCandidate.uuid),
                NodeSelectionContext.Nodes,
                owner.uuid,
                nameof(TestHost.list),
                1,
                oldTarget.uuid,
                "Replace list reference"), Is.True);
            Assert.That(owner.list.Select(reference => reference.UUID), Is.EqualTo(
                new[] { first.uuid, detachedCandidate.uuid, other.uuid }));
            Assert.That(oldTarget.parent.UUID, Is.EqualTo(UUID.Empty));
            Assert.That(detachedCandidate.parent.UUID, Is.EqualTo(owner.uuid));
            AssertValid(tree);

            Undo.PerformUndo();
            tree.RegenerateTable();
            Assert.That(owner.list.Select(reference => reference.UUID), Is.EqualTo(
                new[] { first.uuid, oldTarget.uuid, other.uuid }));
            Assert.That(oldTarget.parent.UUID, Is.EqualTo(owner.uuid));
            Assert.That(detachedCandidate.parent.UUID, Is.EqualTo(UUID.Empty));
            AssertValid(tree);

            Undo.PerformRedo();
            tree.RegenerateTable();
            Assert.That(owner.list.Select(reference => reference.UUID), Is.EqualTo(
                new[] { first.uuid, detachedCandidate.uuid, other.uuid }));
            Assert.That(oldTarget.parent.UUID, Is.EqualTo(UUID.Empty));
            Assert.That(detachedCandidate.parent.UUID, Is.EqualTo(owner.uuid));
            AssertValid(tree);
        }

        /// <summary>Verifies self replacement is rejected without changing data or dirty state.</summary>
        [Test]
        public void CommitChoiceToReference_RejectsSelfWithoutMutation()
        {
            TestHost owner = Node<TestHost>("Owner");
            TestNode child = Node<TestNode>("Child");
            owner.list = new[] { Reference(child) };
            child.parent = Reference(owner);
            BehaviourTreeData tree = Tree(owner, child);
            TreeNodeModule module = OpenWindow(tree).TreeModule;
            EditorUtility.ClearDirty(tree);
            string before = JsonUtility.ToJson(owner);

            Assert.That(module.CommitChoiceToReference(
                NodeSelectionChoice.Existing(owner.uuid),
                NodeSelectionContext.Nodes,
                owner.uuid,
                nameof(TestHost.list),
                0,
                child.uuid,
                "Reject self reference"), Is.False);
            Assert.That(JsonUtility.ToJson(owner), Is.EqualTo(before));
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
            AssertValid(tree);
        }

        /// <summary>Creates a hidden AI editor window bound to one test tree.</summary>
        /// <param name="tree">The tree to load.</param>
        /// <returns>The initialized hidden editor window.</returns>
        private AIEditorWindow OpenWindow(BehaviourTreeData tree)
        {
            AIEditorWindow window = ScriptableObject.CreateInstance<AIEditorWindow>();
            windows.Add(window);
            window.Load(tree);
            return window;
        }

        /// <summary>Creates and tracks an in-memory BehaviourTreeData fixture.</summary>
        /// <param name="nodes">The authored nodes in declaration order.</param>
        /// <returns>The tracked tree fixture.</returns>
        private BehaviourTreeData Tree(params TreeNode[] nodes)
        {
            BehaviourTreeData tree = ScriptableObject.CreateInstance<BehaviourTreeData>();
            tree.noActionMaximumDurationLimit = true;
            tree.headNodeUUID = nodes.Length == 0 ? UUID.Empty : nodes[0].uuid;
            tree.nodes.AddRange(nodes);
            tree.RegenerateTable();
            trees.Add(tree);
            return tree;
        }

        /// <summary>Creates a named test node with a fresh authored UUID.</summary>
        /// <typeparam name="T">The concrete node type.</typeparam>
        /// <param name="name">The authored node name.</param>
        /// <returns>The initialized test node.</returns>
        private static T Node<T>(string name) where T : TreeNode, new()
        {
            return new T { name = name, uuid = UUID.NewUUID(), parent = NodeReference.Empty };
        }

        /// <summary>Creates a structural reference to a node.</summary>
        /// <param name="node">The referenced node.</param>
        /// <returns>The authored structural reference.</returns>
        private static NodeReference Reference(TreeNode node) => new(node.uuid);

        /// <summary>Creates a non-owning Raw reference to a node.</summary>
        /// <param name="node">The referenced node.</param>
        /// <returns>The authored Raw reference.</returns>
        private static RawNodeReference RawReference(TreeNode node) => new() { UUID = node.uuid };

        /// <summary>Assigns deterministic persisted positions for the supplied nodes.</summary>
        /// <param name="tree">The tree receiving the layout.</param>
        /// <param name="nodes">The nodes receiving positions.</param>
        private static void SetLayout(BehaviourTreeData tree, params TreeNode[] nodes)
        {
            tree.GraphLayout = GraphLayoutData.Create(nodes.Select((node, index) => new GraphLayoutEntry(
                node.uuid,
                new Vector2(index * 10f + 1f, index * 10f + 2f))));
        }

        /// <summary>Reads one persisted position and fails if it is missing.</summary>
        /// <param name="tree">The tree containing the layout.</param>
        /// <param name="node">The positioned node.</param>
        /// <returns>The persisted position.</returns>
        private static Vector2 Position(BehaviourTreeData tree, TreeNode node)
        {
            Assert.That(tree.GraphLayout.TryGetPosition(node.uuid, out Vector2 position), Is.True);
            return position;
        }

        private static void AssertLayoutAfterDelete(
            BehaviourTreeData tree,
            UUID deletedUUID,
            UUID preservedUUID,
            Vector2 preservedPosition,
            UUID secondPreservedUUID = default,
            Vector2 secondPreservedPosition = default)
        {
            Assert.That(tree.GraphLayout.TryGetPosition(deletedUUID, out _), Is.False);
            Assert.That(Position(tree, tree.GetNode(preservedUUID)), Is.EqualTo(preservedPosition));
            if (secondPreservedUUID != UUID.Empty)
            {
                Assert.That(Position(tree, tree.GetNode(secondPreservedUUID)), Is.EqualTo(secondPreservedPosition));
            }
        }

        private static INodeReferenceSingleSlot SingleSlot(TreeNode owner, string name)
        {
            return owner.ToReferenceSlots().OfType<INodeReferenceSingleSlot>().Single(slot => slot.Name == name);
        }

        private static INodeReferenceListSlot ListSlot(TreeNode owner, string name)
        {
            return owner.ToReferenceSlots().OfType<INodeReferenceListSlot>().Single(slot => slot.Name == name);
        }

        private static INodeReferenceListSlot WeightedSlot(TreeNode owner)
        {
            return owner.ToReferenceSlots().OfType<INodeReferenceListSlot>().Single(slot => slot.Name == nameof(TestHost.weighted));
        }

        private static void AssertValid(BehaviourTreeData tree)
        {
            Assert.That(tree.nodes, Does.Not.Contain(null));
            Assert.That(tree.nodes.Select(node => node.uuid).Distinct().Count(), Is.EqualTo(tree.nodes.Count));
            if (tree.headNodeUUID != UUID.Empty)
            {
                Assert.That(tree.GetNode(tree.headNodeUUID), Is.Not.Null);
            }

            foreach (TreeNode owner in tree.nodes)
            {
                NodeAccessor accessor = NodeAccessorProvider.GetAccessor(owner.GetType());
                foreach (INodeReferenceFieldAccessor field in accessor.NodeReferences)
                {
                    AssertReferenceResolves(tree, owner, field.Get(owner));
                }

                foreach (INodeReferenceCollectionFieldAccessor field in accessor.NodeReferenceCollections)
                {
                    foreach (object entry in field.Get(owner) ?? Array.Empty<object>())
                    {
                        if (entry is INodeReference reference)
                        {
                            AssertReferenceResolves(tree, owner, reference);
                        }
                    }
                }

                List<(TreeNode owner, bool service)> incoming = Incoming(tree, owner);
                Assert.That(incoming.Count, Is.LessThanOrEqualTo(1), $"Incoming ownership for {owner.name}");
                if (incoming.Count == 0)
                {
                    Assert.That(owner.parent?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
                }
                else
                {
                    Assert.That(owner.parent?.UUID, Is.EqualTo(incoming[0].owner.uuid));
                    if (incoming[0].service)
                    {
                        Assert.That(incoming[0].owner.GetServices().Any(reference => reference.UUID == owner.uuid), Is.True);
                    }
                }
            }

            Assert.That(tree.GetStructureValidationErrors(), Is.Empty);
        }

        private static void AssertReferenceResolves(BehaviourTreeData tree, TreeNode owner, INodeReference reference)
        {
            if (reference == null || reference.UUID == UUID.Empty)
            {
                return;
            }

            Assert.That(tree.GetNode(reference.UUID), Is.Not.Null, $"{owner.name} has a dangling reference {reference.UUID}");
        }

        private static List<(TreeNode owner, bool service)> Incoming(BehaviourTreeData tree, TreeNode target)
        {
            List<(TreeNode owner, bool service)> result = new();
            foreach (TreeNode owner in tree.nodes)
            {
                NodeAccessor accessor = NodeAccessorProvider.GetAccessor(owner.GetType());
                foreach (INodeReferenceFieldAccessor field in accessor.NodeReferences)
                {
                    if (field.Name != nameof(TreeNode.parent)
                        && field.Get(owner) is INodeReference reference
                        && !reference.IsRawReference
                        && reference.UUID == target.uuid)
                    {
                        result.Add((owner, false));
                    }
                }

                foreach (INodeReferenceCollectionFieldAccessor field in accessor.NodeReferenceCollections)
                {
                    foreach (object entry in field.Get(owner) ?? Array.Empty<object>())
                    {
                        if (entry is INodeReference reference
                            && !reference.IsRawReference
                            && reference.UUID == target.uuid)
                        {
                            result.Add((owner, field.Name == nameof(ServiceHostNode.services)));
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>Constructs a real BehaviourTree and waits for asynchronous initialization.</summary>
        /// <param name="data">The mutated authored data to initialize.</param>
        /// <returns>An EditMode coroutine that completes after initialization validation.</returns>
        private static IEnumerator AssertRuntimeInitializes(BehaviourTreeData data)
        {
            GameObject gameObject = new("LegacyNodeEditorMutationTest");
            TestBehaviour behaviour = gameObject.AddComponent<TestBehaviour>();
            BehaviourTree runtime = null;
            try
            {
                runtime = new BehaviourTree(data, gameObject, behaviour);
                float deadline = Time.realtimeSinceStartup + 5f;
                while (!runtime.IsInitialized && !runtime.IsError && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                Assert.That(runtime.IsError, Is.False, "BehaviourTree initialization faulted.");
                Assert.That(runtime.IsInitialized, Is.True, "BehaviourTree initialization timed out.");
            }
            finally
            {
                if (runtime != null && runtime.IsRunning)
                {
                    runtime.End();
                }

                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Serializable]
        private sealed class TestNode : TreeNode
        {
            public int value;
            public NodeReference child;
            public RawNodeReference raw;

            public override void Initialize() { }
            public override State Execute() => State.Success;
        }

        [Serializable]
        private sealed class TestHost : ServiceHostNode
        {
            public NodeReference single;
            public NodeReference[] list = Array.Empty<NodeReference>();
            public Probability.EventWeight[] weighted = Array.Empty<Probability.EventWeight>();
            public RawNodeReference raw;

            public override void Initialize() { }
            public override State Execute() => State.Success;
        }

        [Serializable]
        private sealed class TestService : Service
        {
            public NodeReference child;

            public override bool IsReady => true;
            public override void UpdateTimer() { }
            public override void Initialize() { }
            public override State Execute() => State.Success;
        }

        [Serializable]
        private sealed class UpgradeableHost : ServiceHostNode
        {
            public int value;

            public override TreeNode Upgrade() => new UpgradeableHostV2 { upgradedValue = value + 1 };
            public override void Initialize() { }
            public override State Execute() => State.Success;
        }

        [Serializable]
        private sealed class UpgradeableHostV2 : ServiceHostNode
        {
            public int upgradedValue;

            public override void Initialize() { }
            public override State Execute() => State.Success;
        }

        private sealed class TestBehaviour : MonoBehaviour
        {
        }
    }
}
        /// <summary>Checks that deletion removed only deleted layout entries and preserved requested coordinates.</summary>
        /// <summary>Finds one authored single-reference slot by field name.</summary>
        /// <summary>Finds one authored list-reference slot by field name.</summary>
        /// <summary>Finds the weighted list slot on a test host.</summary>
        /// <summary>Checks UUID, reference, ownership, Service, and structure invariants.</summary>
        /// <summary>Checks that a non-empty reference resolves to a node in the same tree.</summary>
        /// <summary>Enumerates authored incoming ownership for one target node.</summary>
