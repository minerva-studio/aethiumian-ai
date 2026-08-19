using Aethiumian.AI.Editor;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Aethiumian.AI.Editor.Tests.NodeDrawers
{
    /// <summary>
    /// EditMode coverage for the data-only node selection dropdown catalogue.
    /// </summary>
    public sealed class NodeSelectionDropdownTests
    {
        private readonly List<BehaviourTreeData> trees = new();

        /// <summary>Destroys editor fixtures created by dropdown catalogue tests.</summary>
        [TearDown]
        public void TearDown()
        {
            foreach (BehaviourTreeData tree in trees)
            {
                if (tree)
                {
                    UnityEngine.Object.DestroyImmediate(tree);
                }
            }
        }

        [Test]
        public void NodesContext_PlacesReachableEntriesDirectlyUnderExistingNodes()
        {
            Sequence head = CreateNode<Sequence>("Head");
            Sequence orphan = CreateNode<Sequence>("Orphan");
            BehaviourTreeData tree = CreateTree(head, orphan);
            NodeSelectionDropdown dropdown = CreateDropdown(tree, new Clipboard(), NodeSelectionContext.Nodes);

            AdvancedDropdownItem root = BuildRoot(dropdown);
            List<string> names = Flatten(root).Select(item => item.name).ToList();
            AdvancedDropdownItem existingRoot = root.children.First(item => item.name == "Existing Nodes");
            List<AdvancedDropdownItem> existingChildren = existingRoot.children.ToList();

            Assert.That(root.name, Is.EqualTo("Nodes"));
            Assert.That(names, Does.Contain("Existing Nodes"));
            Assert.That(existingRoot.children.Select(item => item.name), Is.EqualTo(new[] { "Head — Sequence", "Non-reachables" }));
            Assert.That(existingRoot.children.Select(item => item.name), Does.Not.Contain("Reachables"));
            Assert.That(existingChildren[1].children.Select(item => item.name), Is.EqualTo(new[] { "Orphan — Sequence" }));
            Assert.That(names, Does.Contain("Control Flow"));
        }

        [Test]
        public void ServicesContext_UsesServicesRootAndFiltersExistingNodes()
        {
            Type serviceType = NodeMenuCache.Shared.GetCreationTypes(NodeCreationMenuContext.Services).First();
            Service service = (Service)NodeFactory.Create(serviceType);
            service.name = "Existing Service";
            BehaviourTreeData tree = CreateTree(service);
            NodeSelectionDropdown dropdown = CreateDropdown(tree, new Clipboard(), NodeSelectionContext.Services);

            AdvancedDropdownItem root = BuildRoot(dropdown);
            List<string> names = Flatten(root).Select(item => item.name).ToList();
            AdvancedDropdownItem existingRoot = root.children.First(item => item.name == "Existing Nodes");
            string existingServiceName = "Existing Service — "+NodeMenuCache.Shared.GetDisplayName(serviceType);

            Assert.That(root.name, Is.EqualTo("Services"));
            Assert.That(names, Does.Contain("Existing Nodes"));
            Assert.That(existingRoot.children.Select(item => item.name), Is.EqualTo(new[] { existingServiceName }));
            Assert.That(existingRoot.children.Select(item => item.name), Does.Not.Contain("Reachables"));
            Assert.That(names, Does.Contain(NodeMenuCache.Shared.GetDisplayName(serviceType)));
            Assert.That(names, Does.Not.Contain("Control Flow"));
        }

        [Test]
        public void ClipboardContext_OffersSingleRootPasteEntry()
        {
            Sequence head = CreateNode<Sequence>("Head");
            BehaviourTreeData tree = CreateTree(head);
            Clipboard clipboard = new();
            clipboard.Write(head, tree);
            NodeSelectionDropdown dropdown = CreateDropdown(tree, clipboard, NodeSelectionContext.Nodes);

            List<string> names = Flatten(BuildRoot(dropdown)).Select(item => item.name).ToList();

            Assert.That(names, Does.Contain("Paste (Head)"));
        }

        [Test]
        public void ExistingOnlyContext_ExpandsReachableNodesAndGroupsNonReachables()
        {
            Sequence head = CreateNode<Sequence>("Head");
            Sequence orphan = CreateNode<Sequence>("Orphan");
            BehaviourTreeData tree = CreateTree(head, orphan);
            Clipboard clipboard = new();
            clipboard.Write(head, tree);
            NodeSelectionDropdown dropdown = CreateDropdown(
                tree,
                clipboard,
                NodeSelectionContext.Nodes,
                sources: NodeSelectionSources.Existing);

            AdvancedDropdownItem root = BuildRoot(dropdown);
            List<string> names = Flatten(root).Select(item => item.name).ToList();

            Assert.That(root.name, Is.EqualTo("Select Existing Node"));
            Assert.That(root.children.Select(item => item.name), Is.EqualTo(new[] { "Head — Sequence", "Non-reachables" }));
            Assert.That(names, Does.Not.Contain("Existing Nodes"));
            Assert.That(names, Does.Not.Contain("Reachables"));
            Assert.That(names, Does.Not.Contain("Control Flow"));
            Assert.That(names.Any(name => name.StartsWith("Paste (", StringComparison.Ordinal)), Is.False);
        }

        [Test]
        public void CreateOnlyContext_ContainsOnlyCreationAndInvokesExplicitCallback()
        {
            Sequence head = CreateNode<Sequence>("Head");
            BehaviourTreeData tree = CreateTree(head);
            List<NodeSelectionChoice> choices = new();
            NodeSelectionDropdown dropdown = CreateDropdown(
                tree,
                new Clipboard(),
                NodeSelectionContext.Nodes,
                choice => choices.Add(choice),
                sources: NodeSelectionSources.Create);

            string sequenceName = NodeMenuCache.Shared.GetDisplayName(typeof(Sequence));
            AdvancedDropdownItem root = BuildRoot(dropdown);
            List<string> names = Flatten(root).Select(item => item.name).ToList();
            AdvancedDropdownItem sequenceItem = Flatten(root)
                .FirstOrDefault(item => item.name == sequenceName);
            Assert.That(root.name, Is.EqualTo("Create Node"));
            Assert.That(names, Does.Not.Contain("Existing Nodes"));
            Assert.That(names, Does.Not.Contain("Reachables"));
            Assert.That(names.Any(name => name.StartsWith("Paste (", StringComparison.Ordinal)), Is.False);
            Assert.That(sequenceItem, Is.Not.Null);

            SelectItem(dropdown, sequenceItem);

            Assert.That(choices, Has.Count.EqualTo(1));
            Assert.That(choices[0].Kind, Is.EqualTo(NodeSelectionChoiceKind.CreateType));
            Assert.That(choices[0].CreateType, Is.EqualTo(typeof(Sequence)));
        }

        /// <summary>Verifies that a caller can remove invalid existing-node candidates without changing other catalogue entries.</summary>
        [Test]
        public void ExistingNodeFilter_HidesInvalidCandidateAndKeepsValidCandidate()
        {
            Sequence hidden = CreateNode<Sequence>("Hidden");
            Sequence visible = CreateNode<Sequence>("Visible");
            BehaviourTreeData tree = CreateTree(hidden, visible);
            NodeSelectionDropdown dropdown = CreateDropdown(
                tree,
                new Clipboard(),
                NodeSelectionContext.Nodes,
                existingNodeFilter: node => node != hidden);

            List<string> names = Flatten(BuildRoot(dropdown)).Select(item => item.name).ToList();

            Assert.That(names, Does.Not.Contain("Hidden — Sequence"));
            Assert.That(names, Does.Contain("Visible — Sequence"));
        }

        /// <summary>Verifies that the owner can be excluded from structural reference choices.</summary>
        [Test]
        public void ExistingNodeFilter_HidesOwnerSelfReference()
        {
            Sequence owner = CreateNode<Sequence>("Owner");
            Sequence candidate = CreateNode<Sequence>("Candidate");
            BehaviourTreeData tree = CreateTree(owner, candidate);
            NodeSelectionDropdown dropdown = CreateDropdown(
                tree,
                new Clipboard(),
                NodeSelectionContext.Nodes,
                existingNodeFilter: node => tree.CanInsertReference(
                    owner.uuid,
                    nameof(Sequence.events),
                    node.uuid,
                    allowMoveExisting: true));

            List<string> names = Flatten(BuildRoot(dropdown)).Select(item => item.name).ToList();

            Assert.That(names.Any(name => name.StartsWith("Owner — ", StringComparison.Ordinal)), Is.False);
            Assert.That(names.Any(name => name.StartsWith("Candidate — ", StringComparison.Ordinal)), Is.True);
        }

        private NodeSelectionDropdown CreateDropdown(
            BehaviourTreeData tree,
            Clipboard clipboard,
            NodeSelectionContext context,
            Action<NodeSelectionChoice> callback = null,
            Func<TreeNode, bool> existingNodeFilter = null,
            NodeSelectionSources sources = NodeSelectionSources.Mixed)
        {
            return new NodeSelectionDropdown(
                tree,
                clipboard,
                context,
                callback ?? (_ => { }),
                existingNodeFilter,
                sources);
        }

        private BehaviourTreeData CreateTree(params TreeNode[] nodes)
        {
            BehaviourTreeData tree = ScriptableObject.CreateInstance<BehaviourTreeData>();
            tree.noActionMaximumDurationLimit = true;
            tree.headNodeUUID = nodes.Length == 0 ? UUID.Empty : nodes[0].uuid;
            tree.nodes.AddRange(nodes);
            tree.RegenerateTable();
            trees.Add(tree);
            return tree;
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

        private static AdvancedDropdownItem BuildRoot(NodeSelectionDropdown dropdown)
        {
            MethodInfo buildRoot = typeof(NodeSelectionDropdown).GetMethod(
                "BuildRoot",
                BindingFlags.Instance | BindingFlags.NonPublic);
            return (AdvancedDropdownItem)buildRoot.Invoke(dropdown, null);
        }

        /// <summary>
        /// Invokes the dropdown selection callback for a concrete item.
        /// </summary>
        private static void SelectItem(NodeSelectionDropdown dropdown, AdvancedDropdownItem item)
        {
            MethodInfo itemSelected = typeof(NodeSelectionDropdown).GetMethod(
                "ItemSelected",
                BindingFlags.Instance | BindingFlags.NonPublic);
            itemSelected.Invoke(dropdown, new object[] { item });
        }

        private static IEnumerable<AdvancedDropdownItem> Flatten(AdvancedDropdownItem item)
        {
            yield return item;
            foreach (AdvancedDropdownItem child in item.children)
            {
                foreach (AdvancedDropdownItem descendant in Flatten(child))
                {
                    yield return descendant;
                }
            }
        }
    }
}
