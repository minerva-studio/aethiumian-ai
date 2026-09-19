using Aethiumian.AI.Nodes;
using Aethiumian.AI.Variables;
using NUnit.Framework;
using System;
using UnityEngine;

namespace Aethiumian.AI.Editor.Tests
{
    /// <summary>Verifies the baseline and identity semantics of the ValueChanged determine.</summary>
    public sealed class ValueChangedTests
    {
        [Test]
        public void FirstObservationEstablishesBaselineWithoutReportingChange()
        {
            TreeVariable variable = CreateVariable(VariableType.Int, 1);
            ValueChanged node = CreateNode(CreateReference(variable));

            Assert.That(node.GetValue(), Is.False);
            Assert.That(node.GetValue(), Is.False);
        }

        [Test]
        public void ChangedValueReportsOneEdgeThenRefreshesBaseline()
        {
            TreeVariable variable = CreateVariable(VariableType.Int, 1);
            ValueChanged node = CreateNode(CreateReference(variable));

            Assert.That(node.GetValue(), Is.False);
            variable.SetValue(2);

            Assert.That(node.GetValue(), Is.True);
            Assert.That(node.GetValue(), Is.False);
        }

        [Test]
        public void ComponentAndGameObjectOnSameObjectAreEqual()
        {
            GameObject target = new("ValueChangedTarget");
            try
            {
                TreeVariable variable = CreateVariable(VariableType.UnityObject, target.transform);
                ValueChanged node = CreateNode(CreateReference(variable));

                Assert.That(node.GetValue(), Is.False);
                variable.SetValue(target);

                Assert.That(node.GetValue(), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void UnityObjectNullTransitionsAreReported()
        {
            GameObject target = new("ValueChangedTarget");
            try
            {
                TreeVariable variable = CreateVariable<UnityEngine.Object>(VariableType.UnityObject, null);
                ValueChanged node = CreateNode(CreateReference(variable));

                Assert.That(node.GetValue(), Is.False);
                variable.SetValue(target);
                Assert.That(node.GetValue(), Is.True);
                variable.SetValue<UnityEngine.Object>(null);
                Assert.That(node.GetValue(), Is.True);
                Assert.That(node.GetValue(), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void InitializeClearsPreviousBaseline()
        {
            TreeVariable variable = CreateVariable(VariableType.Int, 1);
            ValueChanged node = CreateNode(CreateReference(variable));

            Assert.That(node.GetValue(), Is.False);
            variable.SetValue(2);
            Assert.That(node.GetValue(), Is.True);

            node.Initialize();
            Assert.That(node.GetValue(), Is.False);
        }

        [Test]
        public void RepeatedScalarObservationsDoNotAllocate()
        {
            TreeVariable variable = CreateVariable(VariableType.Int, 1);
            ValueChanged node = CreateNode(CreateReference(variable));
            node.GetValue();

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 1000; index++)
            {
                node.GetValue();
            }

            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }

        private static ValueChanged CreateNode(VariableReference field)
        {
            ValueChanged node = new() { value = field };
            node.Initialize();
            return node;
        }

        private static VariableReference CreateReference(TreeVariable variable)
        {
            VariableReference reference = new();
            reference.SetRuntimeReference(variable);
            return reference;
        }

        private static TreeVariable CreateVariable<T>(VariableType type, T value)
        {
            TreeVariable variable = new(new VariableData("ValueChanged", type));
            variable.SetValue(value);
            return variable;
        }
    }
}
