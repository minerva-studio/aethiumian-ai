using Aethiumian.AI.Editor;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Aethiumian.AI.Tests
{
    public sealed class AIEditorWindowMultiTreeTests
    {
        private readonly List<AIEditorWindow> openedWindows = new();
        private readonly List<AIInspector> openedInspectors = new();
        private readonly List<BehaviourTreeData> createdTrees = new();
        private readonly List<GameObject> createdGameObjects = new();

        [SetUp]
        public void SetUp()
        {
            Selection.activeObject = null;
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
        public void CreateGUI_ShellContainsThreePagesAndNoGraphPage()
        {
            AIEditorWindow window = Track(AIEditorWindow.ShowWindow());
            window.CreateGUI();

            Assert.That(window.rootVisualElement.Q<VisualElement>("ai-editor-shell"), Is.Not.Null);
            Assert.That(window.rootVisualElement.Q<ToolbarToggle>("ai-editor-nodes-tab"), Is.Not.Null);
            Assert.That(window.rootVisualElement.Q<ToolbarToggle>("ai-editor-variables-tab"), Is.Not.Null);
            Assert.That(window.rootVisualElement.Q<ToolbarToggle>("ai-editor-properties-tab"), Is.Not.Null);
            Assert.That(window.rootVisualElement.Q<IMGUIContainer>("ai-editor-nodes-pane"), Is.Not.Null);
            Assert.That(window.rootVisualElement.Q<IMGUIContainer>("ai-editor-variables-pane"), Is.Not.Null);
            Assert.That(window.rootVisualElement.Q<IMGUIContainer>("ai-editor-properties-pane"), Is.Not.Null);
            Assert.That(window.rootVisualElement.Q("ai-editor-graph-tab"), Is.Null);
        }

        [Test]
        public void CreateGUI_TreeSelectionUsesSeparateFullWidthRowBelowToolbar()
        {
            AIEditorWindow window = Track(AIEditorWindow.ShowWindow());
            window.CreateGUI();

            VisualElement shell = window.rootVisualElement.Q<VisualElement>("ai-editor-shell");
            Toolbar toolbar = shell.Q<Toolbar>("ai-editor-toolbar");
            VisualElement selectionRow = shell.Q<VisualElement>("ai-editor-tree-selection");
            ObjectField treeField = shell.Q<ObjectField>("ai-editor-tree-field");

            Assert.That(selectionRow, Is.Not.Null);
            Assert.That(treeField.parent, Is.SameAs(selectionRow));
            Assert.That(treeField.GetFirstAncestorOfType<Toolbar>(), Is.Null);
            Assert.That(shell.IndexOf(toolbar), Is.LessThan(shell.IndexOf(selectionRow)));
            Assert.That(shell.Q<VisualElement>("ai-editor-toolbar-spacer"), Is.Not.Null);
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

            window.SelectionLocked = true;

            Assert.That(lockToggle.value, Is.True);
            Assert.That(lockIcon.image, Is.Not.Null);
        }

        [Test]
        public void CreateGUI_LegacyGraphWindowValueFallsBackToNodes()
        {
            AIEditorWindow window = Track(AIEditorWindow.ShowWindow());
            window.window = (AIEditorWindow.Window)1;

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
