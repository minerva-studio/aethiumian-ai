using Aethiumian.AI.Variables;
using NUnit.Framework;
using System;
using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Editor.Tests.Variables
{
    /// <summary>Validates the editor-only variable usage configuration contract.</summary>
    public sealed class VariableAuthoringTests
    {
        private sealed class ScriptBindingProbe : MonoBehaviour
        {
            public GameObject Target;
        }

        [SetUp]
        public void SetUp()
        {
            Undo.ClearAll();
        }

        /// <summary>Verifies Timer authoring resets the definition to the sole supported TimerVariable shape.</summary>
        [Test]
        public void ConfigureTimer_UsesLocalInactiveFloatTimerWithoutScriptBinding()
        {
            VariableData data = new("Cooldown", VariableType.String);
            data.SetScript(true);
            data.Path = "LegacyMember";
            UUID uuid = data.UUID;

            VariableAuthoring.ConfigureTimer(data);

            Assert.That(data.UUID, Is.EqualTo(uuid));
            Assert.That(data.IsTimer, Is.True);
            Assert.That(data.Type, Is.EqualTo(VariableType.Float));
            Assert.That(data.GetDefaultValue(), Is.EqualTo(0f));
            Assert.That(data.IsScript, Is.False);
            Assert.That(data.Path, Is.Null);
            Assert.That(data.IsStatic, Is.False);
            Assert.That(data.IsGlobal, Is.False);
        }

        /// <summary>Verifies script bindings derive both their path and their Unity object type from the selected member.</summary>
        [Test]
        public void ConfigureScriptBinding_DerivesMemberTypeAndClearsRuntimeSource()
        {
            VariableData data = new("Target", VariableType.Float);
            data.Flags |= VariableFlag.Timer;

            VariableAuthoring.ConfigureScriptBinding(data, nameof(ScriptBindingProbe.Target), typeof(GameObject));

            Assert.That(data.IsScript, Is.True);
            Assert.That(data.Path, Is.EqualTo(nameof(ScriptBindingProbe.Target)));
            Assert.That(data.IsTimer, Is.False);
            Assert.That(data.Type, Is.EqualTo(VariableType.UnityObject));
            Assert.That(data.ObjectType, Is.EqualTo(typeof(GameObject)));
        }

        /// <summary>Verifies a tree asset restores and reapplies the complete Timer authoring state through one Undo record.</summary>
        [Test]
        public void ConfigureTimer_UndoRedoRestoresTheCompleteUsage()
        {
            BehaviourTreeData tree = ScriptableObject.CreateInstance<BehaviourTreeData>();
            try
            {
                VariableData data = new("Cooldown", VariableType.Float);
                tree.variables.Add(data);
                UUID uuid = data.UUID;

                Undo.RecordObject(tree, "Configure Timer");
                VariableAuthoring.ConfigureTimer(data);
                EditorUtility.SetDirty(tree);

                Assert.That(data.IsTimer, Is.True);
                Undo.PerformUndo();
                tree.SerializedObject.Update();
                data = tree.variables.Find(variable => variable.UUID == uuid);
                Assert.That(data.IsTimer, Is.False);
                Assert.That(data.IsScript, Is.False);

                Undo.PerformRedo();
                tree.SerializedObject.Update();
                data = tree.variables.Find(variable => variable.UUID == uuid);
                Assert.That(data.IsTimer, Is.True);
                Assert.That(data.IsScript, Is.False);
                Assert.That(string.IsNullOrEmpty(data.Path), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tree);
            }
        }

    }
}
