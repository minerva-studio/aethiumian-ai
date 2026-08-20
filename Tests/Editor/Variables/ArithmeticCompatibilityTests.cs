using Aethiumian.AI.Nodes;
using Aethiumian.AI.Variables;
using NUnit.Framework;
using System;
using UnityEngine;

namespace Aethiumian.AI.Editor.Tests.Variables
{
    public sealed class ArithmeticCompatibilityTests
    {
        [Test]
        public void FloatBroadcastsToVector4AndPreservesW()
        {
            Add node = new()
            {
                a = Constant(VariableType.Float, 2f),
                b = Constant(VariableType.Vector4, new Vector4(1f, 2f, 3f, 4f)),
                result = Reference(VariableType.Vector4, Vector4.zero),
            };

            Assert.That(node.Execute(), Is.EqualTo(State.Success));
            Assert.That(node.result.Vector4Value, Is.EqualTo(new Vector4(3f, 4f, 5f, 6f)));
        }

        [Test]
        public void SameWidthVectorsAreAccepted()
        {
            Min node = new()
            {
                a = Constant(VariableType.Vector3, new Vector3(3f, -2f, 5f)),
                b = Constant(VariableType.Vector3, new Vector3(1f, 4f, 2f)),
                result = Reference(VariableType.Vector3, Vector3.zero),
            };

            Assert.That(node.Execute(), Is.EqualTo(State.Success));
            Assert.That(node.result.Vector3Value, Is.EqualTo(new Vector3(1f, -2f, 2f)));
        }

        [Test]
        public void DifferentVectorWidthsAreRejected()
        {
            Add node = new()
            {
                a = Constant(VariableType.Vector2, new Vector2(1f, 2f)),
                b = Constant(VariableType.Vector3, new Vector3(3f, 4f, 5f)),
                result = Reference(VariableType.Vector3, Vector3.zero),
            };

            Assert.That(node.Execute(), Is.EqualTo(State.Failed));
        }

        [Test]
        public void IntIntArithmeticKeepsIntegerResult()
        {
            Multiply node = new()
            {
                a = Constant(VariableType.Int, 7),
                b = Constant(VariableType.Int, 6),
                result = Reference(VariableType.Int, 0),
            };

            Assert.That(node.Execute(), Is.EqualTo(State.Success));
            Assert.That(node.result.IntValue, Is.EqualTo(42));
        }

        [Test]
        public void DivideUsesVector2Components()
        {
            Divide node = new()
            {
                a = Constant(VariableType.Vector2, new Vector2(8f, 9f)),
                b = Constant(VariableType.Float, 2f),
                result = Reference(VariableType.Vector2, Vector2.zero),
            };

            Assert.That(node.Execute(), Is.EqualTo(State.Success));
            Assert.That(node.result.Vector2Value, Is.EqualTo(new Vector2(4f, 4.5f)));
        }

        [Test]
        public void MinMaxAndRoundingSupportVector4()
        {
            Max max = new()
            {
                a = Constant(VariableType.Vector4, new Vector4(1.2f, -2.2f, 3.9f, 4.1f)),
                b = Constant(VariableType.Float, 2f),
                result = Reference(VariableType.Vector4, Vector4.zero),
            };
            Floor floor = new()
            {
                a = Constant(VariableType.Vector4, new Vector4(1.9f, -2.1f, 3.5f, 4.9f)),
                result = Reference(VariableType.Vector4, Vector4.zero),
            };
            Ceil ceil = new()
            {
                a = Constant(VariableType.Vector4, new Vector4(1.1f, -2.9f, 3.1f, 4.01f)),
                result = Reference(VariableType.Vector4, Vector4.zero),
            };
            Round round = new()
            {
                a = Constant(VariableType.Vector4, new Vector4(1.4f, 2.6f, -3.4f, -4.6f)),
                result = Reference(VariableType.Vector4, Vector4.zero),
            };

            Assert.That(max.Execute(), Is.EqualTo(State.Success));
            Assert.That(max.result.Vector4Value, Is.EqualTo(new Vector4(2f, 2f, 3.9f, 4.1f)));
            Assert.That(floor.Execute(), Is.EqualTo(State.Success));
            Assert.That(floor.result.Vector4Value, Is.EqualTo(new Vector4(1f, -3f, 3f, 4f)));
            Assert.That(ceil.Execute(), Is.EqualTo(State.Success));
            Assert.That(ceil.result.Vector4Value, Is.EqualTo(new Vector4(2f, -2f, 4f, 5f)));
            Assert.That(round.Execute(), Is.EqualTo(State.Success));
            Assert.That(round.result.Vector4Value, Is.EqualTo(new Vector4(1f, 3f, -3f, -5f)));
        }

        [Test]
        public void GenericTypeDoesNotEnterTypedMathPath()
        {
            Assert.That(
                ArithmeticCompatibility.TryResolveComponentwiseType(
                    VariableType.Generic,
                    VariableType.Float,
                    out _),
                Is.False);
        }

        [Test]
        public void TypedScalarReadsDoNotAllocateAfterWarmup()
        {
            VariableField<float> field = 2.5f;
            _ = field.GetValue<float>();

            long before = GC.GetAllocatedBytesForCurrentThread();
            float sink = 0f;
            for (int i = 0; i < 1000; i++)
            {
                sink += field.GetValue<float>();
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(sink, Is.EqualTo(2500f));
            Assert.That(allocated, Is.EqualTo(0));
        }

        [Test]
        public void AssignReadsUsingTheDestinationType()
        {
            VariableReference destination = Reference(VariableType.Float, 0f);
            Assign node = new()
            {
                destination = destination,
                source = Constant(VariableType.Int, 7),
            };

            Assert.That(node.Execute(), Is.EqualTo(State.Success));
            Assert.That(destination.FloatValue, Is.EqualTo(7f));
        }

        [Test]
        public void CopyDispatchesOnTheSourceTypeAndUsesTargetConversion()
        {
            VariableReference destination = Reference(VariableType.Float, 0f);
            Copy node = new()
            {
                from = Constant(VariableType.Int, 7),
                to = destination,
            };

            Assert.That(node.Execute(), Is.EqualTo(State.Success));
            Assert.That(destination.FloatValue, Is.EqualTo(7f));
        }

        private static VariableField Constant(VariableType type, object value)
        {
            VariableField field = new(type);
            field.ForceSetConstantValue(value);
            return field;
        }

        private static VariableReference Reference<T>(VariableType type, T value)
        {
            VariableData data = new("Arithmetic result", type);
            TreeVariable variable = new(data);
            variable.SetValue(value);

            VariableReference reference = new();
            reference.SetRuntimeReference(variable);
            return reference;
        }
    }
}
