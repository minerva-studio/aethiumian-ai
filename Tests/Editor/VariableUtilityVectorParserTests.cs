using Aethiumian.AI.Variables;
using NUnit.Framework;
using System;
using System.Globalization;
using UnityEngine;

namespace Aethiumian.AI.Tests
{
    public sealed class VariableUtilityVectorParserTests
    {
        [Test]
        public void ParseVector2_AcceptsBareParenthesizedAndTypedFormats()
        {
            Assert.That((Vector2)VariableUtility.Parse(VariableType.Vector2, "1,2"), Is.EqualTo(new Vector2(1f, 2f)));
            Assert.That((Vector2)VariableUtility.Parse(VariableType.Vector2, "(1, 2)"), Is.EqualTo(new Vector2(1f, 2f)));
            Assert.That((Vector2)VariableUtility.Parse(VariableType.Vector2, "Vector2(1,2)"), Is.EqualTo(new Vector2(1f, 2f)));
        }

        [Test]
        public void ParseVector3_AcceptsCaseInsensitiveTypedFormat()
        {
            var result = (Vector3)VariableUtility.Parse(VariableType.Vector3, "vEcToR3(-1,2.5,3e2)");

            Assert.That(result, Is.EqualTo(new Vector3(-1f, 2.5f, 300f)));
        }

        [Test]
        public void ParseVector4_AcceptsIntegerAndFloatComponents()
        {
            var result = (Vector4)VariableUtility.Parse(VariableType.Vector4, "Vector4(1, -2, 3.5, 4e-1)");

            Assert.That(result, Is.EqualTo(new Vector4(1f, -2f, 3.5f, 0.4f)));
        }

        [Test]
        public void TryParseVector_UsesInvariantCulture()
        {
            CultureInfo previousCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

                Assert.True(VectorUtility.TryParse(VariableType.Vector2, "1.5,2.25", out object value));
                Assert.That((Vector2)value, Is.EqualTo(new Vector2(1.5f, 2.25f)));
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
            }
        }

        [TestCase(VariableType.Vector2, "")]
        [TestCase(VariableType.Vector2, "1")]
        [TestCase(VariableType.Vector2, "1,2,")]
        [TestCase(VariableType.Vector2, "1,,2")]
        [TestCase(VariableType.Vector2, "Vector2(1,2,3)")]
        [TestCase(VariableType.Vector2, "Vector2[1,2]")]
        [TestCase(VariableType.Vector2, "Vector2(abc,2)")]
        [TestCase(VariableType.Vector3, "Vector3(1,2)")]
        [TestCase(VariableType.Vector4, "(1,2,3)")]
        public void TryParseVector_ReturnsFalseForInvalidInput(VariableType type, string value)
        {
            Assert.DoesNotThrow(() =>
            {
                Assert.False(VectorUtility.TryParse(type, value, out object result));
                Assert.That(result, Is.Not.Null);
            });
        }

        [Test]
        public void ParseVector_ThrowsFormatExceptionForInvalidInput()
        {
            Assert.Throws<FormatException>(() => VariableUtility.Parse(VariableType.Vector3, "Vector3(1,2)"));
        }
    }
}
