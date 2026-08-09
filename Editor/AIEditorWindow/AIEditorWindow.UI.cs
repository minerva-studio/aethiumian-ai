using System;
using Aethiumian.AI.Nodes;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Aethiumian.AI.Editor
{
    public partial class AIEditorWindow
    {
        private VisualElement shellRoot;
        private VisualElement contentHost;
        private ObjectField treeField;
        private ToolbarToggle nodesTab;
        private ToolbarToggle variablesTab;
        private ToolbarToggle propertiesTab;
        private ToolbarToggle lockToggle;
        private Image lockIcon;
        private ToolbarButton upgradeButton;
        private ToolbarMenu clipboardMenu;
        private ToolbarButton refreshButton;
        private ToolbarButton settingsButton;
        private ToolbarMenu maintenanceMenu;
        private IMGUIContainer nodesContainer;
        private IMGUIContainer variablesContainer;
        private IMGUIContainer propertiesContainer;

        /// <summary>
        /// Builds the UI Toolkit shell that hosts the existing IMGUI editor pages.
        /// </summary>
        public void CreateGUI()
        {
            rootVisualElement.Clear();
            Initialize();

            shellRoot = new VisualElement { name = "ai-editor-shell" };
            shellRoot.style.flexGrow = 1;
            rootVisualElement.Add(shellRoot);

            BuildToolbar();
            BuildTreeSelection();
            BuildContentHost();
            RegisterShellCallbacks();
            RefreshShell();
        }

        /// <summary>
        /// Registers the editor-wide undo callback for the lifetime of the window.
        /// </summary>
        private void OnEnable()
        {
            if (!undoEventRegistered)
            {
                undoEventRegistered = true;
                Undo.undoRedoPerformed += Refresh;
            }
        }

        /// <summary>
        /// Removes the editor-wide undo callback when the window is disabled.
        /// </summary>
        private void OnDisable()
        {
            if (undoEventRegistered)
            {
                Undo.undoRedoPerformed -= Refresh;
                undoEventRegistered = false;
            }
        }

        /// <summary>
        /// Creates the native toolbar controls used by the shell.
        /// </summary>
        private void BuildToolbar()
        {
            Toolbar toolbar = new() { name = "ai-editor-toolbar" };

            nodesTab = CreateTab("Nodes", "ai-editor-nodes-tab", Window.Nodes);
            variablesTab = CreateTab("Variables", "ai-editor-variables-tab", Window.Variables);
            propertiesTab = CreateTab("Properties", "ai-editor-properties-tab", Window.Properties);
            toolbar.Add(nodesTab);
            toolbar.Add(variablesTab);
            toolbar.Add(propertiesTab);

            // Keep page navigation on the left and maintenance actions on the right,
            // matching the layout of the legacy IMGUI toolbar.
            VisualElement toolbarSpacer = new() { name = "ai-editor-toolbar-spacer" };
            toolbarSpacer.style.flexGrow = 1;
            toolbar.Add(toolbarSpacer);

            upgradeButton = new ToolbarButton(UpradeAllNode)
            {
                name = "ai-editor-upgrade-button",
            };
            toolbar.Add(upgradeButton);

            clipboardMenu = new ToolbarMenu
            {
                name = "ai-editor-clipboard-menu",
                tooltip = "Show shared node clipboard status.",
            };
            clipboardMenu.menu.AppendAction("Clipboard status", _ => { }, _ => DropdownMenuAction.Status.Disabled);
            clipboardMenu.menu.AppendSeparator();
            clipboardMenu.menu.AppendAction("Clear Clipboard", _ => Clipboard.Clear(), _ => Clipboard.HasContent
                ? DropdownMenuAction.Status.Normal
                : DropdownMenuAction.Status.Disabled);
            toolbar.Add(clipboardMenu);

            refreshButton = new ToolbarButton(Refresh)
            {
                name = "ai-editor-refresh-button",
            };
            toolbar.Add(refreshButton);

            settingsButton = new ToolbarButton(AIEditorPreferenceProvider.OpenPreferences)
            {
                name = "ai-editor-settings-button",
            };
            toolbar.Add(settingsButton);

            maintenanceMenu = new ToolbarMenu
            {
                name = "ai-editor-maintenance-menu",
                text = "Maintenance",
            };
            BuildMaintenanceMenu(maintenanceMenu.menu);
            toolbar.Add(maintenanceMenu);

            lockToggle = new ToolbarToggle
            {
                name = "ai-editor-lock-toggle",
                tooltip = "Lock the selected behaviour tree.",
            };
            lockToggle.style.width = 24f;
            lockIcon = new Image
            {
                name = "ai-editor-lock-icon",
                pickingMode = PickingMode.Ignore,
                scaleMode = ScaleMode.ScaleToFit,
            };
            lockIcon.style.width = 16f;
            lockIcon.style.height = 16f;
            lockToggle.Add(lockIcon);
            toolbar.Add(lockToggle);

            shellRoot.Add(toolbar);
        }

        /// <summary>
        /// Creates the full-width behaviour tree selector below the toolbar.
        /// </summary>
        private void BuildTreeSelection()
        {
            VisualElement selectionRow = new() { name = "ai-editor-tree-selection" };
            selectionRow.style.flexDirection = FlexDirection.Row;

            treeField = new ObjectField("Behaviour Tree")
            {
                name = "ai-editor-tree-field",
                objectType = typeof(BehaviourTreeData),
                allowSceneObjects = false,
            };
            treeField.style.flexGrow = 1;
            selectionRow.Add(treeField);

            shellRoot.Add(selectionRow);
        }

        /// <summary>
        /// Creates one mutually exclusive page tab.
        /// </summary>
        /// <param name="label">The full tab label.</param>
        /// <param name="name">The UI Toolkit element name.</param>
        /// <param name="targetWindow">The window mode selected by the tab.</param>
        /// <returns>The configured tab toggle.</returns>
        private ToolbarToggle CreateTab(string label, string name, Window targetWindow)
        {
            ToolbarToggle tab = new()
            {
                name = name,
                text = label,
                tooltip = $"Show {label.ToLowerInvariant()}.",
            };
            tab.userData = targetWindow;
            return tab;
        }

        /// <summary>
        /// Creates the three IMGUI page hosts used during the migration period.
        /// </summary>
        private void BuildContentHost()
        {
            contentHost = new VisualElement { name = "ai-editor-content-host" };
            contentHost.style.flexGrow = 1;
            contentHost.style.flexDirection = FlexDirection.Column;

            nodesContainer = CreateIMGUIContainer("ai-editor-nodes-pane", DrawNodesPane);
            variablesContainer = CreateIMGUIContainer("ai-editor-variables-pane", DrawVariablesPane);
            propertiesContainer = CreateIMGUIContainer("ai-editor-properties-pane", DrawPropertiesPane);

            contentHost.Add(nodesContainer);
            contentHost.Add(variablesContainer);
            contentHost.Add(propertiesContainer);
            shellRoot.Add(contentHost);
        }

        /// <summary>
        /// Creates an IMGUI container with a flexible content area.
        /// </summary>
        /// <param name="name">The UI Toolkit element name.</param>
        /// <param name="handler">The legacy IMGUI drawing callback.</param>
        /// <returns>The configured container.</returns>
        private static IMGUIContainer CreateIMGUIContainer(string name, System.Action handler)
        {
            IMGUIContainer container = new(handler)
            {
                name = name,
            };
            container.style.flexGrow = 1;
            return container;
        }

        /// <summary>
        /// Registers callbacks for shell controls exactly once per visual tree.
        /// </summary>
        private void RegisterShellCallbacks()
        {
            treeField.RegisterValueChangedCallback(evt => SetSelectedTree(evt.newValue as BehaviourTreeData));
            nodesTab.RegisterValueChangedCallback(_ => SelectWindow(Window.Nodes));
            variablesTab.RegisterValueChangedCallback(_ => SelectWindow(Window.Variables));
            propertiesTab.RegisterValueChangedCallback(_ => SelectWindow(Window.Properties));
            lockToggle.RegisterValueChangedCallback(evt =>
            {
                selectionLocked = evt.newValue;
                RefreshShell();
            });
            shellRoot.RegisterCallback<GeometryChangedEvent>(_ => UpdateToolbarLabels());
        }

        /// <summary>
        /// Updates shell controls after tree, tab, settings, or selection state changes.
        /// </summary>
        private void RefreshShell()
        {
            if (shellRoot == null)
            {
                return;
            }

            if (!Enum.IsDefined(typeof(Window), window))
            {
                // Serialized value 1 belonged to the removed experimental Graph page.
                window = Window.Nodes;
            }

            treeField?.SetValueWithoutNotify(tree);
            nodesTab?.SetValueWithoutNotify(window == Window.Nodes);
            variablesTab?.SetValueWithoutNotify(window == Window.Variables);
            propertiesTab?.SetValueWithoutNotify(window == Window.Properties);
            lockToggle?.SetValueWithoutNotify(selectionLocked);
            UpdateLockIcon();

            SetContainerDisplay(nodesContainer, window == Window.Nodes);
            SetContainerDisplay(variablesContainer, window == Window.Variables);
            SetContainerDisplay(propertiesContainer, window == Window.Properties);
            contentHost?.SetEnabled(editorSetting == null || !editorSetting.safeMode);

            UpdateToolbarLabels();
            GetActiveContainer()?.MarkDirtyRepaint();
        }

        /// <summary>
        /// Selects one of the supported editor pages and preserves module state.
        /// </summary>
        /// <param name="targetWindow">The page to display.</param>
        private void SelectWindow(Window targetWindow)
        {
            if (!Enum.IsDefined(typeof(Window), targetWindow))
            {
                targetWindow = Window.Nodes;
            }

            window = targetWindow;
            RefreshShell();
        }

        /// <summary>
        /// Sets visibility for one page host without destroying its IMGUI state.
        /// </summary>
        /// <param name="container">The page host.</param>
        /// <param name="visible">Whether the page should be visible.</param>
        private static void SetContainerDisplay(VisualElement container, bool visible)
        {
            if (container != null)
            {
                container.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        /// <summary>
        /// Draws the legacy nodes page inside its UI Toolkit host.
        /// </summary>
        private void DrawNodesPane()
        {
            GetAllNode();
            treeWindow?.DrawTree();
            RepaintIfGUIChanged(nodesContainer);
        }

        /// <summary>
        /// Draws the legacy variables page inside its UI Toolkit host.
        /// </summary>
        private void DrawVariablesPane()
        {
            variableTable?.DrawVariableTable();
            RepaintIfGUIChanged(variablesContainer);
        }

        /// <summary>
        /// Draws the legacy properties page inside its UI Toolkit host.
        /// </summary>
        private void DrawPropertiesPane()
        {
            DrawProperties();
            RepaintIfGUIChanged(propertiesContainer);
        }

        /// <summary>
        /// Repaints the window when an IMGUI page changed authored data or view state.
        /// </summary>
        /// <param name="container">The active IMGUI container.</param>
        private void RepaintIfGUIChanged(IMGUIContainer container)
        {
            if (GUI.changed)
            {
                container?.MarkDirtyRepaint();
                Repaint();
            }
        }

        /// <summary>
        /// Updates toolbar labels for the current window width and state.
        /// </summary>
        private void UpdateToolbarLabels()
        {
            if (shellRoot == null)
            {
                return;
            }

            float width = shellRoot.resolvedStyle.width;
            if (float.IsNaN(width) || width <= 0f)
            {
                width = position.width;
            }

            bool compact = UseCompactToolbar(width);
            variablesTab.text = compact ? "Vars" : "Variables";
            propertiesTab.text = compact ? "Props" : "Properties";
            refreshButton.text = GetRefreshButtonContent(compact).text;
            settingsButton.text = GetSettingsButtonContent(compact).text;
            int upgradableNodeCount = CountUpgradableNodes();
            upgradeButton.text = upgradableNodeCount > 0 ? GetUpgradeButtonContent(upgradableNodeCount, compact).text : string.Empty;
            upgradeButton.style.display = upgradableNodeCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            clipboardMenu.text = GetClipboardButtonContent(Clipboard.Count, Clipboard.HasContent, compact, Clipboard.GetStatusText()).text;
            clipboardMenu.SetEnabled(Clipboard.HasContent);
            lockToggle.SetValueWithoutNotify(selectionLocked);
            UpdateLockIcon();
        }

        /// <summary>
        /// Updates the lock toggle icon to match the current selection-following state.
        /// </summary>
        private void UpdateLockIcon()
        {
            if (lockIcon != null)
            {
                lockIcon.image = EditorGUIUtility.IconContent(selectionLocked ? "LockIcon-On" : "LockIcon").image;
            }
        }

        /// <summary>
        /// Appends maintenance actions while keeping their enabled state live.
        /// </summary>
        /// <param name="menu">The UI Toolkit maintenance menu.</param>
        private void BuildMaintenanceMenu(DropdownMenu menu)
        {
            menu.AppendAction("Refresh", _ => Refresh());
            menu.AppendSeparator();
            menu.AppendAction("Open Containing Folder", _ => OpenTreeContainingFolder(), _ => tree
                ? DropdownMenuAction.Status.Normal
                : DropdownMenuAction.Status.Disabled);
            menu.AppendAction("Reveal Asset in Explorer", _ => RevealTreeAssetInExplorer(), _ => tree
                ? DropdownMenuAction.Status.Normal
                : DropdownMenuAction.Status.Disabled);
            menu.AppendAction("Open In External Editor", _ => OpenTreeInExternalEditor(), _ => tree
                ? DropdownMenuAction.Status.Normal
                : DropdownMenuAction.Status.Disabled);
            menu.AppendAction("Open In Unity Inspector", _ => OpenTreeInUnityInspector(), _ => tree
                ? DropdownMenuAction.Status.Normal
                : DropdownMenuAction.Status.Disabled);
            menu.AppendSeparator();
            menu.AppendAction("Upgrade All", _ => UpradeAllNode(), _ => CountUpgradableNodes() > 0
                ? DropdownMenuAction.Status.Normal
                : DropdownMenuAction.Status.Disabled);
            menu.AppendAction("Clear All Null Reference", _ =>
            {
                foreach (TreeNode node in AllNodes)
                {
                    NodeFactory.FillNull(node);
                }
            }, _ => tree ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            menu.AppendAction("Fix Null Parent Issue", _ => tree.Relink(), _ => tree
                ? DropdownMenuAction.Status.Normal
                : DropdownMenuAction.Status.Disabled);
            menu.AppendAction("Delete All Unused Nodes", _ => DeleteAllUnusedNodes(), _ => tree && GetUnusedNodes().Count > 0
                ? DropdownMenuAction.Status.Normal
                : DropdownMenuAction.Status.Disabled);
            menu.AppendSeparator();
            menu.AppendAction("Debug", _ =>
            {
                editorSetting.debugMode = !editorSetting.debugMode;
                AIEditorSetting.SaveSettings(editorSetting);
            }, _ => editorSetting != null && editorSetting.debugMode
                ? DropdownMenuAction.Status.Checked
                : DropdownMenuAction.Status.Normal);
        }

        /// <summary>
        /// Returns the currently visible IMGUI container.
        /// </summary>
        /// <returns>The active page container, or null before shell creation.</returns>
        private IMGUIContainer GetActiveContainer()
        {
            return window switch
            {
                Window.Nodes => nodesContainer,
                Window.Variables => variablesContainer,
                Window.Properties => propertiesContainer,
                _ => null,
            };
        }
    }
}
