using NUnit.Framework;
using System;
using UnityEngine;
using Aethiumian.AI.Variables;

namespace Aethiumian.AI.Editor.Tests.Variables
{
    public sealed class ImplicitConverterTests
    {
        [TestCase(typeof(int), typeof(float), true)]
        [TestCase(typeof(float), typeof(int), true)]
        [TestCase(typeof(string), typeof(float), false)]
        [TestCase(typeof(float), typeof(string), true)]
        [TestCase(typeof(float), typeof(Vector2), true)]
        [TestCase(typeof(Vector2), typeof(float), false)]
        [TestCase(typeof(LayerMask), typeof(int), true)]
        [TestCase(typeof(int), typeof(LayerMask), true)]
        [TestCase(typeof(TestEnum), typeof(int), true)]
        [TestCase(typeof(string), typeof(TestEnum), true)]
        [TestCase(typeof(GameObject), typeof(TestComponent), true)]
        [TestCase(typeof(CustomStruct), typeof(CustomStruct), true)]
        public void TypeQueriesReportRuntimeRuleSupport(Type source, Type target, bool supported)
        {
            Assert.That(ConversionRegistry.CanConvert(source, target), Is.EqualTo(supported));
            ConversionDefinition definition = ConversionRegistry.Find(source, target);
            Assert.That(definition != null, Is.EqualTo(supported));
            if (supported) Assert.That(definition.Matches(source, target), Is.True);
        }

        [TestCase(typeof(float), typeof(int), "Fractional")]
        [TestCase(typeof(int), typeof(float), "precision")]
        [TestCase(typeof(Vector3), typeof(Vector2), "Z discarded")]
        [TestCase(typeof(float), typeof(Vector2), "X and Y")]
        [TestCase(typeof(object), typeof(float), "Runtime type")]
        [TestCase(typeof(GameObject), typeof(TestComponent), "missing component")]
        [TestCase(typeof(CustomStruct), typeof(CustomStruct), "No conversion needed")]
        [TestCase(typeof(string), typeof(TestEnum), "invalid text")]
        [TestCase(typeof(TestEnum), typeof(int), "out-of-range")]
        public void MatchedDefinitionsExplainTheSelectedOperation(Type source, Type target, string explanation)
        {
            ConversionDefinition definition = ConversionRegistry.Find(source, target);
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.Describe(source, target), Does.Contain(explanation));
        }

        [TestCase(typeof(int), typeof(float), true, true)]
        [TestCase(typeof(string), typeof(float), false, true)]
        [TestCase(typeof(float), typeof(string), true, false)]
        [TestCase(typeof(float), typeof(Vector2), true, false)]
        [TestCase(typeof(Vector2), typeof(float), false, true)]
        public void AccessCompatibilityUsesTheRequestedDirections(Type variableType, Type fieldType, bool readable, bool writable)
        {
            Assert.That(VariableTypeCatalog.SupportsAccess(variableType, fieldType, VariableAccessFlag.Read), Is.EqualTo(readable));
            Assert.That(VariableTypeCatalog.SupportsAccess(variableType, fieldType, VariableAccessFlag.Write), Is.EqualTo(writable));
            Assert.That(VariableTypeCatalog.SupportsAccess(variableType, fieldType, VariableAccessFlag.All), Is.EqualTo(readable && writable));
            Assert.That(VariableTypeCatalog.SupportsAccess(variableType, fieldType), Is.EqualTo(readable && writable));
            Assert.That(VariableTypeCatalog.SupportsAccess(variableType, fieldType, VariableAccessFlag.None), Is.EqualTo(readable && writable));
        }

        [Test]
        public void RuleSupportDoesNotGuaranteeEveryDynamicValueConverts()
        {
            Assert.That(ConversionRegistry.CanConvert(typeof(object), typeof(float)), Is.True);
            Assert.That(ImplicitConverter<float>.TryFrom<object>(4, out float number), Is.True);
            Assert.That(number, Is.EqualTo(4f));
            Assert.That(ImplicitConverter<float>.TryFrom<object>("4", out _), Is.False);
            Assert.That(ConversionRegistry.CanConvert(typeof(string), typeof(TestEnum)), Is.True);
            Assert.That(ImplicitConverter<TestEnum>.TryFrom("missing-enum-value", out _), Is.False);
        }

        [Test]
        public void TypeQueriesRejectInvalidTypeArgumentsAndAccessFlags()
        {
            Assert.Throws<ArgumentNullException>(() => ConversionRegistry.CanConvert(null, typeof(float)));
            Assert.Throws<ArgumentNullException>(() => ConversionRegistry.CanConvert(typeof(float), null));
            Assert.Throws<ArgumentException>(() => ConversionRegistry.CanConvert(typeof(VariableField<>), typeof(float)));
            Assert.Throws<ArgumentException>(() => ConversionRegistry.CanConvert(typeof(void), typeof(float)));
            Assert.Throws<ArgumentException>(() => ConversionRegistry.CanConvert(typeof(int).MakeByRefType(), typeof(float)));
            Assert.Throws<ArgumentException>(() => ConversionRegistry.CanConvert(typeof(float), typeof(Span<int>)));
            Assert.Throws<ArgumentOutOfRangeException>(() => VariableTypeCatalog.SupportsAccess(typeof(float), typeof(float), (VariableAccessFlag)8));
        }

        [Test]
        public void CatalogListsDeriveFromTheRuntimeRulesAndPreserveControlCategories()
        {
            Assert.That(VariableTypeCatalog.GetCompatibleTypes(VariableType.Float), Is.EquivalentTo(new[] {
                VariableType.Int, VariableType.Float, VariableType.Bool, VariableType.UnityObject, VariableType.Generic }));
            Assert.That(VariableTypeCatalog.GetCompatibleTypes(VariableType.String), Does.Contain(VariableType.Float));
            Assert.That(VariableTypeCatalog.GetCompatibleTypes(VariableType.Node), Is.EqualTo(new[] { VariableType.Node }));
            Assert.That(VariableTypeCatalog.GetCompatibleTypes(VariableType.Invalid), Is.Empty);
            Assert.That(VariableTypeCatalog.IsCompatible(VariableType.Node, VariableType.String), Is.False);
            Assert.That(VariableTypeCatalog.IsCompatible(VariableType.Invalid, VariableType.Invalid), Is.False);
        }

        [Test]
        public void RepeatedTypeQueriesAndCategoryListsDoNotAllocate()
        {
            _ = ConversionRegistry.CanConvert(typeof(int), typeof(float));
            _ = VariableTypeCatalog.GetCompatibleTypes(VariableType.Float);
            long before = GC.GetAllocatedBytesForCurrentThread();
            bool supported = true;
            for (int i = 0; i < 1000; i++)
            {
                supported &= ConversionRegistry.CanConvert(typeof(int), typeof(float));
                _ = VariableTypeCatalog.GetCompatibleTypes(VariableType.Float);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(supported, Is.True);
            Assert.That(allocated, Is.EqualTo(0));
        }

        [Test]
        public void StructuralValueOperationsShareQueryAndExecutionRules()
        {
            AssertConversion(2f, "2");
            AssertConversion(2f, true);
            AssertConversion(new Vector2(0f, 1f), true);
            AssertConversion(new Vector4(1.9f, 2.1f, 3.8f, 4.2f), new Rect(1.9f, 2.1f, 3.8f, 4.2f));
            AssertConversion(new Vector4(1.9f, 2.1f, 3.8f, 4.2f), new RectInt(1, 2, 3, 4));
            AssertConversion(TestEnum.Two, 2);
            AssertConversion("Two", TestEnum.Two);
            AssertConversion<object, float>(4, 4f);
            CustomStruct value = new(17);
            AssertConversion(value, value);
            CustomClass instance = new();
            AssertConversion<CustomClass, object>(instance, instance);
        }

        [Test]
        public void UnityValueOperationsShareQueryAndExecutionRules()
        {
            GameObject gameObject = new("ConversionRegistryTest");
            try
            {
                TestComponent component = gameObject.AddComponent<TestComponent>();
                AssertConversion(gameObject, true);
                AssertConversion(gameObject, 1);
                AssertConversion(gameObject, 1f);
                AssertConversion(gameObject, Vector2.one);
                AssertConversion<GameObject, TestComponent>(gameObject, component);
                AssertConversion<TestComponent, GameObject>(component, gameObject);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void CatalogCompatibilityFollowsRuntimeConversionDirections()
        {
            Assert.That(VariableTypeCatalog.IsCompatible(VariableType.String, VariableType.Float), Is.False);
            Assert.That(VariableTypeCatalog.IsCompatible(VariableType.Float, VariableType.String), Is.True);
            Assert.That(VariableTypeCatalog.IsCompatible(VariableType.Vector2, VariableType.Float), Is.False);
            Assert.That(VariableTypeCatalog.IsCompatible(VariableType.Float, VariableType.Vector2), Is.True);
        }

        [Test]
        public void BuiltInNumericAndVectorEdgesUseTheRegisteredRules()
        {
            Assert.That(ImplicitConverter<float>.CanConvertFrom<int>(), Is.True);
            Assert.That(ImplicitConverter<float>.From(7), Is.EqualTo(7f));
            Assert.That(ImplicitConverter<int>.From(2.9f), Is.EqualTo(2));
            Assert.That(ImplicitConverter<Vector4>.From(2f), Is.EqualTo(new Vector4(2f, 2f, 2f, 2f)));
            Assert.That(ImplicitConverter<Vector2>.From(new Vector4(1f, 2f, 3f, 4f)), Is.EqualTo(new Vector2(1f, 2f)));
            Assert.That(ImplicitConverter<Color>.From(new Vector4(1f, 2f, 3f, 4f)), Is.EqualTo(new Color(1f, 2f, 3f, 4f)));
            Assert.That(ImplicitConverter<int>.From((LayerMask)1088), Is.EqualTo(1088));
        }

        [Test]
        public void BoolAndColorRulesPreserveTheirDocumentedSemantics()
        {
            Assert.That(ImplicitConverter<Vector3>.From(false), Is.EqualTo(Vector3.zero));
            Assert.That(ImplicitConverter<Vector3>.From(true), Is.EqualTo(Vector3.one));
            Assert.That(ImplicitConverter<bool>.From(new Vector2(0f, 1f)), Is.True);
            Assert.That(ImplicitConverter<Vector3>.From(new Color(1f, 2f, 3f, 4f)), Is.EqualTo(new Vector3(1f, 2f, 3f)));
        }

        [Test]
        public void BooleanScalarAndVectorRulesPreserveZeroComparison()
        {
            Assert.That(ImplicitConverter<bool>.From(0), Is.False);
            Assert.That(ImplicitConverter<bool>.From(-1), Is.True);
            Assert.That(ImplicitConverter<bool>.From(0f), Is.False);
            Assert.That(ImplicitConverter<bool>.From(-1f), Is.True);
            Assert.That(ImplicitConverter<bool>.From(float.NaN), Is.True);
            Assert.That(ImplicitConverter<bool>.From(Vector2.zero), Is.False);
            Assert.That(ImplicitConverter<bool>.From(new Vector2(0.000001f, 0f)), Is.False);
            Assert.That(ImplicitConverter<bool>.From(Vector2.one), Is.True);
            Assert.That(ImplicitConverter<bool>.From(Vector3.zero), Is.False);
            Assert.That(ImplicitConverter<bool>.From(Vector3.one), Is.True);
            Assert.That(ImplicitConverter<bool>.From(Vector4.zero), Is.False);
            Assert.That(ImplicitConverter<bool>.From(Vector4.one), Is.True);
        }

        [Test]
        public void BooleanUnityObjectRulesObserveLifetimeAfterBinding()
        {
            Assert.That(ImplicitConverter<bool>.From<GameObject>(null), Is.False);
            Assert.That(ImplicitConverter<bool>.From<TestComponent>(null), Is.False);
            GameObject gameObject = new("BooleanConversionLifetimeTest");
            TestComponent component = gameObject.AddComponent<TestComponent>();
            try
            {
                Assert.That(ImplicitConverter<bool>.From(gameObject), Is.True);
                Assert.That(ImplicitConverter<bool>.From(component), Is.True);
                UnityEngine.Object.DestroyImmediate(gameObject);
                Assert.That(ImplicitConverter<bool>.From(gameObject), Is.False);
                Assert.That(ImplicitConverter<bool>.From(component), Is.False);
            }
            finally
            {
                if (gameObject != null) UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void StructuralRulesCoverIdentityReferencesEnumsAndObjectBoundaries()
        {
            CustomStruct value = new(11);
            CustomClass instance = new();

            Assert.That(ImplicitConverter<CustomStruct>.CanConvertFrom<CustomStruct>(), Is.True);
            Assert.That(ImplicitConverter<CustomStruct>.From(value), Is.EqualTo(value));
            Assert.That(ImplicitConverter<object>.From(instance), Is.SameAs(instance));
            Assert.That(ImplicitConverter<TestEnum>.From(2), Is.EqualTo(TestEnum.Two));
            Assert.That(ImplicitConverter<int>.From(TestEnum.Two), Is.EqualTo(2));
            Assert.That(ImplicitConverter<byte>.CanConvertFrom<ByteEnum>(), Is.True);
            Assert.That(ImplicitConverter<byte>.From(ByteEnum.Three), Is.EqualTo(3));
            Assert.That(ImplicitConverter<ByteEnum>.From((byte)3), Is.EqualTo(ByteEnum.Three));

            object boxed = 9;
            Assert.That(ImplicitConverter<float>.From(boxed), Is.EqualTo(9f));
        }

        [Test]
        public void UnsupportedPairsAreRejectedWithoutCallingFrom()
        {
            Assert.That(ImplicitConverter<bool>.CanConvertFrom<string>(), Is.False);

            bool converted = ImplicitConverter<bool>.TryFrom("true", out _);

            Assert.That(converted, Is.False);
            Assert.Throws<InvalidCastException>(() => ImplicitConverter<bool>.From("true"));
        }

        [Test]
        public void UnityObjectConversionDistinguishesMissingComponentsFromUnsupportedPairs()
        {
            GameObject gameObject = new("ImplicitConverterTestObject");
            TestComponent component = gameObject.AddComponent<TestComponent>();

            try
            {
                Assert.That(ImplicitConverter<TestComponent>.CanConvertFrom<GameObject>(), Is.True);
                Assert.That(ImplicitConverter<TestComponent>.From(gameObject), Is.SameAs(component));
                Assert.That(ImplicitConverter<BoxCollider>.CanConvertFrom<GameObject>(), Is.True);
                Assert.That(ImplicitConverter<BoxCollider>.TryFrom(gameObject, out _), Is.False);
                Assert.That(ImplicitConverter<bool>.From(gameObject), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void WarmedUpValueConversionsDoNotAllocate()
        {
            _ = ImplicitConverter<float>.From(1);
            _ = ImplicitConverter<Vector4>.From(1f);
            _ = ImplicitConverter<Vector2>.From(Vector4.one);
            CustomStruct value = new(11);
            _ = ImplicitConverter<CustomStruct>.From(value);

            long before = GC.GetAllocatedBytesForCurrentThread();
            float scalar = 0f;
            Vector4 vector = default;
            Vector2 truncated = default;
            CustomStruct identity = default;
            for (int i = 0; i < 1000; i++)
            {
                scalar += ImplicitConverter<float>.From(i);
                vector += ImplicitConverter<Vector4>.From(scalar);
                truncated += ImplicitConverter<Vector2>.From(vector);
                identity = ImplicitConverter<CustomStruct>.From(value);
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(scalar, Is.Not.EqualTo(0f));
            Assert.That(vector, Is.Not.EqualTo(Vector4.zero));
            Assert.That(truncated, Is.Not.EqualTo(Vector2.zero));
            Assert.That(identity, Is.EqualTo(value));
            Assert.That(allocated, Is.EqualTo(0));
        }

        [Test]
        public void UnsupportedPairQueriesDoNotAllocateARejectionDelegate()
        {
            _ = ImplicitConverter<bool>.CanConvertFrom<string>();

            long before = GC.GetAllocatedBytesForCurrentThread();
            bool supported = true;
            for (int i = 0; i < 1000; i++)
            {
                supported &= ImplicitConverter<bool>.CanConvertFrom<string>();
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(supported, Is.False);
            Assert.That(allocated, Is.EqualTo(0));
        }

        /// <summary>Checks a declared conversion against both the type query and its executable generic operation.</summary>
        private static void AssertConversion<TSource, TTarget>(TSource source, TTarget expected)
        {
            ConversionDefinition definition = ConversionRegistry.Find(typeof(TSource), typeof(TTarget));
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.Matches(typeof(TSource), typeof(TTarget)), Is.True);
            Assert.That(definition.Describe(typeof(TSource), typeof(TTarget)), Is.Not.Empty);
            Assert.That(ImplicitConverter<TTarget>.CanConvertFrom<TSource>(), Is.True);
            Assert.That(ImplicitConverter<TTarget>.TryFrom(source, out TTarget result), Is.True);
            Assert.That(result, Is.EqualTo(expected));
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

        private sealed class CustomClass
        {
        }

        private enum TestEnum
        {
            Zero,
            Two = 2,
        }

        private enum ByteEnum : byte
        {
            Three = 3,
        }

        private sealed class TestComponent : MonoBehaviour
        {
        }
    }
}
