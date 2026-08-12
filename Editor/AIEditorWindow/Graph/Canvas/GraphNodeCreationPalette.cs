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
        private const float RowHeight = 28f;

        private readonly NodeMenuCache menuCache;
        private readonly NodeCreationMenuContext context;
        private readonly Action<Type> onNodeSelected;
        private readonly Action onClosed;
        private readonly ToolbarSearchField searchField;
        private readonly Button backButton;
        private readonly Label titleLabel;
        private readonly ListView results;
        private readonly Label detailLabel;
        private readonly List<Entry> visibleEntries = new();
        private readonly NodeCreationMenuFolder rootFolder;
        private NodeCreationMenuFolder currentFolder;
        private int selectedIndex;
        private readonly Stack<FolderState> history = new();
        private FolderState searchOrigin;

        /// <summary>Initializes a list-backed node creation palette.</summary>
        internal GraphNodeCreationPalette(NodeCreationMenuContext context, Action<Type> onNodeSelected, Action onClosed)
        {
            this.context = context;
            this.onNodeSelected = onNodeSelected ?? throw new ArgumentNullException(nameof(onNodeSelected));
            this.onClosed = onClosed ?? throw new ArgumentNullException(nameof(onClosed));
            menuCache = NodeMenuCache.Shared;
            rootFolder = menuCache.BuildCreationMenu(context);
            currentFolder = rootFolder;

            name = "ai-editor-graph-node-creation-palette";
            AddToClassList("ai-editor-graph-node-creation-palette");
            style.position = UIPosition.Absolute;
            style.width = Width;
            style.height = Height;

            searchField = new ToolbarSearchField { name = "ai-editor-graph-node-creation-search" };
            searchField.AddToClassList("ai-editor-graph-node-creation-search");
            searchField.RegisterValueChangedCallback(OnSearchChanged);
            Add(searchField);

            backButton = new Button(NavigateBack)
            {
                name = "ai-editor-graph-node-creation-back",
                focusable = false,
            };
            backButton.text = "‹";
            backButton.AddToClassList("ai-editor-graph-node-creation-back");
            titleLabel = new Label { name = "ai-editor-graph-node-creation-title", pickingMode = PickingMode.Ignore };
            titleLabel.AddToClassList("ai-editor-graph-node-creation-title");
            VisualElement header = new();
            header.AddToClassList("ai-editor-graph-node-creation-header");
            VisualElement leadingSlot = new()
            {
                name = "ai-editor-graph-node-creation-leading-slot",
            };
            leadingSlot.AddToClassList("ai-editor-graph-node-creation-header-slot");
            leadingSlot.Add(backButton);
            VisualElement trailingSlot = new()
            {
                name = "ai-editor-graph-node-creation-trailing-slot",
                pickingMode = PickingMode.Ignore,
            };
            trailingSlot.AddToClassList("ai-editor-graph-node-creation-header-slot");
            header.Add(leadingSlot);
            header.Add(titleLabel);
            header.Add(trailingSlot);
            Add(header);

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
            results.selectionChanged += OnSelectionChanged;
            results.RegisterCallback<WheelEvent>(StopWheelPropagation);
            Add(results);

            detailLabel = new Label { name = "ai-editor-graph-node-creation-detail" };
            detailLabel.AddToClassList("ai-editor-graph-node-creation-detail");
            Add(detailLabel);

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
            row.RegisterCallback<PointerMoveEvent>(OnRowPointerEnter);
            row.RegisterCallback<PointerLeaveEvent>(OnRowPointerLeave);
            return row;
        }

        private void BindRow(VisualElement element, int index)
        {
            RowElement row = (RowElement)element;
            Entry entry = visibleEntries[index];
            row.userData = index;
            row.Title.text = entry.Folder != null ? entry.Folder.Name : menuCache.GetDisplayName(entry.Type);
            row.Detail.text = entry.Folder != null ? "Browse category" : menuCache.GetTooltip(entry.Type);
            row.tooltip = entry.Folder == null ? menuCache.GetTooltip(entry.Type) : string.Empty;
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
            results.selectedIndex = index;
            Activate(visibleEntries[index]);
            evt.StopPropagation();
        }

        private void OnSelectionChanged(IEnumerable<object> selection)
        {
            object selected = selection?.FirstOrDefault();
            int index = selected is Entry entry ? visibleEntries.IndexOf(entry) : results.selectedIndex;
            UpdateDetail(index);
        }

        private void OnRowPointerEnter(PointerMoveEvent evt)
        {
            if (evt.currentTarget is RowElement row && row.userData is int index)
            {
                UpdateDetail(index);
            }
        }

        private void OnRowPointerLeave(PointerLeaveEvent evt)
        {
            UpdateDetail(results.selectedIndex);
        }

        private void OnSearchChanged(ChangeEvent<string> evt)
        {
            bool searching = !string.IsNullOrWhiteSpace(evt.newValue);
            if (searching && string.IsNullOrWhiteSpace(evt.previousValue))
            {
                searchOrigin = CaptureState();
            }
            else if (!searching && !string.IsNullOrWhiteSpace(evt.previousValue))
            {
                RestoreState(searchOrigin);
            }

            RebuildEntries();
        }

        private void NavigateBack()
        {
            if (!string.IsNullOrWhiteSpace(searchField.value))
            {
                searchField.value = string.Empty;
                return;
            }

            if (history.Count == 0)
            {
                return;
            }

            RestoreState(history.Pop());
            RebuildEntries();
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

            titleLabel.text = searching ? "Search Results" : GetFolderPath(rootFolder, currentFolder);
            backButton.style.display = searching || history.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            if (searching)
            {
                foreach (Type type in menuCache.GetCreationTypes(context).Where(type => Matches(type, query)).OrderBy(menuCache.GetDisplayName))
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
            UpdateDetail(results.selectedIndex);
        }

        private void UpdateDetail(int index)
        {
            if (detailLabel == null || index < 0 || index >= visibleEntries.Count || visibleEntries[index].Type == null)
            {
                if (detailLabel != null) detailLabel.text = string.Empty;
                return;
            }

            detailLabel.text = menuCache.GetTooltip(visibleEntries[index].Type);
        }

        private void Activate(Entry entry)
        {
            if (entry.Folder != null)
            {
                history.Push(CaptureState());
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
                if (!string.IsNullOrWhiteSpace(searchField.value) || history.Count > 0)
                {
                    NavigateBack();
                }
                else
                {
                    onClosed();
                }
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
                UpdateDetail(selectedIndex);
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

        private FolderState CaptureState()
        {
            return new FolderState(currentFolder, selectedIndex, GetScrollOffset());
        }

        private void RestoreState(FolderState state)
        {
            if (state.Folder == null) return;
            currentFolder = state.Folder;
            selectedIndex = state.SelectedIndex;
            GetResultsScrollView().scrollOffset = state.ScrollOffset;
        }

        private Vector2 GetScrollOffset()
        {
            return GetResultsScrollView().scrollOffset;
        }

        private ScrollView GetResultsScrollView()
        {
            return results.Q<ScrollView>();
        }

        private static string GetFolderPath(NodeCreationMenuFolder root, NodeCreationMenuFolder folder)
        {
            if (folder == root)
            {
                return root.Name;
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

        private readonly struct FolderState
        {
            internal FolderState(NodeCreationMenuFolder folder, int selectedIndex, Vector2 scrollOffset)
            {
                Folder = folder;
                SelectedIndex = selectedIndex;
                ScrollOffset = scrollOffset;
            }

            internal NodeCreationMenuFolder Folder { get; }
            internal int SelectedIndex { get; }
            internal Vector2 ScrollOffset { get; }
        }

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
                Add(Title);
                Add(Detail);
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
