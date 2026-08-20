using NUnit.Framework;
using System;
using UnityEngine;
using Aethiumian.AI.Variables;

namespace Aethiumian.AI.Editor.Tests.Variables
{
    public sealed class ImplicitConverterTests
    {
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
        public void RegistryRejectsRegistrationAfterBuiltInsAreFrozen()
        {
            _ = ImplicitConverter<int>.CanConvertFrom<int>();

            Assert.Throws<InvalidOperationException>(
                () => ConversionPair<CustomStruct, CustomClass>.Register(ConvertCustom));
        }

        [Test]
        public void RegistryRejectsDuplicateRegistration()
        {
            _ = ImplicitConverter<float>.CanConvertFrom<int>();

            Assert.Throws<InvalidOperationException>(
                () => ConversionPair<int, float>.Register(ConvertIntToFloat));
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

        private static bool ConvertCustom(CustomStruct source, out CustomClass result)
        {
            result = new CustomClass();
            return true;
        }

        private static bool ConvertIntToFloat(int source, out float result)
        {
            result = source;
            return true;
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
