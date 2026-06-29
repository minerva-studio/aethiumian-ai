using Aethiumian.AI.Editor;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Tests
{
    /// <summary>
    /// Author: Codex
    /// Verifies that AI runtime inspector windows bind to individual AI instances.
    /// </summary>
    public sealed class AIInspectorMultiInstanceTests
    {
        private readonly List<AIInspector> openedInspectors = new();
        private readonly List<GameObject> createdGameObjects = new();

        [SetUp]
        public void SetUp()
        {
            Selection.activeObject = null;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (AIInspector inspector in openedInspectors.Where(inspector => inspector).Distinct())
            {
                inspector.Close();
            }

            foreach (GameObject gameObject in createdGameObjects.Where(gameObject => gameObject))
            {
                Object.DestroyImmediate(gameObject);
            }

            Selection.activeObject = null;
            openedInspectors.Clear();
            createdGameObjects.Clear();
        }

        [Test]
        public void ShowWindow_SameAI_ReusesExistingWindow()
        {
            AI ai = CreateAI("Shared AI");

            AIInspector firstInspector = Track(AIInspector.ShowWindow(ai));
            AIInspector secondInspector = Track(AIInspector.ShowWindow(ai));

            Assert.That(secondInspector, Is.SameAs(firstInspector));
            Assert.That(AIInspector.TryGetOpenWindow(ai, out AIInspector foundInspector), Is.True);
            Assert.That(foundInspector, Is.SameAs(firstInspector));
        }

        [Test]
        public void ShowWindow_DifferentAIs_OpensSeparateWindows()
        {
            AI firstAI = CreateAI("First AI");
            AI secondAI = CreateAI("Second AI");

            AIInspector firstInspector = Track(AIInspector.ShowWindow(firstAI));
            AIInspector secondInspector = Track(AIInspector.ShowWindow(secondAI));

            Assert.That(secondInspector, Is.Not.SameAs(firstInspector));
            Assert.That(firstInspector.SelectedAI, Is.SameAs(firstAI));
            Assert.That(secondInspector.SelectedAI, Is.SameAs(secondAI));
        }

        [Test]
        public void FollowUnitySelection_UnlockedWindow_UsesSelectedGameObjectAI()
        {
            AI ai = CreateAI("Selected AI");
            AIInspector inspector = Track(EditorWindow.CreateWindow<AIInspector>());

            Selection.activeObject = ai.gameObject;
            inspector.FollowUnitySelection();

            Assert.That(inspector.SelectedAI, Is.SameAs(ai));
        }

        [Test]
        public void FollowUnitySelection_LockedWindow_KeepsCurrentAI()
        {
            AI firstAI = CreateAI("Locked AI");
            AI secondAI = CreateAI("Ignored AI");
            AIInspector inspector = Track(AIInspector.ShowWindow(firstAI));
            inspector.SelectionLocked = true;

            Selection.activeObject = secondAI.gameObject;
            inspector.FollowUnitySelection();

            Assert.That(inspector.SelectedAI, Is.SameAs(firstAI));
        }

        [Test]
        public void FollowUnitySelection_InvalidSelection_KeepsCurrentAI()
        {
            AI ai = CreateAI("Current AI");
            GameObject unrelatedObject = CreateGameObject("Unrelated Object");
            AIInspector inspector = Track(AIInspector.ShowWindow(ai));

            Selection.activeObject = unrelatedObject;
            inspector.FollowUnitySelection();

            Assert.That(inspector.SelectedAI, Is.SameAs(ai));
        }

        private AI CreateAI(string objectName)
        {
            return CreateGameObject(objectName).AddComponent<AI>();
        }

        private GameObject CreateGameObject(string objectName)
        {
            GameObject gameObject = new(objectName);
            createdGameObjects.Add(gameObject);
            return gameObject;
        }

        private AIInspector Track(AIInspector inspector)
        {
            openedInspectors.Add(inspector);
            return inspector;
        }
    }
}
