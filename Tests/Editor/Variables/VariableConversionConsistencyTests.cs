using Aethiumian.AI.Nodes;
using Aethiumian.AI.Variables;
using NUnit.Framework;
using System;
using UnityEngine;

namespace Aethiumian.AI.Editor.Tests.Variables
{
    public sealed class VariableConversionConsistencyTests
    {
        [Test]
        public void IntProvidersShareTypedAndGenericConversionSemantics()
        {
            TreeVariable tree = CreateTreeVariable(VariableType.Int, 7);
            VariableField<int> fixedField = 7;
            VariableField dynamicField = CreateDynamicField(VariableType.Int, 7);
            VariableReference<int> fixedReference = Reference<int>(tree);
            VariableReference dynamicReference = Reference(tree);
            TargetScriptVariable targetScript = CreateTargetScriptVariable(new TargetScriptValues(), nameof(TargetScriptValues.IntValue));

            AssertProvider(fixedField.GetValue<int>(), fixedField.IntValue, 7);
            AssertProvider(fixedField.GetValue<bool>(), fixedField.BoolValue, true);
            Assert.That(fixedField.GetValue<float>(), Is.EqualTo(7f));
            Assert.That(fixedField.FloatValue, Is.EqualTo(7f));
            Assert.That((int)fixedField, Is.EqualTo(7));

            AssertProvider(dynamicField.GetValue<int>(), dynamicField.IntValue, 7);
            AssertProvider(dynamicField.GetValue<bool>(), dynamicField.BoolValue, true);
            AssertProvider(dynamicField.GetValue<float>(), dynamicField.FloatValue, 7f);
            AssertProvider(tree.GetValue<int>(), tree.IntValue, 7);
            AssertProvider(tree.GetValue<bool>(), tree.BoolValue, true);
            AssertProvider(tree.GetValue<float>(), tree.FloatValue, 7f);
            AssertProvider(fixedReference.GetValue<int>(), fixedReference.IntValue, 7);
            AssertProvider(fixedReference.GetValue<bool>(), fixedReference.BoolValue, true);
            AssertProvider(fixedReference.GetValue<float>(), fixedReference.FloatValue, 7f);
            AssertProvider(dynamicReference.GetValue<int>(), dynamicReference.IntValue, 7);
            AssertProvider(dynamicReference.GetValue<bool>(), dynamicReference.BoolValue, true);
            AssertProvider(dynamicReference.GetValue<float>(), dynamicReference.FloatValue, 7f);
            AssertProvider(targetScript.GetValue<int>(), targetScript.IntValue, 7);
            AssertProvider(targetScript.GetValue<bool>(), targetScript.BoolValue, true);
            AssertProvider(targetScript.GetValue<float>(), targetScript.FloatValue, 7f);
            Assert.That((int)fixedReference, Is.EqualTo(7));

            Assert.That(VariableUtility.ImplicitConversion<bool, int>(7), Is.True);
            Assert.That(VariableUtility.ImplicitConversion<float, int>(7), Is.EqualTo(7f));
            Assert.That(VariableUtility.ImplicitConversion<float>((object)7), Is.EqualTo(7f));
        }

        [Test]
        public void Vector4ProvidersShareColorAndWidthConversionSemantics()
        {
            Vector4 source = new(1f, 2f, 3f, 4f);
            Color expectedColor = new(1f, 2f, 3f, 4f);
            Vector2 expectedVector2 = new(1f, 2f);
            TreeVariable tree = CreateTreeVariable(VariableType.Vector4, source);
            VariableField<Vector4> fixedField = source;
            VariableField dynamicField = CreateDynamicField(VariableType.Vector4, source);
            VariableReference<Vector4> fixedReference = Reference<Vector4>(tree);
            VariableReference dynamicReference = Reference(tree);
            TargetScriptValues scriptValues = new() { Vector4Value = source };
            TargetScriptVariable targetScript = CreateTargetScriptVariable(scriptValues, nameof(TargetScriptValues.Vector4Value));

            Vector3 expectedVector3 = new(1f, 2f, 3f);
            AssertVectorProvider(fixedField, expectedColor, expectedVector2, expectedVector3, source);
            AssertVectorProvider(dynamicField, expectedColor, expectedVector2, expectedVector3, source);
            AssertVectorProvider(tree, expectedColor, expectedVector2, expectedVector3, source);
            AssertVectorProvider(fixedReference, expectedColor, expectedVector2, expectedVector3, source);
            AssertVectorProvider(dynamicReference, expectedColor, expectedVector2, expectedVector3, source);
            AssertVectorProvider(targetScript, expectedColor, expectedVector2, expectedVector3, source);

            Assert.That(VariableUtility.ImplicitConversion<Color, Vector4>(source), Is.EqualTo(expectedColor));
            Assert.That(VariableUtility.ImplicitConversion<Vector2, Vector4>(source), Is.EqualTo(expectedVector2));
            Assert.That(VariableUtility.ImplicitConversion<Vector3, Vector4>(source), Is.EqualTo(expectedVector3));
        }

        [Test]
        public void CustomStructProvidersPreserveIdentitySemantics()
        {
            CustomStruct source = new(17);
            TreeVariable tree = CreateTreeVariable(VariableType.Generic, source);
            VariableField<CustomStruct> fixedField = source;
            VariableReference<CustomStruct> fixedReference = Reference<CustomStruct>(tree);
            VariableReference dynamicReference = Reference(tree);
            TargetScriptValues scriptValues = new() { CustomValue = source };
            TargetScriptVariable targetScript = CreateTargetScriptVariable(scriptValues, nameof(TargetScriptValues.CustomValue));

            Assert.That(fixedField.GetValue<CustomStruct>(), Is.EqualTo(source));
            Assert.That(fixedField.Constant, Is.EqualTo(source));
            Assert.That(tree.GetValue<CustomStruct>(), Is.EqualTo(source));
            Assert.That(fixedReference.GetValue<CustomStruct>(), Is.EqualTo(source));
            Assert.That(dynamicReference.GetValue<CustomStruct>(), Is.EqualTo(source));
            Assert.That(targetScript.GetValue<CustomStruct>(), Is.EqualTo(source));
        }

        [Test]
        public void UnityObjectProvidersShareComponentConversionSemantics()
        {
            GameObject gameObject = new("VariableConversionConsistencyTestObject");
            TestComponent component = gameObject.AddComponent<TestComponent>();

            try
            {
                TreeVariable tree = CreateTreeVariable(VariableType.UnityObject, gameObject);
                VariableField<GameObject> fixedField = gameObject;
                VariableField dynamicField = CreateDynamicField(VariableType.UnityObject, gameObject);
                VariableReference<GameObject> fixedReference = Reference<GameObject>(tree);
                VariableReference dynamicReference = Reference(tree);
                TargetScriptValues scriptValues = new() { GameObjectValue = gameObject };
                TargetScriptVariable targetScript = CreateTargetScriptVariable(scriptValues, nameof(TargetScriptValues.GameObjectValue));

                AssertUnityProvider(fixedField, gameObject, component);
                AssertUnityProvider(dynamicField, gameObject, component);
                AssertUnityProvider(tree, gameObject, component);
                AssertUnityProvider(fixedReference, gameObject, component);
                AssertUnityProvider(dynamicReference, gameObject, component);
                AssertUnityProvider(targetScript, gameObject, component);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void TypedProviderReadsDoNotAllocateAfterWarmup()
        {
            TreeVariable tree = CreateTreeVariable(VariableType.Int, 7);
            VariableField<int> fixedField = 7;
            VariableField dynamicField = CreateDynamicField(VariableType.Int, 7);
            VariableReference<int> fixedReference = Reference<int>(tree);
            VariableReference dynamicReference = Reference(tree);

            _ = fixedField.GetValue<int>();
            _ = dynamicField.GetValue<int>();
            _ = tree.GetValue<int>();
            _ = fixedReference.GetValue<int>();
            _ = dynamicReference.GetValue<int>();

            long before = GC.GetAllocatedBytesForCurrentThread();
            int sink = 0;
            for (int i = 0; i < 1000; i++)
            {
                sink ^= fixedField.GetValue<int>();
                sink ^= dynamicField.GetValue<int>();
                sink ^= tree.GetValue<int>();
                sink ^= fixedReference.GetValue<int>();
                sink ^= dynamicReference.GetValue<int>();
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(sink, Is.EqualTo(0));
            Assert.That(allocated, Is.EqualTo(0));
        }

        [Test]
        public void ConversionProviderPathsDoNotAllocateAfterWarmup()
        {
            VariableField<int> fixedField = 7;
            VariableField dynamicField = CreateDynamicField(VariableType.Int, 7);
            TreeVariable tree = CreateTreeVariable(VariableType.Int, 7);
            VariableReference<int> fixedReference = Reference<int>(tree);
            VariableReference dynamicReference = Reference(tree);

            _ = ImplicitConverter<float>.From(7);
            _ = ImplicitConverter<Vector4>.From(7f);
            _ = ImplicitConverter<Vector2>.From(new Vector4(1f, 2f, 3f, 4f));
            _ = fixedField.GetValue<float>();
            _ = dynamicField.GetValue<float>();
            _ = tree.GetValue<float>();
            _ = fixedReference.GetValue<float>();
            _ = dynamicReference.GetValue<float>();

            long before = GC.GetAllocatedBytesForCurrentThread();
            float scalar = 0f;
            Vector4 vector = default;
            Vector2 truncated = default;
            for (int i = 0; i < 1000; i++)
            {
                scalar += ImplicitConverter<float>.From(i);
                vector += ImplicitConverter<Vector4>.From(scalar);
                truncated += ImplicitConverter<Vector2>.From(vector);
                scalar += fixedField.GetValue<float>();
                scalar += dynamicField.GetValue<float>();
                scalar += tree.GetValue<float>();
                scalar += fixedReference.GetValue<float>();
                scalar += dynamicReference.GetValue<float>();
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(scalar, Is.Not.EqualTo(0f));
            Assert.That(vector, Is.Not.EqualTo(Vector4.zero));
            Assert.That(truncated, Is.Not.EqualTo(Vector2.zero));
            Assert.That(allocated, Is.EqualTo(0));
        }

        [Test]
        public void ArithmeticAssignAndCopyPathsDoNotAllocateAfterWarmup()
        {
            VariableField left = CreateDynamicField(VariableType.Int, 7);
            VariableField right = CreateDynamicField(VariableType.Int, 3);
            VariableReference addResult = Reference(CreateTreeVariable(VariableType.Int, 0));
            VariableReference multiplyResult = Reference(CreateTreeVariable(VariableType.Int, 0));
            VariableReference assignResult = Reference(CreateTreeVariable(VariableType.Float, 0f));
            VariableReference copyResult = Reference(CreateTreeVariable(VariableType.Float, 0f));

            Add add = new() { a = left, b = right, result = addResult };
            Multiply multiply = new() { a = left, b = right, result = multiplyResult };
            Assign assign = new() { destination = assignResult, source = left };
            Copy copy = new() { from = left, to = copyResult };

            _ = add.Execute();
            _ = multiply.Execute();
            _ = assign.Execute();
            _ = copy.Execute();

            long before = GC.GetAllocatedBytesForCurrentThread();
            State addState = State.Failed;
            State multiplyState = State.Failed;
            State assignState = State.Failed;
            State copyState = State.Failed;
            for (int i = 0; i < 1000; i++)
            {
                addState = add.Execute();
                multiplyState = multiply.Execute();
                assignState = assign.Execute();
                copyState = copy.Execute();
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(addState, Is.EqualTo(State.Success));
            Assert.That(multiplyState, Is.EqualTo(State.Success));
            Assert.That(assignState, Is.EqualTo(State.Success));
            Assert.That(copyState, Is.EqualTo(State.Success));
            Assert.That(addResult.IntValue, Is.EqualTo(10));
            Assert.That(multiplyResult.IntValue, Is.EqualTo(21));
            Assert.That(assignResult.FloatValue, Is.EqualTo(7f));
            Assert.That(copyResult.FloatValue, Is.EqualTo(7f));
            Assert.That(allocated, Is.EqualTo(0));
        }

        [Test]
        public void TargetScriptAccessorUsesTypedPropertyMethodAndFieldPaths()
        {
            TargetScriptValues values = new()
            {
                IntField = 7,
                Vector4Field = new Vector4(1f, 2f, 3f, 4f),
                CustomField = new CustomStruct(17),
            };

            TargetScriptVariable intField = CreateTargetScriptVariable(values, nameof(TargetScriptValues.IntField));
            TargetScriptVariable intProperty = CreateTargetScriptVariable(values, nameof(TargetScriptValues.IntValue));
            TargetScriptVariable readMethod = CreateTargetScriptVariable(values, nameof(TargetScriptValues.ReadInt));
            TargetScriptVariable writeMethod = CreateTargetScriptVariable(values, nameof(TargetScriptValues.WriteInt));
            TargetScriptVariable vectorField = CreateTargetScriptVariable(values, nameof(TargetScriptValues.Vector4Field));
            TargetScriptVariable customField = CreateTargetScriptVariable(values, nameof(TargetScriptValues.CustomField));

            Assert.That(intField.GetValue<float>(), Is.EqualTo(7f));
            intField.SetValue(2.9f);
            Assert.That(values.IntField, Is.EqualTo(2));

            Assert.That(intProperty.GetValue<int>(), Is.EqualTo(7));
            intProperty.SetValue(3.9f);
            Assert.That(values.IntValue, Is.EqualTo(3));

            Assert.That(readMethod.GetValue<int>(), Is.EqualTo(values.IntField));
            writeMethod.SetValue(11);
            Assert.That(values.IntField, Is.EqualTo(11));

            Assert.That(vectorField.GetValue<Vector2>(), Is.EqualTo(new Vector2(1f, 2f)));
            Assert.That(customField.GetValue<CustomStruct>(), Is.EqualTo(new CustomStruct(17)));
        }

        [Test]
        public void TargetScriptAccessorPreservesRealEnumMaskIntegerVectorAndColorTypes()
        {
            TargetScriptValues values = new()
            {
                EnumField = TargetEnum.Two,
                LayerMaskField = (LayerMask)1088,
                Vector2IntField = new Vector2Int(2, 3),
                Vector3IntField = new Vector3Int(4, 5, 6),
                ColorField = new Color(1f, 2f, 3f, 4f),
            };

            TargetScriptVariable enumField = CreateTargetScriptVariable(values, nameof(TargetScriptValues.EnumField));
            TargetScriptVariable layerMaskField = CreateTargetScriptVariable(values, nameof(TargetScriptValues.LayerMaskField));
            TargetScriptVariable vector2IntField = CreateTargetScriptVariable(values, nameof(TargetScriptValues.Vector2IntField));
            TargetScriptVariable vector3IntField = CreateTargetScriptVariable(values, nameof(TargetScriptValues.Vector3IntField));
            TargetScriptVariable colorField = CreateTargetScriptVariable(values, nameof(TargetScriptValues.ColorField));

            Assert.That(enumField.GetValue<int>(), Is.EqualTo(2));
            enumField.SetValue(1);
            Assert.That(values.EnumField, Is.EqualTo(TargetEnum.One));

            Assert.That(layerMaskField.GetValue<int>(), Is.EqualTo(1088));
            layerMaskField.SetValue(2048);
            Assert.That(values.LayerMaskField.value, Is.EqualTo(2048));

            Assert.That(vector2IntField.GetValue<Vector3>(), Is.EqualTo(new Vector3(2f, 3f, 0f)));
            Assert.That(vector3IntField.GetValue<Vector2>(), Is.EqualTo(new Vector2(4f, 5f)));
            Assert.That(colorField.GetValue<Vector4>(), Is.EqualTo(new Vector4(1f, 2f, 3f, 4f)));
        }

        [Test]
        public void TargetScriptAccessorSupportsStaticAndReadOnlyMembers()
        {
            TargetScriptValues.StaticIntField = 13;
            TargetScriptValues values = new();

            try
            {
                TargetScriptVariable staticField = CreateTargetScriptVariable(values, nameof(TargetScriptValues.StaticIntField));
                TargetScriptVariable readOnly = CreateTargetScriptVariable(values, nameof(TargetScriptValues.ReadOnlyValue));

                Assert.That(staticField.GetValue<int>(), Is.EqualTo(13));
                staticField.SetValue(21);
                Assert.That(TargetScriptValues.StaticIntField, Is.EqualTo(21));

                Assert.That(readOnly.GetValue<int>(), Is.EqualTo(19));
                Assert.Throws<InvalidOperationException>(() => readOnly.SetValue(20));
            }
            finally
            {
                TargetScriptValues.StaticIntField = 0;
            }
        }

        [Test]
        public void TargetScriptTypedFieldAccessDoesNotAllocateAfterWarmup()
        {
            TargetScriptValues values = new()
            {
                IntField = 7,
                Vector4Field = new Vector4(1f, 2f, 3f, 4f),
            };
            TargetScriptVariable intField = CreateTargetScriptVariable(values, nameof(TargetScriptValues.IntField));
            TargetScriptVariable vectorField = CreateTargetScriptVariable(values, nameof(TargetScriptValues.Vector4Field));

            _ = intField.GetValue<int>();
            _ = intField.GetValue<float>();
            _ = vectorField.GetValue<Vector2>();
            intField.SetValue(8);
            vectorField.SetValue(new Vector4(2f, 3f, 4f, 5f));

            long before = GC.GetAllocatedBytesForCurrentThread();
            int integer = 0;
            Vector2 vector = default;
            for (int i = 0; i < 1000; i++)
            {
                integer += intField.GetValue<int>();
                integer += intField.GetValue<float>() > 0f ? 1 : 0;
                vector += vectorField.GetValue<Vector2>();
                intField.SetValue(i);
                vectorField.SetValue(new Vector4(i, i + 1f, i + 2f, i + 3f));
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(integer, Is.Not.EqualTo(0));
            Assert.That(vector, Is.Not.EqualTo(Vector2.zero));
            Assert.That(allocated, Is.EqualTo(0));
        }

        private static void AssertProvider<T>(T genericValue, T typedValue, T expected)
        {
            Assert.That(genericValue, Is.EqualTo(expected));
            Assert.That(typedValue, Is.EqualTo(expected));
        }

        private static void AssertVectorProvider(
            object provider,
            Color expectedColor,
            Vector2 expectedVector2,
            Vector3 expectedVector3,
            Vector4 expectedVector4)
        {
            switch (provider)
            {
                case VariableFieldBase field:
                    AssertProvider(field.GetValue<Color>(), field.ColorValue, expectedColor);
                    AssertProvider(field.GetValue<Vector2>(), field.Vector2Value, expectedVector2);
                    AssertProvider(field.GetValue<Vector3>(), field.Vector3Value, expectedVector3);
                    AssertProvider(field.GetValue<Vector4>(), field.Vector4Value, expectedVector4);
                    break;
                case RuntimeVariable variable:
                    AssertProvider(variable.GetValue<Color>(), variable.ColorValue, expectedColor);
                    AssertProvider(variable.GetValue<Vector2>(), variable.Vector2Value, expectedVector2);
                    AssertProvider(variable.GetValue<Vector3>(), variable.Vector3Value, expectedVector3);
                    AssertProvider(variable.GetValue<Vector4>(), variable.Vector4Value, expectedVector4);
                    break;
                default:
                    Assert.Fail($"Unsupported provider type: {provider.GetType().FullName}");
                    break;
            }
        }

        private static void AssertUnityProvider(object provider, GameObject expectedGameObject, TestComponent expectedComponent)
        {
            switch (provider)
            {
                case VariableFieldBase field:
                    Assert.That(field.GetValue<GameObject>(), Is.SameAs(expectedGameObject));
                    Assert.That(field.GetValue<TestComponent>(), Is.SameAs(expectedComponent));
                    break;
                case RuntimeVariable variable:
                    Assert.That(variable.GetValue<GameObject>(), Is.SameAs(expectedGameObject));
                    Assert.That(variable.GetValue<TestComponent>(), Is.SameAs(expectedComponent));
                    break;
                default:
                    Assert.Fail($"Unsupported provider type: {provider.GetType().FullName}");
                    break;
            }
        }

        private static VariableField CreateDynamicField(VariableType type, object value)
        {
            VariableField field = new(type);
            field.ForceSetConstantValue(value);
            return field;
        }

        private static TreeVariable CreateTreeVariable<T>(VariableType type, T value)
        {
            VariableData data = new("Consistency value", type);
            TreeVariable variable = new(data);
            variable.SetValue(value);
            return variable;
        }

        private static VariableReference<T> Reference<T>(TreeVariable variable)
        {
            VariableReference<T> reference = new();
            reference.SetRuntimeReference(variable);
            return reference;
        }

        private static VariableReference Reference(TreeVariable variable)
        {
            VariableReference reference = new();
            reference.SetRuntimeReference(variable);
            return reference;
        }

        private static VariableReference Reference<T>(VariableType type, T value)
        {
            return Reference(CreateTreeVariable(type, value));
        }

        private static TargetScriptVariable CreateTargetScriptVariable(object target, string memberName)
        {
            VariableData data = new("Target script value") { Path = memberName };
            return new TargetScriptVariable(data, target);
        }

        [Serializable]
        private readonly struct CustomStruct : IEquatable<CustomStruct>
        {
            private readonly int value;

            public CustomStruct(int value)
            {
                this.value = value;
            }

            public bool Equals(CustomStruct other) => value == other.value;
            public override bool Equals(object obj) => obj is CustomStruct other && Equals(other);
            public override int GetHashCode() => value;
        }

        private sealed class TargetScriptValues
        {
            public int IntField;
            public int IntValue { get; set; } = 7;
            public Vector4 Vector4Value { get; set; }
            public Vector4 Vector4Field;
            public CustomStruct CustomValue { get; set; }
            public CustomStruct CustomField;
            public TargetEnum EnumField;
            public LayerMask LayerMaskField;
            public Vector2Int Vector2IntField;
            public Vector3Int Vector3IntField;
            public Color ColorField;
            public int ReadOnlyValue => 19;
            public static int StaticIntField;
            public GameObject GameObjectValue { get; set; }

            public int ReadInt() => IntField;

            public void WriteInt(int value) => IntField = value;
        }

        private enum TargetEnum
        {
            One = 1,
            Two = 2,
        }

        private sealed class TestComponent : MonoBehaviour
        {
        }
    }
}
