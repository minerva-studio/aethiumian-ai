using Aethiumian.AI.Nodes;
using Aethiumian.AI.Editor.Tests.Graph;
using NUnit.Framework;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Aethiumian.AI.Editor.Tests
{
    /// <summary>Checks creation, serialized editing, and the shared determine inspector for displacement conditions.</summary>
    public sealed class DisplacementToEditorTests : GraphEditorTestFixture
    {
        [Test]
        public void CreationMenu_OffersDisplacementToInConditions()
        {
            NodeCreationMenuFolder menu = NodeMenuCache.Shared.BuildCreationMenu(NodeCreationMenuContext.Nodes);
            Assert.That(menu.Children.Single(folder => folder.Name == "Conditions").Types, Does.Contain(typeof(DisplacementTo)));
            Assert.That(NodeMenuCache.Shared.GetDisplayName(typeof(DisplacementTo)), Is.EqualTo("Displacement To"));
        }

        [Test]
        public void SerializedSettings_SupportUndoRedoAndRetainHiddenThresholds()
        {
            DisplacementTo node = Node<DisplacementTo>("Displacement To");
            node.xExpect = 4f;
            BehaviourTreeData tree = Tree(node);
            SerializedProperty property = tree.GetNodeProperty(node);
            Undo.IncrementCurrentGroup();
            property.FindPropertyRelative(nameof(DisplacementTo.compareX)).boolValue = true;
            property.FindPropertyRelative(nameof(DisplacementTo.storeResult)).boolValue = true;
            property.FindPropertyRelative(nameof(DisplacementTo.absoluteX)).boolValue = true;
            property.serializedObject.ApplyModifiedProperties();
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();

            Undo.PerformUndo();
            tree.SerializedObject.Update();
            tree.RegenerateTable();
            DisplacementTo undone = (DisplacementTo)tree.GetNode(node.uuid);
            Assert.That(undone.compareX, Is.False);
            Assert.That(undone.storeResult, Is.False);
            Assert.That(undone.absoluteX, Is.False);
            Assert.That(undone.xExpect.Constant, Is.EqualTo(4f));

            Undo.PerformRedo();
            tree.SerializedObject.Update();
            tree.RegenerateTable();
            DisplacementTo redone = (DisplacementTo)tree.GetNode(node.uuid);
            Assert.That(redone.compareX, Is.True);
            Assert.That(redone.storeResult, Is.True);
            Assert.That(redone.absoluteX, Is.True);
            Assert.That(redone.xExpect.Constant, Is.EqualTo(4f));
        }

        [UnityTest]
        public IEnumerator SharedDetermineInspector_RepaintsWithoutChangingAuthoredValues()
        {
            DisplacementTo node = Node<DisplacementTo>("Displacement To");
            node.xExpect = 4f;
            node.yExpect = 0.5f;
            BehaviourTreeData tree = Tree(node);
            AIEditorWindow window = ShowGraphWindow(tree);
            window.rootVisualElement.Clear();
            DetermineNodeDrawer drawer = new();
            int repaints = 0;
            IMGUIContainer inspector = new(() =>
            {
                drawer.Init(window, tree.GetNodeProperty(node));
                drawer.Draw();
                if (Event.current.type == EventType.Repaint) repaints++;
            });
            window.rootVisualElement.Add(inspector);
            inspector.style.flexGrow = 1f;
            for (int mask = 0; mask < 8; mask++)
            {
                node.compareX = (mask & 1) != 0;
                node.compareY = (mask & 2) != 0;
                node.storeResult = (mask & 4) != 0;
                tree.SerializedObject.Update();
                string before = JsonUtility.ToJson(node);
                int previousRepaints = repaints;
                inspector.MarkDirtyRepaint();
                window.Repaint();
                for (int frame = 0; frame < 30 && repaints == previousRepaints; frame++) yield return null;
                Assert.That(repaints, Is.GreaterThan(previousRepaints), $"Inspector did not repaint for mask {mask}.");
                Assert.That(JsonUtility.ToJson(node), Is.EqualTo(before), $"Drawing changed settings for mask {mask}.");
            }
        }
    }
}
