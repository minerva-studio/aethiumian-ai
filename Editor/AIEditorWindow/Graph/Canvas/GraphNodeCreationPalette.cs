using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UIPosition = UnityEngine.UIElements.Position;

namespace Aethiumian.AI.Editor
{
    /// <summary>
    /// A canvas-local UI Toolkit command palette for creating behaviour-tree nodes.
    /// </summary>
    internal sealed class GraphNodeCreationPalette : VisualElement
    {
        private const float Width = 320f;
        private const float Height = 360f;

        private readonly NodeMenuCache menuCache;
        private readonly Func<Type, bool> typeFilter;
        private readonly Action<Type> onNodeSelected;
        private readonly Action onClosed;
        private readonly ToolbarSearchField searchField;
        private readonly Button breadcrumb;
        private readonly ScrollView results;
        private readonly List<Entry> visibleEntries = new();
        private NodeMenuPathFolder currentFolder;
        private int selectedIndex;

        /// <summary>
        /// Initializes a node-creation palette using the shared node menu catalogue.
        /// </summary>
        /// <param name="typeFilter">Limits candidates for the current graph port context.</param>
        /// <param name="onNodeSelected">Receives a selected node type.</param>
        /// <param name="onClosed">Closes the owning canvas overlay.</param>
        internal GraphNodeCreationPalette(Func<Type, bool> typeFilter, Action<Type> onNodeSelected, Action onClosed)
        {
            this.typeFilter = typeFilter ?? throw new ArgumentNullException(nameof(typeFilter));
            this.onNodeSelected = onNodeSelected ?? throw new ArgumentNullException(nameof(onNodeSelected));
            this.onClosed = onClosed ?? throw new ArgumentNullException(nameof(onClosed));
            menuCache = NodeMenuCache.Shared;
            currentFolder = menuCache.MenuPathRoot;

            name = "ai-editor-graph-node-creation-palette";
            AddToClassList("ai-editor-graph-node-creation-palette");
            style.position = UIPosition.Absolute;
            style.width = Width;
            style.height = Height;

            searchField = new ToolbarSearchField { name = "ai-editor-graph-node-creation-search" };
            searchField.AddToClassList("ai-editor-graph-node-creation-search");
            searchField.RegisterValueChangedCallback(_ => RebuildEntries());
            Add(searchField);

            breadcrumb = new Button(NavigateUp) { name = "ai-editor-graph-node-creation-breadcrumb" };
            breadcrumb.AddToClassList("ai-editor-graph-node-creation-breadcrumb");
            Add(breadcrumb);

            results = new ScrollView { name = "ai-editor-graph-node-creation-results" };
            results.AddToClassList("ai-editor-graph-node-creation-results");
            results.style.flexGrow = 1f;
            Add(results);

            RegisterCallback<PointerDownEvent>(StopPointerPropagation, TrickleDown.TrickleDown);
            RegisterCallback<WheelEvent>(StopWheelPropagation, TrickleDown.TrickleDown);
            RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            RebuildEntries();
        }

        /// <summary>Shows the palette at a viewport-local position, constrained to the canvas bounds.</summary>
        internal void ShowAt(Vector2 viewportPosition, Vector2 viewportSize)
        {
            float x = Mathf.Clamp(viewportPosition.x, 0f, Mathf.Max(0f, viewportSize.x - Width));
            float y = Mathf.Clamp(viewportPosition.y, 0f, Mathf.Max(0f, viewportSize.y - Height));
            style.left = x;
            style.top = y;
            schedule.Execute(() => searchField.Focus());
        }

        private void NavigateUp()
        {
            if (currentFolder == menuCache.MenuPathRoot || !string.IsNullOrEmpty(searchField.value))
            {
                searchField.value = string.Empty;
                currentFolder = menuCache.MenuPathRoot;
                return;
            }

            currentFolder = FindParent(menuCache.MenuPathRoot, currentFolder) ?? menuCache.MenuPathRoot;
            RebuildEntries();
        }

        private void RebuildEntries()
        {
            visibleEntries.Clear();
            results.Clear();
            string query = searchField.value?.Trim() ?? string.Empty;
            bool searching = !string.IsNullOrEmpty(query);
            breadcrumb.text = searching ? "Search results (Back)" : GetFolderPath(currentFolder);
            breadcrumb.SetEnabled(searching || currentFolder != menuCache.MenuPathRoot);

            if (searching)
            {
                foreach (Type type in menuCache.AllNodeTypes.Where(typeFilter).Where(type => Matches(type, query)).OrderBy(menuCache.GetMenuPath).ThenBy(menuCache.GetDisplayName))
                {
                    AddNodeEntry(type, menuCache.GetMenuPath(type));
                }
            }
            else
            {
                foreach (NodeMenuPathFolder folder in currentFolder.Children.Values.Where(HasVisibleEntries))
                {
                    AddFolderEntry(folder);
                }

                foreach (Type type in currentFolder.Types.Where(typeFilter).OrderBy(menuCache.GetDisplayName))
                {
                    AddNodeEntry(type, menuCache.GetTooltip(type));
                }
            }

            if (visibleEntries.Count == 0)
            {
                Label empty = new("No matching nodes");
                empty.AddToClassList("ai-editor-graph-node-creation-empty");
                results.Add(empty);
            }

            selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, visibleEntries.Count - 1));
            RefreshSelection();
        }

        private void AddFolderEntry(NodeMenuPathFolder folder)
        {
            AddEntry(new Entry(folder), folder.Name, "Browse category");
        }

        private void AddNodeEntry(Type type, string detail)
        {
            AddEntry(new Entry(type), menuCache.GetDisplayName(type), detail);
        }

        private void AddEntry(Entry entry, string title, string detail)
        {
            Button row = new(() => Activate(entry)) { text = title };
            row.AddToClassList("ai-editor-graph-node-creation-row");
            row.tooltip = detail ?? string.Empty;
            visibleEntries.Add(entry.WithRow(row));
            results.Add(row);
        }

        private void Activate(Entry entry)
        {
            if (entry.Folder != null)
            {
                currentFolder = entry.Folder;
                RebuildEntries();
                return;
            }

            onNodeSelected(entry.Type);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape)
            {
                onClosed();
                evt.StopPropagation();
                return;
            }

            if (visibleEntries.Count == 0)
            {
                return;
            }

            if (evt.keyCode == KeyCode.DownArrow || evt.keyCode == KeyCode.UpArrow)
            {
                selectedIndex = Mathf.Clamp(selectedIndex + (evt.keyCode == KeyCode.DownArrow ? 1 : -1), 0, visibleEntries.Count - 1);
                RefreshSelection();
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                Activate(visibleEntries[selectedIndex]);
                evt.StopPropagation();
            }
        }

        private void RefreshSelection()
        {
            for (int index = 0; index < visibleEntries.Count; index++)
            {
                visibleEntries[index].Row.EnableInClassList("ai-editor-graph-node-creation-row-selected", index == selectedIndex);
            }
        }

        private bool HasVisibleEntries(NodeMenuPathFolder folder)
        {
            return folder.Types.Any(typeFilter) || folder.Children.Values.Any(HasVisibleEntries);
        }

        private bool Matches(Type type, string query)
        {
            return menuCache.GetDisplayName(type).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || type.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || menuCache.GetMenuPath(type).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string GetFolderPath(NodeMenuPathFolder folder)
        {
            if (folder == menuCache.MenuPathRoot)
            {
                return "Nodes";
            }

            List<string> parts = new();
            while (folder != null && folder != menuCache.MenuPathRoot)
            {
                parts.Add(folder.Name);
                folder = FindParent(menuCache.MenuPathRoot, folder);
            }

            parts.Reverse();
            return string.Join(" / ", parts);
        }

        private static NodeMenuPathFolder FindParent(NodeMenuPathFolder root, NodeMenuPathFolder target)
        {
            foreach (NodeMenuPathFolder child in root.Children.Values)
            {
                if (child == target)
                {
                    return root;
                }

                NodeMenuPathFolder nested = FindParent(child, target);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static void StopPointerPropagation(PointerDownEvent evt) => evt.StopPropagation();
        private static void StopWheelPropagation(WheelEvent evt) => evt.StopPropagation();

        private readonly struct Entry
        {
            internal Entry(NodeMenuPathFolder folder) { Folder = folder; Type = null; Row = null; }
            internal Entry(Type type) { Folder = null; Type = type; Row = null; }
            internal NodeMenuPathFolder Folder { get; }
            internal Type Type { get; }
            internal Button Row { get; }
            internal Entry WithRow(Button row) => new(Folder, Type, row);
            private Entry(NodeMenuPathFolder folder, Type type, Button row) { Folder = folder; Type = type; Row = row; }
        }
    }
}
