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

namespace Aethiumian.AI.Tests
{
    /// <summary>
    /// EditMode coverage for the legacy Nodes AdvancedDropdown catalogue.
    /// </summary>
    public sealed class NodeSelectionDropdownTests
    {
        private readonly List<BehaviourTreeData> trees = new();
        private readonly List<AIEditorWindow> windows = new();

        /// <summary>Destroys editor fixtures created by dropdown catalogue tests.</summary>
        [TearDown]
        public void TearDown()
        {
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
        }

        [Test]
        public void NodesContext_ContainsCreationFoldersAndExistingReachableNodes()
        {
            Sequence head = CreateNode<Sequence>("Head");
            BehaviourTreeData tree = CreateTree(head);
            AIEditorWindow window = CreateWindow(tree);

            NodeSelectionDropdown dropdown = new(window.TreeModule, NodeSelectionContext.Nodes, null, false);
            AdvancedDropdownItem root = BuildRoot(dropdown);
            List<string> names = Flatten(root).Select(item => item.name).ToList();

            Assert.That(root.name, Is.EqualTo("Nodes"));
            Assert.That(names, Does.Contain("Existing Nodes"));
            Assert.That(names, Does.Contain("Reachables"));
            Assert.That(names.Any(name => name.StartsWith("Head — ", StringComparison.Ordinal)), Is.True);
            Assert.That(names, Does.Contain("Control Flow"));
        }

        [Test]
        public void RawReferenceContext_ExcludesCreationEntries()
        {
            Sequence head = CreateNode<Sequence>("Head");
            BehaviourTreeData tree = CreateTree(head);
            AIEditorWindow window = CreateWindow(tree);

            NodeSelectionDropdown dropdown = new(window.TreeModule, NodeSelectionContext.Nodes, null, true);
            AdvancedDropdownItem root = BuildRoot(dropdown);

            Assert.That(root.children.Select(item => item.name), Is.EqualTo(new[] { "Existing Nodes" }));
        }

        [Test]
        public void ServicesContext_UsesServicesRootAndIncludesExistingServices()
        {
            Type serviceType = NodeMenuCache.Shared.GetCreationTypes(NodeCreationMenuContext.Services).First();
            Service service = (Service)NodeFactory.Create(serviceType);
            service.name = "Existing Service";
            BehaviourTreeData tree = CreateTree(service);
            AIEditorWindow window = CreateWindow(tree);

            NodeSelectionDropdown dropdown = new(window.TreeModule, NodeSelectionContext.Services, null, false);
            AdvancedDropdownItem root = BuildRoot(dropdown);
            List<string> names = Flatten(root).Select(item => item.name).ToList();

            Assert.That(root.name, Is.EqualTo("Services"));
            Assert.That(names, Does.Contain("Existing Nodes"));
            Assert.That(names.Any(name => name.StartsWith("Existing Service — ", StringComparison.Ordinal)), Is.True);
            string serviceDisplayName = NodeMenuCache.Shared.GetDisplayName(serviceType);
            Assert.That(names, Does.Contain(serviceDisplayName));
        }

        [Test]
        public void SelectingCreationItem_InvokesDropdownCallbackAfterCreate()
        {
            Sequence head = CreateNode<Sequence>("Head");
            BehaviourTreeData tree = CreateTree(head);
            AIEditorWindow window = CreateWindow(tree);
            List<TreeNode> selectedNodes = new();
            NodeSelectionDropdown dropdown = new(
                window.TreeModule,
                NodeSelectionContext.Nodes,
                node => selectedNodes.Add(node),
                false);

            AdvancedDropdownItem root = BuildRoot(dropdown);
            string sequenceName = NodeMenuCache.Shared.GetDisplayName(typeof(Sequence));
            AdvancedDropdownItem sequenceItem = Flatten(root)
                .FirstOrDefault(item => item.name == sequenceName);
            Assert.That(sequenceItem, Is.Not.Null);

            SelectItem(dropdown, sequenceItem);

            Assert.That(selectedNodes, Has.Count.EqualTo(1));
            Assert.That(tree.GetNode(selectedNodes[0].uuid), Is.SameAs(selectedNodes[0]));
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

        private AIEditorWindow CreateWindow(BehaviourTreeData tree)
        {
            AIEditorWindow window = ScriptableObject.CreateInstance<AIEditorWindow>();
            windows.Add(window);
            window.Load(tree);
            return window;
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
        /// <param name="dropdown">The dropdown that owns the selection callback.</param>
        /// <param name="item">The item to select.</param>
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
