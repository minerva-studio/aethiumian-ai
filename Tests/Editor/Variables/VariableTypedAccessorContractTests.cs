using System;
using Aethiumian.AI.Variables;
using NUnit.Framework;
using UnityEngine;

namespace Aethiumian.AI.Editor.Tests.Variables
{
    public sealed class VariableTypedAccessorContractTests
    {
        [Test]
        public void VariableFieldBaseTypedPropertiesUseGetValueAsCanonicalAccessor()
        {
            ProbeVariableFieldBase variable = new();

            AssertTypedValues(variable);
            Assert.That(variable.GetValueCalls, Is.EqualTo(9));
        }

        [Test]
        public void VariableTypedPropertiesUseGetValueAsCanonicalAccessor()
        {
            ProbeVariable variable = new();

            AssertTypedValues(variable);
            Assert.That(variable.GetValueCalls, Is.EqualTo(9));
        }

        private static void AssertTypedValues(ProbeVariableFieldBase variable)
        {
            Assert.That(variable.StringValue, Is.EqualTo("string"));
            Assert.That(variable.IntValue, Is.EqualTo(7));
            Assert.That(variable.FloatValue, Is.EqualTo(2.5f));
            Assert.That(variable.BoolValue, Is.True);
            Assert.That(variable.Vector2Value, Is.EqualTo(new Vector2(1f, 2f)));
            Assert.That(variable.Vector3Value, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(variable.Vector4Value, Is.EqualTo(new Vector4(1f, 2f, 3f, 4f)));
            Assert.That(variable.ColorValue, Is.EqualTo(new Color(1f, 2f, 3f, 4f)));
            Assert.That(variable.UnityObjectValue, Is.Null);
        }

        private static void AssertTypedValues(ProbeVariable variable)
        {
            Assert.That(variable.StringValue, Is.EqualTo("string"));
            Assert.That(variable.IntValue, Is.EqualTo(7));
            Assert.That(variable.FloatValue, Is.EqualTo(2.5f));
            Assert.That(variable.BoolValue, Is.True);
            Assert.That(variable.Vector2Value, Is.EqualTo(new Vector2(1f, 2f)));
            Assert.That(variable.Vector3Value, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(variable.Vector4Value, Is.EqualTo(new Vector4(1f, 2f, 3f, 4f)));
            Assert.That(variable.ColorValue, Is.EqualTo(new Color(1f, 2f, 3f, 4f)));
            Assert.That(variable.UnityObjectValue, Is.Null);
        }

        private sealed class ProbeVariableFieldBase : VariableFieldBase
        {
            public int GetValueCalls { get; private set; }

            public override Type FieldObjectType => typeof(object);
            public override VariableType Type => VariableType.Generic;
            public override object ConstantBoxed => null;
            public override object Value => null;

            public override TTarget GetValue<TTarget>()
            {
                GetValueCalls++;
                return GetProbeValue<TTarget>();
            }

            public override void SetValue<T>(T value)
            {
            }
        }

        private sealed class ProbeVariable : RuntimeVariable
        {
            public int GetValueCalls { get; private set; }

            public override object Value => null;
            public override VariableType Type => VariableType.Generic;
            public override Type ObjectType => typeof(object);

            public override TTarget GetValue<TTarget>()
            {
                GetValueCalls++;
                return GetProbeValue<TTarget>();
            }

            public override void SetValue<T>(T value)
            {
            }
        }

        private static TTarget GetProbeValue<TTarget>()
        {
            object value = typeof(TTarget) switch
            {
                var type when type == typeof(string) => "string",
                var type when type == typeof(int) => 7,
                var type when type == typeof(float) => 2.5f,
                var type when type == typeof(bool) => true,
                var type when type == typeof(Vector2) => new Vector2(1f, 2f),
                var type when type == typeof(Vector3) => new Vector3(1f, 2f, 3f),
                var type when type == typeof(Vector4) => new Vector4(1f, 2f, 3f, 4f),
                var type when type == typeof(Color) => new Color(1f, 2f, 3f, 4f),
                _ => null,
            };

            return (TTarget)value;
        }
    }
}
