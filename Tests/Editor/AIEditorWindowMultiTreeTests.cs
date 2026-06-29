using Aethiumian.AI.Editor;
using Aethiumian.AI.Nodes;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

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
        public void TryGetTreeAssetDiskPaths_NullTree_ReturnsFalseWithoutPaths()
        {
            bool result = AIEditorWindow.TryGetTreeAssetDiskPaths(
                null,
                out string assetPath,
                out string fullPath,
                out string folderPath,
                showDialog: false);

            Assert.That(result, Is.False);
            Assert.That(assetPath, Is.Null);
            Assert.That(fullPath, Is.Null);
            Assert.That(folderPath, Is.Null);
        }

        [Test]
        public void TryGetTreeAssetDiskPaths_UnsavedTree_ReturnsFalseWithoutPaths()
        {
            BehaviourTreeData tree = CreateTree("Unsaved Tree");

            bool result = AIEditorWindow.TryGetTreeAssetDiskPaths(
                tree,
                out string assetPath,
                out string fullPath,
                out string folderPath,
                showDialog: false);

            Assert.That(result, Is.False);
            Assert.That(assetPath, Is.Empty);
            Assert.That(fullPath, Is.Null);
            Assert.That(folderPath, Is.Null);
        }

        [Test]
        public void TryBuildTreeAssetDiskPaths_EmptyAssetPath_ReturnsFalseWithoutPaths()
        {
            bool result = AIEditorWindow.TryBuildTreeAssetDiskPaths(
                string.Empty,
                out string fullPath,
                out string folderPath);

            Assert.That(result, Is.False);
            Assert.That(fullPath, Is.Null);
            Assert.That(folderPath, Is.Null);
        }

        [Test]
        public void TryBuildTreeAssetDiskPaths_AssetPath_ReturnsFullFileAndFolderPaths()
        {
            string assetPath = "Assets/TestData/Nested/TestTree.asset";
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string expectedFullPath = Path.GetFullPath(Path.Combine(
                projectRoot,
                "Assets",
                "TestData",
                "Nested",
                "TestTree.asset"));
            string expectedFolderPath = Path.GetDirectoryName(expectedFullPath);

            bool result = AIEditorWindow.TryBuildTreeAssetDiskPaths(
                assetPath,
                out string fullPath,
                out string folderPath);

            Assert.That(result, Is.True);
            Assert.That(fullPath, Is.EqualTo(expectedFullPath));
            Assert.That(folderPath, Is.EqualTo(expectedFolderPath));
        }

        [Test]
        public void GetUpgradableNodeCount_EmptyOrNullNodes_ReturnsZero()
        {
            Assert.That(AIEditorWindow.GetUpgradableNodeCount(null), Is.Zero);
            Assert.That(AIEditorWindow.GetUpgradableNodeCount(Array.Empty<TreeNode>()), Is.Zero);
        }

        [Test]
        public void GetUpgradableNodeCount_MixedNodes_ReturnsOnlyUpgradableNodes()
        {
            TreeNode[] nodes =
            {
                new NonUpgradeableProbeNode(),
                new UpgradableProbeNode(),
                null,
                new UpgradableProbeNode(),
            };

            Assert.That(AIEditorWindow.GetUpgradableNodeCount(nodes), Is.EqualTo(2));
        }

        [Test]
        public void GetUpgradableNodeCount_CurrentTree_UsesEditorNodes()
        {
            BehaviourTreeData tree = CreateTree("Upgrade Count Tree");
            AIEditorWindow window = Track(AIEditorWindow.ShowWindow(tree));

            Assert.That(window.GetUpgradableNodeCount(), Is.Zero);

            tree.nodes.Add(new UpgradableProbeNode());
            tree.nodes.Add(new NonUpgradeableProbeNode());

            Assert.That(window.GetUpgradableNodeCount(), Is.EqualTo(1));
        }

        [Test]
        public void GetUpgradeButtonContent_UsesCountInLabelAndTooltip()
        {
            GUIContent content = AIEditorWindow.GetUpgradeButtonContent(3);

            Assert.That(content.text, Is.EqualTo("Upgrade (3)"));
            Assert.That(content.tooltip, Does.Contain("3 node(s)"));
        }

        [Test]
        public void GetClipboardButtonContent_EmptyClipboard_UsesStableDisabledLabel()
        {
            Clipboard clipboard = new();

            GUIContent content = AIEditorWindow.GetClipboardButtonContent(clipboard);

            Assert.That(content.text, Is.EqualTo("Clipboard"));
            Assert.That(content.tooltip, Is.EqualTo("Clipboard is empty."));
        }

        [Test]
        public void GetClipboardButtonContent_WithContent_UsesCountAndRootName()
        {
            Clipboard clipboard = new();
            clipboard.treeNodes.Add(new NonUpgradeableProbeNode { name = "Root Node" });

            GUIContent buttonContent = AIEditorWindow.GetClipboardButtonContent(clipboard);
            GUIContent statusContent = AIEditorWindow.GetClipboardStatusContent(clipboard);

            Assert.That(buttonContent.text, Is.EqualTo("Clipboard (1)"));
            Assert.That(buttonContent.tooltip, Is.EqualTo("Clipboard: 1 node(s), root: Root Node"));
            Assert.That(statusContent.text, Is.EqualTo("Clipboard: 1 node(s), root: Root Node"));
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

        [Serializable]
        private class NonUpgradeableProbeNode : TreeNode
        {
            public override void Initialize()
            {
            }

            public override State Execute()
            {
                return State.Success;
            }
        }

        [Serializable]
        private sealed class UpgradableProbeNode : NonUpgradeableProbeNode
        {
            public override TreeNode Upgrade()
            {
                return new NonUpgradeableProbeNode();
            }
        }
    }
}
