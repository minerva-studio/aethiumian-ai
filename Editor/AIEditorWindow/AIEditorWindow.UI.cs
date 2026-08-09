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
        private ToolbarToggle graphTab;
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
        private VisualElement graphHost;
        private GraphEditorModule graphModule;
        private bool shellCallbacksRegistered;

        /// <summary>
        /// Builds the UI Toolkit shell that hosts the existing IMGUI editor pages.
        /// </summary>
        public void CreateGUI()
        {
            UnregisterShellCallbacks();
            rootVisualElement.Clear();
            Initialize();

            if (shellAsset == null)
            {
                throw new InvalidOperationException("AI Editor UI configuration error: the shell UXML default reference is missing.");
            }

            shellAsset.CloneTree(rootVisualElement);

            shellRoot = RequireElement<VisualElement>(rootVisualElement, "ai-editor-shell");
            QueryShellControls();
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
            UnregisterShellCallbacks();
            if (undoEventRegistered)
            {
                Undo.undoRedoPerformed -= Refresh;
                undoEventRegistered = false;
            }
        }

        /// <summary>
        /// Queries all static controls declared by the UXML shell.
        /// </summary>
        private void QueryShellControls()
        {
            Toolbar toolbar = RequireElement<Toolbar>(shellRoot, "ai-editor-toolbar");
            nodesTab = RequireElement<ToolbarToggle>(toolbar, "ai-editor-nodes-tab");
            graphTab = RequireElement<ToolbarToggle>(toolbar, "ai-editor-graph-tab");
            variablesTab = RequireElement<ToolbarToggle>(toolbar, "ai-editor-variables-tab");
            propertiesTab = RequireElement<ToolbarToggle>(toolbar, "ai-editor-properties-tab");
            upgradeButton = RequireElement<ToolbarButton>(toolbar, "ai-editor-upgrade-button");
            clipboardMenu = RequireElement<ToolbarMenu>(toolbar, "ai-editor-clipboard-menu");
            refreshButton = RequireElement<ToolbarButton>(toolbar, "ai-editor-refresh-button");
            settingsButton = RequireElement<ToolbarButton>(toolbar, "ai-editor-settings-button");
            maintenanceMenu = RequireElement<ToolbarMenu>(toolbar, "ai-editor-maintenance-menu");
            lockToggle = RequireElement<ToolbarToggle>(toolbar, "ai-editor-lock-toggle");
            lockIcon = RequireElement<Image>(lockToggle, "ai-editor-lock-icon");
            treeField = RequireElement<ObjectField>(shellRoot, "ai-editor-tree-field");

            // UXML owns the selector's placement and label; the concrete asset type remains a code contract.
            treeField.objectType = typeof(BehaviourTreeData);
            treeField.allowSceneObjects = false;
            if (treeField.objectType != typeof(BehaviourTreeData) || treeField.allowSceneObjects)
            {
                throw new InvalidOperationException("AI Editor UI configuration error: the Behaviour Tree ObjectField has invalid configuration.");
            }
        }

        /// <summary>
        /// Finds a required named control and reports a configuration error when it is absent.
        /// </summary>
        /// <typeparam name="T">The expected UI Toolkit element type.</typeparam>
        /// <param name="root">The element below which the control must exist.</param>
        /// <param name="name">The stable UXML element name.</param>
        /// <returns>The required control.</returns>
        private static T RequireElement<T>(VisualElement root, string name) where T : VisualElement
        {
            T element = root.Q<T>(name);
            if (element == null)
            {
                throw new InvalidOperationException($"AI Editor UI configuration error: UXML element '{name}' of type '{typeof(T).Name}' is missing.");
            }

            return element;
        }

        /// <summary>
        /// Creates the three IMGUI page hosts used during the migration period.
        /// </summary>
        private void BuildContentHost()
        {
            contentHost = RequireElement<VisualElement>(shellRoot, "ai-editor-content-host");
            graphHost = RequireElement<VisualElement>(contentHost, "ai-editor-graph-host");
            contentHost.Clear();
            contentHost.Add(graphHost);

            nodesContainer = CreateIMGUIContainer("ai-editor-nodes-pane", DrawNodesPane);
            variablesContainer = CreateIMGUIContainer("ai-editor-variables-pane", DrawVariablesPane);
            propertiesContainer = CreateIMGUIContainer("ai-editor-properties-pane", DrawPropertiesPane);

            contentHost.Add(nodesContainer);
            contentHost.Add(variablesContainer);
            contentHost.Add(propertiesContainer);

            graphModule ??= new GraphEditorModule(this);
            graphModule.Attach(graphHost);
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
            container.AddToClassList("ai-editor-imgui-pane");
            return container;
        }

        /// <summary>
        /// Registers callbacks for shell controls exactly once per visual tree.
        /// </summary>
        private void RegisterShellCallbacks()
        {
            if (shellCallbacksRegistered)
            {
                return;
            }

            treeField.RegisterValueChangedCallback(OnTreeFieldChanged);
            nodesTab.RegisterValueChangedCallback(OnNodesTabChanged);
            graphTab.RegisterValueChangedCallback(OnGraphTabChanged);
            variablesTab.RegisterValueChangedCallback(OnVariablesTabChanged);
            propertiesTab.RegisterValueChangedCallback(OnPropertiesTabChanged);
            upgradeButton.clicked += UpradeAllNode;
            refreshButton.clicked += Refresh;
            settingsButton.clicked += AIEditorPreferenceProvider.OpenPreferences;
            lockToggle.RegisterValueChangedCallback(OnLockChanged);
            BuildClipboardMenu();
            BuildMaintenanceMenu(maintenanceMenu.menu);
            shellRoot.RegisterCallback<GeometryChangedEvent>(OnShellGeometryChanged);
            shellCallbacksRegistered = true;
        }

        /// <summary>
        /// Removes callbacks from the current visual tree before it is rebuilt or destroyed.
        /// </summary>
        private void UnregisterShellCallbacks()
        {
            if (treeField != null)
            {
                treeField.UnregisterValueChangedCallback(OnTreeFieldChanged);
            }

            if (nodesTab != null)
            {
                nodesTab.UnregisterValueChangedCallback(OnNodesTabChanged);
            }

            if (graphTab != null)
            {
                graphTab.UnregisterValueChangedCallback(OnGraphTabChanged);
            }

            if (variablesTab != null)
            {
                variablesTab.UnregisterValueChangedCallback(OnVariablesTabChanged);
            }

            if (propertiesTab != null)
            {
                propertiesTab.UnregisterValueChangedCallback(OnPropertiesTabChanged);
            }

            if (lockToggle != null)
            {
                lockToggle.UnregisterValueChangedCallback(OnLockChanged);
            }

            if (upgradeButton != null)
            {
                upgradeButton.clicked -= UpradeAllNode;
            }

            if (refreshButton != null)
            {
                refreshButton.clicked -= Refresh;
            }

            if (settingsButton != null)
            {
                settingsButton.clicked -= AIEditorPreferenceProvider.OpenPreferences;
            }

            if (shellRoot != null)
            {
                shellRoot.UnregisterCallback<GeometryChangedEvent>(OnShellGeometryChanged);
            }

            shellCallbacksRegistered = false;
        }

        /// <summary>
        /// Applies a tree selected through the shell object field.
        /// </summary>
        private void OnTreeFieldChanged(ChangeEvent<UnityEngine.Object> evt)
        {
            if (!this)
            {
                return;
            }

            SetSelectedTree(evt.newValue as BehaviourTreeData);
        }

        /// <summary>
        /// Selects the Nodes page when its toolbar toggle changes.
        /// </summary>
        private void OnNodesTabChanged(ChangeEvent<bool> evt)
        {
            if (this && evt.newValue)
            {
                SelectWindow(Window.Nodes);
            }
        }

        /// <summary>
        /// Selects the Graph page when its toolbar toggle changes.
        /// </summary>
        private void OnGraphTabChanged(ChangeEvent<bool> evt)
        {
            if (this && evt.newValue)
            {
                SelectWindow(Window.Graph);
            }
        }

        /// <summary>
        /// Selects the Variables page when its toolbar toggle changes.
        /// </summary>
        private void OnVariablesTabChanged(ChangeEvent<bool> evt)
        {
            if (this && evt.newValue)
            {
                SelectWindow(Window.Variables);
            }
        }

        /// <summary>
        /// Selects the Properties page when its toolbar toggle changes.
        /// </summary>
        private void OnPropertiesTabChanged(ChangeEvent<bool> evt)
        {
            if (this && evt.newValue)
            {
                SelectWindow(Window.Properties);
            }
        }

        /// <summary>
        /// Updates selection locking when the lock toolbar toggle changes.
        /// </summary>
        private void OnLockChanged(ChangeEvent<bool> evt)
        {
            if (!this)
            {
                return;
            }

            selectionLocked = evt.newValue;
            RefreshShell();
        }

        /// <summary>
        /// Recomputes compact toolbar labels after shell geometry changes.
        /// </summary>
        private void OnShellGeometryChanged(GeometryChangedEvent evt)
        {
            if (this)
            {
                UpdateToolbarLabels();
            }
        }

        /// <summary>
        /// Populates the shared clipboard actions on the declarative toolbar menu.
        /// </summary>
        private void BuildClipboardMenu()
        {
            clipboardMenu.menu.AppendAction("Clipboard status", _ => { }, _ => DropdownMenuAction.Status.Disabled);
            clipboardMenu.menu.AppendSeparator();
            clipboardMenu.menu.AppendAction("Clear Clipboard", _ => Clipboard.Clear(), _ => Clipboard.HasContent
                ? DropdownMenuAction.Status.Normal
                : DropdownMenuAction.Status.Disabled);
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
            graphTab?.SetValueWithoutNotify(window == Window.Graph);
            variablesTab?.SetValueWithoutNotify(window == Window.Variables);
            propertiesTab?.SetValueWithoutNotify(window == Window.Properties);
            lockToggle?.SetValueWithoutNotify(selectionLocked);
            UpdateLockIcon();

            SetContainerDisplay(nodesContainer, window == Window.Nodes);
            SetContainerDisplay(graphHost, window == Window.Graph);
            SetContainerDisplay(variablesContainer, window == Window.Variables);
            SetContainerDisplay(propertiesContainer, window == Window.Properties);
            contentHost?.SetEnabled(editorSetting == null || !editorSetting.safeMode);

            graphModule?.UpdateView();

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
