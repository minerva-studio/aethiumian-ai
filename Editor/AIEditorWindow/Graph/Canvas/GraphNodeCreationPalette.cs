using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UIPosition = UnityEngine.UIElements.Position;

namespace Aethiumian.AI.Editor
{
    /// <summary>A canvas-local UI Toolkit list palette for creating behaviour-tree nodes.</summary>
    internal sealed class GraphNodeCreationPalette : VisualElement
    {
        private const float Width = 320f;
        private const float Height = 360f;
        private const float RowHeight = 38f;

        private readonly NodeMenuCache menuCache;
        private readonly Func<Type, bool> typeFilter;
        private readonly Action<Type> onNodeSelected;
        private readonly Action onClosed;
        private readonly ToolbarSearchField searchField;
        private readonly Button breadcrumb;
        private readonly ListView results;
        private readonly List<Entry> visibleEntries = new();
        private readonly NodeCreationMenuFolder rootFolder;
        private NodeCreationMenuFolder currentFolder;
        private int selectedIndex;

        /// <summary>Initializes a list-backed node creation palette.</summary>
        internal GraphNodeCreationPalette(Func<Type, bool> typeFilter, Action<Type> onNodeSelected, Action onClosed)
        {
            this.typeFilter = typeFilter ?? throw new ArgumentNullException(nameof(typeFilter));
            this.onNodeSelected = onNodeSelected ?? throw new ArgumentNullException(nameof(onNodeSelected));
            this.onClosed = onClosed ?? throw new ArgumentNullException(nameof(onClosed));
            menuCache = NodeMenuCache.Shared;
            rootFolder = menuCache.BuildCreationMenu(typeFilter);
            currentFolder = rootFolder;

            name = "ai-editor-graph-node-creation-palette";
            AddToClassList("ai-editor-graph-node-creation-palette");
            style.position = UIPosition.Absolute;
            style.width = Width;
            style.height = Height;

            searchField = new ToolbarSearchField { name = "ai-editor-graph-node-creation-search" };
            searchField.AddToClassList("ai-editor-graph-node-creation-search");
            searchField.RegisterValueChangedCallback(_ => RebuildEntries());
            Add(searchField);

            breadcrumb = new Button(NavigateUp)
            {
                name = "ai-editor-graph-node-creation-breadcrumb",
                focusable = false,
            };
            breadcrumb.AddToClassList("ai-editor-graph-node-creation-breadcrumb");
            Add(breadcrumb);

            results = new ListView
            {
                name = "ai-editor-graph-node-creation-results",
                fixedItemHeight = RowHeight,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                selectionType = SelectionType.Single,
                makeItem = MakeRow,
                bindItem = BindRow,
            };
            results.AddToClassList("ai-editor-graph-node-creation-results");
            results.RegisterCallback<WheelEvent>(StopWheelPropagation);
            Add(results);

            RegisterCallback<PointerDownEvent>(StopPointerPropagation);
            RegisterCallback<WheelEvent>(StopWheelPropagation);
            RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            RebuildEntries();
        }

        /// <summary>Shows the palette at a viewport-local position constrained to the canvas bounds.</summary>
        internal void ShowAt(Vector2 viewportPosition, Vector2 viewportSize)
        {
            style.left = Mathf.Clamp(viewportPosition.x, 0f, Mathf.Max(0f, viewportSize.x - Width));
            style.top = Mathf.Clamp(viewportPosition.y, 0f, Mathf.Max(0f, viewportSize.y - Height));
            schedule.Execute(() => searchField.Focus());
        }

        private VisualElement MakeRow()
        {
            RowElement row = new();
            row.RegisterCallback<PointerUpEvent>(OnRowPointerUp);
            return row;
        }

        private void BindRow(VisualElement element, int index)
        {
            RowElement row = (RowElement)element;
            Entry entry = visibleEntries[index];
            row.userData = index;
            row.Title.text = entry.Folder != null ? entry.Folder.Name : menuCache.GetDisplayName(entry.Type);
            row.Detail.text = entry.Folder != null ? "Browse category" : menuCache.GetTooltip(entry.Type);
            row.Marker.text = entry.Folder != null ? "›" : string.Empty;
            row.EnableInClassList("ai-editor-graph-node-creation-row-selected", index == selectedIndex);
        }

        private void OnRowPointerUp(PointerUpEvent evt)
        {
            if (evt.button != 0 || evt.currentTarget is not RowElement row || row.userData is not int index)
            {
                return;
            }

            selectedIndex = index;
            Activate(visibleEntries[index]);
            evt.StopPropagation();
        }

        private void NavigateUp()
        {
            if (currentFolder == rootFolder || !string.IsNullOrEmpty(searchField.value))
            {
                searchField.value = string.Empty;
                currentFolder = rootFolder;
                RebuildEntries();
                searchField.Focus();
                return;
            }

            currentFolder = FindParent(rootFolder, currentFolder) ?? rootFolder;
            RebuildEntries();
            searchField.Focus();
        }

        private void RebuildEntries()
        {
            visibleEntries.Clear();
            string query = searchField.value?.Trim() ?? string.Empty;
            bool searching = !string.IsNullOrEmpty(query);
            if (currentFolder == null || (currentFolder != rootFolder && FindParent(rootFolder, currentFolder) == null))
            {
                currentFolder = rootFolder;
            }

            breadcrumb.text = searching ? "Search results (Back)" : GetFolderPath(rootFolder, currentFolder);
            breadcrumb.SetEnabled(searching || currentFolder != rootFolder);

            if (searching)
            {
                foreach (Type type in menuCache.AllNodeTypes.Where(typeFilter).Where(type => Matches(type, query)).OrderBy(menuCache.GetDisplayName))
                {
                    visibleEntries.Add(new Entry(type));
                }
            }
            else
            {
                foreach (NodeCreationMenuFolder folder in currentFolder.Children)
                {
                    visibleEntries.Add(new Entry(folder));
                }

                foreach (Type type in currentFolder.Types.OrderBy(menuCache.GetDisplayName))
                {
                    visibleEntries.Add(new Entry(type));
                }
            }

            selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, visibleEntries.Count - 1));
            results.itemsSource = visibleEntries;
            results.RefreshItems();
            results.selectedIndex = visibleEntries.Count == 0 ? -1 : selectedIndex;
        }

        private void Activate(Entry entry)
        {
            if (entry.Folder != null)
            {
                currentFolder = entry.Folder;
                selectedIndex = 0;
                RebuildEntries();
                searchField.Focus();
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

            if (evt.keyCode is KeyCode.DownArrow or KeyCode.UpArrow)
            {
                selectedIndex = Mathf.Clamp(selectedIndex + (evt.keyCode == KeyCode.DownArrow ? 1 : -1), 0, visibleEntries.Count - 1);
                results.selectedIndex = selectedIndex;
                results.ScrollToItem(selectedIndex);
                results.RefreshItems();
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
            {
                Activate(visibleEntries[selectedIndex]);
                evt.StopPropagation();
            }
        }

        private bool Matches(Type type, string query)
        {
            return menuCache.GetDisplayName(type).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || type.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || menuCache.GetMenuPath(type).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static NodeCreationMenuFolder FindParent(NodeCreationMenuFolder root, NodeCreationMenuFolder target)
        {
            if (root == null || target == null)
            {
                return null;
            }

            foreach (NodeCreationMenuFolder child in root.Children)
            {
                if (child == target)
                {
                    return root;
                }

                NodeCreationMenuFolder parent = FindParent(child, target);
                if (parent != null)
                {
                    return parent;
                }
            }

            return null;
        }

        private static string GetFolderPath(NodeCreationMenuFolder root, NodeCreationMenuFolder folder)
        {
            if (folder == root)
            {
                return "Nodes";
            }

            List<string> parts = new();
            NodeCreationMenuFolder current = folder;
            while (current != null && current != root)
            {
                parts.Add(current.Name);
                current = FindParent(root, current);
            }

            parts.Reverse();
            return string.Join(" / ", parts);
        }

        private static void StopPointerPropagation(PointerDownEvent evt) => evt.StopPropagation();
        private static void StopWheelPropagation(WheelEvent evt) => evt.StopPropagation();

        private sealed class RowElement : VisualElement
        {
            internal readonly Label Marker = new();
            internal readonly Label Title = new();
            internal readonly Label Detail = new();

            internal RowElement()
            {
                AddToClassList("ai-editor-graph-node-creation-row");
                focusable = false;
                Marker.AddToClassList("ai-editor-graph-node-creation-row-marker");
                Title.AddToClassList("ai-editor-graph-node-creation-row-title");
                Detail.AddToClassList("ai-editor-graph-node-creation-row-detail");
                Add(Marker);
                VisualElement text = new();
                text.AddToClassList("ai-editor-graph-node-creation-row-text");
                text.Add(Title);
                text.Add(Detail);
                Add(text);
            }
        }

        private readonly struct Entry
        {
            internal Entry(NodeCreationMenuFolder folder) { Folder = folder; Type = null; }
            internal Entry(Type type) { Folder = null; Type = type; }
            internal NodeCreationMenuFolder Folder { get; }
            internal Type Type { get; }
        }
    }
}
