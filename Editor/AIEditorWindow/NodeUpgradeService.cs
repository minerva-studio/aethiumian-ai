using Aethiumian.AI.Accessors;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using System;
using System.Collections.Generic;
using UnityEditor;

namespace Aethiumian.AI.Editor
{
    /// <summary>Owns editor-side node upgrades, including legacy one-to-many wait migrations.</summary>
    internal static class NodeUpgradeService
    {
        internal static bool CanUpgrade(BehaviourTreeData tree, TreeNode node)
        {
            if (tree == null || node == null || tree.GetNode(node.uuid) != node) return false;
            if (node is WaitWhile waitWhile) return waitWhile.condition?.UUID != UUID.Empty && tree.GetNode(waitWhile.condition.UUID) != null;
            if (node is WaitUntil waitUntil) return waitUntil.condition?.UUID != UUID.Empty && tree.GetNode(waitUntil.condition.UUID) != null;
            try { return node.CanUpgrade(); }
            catch (NotImplementedException) { return false; }
        }

        /// <summary>Replaces one node in-place and returns all newly inserted helper nodes.</summary>
        internal static bool Upgrade(BehaviourTreeData tree, TreeNode node, out TreeNode replacement, out IReadOnlyList<TreeNode> added)
        {
            replacement = null;
            added = Array.Empty<TreeNode>();
            if (!CanUpgrade(tree, node)) return false;

            int index = tree.nodes.IndexOf(node);
            TreeNode parent = tree.GetNode(node.parent);
            if (node is WaitWhile waitWhile)
            {
                Loop loop = CreateIdentity<Loop>(tree, node);
                loop.loopType = Loop.LoopType.@while;
                loop.condition = waitWhile.condition;
                Yield yield = CreateHelper<Yield>(tree, "Yield");
                loop.events = new[] { yield.ToReference() };
                yield.parent = loop.ToReference();
                replacement = loop;
                added = new[] { yield };
            }
            else if (node is WaitUntil waitUntil)
            {
                Loop loop = CreateIdentity<Loop>(tree, node);
                loop.loopType = Loop.LoopType.@while;
                Inverter inverter = CreateHelper<Inverter>(tree, "Inverter");
                inverter.node = waitUntil.condition;
                loop.condition = inverter.ToReference();
                Yield yield = CreateHelper<Yield>(tree, "Yield");
                loop.events = new[] { yield.ToReference() };
                inverter.parent = loop.ToReference();
                yield.parent = loop.ToReference();
                TreeNode condition = tree.GetNode(waitUntil.condition.UUID);
                condition.parent = inverter.ToReference();
                replacement = loop;
                added = new TreeNode[] { inverter, yield };
            }
            else
            {
                try { replacement = node.Upgrade(); }
                catch (Exception) { return false; }
                if (replacement == null) return false;
                CopyIdentity(node, replacement);
            }

            tree.nodes[index] = replacement;
            foreach (TreeNode helper in added) tree.nodes.Add(helper);
            tree.RegenerateTable();
            foreach (TreeNode current in tree.EditorNodes)
            {
                if (current != null && current != replacement && current != node && current.parent?.UUID == node.uuid)
                    current.parent = replacement.ToReference();
            }
            EditorUtility.SetDirty(tree);
            return true;
        }

        /// <summary>Resolves and persists the complete graph layout after a topology change.</summary>
        internal static void CommitLayout(BehaviourTreeData tree)
        {
            GraphTopology topology = GraphTopologyBuilder.Build(tree, true);
            GraphLayoutResolver.Resolve(tree, topology);
            tree.GraphLayout = GraphLayoutResolver.CreateLayout(topology, tree.GraphLayout);
            EditorUtility.SetDirty(tree);
        }

        private static T CreateIdentity<T>(BehaviourTreeData tree, TreeNode source) where T : TreeNode, new()
        {
            T result = new()
            {
                UUID = source.UUID,
                name = source.name,
                parent = source.parent,
            };
            CopyServices(source, result);
            return result;
        }

        private static T CreateHelper<T>(BehaviourTreeData tree, string displayName) where T : TreeNode, new()
        {
            T helper = new() { UUID = UUID.NewUUID(), name = tree.GenerateNewNodeName(displayName) };
            return helper;
        }

        private static void CopyIdentity(TreeNode source, TreeNode replacement)
        {
            replacement.UUID = source.UUID;
            replacement.name = source.name;
            replacement.parent = source.parent;
            CopyServices(source, replacement);
        }

        private static void CopyServices(TreeNode source, TreeNode replacement)
        {
            if (!ServiceHostNodeUtility.TryAsServiceHost(source, out IServiceHostNode oldHost)
                || !ServiceHostNodeUtility.TryAsServiceHost(replacement, out IServiceHostNode newHost)
                || oldHost.Services == null || oldHost.Services.Count == 0) return;
            newHost.EnsureServices().AddRange(oldHost.Services);
        }
    }
}
