using Aethiumian.AI.Editor;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using Minerva.Module;
using NUnit.Framework;
using System;
using UnityEngine;

namespace Aethiumian.AI.Tests
{
    /// <summary>
    /// Verifies AIInspector runtime field drawing decisions without requiring IMGUI layout execution.
    /// </summary>
    public sealed class AIInspectorRuntimeFieldDrawerTests
    {
        [Test]
        public void ResolveDrawKind_NullField_ReturnsNull()
        {
            var kind = AIInspectorRuntimeFieldDrawer.ResolveDrawKind(null, typeof(string));

            Assert.That(kind, Is.EqualTo(AIInspectorRuntimeFieldDrawer.FieldDrawKind.Null));
        }

        [Test]
        public void ResolveDrawKind_NodeReference_ReturnsNodeReference()
        {
            var reference = new NodeReference(UUID.NewUUID());

            var kind = AIInspectorRuntimeFieldDrawer.ResolveDrawKind(reference, typeof(NodeReference));

            Assert.That(kind, Is.EqualTo(AIInspectorRuntimeFieldDrawer.FieldDrawKind.NodeReference));
        }

        [Test]
        public void ResolveDrawKind_Variable_ReturnsVariable()
        {
            var variable = new VariableField<int>();

            var kind = AIInspectorRuntimeFieldDrawer.ResolveDrawKind(variable, typeof(VariableBase));

            Assert.That(kind, Is.EqualTo(AIInspectorRuntimeFieldDrawer.FieldDrawKind.Variable));
        }

        [TestCase(1, typeof(int))]
        [TestCase("value", typeof(string))]
        [TestCase(TestEnum.First, typeof(TestEnum))]
        public void ResolveDrawKind_EditableSimpleField_ReturnsGenericEditable(object value, Type declaredType)
        {
            var kind = AIInspectorRuntimeFieldDrawer.ResolveDrawKind(value, declaredType);

            Assert.That(kind, Is.EqualTo(AIInspectorRuntimeFieldDrawer.FieldDrawKind.GenericEditable));
        }

        [Test]
        public void ResolveDrawKind_Uuid_ReturnsReadOnlyUuid()
        {
            UUID uuid = UUID.NewUUID();

            var kind = AIInspectorRuntimeFieldDrawer.ResolveDrawKind(uuid, typeof(UUID));

            Assert.That(kind, Is.EqualTo(AIInspectorRuntimeFieldDrawer.FieldDrawKind.Uuid));
        }

        [TestCase(typeof(IDisposable))]
        [TestCase(typeof(AbstractPayload))]
        [TestCase(typeof(Action))]
        public void ResolveDrawKind_UnsupportedRuntimeField_ReturnsReadOnlyUnsupported(Type declaredType)
        {
            object value = declaredType == typeof(Action)
                ? new Action(() => { })
                : null;

            var kind = AIInspectorRuntimeFieldDrawer.ResolveDrawKind(value, declaredType);

            Assert.That(kind, Is.EqualTo(AIInspectorRuntimeFieldDrawer.FieldDrawKind.ReadOnlyUnsupported));
        }

        private enum TestEnum
        {
            First
        }

        private abstract class AbstractPayload
        {
            public int Value;
        }
    }
}
