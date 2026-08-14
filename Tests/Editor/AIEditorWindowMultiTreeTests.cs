using Aethiumian.AI.Editor;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Aethiumian.AI.Tests
{
    public sealed class AIEditorWindowMultiTreeTests
    {
        private readonly List<AIEditorWindow> openedWindows = new();
        private readonly List<AIInspector> openedInspectors = new();
        private readonly List<BehaviourTreeData> createdTrees = new();
        private readonly List<GameObject> createdGameObjects = new();
        private readonly HashSet<int> baselineWindowIds = new();

        [SetUp]
        public void SetUp()
        {
            Selection.activeObject = null;
            baselineWindowIds.Clear();
            foreach (AIEditorWindow window in Resources.FindObjectsOfTypeAll<AIEditorWindow>())
            {
                if (window)
                {
                    baselineWindowIds.Add(window.GetInstanceID());
                }
            }
        }

        [TearDown]
        public void TearDown()
        {
            // Close only the windows created through the temporary trees used by these tests.
            foreach (AIEditorWindow window in openedWindows.Where(window => window).Distinct())
            {
                window.Close();
            }

            foreach (AIInspector inspector in openedInspectors.Where(inspector => inspector).Distinct())
            {
                inspector.Close();
            }

            foreach (BehaviourTreeData tree in createdTrees.Where(tree => tree))
            {
                UnityEngine.Object.DestroyImmediate(tree);
            }

            foreach (GameObject gameObject in createdGameObjects.Where(gameObject => gameObject))
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }

            Selection.activeObject = null;
            openedWindows.Clear();
            openedInspectors.Clear();
            createdTrees.Clear();
            createdGameObjects.Clear();
            baselineWindowIds.Clear();
        }

        [Test]
        public void ShowWindow_SameTree_ReusesExistingWindow()
        {
            BehaviourTreeData tree = CreateTree("Shared Tree");

            AIEditorWindow firstWindow = Track(AIEditorWindow.ShowWindow(tree));
            AIEditorWindow secondWindow = Track(AIEditorWindow.ShowWindow(tree));

            Assert.That(secondWindow, Is.SameAs(firstWindow));
            Assert.That(AIEditorWindow.TryGetOpenWindow(tree, out AIEditorWindow foundWindow), Is.True);
            Assert.That(foundWindow, Is.SameAs(firstWindow));
        }

        [Test]
        public void ShowWindow_DifferentTrees_OpensSeparateWindows()
        {
            BehaviourTreeData firstTree = CreateTree("First Tree");
            BehaviourTreeData secondTree = CreateTree("Second Tree");

            AIEditorWindow firstWindow = Track(AIEditorWindow.ShowWindow(firstTree));
            AIEditorWindow secondWindow = Track(AIEditorWindow.ShowWindow(secondTree));

            Assert.That(secondWindow, Is.Not.SameAs(firstWindow));
            Assert.That(firstWindow.tree, Is.SameAs(firstTree));
            Assert.That(secondWindow.tree, Is.SameAs(secondTree));
        }

        /// <summary>Verifies closing test-created windows restores the pre-test window set.</summary>
        [UnityTest]
        public IEnumerator ClosingShownWindows_RestoresPreTestWindowBaseline()
        {
            AIEditorWindow first = Track(AIEditorWindow.ShowWindow(CreateTree("Cleanup First")));
            AIEditorWindow second = Track(AIEditorWindow.ShowWindow(CreateTree("Cleanup Second")));
            yield return null;

            first.Close();
            second.Close();
            yield return null;

            AIEditorWindow[] leaked = Resources.FindObjectsOfTypeAll<AIEditorWindow>()
                .Where(window => window && !baselineWindowIds.Contains(window.GetInstanceID()))
                .ToArray();
            Assert.That(leaked, Is.Empty);
        }

        [Test]
        public void Clipboard_MultipleWindows_ShareGlobalClipboard()
        {
            BehaviourTreeData firstTree = CreateTree("Clipboard First Tree");
            BehaviourTreeData secondTree = CreateTree("Clipboard Second Tree");

            AIEditorWindow firstWindow = Track(AIEditorWindow.ShowWindow(firstTree));
            AIEditorWindow secondWindow = Track(AIEditorWindow.ShowWindow(secondTree));

            Assert.That(firstWindow.Clipboard, Is.SameAs(AIEditorWindow.SharedClipboard));
            Assert.That(secondWindow.Clipboard, Is.SameAs(AIEditorWindow.SharedClipboard));
            Assert.That(secondWindow.Clipboard, Is.SameAs(firstWindow.Clipboard));
        }

        [Test]
        public void ShowWindow_EmptyEditorWindow_UsesEditorTitleIcon()
        {
            AIEditorWindow window = Track(AIEditorWindow.ShowWindow());
            Texture2D editorIcon = AIEditorTitleContent.LoadIcon(AIEditorTitleContent.AI_EDITOR_ICON_GUID);

            Assert.That(window.titleContent.text, Is.EqualTo("AI Editor"));
            Assert.That(window.titleContent.image, Is.SameAs(editorIcon));
        }

        [Test]
        public void ShowWindow_TreeEditorWindow_UsesTreeTitleAndEditorTitleIcon()
        {
            BehaviourTreeData tree = CreateTree("Icon Tree");

            AIEditorWindow window = Track(AIEditorWindow.ShowWindow(tree));
            Texture2D editorIcon = AIEditorTitleContent.LoadIcon(AIEditorTitleContent.AI_EDITOR_ICON_GUID);

            Assert.That(window.titleContent.text, Is.EqualTo(tree.name));
            Assert.That(window.titleContent.image, Is.SameAs(editorIcon));
        }

        [Test]
        public void ShowWindow_AIInspector_UsesInspectorTitleIcon()
        {
            AIEditorWindow editorWindow = Track(AIEditorWindow.ShowWindow());
            AIInspector inspector = Track(AIInspector.ShowWindow());
            Texture2D inspectorIcon = AIEditorTitleContent.LoadIcon(AIEditorTitleContent.AI_INSPECTOR_ICON_GUID);

            Assert.That(inspector.titleContent.text, Is.EqualTo("AI Inspector"));
            Assert.That(inspector.titleContent.image, Is.SameAs(inspectorIcon));
            Assert.That(inspector.titleContent.image, Is.Not.SameAs(editorWindow.titleContent.image));
        }

        [Test]
        public void FollowUnitySelection_UnlockedWindow_UsesSelectedTreeAsset()
        {
            BehaviourTreeData tree = CreateTree("Selected Asset Tree");
            AIEditorWindow window = Track(AIEditorWindow.ShowWindow());

            Selection.activeObject = tree;
            window.FollowUnitySelection();

            Assert.That(window.tree, Is.SameAs(tree));
        }

        [Test]
        public void FollowUnitySelection_UnlockedWindow_UsesSelectedGameObjectAIData()
        {
            BehaviourTreeData tree = CreateTree("Selected GameObject Tree");
            GameObject gameObject = CreateGameObjectWithTree("AI Host", tree);
            AIEditorWindow window = Track(AIEditorWindow.ShowWindow());

            Selection.activeObject = gameObject;
            window.FollowUnitySelection();

            Assert.That(window.tree, Is.SameAs(tree));
        }

        [Test]
        public void FollowUnitySelection_LockedWindow_KeepsCurrentTree()
        {
            BehaviourTreeData firstTree = CreateTree("Locked Tree");
            BehaviourTreeData secondTree = CreateTree("Ignored Selection Tree");
            AIEditorWindow window = Track(AIEditorWindow.ShowWindow(firstTree));
            window.SelectionLocked = true;

            Selection.activeObject = secondTree;
            window.FollowUnitySelection();

            Assert.That(window.tree, Is.SameAs(firstTree));
        }

        [Test]
        public void FollowUnitySelection_InvalidSelection_KeepsCurrentTree()
        {
            BehaviourTreeData tree = CreateTree("Current Tree");
            GameObject unrelatedObject = CreateGameObject("Unrelated Object");
            AIEditorWindow window = Track(AIEditorWindow.ShowWindow(tree));

            Selection.activeObject = unrelatedObject;
            window.FollowUnitySelection();

            Assert.That(window.tree, Is.SameAs(tree));
        }

        [Test]
        public void ShowWindow_EmptyEditorWindow_AppliesMinimumSize()
        {
            AIEditorWindow window = Track(AIEditorWindow.ShowWindow());

            Assert.That(window.minSize.x, Is.GreaterThanOrEqualTo(760f));
            Assert.That(window.minSize.y, Is.GreaterThanOrEqualTo(420f));
        }

        [Test]
        public void CreateGUI_ShellContainsFourPagesAndNativeGraphHost()
        {
            AIEditorWindow window = Track(AIEditorWindow.ShowWindow());
            window.CreateGUI();

            VisualElement shell = window.rootVisualElement.Q<VisualElement>("ai-editor-shell");
            Assert.That(shell, Is.Not.Null);
            Assert.That(shell.ClassListContains("ai-editor-shell"), Is.True);
            ToolbarToggle nodesTab = window.rootVisualElement.Q<ToolbarToggle>("ai-editor-nodes-tab");
            ToolbarToggle graphTab = window.rootVisualElement.Q<ToolbarToggle>("ai-editor-graph-tab");
            ToolbarToggle variablesTab = window.rootVisualElement.Q<ToolbarToggle>("ai-editor-variables-tab");
            ToolbarToggle propertiesTab = window.rootVisualElement.Q<ToolbarToggle>("ai-editor-properties-tab");
            Assert.That(nodesTab, Is.Not.Null);
            Assert.That(graphTab, Is.Not.Null);
            Assert.That(variablesTab, Is.Not.Null);
            Assert.That(propertiesTab, Is.Not.Null);
            Assert.That(string.IsNullOrEmpty(nodesTab.label), Is.True);
            Assert.That(string.IsNullOrEmpty(graphTab.label), Is.True);
            Assert.That(string.IsNullOrEmpty(variablesTab.label), Is.True);
            Assert.That(string.IsNullOrEmpty(propertiesTab.label), Is.True);
            Assert.That(window.rootVisualElement.Q<IMGUIContainer>("ai-editor-nodes-pane"), Is.Not.Null);
            Assert.That(window.rootVisualElement.Q<IMGUIContainer>("ai-editor-variables-pane"), Is.Not.Null);
            Assert.That(window.rootVisualElement.Q<IMGUIContainer>("ai-editor-properties-pane"), Is.Not.Null);
            Assert.That(window.rootVisualElement.Q<VisualElement>("ai-editor-graph-host"), Is.Not.Null);
            Assert.That(window.rootVisualElement.Q<GraphCanvasElement>("ai-editor-graph-canvas"), Is.Not.Null);
            Assert.That(window.rootVisualElement.Q<IMGUIContainer>("ai-editor-graph-inspector-imgui"), Is.Not.Null);
        }

        /// <summary>Verifies the Graph shell uses one global toolbar and canvas-local contextual tools.</summary>
        [Test]
        public void CreateGUI_GraphShellUsesDeclaredHierarchyAndSingleRuntimeMounts()
        {
            AIEditorWindow window = Track(AIEditorWindow.ShowWindow());
            window.CreateGUI();

            VisualElement graphHost = window.rootVisualElement.Q<VisualElement>("ai-editor-graph-host");
            VisualElement body = graphHost.Q<VisualElement>("ai-editor-graph-body");
            VisualElement canvasHost = body.Q<VisualElement>("ai-editor-graph-canvas-host");
            VisualElement splitter = body.Q<VisualElement>("ai-editor-graph-inspector-splitter");
            VisualElement inspector = body.Q<VisualElement>("ai-editor-graph-inspector");
            VisualElement inspectorContent = inspector.Q<VisualElement>("ai-editor-graph-inspector-content-host");

            Assert.That(body.parent, Is.SameAs(graphHost));
            Assert.That(canvasHost.parent, Is.SameAs(body));
            Assert.That(splitter.parent, Is.SameAs(body));
            Assert.That(inspector.parent, Is.SameAs(body));
            Assert.That(inspectorContent.parent, Is.SameAs(inspector));
            Assert.That(graphHost.Q<Toolbar>("ai-editor-graph-toolbar"), Is.Null);
            GraphCanvasElement canvas = canvasHost.Q<GraphCanvasElement>();
            Assert.That(canvas.Q<VisualElement>("ai-editor-graph-view-options"), Is.Not.Null);
            Assert.That(canvas.Q<Button>("ai-editor-graph-view-options-fit-all"), Is.Not.Null);
            Assert.That(canvas.Q<Button>("ai-editor-graph-view-options-frame-selected"), Is.Not.Null);
            Assert.That(canvas.Q<Button>("ai-editor-graph-view-options-auto-layout"), Is.Not.Null);
            Assert.That(canvas.Q<Button>("ai-editor-graph-view-options-raw-references"), Is.Not.Null);
            Assert.That(canvas.Q<Button>("ai-editor-graph-view-options-inspector"), Is.Not.Null);
            Assert.That(canvasHost.Query<GraphCanvasElement>().ToList(), Has.Count.EqualTo(1));
            Assert.That(inspectorContent.Query<IMGUIContainer>().ToList(), Has.Count.EqualTo(1));
        }

        [Test]
        public void CreateGUI_ShellUsesDefaultReference()
        {
            AIEditorWindow window = Track(AIEditorWindow.ShowWindow());
            SerializedObject serializedWindow = new(window);
            VisualTreeAsset shellAsset = serializedWindow.FindProperty("shellAsset").objectReferenceValue as VisualTreeAsset;

            Assert.That(shellAsset, Is.Not.Null);

            window.CreateGUI();

            Assert.That(window.rootVisualElement.Q<VisualElement>("ai-editor-shell"), Is.Not.Null);
            Assert.That(AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(shellAsset))
                .Any(path => AssetDatabase.LoadAssetAtPath<StyleSheet>(path) != null), Is.True);
        }

        [Test]
        public void CreateGUI_MissingShellDefaultReference_ThrowsConfigurationError()
        {
            // Keep the deliberately invalid window hidden so Unity's window backend does not
            // invoke CreateGUI independently and report the expected exception to the Console.
            AIEditorWindow window = ScriptableObject.CreateInstance<AIEditorWindow>();
            try
            {
                SerializedObject serializedWindow = new(window);
                SerializedProperty shellAsset = serializedWindow.FindProperty("shellAsset");
                shellAsset.objectReferenceValue = null;
                serializedWindow.ApplyModifiedPropertiesWithoutUndo();

                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => window.CreateGUI());

                Assert.That(exception.Message, Does.Contain("default reference is missing"));
            }
            finally
            {
                ScriptableObject.DestroyImmediate(window);
            }
        }

        [Test]
        public void CreateGUI_TabSwitchingOnlyDisplaysSelectedPage()
        {
            AIEditorWindow window = Track(AIEditorWindow.ShowWindow());
            window.CreateGUI();

            ToolbarToggle variablesTab = window.rootVisualElement.Q<ToolbarToggle>("ai-editor-variables-tab");
            variablesTab.value = true;

            Assert.That(window.window, Is.EqualTo(AIEditorWindow.Window.Variables));
            Assert.That(window.rootVisualElement.Q<IMGUIContainer>("ai-editor-nodes-pane").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(window.rootVisualElement.Q<IMGUIContainer>("ai-editor-variables-pane").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(window.rootVisualElement.Q<IMGUIContainer>("ai-editor-properties-pane").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));

            ToolbarToggle graphTab = window.rootVisualElement.Q<ToolbarToggle>("ai-editor-graph-tab");
            graphTab.value = true;

            Assert.That(window.window, Is.EqualTo(AIEditorWindow.Window.Graph));
            Assert.That(window.rootVisualElement.Q<VisualElement>("ai-editor-graph-host").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(window.rootVisualElement.Q<IMGUIContainer>("ai-editor-variables-pane").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void CreateGUI_ObjectFieldAndLockReflectWindowState()
        {
            BehaviourTreeData tree = CreateTree("Bound Shell Tree");
            AIEditorWindow window = Track(AIEditorWindow.ShowWindow(tree));
            window.CreateGUI();

            ObjectField treeField = window.rootVisualElement.Q<ObjectField>("ai-editor-tree-field");
            ToolbarToggle lockToggle = window.rootVisualElement.Q<ToolbarToggle>("ai-editor-lock-toggle");
            Image lockIcon = lockToggle.Q<Image>("ai-editor-lock-icon");

            Assert.That(treeField.value, Is.SameAs(tree));
            Assert.That(lockToggle.value, Is.False);
            Assert.That(string.IsNullOrEmpty(lockToggle.text), Is.True);
            Assert.That(lockIcon.image, Is.Not.Null);
            Assert.That(lockIcon.ClassListContains("ai-editor-lock-icon"), Is.True);

            window.SelectionLocked = true;

            Assert.That(lockToggle.value, Is.True);
            Assert.That(lockIcon.image, Is.Not.Null);
        }

        [Test]
        public void CreateGUI_UndefinedWindowValueFallsBackToNodes()
        {
            AIEditorWindow window = Track(AIEditorWindow.ShowWindow());
            window.window = (AIEditorWindow.Window)999;

            window.CreateGUI();

            Assert.That(window.window, Is.EqualTo(AIEditorWindow.Window.Nodes));
            Assert.That(window.rootVisualElement.Q<IMGUIContainer>("ai-editor-nodes-pane").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void CreateGUI_RepeatedBuildDoesNotDirtyTree()
        {
            BehaviourTreeData tree = CreateTree("Shell Tree");
            EditorUtility.ClearDirty(tree);
            AIEditorWindow window = Track(AIEditorWindow.ShowWindow(tree));

            window.CreateGUI();
            window.CreateGUI();
            window.Refresh();

            Assert.That(EditorUtility.IsDirty(tree), Is.False);
            Assert.That(window.rootVisualElement.Q<VisualElement>("ai-editor-shell"), Is.Not.Null);
            Assert.That(window.rootVisualElement.Query<Toolbar>("ai-editor-graph-toolbar").ToList(), Is.Empty);
            Assert.That(window.rootVisualElement.Query<VisualElement>("ai-editor-graph-view-options").ToList(), Has.Count.EqualTo(1));
            Assert.That(window.rootVisualElement.Query<GraphCanvasElement>("ai-editor-graph-canvas").ToList(), Has.Count.EqualTo(1));
            Assert.That(window.rootVisualElement.Query<VisualElement>("ai-editor-graph-inspector-splitter").ToList(), Has.Count.EqualTo(1));
            Assert.That(window.rootVisualElement.Query<VisualElement>("ai-editor-graph-inspector").ToList(), Has.Count.EqualTo(1));
            Assert.That(window.rootVisualElement.Query<IMGUIContainer>("ai-editor-graph-inspector-imgui").ToList(), Has.Count.EqualTo(1));
        }

        [Test]
        public void ToolbarContent_CompactWidth_UsesShortLabels()
        {
            Assert.That(AIEditorWindow.UseCompactToolbar(899f), Is.True);
            Assert.That(AIEditorWindow.UseCompactToolbar(900f), Is.False);
            Assert.That(AIEditorWindow.GetUpgradeButtonContent(2, compact: true).text, Is.EqualTo("Up (2)"));
            Assert.That(AIEditorWindow.GetClipboardButtonContent(3, hasContent: true, compact: true, statusText: "status").text, Is.EqualTo("Clip (3)"));
            Assert.That(AIEditorWindow.GetRefreshButtonContent(compact: true).text, Is.EqualTo("Ref"));
            Assert.That(AIEditorWindow.GetSettingsButtonContent(compact: true).text, Is.EqualTo("Prefs"));
        }

        [Test]
        public void ToolbarContent_DefaultWidth_UsesFullLabels()
        {
            Assert.That(AIEditorWindow.GetUpgradeButtonContent(2, compact: false).text, Is.EqualTo("Upgrade (2)"));
            Assert.That(AIEditorWindow.GetClipboardButtonContent(3, hasContent: true, compact: false, statusText: "status").text, Is.EqualTo("Clipboard (3)"));
            Assert.That(AIEditorWindow.GetClipboardButtonContent(0, hasContent: false, compact: false, statusText: "empty").text, Is.EqualTo("Clipboard"));
            Assert.That(AIEditorWindow.GetRefreshButtonContent(compact: false).text, Is.EqualTo("Refresh"));
            Assert.That(AIEditorWindow.GetSettingsButtonContent(compact: false).text, Is.EqualTo("Settings"));
        }

        [Test]
        public void ClampSidePaneWidth_InsideRange_ReturnsRequestedWidth()
        {
            Assert.That(TreeNodeModule.ClampSidePaneWidth(300f, 160f, 600f), Is.EqualTo(300f));
        }

        [Test]
        public void ClampSidePaneWidth_OutsideRange_ClampsToBounds()
        {
            Assert.That(TreeNodeModule.ClampSidePaneWidth(100f, 160f, 600f), Is.EqualTo(160f));
            Assert.That(TreeNodeModule.ClampSidePaneWidth(700f, 160f, 600f), Is.EqualTo(600f));
        }

        private BehaviourTreeData CreateTree(string treeName)
        {
            BehaviourTreeData tree = ScriptableObject.CreateInstance<BehaviourTreeData>();
            tree.name = treeName;
            createdTrees.Add(tree);
            return tree;
        }

        private GameObject CreateGameObject(string objectName)
        {
            GameObject gameObject = new(objectName);
            createdGameObjects.Add(gameObject);
            return gameObject;
        }

        private GameObject CreateGameObjectWithTree(string objectName, BehaviourTreeData tree)
        {
            GameObject gameObject = CreateGameObject(objectName);
            AI ai = gameObject.AddComponent<AI>();
            SerializedObject serializedAI = new(ai);

            // AI.Data has an internal setter, so tests assign the serialized backing field like the Inspector does.
            serializedAI.FindProperty("data").objectReferenceValue = tree;
            serializedAI.ApplyModifiedPropertiesWithoutUndo();
            return gameObject;
        }

        private AIEditorWindow Track(AIEditorWindow window)
        {
            openedWindows.Add(window);
            return window;
        }

        private AIInspector Track(AIInspector inspector)
        {
            openedInspectors.Add(inspector);
            return inspector;
        }
    }
}
