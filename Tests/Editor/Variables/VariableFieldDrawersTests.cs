using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Editor.Tests.Variables
{
    /// <summary>Validates variable-row layout and the production mutation commit boundary.</summary>
    public sealed class VariableFieldDrawersTests
    {
        private static readonly MethodInfo ApplyMutation = typeof(VariableFieldDrawers).GetMethod(
            "ApplyMutation", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly MethodInfo CreateVariable = typeof(VariableFieldDrawers).GetMethod(
            "CreateVariable", BindingFlags.NonPublic | BindingFlags.Static,
            null, new[] { typeof(BehaviourTreeData), typeof(VariableFieldBase), typeof(string) }, null);

        [Test]
        public void CalculateRowLayout_WithoutActionDoesNotReserveOverflow()
        {
            VariableFieldDrawers.VariableRowLayout layout = VariableFieldDrawers.CalculateRowLayout(new Rect(0f, 0f, 8f, 17f), false);

            Assert.That(layout.HasOverflow, Is.False);
            Assert.That(layout.ContentRect.width, Is.EqualTo(8f));
            Assert.That(layout.OverflowRect, Is.EqualTo(Rect.zero));
        }

        [Test]
        public void CalculateRowLayout_NarrowActionRowHasNonNegativeNonOverlappingRects()
        {
            Rect input = new(10f, 20f, 8f, 17f);
            VariableFieldDrawers.VariableRowLayout layout = VariableFieldDrawers.CalculateRowLayout(input, true);

            Assert.That(layout.ContentRect.width, Is.GreaterThanOrEqualTo(0f));
            Assert.That(layout.OverflowRect.width, Is.GreaterThanOrEqualTo(0f));
            Assert.That(layout.ContentRect.Overlaps(layout.OverflowRect), Is.False);
            Assert.That(layout.ContentRect.xMax, Is.LessThanOrEqualTo(layout.OverflowRect.xMin));
        }

        [Test]
        public void VariableFieldDrawers_GetVariableHeight_RemainsSingleLineForPseudoProbabilityWeight()
        {
            VariableField<int> weight = new();

            Assert.That(VariableFieldDrawers.GetVariableHeight(weight, null),
                Is.EqualTo(EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing));
        }

        [Test]
        public void ApplyMutationCommitPath_UseVariable_UndoRedoRestoresReferenceAndDirty()
        {
            VariableData existing = new("Existing", VariableType.Int);
            VariableMutationHost host = CreateHost(new VariableField(VariableType.Int));
            BehaviourTreeData tree = ScriptableObject.CreateInstance<BehaviourTreeData>();
            try
            {
                InvokeCommit(host, tree, value => value.SetReference(existing));
                AssertReference(host.value, existing);
                Assert.That(EditorUtility.IsDirty(host), Is.True);
                Undo.PerformUndo();
                Assert.That(host.value.HasEditorReference, Is.False);
                Undo.PerformRedo();
                AssertReference(host.value, existing);
            }
            finally { Destroy(host, tree); }
        }

        [Test]
        public void ApplyMutationCommitPath_CreateVariable_UndoRedoRestoresVariableAndReference()
        {
            VariableMutationHost host = CreateHost(new VariableField(VariableType.Int));
            BehaviourTreeData tree = ScriptableObject.CreateInstance<BehaviourTreeData>();
            try
            {
                int before = tree.EditorVariables.Count;
                InvokeCommit(host, tree, value => InvokeCreateVariable(tree, value));
                Assert.That(tree.EditorVariables.Count, Is.EqualTo(before + 1));
                Assert.That(host.value.HasEditorReference, Is.True);
                UUID created = host.value.UUID;
                Undo.PerformUndo();
                Assert.That(tree.EditorVariables.Count, Is.EqualTo(before));
                Assert.That(host.value.HasEditorReference, Is.False);
                Undo.PerformRedo();
                Assert.That(tree.EditorVariables.Count, Is.EqualTo(before + 1));
                Assert.That(host.value.UUID, Is.EqualTo(created));
            }
            finally { Destroy(host, tree); }
        }

        [Test]
        public void ApplyMutationCommitPath_RecreateInvalidReference_UndoRedoRestoresVariableAndReference()
        {
            VariableMutationHost host = CreateHost(new VariableField(VariableType.Int));
            host.value.SetReference(new VariableData("Missing", VariableType.Int));
            BehaviourTreeData tree = ScriptableObject.CreateInstance<BehaviourTreeData>();
            try
            {
                int before = tree.EditorVariables.Count;
                InvokeCommit(host, tree, value => InvokeCreateVariable(tree, value));
                Assert.That(tree.EditorVariables.Count, Is.EqualTo(before + 1));
                Assert.That(host.value.HasEditorReference, Is.True);
                Assert.That(host.value.UUID, Is.Not.EqualTo(UUID.Empty));
                Undo.PerformUndo();
                Assert.That(tree.EditorVariables.Count, Is.EqualTo(before));
                Assert.That(host.value.HasEditorReference, Is.True);
                Undo.PerformRedo();
                Assert.That(tree.EditorVariables.Count, Is.EqualTo(before + 1));
                Assert.That(host.value.HasEditorReference, Is.True);
            }
            finally { Destroy(host, tree); }
        }

        [Test]
        public void ApplyMutationCommitPath_SetConstant_UndoRedoRestoresReferenceState()
        {
            VariableData existing = new("Existing", VariableType.Int);
            VariableMutationHost host = CreateHost(new VariableField(VariableType.Int));
            host.value.SetReference(existing);
            BehaviourTreeData tree = ScriptableObject.CreateInstance<BehaviourTreeData>();
            try
            {
                InvokeCommit(host, tree, value => value.SetReference(null));
                Assert.That(host.value.HasEditorReference, Is.False);
                Undo.PerformUndo();
                AssertReference(host.value, existing);
                Undo.PerformRedo();
                Assert.That(host.value.HasEditorReference, Is.False);
            }
            finally { Destroy(host, tree); }
        }

        [Test]
        public void ApplyMutationCommitPath_Clear_UndoRedoRestoresReferenceState()
        {
            VariableData existing = new("Existing", VariableType.Int);
            VariableMutationHost host = CreateHost(new VariableField(VariableType.Int));
            host.value.SetReference(existing);
            BehaviourTreeData tree = ScriptableObject.CreateInstance<BehaviourTreeData>();
            try
            {
                InvokeCommit(host, tree, value => value.SetReference(null));
                Assert.That(host.value.HasEditorReference, Is.False);
                Undo.PerformUndo();
                AssertReference(host.value, existing);
                Undo.PerformRedo();
                Assert.That(host.value.HasEditorReference, Is.False);
            }
            finally { Destroy(host, tree); }
        }

        [Test]
        public void ApplyMutationCommitPath_ConstantType_UndoRedoRestoresType()
        {
            VariableField hostValue = new(VariableType.Float);
            VariableMutationHost host = CreateHost(hostValue);
            BehaviourTreeData tree = ScriptableObject.CreateInstance<BehaviourTreeData>();
            try
            {
                InvokeCommit(host, tree, value => ((VariableField)value).ForceSetConstantType(VariableType.Int));
                Assert.That(host.value.Type, Is.EqualTo(VariableType.Int));
                Undo.PerformUndo();
                Assert.That(host.value.Type, Is.EqualTo(VariableType.Float));
                Undo.PerformRedo();
                Assert.That(host.value.Type, Is.EqualTo(VariableType.Int));
            }
            finally { Destroy(host, tree); }
        }

        private static VariableMutationHost CreateHost(VariableField value)
        {
            VariableMutationHost host = ScriptableObject.CreateInstance<VariableMutationHost>();
            host.value = value;
            return host;
        }

        private static void InvokeCommit(VariableMutationHost host, BehaviourTreeData tree, Action<VariableFieldBase> mutation)
        {
            SerializedObject serializedObject = new(host);
            serializedObject.Update();
            serializedObject.FindProperty(nameof(VariableMutationHost.value)).boxedValue = host.value;
            serializedObject.ApplyModifiedProperties();
            ApplyMutation.Invoke(null, new object[] { tree, host, nameof(VariableMutationHost.value), host.value, mutation });
            serializedObject.Update();
            Assert.That(EditorUtility.IsDirty(tree), Is.True);
        }

        private static void InvokeCreateVariable(BehaviourTreeData tree, VariableFieldBase value)
        {
            CreateVariable.Invoke(null, new object[] { tree, value, null });
        }

        private static void AssertReference(VariableFieldBase value, VariableData expected)
        {
            Assert.That(value.HasEditorReference, Is.True);
            Assert.That(value.UUID, Is.EqualTo(expected.UUID));
        }

        private static void Destroy(VariableMutationHost host, BehaviourTreeData tree)
        {
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(tree);
            Undo.ClearAll();
        }

        /// <summary>Serialized host used only to exercise the production serialized commit boundary.</summary>
        private sealed class VariableMutationHost : ScriptableObject
        {
            public VariableField value;
        }
    }
}
