using Aethiumian.AI.Accessors;
using Aethiumian.AI.Editor.Exporting;
using Aethiumian.AI.Nodes;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using CommandAction = System.Action;

namespace Aethiumian.AI.Editor
{
    /// <summary>UI-neutral sink used by the shared node command registrar.</summary>
    internal interface INodeCommandMenu
    {
        void AddAction(string path, CommandAction execute);
        void AddDisabledAction(string path);
        void AddSeparator();
    }

    /// <summary>Adapts shared node commands to a UI Toolkit dropdown.</summary>
    internal sealed class DropdownNodeCommandMenu : INodeCommandMenu
    {
        private readonly DropdownMenu menu;

        internal DropdownNodeCommandMenu(DropdownMenu menu) => this.menu = menu ?? throw new ArgumentNullException(nameof(menu));

        public void AddAction(string path, CommandAction execute)
        {
            if (execute == null) throw new ArgumentNullException(nameof(execute));
            menu.AppendAction(path, _ => execute(), _ => DropdownMenuAction.Status.Normal);
        }

        public void AddDisabledAction(string path) => menu.AppendAction(path, null, _ => DropdownMenuAction.Status.Disabled);

        public void AddSeparator() => menu.AppendSeparator();
    }

    /// <summary>Adapts shared node commands to the legacy IMGUI menu.</summary>
    internal sealed class GenericNodeCommandMenu : INodeCommandMenu
    {
        private readonly GenericMenu menu;

        internal GenericNodeCommandMenu(GenericMenu menu) => this.menu = menu ?? throw new ArgumentNullException(nameof(menu));

        public void AddAction(string path, CommandAction execute)
        {
            if (execute == null) throw new ArgumentNullException(nameof(execute));
            menu.AddItem(new GUIContent(path), false, new GenericMenu.MenuFunction(execute));
        }

        public void AddDisabledAction(string path) => menu.AddDisabledItem(new GUIContent(path));

        public void AddSeparator() => menu.AddSeparator(string.Empty);
    }

    /// <summary>Records transient menu entries for tests without opening a native popup.</summary>
    internal sealed class RecordingNodeCommandMenu : INodeCommandMenu
    {
        internal sealed class Entry
        {
            internal string Path { get; }
            internal bool IsSeparator { get; }
            internal bool Enabled { get; }
            internal CommandAction Execute { get; }

            private Entry(string path, bool isSeparator, bool enabled, CommandAction execute)
            {
                Path = path;
                IsSeparator = isSeparator;
                Enabled = enabled;
                Execute = execute;
            }

            internal static Entry Separator() => new(null, true, false, null);
            internal static Entry Action(string path, bool enabled, CommandAction execute) => new(path, false, enabled, execute);
        }

        private readonly List<Entry> entries = new();
        internal IReadOnlyList<Entry> Entries => entries;

        public void AddAction(string path, CommandAction execute)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A menu action requires a path.", nameof(path));
            if (execute == null) throw new ArgumentNullException(nameof(execute));
            entries.Add(Entry.Action(path, true, execute));
        }

        public void AddDisabledAction(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A menu action requires a path.", nameof(path));
            entries.Add(Entry.Action(path, false, null));
        }

        public void AddSeparator() => entries.Add(Entry.Separator());
    }

    /// <summary>Executes a node command on one editor surface.</summary>
    internal interface INodeCommandHandler
    {
        bool SupportsRename { get; }
        void Rename(TreeNode node);
        void Copy(TreeNode node);
        void CopySubtree(TreeNode node);
        void Duplicate(TreeNode node);
        void PasteValue(TreeNode node);
        void PasteTo(TreeNode owner, INodeReferenceSingleSlot slot);
        void PasteAt(TreeNode owner, INodeReferenceListSlot slot, int index);
        void Delete(TreeNode node);
    }

    /// <summary>Owns shared command ordering and capability-to-menu translation.</summary>
    internal static class NodeCommandMenuRegistrar
    {
        /// <summary>Registers the node command groups without executing or mutating the tree.</summary>
        internal static void Register(INodeCommandMenu menu, NodeEditorCommandService queries, TreeNode node, INodeCommandHandler handler)
        {
            if (menu == null) throw new ArgumentNullException(nameof(menu));
            if (queries == null) throw new ArgumentNullException(nameof(queries));
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            if (handler.SupportsRename)
            {
                menu.AddAction("Rename", () => handler.Rename(node));
                menu.AddSeparator();
            }

            menu.AddAction("Copy", () => handler.Copy(node));
            menu.AddAction("Copy Subtree", () => handler.CopySubtree(node));
            if (queries.CanDuplicateNode(node)) menu.AddAction("Duplicate", () => handler.Duplicate(node));
            else menu.AddDisabledAction("Duplicate");
            menu.AddSeparator();

            if (queries.CanPasteValue(node)) menu.AddAction("Paste Value", () => handler.PasteValue(node));
            else menu.AddDisabledAction("Paste Value");

            IReadOnlyList<INodeReferenceSingleSlot> singles = queries.GetPasteSingleTargets(node);
            IReadOnlyList<INodeReferenceListSlot> lists = queries.GetPasteListTargets(node);
            if (singles.Count == 0 && lists.Count == 0)
            {
                menu.AddDisabledAction("Paste Under");
            }
            else
            {
                bool enabled = queries.CanPasteStructure;
                foreach (INodeReferenceSingleSlot slot in singles)
                {
                    string path = $"Paste Under/As {slot.Name.ToTitleCase()}";
                    if (enabled) menu.AddAction(path, () => handler.PasteTo(node, slot));
                    else menu.AddDisabledAction(path);
                }

                foreach (INodeReferenceListSlot slot in lists)
                {
                    string first = $"Paste Under/First/{slot.Name.ToTitleCase()}";
                    string last = $"Paste Under/Last/{slot.Name.ToTitleCase()}";
                    if (enabled)
                    {
                        menu.AddAction(first, () => handler.PasteAt(node, slot, 0));
                        menu.AddAction(last, () => handler.PasteAt(node, slot, slot.Count));
                    }
                    else
                    {
                        menu.AddDisabledAction(first);
                        menu.AddDisabledAction(last);
                    }
                }
            }

            if (queries.TryGetSiblingPasteTarget(node, out TreeNode parent, out INodeReferenceListSlot siblingSlot, out int index))
            {
                menu.AddAction("Paste Before", () => handler.PasteAt(parent, siblingSlot, index));
                menu.AddAction("Paste After", () => handler.PasteAt(parent, siblingSlot, index + 1));
            }
            else
            {
                menu.AddDisabledAction("Paste Before");
                menu.AddDisabledAction("Paste After");
            }

            menu.AddSeparator();
            menu.AddAction("Delete", () => handler.Delete(node));
            menu.AddSeparator();
            menu.AddAction("Open Documentation", () => NodeDocumentation.Open(node.GetType()));
            menu.AddSeparator();
            if (queries.Tree != null)
            {
                menu.AddAction("Readonly DOM/Copy YAML", () => BehaviourTreeDomExportCommands.CopyYaml(queries.Tree, node));
                menu.AddAction("Readonly DOM/Save YAML...", () => BehaviourTreeDomExportCommands.SaveYaml(queries.Tree, node));
            }
            else
            {
                menu.AddDisabledAction("Readonly DOM/Copy YAML");
                menu.AddDisabledAction("Readonly DOM/Save YAML...");
            }
        }
    }

    /// <summary>Routes Graph commands through GraphEditorModule's transaction boundary.</summary>
    internal sealed class GraphNodeCommandHandler : INodeCommandHandler
    {
        private readonly GraphEditorModule module;
        private readonly GraphCanvasElement canvas;

        internal GraphNodeCommandHandler(GraphEditorModule module, GraphCanvasElement canvas)
        {
            this.module = module ?? throw new ArgumentNullException(nameof(module));
            this.canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        }

        public bool SupportsRename => true;
        public void Rename(TreeNode node) => canvas.ShowRenameOverlay(node);
        public void Copy(TreeNode node) => module.CopyNode(node, false);
        public void CopySubtree(TreeNode node) => module.CopyNode(node, true);
        public void Duplicate(TreeNode node) => module.DuplicateNode(node);
        public void PasteValue(TreeNode node) => module.PasteValue(node);
        public void PasteTo(TreeNode owner, INodeReferenceSingleSlot slot) => module.PasteTo(owner, slot);
        public void PasteAt(TreeNode owner, INodeReferenceListSlot slot, int index) => module.PasteAt(owner, slot, index);
        public void Delete(TreeNode node) => module.TryDeleteNode(node);
    }

    /// <summary>Routes legacy Nodes commands through TreeNodeModule's existing mutation paths.</summary>
    internal sealed class TreeNodeCommandHandler : INodeCommandHandler
    {
        private readonly TreeNodeModule module;
        private readonly NodeEditorCommandService commands;

        internal TreeNodeCommandHandler(TreeNodeModule module, NodeEditorCommandService commands)
        {
            this.module = module ?? throw new ArgumentNullException(nameof(module));
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        public bool SupportsRename => false;
        public void Rename(TreeNode node) => throw new InvalidOperationException("The legacy Nodes menu does not support Rename.");
        public void Copy(TreeNode node) => commands.Copy(node, false);
        public void CopySubtree(TreeNode node) => commands.Copy(node, true);
        public void Duplicate(TreeNode node)
        {
            if (commands.Duplicate(node) != null) module.RefreshAfterCommand();
        }
        public void PasteValue(TreeNode node)
        {
            if (commands.PasteValue(node)) module.RefreshAfterCommand();
        }
        public void PasteTo(TreeNode owner, INodeReferenceSingleSlot slot)
        {
            if (commands.PasteTo(owner, slot) != null)
            {
                module.RefreshAfterCommand();
            }
            else if (commands.CanPasteStructure)
            {
                module.ShowConnectionRejectedNotification();
            }
        }
        public void PasteAt(TreeNode owner, INodeReferenceListSlot slot, int index)
        {
            if (commands.PasteAt(owner, slot, index) != null)
            {
                module.RefreshAfterCommand();
            }
            else if (commands.CanPasteStructure)
            {
                module.ShowConnectionRejectedNotification();
            }
        }
        public void Delete(TreeNode node) => module.TryDeleteNode(node);
    }
}
