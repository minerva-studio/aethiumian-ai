using Aethiumian.AI.Editor;
using NUnit.Framework;
using System.Collections.Generic;
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
                Object.DestroyImmediate(tree);
            }

            foreach (GameObject gameObject in createdGameObjects.Where(gameObject => gameObject))
            {
                Object.DestroyImmediate(gameObject);
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
